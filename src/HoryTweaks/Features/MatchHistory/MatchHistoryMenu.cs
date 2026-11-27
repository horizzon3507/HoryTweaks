using AmongUs.GameOptions;
using HarmonyLib;
using HoryTweaks.Core.Formatting;
using HoryTweaks.Core.MatchHistory;
using HoryTweaks.Features.ClientOptions;
using HoryTweaks.Features.GameSummary;
using HoryTweaks.Features.Hud;
using HoryTweaks.Game;
using HoryTweaks.Generated;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HoryTweaks.Features.MatchHistory;

/// <summary>
/// Match history page inside the Tweaks options tab: a list of saved matches and, for one match,
/// a timeline with a 2D replay drawn on the map. Only usable outside of running matches.
/// </summary>
internal static class MatchHistoryMenu
{
    private const string LogTag = "MatchHistory";
    private const float ConfirmWindowSeconds = 8f;
    private const int EntriesPerPage = 6;
    private const int EventLines = 4;
    private const float PanelWidth = 4.6f;
    private const float PanelHeight = 2.4f;
    private const float BarY = -0.6f;
    private const float DotSize = 0.12f;
    private const float Z = -6f;

    private const float MarkerHeight = 2.6f;
    private const float BodyHeight = 1.8f;
    private const float IconHeight = 1.2f;

    private static readonly int[] Speeds = [1, 2, 4, 8];
    private static readonly Color BarColor = new(0.25f, 0.25f, 0.25f, 1f);
    private static readonly Color BarFillColor = new(0.8f, 0.8f, 0.8f, 1f);

    private static GameObject? root;
    private static GameObject? listRoot;
    private static GameObject? matchRoot;
    private static ToggleButtonBehaviour? template;
    private static readonly List<GameObject> hiddenSiblings = [];

    // List screen
    private static IReadOnlyList<MatchHistoryListItem> entries = [];
    private static int page;
    private static ToggleButtonBehaviour? deleteButton;
    private static float pendingDeleteUntil;

    // Match screen
    private static MatchRecord? record;
    private static string? currentFileName;
    private static string? currentVideoPath;
    private static float nextVideoCheckTime;
    private static ToggleButtonBehaviour? videoButton;
    private static MatchSampler? sampler;
    private static readonly Dictionary<byte, MatchPlayer> players = [];
    private static float timeMs;
    private static bool playing;
    private static int speedIndex;
    private static TextMeshPro? headerText;
    private static TextMeshPro? timeText;
    private static TextMeshPro? eventText;
    private static ToggleButtonBehaviour? playButton;
    private static ToggleButtonBehaviour? speedButton;
    private static Transform? mapRoot;
    private static Transform? markers;
    private static Transform? barFill;
    private static GameObject? background;
    private static float mapFitScale = 1f;
    private static float markersScale = 1f;
    private static bool mapReady;
    private static string mapStatus = string.Empty;
    private static AsyncOperationHandle<GameObject>? mapHandle;
    private static readonly Dictionary<byte, MatchHistoryAssets.PlayerMarker> playerMarkers = [];
    private static readonly Dictionary<byte, SpriteRenderer> ventIcons = [];
    private static readonly Dictionary<byte, SpriteRenderer> roleIcons = [];
    private static readonly Dictionary<byte, SpriteRenderer> bodies = [];
    private static readonly HashSet<byte> invisible = [];
    private static readonly Dictionary<byte, byte> shifted = [];
    private static readonly Dictionary<byte, Vector2> bodyPositions = [];
    private static readonly HashSet<byte> visible = [];
    private static int renderedEventIndex = -1;
    private static int markerLayer;
    private static int markerSortingLayerId;
    private static int markerSortingOrder;

    private static Sprite? squareSprite;

    /// <summary>
    /// Checks the anti-cheat gate and tells the player why the page cannot open.
    /// </summary>
    internal static bool CanOpenWithNotice()
    {
        bool canView = MatchHistoryRules.CanView(MatchHistoryGameState.Current);
        if (!canView)
        {
            BetterNotificationManager.Notify(TranslationStrings.MatchHistory_InGame.LocalizedString, 2.5f);
        }

        return canView;
    }

    /// <summary>
    /// Drops references to the previous options menu. Called when a new OptionsMenuBehaviour starts.
    /// </summary>
    internal static void Reset()
    {
        ReleaseMap();
        root = null;
        listRoot = null;
        matchRoot = null;
        template = null;
        hiddenSiblings.Clear();
        entries = [];
        deleteButton = null;
        pendingDeleteUntil = 0f;
        ClearMatchState();
    }

    internal static void Open(OptionsMenuBehaviour optionsMenu)
    {
        if (!MatchHistoryRules.CanView(MatchHistoryGameState.Current))
            return;

        try
        {
            OpenPage(optionsMenu);
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
            try
            {
                Close();
            }
            catch (Exception closeEx)
            {
                BAUPlugin.Logger.Error(closeEx, LogTag);
            }
        }
    }

