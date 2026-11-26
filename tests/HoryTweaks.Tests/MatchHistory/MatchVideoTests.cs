using System.Text;
using HoryTweaks.Core.Compression;
using HoryTweaks.Core.MatchHistory;
using Xunit;

namespace HoryTweaks.Tests.MatchHistory;

public class MatchVideoTests
{
    [Theory]
    [InlineData(1920, 1080, 848, 480)]
    [InlineData(1280, 720, 848, 480)]
    [InlineData(2560, 1080, 864, 368)]
    [InlineData(640, 480, 640, 480)]
    [InlineData(100, 80, 160, 112)]
    [InlineData(0, 0, 848, 480)]
    public void ComputeScaledDimensions_preserves_aspect_and_aligns_to_16(int inW, int inH, int expW, int expH)
    {
        var (w, h) = MatchVideoRules.ComputeScaledDimensions(inW, inH);
        Assert.Equal(expW, w);
        Assert.Equal(expH, h);
        Assert.Equal(0, w % 16);
        Assert.Equal(0, h % 16);
    }

    [Fact]
    public void ShouldBeginRecording_requires_both_match_history_and_video_enabled()
    {
        Assert.True(MatchVideoRules.ShouldBeginRecording(true, true, true, false, ClientGameState.Started, true, false));
        Assert.False(MatchVideoRules.ShouldBeginRecording(true, false, true, false, ClientGameState.Started, true, false));
        Assert.False(MatchVideoRules.ShouldBeginRecording(false, true, true, false, ClientGameState.Started, true, false));
        Assert.False(MatchVideoRules.ShouldBeginRecording(true, true, true, true, ClientGameState.Started, true, false));
        Assert.False(MatchVideoRules.ShouldBeginRecording(true, true, true, false, ClientGameState.Started, true, true));
    }

    [Fact]
    public void Retention_pairs_match_file_names_and_enforces_count_and_size_caps()
    {
        var started = new DateTime(2026, 11, 24, 22, 30, 15, 123, DateTimeKind.Utc);
        string matchFile = MatchHistoryRetention.FileNameFor(started);
        string videoFile = MatchVideoRetention.FileNameFor(started);

        Assert.Equal(videoFile, MatchVideoRetention.VideoFileNameForMatchFile(matchFile));
        Assert.Null(MatchVideoRetention.VideoFileNameForMatchFile("not-a-match.htmatch"));
        Assert.True(MatchVideoRetention.IsVideoFile(videoFile));
        Assert.False(MatchVideoRetention.IsVideoFile(videoFile + ".tmp"));

        var files = Enumerable.Range(0, 7).Select(i =>
            (MatchVideoRetention.FileNameFor(started.AddMinutes(i)), 60L * 1024 * 1024)).ToList();

        var toDelete = MatchVideoRetention.SelectForDeletion(files, maxVideos: 5, maxTotalBytes: 150L * 1024 * 1024);
        Assert.Equal(5, toDelete.Count);
        Assert.DoesNotContain(files[^1].Item1, toDelete);
        Assert.DoesNotContain(files[^2].Item1, toDelete);

        var huge = new[] { (videoFile, 500L * 1024 * 1024) };
        Assert.Empty(MatchVideoRetention.SelectForDeletion(huge, maxVideos: 5, maxTotalBytes: 100L * 1024 * 1024));
    }

