namespace HoryTweaks.Features.MatchHistory.Video;

/// <summary>
/// Streaming H.264 MP4 video encoder backend fed YUV 4:2:0 frames on a dedicated background thread.
/// </summary>
internal interface IMatchVideoEncoder : IDisposable
{
    string BackendName { get; }

    /// <summary>
    /// True when the encoder expects planar I420 (Y, U, V); false when it expects semi-planar NV12 (Y, interleaved UV).
    /// </summary>
    bool ExpectsI420 { get; }

    /// <summary>
    /// Encodes one YUV 4:2:0 frame. Returns false if the underlying backend failed and recording should stop.
    /// </summary>
    bool WriteYuvFrame(byte[] yuvBuffer, int length, long timestamp100ns, long duration100ns);

    /// <summary>
    /// Flushes and finalizes the MP4 container at the temp output path.
    /// </summary>
    bool Finish();

    /// <summary>
    /// Cancels encoding and removes any partial temp output file.
    /// </summary>
    void Abort();
}