    private static void OpenPage(OptionsMenuBehaviour optionsMenu)
    {
        var tab = OptionsMenuBehaviourPatch.BetterOptionsTab;
        if (tab == null || tab.Content == null)
            return;

        if (root == null)
        {
            template = optionsMenu.DisableMouseMovement;
            if (template == null)
                return;

            root = new GameObject("MatchHistory");
            root.transform.SetParent(tab.Content.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
        }

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
        entries = MatchHistoryStore.ListAll();
        page = 0;
        pendingDeleteUntil = 0f;
        BuildList();
    }

    /// <summary>
    /// Closes the page, releases the map asset and drops all loaded match data.
    /// </summary>
    internal static void Close()
    {
        CloseMatch();

        if (listRoot != null)
        {
            listRoot.DestroyObj();
        }

        listRoot = null;
        deleteButton = null;
        entries = [];
        pendingDeleteUntil = 0f;

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
    }

    /// <summary>
    /// Per-frame update driven by the options menu: enforces the gate and advances playback.
    /// </summary>
    internal static void Tick()
    {
        if (root == null || !root.activeInHierarchy)
            return;

        if (!MatchHistoryRules.CanView(MatchHistoryGameState.Current))
        {
            Close();
            return;
        }

        if (pendingDeleteUntil > 0f && Time.unscaledTime > pendingDeleteUntil)
        {
            pendingDeleteUntil = 0f;
            UpdateDeleteButton();
        }

        if (record == null || matchRoot == null)
            return;

        PollMap();
        PollVideoAvailability();

        if (playing)
        {
            timeMs += Time.unscaledDeltaTime * 1000f * Speeds[speedIndex];
            if (timeMs >= record.DurationMs)
            {
                timeMs = record.DurationMs;
                SetPlaying(false);
            }
        }

        Render();
    }

    #region List screen

    private static void BuildList()
    {
        if (root == null || template == null)
            return;

        if (listRoot != null)
        {
            listRoot.DestroyObj();
        }

        listRoot = new GameObject("List");
        listRoot.transform.SetParent(root.transform, false);
        listRoot.transform.localPosition = Vector3.zero;
        listRoot.transform.localScale = Vector3.one;
        var parent = listRoot.transform;

        var title = OptionsPageWidgets.CreateText(parent, template, "Title", new Vector3(0f, 2.05f, Z), new Vector2(4.8f, 0.5f), 0.8f, TextAlignmentOptions.Center);
        if (title != null)
        {
            title.text = entries.Count == 0
                ? TranslationStrings.MatchHistory_Empty.LocalizedString
                : $"<color=#ffffbe>{TranslationStrings.MatchHistory_Title.LocalizedString}</color>";
        }

        int pages = Math.Max(1, (entries.Count + EntriesPerPage - 1) / EntriesPerPage);
        page = Math.Clamp(page, 0, pages - 1);

        for (int i = 0; i < EntriesPerPage; i++)
        {
            int index = page * EntriesPerPage + i;
            if (index >= entries.Count)
                break;

            var entry = entries[index];
            if (entry.IsVideoOnly)
            {
                string videoLabel = $"{entry.StartedUtc.ToLocalTime():dd/MM HH:mm} · [MP4]";
                var videoPath = entry.VideoPath!;
                var videoRowButton = OptionsPageWidgets.CreateButton(parent, template, videoLabel, new Vector3(0f, 1.55f - 0.45f * i, Z), 1.6f, () => OpenVideoPath(videoPath));
                OptionsPageWidgets.SetButtonColor(videoRowButton, OptionsPageWidgets.IdleColor);
            }
            else
            {
                string label = EntryLabel(entry.Record!) + (entry.HasVideo ? " · [MP4]" : string.Empty);
                var fileName = entry.FileName;
                var button = OptionsPageWidgets.CreateButton(parent, template, label, new Vector3(0f, 1.55f - 0.45f * i, Z), 1.6f, () => OpenMatch(fileName));
                OptionsPageWidgets.SetButtonColor(button, entry.Record!.LocalWon ? OptionsPageWidgets.SelectedColor : OptionsPageWidgets.IdleColor);
            }
        }

        if (page > 0)
        {
            OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.BetterOption_Previous.LocalizedString, new Vector3(-1.6f, -1.45f, Z), 0.6f, () => ChangePage(-1));
        }

        if (page < pages - 1)
        {
            OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.BetterOption_Next.LocalizedString, new Vector3(1.6f, -1.45f, Z), 0.6f, () => ChangePage(1));
        }

