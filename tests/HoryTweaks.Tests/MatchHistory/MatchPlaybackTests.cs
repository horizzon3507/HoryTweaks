using HoryTweaks.Core.MatchHistory;
using Xunit;

namespace HoryTweaks.Tests.MatchHistory;

public class MatchPlaybackTests
{
    private static MatchPosition P(byte id, float x, float y, MatchPositionFlags flags = MatchPositionFlags.None) => new(id, x, y, flags);

    private static MatchEvent E(int time) => new(time, MatchEventKind.Kill, 0, 1, 0, 0f, 0f);

    [Fact]
    public void Sample_interpolates_at_the_midpoint()
    {
        MatchFrame[] frames = [new(0, [P(1, 0f, 0f)]), new(200, [P(1, 2f, -1f)])];

        var position = Assert.Single(MatchPlayback.Sample(frames, 100));

        Assert.Equal(1f, position.X, 4);
        Assert.Equal(-0.5f, position.Y, 4);
    }

    [Fact]
    public void Sample_snaps_on_long_moves_and_flag_changes()
    {
        MatchFrame[] frames =
        [
            new(0, [P(1, 0f, 0f), P(2, 0f, 0f)]),
            new(200, [P(1, 3f, 0f), P(2, 1f, 0f, MatchPositionFlags.InVent)])
        ];

        var positions = MatchPlayback.Sample(frames, 150);

        Assert.Equal(P(1, 0f, 0f), positions[0]);
        Assert.Equal(P(2, 0f, 0f), positions[1]);
    }

    [Fact]
    public void Sample_keeps_players_missing_from_the_next_frame_in_place_and_omits_new_ones()
    {
        MatchFrame[] frames = [new(0, [P(1, 4f, 5f)]), new(200, [P(2, 0f, 0f)])];

        var position = Assert.Single(MatchPlayback.Sample(frames, 100));

        Assert.Equal(P(1, 4f, 5f), position);
    }

    [Fact]
    public void Sample_is_empty_before_the_first_frame_and_holds_the_last_frame()
    {
        MatchFrame[] frames = [new(100, [P(1, 0f, 0f)]), new(300, [P(1, 1f, 1f)])];

        Assert.Empty(MatchPlayback.Sample(frames, 99));
        Assert.Empty(MatchPlayback.Sample([], 0));
        Assert.Equal(P(1, 1f, 1f), Assert.Single(MatchPlayback.Sample(frames, 10_000)));
        Assert.Equal(P(1, 0f, 0f), Assert.Single(MatchPlayback.Sample(frames, 100)));
    }

    [Fact]
    public void FrameIndexAtOrBefore_finds_boundaries()
    {
        MatchFrame[] frames = [new(100, []), new(300, []), new(500, [])];

        Assert.Equal(-1, MatchPlayback.FrameIndexAtOrBefore(frames, 99));
        Assert.Equal(0, MatchPlayback.FrameIndexAtOrBefore(frames, 100));
        Assert.Equal(1, MatchPlayback.FrameIndexAtOrBefore(frames, 499));
        Assert.Equal(2, MatchPlayback.FrameIndexAtOrBefore(frames, 501));
    }

    [Fact]
    public void PreviousEventTime_skips_events_inside_the_grace_window()
    {
        MatchEvent[] events = [E(1000), E(1000), E(3000)];

        Assert.Equal(1000, MatchPlayback.PreviousEventTime(events, 2999));
        Assert.Equal(1000, MatchPlayback.PreviousEventTime(events, 3500));
        Assert.Equal(3000, MatchPlayback.PreviousEventTime(events, 3501));
        Assert.Null(MatchPlayback.PreviousEventTime(events, 1500));
        Assert.Null(MatchPlayback.PreviousEventTime([], 5000));
    }

    [Fact]
    public void NextEventTime_returns_the_first_later_event()
    {
        MatchEvent[] events = [E(1000), E(1000), E(3000)];

        Assert.Equal(1000, MatchPlayback.NextEventTime(events, 0));
        Assert.Equal(3000, MatchPlayback.NextEventTime(events, 1000));
        Assert.Null(MatchPlayback.NextEventTime(events, 3000));
        Assert.Null(MatchPlayback.NextEventTime([], 0));
    }

    [Fact]
    public void Sample_interpolates_at_a_fraction_of_the_span()
    {
        MatchFrame[] frames = [new(0, [P(1, 0f, 0f)]), new(400, [P(1, 2f, 1f)])];

        var position = Assert.Single(MatchPlayback.Sample(frames, 100));

        Assert.Equal(0.5f, position.X, 4);
        Assert.Equal(0.25f, position.Y, 4);
    }

