using System.Runtime.InteropServices;

namespace HoryTweaks.Features.MatchHistory.Video;

/// <summary>
/// Native Windows Media Foundation (<c>IMFSinkWriter</c>) H.264 MP4 encoder using direct COM vtable calls.
/// Requires no external binaries on Windows 10/11 and enables hardware MFT encoding when available.
/// </summary>
internal sealed unsafe class MediaFoundationMp4Encoder : IMatchVideoEncoder
{
    private const uint MfVersion = 0x00020070;
    private const uint MfStartupNoSocket = 0x1;
    private const uint CoInitMultithreaded = 0x0;
    private const uint MfVideoInterlaceProgressive = 2;
    private const uint AvEncH264ProfileMain = 77;
    private const int RpcEChangedMode = unchecked((int)0x80010106);

    // Verified against Windows SDK mfapi.h / mfreadwrite.h
    private static readonly Guid MfMediaTypeVideo = new(0x73646976, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71);
    private static readonly Guid MfVideoFormatH264 = new(0x34363248, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71);
    private static readonly Guid MfVideoFormatNv12 = new(0x3231564E, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71);
    private static readonly Guid MfMtMajorType = new(0x48eba18e, 0xf8c9, 0x4687, 0xbf, 0x11, 0x0a, 0x74, 0xc9, 0xf9, 0x6a, 0x8f);
    private static readonly Guid MfMtSubtype = new(0xf7e34c9a, 0x42e8, 0x4714, 0xb7, 0x4b, 0xcb, 0x29, 0xd7, 0x2c, 0x35, 0xe5);
    private static readonly Guid MfMtAvgBitrate = new(0x20332624, 0xfb0d, 0x4d9e, 0xbd, 0x0d, 0xcb, 0xf6, 0x78, 0x6c, 0x10, 0x2e);
    private static readonly Guid MfMtFrameRate = new(0xc459a2e8, 0x3d2c, 0x4e44, 0xb1, 0x32, 0xfe, 0xe5, 0x15, 0x6c, 0x7b, 0xb0);
    private static readonly Guid MfMtFrameSize = new(0x1652c33d, 0xd6b2, 0x4012, 0xb8, 0x34, 0x72, 0x03, 0x08, 0x49, 0xa3, 0x7d);
    private static readonly Guid MfMtInterlaceMode = new(0xe2724bb8, 0xe676, 0x4806, 0xb4, 0xb2, 0xa8, 0xd6, 0xef, 0xb4, 0x4c, 0xcd);
    private static readonly Guid MfMtPixelAspectRatio = new(0xc6376a1e, 0x8d0a, 0x4027, 0xbe, 0x45, 0x6d, 0x9a, 0x0a, 0xd3, 0x9b, 0xb6);
    private static readonly Guid MfMtMpeg2Profile = new(0xad76a80b, 0x2d5c, 0x4e0b, 0xb3, 0x75, 0x64, 0xe5, 0x20, 0x13, 0x70, 0x36);
    private static readonly Guid MfReadwriteEnableHardwareTransforms = new(0xa634a91c, 0x822b, 0x41b9, 0xa4, 0x94, 0x4d, 0xe4, 0x64, 0x36, 0x12, 0xb0);

    private readonly string _tempPath;
    private readonly uint _streamIndex;
    private readonly bool _ownsComInit;
    private nint _sinkWriter;
    private bool _mfStarted;
    private bool _disposed;
    private bool _finalized;

    public string BackendName => "MediaFoundation";
    public bool ExpectsI420 => false;

    private MediaFoundationMp4Encoder(string tempPath, nint sinkWriter, uint streamIndex, bool ownsComInit)
    {
        _tempPath = tempPath;
        _sinkWriter = sinkWriter;
        _streamIndex = streamIndex;
        _ownsComInit = ownsComInit;
        _mfStarted = true;
    }

