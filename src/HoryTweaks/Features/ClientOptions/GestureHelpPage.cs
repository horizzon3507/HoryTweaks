using HoryTweaks.Generated;
using HoryTweaks.Utilities;
using System.Text;
using TMPro;
using UnityEngine;
using HoryTweaks.Infrastructure.UnityInterop;

namespace HoryTweaks.Features.ClientOptions;

/// <summary>
/// Read-only "Controls &amp; Gestures" page inside the Better Options tab. Documents the mod's
/// hidden keyboard and mouse shortcuts so players can find them without reading external docs.
/// </summary>
internal static class GestureHelpPage
{
    private const int Page = 4;

    internal static void Create(OptionsMenuBehaviour optionsMenu)
    {
        var template = optionsMenu.DisableMouseMovement;
        var page = ClientOptionItem.GetOrCreatePage(Page, optionsMenu);
        if (template == null || template.Text == null || page == null)
            return;

        var title = CreateText(template, page.transform, "GesturesTitle",
            new Vector3(0f, 1.9f, -6f), new Vector2(4.8f, 0.5f), TextAlignmentOptions.Center);
        if (title != null)
        {
            title.text = $"<color=#ffffbe><b>{TranslationStrings.Gestures_Title.LocalizedString}</b></color>";
        }

        var body = CreateText(template, page.transform, "GesturesBody",
            new Vector3(0f, -0.35f, -6f), new Vector2(4.9f, 3.7f), TextAlignmentOptions.TopLeft);
        if (body == null)
            return;

        body.fontSize *= 0.55f;
        body.fontSizeMax = body.fontSize;
        body.fontSizeMin = body.fontSize * 0.5f;
        body.text = BuildBody();
    }

    private static TextMeshPro? CreateText(ToggleButtonBehaviour template, Transform parent, string name, Vector3 position, Vector2 size, TextAlignmentOptions alignment)
    {
        var go = UnityEngine.Object.Instantiate(template.Text.gameObject, parent);
        go.name = name;
        go.DestroyTextTranslators();
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one;

        var text = go.GetComponent<TextMeshPro>();
        if (text == null)
            return null;

        text.enableAutoSizing = true;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = alignment;
        text.color = Color.white;
        text.rectTransform.sizeDelta = size;
        go.SetActive(true);
        return text;
    }

    private static string BuildBody()
    {
        StringBuilder sb = new();
        sb.AppendLine($"<color=#868686>{TranslationStrings.Gestures_Subtitle.LocalizedString}</color>");

        Section(sb, TranslationStrings.Gestures_Section_Spectate);
        Item(sb, TranslationStrings.Gestures_Zoom);

        Section(sb, TranslationStrings.Gestures_Section_Lobby);
        Item(sb, TranslationStrings.Gestures_InstantStart);
        Item(sb, TranslationStrings.Gestures_CancelStart);

        Section(sb, TranslationStrings.Gestures_Section_Chat);
        Item(sb, TranslationStrings.Gestures_ChatCommands);
        Item(sb, TranslationStrings.Gestures_ChatHistory);
        Item(sb, TranslationStrings.Gestures_ChatHelp);

        Section(sb, TranslationStrings.Gestures_Section_More);
        Item(sb, TranslationStrings.Gestures_NumberStep);
        Item(sb, TranslationStrings.Gestures_FavoriteColor);
        Item(sb, TranslationStrings.Gestures_SkipIntro);

        return sb.ToString();
    }

    private static void Section(StringBuilder sb, TranslationStrings.TranslationString header)
    {
        sb.AppendLine();
        sb.AppendLine($"<color=#ffffbe><b>{header.LocalizedString}</b></color>");
    }

    private static void Item(StringBuilder sb, TranslationStrings.TranslationString line)
    {
        sb.AppendLine($"• {line.LocalizedString}");
    }
}
