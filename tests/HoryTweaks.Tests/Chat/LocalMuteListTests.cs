using HoryTweaks.Core.Chat;
using Xunit;

namespace HoryTweaks.Tests.Chat;

public class LocalMuteListTests
{
    [Fact]
    public void Toggle_mute_unmute_and_clear_behave_consistently()
    {
        var list = new LocalMuteList();

        Assert.False(list.IsMuted(3));
        Assert.True(list.Toggle(3, "Spammer"));
        Assert.True(list.IsMuted(3));
        Assert.Equal(1, list.Count);

        Assert.False(list.Toggle(3, "Spammer"));
        Assert.False(list.IsMuted(3));
        Assert.Equal(0, list.Count);

        Assert.True(list.Mute(5, "PlayerFive"));
        Assert.False(list.Mute(5, "PlayerFive"));
        Assert.True(list.Mute(2, "PlayerTwo"));

        var snap = list.Snapshot();
        Assert.Equal(2, snap.Count);
        Assert.Equal((byte)2, snap[0].PlayerId);
        Assert.Equal((byte)5, snap[1].PlayerId);

        Assert.True(list.Unmute(2));
        Assert.False(list.Unmute(2));
        Assert.Equal(1, list.Clear());
        Assert.Equal(0, list.Count);
    }
}