    internal static MediaFoundationMp4Encoder? TryCreate(string tempMp4Path, int width, int height, int fps, int bitrateBps, out string? error)
    {
        error = null;
        if (!OperatingSystem.IsWindows() || NativePlatform.IsWineOrProton())
        {
            error = "Not running on native Windows";
            return null;
        }

        bool ownsCom = false;
        bool mfStarted = false;
        nint attrs = 0;
        nint sinkWriter = 0;
        nint outType = 0;
        nint inType = 0;

        try
        {
            int comHr = CoInitializeEx(0, CoInitMultithreaded);
            if (comHr >= 0)
            {
                ownsCom = true;
            }
            else if (comHr != RpcEChangedMode)
            {
                error = $"CoInitializeEx failed (0x{comHr:X8})";
                return null;
            }

            int hr = MFStartup(MfVersion, MfStartupNoSocket);
            if (hr < 0)
            {
                error = $"MFStartup failed (0x{hr:X8})";
                return null;
            }
            mfStarted = true;

            hr = MFCreateAttributes(out attrs, 2);
            if (hr >= 0 && attrs != 0)
            {
                // Hardware MFT hint; Media Foundation falls back to the inbox software H.264 MFT if unavailable.
                SetUint32(attrs, MfReadwriteEnableHardwareTransforms, 1);
            }

            hr = MFCreateSinkWriterFromURL(tempMp4Path, 0, attrs, out sinkWriter);
            if (hr < 0 || sinkWriter == 0)
            {
                error = $"MFCreateSinkWriterFromURL failed (0x{hr:X8})";
                return null;
            }

            hr = MFCreateMediaType(out outType);
            if (hr < 0 || outType == 0)
            {
                error = $"MFCreateMediaType (out) failed (0x{hr:X8})";
                return null;
            }

            ulong frameSize = PackPair((uint)width, (uint)height);
            ulong frameRate = PackPair((uint)fps, 1);
            ulong pixelAspect = PackPair(1, 1);

            if (!Check(SetGuid(outType, MfMtMajorType, MfMediaTypeVideo), "out MajorType", out error)
                || !Check(SetGuid(outType, MfMtSubtype, MfVideoFormatH264), "out Subtype", out error)
                || !Check(SetUint32(outType, MfMtAvgBitrate, (uint)bitrateBps), "out Bitrate", out error)
                || !Check(SetUint32(outType, MfMtInterlaceMode, MfVideoInterlaceProgressive), "out Interlace", out error)
                || !Check(SetUint64(outType, MfMtFrameSize, frameSize), "out FrameSize", out error)
                || !Check(SetUint64(outType, MfMtFrameRate, frameRate), "out FrameRate", out error)
                || !Check(SetUint64(outType, MfMtPixelAspectRatio, pixelAspect), "out PAR", out error))
            {
                return null;
            }

            // Main profile is widely supported by desktop/mobile players and Discord.
            SetUint32(outType, MfMtMpeg2Profile, AvEncH264ProfileMain);

            if (!Check(SinkAddStream(sinkWriter, outType, out uint streamIndex), "AddStream", out error))
                return null;

            hr = MFCreateMediaType(out inType);
            if (hr < 0 || inType == 0)
            {
                error = $"MFCreateMediaType (in) failed (0x{hr:X8})";
                return null;
            }

            if (!Check(SetGuid(inType, MfMtMajorType, MfMediaTypeVideo), "in MajorType", out error)
                || !Check(SetGuid(inType, MfMtSubtype, MfVideoFormatNv12), "in Subtype", out error)
                || !Check(SetUint32(inType, MfMtInterlaceMode, MfVideoInterlaceProgressive), "in Interlace", out error)
                || !Check(SetUint64(inType, MfMtFrameSize, frameSize), "in FrameSize", out error)
                || !Check(SetUint64(inType, MfMtFrameRate, frameRate), "in FrameRate", out error)
                || !Check(SetUint64(inType, MfMtPixelAspectRatio, pixelAspect), "in PAR", out error)
                || !Check(SinkSetInputMediaType(sinkWriter, streamIndex, inType, 0), "SetInputMediaType", out error)
                || !Check(SinkBeginWriting(sinkWriter), "BeginWriting", out error))
            {
                return null;
            }

            nint ownedWriter = sinkWriter;
            sinkWriter = 0;
            mfStarted = false;
            bool transferCom = ownsCom;
            ownsCom = false;
            return new MediaFoundationMp4Encoder(tempMp4Path, ownedWriter, streamIndex, transferCom);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            error = ex.Message;
            return null;
        }
        finally
        {
            Release(ref inType);
            Release(ref outType);
            Release(ref attrs);
            if (sinkWriter != 0)
            {
                Release(ref sinkWriter);
                TryDeleteTemp(tempMp4Path);
            }
            if (mfStarted)
            {
                try { MFShutdown(); } catch { }
            }
            if (ownsCom)
            {
                try { CoUninitialize(); } catch { }
            }
        }
    }

