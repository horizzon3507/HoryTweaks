using HoryTweaks.Features.Chat;
using HoryTweaks.Features.Commands.Arguments;
using HoryTweaks.Game;
using HoryTweaks.Generated;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Utilities;

namespace HoryTweaks.Features.Commands;

[RegisterCommand]
internal sealed class MuteCommand : BaseCommand
{
    internal override string Name => "mute";
    internal override string Description => TranslationStrings.Chat_Mute_Description.LocalizedString;

    private readonly PlayerArgument _playerArgument;

    public MuteCommand()
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
        if (!_playerArgument.TryParse(out var target) || target == null)
            return;

        if (target.IsLocalPlayer())
        {
            CommandErrorText(TranslationStrings.Chat_Mute_Self.LocalizedString);
            return;
        }

        string name = target.Data != null ? target.Data.PlayerName : $"ID{target.PlayerId}";
        bool muted = LocalMuteManager.Toggle(target);
        CommandResultText(muted
            ? TranslationStrings.Chat_Mute_Muted.Format(name)
            : TranslationStrings.Chat_Mute_Unmuted.Format(name));
    }
}
