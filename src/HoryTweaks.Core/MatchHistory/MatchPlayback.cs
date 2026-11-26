using System.Collections;

namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Time lookups and position interpolation for replaying a recorded match.
/// </summary>
internal static class MatchPlayback
{
    /// <summary>
    /// Movement longer than this between two samples is a teleport (vent, meeting, shapeshift) and is not interpolated.
    /// </summary>
    internal const float SnapDistance = 2.5f;

    internal const int PreviousEventGraceMs = 500;

    /// <summary>
    /// Returns the index of the last frame with <c>TimeMs &lt;= timeMs</c>, or -1.
    /// </summary>
    internal static int FrameIndexAtOrBefore(IReadOnlyList<MatchFrame> frames, int timeMs)
    {
        int low = 0;
        int high = frames.Count - 1;
        int result = -1;
        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            if (frames[mid].TimeMs <= timeMs)
            {
                result = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return result;
    }

    /// <summary>
    /// Returns player positions at <paramref name="timeMs"/>, interpolating between neighbouring frames.
    /// </summary>
    internal static IReadOnlyList<MatchPosition> Sample(IReadOnlyList<MatchFrame> frames, int timeMs)
    {
        int index = FrameIndexAtOrBefore(frames, timeMs);
        if (index < 0)
            return [];

        var a = frames[index];
        if (index == frames.Count - 1)
            return a.Positions;

        var result = new MatchPosition[a.Positions.Count];
        InterpolateInto(a, frames[index + 1], timeMs, result);
        return result;
    }

    /// <summary>
    /// Writes positions at <paramref name="timeMs"/> interpolated between frames <paramref name="a"/> and <paramref name="b"/>
    /// into <paramref name="dest"/>, which must hold at least <c>a.Positions.Count</c> elements.
    /// </summary>
    internal static void InterpolateInto(MatchFrame a, MatchFrame b, int timeMs, MatchPosition[] dest)
    {
        int span = b.TimeMs - a.TimeMs;
        float t = span <= 0 ? 0f : Math.Clamp((timeMs - a.TimeMs) / (float)span, 0f, 1f);

        for (int i = 0; i < a.Positions.Count; i++)
        {
            var start = a.Positions[i];
            dest[i] = start;

            if (!TryFind(b.Positions, start.PlayerId, out var end) || end.Flags != start.Flags)
                continue;

            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            if (dx * dx + dy * dy > SnapDistance * SnapDistance)
                continue;

            dest[i] = start with { X = start.X + dx * t, Y = start.Y + dy * t };
        }
    }

    /// <summary>
    /// Returns the index of the last event with <c>TimeMs &lt;= timeMs</c>, or -1. Events are in time order.
    /// </summary>
    internal static int EventIndexAtOrBefore(IReadOnlyList<MatchEvent> events, int timeMs)
    {
        int low = 0;
        int high = events.Count - 1;
        int result = -1;
        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            if (events[mid].TimeMs <= timeMs)
            {
                result = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the latest event time before <paramref name="timeMs"/> minus a short grace window,
    /// so repeated presses keep stepping back instead of sticking to the event just passed.
    /// </summary>
    internal static int? PreviousEventTime(IReadOnlyList<MatchEvent> events, int timeMs)
    {
        int index = EventIndexAtOrBefore(events, timeMs - PreviousEventGraceMs - 1);
        return index < 0 ? null : events[index].TimeMs;
    }

    /// <summary>
    /// Returns the earliest event time after <paramref name="timeMs"/>.
    /// </summary>
    internal static int? NextEventTime(IReadOnlyList<MatchEvent> events, int timeMs)
    {
        int index = EventIndexAtOrBefore(events, timeMs) + 1;
        return index < events.Count ? events[index].TimeMs : null;
    }

    private static bool TryFind(IReadOnlyList<MatchPosition> positions, byte playerId, out MatchPosition position)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            if (positions[i].PlayerId == playerId)
            {
                position = positions[i];
                return true;
            }
        }

        position = default;
        return false;
    }
}

/// <summary>
/// Stateful, allocation-free counterpart to <see cref="MatchPlayback"/> for per-frame rendering.
/// Playback time only moves forward, so a monotonic cursor replaces the per-call binary searches and one
/// pooled buffer replaces the per-call array; a backward jump (seek) falls back to the binary search.
/// The list returned by <see cref="Sample"/> reuses the buffer and is only valid until the next call.
/// </summary>
internal sealed class MatchSampler
{
    private readonly IReadOnlyList<MatchFrame> _frames;
    private readonly IReadOnlyList<MatchEvent> _events;
    private readonly SampledPositions _sampled;
    private MatchPosition[] _buffer = [];
    private int _count;
    private int _frameIndex = -1;
    private int _eventIndex = -1;

