using System.Buffers.Binary;
using HoryTweaks.Core.Audio;
using Xunit;

namespace HoryTweaks.Tests.Audio;

public class WavDecoderTests
{
    private static void Put(List<byte> bytes, string id)
        => bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(id));

    private static void Put(List<byte> bytes, int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        bytes.AddRange(buf.ToArray());
    }

    private static void Put(List<byte> bytes, ushort value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, value);
        bytes.AddRange(buf.ToArray());
    }

    private static List<byte> Chunk(string id, byte[] payload)
    {
        var chunk = new List<byte>();
        Put(chunk, id);
        Put(chunk, payload.Length);
        chunk.AddRange(payload);
        if ((payload.Length & 1) != 0)
        {
            chunk.Add(0); // word-alignment pad byte
        }
        return chunk;
    }

    private static byte[] BuildWav(
        short[] samples,
        int channels = 1,
        int sampleRate = 44100,
        ushort audioFormat = 1,
        int bitsPerSample = 16,
        int? blockAlign = null,
        byte[]? extraChunkBeforeData = null,
        bool omitFmt = false,
        bool omitData = false)
    {
        var fmt = new List<byte>();
        Put(fmt, audioFormat);
        Put(fmt, (ushort)channels);
        Put(fmt, sampleRate);
        Put(fmt, sampleRate * channels * (bitsPerSample / 8)); // byte rate
        Put(fmt, (ushort)(blockAlign ?? channels * (bitsPerSample / 8)));
        Put(fmt, (ushort)bitsPerSample);

        var data = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i * 2, 2), samples[i]);
        }

        var body = new List<byte>();
        Put(body, "WAVE");
        if (!omitFmt)
        {
            body.AddRange(Chunk("fmt ", [.. fmt]));
        }
        if (extraChunkBeforeData != null)
        {
            body.AddRange(Chunk("JUNK", extraChunkBeforeData));
        }
        if (!omitData)
        {
            body.AddRange(Chunk("data", data));
        }

        var file = new List<byte>();
        Put(file, "RIFF");
        Put(file, body.Count);
        file.AddRange(body);
        return [.. file];
    }

    [Fact]
    public void TryDecode_mono_pcm16_roundtrips_samples()
    {
        short[] samples = [0, 1000, -1000, 32767, -32768];
        var wav = BuildWav(samples, channels: 1, sampleRate: 22050);

        Assert.True(WavDecoder.TryDecode(wav, out var audio));
        Assert.Equal(1, audio.Channels);
        Assert.Equal(22050, audio.SampleRate);
        Assert.Equal(samples, audio.Samples);
    }

    [Fact]
    public void TryDecode_stereo_preserves_interleaved_frames()
    {
        short[] samples = [1, -1, 2, -2, 3, -3];
        var wav = BuildWav(samples, channels: 2, sampleRate: 48000);

        Assert.True(WavDecoder.TryDecode(wav, out var audio));
        Assert.Equal(2, audio.Channels);
        Assert.Equal(48000, audio.SampleRate);
        Assert.Equal(samples, audio.Samples);
    }

    [Fact]
    public void TryDecode_skips_odd_sized_junk_chunk_before_data()
    {
        var wav = BuildWav([7, -7], extraChunkBeforeData: [1, 2, 3]);

        Assert.True(WavDecoder.TryDecode(wav, out var audio));
        Assert.Equal([7, -7], audio.Samples);
    }

    [Fact]
    public void TryDecode_ignores_junk_inside_data_chunk_id_bytes()
    {
        // A non-data chunk whose payload starts with the bytes "data" must not
        // be mistaken for the data chunk (the old code scanned byte-by-byte).
        var wav = BuildWav([9], extraChunkBeforeData: [(byte)'d', (byte)'a', (byte)'t', (byte)'a', 5, 6]);

        Assert.True(WavDecoder.TryDecode(wav, out var audio));
        Assert.Equal([9], audio.Samples);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(11)]
    public void TryDecode_rejects_buffers_too_short_for_a_riff_header(int length)
    {
        Assert.False(WavDecoder.TryDecode(new byte[length], out _));
    }

    [Fact]
    public void TryDecode_rejects_non_riff_magic()
    {
        var wav = BuildWav([1]);
        wav[0] = (byte)'X';

        Assert.False(WavDecoder.TryDecode(wav, out _));
    }

    [Fact]
    public void TryDecode_rejects_non_wave_container()
    {
        var wav = BuildWav([1]);
        wav[8] = (byte)'X';

        Assert.False(WavDecoder.TryDecode(wav, out _));
    }

    [Fact]
    public void TryDecode_rejects_missing_data_chunk()
    {
        var wav = BuildWav([1], omitData: true);

        Assert.False(WavDecoder.TryDecode(wav, out _));
    }

    [Fact]
    public void TryDecode_rejects_missing_fmt_chunk()
    {
        var wav = BuildWav([1], omitFmt: true);

        Assert.False(WavDecoder.TryDecode(wav, out _));
    }

    [Theory]
    [InlineData(3, 16)]   // float32 PCM is not supported
    [InlineData(1, 8)]    // PCM8 is not supported
    public void TryDecode_rejects_unsupported_formats(ushort audioFormat, int bitsPerSample)
    {
        var wav = BuildWav([1, 2], audioFormat: audioFormat, bitsPerSample: bitsPerSample);

        Assert.False(WavDecoder.TryDecode(wav, out _));
    }

    [Fact]
    public void TryDecode_rejects_zero_channels()
    {
        var wav = BuildWav([1, 2], channels: 0, blockAlign: 2);

        Assert.False(WavDecoder.TryDecode(wav, out _));
    }

    [Fact]
    public void TryDecode_rejects_truncated_chunk()
    {
        var wav = BuildWav([1, 2, 3]);
        // Declare a data size larger than what is actually present.
        var dataHeader = wav.AsSpan().IndexOf("data"u8.ToArray());
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(dataHeader + 4, 4), 0x1000);

        Assert.False(WavDecoder.TryDecode(wav, out _));
    }

    [Fact]
    public void TryDecode_rejects_data_size_that_is_not_a_whole_frame()
    {
        var wav = BuildWav([1, 2, 3], channels: 2);
        // Stereo frames need 4 bytes; 6 bytes (3 samples) is not a whole frame count.
        Assert.False(WavDecoder.TryDecode(wav, out _));
    }

    [Fact]
    public void TryDecode_rejects_block_align_mismatch()
    {
        var wav = BuildWav([1, 2], blockAlign: 8);

        Assert.False(WavDecoder.TryDecode(wav, out _));
    }

    [Fact]
    public void TryDecode_empty_data_chunk_returns_empty_samples()
    {
        var wav = BuildWav([]);

        Assert.True(WavDecoder.TryDecode(wav, out var audio));
        Assert.Empty(audio.Samples);
    }
}
