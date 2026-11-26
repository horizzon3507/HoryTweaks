using System.Runtime.InteropServices;
using HoryTweaks.Core.MatchHistory;

namespace HoryTweaks.Features.MatchHistory.Video;

/// <summary>
/// In-process H.264 MP4 encoder powered by Cisco OpenH264 (<c>openh264-2.6.0-win64.dll</c>) and
/// <see cref="Mp4AvcMuxer"/>. Runs inside the game process with zero pipes or child processes, working
/// identically on Linux (Steam Proton / Wine / Flatpak / Steam Deck) and Windows.
/// </summary>
internal sealed unsafe class OpenH264Mp4Encoder : IMatchVideoEncoder
{
    private const int UsageScreenContentRealTime = 1;
    private const int RcBitrateMode = 1;
    private const int VideoFormatI420 = 23;
    private const int VideoFrameTypeInvalid = 0;
    private const int VideoFrameTypeIdr = 1;
    private const int VideoFrameTypeSkip = 4;
    private const int EncoderOptionIdrInterval = 1;
    private const int EncoderOptionSpsPpsIdStrategy = 22;
    private const int SpsPpsConstantId = 0;
    private const int EncoderOptionTraceLevel = 25;
    private const int WelsLogQuiet = 0;

    private static readonly object LibraryLock = new();
    private static nint _libraryHandle;
    private static delegate* unmanaged[Cdecl]<nint*, int> _createEncoder;
    private static delegate* unmanaged[Cdecl]<nint, void> _destroyEncoder;

    private readonly string _tempPath;
    private readonly int _width;
    private readonly int _height;
    private readonly FileStream _fileStream;
    private readonly Mp4AvcMuxer _muxer;
    private nint _encoder;
    private byte[] _annexBScratch = new byte[128 * 1024];
    private bool _closed;
    private bool _disposed;

    public string BackendName => "OpenH264";
    public bool ExpectsI420 => true;

    private OpenH264Mp4Encoder(string tempPath, int width, int height, nint encoder, FileStream fileStream, Mp4AvcMuxer muxer)
    {
        _tempPath = tempPath;
        _width = width;
        _height = height;
        _encoder = encoder;
        _fileStream = fileStream;
        _muxer = muxer;
    }

