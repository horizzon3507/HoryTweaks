using BepInEx.Unity.IL2CPP.Utils;
using BetterAmongUs.Attributes;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.Modules.Updater;
using BetterAmongUs.Network;
using BetterAmongUs.Network.Loaders;
using BetterAmongUs.Utilities;
using BetterAmongUs.Utilities.Extension;
using Il2CppInterop.Runtime.Attributes;
using System.Collections;
using TMPro;
using UnityEngine;

namespace BetterAmongUs.Managers;

/// <summary>
/// Manages update functionality for BetterAmongUs, including download and installation.
/// </summary>
[RegisterInIl2Cpp]
internal sealed class BAUUpdateManager : MonoBehaviour
{
    private bool _updateing;
    private GameObject? mainMenu;
    private GameObject? ambience;

    /// <summary>
    /// Gets the singleton instance of the UpdateManager.
    /// </summary>
    internal static BAUUpdateManager? Instance { get; private set; }

    /// <summary>
    /// Gets whether the application is waiting for a restart after an update.
    /// </summary>
    internal static bool WaitForRestart { get; private set; }

    /// <summary>
    /// Initializes the UpdateManager singleton.
    /// </summary>
    internal static void Init()
    {
        var obj = new GameObject("UpdateManager(BAU)") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(obj);
        Instance = obj.AddComponent<BAUUpdateManager>();
    }

