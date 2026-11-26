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
            currentReceiveBufferSize: 65536,
            currentSecondsSuspended: 10);

        Assert.Equal(NetworkTuningPolicy.TargetKeepAliveIntervalMs, tuned.KeepAliveIntervalMs);
        Assert.Equal(NetworkTuningPolicy.TargetDisconnectTimeoutMs, tuned.DisconnectTimeoutMs);
        Assert.Equal(NetworkTuningPolicy.TargetResendTimeoutMs, tuned.ResendTimeoutMs);
        Assert.Equal(NetworkTuningPolicy.TargetResendLimit, tuned.ResendLimit);
        Assert.Equal(NetworkTuningPolicy.TargetResendPingMultiplier, tuned.ResendPingMultiplier);
        Assert.Equal(NetworkTuningPolicy.TargetReceiveBufferSize, tuned.ReceiveBufferSize);
        Assert.Equal(NetworkTuningPolicy.TargetSecondsSuspendedBeforeDisconnect, tuned.SecondsSuspendedBeforeDisconnect);
    }

    [Fact]
    public void ComputeTunedSettings_never_weakens_higher_existing_values()
    {
        var tuned = NetworkTuningPolicy.ComputeTunedSettings(
            currentDisconnectTimeoutMs: 20000,
            currentResendLimit: 16,
            currentResendPingMultiplier: 2.5f,
            currentReceiveBufferSize: 2 * 1024 * 1024,
            currentSecondsSuspended: 45);

        Assert.Equal(20000, tuned.DisconnectTimeoutMs);
        Assert.Equal(16, tuned.ResendLimit);
        Assert.Equal(2.5f, tuned.ResendPingMultiplier);
        Assert.Equal(2 * 1024 * 1024, tuned.ReceiveBufferSize);
        Assert.Equal(45, tuned.SecondsSuspendedBeforeDisconnect);
    }

    [Fact]
    public void KeepAliveInterval_stays_well_below_disconnect_timeout()
    {
        Assert.InRange(NetworkTuningPolicy.TargetKeepAliveIntervalMs, 1000, 3000);
        Assert.True(NetworkTuningPolicy.TargetDisconnectTimeoutMs >= NetworkTuningPolicy.TargetKeepAliveIntervalMs * 5);
    }
}
