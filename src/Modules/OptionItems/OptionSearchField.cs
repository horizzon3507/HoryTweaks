using BetterAmongUs.Generated;
using BetterAmongUs.Utilities;
using TMPro;
using UnityEngine;

namespace BetterAmongUs.Modules.OptionItems;

/// <summary>
/// A free-text search field pinned above the option list of an <see cref="OptionTab"/>.
/// Reuses the game's chat input field so typing, focus and the caret behave like
/// any other text box. The list re-filters live on every text change.
/// </summary>
internal sealed class OptionSearchField
{
    /// <summary>Vertical space <see cref="OptionTab"/> reserves at the top of the list for this field.</summary>
    internal const float ReservedHeight = 0.55f;

    private const float FieldMarginTop = 0.1f;
    private const float FieldScaleY = 0.6f;
    private const float FieldWidthPercent = 0.95f;
    private const float FieldZ = -5f;

    private readonly OptionTab _tab;
    private readonly FreeChatInputField _field;

    private OptionSearchField(OptionTab tab, FreeChatInputField field)
    {
        _tab = tab;
        _field = field;
    }

    /// <summary>
    /// Clones the chat input field onto the tab and wires it to
    /// <see cref="OptionTab.SetSearchQuery"/>. Returns null without changing the tab
    /// when no text field is available to clone.
    /// </summary>
    internal static OptionSearchField? Create(OptionTab tab)
    {
        var chat = HudManager.Instance;
        if (!chat || chat.Chat == null || chat.Chat.freeChatField == null || tab.AUTab == null)
            return null;

        var template = chat.Chat.freeChatField;

        var field = UnityEngine.Object.Instantiate(template, tab.AUTab.transform);
        field.gameObject.name = "SettingsSearchField";
        field.SetVisible(true);
        if (field.charCountText != null)
            field.charCountText.gameObject.SetActive(false);

        var search = new OptionSearchField(tab, field);
        search.SetupTextArea();
        search.SetupClearButton();
        search.PositionField();

        tab.SearchField = search;
        return search;
    }

    /// <summary>Clears the query text and restores the full option list.</summary>
    internal void Clear()
    {
        if (_field.textArea != null)
            _field.textArea.SetText(string.Empty, string.Empty);
        _tab.SetSearchQuery(string.Empty);
    }

    private void SetupTextArea()
    {
        // The clone keeps the chat field's submit wiring; strip it so pressing Enter
        // can never reach the chat send path.
        _field.OnSubmitEvent = null;
        _field.canSubmit = false;

        var textArea = _field.textArea;
        if (textArea == null)
            return;

        textArea.ForceUppercase = false;
        textArea.OnEnter.RemoveAllListeners();
        textArea.SetText(_tab.SearchQuery, _tab.SearchQuery);
        textArea.OnChange.AddListener((Action)(() => _tab.SetSearchQuery(textArea.text)));

        if (textArea.placeholderText != null)
            textArea.placeholderText.text = TranslationStrings.SettingsSearch_Placeholder.LocalizedString;
    }

    private void SetupClearButton()
    {
        var submit = _field.submitButton;
        if (submit == null)
            return;

        submit.OnPressed = null;
        if (submit.button != null)
        {
            submit.button.OnClick.RemoveAllListeners();
            submit.button.OnClick.AddListener((Action)(() => Clear()));
        }

        if (submit.translator != null)
            submit.translator.DestroyMono();

        var applied = TryRelabel(submit.text) || TryRelabel(submit.GetComponentInChildren<TextMeshPro>(true));
        if (applied && submit.iconSprites != null)
        {
            foreach (var icon in submit.iconSprites)
            {
                if (icon != null)
                    icon.enabled = false;
            }
        }
    }

    private static bool TryRelabel(TextMeshPro? label)
    {
        if (label == null)
            return false;

        label.text = "×";
        label.color = Color.white;
        return true;
    }

    private void PositionField()
    {
        var mask = _tab.AUTab == null ? null : _tab.AUTab.MaskArea;
        var fieldSize = _field.background == null ? Vector2.zero : _field.background.size;
        if (mask != null && fieldSize.x > 0f)
        {
            var bounds = mask.bounds;
            var scaleX = bounds.size.x * FieldWidthPercent / fieldSize.x;
            _field.transform.localScale = new Vector3(scaleX, FieldScaleY, 1f);
            _field.transform.position = new Vector3(
                bounds.center.x,
                bounds.max.y + (fieldSize.y * FieldScaleY * 0.5f) + FieldMarginTop,
                _field.transform.position.z);
            var local = _field.transform.localPosition;
            _field.transform.localPosition = new Vector3(local.x, local.y, FieldZ);
        }
        else
        {
            _field.transform.localScale = new Vector3(0.5f, FieldScaleY, 1f);
            _field.transform.localPosition = new Vector3(0.95f, 2.6f, FieldZ);
        }
    }
}
