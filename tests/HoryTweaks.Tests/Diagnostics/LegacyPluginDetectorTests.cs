using HoryTweaks.Core.Diagnostics;
using Xunit;

namespace HoryTweaks.Tests.Diagnostics;

public class LegacyPluginDetectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "horytweaks-tests-" + Guid.NewGuid().ToString("N"));

    public LegacyPluginDetectorTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FindLegacyPluginFiles_FindsDllInPluginsRootAndSubfolders_WithoutTouchingThem()
    {
        var direct = Path.Combine(_root, "BetterAmongUs.dll");
        var nested = Path.Combine(_root, "old", "betteramongus.DLL");
        var unrelated = Path.Combine(_root, "HoryTweaks.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllText(direct, "legacy");
        File.WriteAllText(nested, "legacy");
        File.WriteAllText(unrelated, "current");

        var found = LegacyPluginDetector.FindLegacyPluginFiles(_root);

        Assert.Equal(new[] { direct, nested }.OrderBy(p => p, StringComparer.Ordinal), found);
        Assert.Equal("legacy", File.ReadAllText(direct));
        Assert.Equal("legacy", File.ReadAllText(nested));
        Assert.True(File.Exists(unrelated));
    }

    [Fact]
    public void FindLegacyPluginFiles_ReturnsEmptyForCleanOrMissingFolder()
    {
        File.WriteAllText(Path.Combine(_root, "HoryTweaks.dll"), "current");

        Assert.Empty(LegacyPluginDetector.FindLegacyPluginFiles(_root));
        Assert.Empty(LegacyPluginDetector.FindLegacyPluginFiles(Path.Combine(_root, "missing")));
        Assert.Empty(LegacyPluginDetector.FindLegacyPluginFiles(null));
    }

    [Theory]
    [InlineData("com.d1gq.betteramongus", true)]
    [InlineData("COM.D1GQ.BetterAmongUs", true)]
    [InlineData("com.horizzon3507.horytweaks", false)]
    [InlineData(null, false)]
    public void IsLegacyPluginGuid_MatchesOnlyTheLegacyGuid(string? guid, bool expected)
    {
        Assert.Equal(expected, LegacyPluginDetector.IsLegacyPluginGuid(guid));
    }
}
