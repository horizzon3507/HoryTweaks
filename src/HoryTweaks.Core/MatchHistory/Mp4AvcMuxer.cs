using System.Buffers.Binary;
using System.Text;

namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Pure-C# streaming ISO BMFF (.mp4) muxer for H.264/AVC1 Annex-B video streams.
/// Writes frames sequentially to <c>mdat</c> and appends a spec-compliant <c>moov</c> box on <see cref="Finish"/>.
/// </summary>
internal sealed class Mp4AvcMuxer : IDisposable
{
    private const uint Timescale = 90_000;

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly int _width;
    private readonly int _height;
    private readonly int _fps;
    private readonly long _mdatHeaderOffset;
    private readonly List<SampleEntry> _samples = [];

    private readonly List<byte[]> _spsList = [];
    private readonly List<byte[]> _ppsList = [];
    private bool _finished;
    private bool _disposed;

    internal int SampleCount => _samples.Count;

    internal Mp4AvcMuxer(Stream stream, int width, int height, int fps, bool leaveOpen = false)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite || !stream.CanSeek)
            throw new ArgumentException("MP4 muxer requires a writable, seekable stream.", nameof(stream));
        if (width <= 0 || height <= 0 || fps <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensions and FPS must be positive.");

        _stream = stream;
        _leaveOpen = leaveOpen;
        _width = width;
        _height = height;
        _fps = fps;

        WriteFtyp();

        // Reserve 16-byte 64-bit mdat header: [size=1 (4)] ["mdat" (4)] [largesize (8)].
        _mdatHeaderOffset = _stream.Position;
        Span<byte> header = stackalloc byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(header[0..4], 1u);
        header[4] = (byte)'m';
        header[5] = (byte)'d';
        header[6] = (byte)'a';
        header[7] = (byte)'t';
        BinaryPrimitives.WriteUInt64BigEndian(header[8..16], 16ul);
        _stream.Write(header);
    }

    /// <summary>
    /// Parses one H.264 Annex-B access unit (one encoded video frame), captures SPS/PPS parameter sets,
    /// and writes AVCC length-prefixed NAL units into <c>mdat</c>.
    /// </summary>
    internal void AddAnnexBFrame(ReadOnlySpan<byte> annexB, long timestampMs, bool isKeyFrameHint)
    {
        if (_disposed || _finished)
            throw new InvalidOperationException("Cannot write frames after Finish or Dispose.");
        if (annexB.IsEmpty)
            return;

        long sampleOffset = _stream.Position;
        int sampleBytes = 0;
        bool hasIdr = false;
        Span<byte> lenPrefix = stackalloc byte[4];

        int pos = 0;
        while (TryFindStartCode(annexB, pos, out int nalStart, out int nextStart))
        {
            var nal = annexB[nalStart..nextStart];
            pos = nextStart;

            if (nal.IsEmpty)
                continue;

            int nalType = nal[0] & 0x1F;
            if (nalType == 7)
            {
                AddUniqueParamSet(_spsList, nal, maxCount: 31);
                continue;
            }

            if (nalType == 8)
            {
                AddUniqueParamSet(_ppsList, nal, maxCount: 255);
                continue;
            }

            // Skip Access Unit Delimiters (type 9) — unnecessary inside MP4 AVCC samples.
            if (nalType == 9)
                continue;

            if (nalType == 5)
            {
                hasIdr = true;
            }

            BinaryPrimitives.WriteUInt32BigEndian(lenPrefix, (uint)nal.Length);
            _stream.Write(lenPrefix);
            _stream.Write(nal);
            sampleBytes += 4 + nal.Length;
        }

        if (sampleBytes > 0)
        {
            _samples.Add(new SampleEntry(sampleOffset, sampleBytes, timestampMs, hasIdr || isKeyFrameHint));
        }
    }

    /// <summary>
    /// Patches the <c>mdat</c> size header and writes the <c>moov</c> metadata box.
    /// Returns false if no valid H.264 samples or parameter sets were recorded.
    /// </summary>
    internal bool Finish()
    {
        if (_disposed || _finished)
            return false;

        _finished = true;
        if (_samples.Count == 0 || _spsList.Count == 0 || _spsList[0].Length < 4 || _ppsList.Count == 0)
            return false;

        long mdatEnd = _stream.Position;
        ulong mdatSize = (ulong)(mdatEnd - _mdatHeaderOffset);

        _stream.Seek(_mdatHeaderOffset + 8, SeekOrigin.Begin);
        Span<byte> sizeBuf = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(sizeBuf, mdatSize);
        _stream.Write(sizeBuf);

        _stream.Seek(mdatEnd, SeekOrigin.Begin);
        byte[] moov = BuildMoov();
        _stream.Write(moov, 0, moov.Length);
        _stream.Flush();
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    private void WriteFtyp()
    {
        using var body = new MemoryStream(32);
        WriteAscii(body, "isom");
        WriteUInt32(body, 512);
        WriteAscii(body, "isom");
        WriteAscii(body, "iso2");
        WriteAscii(body, "avc1");
        WriteAscii(body, "mp41");
        byte[] box = WrapBox("ftyp", body.ToArray());
        _stream.Write(box, 0, box.Length);
    }

    private byte[] BuildMoov()
    {
        uint defaultDelta = Timescale / (uint)Math.Max(_fps, 1);
        uint[] deltas = new uint[_samples.Count];
        ulong totalDuration = 0;

        for (int i = 0; i < _samples.Count; i++)
        {
            uint delta = defaultDelta;
            if (i + 1 < _samples.Count)
            {
                long diffMs = _samples[i + 1].TimestampMs - _samples[i].TimestampMs;
                if (diffMs > 0)
                {
                    long ticks = (diffMs * Timescale) / 1000L;
                    // Clamp extreme pauses (e.g. debugger freeze) to 2 seconds so players never glitch.
                    delta = (uint)Math.Clamp(ticks, 1L, Timescale * 2L);
                }
            }

            deltas[i] = delta;
            totalDuration += delta;
        }

        byte[] mvhd = BuildMvhd(totalDuration);
        byte[] trak = BuildTrak(totalDuration, deltas);
        return WrapBox("moov", Concat(mvhd, trak));
    }

    private static byte[] BuildMvhd(ulong duration)
    {
        using var ms = new MemoryStream(128);
        ms.WriteByte(1); // version 1 (64-bit times/duration)
        ms.Write(new byte[3]); // flags
        WriteUInt64(ms, 0); // creation_time
        WriteUInt64(ms, 0); // modification_time
        WriteUInt32(ms, Timescale);
        WriteUInt64(ms, duration);
        WriteUInt32(ms, 0x00010000); // rate = 1.0
        WriteUInt16(ms, 0x0100);     // volume = 1.0
        ms.Write(new byte[10]);      // reserved
        WriteIdentityMatrix(ms);
        ms.Write(new byte[24]);      // pre_defined
        WriteUInt32(ms, 2);          // next_track_ID
        return WrapBox("mvhd", ms.ToArray());
    }

    private byte[] BuildTrak(ulong duration, uint[] deltas)
    {
        byte[] tkhd = BuildTkhd(duration);
        byte[] mdia = BuildMdia(duration, deltas);
        return WrapBox("trak", Concat(tkhd, mdia));
    }

    private byte[] BuildTkhd(ulong duration)
    {
        using var ms = new MemoryStream(112);
        ms.WriteByte(1); // version 1
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0x03); // track_enabled | track_in_movie
        WriteUInt64(ms, 0); // creation_time
        WriteUInt64(ms, 0); // modification_time
        WriteUInt32(ms, 1); // track_ID
        WriteUInt32(ms, 0); // reserved
        WriteUInt64(ms, duration);
        ms.Write(new byte[8]); // reserved
        WriteUInt16(ms, 0);    // layer
        WriteUInt16(ms, 0);    // alternate_group
        WriteUInt16(ms, 0);    // volume (0 for video)
        WriteUInt16(ms, 0);    // reserved
        WriteIdentityMatrix(ms);
        WriteUInt32(ms, (uint)_width << 16);
        WriteUInt32(ms, (uint)_height << 16);
        return WrapBox("tkhd", ms.ToArray());
    }

    private byte[] BuildMdia(ulong duration, uint[] deltas)
    {
        byte[] mdhd = BuildMdhd(duration);
        byte[] hdlr = BuildHdlr();
        byte[] minf = BuildMinf(deltas);
        return WrapBox("mdia", Concat(mdhd, hdlr, minf));
    }

    private static byte[] BuildMdhd(ulong duration)
    {
        using var ms = new MemoryStream(48);
        ms.WriteByte(1); // version 1
        ms.Write(new byte[3]); // flags
        WriteUInt64(ms, 0); // creation_time
        WriteUInt64(ms, 0); // modification_time
        WriteUInt32(ms, Timescale);
        WriteUInt64(ms, duration);
        WriteUInt16(ms, 0x55C4); // "und" language
        WriteUInt16(ms, 0);      // pre_defined
        return WrapBox("mdhd", ms.ToArray());
    }

    private static byte[] BuildHdlr()
    {
        using var ms = new MemoryStream(48);
        WriteUInt32(ms, 0); // version + flags
        WriteUInt32(ms, 0); // pre_defined
        WriteAscii(ms, "vide");
        ms.Write(new byte[12]); // reserved
        byte[] name = Encoding.UTF8.GetBytes("VideoHandler\0");
        ms.Write(name, 0, name.Length);
        return WrapBox("hdlr", ms.ToArray());
    }

    private byte[] BuildMinf(uint[] deltas)
    {
        byte[] vmhd = BuildVmhd();
        byte[] dinf = BuildDinf();
        byte[] stbl = BuildStbl(deltas);
        return WrapBox("minf", Concat(vmhd, dinf, stbl));
    }

    private static byte[] BuildVmhd()
    {
        using var ms = new MemoryStream(16);
        WriteUInt32(ms, 1); // version 0, flags = 1
        ms.Write(new byte[8]); // graphicsmode + opcolor
        return WrapBox("vmhd", ms.ToArray());
    }

    private static byte[] BuildDinf()
    {
        using var urlBody = new MemoryStream(4);
        WriteUInt32(urlBody, 1); // self-contained media flag
        byte[] urlBox = WrapBox("url ", urlBody.ToArray());

        using var drefBody = new MemoryStream(16);
        WriteUInt32(drefBody, 0); // version + flags
        WriteUInt32(drefBody, 1); // entry_count
        drefBody.Write(urlBox, 0, urlBox.Length);
        byte[] drefBox = WrapBox("dref", drefBody.ToArray());

        return WrapBox("dinf", drefBox);
    }

    private byte[] BuildStbl(uint[] deltas)
    {
        byte[] stsd = BuildStsd();
        byte[] stts = BuildStts(deltas);
        byte[] stss = BuildStss();
        byte[] stsc = BuildStsc();
        byte[] stsz = BuildStsz();
        byte[] co64 = BuildCo64();
        return WrapBox("stbl", Concat(stsd, stts, stss, stsc, stsz, co64));
    }

    private byte[] BuildStsd()
    {
        byte[] avcC = BuildAvcC(_spsList, _ppsList);
        using var avc1Body = new MemoryStream(96 + avcC.Length);
        avc1Body.Write(new byte[6]); // reserved
        WriteUInt16(avc1Body, 1);    // data_reference_index
        avc1Body.Write(new byte[16]); // pre_defined + reserved
        WriteUInt16(avc1Body, (ushort)_width);
        WriteUInt16(avc1Body, (ushort)_height);
        WriteUInt32(avc1Body, 0x00480000); // horizresolution 72 dpi
        WriteUInt32(avc1Body, 0x00480000); // vertresolution 72 dpi
        WriteUInt32(avc1Body, 0);          // reserved
        WriteUInt16(avc1Body, 1);          // frame_count
        avc1Body.Write(new byte[32]);      // compressorname
        WriteUInt16(avc1Body, 0x0018);     // depth = 24-bit color
        WriteUInt16(avc1Body, 0xFFFF);     // pre_defined = -1
        avc1Body.Write(avcC, 0, avcC.Length);
        byte[] avc1Box = WrapBox("avc1", avc1Body.ToArray());

        using var stsdBody = new MemoryStream(16 + avc1Box.Length);
        WriteUInt32(stsdBody, 0); // version + flags
        WriteUInt32(stsdBody, 1); // entry_count
        stsdBody.Write(avc1Box, 0, avc1Box.Length);
        return WrapBox("stsd", stsdBody.ToArray());
    }

    private static void AddUniqueParamSet(List<byte[]> target, ReadOnlySpan<byte> nal, int maxCount)
    {
        if (target.Count >= maxCount)
            return;

        foreach (byte[] existing in target)
        {
            if (existing.AsSpan().SequenceEqual(nal))
                return;
        }

        target.Add(nal.ToArray());
    }

    private static byte[] BuildAvcC(List<byte[]> spsList, List<byte[]> ppsList)
    {
        byte[] primarySps = spsList[0];
        using var ms = new MemoryStream(64);
        ms.WriteByte(1);             // configurationVersion
        ms.WriteByte(primarySps[1]); // AVCProfileIndication
        ms.WriteByte(primarySps[2]); // profile_compatibility
        ms.WriteByte(primarySps[3]); // AVCLevelIndication
        ms.WriteByte(0xFF);          // lengthSizeMinusOne = 3 (4-byte NAL length prefix)
        ms.WriteByte((byte)(0xE0 | (spsList.Count & 0x1F)));
        foreach (byte[] sps in spsList)
        {
            WriteUInt16(ms, (ushort)sps.Length);
            ms.Write(sps, 0, sps.Length);
        }
        ms.WriteByte((byte)(ppsList.Count & 0xFF));
        foreach (byte[] pps in ppsList)
        {
            WriteUInt16(ms, (ushort)pps.Length);
            ms.Write(pps, 0, pps.Length);
        }
        return WrapBox("avcC", ms.ToArray());
    }

    private static byte[] BuildStts(uint[] deltas)
    {
        var runs = new List<(uint Count, uint Delta)>();
        foreach (uint delta in deltas)
        {
            if (runs.Count > 0 && runs[^1].Delta == delta)
            {
                runs[^1] = (runs[^1].Count + 1, delta);
            }
            else
            {
                runs.Add((1, delta));
            }
        }

        using var ms = new MemoryStream(8 + runs.Count * 8);
        WriteUInt32(ms, 0);
        WriteUInt32(ms, (uint)runs.Count);
        foreach (var (count, delta) in runs)
        {
            WriteUInt32(ms, count);
            WriteUInt32(ms, delta);
        }
        return WrapBox("stts", ms.ToArray());
    }

    private byte[] BuildStss()
    {
        var keyframes = new List<uint>();
        for (int i = 0; i < _samples.Count; i++)
        {
            if (_samples[i].IsKeyFrame || i == 0)
            {
                keyframes.Add((uint)(i + 1)); // 1-based sample number
            }
        }

        using var ms = new MemoryStream(8 + keyframes.Count * 4);
        WriteUInt32(ms, 0);
        WriteUInt32(ms, (uint)keyframes.Count);
        foreach (uint index in keyframes)
        {
            WriteUInt32(ms, index);
        }
        return WrapBox("stss", ms.ToArray());
    }

    private static byte[] BuildStsc()
    {
        // Every sample is stored as its own 1-sample chunk (simple, zero buffering, exact co64 offsets).
        using var ms = new MemoryStream(20);
        WriteUInt32(ms, 0);
        WriteUInt32(ms, 1); // entry_count
        WriteUInt32(ms, 1); // first_chunk
        WriteUInt32(ms, 1); // samples_per_chunk
        WriteUInt32(ms, 1); // sample_description_index
        return WrapBox("stsc", ms.ToArray());
    }

    private byte[] BuildStsz()
    {
        using var ms = new MemoryStream(12 + _samples.Count * 4);
        WriteUInt32(ms, 0);
        WriteUInt32(ms, 0); // variable sample size
        WriteUInt32(ms, (uint)_samples.Count);
        foreach (var sample in _samples)
        {
            WriteUInt32(ms, (uint)sample.Size);
        }
        return WrapBox("stsz", ms.ToArray());
    }

    private byte[] BuildCo64()
    {
        using var ms = new MemoryStream(8 + _samples.Count * 8);
        WriteUInt32(ms, 0);
        WriteUInt32(ms, (uint)_samples.Count);
        foreach (var sample in _samples)
        {
            WriteUInt64(ms, (ulong)sample.Offset);
        }
        return WrapBox("co64", ms.ToArray());
    }

    private static bool TryFindStartCode(ReadOnlySpan<byte> data, int from, out int nalStart, out int nalEnd)
    {
        nalStart = -1;
        nalEnd = data.Length;

        int i = from;
        while (i + 2 < data.Length)
        {
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1)
                {
                    nalStart = i + 3;
                    break;
                }
                if (i + 3 < data.Length && data[i + 2] == 0 && data[i + 3] == 1)
                {
                    nalStart = i + 4;
                    break;
                }
            }
            i++;
        }

        if (nalStart < 0 || nalStart >= data.Length)
            return false;

        int j = nalStart;
        while (j + 2 < data.Length)
        {
            if (data[j] == 0 && data[j + 1] == 0 && (data[j + 2] == 1 || (j + 3 < data.Length && data[j + 2] == 0 && data[j + 3] == 1)))
            {
                nalEnd = j;
                return true;
            }
            j++;
        }

        nalEnd = data.Length;
        return true;
    }

    private static byte[] WrapBox(string type, byte[] payload)
    {
        byte[] result = new byte[8 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(0, 4), (uint)result.Length);
        result[4] = (byte)type[0];
        result[5] = (byte)type[1];
        result[6] = (byte)type[2];
        result[7] = (byte)type[3];
        Buffer.BlockCopy(payload, 0, result, 8, payload.Length);
        return result;
    }

    private static byte[] Concat(params byte[][] chunks)
    {
        int total = 0;
        foreach (var c in chunks)
            total += c.Length;

        byte[] result = new byte[total];
        int offset = 0;
        foreach (var c in chunks)
        {
            Buffer.BlockCopy(c, 0, result, offset, c.Length);
            offset += c.Length;
        }
        return result;
    }

    private static void WriteIdentityMatrix(Stream s)
    {
        WriteUInt32(s, 0x00010000);
        WriteUInt32(s, 0);
        WriteUInt32(s, 0);
        WriteUInt32(s, 0);
        WriteUInt32(s, 0x00010000);
        WriteUInt32(s, 0);
        WriteUInt32(s, 0);
        WriteUInt32(s, 0);
        WriteUInt32(s, 0x40000000);
    }

    private static void WriteAscii(Stream s, string value)
    {
        for (int i = 0; i < value.Length; i++)
            s.WriteByte((byte)value[i]);
    }

    private static void WriteUInt16(Stream s, ushort value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buf, value);
        s.Write(buf);
    }

    private static void WriteUInt32(Stream s, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        s.Write(buf);
    }

    private static void WriteUInt64(Stream s, ulong value)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buf, value);
        s.Write(buf);
    }

    private readonly record struct SampleEntry(long Offset, int Size, long TimestampMs, bool IsKeyFrame);
}
