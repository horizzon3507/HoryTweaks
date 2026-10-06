using BetterAmongUs.Core.Diagnostics;
using Xunit;

namespace HoryTweaks.Tests.Diagnostics;

public class DiagnosticReportBuilderTests
{
    [Fact]
    public void Build_RendersSectionsAndRedactsValues()
    {
        var report = new DiagnosticReportBuilder("HoryTweaks diagnostic report")
            .Sensitive("alice")
            .Section("Mod")
            .Entry("Version", "0.1.1")
            .Entry("Starlight", false)
            .Entry("Missing", null)
            .Section("Recent log")
            .Line("Successfully joined ABCDEF")
            .Lines(["Profile /Users/alice/Library", "Friend Somebody#4242"])
            .Build();

        Assert.StartsWith("HoryTweaks diagnostic report\n============================\n", report.Replace("\r\n", "\n"));
        Assert.Contains("[Mod]", report);
        Assert.Contains("Version: 0.1.1", report);
        Assert.Contains("Starlight: false", report);
        Assert.Contains("Missing: -", report);
        Assert.Contains("[Recent log]", report);
        Assert.DoesNotContain("ABCDEF", report);
        Assert.DoesNotContain("alice", report);
        Assert.DoesNotContain("Somebody#4242", report);
    }
}
