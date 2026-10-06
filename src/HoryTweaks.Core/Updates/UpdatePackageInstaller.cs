using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace BetterAmongUs.Core.Updates;

/// <summary>
/// Stages a verified release package under the writable data folder and schedules its
/// application for the moment the game exits: a detached helper script waits until no
/// game process is running, copies the staged files over the game directory and
/// relaunches the executable. The running game never has to overwrite its own files.
/// </summary>
internal static class UpdatePackageInstaller
{
    /// <summary>
    /// Folder inside the user data directory that holds staged update content.
    /// </summary>
    internal const string UpdateFolderName = "HoryTweaksUpdate";

    /// <summary>
    /// Folder inside the update folder holding the extracted package contents.
    /// </summary>
    internal const string StagingFolderName = "files";

    /// <summary>
    /// Helper script that applies the staged files once no game process is running.
    /// </summary>
    internal const string ApplyScriptName = "apply-update.cmd";

    /// <summary>
    /// Directory holding staged files for the pending update.
    /// </summary>
    internal static string StagingPath(string updateRoot)
    {
        return Path.Combine(updateRoot, StagingFolderName);
    }

    /// <summary>
    /// Full path of the helper script inside the update folder.
    /// </summary>
    internal static string ApplyScriptPath(string updateRoot)
    {
        return Path.Combine(updateRoot, ApplyScriptName);
    }

    /// <summary>
    /// Extracts a validated package to <paramref name="stagingDir"/>, recreating the folder so a
    /// partially staged earlier attempt never contaminates the new payload.
    /// </summary>
    internal static UpdateOutcome ExtractTo(byte[] package, string stagingDir)
    {
        string stagingRoot;
        try
        {
            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, recursive: true);
            }

