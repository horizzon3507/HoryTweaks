using HoryTweaks.Core.MatchHistory;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace HoryTweaks.Tests.MatchHistory;

public class MatchRecordCodecTests
{
    private static MatchRecord Sample(params MatchFrame[] frames)
    {
        return new MatchRecord(
            new DateTime(2025, 3, 4, 5, 6, 7, 890, DateTimeKind.Utc),
            123_456,
            2,
            0.75f,
            true,
            false,
            1,
            true,
            [
                new MatchPlayer(0, "Alice", 3, 1, true, true, true, MatchPlayerState.Alive, 2, 0, 0, "hat_tophat", "skin_science", "visor_planky"),
                new MatchPlayer(7, "Bob", 11, 0, false, false, false, MatchPlayerState.Dead, 0, 3, 5, "", "", "")
            ],
            [
                new MatchEvent(1000, MatchEventKind.Kill, 0, 7, 0, 1.5f, -2.25f),
                new MatchEvent(2000, MatchEventKind.Vote, MatchIds.Unknown, MatchIds.Skipped, 0, 0f, 0f)
            ],
            frames);
    }

    private static byte[] Gzip(string json)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(json));
        }

        return output.ToArray();
    }

    [Fact]
    public void Round_trip_preserves_players_events_and_frames()
    {
        var record = Sample(
            new MatchFrame(0, [new MatchPosition(0, 1.234f, -5.678f, MatchPositionFlags.None), new MatchPosition(7, -10.5f, 3f, MatchPositionFlags.Dead | MatchPositionFlags.InVent)]),
            new MatchFrame(200, [new MatchPosition(0, 1.3f, -5.6f, MatchPositionFlags.InVent)]));

        var decoded = MatchRecordCodec.Decode(MatchRecordCodec.Encode(record), includeFrames: true);

        Assert.NotNull(decoded);
        Assert.Equal(record.StartedUtc, decoded.StartedUtc);
        Assert.Equal(record.DurationMs, decoded.DurationMs);
        Assert.Equal(record.MapId, decoded.MapId);
        Assert.Equal(record.MapScale, decoded.MapScale);
        Assert.Equal(record.MapFlipX, decoded.MapFlipX);
        Assert.Equal(record.LocalWon, decoded.LocalWon);
        Assert.Equal(record.Players, decoded.Players);
        Assert.Equal(record.Events, decoded.Events);
        Assert.Equal(record.Frames.Count, decoded.Frames.Count);
        for (int f = 0; f < record.Frames.Count; f++)
        {
            Assert.Equal(record.Frames[f].TimeMs, decoded.Frames[f].TimeMs);
            Assert.Equal(record.Frames[f].Positions.Count, decoded.Frames[f].Positions.Count);
            for (int p = 0; p < record.Frames[f].Positions.Count; p++)
            {
                var expected = record.Frames[f].Positions[p];
                var actual = decoded.Frames[f].Positions[p];
                Assert.Equal(expected.PlayerId, actual.PlayerId);
                Assert.Equal(expected.Flags, actual.Flags);
                Assert.InRange(actual.X, expected.X - 0.01f, expected.X + 0.01f);
                Assert.InRange(actual.Y, expected.Y - 0.01f, expected.Y + 0.01f);
            }
        }
    }

    [Fact]
    public void Decode_without_frames_returns_metadata_and_no_frames()
    {
        var record = Sample(new MatchFrame(0, [new MatchPosition(0, 1f, 2f, MatchPositionFlags.None)]));

        var decoded = MatchRecordCodec.Decode(MatchRecordCodec.Encode(record), includeFrames: false);

        Assert.NotNull(decoded);
        Assert.Empty(decoded.Frames);
        Assert.Equal(record.StartedUtc, decoded.StartedUtc);
        Assert.Equal(record.Players, decoded.Players);
        Assert.Equal(record.Events, decoded.Events);
    }

    [Fact]
    public void Decode_rejects_random_and_truncated_bytes()
    {
        var random = new byte[256];
        new Random(42).NextBytes(random);
        var encoded = MatchRecordCodec.Encode(Sample(new MatchFrame(0, [new MatchPosition(0, 1f, 2f, MatchPositionFlags.None)])));

        Assert.Null(MatchRecordCodec.Decode(random, includeFrames: true));
        Assert.Null(MatchRecordCodec.Decode(encoded[..(encoded.Length / 2)], includeFrames: true));
        Assert.Null(MatchRecordCodec.Decode([], includeFrames: false));
    }

    [Fact]
    public void Decode_rejects_truncated_frame_blob()
    {
        // 4-byte time, count 1, then only the player id: the position is cut off.
        string blob = Convert.ToBase64String([0, 0, 0, 0, 1, 5]);
        var json = $$"""{"v":2,"players":[],"events":[],"frames":"{{blob}}"}""";

        Assert.Null(MatchRecordCodec.Decode(Gzip(json), includeFrames: true));
        Assert.NotNull(MatchRecordCodec.Decode(Gzip(json), includeFrames: false));
    }

    [Fact]
    public void Round_trip_preserves_cosmetics()
    {
        var decoded = MatchRecordCodec.Decode(MatchRecordCodec.Encode(Sample()), includeFrames: false);

        Assert.NotNull(decoded);
        Assert.Equal("hat_tophat", decoded.Players[0].HatId);
        Assert.Equal("skin_science", decoded.Players[0].SkinId);
        Assert.Equal("visor_planky", decoded.Players[0].VisorId);
        Assert.Equal("", decoded.Players[1].HatId);
        Assert.Equal("", decoded.Players[1].SkinId);
        Assert.Equal("", decoded.Players[1].VisorId);
    }

    [Fact]
    public void Decode_accepts_version_1_and_defaults_cosmetics_to_empty()
    {
        var json = """{"v":1,"players":[{"Id":0,"Name":"A","ColorId":3,"RoleType":1,"IsImpostor":true,"Won":false,"IsLocal":true,"FinalState":0,"Kills":0,"TasksDone":0,"TasksTotal":0}],"events":[],"frames":""}""";

        var decoded = MatchRecordCodec.Decode(Gzip(json), includeFrames: true);

        Assert.NotNull(decoded);
        var player = Assert.Single(decoded.Players);
        Assert.Equal("A", player.Name);
        Assert.Equal("", player.HatId);
        Assert.Equal("", player.SkinId);
        Assert.Equal("", player.VisorId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(100)]
    public void Decode_rejects_unsupported_versions(int version)
    {
        var json = $$"""{"v":{{version}},"players":[],"events":[],"frames":""}""";

        Assert.Null(MatchRecordCodec.Decode(Gzip(json), includeFrames: true));
    }

    [Fact]
    public void Decode_rejects_missing_players_or_events()
    {
        Assert.Null(MatchRecordCodec.Decode(Gzip("""{"v":2,"events":[],"frames":""}"""), includeFrames: true));
        Assert.Null(MatchRecordCodec.Decode(Gzip("""{"v":2,"players":[],"frames":""}"""), includeFrames: true));
    }

    [Fact]
    public void Coordinates_outside_the_quantized_range_are_clamped()
    {
        var record = Sample(new MatchFrame(0, [new MatchPosition(0, 500f, -500f, MatchPositionFlags.None)]));

        var position = MatchRecordCodec.Decode(MatchRecordCodec.Encode(record), includeFrames: true)!.Frames[0].Positions[0];

        Assert.Equal(327.67f, position.X, 3);
        Assert.Equal(-327.68f, position.Y, 3);
    }

    [Fact]
    public void Round_trip_preserves_a_match_with_many_players_events_and_frames()
    {
        var players = Enumerable.Range(0, 10)
            .Select(i => new MatchPlayer((byte)i, $"Player{i}", i, i % 4, i % 3 == 0, i % 2 == 0, i == 0, (MatchPlayerState)(i % 3), i, i + 1, i + 2, $"hat_{i}", $"skin_{i}", $"visor_{i}"))
            .ToList();
        var events = Enum.GetValues<MatchEventKind>()
            .Select((kind, i) => new MatchEvent(i * 750, kind, (byte)i, (byte)(i + 1), i * 3, i * 0.5f, -i * 0.25f))
            .ToList();
        var frames = Enumerable.Range(0, 50)
            .Select(f => new MatchFrame(f * 200, players.Select(p => new MatchPosition(p.Id, f * 0.1f + p.Id, -p.Id * 0.5f, (MatchPositionFlags)(f % 4))).ToList()))
            .ToList();
        var record = new MatchRecord(
            new DateTime(2025, 6, 7, 8, 9, 10, 11, DateTimeKind.Utc),
            60_000,
            4,
            1.25f,
            false,
            true,
            3,
            false,
            players,
            events,
            frames);

        var decoded = MatchRecordCodec.Decode(MatchRecordCodec.Encode(record), includeFrames: true);

        Assert.NotNull(decoded);
        Assert.Equal(record.StartedUtc, decoded.StartedUtc);
        Assert.Equal(record.HideAndSeek, decoded.HideAndSeek);
        Assert.Equal(record.GameOverReason, decoded.GameOverReason);
        Assert.Equal(record.Players, decoded.Players);
        Assert.Equal(record.Events, decoded.Events);
        Assert.Equal(record.Frames.Count, decoded.Frames.Count);
        var expected = record.Frames[25].Positions[3];
        var actual = decoded.Frames[25].Positions[3];
        Assert.Equal(expected.PlayerId, actual.PlayerId);
        Assert.Equal(expected.Flags, actual.Flags);
        Assert.InRange(actual.X, expected.X - 0.01f, expected.X + 0.01f);
        Assert.InRange(actual.Y, expected.Y - 0.01f, expected.Y + 0.01f);
    }

    [Fact]
    public void Round_trip_handles_a_full_length_match()
    {
        // A match that ran the recording limit: MaxDurationMs / SampleIntervalMs frames.
        var frames = Enumerable.Range(0, MatchHistoryRules.MaxDurationMs / MatchHistoryRules.SampleIntervalMs)
            .Select(f => new MatchFrame(
                f * MatchHistoryRules.SampleIntervalMs,
                [new MatchPosition(0, f * 0.001f, f * -0.001f, MatchPositionFlags.None), new MatchPosition(1, 0f, f * 0.001f, MatchPositionFlags.InVent)]))
            .ToArray();

        var decoded = MatchRecordCodec.Decode(MatchRecordCodec.Encode(Sample(frames)), includeFrames: true);

        Assert.NotNull(decoded);
        Assert.Equal(frames.Length, decoded.Frames.Count);
        Assert.Equal(frames[0].TimeMs, decoded.Frames[0].TimeMs);
        Assert.Equal(frames[^1].TimeMs, decoded.Frames[^1].TimeMs);
        Assert.Equal(2, decoded.Frames[^1].Positions.Count);
    }

    [Fact]
    public void Encode_drops_positions_beyond_the_byte_count_limit()
    {
        var positions = Enumerable.Range(0, 300).Select(i => new MatchPosition((byte)i, i, -i, MatchPositionFlags.None)).ToList();
        var record = Sample(new MatchFrame(0, positions));

        var frame = MatchRecordCodec.Decode(MatchRecordCodec.Encode(record), includeFrames: true)!.Frames[0];

        Assert.Equal(byte.MaxValue, frame.Positions.Count);
        Assert.Equal(positions[0], frame.Positions[0]);
        Assert.Equal(positions[254], frame.Positions[254]);
    }

    [Fact]
    public void Decode_rejects_json_that_is_not_a_document()
    {
        Assert.Null(MatchRecordCodec.Decode(Gzip("null"), includeFrames: true));
        Assert.Null(MatchRecordCodec.Decode(Gzip("42"), includeFrames: true));
        Assert.Null(MatchRecordCodec.Decode(Gzip("[1,2,3]"), includeFrames: true));
        Assert.Null(MatchRecordCodec.Decode(Gzip("\"text\""), includeFrames: true));
        Assert.Null(MatchRecordCodec.Decode(Gzip("{broken"), includeFrames: true));
    }

    [Fact]
    public void Decode_rejects_a_corrupt_base64_frame_blob()
    {
        var json = """{"v":2,"players":[],"events":[],"frames":"!!!not base64!!!"}""";

        Assert.Null(MatchRecordCodec.Decode(Gzip(json), includeFrames: true));
        Assert.NotNull(MatchRecordCodec.Decode(Gzip(json), includeFrames: false));
    }

    [Fact]
    public void Decode_treats_a_missing_frame_blob_as_no_frames()
    {
        var json = """{"v":2,"players":[{"Id":1,"Name":"Solo","ColorId":0,"RoleType":0,"IsImpostor":false,"Won":true,"IsLocal":true,"FinalState":0,"Kills":0,"TasksDone":0,"TasksTotal":0,"HatId":"hat","SkinId":"skin","VisorId":"visor"}],"events":[]}""";

        var decoded = MatchRecordCodec.Decode(Gzip(json), includeFrames: true);

        Assert.NotNull(decoded);
        Assert.Empty(decoded.Frames);
        Assert.Equal("Solo", Assert.Single(decoded.Players).Name);
    }

    [Fact]
    public void Decode_ignores_unknown_document_fields()
    {
        var json = """{"v":2,"futureField":42,"players":[],"events":[],"frames":"","nested":{"a":[1,2]}}""";

        Assert.NotNull(MatchRecordCodec.Decode(Gzip(json), includeFrames: true));
    }

    [Fact]
    public void Encode_produces_identical_bytes_for_identical_records()
    {
        var record = Sample(new MatchFrame(0, [new MatchPosition(0, 1.5f, -2.5f, MatchPositionFlags.Dead)]));

        Assert.Equal(MatchRecordCodec.Encode(record), MatchRecordCodec.Encode(record));
    }
}
