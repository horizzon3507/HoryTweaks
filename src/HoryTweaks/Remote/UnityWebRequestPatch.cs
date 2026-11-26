using HarmonyLib;
using HoryTweaks.Modules.Support;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace HoryTweaks.Remote;

[HarmonyPatch]
internal static class UnityWebRequestPatch
{
    private static string? _cachedHeader;

    /// <summary>
    /// Builds and caches the mod version header string for network transmission.
    /// Format: "major;minor;patch;metadata1/metadata2/metadata3"
    /// </summary>
    public static string GetHeader()
    {
        if (_cachedHeader != null)
            return _cachedHeader;

        var stringBuilder = new StringBuilder();
        var version = BAUPlugin.ModInfo.SemVersion;

        stringBuilder.Append(version.Major);
        stringBuilder.Append(';');
        stringBuilder.Append(version.Minor);
        stringBuilder.Append(';');
        stringBuilder.Append(version.Patch);
        stringBuilder.Append(';');

        var buildMetadata = version.Metadata;
        if (buildMetadata.Length > 0)
        {
            for (int i = 0; i < buildMetadata.Length; i++)
            {
                stringBuilder.Append(buildMetadata[i]);
                if (i < buildMetadata.Length - 1)
                    stringBuilder.Append('/');
            }
        }

        _cachedHeader = stringBuilder.ToString();
        return _cachedHeader;
    }

    private static bool IsGameApiRequest(UnityWebRequest request)
    {
        string? url = request.url;
        return !string.IsNullOrEmpty(url) && url.IndexOf("/api/games", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    [HarmonyPatch(typeof(UnityWebRequest), nameof(UnityWebRequest.SendWebRequest))]
    [HarmonyPrefix]
    private static void UnityWebRequest_SendWebRequest_Prefix(UnityWebRequest __instance)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_BAUHttpHeader))
            return;

        if (IsGameApiRequest(__instance))
        {
            __instance.SetRequestHeader("BAU-Mod", GetHeader());
        }
    }

    [HarmonyPatch(typeof(UnityWebRequest), nameof(UnityWebRequest.SendWebRequest))]
    [HarmonyPostfix]
    private static void UnityWebRequest_SendWebRequest_Postfix(UnityWebRequest __instance, UnityWebRequestAsyncOperation __result)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_BAUHttpHeader))
            return;

        if (IsGameApiRequest(__instance))
        {
            __result.add_completed((Action<AsyncOperation>)(_ =>
            {
                if (!HttpUtils.IsSuccess(__instance.responseCode))
                    return;

                var responseHeader = __instance.GetResponseHeader("BAU-Mod-Processed");
                if (responseHeader != null)
                {
                    BAUPlugin.Logger.Log("Connected to a supported Better Among Us matchmaking server");
                }
            }));
        }
    }
}
