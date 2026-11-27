using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace HoryTweaks.Features.MatchHistory.Video;

/// <summary>
/// Exact 16-byte native mirror of Unity's AsyncGPUReadbackRequest struct (a native object pointer
/// plus a version counter), passed by reference to the injected icalls.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeAsyncGpuReadbackRequest
{
    public IntPtr Ptr;
    public int Version;
}

/// <summary>
/// Direct native Unity 2022.3 AsyncGPUReadback icall bridge. The IL2CPP managed wrapper was stripped
/// from the interop assemblies, but the icalls themselves are present in UnityPlayer.dll, so they can
/// be resolved and invoked directly for non-blocking GPU readbacks with zero managed allocations.
/// </summary>
internal static class NativeAsyncGpuReadback
{
    private const string LogTag = "MatchVideo";

    // GraphicsFormat.R8G8B8A8_UNorm, verified in UnityEngine.CoreModule.dll.
    private const int GraphicsFormatR8G8B8A8UNorm = 8;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void RequestTexture2Delegate(IntPtr srcTexture, int mipIndex, int dstFormat, IntPtr nativeArrayData, out NativeAsyncGpuReadbackRequest ret);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void RequestTexture1Delegate(IntPtr srcTexture, int mipIndex, IntPtr nativeArrayData, out NativeAsyncGpuReadbackRequest ret);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void UpdateDelegate(ref NativeAsyncGpuReadbackRequest self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool IsDoneDelegate(ref NativeAsyncGpuReadbackRequest self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool HasErrorDelegate(ref NativeAsyncGpuReadbackRequest self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetIntDelegate(ref NativeAsyncGpuReadbackRequest self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetDataRawDelegate(ref NativeAsyncGpuReadbackRequest self, int layer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool SupportsAsyncReadbackDelegate();

    private static readonly object InitLock = new();
    private static bool _initialized;
    private static RequestTexture2Delegate? _requestTexture2;
    private static RequestTexture1Delegate? _requestTexture1;
    private static UpdateDelegate? _update;
    private static IsDoneDelegate? _isDone;
    private static HasErrorDelegate? _hasError;
    private static GetIntDelegate? _getLayerDataSize;
    private static GetIntDelegate? _getWidth;
    private static GetIntDelegate? _getHeight;
    private static GetDataRawDelegate? _getDataRaw;
    private static SupportsAsyncReadbackDelegate? _supportsAsyncReadback;

    /// <summary>
    /// True when the GPU/driver reports async readback support and every required icall resolved.
    /// </summary>
    internal static bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            bool supported = true;
            if (_supportsAsyncReadback != null)
            {
                try
                {
                    supported = _supportsAsyncReadback();
                }
                catch
                {
                    supported = true;
                }
            }

            return supported
                && (_requestTexture2 != null || _requestTexture1 != null)
                && _update != null
                && _isDone != null
                && _hasError != null
                && _getLayerDataSize != null
                && _getWidth != null
                && _getHeight != null
                && _getDataRaw != null;
        }
    }

    private static void EnsureInitialized()
    {
        if (Volatile.Read(ref _initialized))
            return;

        lock (InitLock)
        {
            if (_initialized)
                return;

            _requestTexture2 = Resolve<RequestTexture2Delegate>("UnityEngine.Rendering.AsyncGPUReadback::Request_Internal_Texture_2_Injected");
            _requestTexture1 = Resolve<RequestTexture1Delegate>("UnityEngine.Rendering.AsyncGPUReadback::Request_Internal_Texture_1_Injected");
            _update = Resolve<UpdateDelegate>("UnityEngine.Rendering.AsyncGPUReadbackRequest::Update_Injected");
            _isDone = Resolve<IsDoneDelegate>("UnityEngine.Rendering.AsyncGPUReadbackRequest::IsDone_Injected");
            _hasError = Resolve<HasErrorDelegate>("UnityEngine.Rendering.AsyncGPUReadbackRequest::HasError_Injected");
            _getLayerDataSize = Resolve<GetIntDelegate>("UnityEngine.Rendering.AsyncGPUReadbackRequest::GetLayerDataSize_Injected");
            _getWidth = Resolve<GetIntDelegate>("UnityEngine.Rendering.AsyncGPUReadbackRequest::GetWidth_Injected");
            _getHeight = Resolve<GetIntDelegate>("UnityEngine.Rendering.AsyncGPUReadbackRequest::GetHeight_Injected");
            _getDataRaw = Resolve<GetDataRawDelegate>("UnityEngine.Rendering.AsyncGPUReadbackRequest::GetDataRaw_Injected");
            _supportsAsyncReadback = Resolve<SupportsAsyncReadbackDelegate>("UnityEngine.SystemInfo::SupportsAsyncGPUReadback")
                ?? Resolve<SupportsAsyncReadbackDelegate>("UnityEngine.SystemInfo::get_supportsAsyncGPUReadback");

            Volatile.Write(ref _initialized, true);
        }
    }

    private static T? Resolve<T>(string signature) where T : Delegate
    {
        try
        {
            return IL2CPP.ResolveICall<T>(signature);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Queues an asynchronous RGBA32 readback of <paramref name="texture"/>'s first mip level.
    /// </summary>
    internal static bool TryRequestRgba32(Texture texture, out NativeAsyncGpuReadbackRequest request)
    {
        request = default;
        if (!IsAvailable || texture == null)
            return false;

        try
        {
            IntPtr ptr = IL2CPP.Il2CppObjectBaseToPtrNotNull(texture);
            if (_requestTexture2 != null)
            {
                _requestTexture2(ptr, 0, GraphicsFormatR8G8B8A8UNorm, IntPtr.Zero, out request);
            }
            else
            {
                _requestTexture1!(ptr, 0, IntPtr.Zero, out request);
            }

            return request.Ptr != IntPtr.Zero;
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
            request = default;
            return false;
        }
    }

    internal static void Update(ref NativeAsyncGpuReadbackRequest req)
    {
        _update?.Invoke(ref req);
    }

    internal static bool IsDone(ref NativeAsyncGpuReadbackRequest req)
    {
        return _isDone != null && _isDone(ref req);
    }

    internal static bool HasError(ref NativeAsyncGpuReadbackRequest req)
    {
        return _hasError != null && _hasError(ref req);
    }

    /// <summary>
    /// Copies the completed readback's raw RGBA bytes straight into <paramref name="destination"/>
    /// with zero managed allocations. Returns false on any size or pointer mismatch.
    /// </summary>
    internal static unsafe bool TryCopyRgbaData(ref NativeAsyncGpuReadbackRequest req, int expectedWidth, int expectedHeight, byte[] destination)
    {
        if (_getWidth == null || _getHeight == null || _getLayerDataSize == null || _getDataRaw == null)
            return false;

        if (_getWidth(ref req) != expectedWidth || _getHeight(ref req) != expectedHeight)
            return false;

        int size = _getLayerDataSize(ref req);
        int expectedBytes = expectedWidth * expectedHeight * 4;
        if (size < expectedBytes || destination.Length < expectedBytes)
            return false;

        IntPtr rawPtr = _getDataRaw(ref req, 0);
        if (rawPtr == IntPtr.Zero)
            return false;

        fixed (byte* dst = destination)
        {
            Buffer.MemoryCopy((void*)rawPtr, dst, destination.Length, expectedBytes);
        }

        return true;
    }
}
