namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Fast allocation-free pixel conversion from Unity RGBA32 readback buffers to H.264-ready NV12 (YUV 4:2:0 semi-planar).
/// </summary>
internal static class PixelConverter
{
    internal static int Nv12BufferSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width & 1) != 0 || (height & 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive even numbers.");

        return checked(width * height * 3 / 2);
    }

    /// <summary>
    /// Converts tightly packed RGBA32 pixels into BT.601 studio-range NV12 (Y plane followed by interleaved U/V plane).
    /// When <paramref name="flipVertical"/> is true, source rows are read bottom-to-top to convert OpenGL row order
    /// to standard top-down video frame order.
    /// </summary>
    internal static void Rgba32ToNv12(
        ReadOnlySpan<byte> rgba,
        Span<byte> nv12,
        int width,
        int height,
        bool flipVertical)
    {
        if (width <= 0 || height <= 0 || (width & 1) != 0 || (height & 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive even numbers.");

        int expectedRgba = checked(width * height * 4);
        int expectedNv12 = Nv12BufferSize(width, height);
        if (rgba.Length < expectedRgba)
            throw new ArgumentException("Source RGBA buffer is smaller than width * height * 4.", nameof(rgba));
        if (nv12.Length < expectedNv12)
            throw new ArgumentException("Destination NV12 buffer is smaller than width * height * 3 / 2.", nameof(nv12));

        int stride = width * 4;
        int uvPlaneOffset = width * height;

        for (int y = 0; y < height; y += 2)
        {
            int srcY0 = flipVertical ? (height - 1 - y) : y;
            int srcY1 = flipVertical ? (height - 2 - y) : (y + 1);
            int srcRow0 = srcY0 * stride;
            int srcRow1 = srcY1 * stride;
            int yRow0 = y * width;
            int yRow1 = (y + 1) * width;
            int uvRow = uvPlaneOffset + (y >> 1) * width;

            for (int x = 0; x < width; x += 2)
            {
                int i00 = srcRow0 + x * 4;
                int i01 = i00 + 4;
                int i10 = srcRow1 + x * 4;
                int i11 = i10 + 4;

                int r00 = rgba[i00], g00 = rgba[i00 + 1], b00 = rgba[i00 + 2];
                int r01 = rgba[i01], g01 = rgba[i01 + 1], b01 = rgba[i01 + 2];
                int r10 = rgba[i10], g10 = rgba[i10 + 1], b10 = rgba[i10 + 2];
                int r11 = rgba[i11], g11 = rgba[i11 + 1], b11 = rgba[i11 + 2];

                nv12[yRow0 + x] = RgbToY(r00, g00, b00);
                nv12[yRow0 + x + 1] = RgbToY(r01, g01, b01);
                nv12[yRow1 + x] = RgbToY(r10, g10, b10);
                nv12[yRow1 + x + 1] = RgbToY(r11, g11, b11);

                // 2x2 box average for chroma preserves crisp text and UI edges in 4:2:0 subsampling.
                int rAvg = (r00 + r01 + r10 + r11 + 2) >> 2;
                int gAvg = (g00 + g01 + g10 + g11 + 2) >> 2;
                int bAvg = (b00 + b01 + b10 + b11 + 2) >> 2;

                nv12[uvRow + x] = RgbToU(rAvg, gAvg, bAvg);
                nv12[uvRow + x + 1] = RgbToV(rAvg, gAvg, bAvg);
            }
        }
    }

    /// <summary>
    /// Converts tightly packed RGBA32 pixels into BT.601 studio-range planar I420 (Y plane, U plane, V plane).
    /// Used by Cisco OpenH264 in-process encoding.
    /// </summary>
    internal static void Rgba32ToI420(
        ReadOnlySpan<byte> rgba,
        Span<byte> i420,
        int width,
        int height,
        bool flipVertical)
    {
        if (width <= 0 || height <= 0 || (width & 1) != 0 || (height & 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive even numbers.");

        int expectedRgba = checked(width * height * 4);
        int expectedI420 = Nv12BufferSize(width, height);
        if (rgba.Length < expectedRgba)
            throw new ArgumentException("Source RGBA buffer is smaller than width * height * 4.", nameof(rgba));
        if (i420.Length < expectedI420)
            throw new ArgumentException("Destination I420 buffer is smaller than width * height * 3 / 2.", nameof(i420));

        int stride = width * 4;
        int yPlaneSize = width * height;
        int uvWidth = width >> 1;
        int uPlaneOffset = yPlaneSize;
        int vPlaneOffset = yPlaneSize + (yPlaneSize >> 2);

        for (int y = 0; y < height; y += 2)
        {
            int srcY0 = flipVertical ? (height - 1 - y) : y;
            int srcY1 = flipVertical ? (height - 2 - y) : (y + 1);
            int srcRow0 = srcY0 * stride;
            int srcRow1 = srcY1 * stride;
            int yRow0 = y * width;
            int yRow1 = (y + 1) * width;
            int uvRowOffset = (y >> 1) * uvWidth;

            for (int x = 0; x < width; x += 2)
            {
                int i00 = srcRow0 + x * 4;
                int i01 = i00 + 4;
                int i10 = srcRow1 + x * 4;
                int i11 = i10 + 4;

                int r00 = rgba[i00], g00 = rgba[i00 + 1], b00 = rgba[i00 + 2];
                int r01 = rgba[i01], g01 = rgba[i01 + 1], b01 = rgba[i01 + 2];
                int r10 = rgba[i10], g10 = rgba[i10 + 1], b10 = rgba[i10 + 2];
                int r11 = rgba[i11], g11 = rgba[i11 + 1], b11 = rgba[i11 + 2];

                i420[yRow0 + x] = RgbToY(r00, g00, b00);
                i420[yRow0 + x + 1] = RgbToY(r01, g01, b01);
                i420[yRow1 + x] = RgbToY(r10, g10, b10);
                i420[yRow1 + x + 1] = RgbToY(r11, g11, b11);

                int rAvg = (r00 + r01 + r10 + r11 + 2) >> 2;
                int gAvg = (g00 + g01 + g10 + g11 + 2) >> 2;
                int bAvg = (b00 + b01 + b10 + b11 + 2) >> 2;

                int uvIdx = uvRowOffset + (x >> 1);
                i420[uPlaneOffset + uvIdx] = RgbToU(rAvg, gAvg, bAvg);
                i420[vPlaneOffset + uvIdx] = RgbToV(rAvg, gAvg, bAvg);
            }
        }
    }

    private static byte RgbToY(int r, int g, int b)
    {
        int y = ((66 * r + 129 * g + 25 * b + 128) >> 8) + 16;
        return (byte)Math.Clamp(y, 16, 235);
    }

    private static byte RgbToU(int r, int g, int b)
    {
        int u = ((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128;
        return (byte)Math.Clamp(u, 16, 240);
    }

    private static byte RgbToV(int r, int g, int b)
    {
        int v = ((112 * r - 94 * g - 18 * b + 128) >> 8) + 128;
        return (byte)Math.Clamp(v, 16, 240);
    }
}