            Directory.CreateDirectory(stagingDir);
            stagingRoot = Path.GetFullPath(stagingDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            return UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"Could not reset the staging folder '{stagingDir}': {ex.Message}");
        }

        try
        {
            using var stream = new MemoryStream(package, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                if (!UpdatePackageValidator.IsSafeEntryName(entry.FullName))
                {
                    return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The package contains the unsafe path '{entry.FullName}'.");
                }

                var destination = Path.GetFullPath(Path.Combine(stagingRoot, UpdatePackageValidator.NormalizeEntryName(entry.FullName)));
                if (!destination.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The entry '{entry.FullName}' would leave the staging folder.");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is NotSupportedException)
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The package could not be extracted: {ex.Message}");
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            return UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"Could not stage the package files in '{stagingDir}': {ex.Message}");
        }

        return UpdateOutcome.Success($"Staged the package contents in '{stagingDir}'.");
    }

    /// <summary>
    /// Writes the verified package to <paramref name="updateRoot"/> so it stays available
    /// for a manual install whenever the scheduled apply cannot run. The outcome detail
    /// carries the saved file path on success.
    /// </summary>
    internal static UpdateOutcome SavePackage(byte[] package, string updateRoot, string fileName)
    {
        try
        {
            Directory.CreateDirectory(updateRoot);
            var packagePath = Path.Combine(updateRoot, fileName);
            File.WriteAllBytes(packagePath, package);
            return UpdateOutcome.Success(packagePath);
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            return UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"Could not save the package file: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the helper script that waits until no <paramref name="imageName"/> process is
    /// running, copies the staged files over the game directory and relaunches the
    /// executable. A failed copy keeps the staging folder and the script so the apply is
    /// retried on the next launch instead of leaving a half-updated install behind.
    /// </summary>
    /// <param name="imageName">Image name the helper waits on, for example <c>Among Us.exe</c>.</param>
    /// <param name="stagingDir">Folder holding the extracted package contents.</param>
    /// <param name="gameRoot">Game directory the files are copied over.</param>
    /// <param name="executablePath">Game executable relaunched after a successful copy.</param>
    internal static string BuildApplyScript(string imageName, string stagingDir, string gameRoot, string? executablePath)
    {
        var sb = new StringBuilder();
        sb.Append("@echo off\r\n");
        sb.Append("chcp 65001 >nul\r\n");
        sb.Append(":wait\r\n");
        sb.Append("timeout /t 2 /nobreak >nul\r\n");
        sb.Append("tasklist /FI \"IMAGENAME eq ").Append(imageName).Append("\" | findstr /C:\"").Append(imageName).Append("\" >nul\r\n");
        sb.Append("if not errorlevel 1 goto wait\r\n");
        sb.Append("timeout /t 2 /nobreak >nul\r\n");
        sb.Append("robocopy \"").Append(stagingDir).Append("\" \"").Append(gameRoot).Append("\" /E /R:5 /W:2 /NFL /NDL /NJH /NJS\r\n");
        sb.Append("if errorlevel 8 (\r\n");
        sb.Append("    echo HoryTweaks update could not be applied; it will be retried on the next launch.\r\n");
        sb.Append("    exit /b 8\r\n");
        sb.Append(")\r\n");
        sb.Append("rmdir /s /q \"").Append(stagingDir).Append("\"\r\n");
        if (!string.IsNullOrEmpty(executablePath))
        {
            sb.Append("start \"\" \"").Append(executablePath).Append("\"\r\n");
        }

        sb.Append("(goto) 2>nul & del \"%~f0\"\r\n");
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the package, writes the helper script and launches it detached.
    /// Returns <see cref="UpdateStatus.Staged"/> when the apply is scheduled and
    /// <see cref="UpdateStatus.SavedToDisk"/> when the files are staged but no helper
    /// could be started, so the user has to apply the saved package by hand.
    /// </summary>
    internal static UpdateOutcome StageAndSchedule(byte[] package, string updateRoot, string imageName, string gameRoot, string? executablePath)
    {
        var stagingDir = StagingPath(updateRoot);
        var extract = ExtractTo(package, stagingDir);
        if (!extract.IsSuccess)
        {
            return extract;
        }

        var scriptPath = ApplyScriptPath(updateRoot);
        try
        {
            File.WriteAllText(scriptPath, BuildApplyScript(imageName, stagingDir, gameRoot, executablePath), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            return UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"Could not write the update helper script '{scriptPath}': {ex.Message}");
        }

        if (!TryLaunchHelper(scriptPath))
        {
            return new UpdateOutcome(UpdateStatus.SavedToDisk, $"Staged the update in '{stagingDir}' but the helper script could not be started.");
        }

        return new UpdateOutcome(UpdateStatus.Staged, $"Update staged in '{stagingDir}'; it is applied when the game exits.");
    }

    /// <summary>
    /// Called on every launch: re-launches the helper when staged files are still waiting
    /// (the previous helper may have died with a reboot or a failed copy), and clears
    /// leftover staging content, archives and scripts once nothing remains to apply.
    /// </summary>
    internal static void ResumeOrCleanup(string updateRoot)
    {
        try
        {
            if (!Directory.Exists(updateRoot))
            {
                return;
            }

            var stagingExists = Directory.Exists(StagingPath(updateRoot));
            var scriptExists = File.Exists(ApplyScriptPath(updateRoot));
            if (stagingExists && scriptExists)
            {
                TryLaunchHelper(ApplyScriptPath(updateRoot));
                return;
            }

            if (stagingExists)
            {
                Directory.Delete(StagingPath(updateRoot), recursive: true);
            }
            else
            {
                foreach (var leftover in Directory.EnumerateFiles(updateRoot, "*.zip"))
                {
                    TryDelete(leftover);
                }
            }

            TryDelete(ApplyScriptPath(updateRoot));
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            // Leftover update files are cosmetic; the next successful update rewrites them.
        }
    }

    /// <summary>
    /// Starts the helper script detached from the game. Lives behind a small wrapper so the
    /// staging flow stays testable.
    /// </summary>
    internal static bool TryLaunchHelper(string scriptPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{scriptPath}\"\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? string.Empty,
            });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException || ex is IOException || ex is NotSupportedException || ex is PlatformNotSupportedException)
        {
            return false;
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
