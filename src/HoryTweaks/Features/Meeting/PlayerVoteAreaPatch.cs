using HarmonyLib;

namespace HoryTweaks.Features.Meeting;

// Vanilla only hides the confirm/cancel overlay when results arrive, so vote
// areas keep playing their hover feedback and accepting clicks afterwards.
[HarmonyPatch]
internal static class PlayerVoteAreaPatch
{
    private static bool VotingFinished
        => MeetingHud.Instance != null
        && MeetingHud.Instance.CurrentState is MeetingHud.MeetingStates.Results or MeetingHud.MeetingStates.Proceeding;

    // Unhighlight calls must still pass so a stuck hover clears on mouse-exit.
    [HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetHighlighted))]
    [HarmonyPrefix]
    private static bool PlayerVoteArea_SetHighlighted_Prefix(bool value)
    {
        return !value || !VotingFinished;
    }

    [HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.Select))]
    [HarmonyPrefix]
    private static bool PlayerVoteArea_Select_Prefix()
    {
        return !VotingFinished;
    }
}
