using BepInEx;
using BepInEx.Unity.IL2CPP;
using BetterAmongUs.Diagnostics;
using BetterAmongUs.Generated;
using BetterAmongUs.Utilities;

namespace BetterAmongUs.Modules;

/// <summary>
/// Checks the installation once at startup for a legacy BetterAmongUs plugin and an unsupported
/// Among Us version, logs the findings and shows them once as a localized, non-destructive popup.
/// </summary>
internal static class StartupCompatibility
{
    private static bool _noticesShown;

    internal static GameVersionStatus GameVersionStatus { get; private set; } = GameVersionStatus.Unknown;
    internal static IReadOnlyList<string> LegacyPluginFiles { get; private set; } = [];
    internal static bool LegacyPluginLoaded { get; private set; }

    internal static bool HasWarnings => LegacyPluginLoaded || LegacyPluginFiles.Count > 0 ||
        GameVersionStatus is GameVersionStatus.Newer or GameVersionStatus.Older;

    internal static void Initialize()
    {
        try
        {
            GameVersionStatus = GameVersionCompatibility.Compare(BAUPlugin.AppVersion, BAUPlugin.ModInfo.SupportedAmongUsVersions);
            LegacyPluginFiles = LegacyPluginDetector.FindLegacyPluginFiles(Paths.PluginPath);
            LegacyPluginLoaded = IL2CPPChainloader.Instance.Plugins.Keys.Any(LegacyPluginDetector.IsLegacyPluginGuid);
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error($"Startup compatibility check failed: {ex}", "Compatibility");
            return;
        }

        var supportedRange = GameVersionCompatibility.FormatSupportedRange(BAUPlugin.ModInfo.SupportedAmongUsVersions);
        switch (GameVersionStatus)
        {
            case GameVersionStatus.Newer:
                BAUPlugin.Logger.Warning($"Among Us {BAUPlugin.AppVersion} is newer than the supported {supportedRange}.", "Compatibility");
                break;
            case GameVersionStatus.Older:
                BAUPlugin.Logger.Warning($"Among Us {BAUPlugin.AppVersion} is older than the supported {supportedRange}.", "Compatibility");
                break;
            case GameVersionStatus.Unknown:
                BAUPlugin.Logger.Warning($"Could not compare Among Us version '{BAUPlugin.AppVersion}' with the supported {supportedRange}.", "Compatibility");
                break;
        }

        foreach (var file in LegacyPluginFiles)
            BAUPlugin.Logger.Warning($"Legacy BetterAmongUs plugin found at '{file}'. Remove it so it does not load beside {BAUPlugin.ModInfo.PLUGIN_NAME}.", "Compatibility");

        if (LegacyPluginLoaded)
            BAUPlugin.Logger.Warning($"Legacy BetterAmongUs plugin ({LegacyPluginDetector.LegacyPluginGuid}) is loaded beside {BAUPlugin.ModInfo.PLUGIN_NAME}.", "Compatibility");
    }

    /// <summary>
    /// Shows the collected warnings in a single popup the first time it is called.
    /// </summary>
    internal static void ShowNoticesOnce()
    {
        if (_noticesShown)
            return;

        var notices = BuildNotices();
        if (notices.Count == 0)
            return;

        _noticesShown = true;

        var text = $"<size=200%>-= <color=#ff2200><b>{TranslationStrings.Startup_Warning_Title.LocalizedString}</b></color> =-</size>\n\n" +
            $"<size=125%>{string.Join("\n\n", notices)}</size>";
        Utils.ShowPopUp(text, enableWordWrapping: true);
    }

    /// <summary>
    /// Builds the localized warning lines, one per detected issue.
    /// </summary>
    internal static List<string> BuildNotices()
    {
        var notices = new List<string>();
        var modName = $"<color=#ffffbe>{BAUPlugin.ModInfo.PLUGIN_NAME} {BAUPlugin.ModInfo.VERSION_STRING}</color>";
        var supportedRange = $"<color=#4f92ff><b>{GameVersionCompatibility.FormatSupportedRange(BAUPlugin.ModInfo.SupportedAmongUsVersions)}</b></color>";
        var currentVersion = $"<color=#4f92ff><b>{BAUPlugin.AppVersion}</b></color>";

        if (GameVersionStatus == GameVersionStatus.Newer)
            notices.Add(TranslationStrings.Startup_GameVersion_Newer.Format(modName, supportedRange, currentVersion));
        else if (GameVersionStatus == GameVersionStatus.Older)
            notices.Add(TranslationStrings.Startup_GameVersion_Older.Format(modName, supportedRange, currentVersion));

        if (LegacyPluginLoaded)
        {
            notices.Add(TranslationStrings.Startup_LegacyPlugin_Loaded.Format(modName));
        }
        else if (LegacyPluginFiles.Count > 0)
        {
            var paths = string.Join("\n", LegacyPluginFiles.Select(DisplayPath));
            notices.Add(TranslationStrings.Startup_LegacyPlugin_Found.Format(modName, $"<color=#b1b1b1>{paths}</color>"));
        }

        return notices;
    }

    private static string DisplayPath(string path)
    {
        try
        {
            return Path.GetRelativePath(Paths.GameRootPath, path);
        }
        catch (Exception)
        {
            return Path.GetFileName(path);
        }
    }
}
