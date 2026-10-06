using HoryTweaks.Generated;
using HoryTweaks.Utilities;
using TMPro;
using UnityEngine;
using HoryTweaks.Modules.OptionItems;
using HoryTweaks.Infrastructure.UnityInterop;

namespace HoryTweaks.Features.GameOptions.Items.Decor;

/// <summary>
/// A settings row that runs an action when clicked instead of storing a value.
/// </summary>
internal sealed class OptionButtonItem : OptionItem
{
    private const float ConfirmWindowSeconds = 5f;
    private const int NotifierId = 2199;

    private ToggleOption? optionBehaviour;
    private TextMeshPro? stateText;
    private float armedUntil;

    internal override bool CanLoad => false;

    private TranslationStrings.TranslationString ActionLabel { get; set; }
    private TranslationStrings.TranslationString? ConfirmLabel { get; set; }
    private Func<bool> CanArm { get; set; } = () => true;
    private Action Action { get; set; } = () => { };

    private bool IsArmed => ConfirmLabel != null && Time.unscaledTime < armedUntil;

    /// <summary>
    /// Creates a button row in the given tab.
    /// </summary>
    /// <param name="tab">The tab that will hold the row.</param>
    /// <param name="translationString">The row title.</param>
    /// <param name="actionLabel">The label shown on the button.</param>
    /// <param name="action">The action to run once the click is confirmed.</param>
    /// <param name="confirmLabel">Optional label shown after the first click while waiting for confirmation.</param>
    /// <param name="canArm">Optional check run on the first click; the confirmation is only armed when it returns true.</param>
    /// <param name="parent">Optional parent option controlling visibility.</param>
    internal static OptionButtonItem Create(
        OptionTab tab,
        TranslationStrings.TranslationString translationString,
        TranslationStrings.TranslationString actionLabel,
        Action action,
        TranslationStrings.TranslationString? confirmLabel = null,
        Func<bool>? canArm = null,
        OptionItem? parent = null)
    {
        var item = new OptionButtonItem
        {
            TranslationName = translationString,
            Tab = tab,
            ActionLabel = actionLabel,
            ConfirmLabel = confirmLabel,
            CanArm = canArm ?? (() => true),
            Action = action,
            Parent = parent,
        };
        parent?.Children.Add(item);
        item.CreateBehavior();
        return item;
    }

    private void CreateBehavior()
    {
        if (!GameSettingMenu.Instance)
            return;

        AllOptionsTemp.Add(this);
        optionBehaviour = UnityEngine.Object.Instantiate(Tab.AUTab.checkboxOrigin, Tab.AUTab.settingsContainer);
        Obj = optionBehaviour.gameObject;
        Option = optionBehaviour;
        optionBehaviour.enabled = false;
        Tab.Children.Add(this);
        SetupText(optionBehaviour.TitleText);
        SetupAUOption(optionBehaviour);
        optionBehaviour.DestroyTextTranslators();
        optionBehaviour.TitleText.text = Name;
        var button = optionBehaviour.buttons[0];
        button.OnClick = new();
        button.OnClick.AddListener((Action)OnClick);

        // The check mark is replaced by a text label so the row reads as a button.
        optionBehaviour.CheckMark.enabled = false;
        stateText = UnityEngine.Object.Instantiate(optionBehaviour.TitleText, optionBehaviour.transform);
        stateText.DestroyTextTranslators();
        stateText.transform.position = optionBehaviour.CheckMark.transform.position;
        stateText.transform.SetLocalZ(optionBehaviour.TitleText.transform.localPosition.z);
        stateText.alignment = TextAlignmentOptions.Center;
        stateText.rectTransform.sizeDelta = new(2.5f, 1f);

        optionBehaviour.LabelBackground.color = Color.white;
        optionBehaviour.LabelBackground.transform.SetLocalZ(1f);
        optionBehaviour.LabelBackground.transform.localScale = new Vector3(1.6f, 0.8f, 1f);
        optionBehaviour.LabelBackground.transform.SetLocalX(-2.4f);
        UpdateVisuals();
    }

    private void OnClick()
    {
        if (ConfirmLabel != null && !IsArmed)
        {
            if (CanArm.Invoke())
            {
                armedUntil = Time.unscaledTime + ConfirmWindowSeconds;
            }

            UpdateVisuals(false);
            return;
        }

        armedUntil = 0f;
        Action.Invoke();
        UpdateVisuals(false);
    }

    internal sealed override void UpdateVisuals(bool updateTabVisuals = true)
    {
        if (optionBehaviour != null)
        {
            optionBehaviour.TitleText.text = Name;
        }

        if (stateText != null)
        {
            stateText.text = ValueAsString();
        }

        if (updateTabVisuals)
        {
            Tab.UpdateVisuals();
        }
    }

    /// <summary>
    /// Shows a HUD notification for the result of a settings action.
    /// </summary>
    /// <param name="text">The localized text to show.</param>
    internal static void Notify(string text)
    {
        if (HudManager.Instance)
        {
            Utils.SettingsChangeNotifier(NotifierId, text);
        }
    }

    public sealed override string ValueAsString() =>
        IsArmed && ConfirmLabel is { } confirm
            ? $"<color=#FF5A5A>{confirm.LocalizedString}</color>"
            : ActionLabel.LocalizedString;

    public override object? GetBoxedValue() => null;
}
