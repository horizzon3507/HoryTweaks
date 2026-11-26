using HoryTweaks.Generated;

using HoryTweaks.Utilities;
using TMPro;
using UnityEngine;
using HoryTweaks.Core.Moderation;
using HoryTweaks.Features.ClientOptions;
using HoryTweaks.Features.Hud;
using HoryTweaks.Game;
using HoryTweaks.Infrastructure.UnityInterop;

namespace HoryTweaks.Features.Moderation;

/// <summary>
/// Host moderation page inside the Tweaks options tab: current players, kick/ban with confirmation,
/// ban list access and the kick/ban history of this session.
/// </summary>
internal static class ModerationCenterMenu
{
    private const float ConfirmWindowSeconds = 8f;
    private const float SmallButtonScale = 0.6f;
    private const int Columns = 3;
    private const int PlayerRows = 5;
    private const int HistoryRows = 4;

    private static GameObject? root;
    private static ToggleButtonBehaviour? template;
    private static TextMeshPro? statusText;
    private static TextMeshPro? historyText;
    private static ToggleButtonBehaviour? kickButton;
    private static ToggleButtonBehaviour? banButton;
    private static readonly List<GameObject> hiddenSiblings = [];
    private static readonly List<(byte PlayerId, ToggleButtonBehaviour Button)> playerButtons = [];

    private static byte? selectedPlayerId;
    private static ModerationActionKind? pendingAction;
    private static float pendingUntil;

    /// <summary>
    /// Drops references to the previous options menu. Called when a new OptionsMenuBehaviour starts.
    /// </summary>
    internal static void Reset()
    {
        root = null;
        template = null;
        statusText = null;
        historyText = null;
        kickButton = null;
        banButton = null;
        hiddenSiblings.Clear();
        playerButtons.Clear();
        selectedPlayerId = null;
        pendingAction = null;
    }

    internal static void Open(OptionsMenuBehaviour optionsMenu)
    {
        var tab = OptionsMenuBehaviourPatch.BetterOptionsTab;
        if (tab == null || tab.Content == null)
            return;

        if (root == null)
        {
            Build(optionsMenu, tab.Content);
        }

        if (root == null)
            return;

        hiddenSiblings.Clear();
        var content = tab.Content.transform;
        for (int i = 0; i < content.childCount; i++)
        {
            var child = content.GetChild(i).gameObject;
            if (child == root || !child.activeSelf)
                continue;

            child.SetActive(false);
            hiddenSiblings.Add(child);
        }

        root.SetActive(true);
        ClearPending();
        selectedPlayerId = null;
        Refresh();
    }

    internal static void Close()
    {
        if (root != null)
        {
            root.SetActive(false);
        }

        foreach (var sibling in hiddenSiblings)
        {
            if (sibling != null)
            {
                sibling.SetActive(true);
            }
        }

        hiddenSiblings.Clear();
        ClearPending();
    }

