using HoryTweaks.Core.MatchHistory;
using Xunit;

namespace HoryTweaks.Tests.MatchHistory;

public class MatchSamplerTests
{
    private static MatchPosition P(byte id, float x, float y, MatchPositionFlags flags = MatchPositionFlags.None) => new(id, x, y, flags);

    private static MatchEvent E(int time) => new(time, MatchEventKind.Kill, 0, 1, 0, 0f, 0f);

    private static void AssertMatches(IReadOnlyList<MatchFrame> frames, int timeMs, IReadOnlyList<MatchPosition> actual)
    {
        var expected = MatchPlayback.Sample(frames, timeMs);
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
            Assert.Equal(expected[i], actual[i]);
    }

    [Fact]
    public void Sample_matches_the_static_api_on_a_forward_sweep()
    {
        MatchFrame[] frames =
        [
            new(100, [P(1, 0f, 0f), P(2, 5f, 5f)]),
            new(300, [P(1, 1f, 0.5f), P(2, 5f, 6f)]),
            new(500, [P(1, 2f, 1f), P(3, 0f, 0f)]),
            new(700, [P(1, 3f, 1.5f), P(3, 0.5f, 0f)])
        ];
        var sampler = new MatchSampler(frames, []);

        for (int t = 0; t <= 1000; t += 13)
            AssertMatches(frames, t, sampler.Sample(t));
    }

    [Fact]
    public void Sample_recovers_after_backward_seeks()
    {
        MatchFrame[] frames =
        [
            new(100, [P(1, 0f, 0f)]),
            new(300, [P(1, 1f, 0f)]),
            new(500, [P(1, 2f, 0f)]),
            new(700, [P(1, 3f, 0f)])
        ];
        var sampler = new MatchSampler(frames, []);

        foreach (int t in new[] { 100, 350, 650, 800, 200, 0, 450, 450, 250, 900, 50, 400 })
            AssertMatches(frames, t, sampler.Sample(t));
    }

    [Fact]
    public void Sample_matches_across_teleports_flag_changes_and_missing_players()
    {
        MatchFrame[] frames =
        [
            new(0, [P(1, 0f, 0f), P(2, 0f, 0f), P(3, 0f, 0f), P(4, 7f, 7f)]),
            new(200, [P(1, 10f, 0f), P(2, 0f, 1f, MatchPositionFlags.InVent), P(3, 0.5f, 0f)])
        ];
        var sampler = new MatchSampler(frames, []);

        for (int t = -10; t <= 400; t += 10)
            AssertMatches(frames, t, sampler.Sample(t));
    }

    [Fact]
    public void Sample_handles_empty_and_single_frame_inputs()
    {
        var empty = new MatchSampler([], []);
        Assert.Empty(empty.Sample(0));
        Assert.Empty(empty.Sample(1000));

        MatchFrame[] single = [new(200, [P(1, 1f, 2f)])];
        var sampler = new MatchSampler(single, []);
        Assert.Empty(sampler.Sample(199));
        AssertMatches(single, 200, sampler.Sample(200));
        AssertMatches(single, 5000, sampler.Sample(5000));
        Assert.Empty(sampler.Sample(50));
        AssertMatches(single, 300, sampler.Sample(300));
    }

    [Fact]
    public void Sample_reuses_the_same_result_list_and_span()
    {
        MatchFrame[] frames = [new(0, [P(1, 0f, 0f), P(2, 2f, 2f)]), new(200, [P(1, 1f, 1f), P(2, 2f, 2f)])];
        var sampler = new MatchSampler(frames, []);

        var first = sampler.Sample(0);
        var second = sampler.Sample(100);

        Assert.Same(first, second);
        Assert.Equal(2, sampler.Positions.Length);
        Assert.Equal(P(1, 0.5f, 0.5f), sampler.Positions[0]);
    }

    [Fact]
    public void Sample_does_not_allocate_once_the_buffer_is_warm()
    {
        MatchFrame[] frames = [new(0, [P(1, 0f, 0f), P(2, 0f, 0f)]), new(200, [P(1, 1f, 1f), P(2, 1f, -1f)])];
        var sampler = new MatchSampler(frames, []);

        sampler.Sample(50);
        sampler.Sample(100);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int t = 0; t <= 200; t++)
        {
            var sampled = sampler.Sample(t);
            for (int i = 0; i < sampled.Count; i++)
                _ = sampled[i];
            foreach (var position in sampler.Positions)
                _ = position;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
    }

    [Fact]
    public void EventIndex_matches_the_static_api_in_both_directions()
    {
        MatchEvent[] events = [E(100), E(100), E(250), E(900), E(901), E(5000)];
        var sampler = new MatchSampler([], events);

        foreach (int t in new[] { 0, 99, 100, 101, 249, 250, 800, 1000, 400, 249, 5000, 6000, 0, 5000 })
            Assert.Equal(MatchPlayback.EventIndexAtOrBefore(events, t), sampler.EventIndex(t));
    }

    [Fact]
    public void Event_navigation_matches_the_static_api()
    {
        MatchEvent[] events = [E(1000), E(1000), E(3000)];
        var sampler = new MatchSampler([], events);

        foreach (int t in new[] { 0, 999, 1000, 1500, 2999, 3000, 3500, 3501, 5000, 500, 4000 })
        {
            Assert.Equal(MatchPlayback.PreviousEventTime(events, t), sampler.PreviousEventTime(t));
            Assert.Equal(MatchPlayback.NextEventTime(events, t), sampler.NextEventTime(t));
            Assert.Equal(MatchPlayback.EventIndexAtOrBefore(events, t), sampler.EventIndex(t));
        }
    }

    [Fact]
    public void EventIndex_handles_empty_events()
    {
        var sampler = new MatchSampler([], []);
        Assert.Equal(-1, sampler.EventIndex(0));
        Assert.Equal(-1, sampler.EventIndex(5000));
        Assert.Null(sampler.PreviousEventTime(5000));
        Assert.Null(sampler.NextEventTime(0));
    }

    [Fact]
    public void Sampler_matches_the_static_api_on_a_random_walk_with_seeks()
    {
        var random = new Random(1234);
        var frames = new List<MatchFrame>();
        for (int i = 0; i < 200; i++)
        {
            var positions = new List<MatchPosition>();
            for (byte p = 1; p <= 10; p++)
            {
                if (random.Next(10) == 0)
                    continue;

                positions.Add(new MatchPosition(
                    p,
                    (float)(random.NextDouble() * 20 - 10),
                    (float)(random.NextDouble() * 20 - 10),
                    (MatchPositionFlags)random.Next(4)));
            }

            frames.Add(new MatchFrame(i * 200, positions));
        }

        var events = new List<MatchEvent>();
        for (int i = 0; i < 50; i++)
            events.Add(E(random.Next(0, 40_000)));
        events.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        var sampler = new MatchSampler(frames, events);
        int time = 0;
        for (int step = 0; step < 3000; step++)
        {
            if (random.Next(10) == 0)
                time = random.Next(-100, Math.Max(time, 0));
            else
                time += random.Next(1, 250);

            AssertMatches(frames, time, sampler.Sample(time));
            Assert.Equal(MatchPlayback.EventIndexAtOrBefore(events, time), sampler.EventIndex(time));
            Assert.Equal(MatchPlayback.PreviousEventTime(events, time), sampler.PreviousEventTime(time));
            Assert.Equal(MatchPlayback.NextEventTime(events, time), sampler.NextEventTime(time));
        }
    }
}