    internal MatchSampler(IReadOnlyList<MatchFrame> frames, IReadOnlyList<MatchEvent> events)
    {
        _frames = frames;
        _events = events;
        _sampled = new SampledPositions(this);
    }

    internal MatchSampler(MatchRecord record)
        : this(record.Frames, record.Events)
    {
    }

    /// <summary>
    /// Same output as <see cref="MatchPlayback.Sample"/>, written into the pooled buffer.
    /// </summary>
    internal IReadOnlyList<MatchPosition> Sample(int timeMs)
    {
        int index = FrameIndexAt(timeMs);
        if (index < 0)
        {
            _count = 0;
            return _sampled;
        }

        var a = _frames[index];
        if (_buffer.Length < a.Positions.Count)
            _buffer = new MatchPosition[a.Positions.Count];

        _count = a.Positions.Count;
        if (index == _frames.Count - 1)
        {
            for (int i = 0; i < _count; i++)
                _buffer[i] = a.Positions[i];
        }
        else
        {
            MatchPlayback.InterpolateInto(a, _frames[index + 1], timeMs, _buffer);
        }

        return _sampled;
    }

    /// <summary>
    /// The last sampled positions as a span over the pooled buffer, for callers that must not allocate
    /// an enumerator. Valid until the next <see cref="Sample"/>.
    /// </summary>
    internal ReadOnlySpan<MatchPosition> Positions => _buffer.AsSpan(0, _count);

    /// <summary>
    /// Same output as <see cref="MatchPlayback.EventIndexAtOrBefore"/>.
    /// </summary>
    internal int EventIndex(int timeMs)
    {
        if (_eventIndex >= 0 && _events[_eventIndex].TimeMs > timeMs)
        {
            _eventIndex = MatchPlayback.EventIndexAtOrBefore(_events, timeMs);
        }
        else
        {
            while (_eventIndex + 1 < _events.Count && _events[_eventIndex + 1].TimeMs <= timeMs)
                _eventIndex++;
        }

        return _eventIndex;
    }

    /// <summary>
    /// Same output as <see cref="MatchPlayback.PreviousEventTime"/>.
    /// </summary>
    internal int? PreviousEventTime(int timeMs)
    {
        int index = EventIndex(timeMs - MatchPlayback.PreviousEventGraceMs - 1);
        return index < 0 ? null : _events[index].TimeMs;
    }

    /// <summary>
    /// Same output as <see cref="MatchPlayback.NextEventTime"/>.
    /// </summary>
    internal int? NextEventTime(int timeMs)
    {
        int index = EventIndex(timeMs) + 1;
        return index < _events.Count ? _events[index].TimeMs : null;
    }

    private int FrameIndexAt(int timeMs)
    {
        if (_frameIndex >= 0 && _frames[_frameIndex].TimeMs > timeMs)
        {
            _frameIndex = MatchPlayback.FrameIndexAtOrBefore(_frames, timeMs);
        }
        else
        {
            while (_frameIndex + 1 < _frames.Count && _frames[_frameIndex + 1].TimeMs <= timeMs)
                _frameIndex++;
        }

        return _frameIndex;
    }

    /// <summary>
    /// Live view over the sampler's buffer, allocated once and returned by every <see cref="Sample"/>.
    /// </summary>
    private sealed class SampledPositions(MatchSampler owner) : IReadOnlyList<MatchPosition>
    {
        public int Count => owner._count;

        public MatchPosition this[int index] =>
            index >= 0 && index < owner._count
                ? owner._buffer[index]
                : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<MatchPosition> GetEnumerator()
        {
            for (int i = 0; i < owner._count; i++)
                yield return owner._buffer[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
