using BepInEx;
using BetterAmongUs.Attributes;
using BetterAmongUs.Commands.Arguments;
using BetterAmongUs.Data;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Diagnostics;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules;
using BetterAmongUs.Network;
using BetterAmongUs.Utilities;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BetterAmongUs.Commands;

[RegisterCommand]
internal sealed class DumpCommand : BaseCommand
{
    private const int RecentLogLines = 300;
    private const string UploadArg = "upload";
    private const float UploadConfirmWindowSeconds = 15f;

    private static float uploadArmedUntil;
    private static bool uploadConfirmed;

    private readonly StringArgument _modeArgument;

    public DumpCommand()
    {
        _modeArgument = new StringArgument(this, "{upload}") { ArgSuggestions = () => [UploadArg] };
        Arguments = [_modeArgument];
    }

    internal override string Name => "dump";
    internal override string Description => TranslationStrings.Command_Dump_Description.LocalizedString;

    internal override bool CanRunCommand(out string reason)
    {
        if (GameState.IsInGamePlay)
        {
            reason = TranslationStrings.Command_Error_LobbyOnly.LocalizedString;
            return false;
        }

        return base.CanRunCommand(out reason);
    }

    internal override void Run()
    {
        _modeArgument.TryParse(out var mode);
        bool uploadRequested = mode.Equals(UploadArg, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(mode) && !uploadRequested)
        {
            CommandErrorText(TranslationStrings.Command_Error_InvalidSyntax.LocalizedString);
            return;
        }

        if (uploadRequested && !ConfirmUpload())
            return;

        string bepInExLog = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
        if (!File.Exists(bepInExLog))
        {
            CommandErrorText(TranslationStrings.Command_Error_LogNotFound.LocalizedString);
            return;
        }

        string log;
        using (FileStream fileStream = new(bepInExLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (StreamReader reader = new(fileStream))
        {
            log = reader.ReadToEnd();
        }

        string decryptedLog = BAULogger.DecryptLogs(log);
        string timestamp = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");
        string logFileName = "log-" + BAUPlugin.ModInfo.VERSION_STRING + "-" + timestamp + "-bepinex" + ".log";
        string reportFileName = "report-" + BAUPlugin.ModInfo.VERSION_STRING + "-" + timestamp + "-diagnostics" + ".txt";
        string reportFilePath;

        if (!BAUPlugin.ModInfo.Starlight)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string logFolderPath = Path.Combine(desktopPath, "HoryTweaksLogDumps");

            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }

            File.WriteAllText(Path.Combine(logFolderPath, logFileName), decryptedLog);
            reportFilePath = Path.Combine(logFolderPath, reportFileName);
            File.WriteAllText(reportFilePath, BuildDiagnosticReport(log));

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = logFolderPath,
                UseShellExecute = true,
                Verb = "open"
            });