    internal static OpenH264Mp4Encoder? TryCreate(string dllPath, string tempMp4Path, int width, int height, int fps, int bitrateBps, out string? error)
    {
        error = null;
        if (!EnsureLibraryLoaded(dllPath, out error))
            return null;

        nint encoder = 0;
        FileStream? fileStream = null;
        Mp4AvcMuxer? muxer = null;

        try
        {
            int rc = _createEncoder(&encoder);
            if (rc != 0 || encoder == 0)
            {
                error = $"WelsCreateSVCEncoder failed ({rc})";
                return null;
            }

            // Silence OpenH264 stderr/stdout trace logging before initialization.
            int quiet = WelsLogQuiet;
            SetOption(encoder, EncoderOptionTraceLevel, &quiet);

            var param = new SEncParamBase
            {
                iUsageType = UsageScreenContentRealTime,
                iPicWidth = width,
                iPicHeight = height,
                iTargetBitrate = bitrateBps,
                iRCMode = RcBitrateMode,
                fMaxFrameRate = fps
            };

            rc = Initialize(encoder, &param);
            if (rc != 0)
            {
                error = $"ISVCEncoder.Initialize failed ({rc})";
                return null;
            }

            int idrInterval = Math.Max(fps * 2, 1);
            SetOption(encoder, EncoderOptionIdrInterval, &idrInterval);

            // Keep SPS/PPS IDs constant (0) so MP4 avc1 decoder configuration stays minimal and universally playable.
            int constantId = SpsPpsConstantId;
            SetOption(encoder, EncoderOptionSpsPpsIdStrategy, &constantId);

            fileStream = new FileStream(tempMp4Path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, bufferSize: 64 * 1024);
            muxer = new Mp4AvcMuxer(fileStream, width, height, fps, leaveOpen: false);

            nint ownedEncoder = encoder;
            encoder = 0;
            FileStream ownedStream = fileStream;
            fileStream = null;
            Mp4AvcMuxer ownedMuxer = muxer;
            muxer = null;

            return new OpenH264Mp4Encoder(tempMp4Path, width, height, ownedEncoder, ownedStream, ownedMuxer);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
        finally
        {
            muxer?.Dispose();
            fileStream?.Dispose();
            if (encoder != 0)
            {
                try { Uninitialize(encoder); } catch { }
                try { _destroyEncoder(encoder); } catch { }
                TryDeleteTemp(tempMp4Path);
            }
        }
    }

    public bool WriteYuvFrame(byte[] yuvBuffer, int length, long timestamp100ns, long duration100ns)
    {
        if (_disposed || _closed || _encoder == 0)
            return false;

        int ySize = _width * _height;
        int uvSize = ySize >> 2;
        if (length < ySize + uvSize * 2)
            return false;

        try
        {
            fixed (byte* basePtr = yuvBuffer)
            {
                SSourcePicture pic = default;
                pic.iColorFormat = VideoFormatI420;
                pic.iPicWidth = _width;
                pic.iPicHeight = _height;
                pic.uiTimeStamp = timestamp100ns / 10_000L;
                pic.iStride[0] = _width;
                pic.iStride[1] = _width >> 1;
                pic.iStride[2] = _width >> 1;
                pic.pData0 = basePtr;
                pic.pData1 = basePtr + ySize;
                pic.pData2 = basePtr + ySize + uvSize;

                SFrameBSInfo bsInfo = default;
                int rc = EncodeFrame(_encoder, &pic, &bsInfo);
                if (rc != 0)
                    return false;

                if (bsInfo.eFrameType is VideoFrameTypeInvalid or VideoFrameTypeSkip || bsInfo.iLayerNum <= 0)
                    return true;

                int totalBytes = 0;
                int layerCount = Math.Min(bsInfo.iLayerNum, 128);
                SLayerBSInfo* layers = (SLayerBSInfo*)((byte*)&bsInfo + SFrameBSInfo.LayerInfoOffset);

                for (int i = 0; i < layerCount; i++)
                {
                    var layer = layers[i];
                    if (layer.pBsBuf == null || layer.pNalLengthInByte == null || layer.iNalCount <= 0)
                        continue;

                    for (int n = 0; n < layer.iNalCount; n++)
                    {
                        int nalLen = layer.pNalLengthInByte[n];
                        if (nalLen > 0)
                            totalBytes += nalLen;
                    }
                }

                if (totalBytes <= 0)
                    return true;

                if (_annexBScratch.Length < totalBytes)
                {
                    _annexBScratch = new byte[Math.Max(totalBytes, _annexBScratch.Length * 2)];
                }

                int written = 0;
                for (int i = 0; i < layerCount; i++)
                {
                    var layer = layers[i];
                    if (layer.pBsBuf == null || layer.pNalLengthInByte == null || layer.iNalCount <= 0)
                        continue;

                    int layerBytes = 0;
                    for (int n = 0; n < layer.iNalCount; n++)
                    {
                        int nalLen = layer.pNalLengthInByte[n];
                        if (nalLen > 0)
                            layerBytes += nalLen;
                    }

                    if (layerBytes > 0)
                    {
                        Marshal.Copy((nint)layer.pBsBuf, _annexBScratch, written, layerBytes);
                        written += layerBytes;
                    }
                }

                bool isKeyFrame = bsInfo.eFrameType == VideoFrameTypeIdr;
                _muxer.AddAnnexBFrame(_annexBScratch.AsSpan(0, written), pic.uiTimeStamp, isKeyFrame);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    public bool Finish()
    {
        if (_disposed || _closed)
            return false;

        _closed = true;
        bool ok = false;
        try
        {
            ok = _muxer.Finish();
        }
        catch
        {
            ok = false;
        }
        finally
        {
            _muxer.Dispose();
            DestroyEncoder();
        }

        if (!ok || !File.Exists(_tempPath) || new FileInfo(_tempPath).Length == 0)
        {
            TryDeleteTemp(_tempPath);
            return false;
        }

        return true;
    }

    public void Abort()
    {
        _closed = true;
        try { _muxer.Dispose(); } catch { }
        DestroyEncoder();
        TryDeleteTemp(_tempPath);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (!_closed)
        {
            Abort();
        }
        else
        {
            DestroyEncoder();
        }
    }

    private void DestroyEncoder()
    {
        nint enc = _encoder;
        _encoder = 0;
        if (enc == 0)
            return;

        try { Uninitialize(enc); } catch { }
        try { _destroyEncoder(enc); } catch { }
    }

    private static bool EnsureLibraryLoaded(string dllPath, out string? error)
    {
        error = null;
        lock (LibraryLock)
        {
            if (_libraryHandle != 0)
                return true;

            try
            {
                nint handle = NativeLibrary.Load(dllPath);
                nint createPtr = NativeLibrary.GetExport(handle, "WelsCreateSVCEncoder");
                nint destroyPtr = NativeLibrary.GetExport(handle, "WelsDestroySVCEncoder");

                _createEncoder = (delegate* unmanaged[Cdecl]<nint*, int>)createPtr;
                _destroyEncoder = (delegate* unmanaged[Cdecl]<nint, void>)destroyPtr;
                _libraryHandle = handle;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to load OpenH264 ({ex.Message})";
                return false;
            }
        }
    }

    private static void TryDeleteTemp(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    // --- Direct ISVCEncoder vtable dispatch (verified against Cisco OpenH264 v2.6.0 codec_api.h) ---
    // [0] Initialize, [3] Uninitialize, [4] EncodeFrame, [7] SetOption
    private static void** Vtbl(nint encoder) => *(void***)encoder;

    private static int Initialize(nint encoder, SEncParamBase* param)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, SEncParamBase*, int>)Vtbl(encoder)[0];
        return fn(encoder, param);
    }

    private static int Uninitialize(nint encoder)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, int>)Vtbl(encoder)[3];
        return fn(encoder);
    }

    private static int EncodeFrame(nint encoder, SSourcePicture* srcPic, SFrameBSInfo* bsInfo)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, SSourcePicture*, SFrameBSInfo*, int>)Vtbl(encoder)[4];
        return fn(encoder, srcPic, bsInfo);
    }

    private static int SetOption(nint encoder, int optionId, void* option)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, int, void*, int>)Vtbl(encoder)[7];
        return fn(encoder, optionId, option);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SEncParamBase
    {
        public int iUsageType;
        public int iPicWidth;
        public int iPicHeight;
        public int iTargetBitrate;
        public int iRCMode;
        public float fMaxFrameRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SSourcePicture
    {
        public int iColorFormat;
        public fixed int iStride[4];
        public byte* pData0;
        public byte* pData1;
        public byte* pData2;
        public byte* pData3;
        public int iPicWidth;
        public int iPicHeight;
        public long uiTimeStamp;
        public byte bPsnrY;
        public byte bPsnrU;
        public byte bPsnrV;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SLayerBSInfo
    {
        public byte uiTemporalId;
        public byte uiSpatialId;
        public byte uiQualityId;
        public int eFrameType;
        public byte uiLayerType;
        public int iSubSeqId;
        public int iNalCount;
        public int* pNalLengthInByte;
        public byte* pBsBuf;
        public fixed float rPsnr[3];
    }

    // Total size on x86_64: 8 (iLayerNum + pad) + 128 * 56 (sLayerInfo) + 4 (eFrameType) + 4 (iFrameSizeInBytes) + 8 (uiTimeStamp) = 7192 bytes.
    [StructLayout(LayoutKind.Explicit, Size = 7192)]
    private struct SFrameBSInfo
    {
        internal const int LayerInfoOffset = 8;

        [FieldOffset(0)] public int iLayerNum;
        [FieldOffset(7176)] public int eFrameType;
        [FieldOffset(7180)] public int iFrameSizeInBytes;
        [FieldOffset(7184)] public long uiTimeStamp;
    }
}