    public bool WriteYuvFrame(byte[] yuvBuffer, int length, long timestamp100ns, long duration100ns)
    {
        if (_disposed || _finalized || _sinkWriter == 0)
            return false;

        nint buffer = 0;
        nint sample = 0;
        bool locked = false;

        try
        {
            if (MFCreateMemoryBuffer((uint)length, out buffer) < 0 || buffer == 0)
                return false;

            if (BufferLock(buffer, out nint destPtr, out _, out _) < 0 || destPtr == 0)
                return false;
            locked = true;

            Marshal.Copy(yuvBuffer, 0, destPtr, length);

            BufferUnlock(buffer);
            locked = false;

            if (BufferSetCurrentLength(buffer, (uint)length) < 0)
                return false;

            if (MFCreateSample(out sample) < 0 || sample == 0)
                return false;

            if (SampleAddBuffer(sample, buffer) < 0)
                return false;

            if (SampleSetTime(sample, timestamp100ns) < 0)
                return false;

            if (SampleSetDuration(sample, duration100ns) < 0)
                return false;

            return SinkWriteSample(_sinkWriter, _streamIndex, sample) >= 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (locked && buffer != 0)
            {
                try { BufferUnlock(buffer); } catch { }
            }
            Release(ref sample);
            Release(ref buffer);
        }
    }

    public bool Finish()
    {
        if (_disposed || _finalized || _sinkWriter == 0)
            return false;

        _finalized = true;
        int hr = SinkFinalize(_sinkWriter);
        Release(ref _sinkWriter);
        return hr >= 0 && File.Exists(_tempPath);
    }

    public void Abort()
    {
        _finalized = true;
        Release(ref _sinkWriter);
        TryDeleteTemp(_tempPath);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (!_finalized)
        {
            Abort();
        }
        else
        {
            Release(ref _sinkWriter);
        }

        if (_mfStarted)
        {
            _mfStarted = false;
            try { MFShutdown(); } catch { }
        }

        if (_ownsComInit)
        {
            try { CoUninitialize(); } catch { }
        }
    }

    private static bool Check(int hr, string step, out string? error)
    {
        if (hr >= 0)
        {
            error = null;
            return true;
        }

        error = $"{step} failed (0x{hr:X8})";
        return false;
    }

    private static ulong PackPair(uint high, uint low) => ((ulong)high << 32) | low;

