using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using HoryTweaks.Core.MatchHistory;
using HoryTweaks.Utilities;

namespace HoryTweaks.Features.MatchHistory;

/// <summary>
/// Feeds game events into <see cref="MatchRecorder"/>. Every hook swallows its own errors so a recorder
/// bug can never break gameplay.
/// </summary>
[HarmonyPatch]
internal static class MatchRecorderPatch
{
    private const string LogTag = "MatchHistory";

    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }
    }

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Awake))]
    [HarmonyPostfix]
    private static void ShipStatus_Awake_Postfix() => Guard(MatchRecorder.Arm);

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    private static void HudManager_Update_Postfix() => Guard(MatchRecorder.Tick);

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.DisconnectInternal))]
    [HarmonyPostfix]
    private static void InnerNetClient_DisconnectInternal_Postfix() => Guard(MatchRecorder.Discard);

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
    [HarmonyPostfix]
    private static void AmongUsClient_ExitGame_Postfix() => Guard(MatchRecorder.Discard);

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    [HarmonyPostfix]
    private static void PlayerControl_MurderPlayer_Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
    {
        Guard(() =>
        {
            if (target == null)
                return;

            if (resultFlags.HasFlag(MurderResultFlags.Succeeded))
            {
                MatchRecorder.Record(MatchEventKind.Kill, __instance.PlayerId, target.PlayerId, position: target.GetCustomPosition());
            }
            else if (resultFlags.HasFlag(MurderResultFlags.FailedProtected))
            {
                MatchRecorder.Record(MatchEventKind.KillBlocked, __instance.PlayerId, target.PlayerId, position: target.GetCustomPosition());
            }
        });
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
    [HarmonyPostfix]
    private static void PlayerControl_StartMeeting_Postfix(PlayerControl __instance, NetworkedPlayerInfo target)
    {
        Guard(() =>
        {
            if (target == null)
            {
                MatchRecorder.Record(MatchEventKind.EmergencyMeeting, __instance.PlayerId, MatchIds.Unknown);
            }
            else
            {
                MatchRecorder.Record(MatchEventKind.BodyReport, __instance.PlayerId, target.PlayerId);
            }
        });
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
    [HarmonyPostfix]
    private static void MeetingHud_VotingComplete_Postfix(Il2CppStructArray<MeetingHud.VoterState> states, NetworkedPlayerInfo exiled, bool tie, bool wasOverruled)
    {
        Guard(() =>
        {
            // Respect the lobby's anonymity: never store who voted when the game hid it.
            // Fail closed: when the options cannot be read, votes stay anonymous.
            bool anonymous = GameOptionsManager.Instance == null
                || GameOptionsManager.Instance.CurrentGameOptions == null
                || GameOptionsManager.Instance.CurrentGameOptions.GetBool(BoolOptionNames.AnonymousVotes);

            if (states != null)
            {
                foreach (var state in states)
                {
                    if (state.VotedForId == MatchIds.DeadVote)
                        continue;

                    MatchRecorder.Record(MatchEventKind.Vote, anonymous ? MatchIds.Unknown : state.VoterId, state.VotedForId);
                }
            }

            int result = (tie ? 1 : 0) | (wasOverruled ? 2 : 0);
            MatchRecorder.Record(MatchEventKind.MeetingResult, MatchIds.Unknown, exiled != null ? exiled.PlayerId : MatchIds.Unknown, result);
        });
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    [HarmonyPostfix]
    private static void MeetingHud_Close_Postfix()
    {
        Guard(() => MatchRecorder.Record(MatchEventKind.MeetingEnd, MatchIds.Unknown, MatchIds.Unknown));
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.BeginForGameplay))]
    [HarmonyPostfix]
    private static void ExileController_BeginForGameplay_Postfix(NetworkedPlayerInfo player)
    {
        Guard(() =>
        {
            if (player != null && player.Object != null)
            {
                MatchRecorder.Record(MatchEventKind.Ejected, MatchIds.Unknown, player.PlayerId, position: player.Object.GetCustomPosition());
            }
        });
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
    [HarmonyPostfix]
    private static void PlayerControl_ProtectPlayer_Postfix(PlayerControl __instance, PlayerControl target)
    {
        Guard(() =>
        {
            if (target != null)
            {
                MatchRecorder.Record(MatchEventKind.Protect, __instance.PlayerId, target.PlayerId, position: target.GetCustomPosition());
            }
        });
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
    [HarmonyPostfix]
    private static void PlayerControl_CompleteTask_Postfix(PlayerControl __instance)
    {
        Guard(() => MatchRecorder.RecordTasksDone(__instance));
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoEnterVent))]
    [HarmonyPostfix]
    private static void PlayerPhysics_CoEnterVent_Postfix(PlayerPhysics __instance, int id)
    {
        Guard(() => MatchRecorder.Record(MatchEventKind.VentEnter, __instance.myPlayer.PlayerId, MatchIds.Unknown, id, __instance.myPlayer.GetCustomPosition()));
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoExitVent))]
    [HarmonyPostfix]
    private static void PlayerPhysics_CoExitVent_Postfix(PlayerPhysics __instance, int id)
    {
        Guard(() => MatchRecorder.Record(MatchEventKind.VentExit, __instance.myPlayer.PlayerId, MatchIds.Unknown, id, __instance.myPlayer.GetCustomPosition()));
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
    [HarmonyPostfix]
    private static void PlayerControl_Shapeshift_Postfix(PlayerControl __instance, PlayerControl targetPlayer)
    {
        Guard(() =>
        {
            if (targetPlayer == null || targetPlayer.PlayerId == __instance.PlayerId)
            {
                MatchRecorder.Record(MatchEventKind.ShapeshiftRevert, __instance.PlayerId, MatchIds.Unknown);
            }
            else
            {
                MatchRecorder.Record(MatchEventKind.Shapeshift, __instance.PlayerId, targetPlayer.PlayerId);
            }
        });
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetRoleInvisibility))]
    [HarmonyPostfix]
    private static void PlayerControl_SetRoleInvisibility_Postfix(PlayerControl __instance, bool isActive)
    {
        Guard(() => MatchRecorder.Record(isActive ? MatchEventKind.Vanish : MatchEventKind.Appear, __instance.PlayerId, MatchIds.Unknown));
    }

    [HarmonyPatch(typeof(GameData))]
    [HarmonyPatch(nameof(GameData.HandleDisconnect))]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPatch([typeof(PlayerControl), typeof(DisconnectReasons)])]
    [HarmonyPostfix]
    private static void GameData_HandleDisconnect_Postfix(PlayerControl player)
    {
        Guard(() =>
        {
            if (player != null)
            {
                MatchRecorder.Record(MatchEventKind.Disconnect, player.PlayerId, MatchIds.Unknown, position: player.GetCustomPosition());
            }
        });
    }
}
