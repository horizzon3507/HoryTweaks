namespace BetterAmongUs.Modules.Updater;

/// <summary>
/// Swaps the installed mod assembly for a staged one, keeping the previous file as a rollback copy.
/// </summary>
internal static class UpdateAssemblyInstaller
{
    internal const string StagingSuffix = ".temp";
    internal const string BackupSuffix = ".old";

    /// <summary>
    /// Path the downloaded assembly is written to before installation.
    /// </summary>
    internal static string StagingPath(string installedPath)
    {
        return installedPath + StagingSuffix;
    }

    /// <summary>
    /// Path the previous assembly is kept at until the next start.
    /// </summary>
    internal static string BackupPath(string installedPath)
    {
        return installedPath + BackupSuffix;
    }

    /// <summary>
    /// Writes the validated payload to the staging path, replacing any stale staging file.
    /// </summary>
    internal static UpdateOutcome Stage(string stagedPath, byte[] payload)
    {
        try
        {
            File.WriteAllBytes(stagedPath, payload);
            return UpdateOutcome.Success($"Staged {payload.Length} bytes at '{stagedPath}'.");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
        {
            TryDelete(stagedPath);
            return UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"Could not write the staged file '{stagedPath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Moves the installed assembly to its backup path and the staged assembly into place.
    /// If the second move fails the backup is moved back, so the installed path always holds a working file.
    /// </summary>
    internal static UpdateOutcome Install(string installedPath, string stagedPath)
    {
        return Install(installedPath, stagedPath, File.Move);
    }

    internal static UpdateOutcome Install(string installedPath, string stagedPath, Action<string, string> move)
    {
        if (!File.Exists(stagedPath))
        {
            return UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"The staged file '{stagedPath}' does not exist.");
        }

        if (!File.Exists(installedPath))
        {
            TryDelete(stagedPath);
            return UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"The installed file '{installedPath}' does not exist.");
        }

        var backupPath = BackupPath(installedPath);
        if (File.Exists(backupPath) && !TryDelete(backupPath))
        {
            TryDelete(stagedPath);
            return UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"The previous backup '{backupPath}' is in use and could not be removed.");
        }

        try
        {
            move(installedPath, backupPath);
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            TryDelete(stagedPath);
            return UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"Could not move the installed file aside: {ex.Message}");
        }

        try
        {
            move(stagedPath, installedPath);
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            try
            {
                move(backupPath, installedPath);
            }
            catch (Exception rollbackEx) when (IsFileSystemError(rollbackEx))
            {
                return UpdateOutcome.Failure(UpdateStatus.InstallFailed,
                    $"Could not install the new file ({ex.Message}) and rollback failed ({rollbackEx.Message}). The previous file is at '{backupPath}'.");
            }

            TryDelete(stagedPath);
            return UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"Could not install the new file, previous version restored: {ex.Message}");
        }

        return UpdateOutcome.Success($"Installed '{installedPath}', previous version kept at '{backupPath}'.");
    }

    /// <summary>
    /// Removes leftover staging and backup files. The backup is only removed while an installed file exists,
    /// so a failed rollback never loses the last working copy.
    /// </summary>
    internal static void CleanupLeftovers(string installedPath)
    {
        if (string.IsNullOrEmpty(installedPath))
        {
            return;
        }

        TryDelete(StagingPath(installedPath));
        if (File.Exists(installedPath))
        {
            TryDelete(BackupPath(installedPath));
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return true;
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            return false;
        }
    }

    private static bool IsFileSystemError(Exception ex)
    {
        return ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is ArgumentException;
    }
}