            CommandResultText(TranslationStrings.Command_Dump_Success.Format(logFolderPath));
            CommandResultText(TranslationStrings.Command_Dump_ReportSaved.Format(reportFileName));
        }
        else
        {
            string dataPath = BetterDataManager.Folders.fileFolderPath;
            string logFolderPath = Path.Combine(dataPath, "HoryTweaksLogDumps");
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }

            File.WriteAllText(Path.Combine(logFolderPath, logFileName), decryptedLog);
            reportFilePath = Path.Combine(logFolderPath, reportFileName);
            File.WriteAllText(reportFilePath, BuildDiagnosticReport(log));

            CommandResultText(TranslationStrings.Command_Dump_Success.Format(logFolderPath));
            CommandResultText(TranslationStrings.Command_Dump_ReportSaved.Format(reportFileName));
        }

        if (uploadRequested)
            StartUpload(reportFilePath);
    }

    /// <summary>
    /// Upload is an explicit opt-in: the first "/dump upload" of a session only warns
    /// and arms a short window (the same arm-then-confirm pattern the restore-defaults
    /// row uses), a second one inside the window confirms. Once confirmed, later uploads
    /// in the session run without warning.
    /// </summary>
    private bool ConfirmUpload()
    {
        if (uploadConfirmed)
            return true;

        if (Time.unscaledTime < uploadArmedUntil)
        {
            uploadConfirmed = true;
            uploadArmedUntil = 0f;
            return true;
        }

        uploadArmedUntil = Time.unscaledTime + UploadConfirmWindowSeconds;
        CommandResultText(TranslationStrings.Dump_Upload_Confirm.LocalizedString);
        return false;
    }

    private void StartUpload(string reportFilePath)
    {
        if (AmongUsClient.Instance == null)
        {
            CommandErrorText(TranslationStrings.Dump_Upload_Failed.Format("no network client"));
            return;
        }

        CommandResultText(TranslationStrings.Dump_Upload_InProgress.LocalizedString);
        AmongUsClient.Instance.StartCoroutine(ReportUploader.CoUpload(reportFilePath, (url, error) =>
        {
            if (url != null)
            {
                GUIUtility.systemCopyBuffer = url;
                CommandResultText(TranslationStrings.Dump_Upload_Success.Format(url));
            }
            else
            {
                CommandErrorText(TranslationStrings.Dump_Upload_Failed.Format(error ?? "unknown error"));
            }
        }));
    }

    /// <summary>
    /// Builds the shareable diagnostic report. Private (encrypted) log entries are omitted rather than
    /// decrypted, and the whole report is redacted before it is returned.
    /// </summary>
    internal static string BuildDiagnosticReport(string rawLog)
    {
        var report = new DiagnosticReportBuilder($"{BAUPlugin.ModInfo.PLUGIN_NAME} diagnostic report")
            .Sensitive(Environment.UserName);

        report.Section("Report")
            .Entry("Generated (UTC)", DateTime.UtcNow.ToString("O"))
            .Entry("Contains", "mod, game and platform info, compatibility checks, non-secret client options and recent redacted log lines")
            .Entry("Excludes", "lobby codes, friend codes, chat, player names, ban lists, presets and encryption keys");

        report.Section("Mod")
            .Entry("Name", BAUPlugin.ModInfo.PLUGIN_NAME)
            .Entry("Version", BAUPlugin.ModInfo.VERSION_STRING)
            .Entry("Build date", BAUPlugin.ModInfo.BuildDate)
            .Entry("Commit", BAUPlugin.ModInfo.CommitHash)
            .Entry("Plugin GUID", BAUPlugin.ModInfo.PLUGIN_GUID);

        report.Section("Game")
            .Entry("Among Us version", BAUPlugin.AppVersion)
            .Entry("User-facing version", SafeValue(() => BAUPlugin.AmongUsVersion))
            .Entry("Platform", SafeValue(() => Utils.GetPlatformName(BAUPlugin.PlatformData.Platform)))
            .Entry("Starlight", BAUPlugin.ModInfo.Starlight)
            .Entry("Unity", SafeValue(() => Application.unityVersion))
            .Entry("Operating system", RuntimeInformation.OSDescription)
            .Entry("Architecture", RuntimeInformation.ProcessArchitecture)
            .Entry("Runtime", RuntimeInformation.FrameworkDescription)
            .Entry("BepInEx", SafeValue(() => Paths.BepInExVersion.ToString()));

        report.Section("Compatibility")
            .Entry("Supported Among Us versions", GameVersionCompatibility.FormatSupportedRange(BAUPlugin.ModInfo.SupportedAmongUsVersions))
            .Entry("Game version status", StartupCompatibility.GameVersionStatus)
            .Entry("Legacy BetterAmongUs.dll files", StartupCompatibility.LegacyPluginFiles.Count == 0 ? "none" : string.Join("; ", StartupCompatibility.LegacyPluginFiles))
            .Entry("Legacy BetterAmongUs plugin loaded", StartupCompatibility.LegacyPluginLoaded)
            .Entry("Loaded plugins", SafeValue(() => string.Join("; ", IL2CPPChainloader.Instance.Plugins.Values
                .Select(plugin => $"{plugin.Metadata.GUID} {plugin.Metadata.Version}")
                .OrderBy(entry => entry, StringComparer.Ordinal))));

        report.Section("Client options")
            .Entry("SendBetterRpc", BAUConfigs.SendBetterRpc.Value)
            .Entry("BetterNotifications", BAUConfigs.BetterNotifications.Value)
            .Entry("ForceOwnLanguage", BAUConfigs.ForceOwnLanguage.Value)
            .Entry("ChatDarkMode", BAUConfigs.ChatDarkMode.Value)
            .Entry("HideConsole", BAUConfigs.HideConsole.Value)
            .Entry("LobbyPlayerInfo", BAUConfigs.LobbyPlayerInfo.Value)
            .Entry("LessInfo", BAUConfigs.LessInfo.Value)
            .Entry("DisableLobbyTheme", BAUConfigs.DisableLobbyTheme.Value)
            .Entry("UnlockFPS", BAUConfigs.UnlockFPS.Value)
            .Entry("ShowFPS", BAUConfigs.ShowFPS.Value)
            .Entry("MinimapIcons", BAUConfigs.MinimapIcons.Value)
            .Entry("BetterMinimapColors", BAUConfigs.BetterMinimapColors.Value)
            .Entry("VentColorGroups", BAUConfigs.VentColorGroups.Value)
            .Entry("BetterColorblindText", BAUConfigs.BetterColorblindText.Value)
            .Entry("ColorblindTextOnTop", BAUConfigs.ColorblindTextOnTop.Value)
            .Entry("CompressSettingFiles", BAUConfigs.CompressSettingFiles.Value)
            .Entry("CommandPrefix", BAUConfigs.CommandPrefix.Value)
            .Entry("SettingsPreset", BAUConfigs.SettingsPreset.Value);

        report.Section("Data")
            .Entry("Better_Data folder present", SafeValue(() => Directory.Exists(BetterDataManager.Folders.fileFolderPath)))
            .Entry("Legacy config migrated", SafeValue(() => File.Exists($"{BAUPlugin.Instance!.Config.ConfigFilePath}.legacy-migration-complete")));

        var recentLog = DiagnosticRedactor.OmitPrivateEntries(rawLog, BAUPlugin.Constants.ENCRYPTED_LOG_PREFIX, BAUPlugin.Constants.ENCRYPTED_LOG_POSTFIX);
        report.Section($"Recent log (last {RecentLogLines} lines)")
            .Lines(DiagnosticRedactor.TakeRecentLines(recentLog, RecentLogLines));

        return report.Build();
    }

    private static string SafeValue<T>(Func<T> getter)
    {
        try
        {
            var value = getter();
            return value == null ? "-" : value.ToString() ?? "-";
        }
        catch (Exception)
        {
            return "unavailable";
        }
    }
}