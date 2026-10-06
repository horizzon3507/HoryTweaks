using HoryTweaks.Core.Updates;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace HoryTweaks.Tests.Updates;

public class UpdatePackageInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "horytweaks-updater-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static byte[] BuildZip(params string[] entryNames)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in entryNames)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write($"content of {name}");
            }
        }

        return stream.ToArray();
    }

    private static readonly string[] PackageEntries =
    [
        "winhttp.dll",
        "BepInEx/core/BepInEx.Core.dll",
        "BepInEx/plugins/HoryTweaks.dll",
    ];

    [Fact]
    public void ExtractTo_writes_every_entry_under_the_staging_folder()
    {
        var staging = Path.Combine(_root, "files");

        var outcome = UpdatePackageInstaller.ExtractTo(BuildZip(PackageEntries), staging);

        Assert.True(outcome.IsSuccess, outcome.Detail);
        Assert.True(File.Exists(Path.Combine(staging, "winhttp.dll")));
        Assert.True(File.Exists(Path.Combine(staging, "BepInEx", "plugins", "HoryTweaks.dll")));
        Assert.Equal("content of BepInEx/plugins/HoryTweaks.dll", File.ReadAllText(Path.Combine(staging, "BepInEx", "plugins", "HoryTweaks.dll")));
    }

    [Fact]
    public void ExtractTo_resets_a_dirty_staging_folder()
    {
        var staging = Path.Combine(_root, "files");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "stale.txt"), "stale");

        var outcome = UpdatePackageInstaller.ExtractTo(BuildZip(PackageEntries), staging);

        Assert.True(outcome.IsSuccess, outcome.Detail);
        Assert.False(File.Exists(Path.Combine(staging, "stale.txt")));
    }

    [Fact]
    public void ExtractTo_rejects_a_package_with_an_unsafe_path()
    {
        var staging = Path.Combine(_root, "files");

        var outcome = UpdatePackageInstaller.ExtractTo(BuildZip("ok.dll", "../escape.dll"), staging);

        Assert.Equal(UpdateStatus.InvalidPayload, outcome.Status);
        Assert.False(File.Exists(Path.Combine(_root, "escape.dll")));
    }

    [Fact]
    public void SavePackage_writes_the_archive_and_reports_its_path()
    {
        var updateRoot = Path.Combine(_root, "update");

        var outcome = UpdatePackageInstaller.SavePackage([1, 2, 3], updateRoot, "pkg.zip");

        Assert.True(outcome.IsSuccess, outcome.Detail);
        Assert.Equal(Path.Combine(updateRoot, "pkg.zip"), outcome.Detail);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(outcome.Detail));
    }

    [Fact]
    public void BuildApplyScript_waits_for_the_game_copies_staging_and_relaunches()
    {
        var script = UpdatePackageInstaller.BuildApplyScript("Among Us.exe", "C:\\stage\\files", "C:\\game", "C:\\game\\Among Us.exe");

        Assert.Contains("IMAGENAME eq Among Us.exe", script);
        Assert.Contains("robocopy \"C:\\stage\\files\" \"C:\\game\"", script);
        Assert.Contains("rmdir /s /q \"C:\\stage\\files\"", script);
        Assert.Contains("start \"\" \"C:\\game\\Among Us.exe\"", script);
        Assert.Contains("exit /b 8", script);
    }

    [Fact]
    public void BuildApplyScript_skips_relaunch_without_an_executable()
    {
        var script = UpdatePackageInstaller.BuildApplyScript("Among Us.exe", "C:\\stage\\files", "C:\\game", null);

        Assert.DoesNotContain("start \"\"", script);
    }

    [Fact]
    public void StageAndSchedule_stages_and_reports_a_success_family_status()
    {
        var updateRoot = Path.Combine(_root, "update");
        var package = BuildZip(PackageEntries);

        var outcome = UpdatePackageInstaller.StageAndSchedule(package, updateRoot, "NoSuchGameProcess12345.exe", Path.Combine(_root, "game"), null);

        Assert.True(outcome.Obtained, outcome.Detail);
        Assert.True(outcome.Status is UpdateStatus.Staged or UpdateStatus.SavedToDisk);
    }

    [Fact]
    public void ResumeOrCleanup_removes_leftover_archives_once_nothing_is_staged()
    {
        var updateRoot = Path.Combine(_root, "update");
        Directory.CreateDirectory(updateRoot);
        var zip = Path.Combine(updateRoot, "pkg.zip");
        File.WriteAllBytes(zip, [1, 2, 3]);
        File.WriteAllText(Path.Combine(updateRoot, UpdatePackageInstaller.ApplyScriptName), "rem");

        UpdatePackageInstaller.ResumeOrCleanup(updateRoot);

        Assert.False(File.Exists(zip));
        Assert.False(File.Exists(Path.Combine(updateRoot, UpdatePackageInstaller.ApplyScriptName)));
    }

    [Fact]
    public void ResumeOrCleanup_keeps_a_staged_update_pending_application()
    {
        var updateRoot = Path.Combine(_root, "update");
        Directory.CreateDirectory(UpdatePackageInstaller.StagingPath(updateRoot));
        File.WriteAllText(Path.Combine(updateRoot, UpdatePackageInstaller.ApplyScriptName), "rem");
        var zip = Path.Combine(updateRoot, "pkg.zip");
        File.WriteAllBytes(zip, [1, 2, 3]);

        UpdatePackageInstaller.ResumeOrCleanup(updateRoot);

        Assert.True(Directory.Exists(UpdatePackageInstaller.StagingPath(updateRoot)));
        Assert.True(File.Exists(Path.Combine(updateRoot, UpdatePackageInstaller.ApplyScriptName)));
        Assert.True(File.Exists(zip));
    }

    [Fact]
    public void ResumeOrCleanup_removes_orphaned_staging_but_keeps_the_saved_package()
    {
        var updateRoot = Path.Combine(_root, "update");
        Directory.CreateDirectory(UpdatePackageInstaller.StagingPath(updateRoot));
        var zip = Path.Combine(updateRoot, "pkg.zip");
        File.WriteAllBytes(zip, [1, 2, 3]);

        UpdatePackageInstaller.ResumeOrCleanup(updateRoot);

        Assert.False(Directory.Exists(UpdatePackageInstaller.StagingPath(updateRoot)));
        Assert.True(File.Exists(zip));
    }

    [Fact]
    public void ResumeOrCleanup_ignores_a_missing_update_folder()
    {
        UpdatePackageInstaller.ResumeOrCleanup(Path.Combine(_root, "does-not-exist"));

        Assert.False(Directory.Exists(Path.Combine(_root, "does-not-exist")));
    }
}