    [Fact]
    public void PixelConverter_converts_rgba_to_nv12_and_i420_with_optional_vertical_flip()
    {
        const int w = 4;
        const int h = 4;
        byte[] rgba = new byte[w * h * 4];

        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                rgba[i] = 255;
                rgba[i + 1] = 255;
                rgba[i + 2] = 255;
                rgba[i + 3] = 255;
            }
        }

        byte[] nv12NoFlip = new byte[PixelConverter.Nv12BufferSize(w, h)];
        PixelConverter.Rgba32ToNv12(rgba, nv12NoFlip, w, h, flipVertical: false);

        Assert.Equal((byte)235, nv12NoFlip[0]);
        Assert.Equal((byte)16, nv12NoFlip[2 * w]);
        Assert.Equal((byte)128, nv12NoFlip[w * h]);
        Assert.Equal((byte)128, nv12NoFlip[w * h + 1]);

        byte[] nv12Flipped = new byte[PixelConverter.Nv12BufferSize(w, h)];
        PixelConverter.Rgba32ToNv12(rgba, nv12Flipped, w, h, flipVertical: true);

        Assert.Equal((byte)16, nv12Flipped[0]);
        Assert.Equal((byte)235, nv12Flipped[2 * w]);

        byte[] i420 = new byte[PixelConverter.Nv12BufferSize(w, h)];
        PixelConverter.Rgba32ToI420(rgba, i420, w, h, flipVertical: false);
        Assert.Equal((byte)235, i420[0]);
        Assert.Equal((byte)16, i420[2 * w]);
        Assert.Equal((byte)128, i420[w * h]);
        Assert.Equal((byte)128, i420[w * h + (w * h / 4)]);
    }

    [Fact]
    public void FfmpegLocator_prefers_local_tools_then_path_and_skips_linux_elf_on_wine()
    {
        string localTool = Path.Combine("/data", "Tools", "ffmpeg.exe");
        string pathTool = Path.Combine("/tools", "ffmpeg.exe");

        var foundLocal = FfmpegLocator.FindExecutable("/data", "/tools", true, false, p => p == localTool || p == pathTool);
        Assert.Equal(localTool, foundLocal);

        var foundPath = FfmpegLocator.FindExecutable("/data", "/tools", true, false, p => p == pathTool);
        Assert.Equal(pathTool, foundPath);

        // Under Wine/Proton, never pick Linux ELF /usr/bin/ffmpeg over pipe; only ffmpeg.exe.
        var notLinuxElf = FfmpegLocator.FindExecutable("/data", "", true, true, p => p == @"Z:\usr\bin\ffmpeg");
        Assert.Null(notLinuxElf);

        var foundUnixNative = FfmpegLocator.FindExecutable("/data", "/usr/bin", false, false, p => p == Path.Combine("/usr/bin", "ffmpeg"));
        Assert.Equal(Path.Combine("/usr/bin", "ffmpeg"), foundUnixNative);

        string args = FfmpegLocator.BuildArguments(848, 480, 20, @"C:\replays\match.tmp.mp4");
        Assert.Contains("-video_size 848x480", args);
        Assert.Contains("-framerate 20", args);
        Assert.Contains("-pixel_format nv12", args);
        Assert.Contains("-movflags +faststart", args);
        Assert.Contains("-f mp4", args);
    }

    [Fact]
    public void Mp4AvcMuxer_writes_valid_ftyp_mdat_moov_and_avcC_boxes()
    {
        using var ms = new MemoryStream();
        using (var muxer = new Mp4AvcMuxer(ms, 320, 240, 20, leaveOpen: true))
        {
            // Synthetic Annex-B frame containing SPS (type 7), PPS (type 8), and IDR slice (type 5).
            byte[] idrFrame =
            [
                0x00, 0x00, 0x00, 0x01, 0x67, 0x42, 0xC0, 0x1E, 0xAA, 0xBB,
                0x00, 0x00, 0x00, 0x01, 0x68, 0xCE, 0x3C, 0x80,
                0x00, 0x00, 0x01, 0x65, 0x88, 0x84, 0x21, 0x43
            ];
            byte[] pFrame =
            [
                0x00, 0x00, 0x00, 0x01, 0x41, 0x9A, 0x12, 0x34, 0x56
            ];

            muxer.AddAnnexBFrame(idrFrame, timestampMs: 0, isKeyFrameHint: true);
            muxer.AddAnnexBFrame(pFrame, timestampMs: 50, isKeyFrameHint: false);
            Assert.Equal(2, muxer.SampleCount);
            Assert.True(muxer.Finish());
        }

        byte[] mp4 = ms.ToArray();
        string ascii = Encoding.Latin1.GetString(mp4);
        Assert.Contains("ftyp", ascii);
        Assert.Contains("mdat", ascii);
        Assert.Contains("moov", ascii);
        Assert.Contains("avc1", ascii);
        Assert.Contains("avcC", ascii);
        Assert.Contains("stts", ascii);
        Assert.Contains("stss", ascii);
        Assert.Contains("co64", ascii);
    }

    [Fact]
    public void Mp4AvcMuxer_returns_false_without_parameter_sets()
    {
        using var ms = new MemoryStream();
        using var muxer = new Mp4AvcMuxer(ms, 320, 240, 20, leaveOpen: true);
        muxer.AddAnnexBFrame([0x00, 0x00, 0x00, 0x01, 0x41, 0x01], 0, false);
        Assert.False(muxer.Finish());
    }

    [Fact]
    public void BZip2Decoder_decompresses_valid_stream_and_rejects_corrupted_crc()
    {
        // Real bzip2-compressed bytes for "HoryTweaks OpenH264 Linux Support!\n"
        byte[] compressed = Convert.FromBase64String(
            "QlpoOTFBWSZTWQv5NFsAAATfgAAQYAAVAABEjAAiKd7gIAAijU9TTaQyPIbUKAAaBkyCNnyiIqViPxq7Hg/FIPBw/wmexdyRThQkAv5NFsA=");

        byte[] plain = BZip2Decoder.Decompress(compressed, maxOutputBytes: 1024);
        Assert.Equal("HoryTweaks OpenH264 Linux Support!\n", Encoding.UTF8.GetString(plain));

        // Corrupt a payload byte -> must fail CRC or structural validation.
        byte[] corrupted = (byte[])compressed.Clone();
        corrupted[^3] ^= 0xFF;
        Assert.ThrowsAny<InvalidDataException>(() => BZip2Decoder.Decompress(corrupted, maxOutputBytes: 1024));

        // Enforce maxOutputBytes cap.
        Assert.Throws<InvalidDataException>(() => BZip2Decoder.Decompress(compressed, maxOutputBytes: 8));
    }
}
