using UnityEngine;
using HoryTweaks.Core.MatchHistory;
using HoryTweaks.Core.Moderation;
using HoryTweaks.Features.MatchHistory.Video;
using HoryTweaks.Game;
using HoryTweaks.Game.Players;
using HoryTweaks.Infrastructure.Configuration;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Utilities;

namespace HoryTweaks.Features.MatchHistory;

/// <summary>
/// Records events and player positions of the current match in memory. Nothing recorded is exposed
/// while the match runs: the only output is <see cref="Finish"/>, which saves the match after it ended.
/// </summary>
internal static class MatchRecorder
{
    private const string LogTag = "MatchHistory";

    private static bool armed;
    private static bool recording;
    private static DateTime startedUtc;
    private static float startTime;
    private static int nextSampleMs;
    private static byte mapId;
    private static float mapScale;
    private static bool mapFlipX;
    private static bool hideAndSeek;
    private static readonly List<MatchEvent> events = [];
    private static readonly List<MatchFrame> frames = [];
    private static readonly HashSet<SystemTypes> activeSabotages = [];
    private static readonly HashSet<byte> tasksDonePlayers = [];

    private static int ElapsedMs => (int)Math.Min((Time.realtimeSinceStartup - startTime) * 1000f, int.MaxValue);

    /// <summary>
    /// Prepares recording for the ship that just spawned. Recording begins once the intro has finished.
    /// </summary>
    internal static void Arm()
    {
        Discard();
        armed = true;
    }

    /// <summary>
    /// Starts recording when the match is ready, stops on abnormal state changes and samples positions.
    /// </summary>
    internal static void Tick()
    {
        if (!recording)
        {
            if (MatchHistoryRules.ShouldBeginRecording(BAUConfigs.MatchHistory.Value, armed, GameState.IsFreePlay,
                MatchHistoryGameState.Current, GameState.IsShip, GameState.IsInIntro))
            {
                Begin();
            }

            return;
        }

        var state = MatchHistoryGameState.Current;
        if (state != ClientGameState.Started)
        {
            // Ended: wait for EndGameManager to call Finish. Anything else means the match was abandoned.
            if (state != ClientGameState.Ended)
            {
                Discard();
            }

            return;
        }

        int now = ElapsedMs;
        if (now > MatchHistoryRules.MaxDurationMs || now < nextSampleMs)
            return;

        Sample(now);
        nextSampleMs = now + MatchHistoryRules.SampleIntervalMs;
    }

    /// <summary>
    /// Adds an event to the current recording. No-op when not recording or over the limits.
    /// </summary>
    internal static void Record(MatchEventKind kind, byte actor, byte target, int value = 0, Vector2 position = default)
    {
        if (!recording || events.Count >= MatchHistoryRules.MaxEvents)
            return;

        int now = ElapsedMs;
        if (now > MatchHistoryRules.MaxDurationMs)
            return;

        events.Add(new MatchEvent(now, kind, actor, target, value, position.x, position.y));
    }

    /// <summary>
    /// Records the milestone of a player finishing all of their tasks, once per player.
    /// </summary>
    internal static void RecordTasksDone(PlayerControl player)
    {
        if (player == null || player.Data == null || player.isDummy || player.notRealPlayer
            || player.Data.Role == null || player.Data.Role.IsImpostor || !player.AllTasksCompleted())
            return;

        if (tasksDonePlayers.Add(player.PlayerId))
        {
            Record(MatchEventKind.TasksDone, player.PlayerId, MatchIds.Unknown, position: player.GetCustomPosition());
        }
    }

    /// <summary>
    /// Drops the current recording without saving.
    /// </summary>
    internal static void Discard()
    {
        armed = false;
        recording = false;
        events.Clear();
        frames.Clear();
        activeSabotages.Clear();
        tasksDonePlayers.Clear();
        MatchVideoRecorder.Discard();
    }

    /// <summary>
    /// Stops recording immediately when the option is switched off. Switching it on takes effect next match.
    /// </summary>
    internal static void OnConfigToggled()
    {
        if (!BAUConfigs.MatchHistory.Value)
        {
            Discard();
        }
        else
        {
            MatchVideoRecorder.OnConfigToggled();
        }
    }

