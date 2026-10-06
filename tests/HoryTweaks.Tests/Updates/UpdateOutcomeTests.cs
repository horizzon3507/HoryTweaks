using BetterAmongUs.Core.Updates;
using Xunit;

namespace HoryTweaks.Tests.Updates;

public class UpdateOutcomeTests
{
    [Fact]
    public void Success_IsOnlyReportedForSucceededStatus()
    {
        var success = UpdateOutcome.Success("installed");

        Assert.True(success.IsSuccess);
        Assert.Equal(UpdateStatus.Succeeded, success.Status);
        Assert.Equal("installed", success.Detail);
    }

    [Fact]
    public void Failure_IsNeverSuccess()
    {
        var statuses = Enum.GetValues<UpdateStatus>().Where(s => s != UpdateStatus.Succeeded).ToArray();
        Assert.NotEmpty(statuses);

        foreach (var status in statuses)
        {
            var failure = UpdateOutcome.Failure(status, "reason");

            Assert.False(failure.IsSuccess);
            Assert.Equal(status, failure.Status);
            Assert.Equal($"{status}: reason", failure.ToString());
        }
    }

    [Fact]
    public void Failure_RejectsSucceededStatus()
    {
        Assert.Throws<ArgumentException>(() => UpdateOutcome.Failure(UpdateStatus.Succeeded, "nope"));
    }
}