        if (entries.Count > 0)
        {
            var pageText = OptionsPageWidgets.CreateText(parent, template, "Page", new Vector3(0f, -1.45f, Z), new Vector2(2f, 0.4f), 0.7f, TextAlignmentOptions.Center);
            if (pageText != null)
            {
                pageText.text = TranslationStrings.MatchHistory_Page.Format(page + 1, pages);
            }
        }

        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.ModerationCenter_Back.LocalizedString, new Vector3(-2f, -2.5f, Z), 0.6f, Close);
        if (!BAUPlugin.ModInfo.Starlight)
        {
            OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.MatchHistory_OpenFolder.LocalizedString, new Vector3(-0.2f, -2.5f, Z), 0.7f, OpenMatchHistoryFolder);
        }
        deleteButton = OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.MatchHistory_DeleteAll.LocalizedString, new Vector3(1.6f, -2.5f, Z), 0.6f, RequestDeleteAll);
        UpdateDeleteButton();
    }

    private static void OpenMatchHistoryFolder()
    {
        if (!MatchHistoryRules.CanView(MatchHistoryGameState.Current))
            return;

        string folder = HoryTweaks.Infrastructure.Persistence.BetterDataManager.Folders.matchHistoryFolderPath;
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }

        if (!HoryTweaks.Infrastructure.Platform.ShellOpener.TryOpen(folder, isDirectory: true))
        {
            BetterNotificationManager.Notify(TranslationStrings.Shell_OpenFailed.LocalizedString, 2.5f);
        }
    }

    private static string EntryLabel(MatchRecord match)
    {
        return string.Join(" · ",
            match.StartedUtc.ToLocalTime().ToString("dd/MM HH:mm"),
            MapName(match.MapId),
            match.HideAndSeek ? ModeName() : match.LocalWon ? TranslationStrings.MatchHistory_Win.LocalizedString : TranslationStrings.MatchHistory_Loss.LocalizedString,
            FormatTime(match.DurationMs));
    }

    private static void ChangePage(int delta)
    {
        page += delta;
        pendingDeleteUntil = 0f;
        BuildList();
    }

    private static void RequestDeleteAll()
    {
        if (pendingDeleteUntil > 0f && Time.unscaledTime <= pendingDeleteUntil)
        {
            pendingDeleteUntil = 0f;
            MatchHistoryStore.DeleteAll();
            BetterNotificationManager.Notify(TranslationStrings.MatchHistory_Deleted.LocalizedString, 2.5f);
            entries = MatchHistoryStore.ListAll();
            page = 0;
            BuildList();
            return;
        }

        pendingDeleteUntil = Time.unscaledTime + ConfirmWindowSeconds;
        UpdateDeleteButton();
    }

    private static void UpdateDeleteButton()
    {
        bool pending = pendingDeleteUntil > 0f;
        OptionsPageWidgets.SetButtonLabel(deleteButton, pending
            ? TranslationStrings.MatchHistory_ConfirmDelete.LocalizedString
            : TranslationStrings.MatchHistory_DeleteAll.LocalizedString);
        OptionsPageWidgets.SetButtonColor(deleteButton, pending ? OptionsPageWidgets.DangerColor : OptionsPageWidgets.IdleColor);
    }

    #endregion

    #region Match screen

    private static void OpenMatch(string fileName)
    {
        if (root == null || template == null)
            return;

        var loaded = MatchHistoryStore.Load(fileName);
        if (loaded == null)
        {
            BetterNotificationManager.Notify(TranslationStrings.MatchHistory_LoadFailed.LocalizedString, 2.5f);
            return;
        }

        CloseMatch();
        record = loaded;
        currentFileName = fileName;
        currentVideoPath = MatchHistoryStore.TryGetVideoPath(fileName, out var foundVideo) ? foundVideo : null;
        nextVideoCheckTime = Time.unscaledTime + 1f;
        sampler = new MatchSampler(loaded);
        MatchHistoryAssets.RefreshSceneAssets();
        foreach (var player in loaded.Players)
        {
            players[player.Id] = player;
        }

        pendingDeleteUntil = 0f;
        UpdateDeleteButton();
        if (listRoot != null)
        {
            listRoot.SetActive(false);
        }

        BuildMatch(loaded);
        StartMapLoad(loaded);
        Render();
    }

    private static void CloseMatch()
    {
        ReleaseMap();

        if (matchRoot != null)
        {
            matchRoot.DestroyObj();
        }

        matchRoot = null;
        ClearMatchState();

        if (listRoot != null)
        {
            listRoot.SetActive(true);
        }
    }

    private static void ClearMatchState()
    {
        record = null;
        currentFileName = null;
        currentVideoPath = null;
        nextVideoCheckTime = 0f;
        videoButton = null;
        sampler = null;
        players.Clear();
        playerMarkers.Clear();
        ventIcons.Clear();
        roleIcons.Clear();
        bodies.Clear();
        invisible.Clear();
        shifted.Clear();
        bodyPositions.Clear();
        visible.Clear();
        renderedEventIndex = -1;
        timeMs = 0f;
        playing = false;
        speedIndex = 0;
        headerText = null;
        timeText = null;
        eventText = null;
        playButton = null;
        speedButton = null;
        mapRoot = null;
        markers = null;
        barFill = null;
        background = null;
        mapReady = false;
        mapStatus = string.Empty;
        mapFitScale = 1f;
        markersScale = 1f;
    }

    private static void BuildMatch(MatchRecord match)
    {
        if (root == null || template == null)
            return;

        matchRoot = new GameObject("Match");
        matchRoot.transform.SetParent(root.transform, false);
        matchRoot.transform.localPosition = Vector3.zero;
        matchRoot.transform.localScale = Vector3.one;
        var parent = matchRoot.transform;

        markerLayer = template.gameObject.layer;
        markerSortingLayerId = template.Background != null ? template.Background.sortingLayerID : 0;
        markerSortingOrder = template.Background != null ? template.Background.sortingOrder : 0;

        headerText = OptionsPageWidgets.CreateText(parent, template, "Header", new Vector3(0f, 2.1f, Z), new Vector2(4.8f, 0.55f), 0.65f, TextAlignmentOptions.Center);

        var panel = NewChild(parent, "MapPanel", new Vector3(0f, 0.75f, Z));
        mapRoot = NewChild(panel, "MapRoot", Vector3.zero);
        markers = NewChild(mapRoot, "Markers", Vector3.zero);

        var roster = MatchHistoryRoster.Build(panel, match.Players, template, markerLayer, markerSortingLayerId, markerSortingOrder + 5);
        if (roster != null)
        {
            // Overlay at the top of the map area; two rows sit lower so the top row stays clear of the header.
            bool twoRows = match.Players.Count > 8;
            roster.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            roster.transform.localPosition = new Vector3(0f, twoRows ? 0.52f : 0.8f, 0f);
        }

        var bar = CreateRenderer(parent, "TimelineBar", GetSquareSprite(), BarColor, 1);
        bar.transform.localPosition = new Vector3(0f, BarY, Z);
        bar.transform.localScale = new Vector3(PanelWidth, 0.06f, 1f);

        var fill = CreateRenderer(parent, "TimelineFill", GetSquareSprite(), BarFillColor, 2);
        barFill = fill.transform;

        foreach (var matchEvent in match.Events)
        {
            Color? tickColor = matchEvent.Kind switch
            {
                MatchEventKind.Kill => Color.red,
                MatchEventKind.KillBlocked => new Color(1f, 0.5f, 0.5f, 1f),
                MatchEventKind.BodyReport or MatchEventKind.EmergencyMeeting => Color.yellow,
                MatchEventKind.MeetingEnd => new Color(1f, 1f, 0.5f, 1f),
                MatchEventKind.MeetingResult when matchEvent.Target != MatchIds.Unknown => new Color(0.5f, 0.8f, 1f, 1f),
                MatchEventKind.Protect => new Color(0.3f, 0.9f, 0.5f, 1f),
                MatchEventKind.SabotageStart => new Color(1f, 0.55f, 0f, 1f),
                _ => null
            };

            if (tickColor == null)
                continue;

            var tick = CreateRenderer(parent, "Tick", GetSquareSprite(), tickColor.Value, 3);
            tick.transform.localPosition = new Vector3(BarX(matchEvent.TimeMs, match.DurationMs), BarY, Z - 0.01f);
            tick.transform.localScale = new Vector3(0.04f, 0.16f, 1f);
        }

        timeText = OptionsPageWidgets.CreateText(parent, template, "Time", new Vector3(-1.2f, -0.85f, Z), new Vector2(2.4f, 0.3f), 0.6f, TextAlignmentOptions.Left);
        eventText = OptionsPageWidgets.CreateText(parent, template, "Events", new Vector3(0f, -1.35f, Z), new Vector2(4.8f, 0.75f), 0.6f, TextAlignmentOptions.TopLeft);

        const float controlsY = -2f;
        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.MatchHistory_PrevEvent.LocalizedString, new Vector3(-2f, controlsY, Z), 0.4f, PreviousEvent);
        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.MatchHistory_Back10.LocalizedString, new Vector3(-1f, controlsY, Z), 0.4f, () => Seek(timeMs - 10_000f));
        playButton = OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.MatchHistory_Play.LocalizedString, new Vector3(0f, controlsY, Z), 0.4f, TogglePlay);
        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.MatchHistory_Forward10.LocalizedString, new Vector3(1f, controlsY, Z), 0.4f, () => Seek(timeMs + 10_000f));
        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.MatchHistory_NextEvent.LocalizedString, new Vector3(2f, controlsY, Z), 0.4f, NextEvent);

        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.ModerationCenter_Back.LocalizedString, new Vector3(-2f, -2.5f, Z), 0.55f, CloseMatch);
        speedButton = OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.MatchHistory_Speed.Format(Speeds[speedIndex]), new Vector3(-0.7f, -2.5f, Z), 0.55f, CycleSpeed);
        OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.MatchHistory_CopySummary.LocalizedString, new Vector3(0.65f, -2.5f, Z), 0.6f, CopyCurrentMatchSummary);
        videoButton = OptionsPageWidgets.CreateButton(parent, template, TranslationStrings.MatchHistory_OpenVideo.LocalizedString, new Vector3(1.95f, -2.5f, Z), 0.55f, OpenCurrentVideo);
        if (videoButton != null)
        {
            OptionsPageWidgets.SetButtonColor(videoButton, OptionsPageWidgets.SelectedColor);
            videoButton.gameObject.SetActive(currentVideoPath != null);
        }
    }

    private static void PollVideoAvailability()
    {
        if (currentVideoPath != null || currentFileName == null || videoButton == null)
            return;

        if (Time.unscaledTime < nextVideoCheckTime)
            return;

        nextVideoCheckTime = Time.unscaledTime + 1f;
        if (MatchHistoryStore.TryGetVideoPath(currentFileName, out var foundVideo))
        {
            currentVideoPath = foundVideo;
            videoButton.gameObject.SetActive(true);
        }
    }

    private static void OpenCurrentVideo()
    {
        if (!MatchHistoryRules.CanView(MatchHistoryGameState.Current) || string.IsNullOrEmpty(currentVideoPath) || !File.Exists(currentVideoPath))
            return;

        OpenVideoPath(currentVideoPath);
    }

    private static void OpenVideoPath(string path)
    {
        if (!HoryTweaks.Infrastructure.Platform.ShellOpener.TryOpen(path, isDirectory: false))
        {
            BetterNotificationManager.Notify(TranslationStrings.Shell_OpenFailed.LocalizedString, 2.5f);
        }
    }

    private static void CopyCurrentMatchSummary()
    {
        if (record == null)
            return;

        var (team, tag, _) = GameSummaryPatch.GetWinInfo((GameOverReason)record.GameOverReason);
        var summaryPlayers = new List<MatchSummaryPlayer>(record.Players.Count);
        foreach (var player in record.Players)
        {
            string status = player.FinalState switch
            {
                MatchPlayerState.Disconnected => TranslationStrings.DC.LocalizedString,
                MatchPlayerState.Dead => TranslationStrings.Dead.LocalizedString,
                _ => TranslationStrings.Alive.LocalizedString
            };
            string roleName = ((RoleTypes)player.RoleType).GetRoleName();
            summaryPlayers.Add(new MatchSummaryPlayer(
                player.Name,
                roleName,
                player.IsImpostor,
                player.Kills,
                player.TasksDone,
                player.TasksTotal,
                status));
        }

        string formatted = MatchSummaryFormatter.Format(
            MapName(record.MapId),
            team,
            tag,
            record.DurationMs,
            summaryPlayers,
            GameSummaryPatch.CreateSummaryLabels());

        GUIUtility.systemCopyBuffer = formatted;
        BetterNotificationManager.Notify(TranslationStrings.MatchHistory_SummaryCopied.LocalizedString, 2.5f);
    }

    private static void TogglePlay()
    {
        if (record == null)
            return;

        if (!playing && timeMs >= record.DurationMs)
        {
            timeMs = 0f;
        }

        SetPlaying(!playing);
    }

    private static void SetPlaying(bool value)
    {
        playing = value;
        OptionsPageWidgets.SetButtonLabel(playButton, playing
            ? TranslationStrings.MatchHistory_Pause.LocalizedString
            : TranslationStrings.MatchHistory_Play.LocalizedString);
    }

    private static void CycleSpeed()
    {
        speedIndex = (speedIndex + 1) % Speeds.Length;
        OptionsPageWidgets.SetButtonLabel(speedButton, TranslationStrings.MatchHistory_Speed.Format(Speeds[speedIndex]));
    }

    private static void Seek(float target)
    {
        if (record == null)
            return;

        timeMs = Math.Clamp(target, 0f, record.DurationMs);
    }

    private static void PreviousEvent()
    {
        if (record == null)
            return;

        Seek(sampler?.PreviousEventTime((int)timeMs) ?? 0);
    }

    private static void NextEvent()
    {
        if (record == null)
            return;

        int? next = sampler?.NextEventTime((int)timeMs);
        if (next != null)
        {
            Seek(next.Value);
        }
    }

    #endregion

    #region Map

    private static void StartMapLoad(MatchRecord match)
    {
        var client = AmongUsClient.Instance;
        var prefab = client != null && match.MapId < client.ShipPrefabs.Count ? client.ShipPrefabs[match.MapId] : null;
        if (prefab == null)
        {
            UseFallbackMap();
            return;
        }

        try
        {
            // Global Addressables API: never touches the game's own AssetReference state.
            mapHandle = Addressables.LoadAssetAsync<GameObject>(prefab.RuntimeKey);
            mapStatus = TranslationStrings.MatchHistory_LoadingMap.LocalizedString;
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
            UseFallbackMap();
        }
    }

    private static void PollMap()
    {
        if (mapReady || mapHandle == null || !mapHandle.IsDone)
            return;

        if (mapHandle.Status != AsyncOperationStatus.Succeeded || !TryBuildBackground(mapHandle.Result))
        {
            UseFallbackMap();
        }
    }

    private static bool TryBuildBackground(GameObject? prefab)
    {
        if (prefab == null || mapRoot == null || markers == null)
            return false;

        var ship = prefab.GetComponent<ShipStatus>();
        if (ship == null)
            return false;

        var map = ship.MapPrefab;
        if (map == null)
            return false;

        var rend = map.ColorControl != null ? map.ColorControl.rend : null;
        if (rend == null || rend.sprite == null)
        {
            rend = LargestRenderer(map);
        }

        if (rend == null || rend.sprite == null)
            return false;

        var mapTransform = map.transform;
        Vector3 bgPos = mapTransform.InverseTransformPoint(rend.transform.position);
        Vector3 bgScale = Ratio(rend.transform.lossyScale, mapTransform.lossyScale);

        var bgRenderer = CreateRenderer(mapRoot, "Background", rend.sprite, Color.white, 1);
        background = bgRenderer.gameObject;
        bgRenderer.transform.localPosition = new Vector3(bgPos.x, bgPos.y, 0f);
        bgRenderer.transform.localScale = bgScale;

        var herePoint = mapTransform.Find("HereIndicatorParent");
        Vector3 markersPos = herePoint != null ? mapTransform.InverseTransformPoint(herePoint.position) : Vector3.zero;
        Vector3 markersLocalScale = herePoint != null ? Ratio(herePoint.lossyScale, mapTransform.lossyScale) : Vector3.one;
        markers.localPosition = new Vector3(markersPos.x, markersPos.y, -0.1f);
        markers.localScale = markersLocalScale;

        if (record != null)
        {
            MatchHistoryMapOverlay.Draw(mapRoot, ship, map, record.MapScale, record.MapFlipX, markerSortingLayerId, markerSortingOrder);
        }

        var bounds = rend.sprite.bounds;
        var size = Vector3.Scale(bounds.size, bgScale);
        var center = bgPos + Vector3.Scale(bounds.center, bgScale);
        ApplyFit(size.x, size.y, center.x, center.y);

        markersScale = Mathf.Abs(markersLocalScale.x) > 0.0001f ? Mathf.Abs(markersLocalScale.x) : 1f;
        mapStatus = string.Empty;
        mapReady = true;
        return true;
    }

    private static SpriteRenderer? LargestRenderer(MapBehaviour map)
    {
        SpriteRenderer? largest = null;
        float largestArea = 0f;
        foreach (var candidate in map.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (candidate == null || candidate.sprite == null)
                continue;

            var size = Vector3.Scale(candidate.sprite.bounds.size, candidate.transform.lossyScale);
            float area = Mathf.Abs(size.x * size.y);
            if (area > largestArea)
            {
                largestArea = area;
                largest = candidate;
            }
        }

        return largest;
    }

    private static void UseFallbackMap()
    {
        ReleaseMap();
        mapReady = true;
        mapStatus = string.Empty;

        if (markers == null || record == null)
            return;

        markers.localPosition = Vector3.zero;
        markers.localScale = Vector3.one;
        markersScale = 1f;

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var frame in record.Frames)
        {
            foreach (var position in frame.Positions)
            {
                var point = ToMap(position.X, position.Y);
                minX = Math.Min(minX, point.x);
                minY = Math.Min(minY, point.y);
                maxX = Math.Max(maxX, point.x);
                maxY = Math.Max(maxY, point.y);
            }
        }

        if (minX > maxX)
        {
            minX = maxX = minY = maxY = 0f;
        }

        // Without the real map prefab, the bundled map render stands in for it, stretched over
        // the recorded play area so markers still land roughly where they happened.
        var embedded = MatchHistoryAssets.EmbeddedMap(record.MapId);
        if (embedded != null && mapRoot != null)
        {
            float width = Math.Max(maxX - minX, 1f) * 1.15f;
            float height = Math.Max(maxY - minY, 1f) * 1.15f;
            float centerX = (minX + maxX) / 2f;
            float centerY = (minY + maxY) / 2f;

            var bgRenderer = CreateRenderer(mapRoot, "Background", embedded, Color.white, 1);
            background = bgRenderer.gameObject;
            var bounds = embedded.bounds;
            float scaleX = width / Math.Max(bounds.size.x, 0.0001f);
            float scaleY = height / Math.Max(bounds.size.y, 0.0001f);
            bgRenderer.transform.localPosition = new Vector3(centerX - bounds.center.x * scaleX, centerY - bounds.center.y * scaleY, 0f);
            bgRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);

            ApplyFit(width, height, centerX, centerY);
            DrawFallbackVents();
            return;
        }

        mapStatus = TranslationStrings.MatchHistory_MapUnavailable.LocalizedString;
        ApplyFit(Math.Max(maxX - minX, 1f), Math.Max(maxY - minY, 1f), (minX + maxX) / 2f, (minY + maxY) / 2f);
        DrawFallbackVents();
    }

    // Without the real prefab there is no vent data, so vent positions come from
    // the recorded VentEnter events themselves.
    private static void DrawFallbackVents()
    {
        if (markers == null || record == null)
            return;

        var sprite = Utils.LoadSprite("HoryTweaks.Resources.Images.Icons.Vent.png", 380f);
        if (sprite == null)
            return;

        float size = 0.35f / Math.Max(sprite.bounds.size.y * mapFitScale, 0.0001f);
        var seen = new HashSet<(int, int)>();
        foreach (var matchEvent in record.Events)
        {
            if (matchEvent.Kind != MatchEventKind.VentEnter)
                continue;

            var point = ToMap(matchEvent.X, matchEvent.Y);
            if (!seen.Add(((int)(point.x * 2f), (int)(point.y * 2f))))
                continue;

            var icon = CreateRenderer(markers, "MapVent", sprite, new Color(1f, 1f, 1f, 0.4f), 2);
            icon.transform.localPosition = new Vector3(point.x, point.y, -0.05f);
            icon.transform.localScale = new Vector3(size, size, 1f);
        }
    }

    private static void ApplyFit(float width, float height, float centerX, float centerY)
    {
        if (mapRoot == null)
            return;

        float scale = Math.Min(PanelWidth / Math.Max(width, 0.0001f), PanelHeight / Math.Max(height, 0.0001f));
        mapFitScale = scale;
        mapRoot.localScale = new Vector3(scale, scale, 1f);
        mapRoot.localPosition = new Vector3(-centerX * scale, -centerY * scale, 0f);
    }

    private static void ReleaseMap()
    {
        // Destroy the renderer first so no sprite of the released asset stays referenced.
        if (background != null)
        {
            background.DestroyObj();
        }

        background = null;

        if (mapHandle != null)
        {
            var handle = mapHandle;
            mapHandle = null;
            try
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Error(ex, LogTag);
            }
        }
    }

    private static Vector3 Ratio(Vector3 a, Vector3 b)
    {
        return new Vector3(
            Mathf.Abs(b.x) > 0.0001f ? a.x / b.x : 1f,
            Mathf.Abs(b.y) > 0.0001f ? a.y / b.y : 1f,
            Mathf.Abs(b.z) > 0.0001f ? a.z / b.z : 1f);
    }

    private static Vector2 ToMap(float x, float y)
    {
        if (record == null)
            return Vector2.zero;

        float mapScale = record.MapScale != 0f ? record.MapScale : 1f;
        return new Vector2(x / mapScale * (record.MapFlipX ? -1f : 1f), y / mapScale);
    }

    #endregion

    #region Rendering

    private static void Render()
    {
        if (record == null)
            return;

        int now = (int)timeMs;

        if (headerText != null)
        {
            var (team, tag, color) = GameSummaryPatch.GetWinInfo((GameOverReason)record.GameOverReason);
            string header = $"{MapName(record.MapId)} · <color={color}>{team}</color> · {tag} · {FormatTime(record.DurationMs)}";
            headerText.text = mapStatus.Length == 0 ? header : $"{header}\n<size=80%>{mapStatus}</size>";
        }

        if (barFill != null)
        {
            float fraction = record.DurationMs > 0 ? Math.Clamp(timeMs / record.DurationMs, 0f, 1f) : 0f;
            float width = PanelWidth * fraction;
            barFill.localPosition = new Vector3(-PanelWidth / 2f + width / 2f, BarY, Z - 0.005f);
            barFill.localScale = new Vector3(width, 0.06f, 1f);
        }

        if (timeText != null)
        {
            timeText.text = $"{FormatTime(now)} / {FormatTime(record.DurationMs)} · {Speeds[speedIndex]}x";
        }

        int lastEvent = sampler != null ? sampler.EventIndex(now) : MatchPlayback.EventIndexAtOrBefore(record.Events, now);
        RenderEventText(lastEvent);

        if (mapReady)
        {
            RenderMarkers(now, lastEvent);
        }
    }

    private static void RenderEventText(int lastEvent)
    {
        if (eventText == null || record == null)
            return;

        if (lastEvent == renderedEventIndex)
            return;

        renderedEventIndex = lastEvent;

        var lines = new List<string>(EventLines);
        for (int i = lastEvent; i >= 0 && lines.Count < EventLines; i--)
        {
            var matchEvent = record.Events[i];
            // The exile cutscene re-reports what MeetingResult already said; keep its position data only.
            if (matchEvent.Kind == MatchEventKind.Ejected)
                continue;

            lines.Insert(0, $"{FormatTime(matchEvent.TimeMs)}  {DescribeEvent(matchEvent)}");
        }

        eventText.text = string.Join("\n", lines);
    }

    private static void RenderMarkers(int now, int lastEvent)
    {
        if (record == null || markers == null)
            return;

        invisible.Clear();
        shifted.Clear();
        bodyPositions.Clear();
        visible.Clear();
        for (int i = 0; i <= lastEvent; i++)
        {
            var matchEvent = record.Events[i];
            switch (matchEvent.Kind)
            {
                case MatchEventKind.Vanish:
                    invisible.Add(matchEvent.Actor);
                    break;
                case MatchEventKind.Appear:
                    invisible.Remove(matchEvent.Actor);
                    break;
                case MatchEventKind.Shapeshift:
                    shifted[matchEvent.Actor] = matchEvent.Target;
                    break;
                case MatchEventKind.ShapeshiftRevert:
                    shifted.Remove(matchEvent.Actor);
                    break;
                case MatchEventKind.Kill:
                    // Viper's dissolve leaves no corpse to display.
                    if (!players.TryGetValue(matchEvent.Actor, out var killer) || killer.RoleType != (int)RoleTypes.Viper)
                    {
                        bodyPositions[matchEvent.Target] = new Vector2(matchEvent.X, matchEvent.Y);
                    }

                    break;
                case MatchEventKind.BodyReport:
                case MatchEventKind.EmergencyMeeting:
                    bodyPositions.Clear();
                    break;
            }
        }

        float unit = DotSize / Math.Max(mapFitScale * markersScale, 0.0001f);
        var bodySprite = MatchHistoryAssets.DeadBodySprite();

        foreach (var (playerId, body) in bodies)
        {
            body.gameObject.SetActive(bodySprite != null && bodyPositions.ContainsKey(playerId));
        }

        if (bodySprite != null)
        {
            float bodyUnit = unit * BodyHeight / Math.Max(bodySprite.bounds.size.y, 0.0001f);
            foreach (var (playerId, position) in bodyPositions)
            {
                if (!bodies.TryGetValue(playerId, out var body))
                {
                    body = CreateRenderer(markers, $"Body{playerId}", bodySprite, Color.white, 2);
                    if (MatchHistoryAssets.BodyMaterial() != null)
                    {
                        body.sharedMaterial = MatchHistoryAssets.BodyMaterial();
                        PlayerMaterial.SetColors(PlayerColorId(playerId), body);
                    }
                    else
                    {
                        body.color = PlayerColor(playerId);
                    }

                    bodies[playerId] = body;
                }

                var point = ToMap(position.x, position.y);
                body.transform.localPosition = new Vector3(point.x, point.y, -0.1f);
                body.transform.localScale = new Vector3(bodyUnit, bodyUnit, 1f);
                body.gameObject.SetActive(true);
            }
        }

        if (sampler == null)
            return;

        sampler.Sample(now);
        foreach (var position in sampler.Positions)
        {
            if (!playerMarkers.TryGetValue(position.PlayerId, out var marker) || marker == null)
            {
                marker = MatchHistoryAssets.CreatePlayerMarker(markers, position.PlayerId, markerLayer, markerSortingLayerId, markerSortingOrder + 3);
                if (marker == null)
                    continue;

                playerMarkers[position.PlayerId] = marker;
            }

            bool dead = position.Flags.HasFlag(MatchPositionFlags.Dead);
            bool inVent = position.Flags.HasFlag(MatchPositionFlags.InVent);
            bool hidden = invisible.Contains(position.PlayerId);

            // A shapeshifted player wears their target's outfit until the shift reverts.
            byte appearanceId = shifted.TryGetValue(position.PlayerId, out var shiftTarget) ? shiftTarget : position.PlayerId;
            int key = appearanceId * 2 + (dead ? 1 : 0);
            if (marker.AppliedKey != key && players.TryGetValue(appearanceId, out var appearance))
            {
                MatchHistoryAssets.ApplyOutfit(marker, appearance, dead);
                marker.AppliedKey = key;
            }

            float alpha = dead ? 0.45f : inVent ? 0.6f : hidden ? 0.5f : 1f;
            MatchHistoryAssets.SetAlpha(marker, alpha);

            float height = unit * MarkerHeight * (inVent ? 0.7f : 1f);
            var point = ToMap(position.X, position.Y);
            marker.Root.transform.localPosition = new Vector3(point.x, point.y, -0.2f);
            marker.Root.transform.localScale = new Vector3(height / marker.NaturalHeight, height / marker.NaturalHeight, 1f);
            marker.Root.SetActive(true);
            visible.Add(position.PlayerId);

            RenderVentIcon(position.PlayerId, inVent, point, unit);
            RenderRoleIcon(position.PlayerId, point, height, unit);
        }

        foreach (var (playerId, marker) in playerMarkers)
        {
            if (marker != null && !visible.Contains(playerId))
            {
                marker.Root.SetActive(false);
                if (ventIcons.TryGetValue(playerId, out var vent) && vent != null)
                {
                    vent.gameObject.SetActive(false);
                }

                if (roleIcons.TryGetValue(playerId, out var role) && role != null)
                {
                    role.gameObject.SetActive(false);
                }
            }
        }
    }

    private static void RenderVentIcon(byte playerId, bool inVent, Vector2 point, float unit)
    {
        if (!inVent)
        {
            if (ventIcons.TryGetValue(playerId, out var hidden) && hidden != null)
            {
                hidden.gameObject.SetActive(false);
            }

            return;
        }

        if (!ventIcons.TryGetValue(playerId, out var icon) || icon == null)
        {
            var sprite = Utils.LoadSprite("HoryTweaks.Resources.Images.Icons.Vent.png", 380);
            if (sprite == null)
                return;

            icon = CreateRenderer(markers!, $"Vent{playerId}", sprite, new Color(1f, 1f, 1f, 0.85f), 4);
            ventIcons[playerId] = icon;
        }

        icon.transform.localPosition = new Vector3(point.x, point.y, -0.25f);
        icon.transform.localScale = new Vector3(unit, unit, 1f);
        icon.gameObject.SetActive(true);
    }

    private static void RenderRoleIcon(byte playerId, Vector2 point, float markerHeight, float unit)
    {
        if (!players.TryGetValue(playerId, out var player))
            return;

        if (!roleIcons.TryGetValue(playerId, out var icon) || icon == null)
        {
            var sprite = MatchHistoryAssets.RoleIcon(player);
            if (sprite == null)
                return;

            icon = CreateRenderer(markers!, $"Role{playerId}", sprite, Color.white, 4);
            roleIcons[playerId] = icon;
        }

        float iconUnit = unit * IconHeight / Math.Max(icon.sprite.bounds.size.y, 0.0001f);
        icon.transform.localPosition = new Vector3(point.x, point.y + markerHeight * 0.65f, -0.25f);
        icon.transform.localScale = new Vector3(iconUnit, iconUnit, 1f);
        icon.gameObject.SetActive(true);
    }

    private static string DescribeEvent(MatchEvent matchEvent)
    {
        string actor = PlayerName(matchEvent.Actor);
        string target = PlayerName(matchEvent.Target);

        return matchEvent.Kind switch
        {
            MatchEventKind.Kill => TranslationStrings.MatchHistory_Event_Kill.Format(actor, target),
            MatchEventKind.BodyReport => TranslationStrings.MatchHistory_Event_Report.Format(actor, target),
            MatchEventKind.EmergencyMeeting => TranslationStrings.MatchHistory_Event_Emergency.Format(actor),
            MatchEventKind.Vote when matchEvent.Target is MatchIds.Skipped or MatchIds.Missed => matchEvent.Actor == MatchIds.Unknown
                ? TranslationStrings.MatchHistory_Event_SkipAnonymous.LocalizedString
                : TranslationStrings.MatchHistory_Event_Skip.Format(actor),
            MatchEventKind.Vote => matchEvent.Actor == MatchIds.Unknown
                ? TranslationStrings.MatchHistory_Event_VoteAnonymous.Format(target)
                : TranslationStrings.MatchHistory_Event_Vote.Format(actor, target),
            MatchEventKind.MeetingResult when matchEvent.Target != MatchIds.Unknown => TranslationStrings.MatchHistory_Event_Ejected.Format(target),
            MatchEventKind.MeetingResult when (matchEvent.Value & 1) != 0 => TranslationStrings.MatchHistory_Event_Tie.LocalizedString,
            MatchEventKind.MeetingResult => TranslationStrings.MatchHistory_Event_NoEjection.LocalizedString,
            MatchEventKind.VentEnter => TranslationStrings.MatchHistory_Event_VentEnter.Format(actor),
            MatchEventKind.VentExit => TranslationStrings.MatchHistory_Event_VentExit.Format(actor),
            MatchEventKind.SabotageStart => TranslationStrings.MatchHistory_Event_Sabotage.Format(SystemName(matchEvent.Value)),
            MatchEventKind.SabotageEnd => TranslationStrings.MatchHistory_Event_SabotageFixed.Format(SystemName(matchEvent.Value)),
            MatchEventKind.Disconnect => TranslationStrings.MatchHistory_Event_Disconnect.Format(actor),
            MatchEventKind.Shapeshift => TranslationStrings.MatchHistory_Event_Shapeshift.Format(actor, target),
            MatchEventKind.ShapeshiftRevert => TranslationStrings.MatchHistory_Event_Revert.Format(actor),
            MatchEventKind.Vanish => TranslationStrings.MatchHistory_Event_Vanish.Format(actor),
            MatchEventKind.Appear => TranslationStrings.MatchHistory_Event_Appear.Format(actor),
            MatchEventKind.MatchStart => TranslationStrings.MatchHistory_Event_MatchStart.LocalizedString,
            MatchEventKind.MeetingEnd => TranslationStrings.MatchHistory_Event_MeetingEnd.LocalizedString,
            MatchEventKind.Ejected => TranslationStrings.MatchHistory_Event_Ejected.Format(target),
            MatchEventKind.Protect => TranslationStrings.MatchHistory_Event_Protect.Format(actor, target),
            MatchEventKind.KillBlocked => TranslationStrings.MatchHistory_Event_KillBlocked.Format(actor, target),
            MatchEventKind.TasksDone => TranslationStrings.MatchHistory_Event_TasksDone.Format(actor),
            _ => matchEvent.Kind.ToString()
        };
    }

    private static string PlayerName(byte playerId)
    {
        if (!players.TryGetValue(playerId, out var player))
            return "???";

        return $"<color={Utils.Color32ToHex(PlayerColor(playerId))}>{player.Name}</color>";
    }

    private static Color32 PlayerColor(byte playerId)
    {
        if (players.TryGetValue(playerId, out var player)
            && player.ColorId >= 0
            && player.ColorId < Palette.PlayerColors.Length)
        {
            return Palette.PlayerColors[player.ColorId];
        }

        return new Color32(255, 255, 255, 255);
    }

    private static int PlayerColorId(byte playerId)
    {
        return players.TryGetValue(playerId, out var player) ? player.ColorId : 0;
    }

    private static string SystemName(int value)
    {
        var type = (SystemTypes)value;
        var translator = TranslationController.Instance;
        return translator != null ? translator.GetString(type) : type.ToString();
    }

    private static string MapName(byte mapId) => Enum.GetName(typeof(MapNames), (int)mapId) ?? "?";

    private static string ModeName()
    {
        var translator = TranslationController.Instance;
        return translator != null ? translator.GetString(StringNames.GameTypeHideAndSeek) : "Hide & Seek";
    }

    private static string FormatTime(int ms)
    {
        int seconds = Math.Max(ms, 0) / 1000;
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private static float BarX(int time, int duration)
    {
        float fraction = duration > 0 ? Math.Clamp(time / (float)duration, 0f, 1f) : 0f;
        return -PanelWidth / 2f + PanelWidth * fraction;
    }

    #endregion

    #region Unity helpers

    private static Transform NewChild(Transform parent, string name, Vector3 localPosition)
    {
        var go = new GameObject(name);
        if (template != null)
        {
            go.layer = template.gameObject.layer;
        }

        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private static SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, Color color, int orderOffset)
    {
        var renderer = NewChild(parent, name, Vector3.zero).gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.maskInteraction = SpriteMaskInteraction.None;

        if (template != null && template.Background != null)
        {
            renderer.sortingLayerID = template.Background.sortingLayerID;
            renderer.sortingOrder = template.Background.sortingOrder + orderOffset;
        }
        else
        {
            renderer.sortingLayerID = markerSortingLayerId;
            renderer.sortingOrder = markerSortingOrder + orderOffset;
        }

        return renderer;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        squareSprite.hideFlags |= HideFlags.HideAndDontSave;
        return squareSprite;
    }

    #endregion
}

[HarmonyPatch]
internal static class MatchHistoryMenuPatch
{
    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Update))]
    [HarmonyPostfix]
    private static void OptionsMenuBehaviour_Update_Postfix()
    {
        try
        {
            MatchHistoryMenu.Tick();
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "MatchHistory");
            MatchHistoryMenu.Close();
        }
    }

    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.OnDisable))]
    [HarmonyPostfix]
    private static void OptionsMenuBehaviour_OnDisable_Postfix()
    {
        try
        {
            MatchHistoryMenu.Close();
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "MatchHistory");
        }
    }
}
