

using HoryTweaks.Generated;
using HoryTweaks.Features.Commands.Arguments;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Game;
using HoryTweaks.Infrastructure.UnityInterop;

namespace HoryTweaks.Features.Commands;

[RegisterCommand]
internal sealed class ForceSkipCommand : BaseCommand
{
    internal override string Name => "forceskip";
    internal override string Description => TranslationStrings.Command_ForceSkip_Description.LocalizedString;
    internal override bool CanRunCommand(out string reason)
    {
        if (!GameState.IsHost)
        {
            reason = TranslationStrings.Command_Error_HostOnly.LocalizedString;
            return false;
        }

        if (!GameState.IsMeeting)
        {
            reason = TranslationStrings.Command_Error_MeetingOnly.LocalizedString;
            return false;
        }

        return base.CanRunCommand(out reason);
    }

    public ForceSkipCommand()
    {
        _boolArgument = new BoolArgument(this, "{count votes}");
        Arguments = [_boolArgument];
    }
    private readonly BoolArgument _boolArgument;

    internal override void Run()
    {
        if (!_boolArgument.TryParse(out var countVotes))
            return;

        if (!GameState.IsHost || MeetingHud.Instance == null)
            return;

        // Avoid closing mid-intro animation, which can leave overlays stuck on vanilla clients.
        if (MeetingHud.Instance.state == MeetingHud.MeetingStates.Animating)
            return;

        if (!countVotes)
        {
            MeetingHud.Instance.RpcClose();
            return;
        }

        if (MeetingHud.Instance.state is MeetingHud.MeetingStates.Results or MeetingHud.MeetingStates.Proceeding)
            return;

        var dictionary = MeetingHud.Instance.CalculateVotes();
        var max = dictionary.MaxPair(out bool tie);
        NetworkedPlayerInfo? networkedPlayerInfo = GameData.Instance.AllPlayers.FirstOrDefaultIl2Cpp((NetworkedPlayerInfo v) => !tie && v.PlayerId == max.Key);
        MeetingHud.VoterState[] array = new MeetingHud.VoterState[MeetingHud.Instance.playerStates.Length];
        for (int i = 0; i < MeetingHud.Instance.playerStates.Length; i++)
        {
            PlayerVoteArea playerVoteArea = MeetingHud.Instance.playerStates[i];
            array[i] = new MeetingHud.VoterState
            {
                VoterId = playerVoteArea.PlayerId,
                VotedForId = playerVoteArea.VotedForId
            };
        }
        MeetingHud.Instance.RpcVotingComplete(array, networkedPlayerInfo, tie, false, 0);
    }
}
