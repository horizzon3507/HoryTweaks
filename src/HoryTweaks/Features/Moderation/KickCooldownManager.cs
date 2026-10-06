using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;

using UnityEngine;
using BetterAmongUs.Features.GameOptions;

namespace BetterAmongUs.Features.Moderation;

/// <summary>
/// Manages the cooldown period between player kick actions.
/// This cooldown prevents repeated host moderation actions.
/// </summary>
internal static class KickCooldownManager
{
    private static float CooldownSeconds => BetterGameSettings.KickCooldown.GetFloat();

    private static float _lastTriggerTime;

    internal static bool IsReady() => Time.time - _lastTriggerTime > CooldownSeconds;
    internal static void Trigger() => _lastTriggerTime = Time.time;
    private static float TimeToNextAvailableTrigger() => CooldownSeconds - (Time.time - _lastTriggerTime);

    internal static void ScheduleAction(Action action)
    {
        AmongUsClient.Instance.StartCoroutine(RunScheduledAction(action));
    }

    private static IEnumerator RunScheduledAction(Action action)
    {
        while (!IsReady())
        {
            yield return new WaitForSeconds(TimeToNextAvailableTrigger());
        }
        Trigger();
        action();
    }
}