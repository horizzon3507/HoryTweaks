using HoryTweaks.Core.HostTransfer;
using Xunit;

namespace HoryTweaks.Tests.HostTransfer;

public class HostTransferAcceptanceTests
{
    [Fact]
    public void AcceptsTransferFromCurrentHost()
    {
        Assert.True(HostTransferPolicy.AcceptsRemoteTransfer(senderClientId: 3, targetClientId: 7, currentHostId: 3));
    }

    [Fact]
    public void AcceptsRepeatAfterLocalHostIdAlreadyMoved()
    {
        Assert.True(HostTransferPolicy.AcceptsRemoteTransfer(senderClientId: 3, targetClientId: 7, currentHostId: 7));
    }

    [Fact]
    public void RejectsNonHostAnnouncingDifferentTarget()
    {
        Assert.False(HostTransferPolicy.AcceptsRemoteTransfer(senderClientId: 3, targetClientId: 7, currentHostId: 1));
    }

    [Fact]
    public void RejectsNegativeTarget()
    {
        Assert.False(HostTransferPolicy.AcceptsRemoteTransfer(senderClientId: 3, targetClientId: -1, currentHostId: 3));
    }

    [Fact]
    public void RejectsNonHostEvenWhenTargetMatchesTheirOwnId()
    {
        Assert.False(HostTransferPolicy.AcceptsRemoteTransfer(senderClientId: 3, targetClientId: 3, currentHostId: 1));
    }
}
