using HoryTweaks.Features.Chat;
using HoryTweaks.Features.Commands.Arguments;
using HoryTweaks.Game;
using HoryTweaks.Generated;
using HoryTweaks.Plugin.Registration;

namespace HoryTweaks.Features.Commands;

[RegisterCommand]
internal sealed class UnmuteCommand : BaseCommand
{
    internal override string Name => "unmute";
    internal override string Description => TranslationStrings.Chat_Unmute_Description.LocalizedString;

    private readonly PlayerArgument _playerArgument;

    public UnmuteCommand()
    {
        _playerArgument = new PlayerArgument(this, "{player}");
        Arguments = [_playerArgument];
    }

    internal override bool CanRunCommand(out string reason)
    {
        if (!GameState.IsInGame)
        {
            reason = TranslationStrings.Command_Error_GameplayOnly.LocalizedString;
            return false;
        }

        return base.CanRunCommand(out reason);
    }

    internal override void Run()
    {
        if (string.IsNullOrWhiteSpace(_playerArgument.Arg))
        {
            int cleared = LocalMuteManager.ClearAll();
            CommandResultText(cleared > 0
                ? TranslationStrings.Chat_Unmute_Cleared.Format(cleared)
                : TranslationStrings.Chat_Unmute_None.LocalizedString);
            return;
        }

        if (!_playerArgument.TryParse(out var target) || target == null)
            return;

        string name = target.Data != null ? target.Data.PlayerName : $"ID{target.PlayerId}";
        if (LocalMuteManager.Unmute(target))
        {
            CommandResultText(TranslationStrings.Chat_Mute_Unmuted.Format(name));
        }
        else
        {
            CommandResultText(TranslationStrings.Chat_Unmute_NotMuted.Format(name));
        }
    }
}