    [Fact]
    public void Sample_interpolates_when_flags_stay_equal_and_preserves_them()
    {
        MatchFrame[] frames = [new(0, [P(1, 0f, 0f, MatchPositionFlags.Dead)]), new(200, [P(1, 2f, 0f, MatchPositionFlags.Dead)])];

        var position = Assert.Single(MatchPlayback.Sample(frames, 100));

        Assert.Equal(1, position.PlayerId);
        Assert.Equal(MatchPositionFlags.Dead, position.Flags);
        Assert.Equal(1f, position.X, 4);
        Assert.Equal(0f, position.Y, 4);
    }

    [Fact]
    public void Sample_interpolates_a_move_of_exactly_the_snap_distance()
    {
        MatchFrame[] frames = [new(0, [P(1, 0f, 0f)]), new(200, [P(1, MatchPlayback.SnapDistance, 0f)])];

        var position = Assert.Single(MatchPlayback.Sample(frames, 100));

        Assert.Equal(MatchPlayback.SnapDistance / 2f, position.X, 4);
    }

    [Fact]
    public void Sample_snaps_a_diagonal_move_beyond_the_snap_distance()
    {
        // Each axis moves less than SnapDistance, but the diagonal distance clears it.
        MatchFrame[] frames = [new(0, [P(1, 0f, 0f)]), new(200, [P(1, 1.8f, 1.8f)])];

        Assert.Equal(P(1, 0f, 0f), Assert.Single(MatchPlayback.Sample(frames, 199)));
    }

    [Fact]
    public void Sample_at_a_frame_time_returns_that_frame_exactly()
    {
        MatchFrame[] frames =
        [
            new(0, [P(1, 0f, 0f)]),
            new(200, [P(1, 1f, 0f)]),
            new(400, [P(1, 100f, 0f)])
        ];

        // t is 0 toward a far-away next frame, so the result must equal the middle frame, not a blend.
        Assert.Equal(P(1, 1f, 0f), Assert.Single(MatchPlayback.Sample(frames, 200)));
    }

    [Fact]
    public void FrameIndexAtOrBefore_handles_empty_and_single_frame_lists()
    {
        Assert.Equal(-1, MatchPlayback.FrameIndexAtOrBefore([], 0));

        MatchFrame[] frames = [new(500, [])];
        Assert.Equal(-1, MatchPlayback.FrameIndexAtOrBefore(frames, 499));
        Assert.Equal(0, MatchPlayback.FrameIndexAtOrBefore(frames, 500));
        Assert.Equal(0, MatchPlayback.FrameIndexAtOrBefore(frames, 600));
    }

    [Fact]
    public void EventIndexAtOrBefore_finds_boundaries()
    {
        MatchEvent[] events = [E(100), E(300), E(500)];

        Assert.Equal(-1, MatchPlayback.EventIndexAtOrBefore(events, 99));
        Assert.Equal(0, MatchPlayback.EventIndexAtOrBefore(events, 100));
        Assert.Equal(1, MatchPlayback.EventIndexAtOrBefore(events, 499));
        Assert.Equal(2, MatchPlayback.EventIndexAtOrBefore(events, 500));
        Assert.Equal(2, MatchPlayback.EventIndexAtOrBefore(events, int.MaxValue));
        Assert.Equal(-1, MatchPlayback.EventIndexAtOrBefore([], 0));
    }

    [Fact]
    public void EventIndexAtOrBefore_prefers_the_last_of_equal_times()
    {
        MatchEvent[] events = [E(1000), E(1000), E(3000)];

        Assert.Equal(1, MatchPlayback.EventIndexAtOrBefore(events, 1000));
        Assert.Equal(1, MatchPlayback.EventIndexAtOrBefore(events, 2999));
    }

    [Fact]
    public void PreviousEventTime_steps_back_once_per_press()
    {
        MatchEvent[] events = [E(1000), E(2000), E(3000)];

        // Inside the grace window a press skips the nearest event and lands on the one before.
        Assert.Equal(2000, MatchPlayback.PreviousEventTime(events, 3499));
        Assert.Equal(1000, MatchPlayback.PreviousEventTime(events, 2500));
        Assert.Equal(1000, MatchPlayback.PreviousEventTime(events, 2000));
        Assert.Null(MatchPlayback.PreviousEventTime(events, 1000));
    }
}
