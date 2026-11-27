using HoryTweaks.Core.Networking;
using Xunit;

namespace HoryTweaks.Tests.Networking;

public class NetworkTuningPolicyTests
{
    [Fact]
    public void ComputeTunedSettings_raises_tight_vanilla_defaults_to_resilient_targets()
    {
        var tuned = NetworkTuningPolicy.ComputeTunedSettings(
            currentDisconnectTimeoutMs: 5000,
            currentResendLimit: 6,
            currentResendPingMultiplier: 1.2f,
            currentSecondsSuspended: 10);

        Assert.Equal(NetworkTuningPolicy.TargetDisconnectTimeoutMs, tuned.DisconnectTimeoutMs);
        Assert.Equal(NetworkTuningPolicy.TargetResendLimit, tuned.ResendLimit);
        Assert.Equal(NetworkTuningPolicy.TargetResendPingMultiplier, tuned.ResendPingMultiplier);
        Assert.Equal(NetworkTuningPolicy.TargetSecondsSuspendedBeforeDisconnect, tuned.SecondsSuspendedBeforeDisconnect);
    }

    [Fact]
    public void ComputeTunedSettings_never_weakens_higher_existing_values()
    {
        var tuned = NetworkTuningPolicy.ComputeTunedSettings(
            currentDisconnectTimeoutMs: 20000,
            currentResendLimit: 16,
            currentResendPingMultiplier: 2.5f,
            currentSecondsSuspended: 45);

        Assert.Equal(20000, tuned.DisconnectTimeoutMs);
        Assert.Equal(16, tuned.ResendLimit);
        Assert.Equal(2.5f, tuned.ResendPingMultiplier);
        Assert.Equal(45, tuned.SecondsSuspendedBeforeDisconnect);
    }

    [Fact]
    public void ComputeTunedSettings_preserves_zero_unlimited_resend_limit()
    {
        var tuned = NetworkTuningPolicy.ComputeTunedSettings(
            currentDisconnectTimeoutMs: 5000,
            currentResendLimit: 0,
            currentResendPingMultiplier: 1.0f,
            currentSecondsSuspended: 10);

        Assert.Equal(0, tuned.ResendLimit);
    }

    [Fact]
    public void ComputeTunedSettings_raises_finite_low_resend_limit_to_target()
    {
        var tuned = NetworkTuningPolicy.ComputeTunedSettings(
            currentDisconnectTimeoutMs: 12000,
            currentResendLimit: 3,
            currentResendPingMultiplier: 1.6f,
            currentSecondsSuspended: 30);

        Assert.Equal(NetworkTuningPolicy.TargetResendLimit, tuned.ResendLimit);
    }
}
