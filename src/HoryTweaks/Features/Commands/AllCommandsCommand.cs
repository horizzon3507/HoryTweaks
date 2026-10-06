
using HoryTweaks.Generated;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Features.Chat;

namespace HoryTweaks.Features.Commands;

[RegisterCommand]
internal sealed class AllCommandsCommand : BaseCommand
{
    internal override string Name => "commands";
    internal override string Description => TranslationStrings.Command_Commands_Description.LocalizedString;

    internal override void Run()
    {
        BaseCommand[] allNormalCommands = [.. CommandRegistry.Commands.Where(cmd => cmd.IsEnabled())];
        string list;
        var open = "<color=#858585>┌──────── </color>";
        var mid = "<color=#858585>├ </color>";
        var close = "<color=#858585>└──────── </color>";
        list = $"<color=#ffffbe><b><size=150%>{TranslationStrings.Command_List_Title.LocalizedString}</size></b></color>\n" + open;

        if (allNormalCommands.Length > 0)
        {
            foreach (var command in allNormalCommands)
            {
                list += $"\n{mid}<color=#e0b700><b>{ChatCommandsPatch.CommandPrefix}{command.Name}</b></color> <size=65%><color=#735e00>{command.Description}.</color></size>";
            }
        }

        list += "\n" + close;
        CommandResultText(list);
    }
}
