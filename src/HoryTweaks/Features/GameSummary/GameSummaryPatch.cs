using HoryTweaks.Generated;

using HoryTweaks.Modules.Support;

using HoryTweaks.Utilities;

using HarmonyLib;
using System.Text;
using TMPro;
using UnityEngine;
using HoryTweaks.Core.Formatting;
using HoryTweaks.Game;
using HoryTweaks.Localization;
using HoryTweaks.Game.Players;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Features.MatchHistory;

namespace HoryTweaks.Features.GameSummary;

[HarmonyPatch]
internal static class GameSummaryPatch
{
    internal static string? LatestSummaryMarkdown { get; private set; }

    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
    [HarmonyPostfix]
    private static void EndGameManager_SetEverythingUp_Postfix(EndGameManager __instance)
    {
        // Check for null references
        if (__instance == null)
            return;

        // Save the match recording now that the match is over
        MatchRecorder.Finish();

        // Build and cache a clean Discord-ready summary before visual creation
        LatestSummaryMarkdown = BuildMarkdownSummary();

        // Log game end to console
        LogGameEnd();

        // Create visual summary unless disabled
        if (!BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_EndGameSummary))
        {
            CreateGameSummary(__instance);
        }
    }

    // Log game end info to console
    private static void LogGameEnd()
    {
        BAUPlugin.Logger.LogHeader($"Game Has Ended - {Enum.GetName(typeof(MapNames), GameState.GetActiveMapId)}/{GameState.GetActiveMapId}", "GamePlayManager");
        BAUPlugin.Logger.LogHeader("Game Summary Start", "GameSummary");
    }

    // Creates the visual game summary on screen
    private static void CreateGameSummary(EndGameManager endGameManager)
    {
        var summaryObject = CreateSummaryObject(endGameManager);
        var summaryText = summaryObject.GetComponent<TextMeshPro>();

        if (summaryText == null)
            return;

        ConfigureSummaryText(summaryText);

        // Get win condition info
        var (winTeam, winTag, winColor) = GetWinInfo(EndGameResult.CachedGameOverReason);
        BAUPlugin.Logger.Log($"{winTeam}: {winTag}", "GameSummary");

        // Build and display summary text
        var summaryHeader = BuildSummaryHeader(winTeam, winTag, winColor);
        var playerList = BuildPlayerList();

        summaryText.text = $"{summaryHeader}\n\n<size=58%>{playerList}</size>";
        CreateCopySummaryButton(endGameManager, summaryObject.transform);
        BAUPlugin.Logger.LogHeader("Game Summary End", "GameSummary");
    }

    private static void CreateCopySummaryButton(EndGameManager endGameManager, Transform summaryTransform)
    {
        if (string.IsNullOrWhiteSpace(LatestSummaryMarkdown) || endGameManager.WinText == null)
            return;

        try
        {
            var buttonObj = UnityEngine.Object.Instantiate(endGameManager.WinText.gameObject, summaryTransform.parent);
            buttonObj.name = "CopySummaryButton (TMP)";
            buttonObj.DestroyTextTranslators();

            var camera = HudManager.InstanceExists
                ? HudManager.Instance.GetComponentInChildren<Camera>()
                : Camera.main;

            buttonObj.transform.position = AspectPosition.ComputeWorldPosition(
                camera,
                AspectPosition.EdgeAlignments.LeftBottom,
                new Vector3(1.1f, 0.45f, -5f));
            buttonObj.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);

            var label = buttonObj.GetComponent<TextMeshPro>();
            if (label == null)
                return;

            label.autoSizeTextContainer = false;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Left;
            label.color = new Color(1f, 1f, 0.75f, 1f);
            label.text = $"[ {TranslationStrings.MatchHistory_CopySummary.LocalizedString} ]";

            var collider = buttonObj.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = buttonObj.AddComponent<BoxCollider2D>();
            }
            collider.size = new Vector2(12f, 2.2f);
            collider.offset = new Vector2(4.5f, 0f);

            var button = buttonObj.GetComponent<PassiveButton>();
            if (button == null)
            {
                button = buttonObj.AddComponent<PassiveButton>();
            }
            button.Colliders = new Collider2D[] { collider };
            button.OnClick = new();
            button.OnMouseOver = new();
            button.OnMouseOut = new();
            button.OnMouseOver.AddListener(() => label.color = Color.white);
            button.OnMouseOut.AddListener(() => label.color = new Color(1f, 1f, 0.75f, 1f));
            button.OnClick.AddListener(() =>
            {
                if (!string.IsNullOrWhiteSpace(LatestSummaryMarkdown))
                {
                    GUIUtility.systemCopyBuffer = LatestSummaryMarkdown;
                    label.text = $"[ {TranslationStrings.MatchHistory_SummaryCopied.LocalizedString} ]";
                }
            });
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "GameSummary");
        }
    }

    private static string? BuildMarkdownSummary()
    {
        try
        {
            if (GameData.Instance == null || GameData.Instance.AllPlayers == null)
                return null;

            var (winTeam, winTag, _) = GetWinInfo(EndGameResult.CachedGameOverReason);
            string mapName = Enum.GetName(typeof(MapNames), (int)GameState.GetActiveMapId) ?? "?";
            var players = new List<MatchSummaryPlayer>();

            foreach (var playerData in GetSortedPlayers())
            {
                if (playerData == null || playerData.Role == null)
                    continue;

                var extended = playerData.ExtendedData();
                string name = extended != null ? extended.RealName : playerData.PlayerName;
                string roleName = playerData.RoleType.GetRoleName();
                bool isImpostor = playerData.Role.IsImpostor;
                int kills = extended != null && extended.RoleInfo != null ? extended.RoleInfo.Kills : 0;
                int done = playerData.Tasks != null ? playerData.Tasks.WhereIl2Cpp(t => t.Complete).Count : 0;
                int total = playerData.Tasks != null ? playerData.Tasks.Count : 0;
                string status = playerData.Disconnected
                    ? TranslationStrings.DC.LocalizedString
                    : playerData.IsDead
                        ? TranslationStrings.Dead.LocalizedString
                        : TranslationStrings.Alive.LocalizedString;

                players.Add(new MatchSummaryPlayer(name, roleName, isImpostor, kills, done, total, status));
            }

            return MatchSummaryFormatter.Format(
                mapName,
                winTeam,
                winTag,
                durationMs: null,
                players,
                CreateSummaryLabels());
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "GameSummary");
            return null;
        }
    }

    internal static MatchSummaryLabels CreateSummaryLabels()
    {
        return new MatchSummaryLabels(
            Translator.GetString(StringNames.ImpostorsCategory),
            Translator.GetString(StringNames.Crewmates),
            TranslationStrings.Kills.LocalizedString,
            TranslationStrings.Tasks.LocalizedString);
    }

    // Creates the text object for the summary
    private static GameObject CreateSummaryObject(EndGameManager endGameManager)
    {
        var summaryObject = UnityEngine.Object.Instantiate(
            endGameManager.WinText.gameObject,
            endGameManager.WinText.transform.parent
        );

        summaryObject.name = "SummaryObj (TMP)";
        summaryObject.transform.SetSiblingIndex(0);

        // Position in top-left corner
        var camera = HudManager.InstanceExists
            ? HudManager.Instance.GetComponentInChildren<Camera>()
            : Camera.main;

        var position = AspectPosition.ComputeWorldPosition(
            camera,
            AspectPosition.EdgeAlignments.LeftTop,
            new Vector3(1f, 0.2f, -5f)
        );

        summaryObject.transform.position = position;
        summaryObject.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);

        return summaryObject;
    }

    // Sets text properties for the summary
    private static void ConfigureSummaryText(TextMeshPro text)
    {
        text.autoSizeTextContainer = false;
        text.enableAutoSizing = false;
        text.lineSpacing = -25f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
    }

    // Determines which team won and how
    internal static (string Team, string Tag, string Color) GetWinInfo(GameOverReason reason)
    {
        return reason switch
        {
            // Classic mode wins
            GameOverReason.CrewmatesByTask => (
                Translator.GetString(StringNames.Crewmates),
                TranslationStrings.Game_Summary_Result_TasksCompletion.LocalizedString,
                Colors.CrewmateBlue.ColorToHex()
            ),
            GameOverReason.CrewmatesByVote => (
                Translator.GetString(StringNames.Crewmates),
                TranslationStrings.Game_Summary_Result_ImpostersVotedOut.LocalizedString,
                Colors.CrewmateBlue.ColorToHex()
            ),
            GameOverReason.ImpostorDisconnect => (
                Translator.GetString(StringNames.Crewmates),
                TranslationStrings.Game_Summary_Result_ImpostorsDisconnected.LocalizedString,
                Colors.CrewmateBlue.ColorToHex()
            ),
            GameOverReason.ImpostorsByKill => (
                Translator.GetString(StringNames.ImpostorsCategory),
                TranslationStrings.Game_Summary_Result_CrewOutnumbered.LocalizedString,
                Colors.ImpostorRed.ColorToHex()
            ),
            GameOverReason.ImpostorsBySabotage => (
                Translator.GetString(StringNames.ImpostorsCategory),
                TranslationStrings.Game_Summary_Result_Sabotage.LocalizedString,
                Colors.ImpostorRed.ColorToHex()
            ),
            GameOverReason.ImpostorsByVote => (
                Translator.GetString(StringNames.ImpostorsCategory),
                TranslationStrings.Game_Summary_Result_CrewOutnumbered.LocalizedString,
                Colors.ImpostorRed.ColorToHex()
            ),
            GameOverReason.CrewmateDisconnect => (
                Translator.GetString(StringNames.ImpostorsCategory),
                TranslationStrings.Game_Summary_Result_CrematesDisconnected.LocalizedString,
                Colors.ImpostorRed.ColorToHex()
            ),

            // Hide & Seek mode wins
            GameOverReason.HideAndSeek_CrewmatesByTimer => (
                TranslationStrings.Game_Summary_Hiders.LocalizedString,
                TranslationStrings.Game_Summary_Result_TimeOut.LocalizedString,
                Colors.CrewmateBlue.ColorToHex()
            ),
            GameOverReason.HideAndSeek_ImpostorsByKills => (
                TranslationStrings.Game_Summary_Seekers.LocalizedString,
                TranslationStrings.Game_Summary_Result_NoSurvivors.LocalizedString,
                Colors.ImpostorRed.ColorToHex()
            ),

            // Fallback for unknown win conditions
            _ => ("Unknown", "Unknown", "#ffffff")
        };
    }

    // Builds the header text with win info
    private static string BuildSummaryHeader(string winTeam, string winTag, string winColor)
    {
        return $"<align=\"center\"><size=150%>   {TranslationStrings.GameSummary}</size></align>" +
               $"\n\n<size=90%><color={winColor}>{winTeam} {TranslationStrings.Game_Summary_Won}</color></size>" +
               $"\n<size=60%>\n{TranslationStrings.Game_Summary_By} {winTag}</size>";
    }

    // Sorts players for display: disconnected first, then dead, then alive
    private static NetworkedPlayerInfo[] GetSortedPlayers()
    {
        return GameData.Instance.AllPlayers
            .ToArray()
            .OrderBy(p => p.Disconnected)
            .ThenBy(p => p.IsDead)
            .ThenBy(p => !p.Role.IsImpostor)
            .ToArray();
    }

    // Builds the complete player list text
    private static StringBuilder BuildPlayerList()
    {
        var playersData = GetSortedPlayers();
        var stringBuilder = new StringBuilder();

        foreach (var playerData in playersData)
        {
            if (playerData == null)
                continue;

            var playerLine = BuildPlayerLine(playerData);
            stringBuilder.AppendLine($"- {playerLine}\n");
            BAUPlugin.Logger.Log(Utils.RemoveHtmlText(playerLine).Replace("\n", " "), "GameSummary");
        }

        return stringBuilder;
    }

    // Builds a single player line with name, role info, and status
    private static string BuildPlayerLine(NetworkedPlayerInfo playerData)
    {
        var name = $"<color={Utils.Color32ToHex(Palette.PlayerColors[playerData.DefaultOutfit.ColorId])}>{playerData.ExtendedData().RealName}</color>";
        var roleInfo = BuildRoleInfo(playerData);
        var deathReason = BuildDeathReason(playerData);

        return $"{name} {roleInfo} {deathReason}";
    }

    // Builds role info with stats (kills for impostors, tasks for crew)
    private static string BuildRoleInfo(NetworkedPlayerInfo playerData)
    {
        var themeColor = Utils.GetTeamHexColor(playerData.Role.TeamType);
        var theme = (string text) => $"<color={themeColor}>{text}</color>";

        var roleName = theme(playerData.RoleType.GetRoleName());

        if (playerData.Role.IsImpostor)
        {
            var kills = playerData.ExtendedData().RoleInfo.Kills;
            return $"({roleName}) → {theme($"{TranslationStrings.Kills}: {kills}")}";
        }

        var completedTasks = playerData.Tasks.WhereIl2Cpp(task => task.Complete).Count;
        var totalTasks = playerData.Tasks.Count;
        return $"({roleName}) → {theme($"{TranslationStrings.Tasks}: {completedTasks}/{totalTasks}")}";
    }

    // Builds player status (DC/Dead/Alive)
    private static string BuildDeathReason(NetworkedPlayerInfo playerData)
    {
        if (playerData.Disconnected)
            return $"『<color=#838383><b>{TranslationStrings.DC}</b></color>』";

        if (!playerData.IsDead)
            return $"『<color=#80ff00><b>{TranslationStrings.Alive}</b></color>』";

        if (playerData.IsDead)
            return $"『<color=#ff0600><b>{TranslationStrings.Dead}</b></color>』";

        return $"『<color=#838383><b>Unknown</b></color>』";
    }
}