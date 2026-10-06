using BepInEx.Unity.IL2CPP.Utils;
using BetterAmongUs.Generated;
using BetterAmongUs.Game;
using BetterAmongUs.Features.Chat;
using BetterAmongUs.Infrastructure.UnityInterop;
using InnerNet;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BetterAmongUs.Utilities;

/// <summary>
/// Provides utility methods for string manipulation, network operations, player lookups, and game utilities.
/// </summary>
internal static partial class Utils
{
    /// <summary>
    /// Adds a private chat message with custom formatting options.
    /// </summary>
    /// <param name="text">The message text.</param>
    /// <param name="overrideName">Optional override for the sender name.</param>
    /// <param name="setRight">Whether to align the message to the right side.</param>
    internal static void AddChatPrivate(string text, string overrideName = "", bool setRight = false)
    {
        if (!GameState.IsInGame)
            return;

        var chat = HudManager.Instance.Chat;
        if (chat == null)
            return;

        var data = PlayerControl.LocalPlayer.Data;
        if (data == null)
            return;

        var pooledBubble = chat.GetPooledBubble();
        var messageName = $"<color=#ffffff><b>(<color=#00ff44>{TranslationStrings.SystemMessage}</color>)</b>" + ChatPatch.COMMAND_POSTFIX_ID;

        if (!string.IsNullOrEmpty(overrideName))
            messageName = overrideName + ChatPatch.COMMAND_POSTFIX_ID;

        try
        {
            pooledBubble.transform.SetParent(chat.scroller.Inner);
            pooledBubble.transform.localScale = Vector3.one;
            pooledBubble.SetCosmetics(data);
            pooledBubble.gameObject.transform.Find("PoolablePlayer").gameObject.SetActive(false);
            pooledBubble.ColorBlindName.gameObject.SetActive(false);

            if (!setRight)
            {
                pooledBubble.SetLeft();
                pooledBubble.gameObject.transform.Find("NameText (TMP)").transform.localPosition += new Vector3(-0.7f, 0f);
                pooledBubble.gameObject.transform.Find("ChatText (TMP)").transform.localPosition += new Vector3(-0.7f, 0f);
            }
            else
            {
                pooledBubble.SetRight();
            }

            chat.SetChatBubbleName(pooledBubble, data, false, false, PlayerNameColor.Get(data), null);
            pooledBubble.SetText(text);
            pooledBubble.AlignChildren();
            chat.AlignAllBubbles();
            pooledBubble.NameText.text = messageName;

            if (!chat.IsOpenOrOpening && chat.notificationRoutine == null)
            {
                chat.notificationRoutine = chat.StartCoroutine(chat.BounceDot());
            }

            SoundManager.Instance.PlaySound(chat.messageSound, false, 1f, null).pitch = 0.5f + data.PlayerId / 15f;
        }
        catch
        {
        }
    }

    // System type checks


    /// <summary>
    /// Shows a settings change notification in the HUD.
    /// </summary>
    /// <param name="id">The notification ID for deduplication.</param>
    /// <param name="text">The notification text.</param>
    /// <param name="playSound">Whether to play the notification sound.</param>
    internal static void SettingsChangeNotifier(int id, string text, bool playSound = true)
    {
        var notifier = HudManager.Instance.Notifier;

        if (notifier.lastMessageKey == id && notifier.activeMessages.Count > 0)
        {
            notifier.activeMessages[^1].UpdateMessage(text);
        }
        else
        {
            notifier.lastMessageKey = id;
            var newMessage = UnityEngine.Object.Instantiate(
                notifier.notificationMessageOrigin,
                Vector3.zero,
                Quaternion.identity,
                notifier.transform
            );

            newMessage.transform.localPosition = new Vector3(0f, 0f, -2f);
            newMessage.SetUp(text, notifier.settingsChangeSprite, notifier.settingsChangeColor, (Action)(() =>
            {
                notifier.OnMessageDestroy(newMessage);
            }));

            notifier.ShiftMessages();
            notifier.AddMessageToQueue(newMessage);
        }

        if (playSound)
        {
            SoundManager.Instance.PlaySoundImmediate(notifier.settingsChangeSound, false, 1f, 1f, null);
        }
    }


    /// <summary>
    /// Disconnects the local player from the game with an optional reason message.
    /// </summary>
    /// <param name="reason">The reason for disconnection.</param>
    /// <param name="showReason">Whether to show the reason in a popup.</param>
    internal static void DisconnectSelf(string reason, bool showReason = true)
    {
        AmongUsClient.Instance.StartCoroutine(CoDisconnectSelf(reason, showReason));
    }


    private static IEnumerator CoDisconnectSelf(string reason, bool showReason = true)
    {
        AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame);

        yield return new WaitForSeconds(0.2f);

        SceneChanger.ChangeScene(Constants.MAIN_MENU_SCENE);

        if (showReason)
        {
            yield return new WaitForSeconds(0.1f);

            var lines = "<color=#ebbd34>----------------------------------------------------------------------------------------------</color>";
            ShowPopUp($"{lines}\n\n\n<size=150%>{reason}</size>\n\n\n{lines}");
        }
    }


    /// <summary>
    /// Shows a popup message using the DisconnectPopup.
    /// </summary>
    /// <param name="text">The text to display.</param>
    /// <param name="enableWordWrapping">Whether to enable word wrapping.</param>
    internal static void ShowPopUp(string text, bool enableWordWrapping = false)
    {
        DisconnectPopup.Instance.gameObject.SetActive(true);
        DisconnectPopup.Instance._textArea.enableWordWrapping = enableWordWrapping;
        DisconnectPopup.Instance._textArea.text = text;
    }

    // Resource loading
}
