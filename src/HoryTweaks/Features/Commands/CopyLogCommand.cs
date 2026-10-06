
using HoryTweaks.Generated;

using HoryTweaks.Utilities;
using UnityEngine;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Core.Formatting;
using HoryTweaks.Features.Chat;

namespace HoryTweaks.Features.Commands;

[RegisterCommand]
internal sealed class CopyLogCommand : BaseCommand
{
    private const int MaxLines = 50;

    internal override string Name => "copy";
    internal override string[] ShortNames => ["copylog"];
    internal override string Description => TranslationStrings.Chat_Copy_Description.LocalizedString;

    internal override void Run()
    {
        var lines = GetRecentChatLines(MaxLines);
        if (lines.Count == 0)
        {
            CommandResultText(TranslationStrings.Chat_Copy_Empty.LocalizedString);
            return;
        }

        GUIUtility.systemCopyBuffer = string.Join('\n', lines);
        CommandResultText(TranslationStrings.Chat_Copy_Done.Format(lines.Count));
    }

    private static List<string> GetRecentChatLines(int max)
    {
        List<string> lines = [];
        if (!HudManager.InstanceExists)
            return lines;

        var pool = HudManager.Instance.Chat?.chatBubblePool;
        if (pool == null)
            return lines;

        var actives = pool.activeChildren.ToArray();
        int start = Math.Max(0, actives.Length - max);
        for (int i = start; i < actives.Length; i++)
        {
            var bubble = actives[i].GetComponent<ChatBubble>();
            if (bubble == null)
                continue;

            string sender = ChatFormat.StripRichText(
                bubble.NameText.text?.Replace(ChatPatch.COMMAND_POSTFIX_ID, string.Empty) ?? string.Empty).Trim();
            string text = ChatFormat.StripRichText(bubble.TextArea.text).Trim();
            if (text.Length == 0)
                continue;

            lines.Add($"{sender}: {text}");
        }

        return lines;
    }
}