    private static void TryDeleteTemp(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    // --- Direct COM vtable dispatch (slot indices verified against mfobjects.h / mfreadwrite.h) ---

    private static void** Vtbl(nint comObj) => *(void***)comObj;

    private static void Release(ref nint comObj)
    {
        nint ptr = comObj;
        comObj = 0;
        if (ptr == 0)
            return;

        try
        {
            var fn = (delegate* unmanaged[Stdcall]<nint, uint>)Vtbl(ptr)[2];
            fn(ptr);
        }
        catch { }
    }

    // IMFAttributes: [21] SetUINT32, [22] SetUINT64, [24] SetGUID
    private static int SetUint32(nint attrs, Guid key, uint value)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, Guid*, uint, int>)Vtbl(attrs)[21];
        return fn(attrs, &key, value);
    }

    private static int SetUint64(nint attrs, Guid key, ulong value)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, Guid*, ulong, int>)Vtbl(attrs)[22];
        return fn(attrs, &key, value);
    }

    private static int SetGuid(nint attrs, Guid key, Guid value)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Vtbl(attrs)[24];
        return fn(attrs, &key, &value);
    }

    // IMFMediaBuffer: [3] Lock, [4] Unlock, [6] SetCurrentLength
    private static int BufferLock(nint buffer, out nint data, out uint maxLength, out uint currentLength)
    {
        nint pData = 0;
        uint maxLen = 0;
        uint curLen = 0;
        var fn = (delegate* unmanaged[Stdcall]<nint, nint*, uint*, uint*, int>)Vtbl(buffer)[3];
        int hr = fn(buffer, &pData, &maxLen, &curLen);
        data = pData;
        maxLength = maxLen;
        currentLength = curLen;
        return hr;
    }

    private static int BufferUnlock(nint buffer)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, int>)Vtbl(buffer)[4];
        return fn(buffer);
    }

    private static int BufferSetCurrentLength(nint buffer, uint length)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, uint, int>)Vtbl(buffer)[6];
        return fn(buffer, length);
    }

    // IMFSample: [36] SetSampleTime, [38] SetSampleDuration, [42] AddBuffer
    private static int SampleSetTime(nint sample, long time100ns)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, long, int>)Vtbl(sample)[36];
        return fn(sample, time100ns);
    }

    private static int SampleSetDuration(nint sample, long duration100ns)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, long, int>)Vtbl(sample)[38];
        return fn(sample, duration100ns);
    }

    private static int SampleAddBuffer(nint sample, nint buffer)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, nint, int>)Vtbl(sample)[42];
        return fn(sample, buffer);
    }

    // IMFSinkWriter: [3] AddStream, [4] SetInputMediaType, [5] BeginWriting, [6] WriteSample, [11] Finalize
    private static int SinkAddStream(nint writer, nint targetMediaType, out uint streamIndex)
    {
        uint idx = 0;
        var fn = (delegate* unmanaged[Stdcall]<nint, nint, uint*, int>)Vtbl(writer)[3];
        int hr = fn(writer, targetMediaType, &idx);
        streamIndex = idx;
        return hr;
    }

    private static int SinkSetInputMediaType(nint writer, uint streamIndex, nint inputMediaType, nint encodingParams)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)Vtbl(writer)[4];
        return fn(writer, streamIndex, inputMediaType, encodingParams);
    }

    private static int SinkBeginWriting(nint writer)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, int>)Vtbl(writer)[5];
        return fn(writer);
    }

    private static int SinkWriteSample(nint writer, uint streamIndex, nint sample)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, uint, nint, int>)Vtbl(writer)[6];
        return fn(writer, streamIndex, sample);
    }

    private static int SinkFinalize(nint writer)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, int>)Vtbl(writer)[11];
        return fn(writer);
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [DllImport("mfplat.dll")]
    private static extern int MFStartup(uint version, uint dwFlags);

    [DllImport("mfplat.dll")]
    private static extern int MFShutdown();

    [DllImport("mfplat.dll")]
    private static extern int MFCreateAttributes(out nint ppMFAttributes, uint cInitialSize);

    [DllImport("mfplat.dll")]
    private static extern int MFCreateMediaType(out nint ppMFType);

    [DllImport("mfplat.dll")]
    private static extern int MFCreateMemoryBuffer(uint cbMaxLength, out nint ppBuffer);

    [DllImport("mfplat.dll")]
    private static extern int MFCreateSample(out nint ppIMFSample);

    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode)]
    private static extern int MFCreateSinkWriterFromURL(
        string pwszOutputURL,
        nint pByteStream,
        nint pAttributes,
        out nint ppSinkWriter);
}