    /// <summary>
    /// Called when the main menu is loaded to set up update UI elements.
    /// </summary>
    internal void OnMainMenu()
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_ModUpdate))
            return;

        var doNotPress = FindObjectOfType<DoNotPressButton>(true);
        if (doNotPress != null)
        {
            doNotPress.gameObject.SetActive(BAUUpdateLoader.UpdateInfo?.IsNewUpdate() == true && !WaitForRestart);
            var buttonPressed = doNotPress.transform.Find("ButtonPressed");
            if (buttonPressed != null)
            {
                doNotPress.pressedSprite = buttonPressed.gameObject.GetComponent<SpriteRenderer>();
            }
            var buttonUnpressed = doNotPress.transform.Find("ButtonUnpressed");
            if (buttonUnpressed != null)
            {
                doNotPress.unpressedSprite = buttonUnpressed.gameObject.GetComponent<SpriteRenderer>();
            }
            doNotPress.pressedSprite.enabled = false;
            doNotPress.pressedSprite.color = new(0.15f, 0.8f, 0.4f);
            doNotPress.unpressedSprite.color = new(0.15f, 0.8f, 0.4f);
            var button = doNotPress.GetComponent<PassiveButton>();
            if (button != null)
            {
                button.OnClick = new();
                button.OnClick.AddListener(() =>
                {
                    if (_updateing || WaitForRestart)
                        return;

                    this.StartCoroutine(CoPressDownload(doNotPress));
                });
            }

            var obj = new GameObject("Update(TMP)");
            obj.transform.SetParent(doNotPress.transform, false);
            obj.transform.localPosition = new Vector3(-0.1018f, -0.1883f, 0f);
            var text = obj.AddComponent<TextMeshPro>();
            text.color = Color.black;
            text.fontSize = 1.5f;
            text.alignment = TextAlignmentOptions.Center;
            text.horizontalAlignment = HorizontalAlignmentOptions.Center;
            text.SetText(TranslationStrings.Update_Button.LocalizedString);
        }
    }

    private void Start()
    {
        UpdateAssemblyInstaller.CleanupLeftovers(BAUPlugin.ModInfo.Assembly.Location);
    }

    /// <summary>
    /// Coroutine that handles the download process when the update button is pressed.
    /// The main menu, ambience and loading bar are restored on every outcome, and success is only
    /// reported when <see cref="BAUUpdateDownloader.CoDownloadAndInstall"/> confirms the install.
    /// </summary>
    /// <param name="button">The DoNotPressButton that was clicked.</param>
    /// <returns>An IEnumerator for the coroutine.</returns>
    [HideFromIl2Cpp]
    private IEnumerator CoPressDownload(DoNotPressButton button)
    {
        _updateing = true;

        button.pressedSprite.enabled = true;
        button.unpressedSprite.enabled = false;
        yield return new WaitForSeconds(0.1f);
        button.unpressedSprite.enabled = true;
        button.pressedSprite.enabled = false;
        yield return new WaitForSeconds(0.1f);
        button.gameObject.SetActive(false);

        mainMenu = GameObject.Find("MainMenuManager");
        ambience = GameObject.Find("Ambience");
        mainMenu?.SetActive(false);
        ambience?.SetActive(false);

        UpdateOutcome? outcome = null;
        var updateInfo = BAUUpdateLoader.UpdateInfo;
        if (updateInfo == null || !updateInfo.IsNewUpdate())
        {
            outcome = UpdateOutcome.Failure(UpdateStatus.MissingDownloadLink, "No newer update is available in the feed.");
        }
        else
        {
            var routine = BAUUpdateDownloader.CoDownloadAndInstall(updateInfo, result => outcome = result);
            while (true)
            {
                object? current;
                try
                {
                    if (!routine.MoveNext())
                    {
                        break;
                    }
                    current = routine.Current;
                }
                catch (Exception ex)
                {
                    BAUPlugin.Logger.Error($"Update failed with an exception: {ex}");
                    outcome = UpdateOutcome.Failure(UpdateStatus.Unexpected, ex.Message);
                    break;
                }
                yield return current;
            }
        }

        if (outcome == null)
        {
            outcome = UpdateOutcome.Failure(UpdateStatus.Unexpected, "The update routine ended without reporting a result.");
        }

        if (outcome.IsSuccess)
        {
            WaitForRestart = true;
            BAUPlugin.Logger.Log($"Update installed: {outcome.Detail}");
        }
        else
        {
            BAUPlugin.Logger.Error($"Update not installed ({outcome.Status}): {outcome.Detail}");
        }

        RestoreMenu(button, showUpdateButton: !outcome.IsSuccess);
        yield return new WaitForSeconds(0.2f);
        Utils.ShowPopUp(GetOutcomeMessage(outcome));
        _updateing = false;
    }

    /// <summary>
    /// Brings the main menu back after an update attempt, never throwing so the flow always completes.
    /// </summary>
    private void RestoreMenu(DoNotPressButton button, bool showUpdateButton)
    {
        try
        {
            if (LoadingBarManager.InstanceExists)
            {
                CustomLoadingBarManager.ToggleLoadingBar(false);
            }

            if (showUpdateButton && button != null)
            {
                button.gameObject.SetActive(true);
            }
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Warning($"Could not restore the update UI after the update attempt: {ex.Message}");
        }

        mainMenu?.SetActive(true);
        ambience?.SetActive(true);
    }

    /// <summary>
    /// Maps an outcome to the localized, actionable popup text shown to the player.
    /// </summary>
    private static string GetOutcomeMessage(UpdateOutcome outcome)
    {
        var modName = TranslationStrings.BetterAmongUs.LocalizedString;
        return outcome.Status switch
        {
            UpdateStatus.Succeeded => TranslationStrings.Update_Complete.Format(modName),
            UpdateStatus.NoInternet => TranslationStrings.Update_Failed_NoInternet.LocalizedString,
            UpdateStatus.MissingDownloadLink => TranslationStrings.Update_Failed_MissingLink.LocalizedString,
            UpdateStatus.DownloadFailed => TranslationStrings.Update_Failed_Download.LocalizedString,
            UpdateStatus.InvalidPayload => TranslationStrings.Update_Failed_InvalidPayload.Format(modName),
            UpdateStatus.InstallFailed => TranslationStrings.Update_Failed_Install.LocalizedString,
            _ => TranslationStrings.Update_Failed_Unexpected.LocalizedString,
        };
    }
}