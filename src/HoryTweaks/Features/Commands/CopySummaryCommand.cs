using HoryTweaks.Features.GameSummary;
using HoryTweaks.Generated;
using HoryTweaks.Plugin.Registration;
using UnityEngine;

namespace HoryTweaks.Features.Commands;

[RegisterCommand]
internal sealed class CopySummaryCommand : BaseCommand
{
    internal override string Name => "summary";
    internal override string[] ShortNames => ["sum"];
    internal override string Description => TranslationStrings.Chat_Summary_Description.LocalizedString;

    internal override void Run()
    {
        string? summary = GameSummaryPatch.LatestSummaryMarkdown;
        if (string.IsNullOrWhiteSpace(summary))
        {
            CommandResultText(TranslationStrings.Chat_Summary_Empty.LocalizedString);
            return;
        }

        GUIUtility.systemCopyBuffer = summary;
        CommandResultText(TranslationStrings.Chat_Summary_Copied.LocalizedString);
    }
}
