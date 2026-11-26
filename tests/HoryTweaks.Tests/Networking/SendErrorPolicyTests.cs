using HoryTweaks.Core.Networking;
using Xunit;

namespace HoryTweaks.Tests.Networking;

public class SendErrorPolicyTests
{
    [Fact]
    public void None_returns_continue_and_resets_consecutive_error_counters()
    {
        var tracker = new SendErrorTracker();

        Assert.Equal(SendErrorAction.IgnoreTransient, tracker.Evaluate(SendErrorKind.Unknown, false, 1.0, out _));
        Assert.Equal(SendErrorAction.IgnoreTransient, tracker.Evaluate(SendErrorKind.Unknown, false, 1.5, out _));
        Assert.Equal(SendErrorAction.Continue, tracker.Evaluate(SendErrorKind.None, false, 2.0, out _));

        for (int i = 0; i < SendErrorTracker.MaxConsecutiveUnreliableErrors - 1; i++)
        {
            Assert.Equal(SendErrorAction.IgnoreTransient, tracker.Evaluate(SendErrorKind.Unknown, false, 2.5 + i * 0.2, out _));
        }
    }

    [Fact]
    public void Disconnected_always_disconnects_immediately()
    {
        var tracker = new SendErrorTracker();

        Assert.Equal(SendErrorAction.Disconnect, tracker.Evaluate(SendErrorKind.Disconnected, isReliable: false, 1.0, out _));
        Assert.Equal(SendErrorAction.Disconnect, tracker.Evaluate(SendErrorKind.Disconnected, isReliable: true, 2.0, out _));
    }

    [Fact]
    public void Reliable_unknown_errors_disconnect_immediately_to_avoid_dropping_reliable_state()
    {
        var tracker = new SendErrorTracker();

        Assert.Equal(SendErrorAction.Disconnect, tracker.Evaluate(SendErrorKind.Unknown, isReliable: true, 1.0, out bool warn));
        Assert.False(warn);
        Assert.Equal(0, tracker.AbsorbedTransientErrors);
    }

    [Fact]
    public void Unreliable_unknown_errors_absorb_transient_blips_and_disconnect_at_threshold()
    {
        var tracker = new SendErrorTracker();

        for (int i = 0; i < SendErrorTracker.MaxConsecutiveUnreliableErrors - 1; i++)
        {
            var action = tracker.Evaluate(SendErrorKind.Unknown, isReliable: false, 10.0 + i * 0.5, out bool warn);
            Assert.Equal(SendErrorAction.IgnoreTransient, action);
            Assert.Equal(i == 0, warn);
        }

        Assert.Equal(SendErrorTracker.MaxConsecutiveUnreliableErrors - 1, tracker.AbsorbedTransientErrors);

        var finalAction = tracker.Evaluate(SendErrorKind.Unknown, isReliable: false, 12.5, out _);
        Assert.Equal(SendErrorAction.Disconnect, finalAction);
    }

    [Fact]
    public void Errors_spaced_beyond_window_do_not_accumulate_into_disconnect()
    {
        var tracker = new SendErrorTracker();

        for (int i = 0; i < 10; i++)
        {
            double t = i * (SendErrorTracker.ErrorWindowSeconds + 1.0);
            Assert.Equal(SendErrorAction.IgnoreTransient, tracker.Evaluate(SendErrorKind.Unknown, isReliable: false, t, out _));
        }

        Assert.Equal(10, tracker.AbsorbedTransientErrors);
    }
}