    /// <summary>
    /// Saves the finished match. Called from the end game screen, after the match is over.
    /// </summary>
    internal static void Finish()
    {
        if (!recording)
            return;

        try
        {
            var winners = EndGameResult.CachedWinners != null ? EndGameResult.CachedWinners.ToArray() : Array.Empty<CachedPlayerData>();
            var localPlayer = PlayerControl.LocalPlayer;
            var players = new List<MatchPlayer>();
            var allPlayers = GameData.Instance != null ? GameData.Instance.AllPlayers.ToArray() : Array.Empty<NetworkedPlayerInfo>();

            foreach (var data in allPlayers)
            {
                if (data == null || (data.Object != null && (data.Object.isDummy || data.Object.notRealPlayer)))
                    continue;

                try
                {
                    var outfit = data.DefaultOutfit;
                    int colorId = outfit != null ? outfit.ColorId : -1;
                    var extended = data.ExtendedData();
                    int kills = extended != null && extended.RoleInfo != null ? extended.RoleInfo.Kills : 0;

                    // Dead players carry a ghost role at finish time; prefer their living role.
                    int roleType = (int)data.RoleType;
                    if (data.RoleType.IsGhostRole() && extended != null && extended.RoleInfo != null
                        && extended.RoleInfo.DeadDisplayRole != 0)
                    {
                        roleType = (int)extended.RoleInfo.DeadDisplayRole;
                    }
                    var state = data.Disconnected
                        ? MatchPlayerState.Disconnected
                        : data.IsDead ? MatchPlayerState.Dead : MatchPlayerState.Alive;

                    players.Add(new MatchPlayer(
                        data.PlayerId,
                        ModerationHistory.StripRichText(data.PlayerName),
                        colorId,
                        roleType,
                        data.Role != null && data.Role.IsImpostor,
                        winners.Any(w => w.PlayerName == data.PlayerName && w.ColorId == colorId),
                        localPlayer != null && data.PlayerId == localPlayer.PlayerId,
                        state,
                        kills,
                        data.Tasks != null ? data.Tasks.WhereIl2Cpp(task => task.Complete).Count : 0,
                        data.Tasks?.Count ?? 0,
                        outfit?.HatId ?? "",
                        outfit?.SkinId ?? "",
                        outfit?.VisorId ?? ""));
                }
                catch (Exception ex)
                {
                    // One bad player entry must not lose the whole recording.
                    BAUPlugin.Logger.Error(ex, LogTag);
                }
            }

            var record = new MatchRecord(
                startedUtc,
                Math.Min(ElapsedMs, MatchHistoryRules.MaxDurationMs),
                mapId,
                mapScale,
                mapFlipX,
                hideAndSeek,
                (int)EndGameResult.CachedGameOverReason,
                winners.Any(w => w.IsYou),
                players,
                [.. events],
                [.. frames]);

            MatchHistoryStore.Save(record);
            MatchVideoRecorder.Finish();
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }
        finally
        {
            Discard();
        }
    }

    private static void Begin()
    {
        var ship = ShipStatus.Instance;
        if (ship == null)
            return;

        Discard();
        startedUtc = DateTime.UtcNow;
        startTime = Time.realtimeSinceStartup;
        nextSampleMs = 0;
        mapId = GameState.GetActiveMapId;
        mapScale = ship.MapScale;
        mapFlipX = Mathf.Sign(ship.transform.localScale.x) < 0f;
        hideAndSeek = GameState.IsHideNSeek;
        recording = true;
        Record(MatchEventKind.MatchStart, MatchIds.Unknown, MatchIds.Unknown);
        MatchVideoRecorder.Begin(startedUtc);
    }

    private static void Sample(int now)
    {
        foreach (var type in SystemAndTaskTypeUtils.AllSabotages)
        {
            bool active = SystemAndTaskTypeUtils.IsSystemActive(type);
            if (active == activeSabotages.Contains(type))
                continue;

            if (active)
            {
                activeSabotages.Add(type);
            }
            else
            {
                activeSabotages.Remove(type);
            }

            Record(active ? MatchEventKind.SabotageStart : MatchEventKind.SabotageEnd, MatchIds.Unknown, MatchIds.Unknown, (int)type);
        }

        if (MeetingHud.Instance != null || ExileController.Instance != null)
            return;

        var positions = new List<MatchPosition>(BAUPlugin.AllPlayerControls.Count);
        foreach (var player in BAUPlugin.AllPlayerControls)
        {
            if (player == null || player.Data == null || player.Data.Disconnected || player.isDummy || player.notRealPlayer)
                continue;

            var flags = MatchPositionFlags.None;
            if (player.Data.IsDead)
            {
                flags |= MatchPositionFlags.Dead;
            }

            if (player.inVent)
            {
                flags |= MatchPositionFlags.InVent;
            }

            var position = player.GetCustomPosition();
            positions.Add(new MatchPosition(player.PlayerId, position.x, position.y, flags));
        }

        frames.Add(new MatchFrame(now, positions));
    }
}
