using HoryTweaks.Core.Updates;
using Xunit;

namespace HoryTweaks.Tests.Updates;

public sealed class UpdateAssemblyInstallerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "HoryTweaksUpdaterTests", Guid.NewGuid().ToString("N"));
    private readonly string _installed;
    private readonly string _staged;
    private readonly string _backup;

    public UpdateAssemblyInstallerTests()
    {
        Directory.CreateDirectory(_directory);
        _installed = Path.Combine(_directory, "HoryTweaks.dll");
        _staged = UpdateAssemblyInstaller.StagingPath(_installed);
        _backup = UpdateAssemblyInstaller.BackupPath(_installed);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Paths_UseExistingSuffixes()
    {
        Assert.Equal(_installed + ".temp", _staged);
        Assert.Equal(_installed + ".old", _backup);
    }

    [Fact]
    public void Stage_WritesPayload()
    {
        var outcome = UpdateAssemblyInstaller.Stage(_staged, [1, 2, 3]);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(_staged));
    }

    [Fact]
    public void Stage_ReportsInstallFailedWhenDirectoryIsMissing()
    {
        var outcome = UpdateAssemblyInstaller.Stage(Path.Combine(_directory, "missing", "HoryTweaks.dll.temp"), [1]);

        Assert.Equal(UpdateStatus.InstallFailed, outcome.Status);
    }

    [Fact]
    public void Install_SwapsFilesAndKeepsBackup()
    {
        File.WriteAllText(_installed, "old");
        File.WriteAllText(_staged, "new");

        var outcome = UpdateAssemblyInstaller.Install(_installed, _staged);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("new", File.ReadAllText(_installed));
        Assert.Equal("old", File.ReadAllText(_backup));
        Assert.False(File.Exists(_staged));
    }

    [Fact]
    public void Install_ReplacesStaleBackup()
    {
        File.WriteAllText(_installed, "old");
        File.WriteAllText(_staged, "new");
        File.WriteAllText(_backup, "stale");

        var outcome = UpdateAssemblyInstaller.Install(_installed, _staged);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("old", File.ReadAllText(_backup));
    }

    [Fact]
    public void Install_FailsWithoutTouchingInstalledWhenStagedIsMissing()
    {
        File.WriteAllText(_installed, "old");

        var outcome = UpdateAssemblyInstaller.Install(_installed, _staged);

        Assert.Equal(UpdateStatus.InstallFailed, outcome.Status);
        Assert.Equal("old", File.ReadAllText(_installed));
        Assert.False(File.Exists(_backup));
    }

    [Fact]
    public void Install_CleansStagedWhenInstalledIsMissing()
    {
        File.WriteAllText(_staged, "new");

        var outcome = UpdateAssemblyInstaller.Install(_installed, _staged);

        Assert.Equal(UpdateStatus.InstallFailed, outcome.Status);
        Assert.False(File.Exists(_staged));
        Assert.False(File.Exists(_installed));
    }

    [Fact]
    public void Install_KeepsInstalledWhenFirstMoveFails()
    {
        File.WriteAllText(_installed, "old");
        File.WriteAllText(_staged, "new");

        var outcome = UpdateAssemblyInstaller.Install(_installed, _staged, (_, _) => throw new IOException("locked"));

        Assert.Equal(UpdateStatus.InstallFailed, outcome.Status);
        Assert.Equal("old", File.ReadAllText(_installed));
        Assert.False(File.Exists(_backup));
        Assert.False(File.Exists(_staged));
    }

    [Fact]
    public void Install_RollsBackWhenSecondMoveFails()
    {
        File.WriteAllText(_installed, "old");
        File.WriteAllText(_staged, "new");

        var outcome = UpdateAssemblyInstaller.Install(_installed, _staged, (source, destination) =>
        {
            if (source == _staged)
            {
                throw new UnauthorizedAccessException("denied");
            }
            File.Move(source, destination);
        });

        Assert.Equal(UpdateStatus.InstallFailed, outcome.Status);
        Assert.Equal("old", File.ReadAllText(_installed));
        Assert.False(File.Exists(_backup));
        Assert.False(File.Exists(_staged));
    }

    [Fact]
    public void Install_ReportsBackupLocationWhenRollbackFails()
    {
        File.WriteAllText(_installed, "old");
        File.WriteAllText(_staged, "new");

        var outcome = UpdateAssemblyInstaller.Install(_installed, _staged, (source, destination) =>
        {
            if (source == _installed)
            {
                File.Move(source, destination);
                return;
            }
            throw new IOException("disk error");
        });

        Assert.Equal(UpdateStatus.InstallFailed, outcome.Status);
        Assert.Contains(_backup, outcome.Detail);
        Assert.Equal("old", File.ReadAllText(_backup));
    }

    [Fact]
    public void CleanupLeftovers_RemovesStagingAndBackupWhenInstalledExists()
    {
        File.WriteAllText(_installed, "current");
        File.WriteAllText(_staged, "partial");
        File.WriteAllText(_backup, "previous");

        UpdateAssemblyInstaller.CleanupLeftovers(_installed);

        Assert.True(File.Exists(_installed));
        Assert.False(File.Exists(_staged));
        Assert.False(File.Exists(_backup));
    }

    [Fact]
    public void CleanupLeftovers_KeepsBackupWhenInstalledIsMissing()
    {
        File.WriteAllText(_staged, "partial");
        File.WriteAllText(_backup, "previous");

        UpdateAssemblyInstaller.CleanupLeftovers(_installed);
        UpdateAssemblyInstaller.CleanupLeftovers(string.Empty);

        Assert.False(File.Exists(_staged));
        Assert.True(File.Exists(_backup));
    }
}