    private static void Build(OptionsMenuBehaviour optionsMenu, GameObject content)
    {
        template = optionsMenu.DisableMouseMovement;
        if (template == null)
            return;

        root = new GameObject("ModerationCenter");
        root.transform.SetParent(content.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;

        var parent = root.transform;
        statusText = OptionsPageWidgets.CreateText(parent, template, "Status", new Vector3(0f, 2.05f, -6f), new Vector2(4.8f, 0.7f), 0.8f, TextAlignmentOptions.Center);
        historyText = OptionsPageWidgets.CreateText(parent, template, "History", new Vector3(0f, -1.95f, -6f), new Vector2(4.8f, 0.9f), 0.7f, TextAlignmentOptions.TopLeft);

        float actionY = -0.75f;
        kickButton = OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.ModerationCenter_Kick.LocalizedString, new Vector3(ColumnX(0), actionY, -6f), SmallButtonScale, () => RequestAction(ModerationActionKind.Kick));
        banButton = OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.ModerationCenter_Ban.LocalizedString, new Vector3(ColumnX(1), actionY, -6f), SmallButtonScale, () => RequestAction(ModerationActionKind.Ban));
        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.ModerationCenter_Refresh.LocalizedString, new Vector3(ColumnX(2), actionY, -6f), SmallButtonScale, Refresh);

        float listY = -1.2f;
        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.ModerationCenter_PlayerList.LocalizedString, new Vector3(ColumnX(0), listY, -6f), SmallButtonScale, () => OpenList(ModerationListKind.Player));
        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.ModerationCenter_NameList.LocalizedString, new Vector3(ColumnX(1), listY, -6f), SmallButtonScale, () => OpenList(ModerationListKind.Name));
        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.ModerationCenter_ChatList.LocalizedString, new Vector3(ColumnX(2), listY, -6f), SmallButtonScale, () => OpenList(ModerationListKind.Chat));

        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.ModerationCenter_Back.LocalizedString, new Vector3(-2f, -2.5f, -6f), SmallButtonScale, Close);
    }

    private static float ColumnX(int column) => -1.6f + 1.6f * column;

    private static void Refresh()
    {
        if (root == null || template == null)
            return;

        foreach (var (_, button) in playerButtons)
        {
            if (button != null)
            {
                button.gameObject.DestroyObj();
            }
        }
        playerButtons.Clear();

        var players = ModerationActions.GetModeratablePlayers();
        bool selectedStillPresent = false;
        int maxPlayers = Columns * PlayerRows;

        for (int i = 0; i < players.Count && i < maxPlayers; i++)
        {
            var player = players[i];
            byte playerId = player.PlayerId;
            selectedStillPresent |= selectedPlayerId == playerId;

            var button = OptionsPageWidgets.CreateButton(
                root.transform,
                template,
                player.GetPlayerNameAndColor(),
                new Vector3(ColumnX(i % Columns), 1.55f - 0.42f * (i / Columns), -6f),
                SmallButtonScale,
                () => SelectPlayer(playerId));

            if (button != null)
            {
                playerButtons.Add((playerId, button));
            }
        }

        if (!selectedStillPresent)
        {
            selectedPlayerId = null;
            ClearPending();
        }

        UpdateVisuals(players.Count);
    }

    private static void SelectPlayer(byte playerId)
    {
        ClearPending();
        selectedPlayerId = selectedPlayerId == playerId ? null : playerId;
        UpdateVisuals(playerButtons.Count);
    }

    private static void RequestAction(ModerationActionKind kind)
    {
        if (selectedPlayerId == null)
        {
            SetStatus(TranslationStrings.ModerationCenter_SelectPlayer.LocalizedString);
            return;
        }

        var target = Utils.PlayerFromPlayerId(selectedPlayerId.Value);
        var denial = ModerationActions.Check(target);
        if (denial != ModerationDenial.None)
        {
            ClearPending();
            Refresh();
            SetStatus(ModerationActions.Describe(denial));
            return;
        }

        string name = target!.GetPlayerNameAndColor();

        if (pendingAction == kind && Time.unscaledTime <= pendingUntil)
        {
            ClearPending();
            ModerationActions.Execute(target, kind, out string message);
            selectedPlayerId = null;
            UpdateVisuals(playerButtons.Count);
            SetStatus(message);
            BetterNotificationManager.Notify(message, 3f, force: true);
            return;
        }

        pendingAction = kind;
        pendingUntil = Time.unscaledTime + ConfirmWindowSeconds;
        UpdateActionButtons();
        SetStatus(kind == ModerationActionKind.Ban
            ? TranslationStrings.ModerationCenter_PendingBan.Format(name)
            : TranslationStrings.ModerationCenter_PendingKick.Format(name));
    }

    private static void OpenList(ModerationListKind kind)
    {
        ClearPending();
        UpdateActionButtons();
        SetStatus(ModerationActions.OpenList(kind));
    }

    private static void ClearPending()
    {
        pendingAction = null;
        pendingUntil = 0f;
    }

    private static void UpdateVisuals(int playerCount)
    {
        foreach (var (playerId, button) in playerButtons)
        {
            OptionsPageWidgets.SetButtonColor(button, selectedPlayerId == playerId ? OptionsPageWidgets.SelectedColor : OptionsPageWidgets.IdleColor);
        }

        UpdateActionButtons();
        UpdateHistory();

        if (!GameState.IsInGame)
        {
            SetStatus(TranslationStrings.ModerationCenter_NotInGame.LocalizedString);
        }
        else if (!GameState.IsHost)
        {
            SetStatus(TranslationStrings.ModerationCenter_NotHost.LocalizedString);
        }
        else if (playerCount == 0)
        {
            SetStatus(TranslationStrings.ModerationCenter_NoPlayers.LocalizedString);
        }
        else if (selectedPlayerId != null)
        {
            var target = Utils.PlayerFromPlayerId(selectedPlayerId.Value);
            SetStatus(TranslationStrings.ModerationCenter_Selected.Format(target != null ? target.GetPlayerNameAndColor() : string.Empty));
        }
        else
        {
            SetStatus(TranslationStrings.ModerationCenter_SelectPlayer.LocalizedString);
        }
    }

    private static void UpdateActionButtons()
    {
        bool pendingKick = pendingAction == ModerationActionKind.Kick;
        bool pendingBan = pendingAction == ModerationActionKind.Ban;

        OptionsPageWidgets.SetButtonLabel(kickButton, pendingKick
            ? TranslationStrings.ModerationCenter_ConfirmKick.LocalizedString
            : TranslationStrings.ModerationCenter_Kick.LocalizedString);
        OptionsPageWidgets.SetButtonColor(kickButton, pendingKick ? OptionsPageWidgets.DangerColor : OptionsPageWidgets.IdleColor);

        OptionsPageWidgets.SetButtonLabel(banButton, pendingBan
            ? TranslationStrings.ModerationCenter_ConfirmBan.LocalizedString
            : TranslationStrings.ModerationCenter_Ban.LocalizedString);
        OptionsPageWidgets.SetButtonColor(banButton, pendingBan ? OptionsPageWidgets.DangerColor : OptionsPageWidgets.IdleColor);
    }

    private static void SetStatus(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }

    private static void UpdateHistory()
    {
        if (historyText == null)
            return;

        var lines = new List<string>
        {
            $"<color=#ffffbe>{TranslationStrings.ModerationCenter_HistoryTitle.LocalizedString}</color>"
        };

        int shown = 0;
        foreach (var entry in ModerationHistory.Latest(HistoryRows))
        {
            string action = entry.Action == ModerationActionKind.Ban
                ? TranslationStrings.HostTools_Ban.LocalizedString
                : TranslationStrings.HostTools_Kick.LocalizedString;
            string reason = entry.Reason.Length > 48 ? entry.Reason[..45] + "..." : entry.Reason;

            lines.Add(TranslationStrings.ModerationCenter_HistoryEntry.Format(
                entry.Time.ToString("HH:mm"),
                action,
                entry.PlayerName,
                reason));
            shown++;
        }

        if (shown == 0)
        {
            lines.Add(TranslationStrings.ModerationCenter_HistoryEmpty.LocalizedString);
        }

        historyText.text = string.Join("\n", lines);
    }
}
