using HoryTweaks.Core.Notifications;
using Xunit;

namespace HoryTweaks.Tests.Notifications;

public class WindowAlertPolicyTests
{
    [Fact]
    public void Alerts_only_when_enabled_unfocused_and_outside_cooldown()
    {
        var policy = new WindowAlertPolicy();

        Assert.False(policy.ShouldAlert(enabled: false, isWindowFocused: false, nowSeconds: 10.0));
        Assert.False(policy.ShouldAlert(enabled: true, isWindowFocused: true, nowSeconds: 10.0));
        Assert.True(policy.ShouldAlert(enabled: true, isWindowFocused: false, nowSeconds: 10.0));
        Assert.False(policy.ShouldAlert(enabled: true, isWindowFocused: false, nowSeconds: 11.0));
        Assert.True(policy.ShouldAlert(enabled: true, isWindowFocused: false, nowSeconds: 13.0));
    }

    [Fact]
    public void Lobby_full_triggers_only_on_rising_edge()
    {
        var policy = new WindowAlertPolicy();

        Assert.False(policy.ObserveLobbyOccupancy(1, 1));
        Assert.False(policy.ObserveLobbyOccupancy(9, 10));
        Assert.True(policy.ObserveLobbyOccupancy(10, 10));
        Assert.False(policy.ObserveLobbyOccupancy(10, 10));
        Assert.False(policy.ObserveLobbyOccupancy(9, 10));
        Assert.True(policy.ObserveLobbyOccupancy(10, 10));

        policy.ResetLobby();
        Assert.True(policy.ObserveLobbyOccupancy(10, 10));
    }
}
