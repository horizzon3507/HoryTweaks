using TMPro;
using UnityEngine;
using HoryTweaks.Infrastructure.UnityInterop;

namespace HoryTweaks.Features.ClientOptions;

/// <summary>
/// Text and button widgets for custom pages inside the Tweaks options tab, cloned from an options toggle.
/// </summary>
internal static class OptionsPageWidgets
{
    internal static readonly Color32 SelectedColor = new(0, 150, 0, 255);
    internal static readonly Color32 IdleColor = new(77, 77, 77, 255);
    internal static readonly Color32 DangerColor = new(150, 40, 40, 255);

    /// <summary>
    /// Creates an empty white text label cloned from the template's text.
    /// </summary>
    internal static TextMeshPro? CreateText(Transform parent, ToggleButtonBehaviour template, string name, Vector3 position, Vector2 size, float fontScale, TextAlignmentOptions alignment)
    {
        if (template.Text == null)
            return null;

        var go = UnityEngine.Object.Instantiate(template.Text.gameObject, parent);
        go.name = name;
        go.DestroyTextTranslators();
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one;

        var text = go.GetComponent<TextMeshPro>();
        if (text == null)
            return null;

        text.text = string.Empty;
        text.enableAutoSizing = false;
        text.fontSize *= fontScale;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;
        text.alignment = alignment;
        text.color = Color.white;
        text.rectTransform.sizeDelta = size;
        go.SetActive(true);
        return text;
    }

    /// <summary>
    /// Creates a button with a horizontally scaled background that runs <paramref name="onClick"/>.
    /// </summary>
    internal static ToggleButtonBehaviour? CreateButton(Transform parent, ToggleButtonBehaviour template, string label, Vector3 position, float widthScale, Action onClick)
    {
        var button = UnityEngine.Object.Instantiate(template, parent);
        button.name = label;
        button.transform.localPosition = position;
        button.gameObject.DestroyTextTranslators();

        var background = button.transform.Find("Background");
        if (background != null)
        {
            background.localScale = new Vector3(widthScale, 1f, 1f);
        }

        var highlight = button.transform.Find("ButtonHighlight");
        if (highlight != null)
        {
            highlight.localScale = new Vector3(widthScale, 0.95f, 1f);
        }

        if (button.Text != null)
        {
            var rect = button.Text.rectTransform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x * widthScale, rect.sizeDelta.y);
            button.Text.enableAutoSizing = true;
            button.Text.fontSizeMax = button.Text.fontSize;
            button.Text.fontSizeMin = button.Text.fontSize * 0.5f;
            button.Text.text = label;
            button.Text.color = Color.white;
        }

        SetButtonColor(button, IdleColor);

        var passiveButton = button.GetComponent<PassiveButton>();
        passiveButton.OnClick = new();
        passiveButton.OnClick.AddListener(onClick);

        button.gameObject.SetActive(true);
        return button;
    }

    internal static void SetButtonColor(ToggleButtonBehaviour? button, Color32 color)
    {
        if (button == null)
            return;

        if (button.Background != null)
        {
            button.Background.color = color;
        }

        if (button.Rollover != null)
        {
            button.Rollover.ChangeOutColor(color);
        }
    }

    internal static void SetButtonLabel(ToggleButtonBehaviour? button, string label)
    {
        if (button != null && button.Text != null)
        {
            button.Text.text = label;
        }
    }
}
