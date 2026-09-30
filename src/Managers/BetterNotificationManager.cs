using BetterAmongUs.Data;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules;
using BetterAmongUs.MonoScripts.Extended;
using BetterAmongUs.Utilities;
using Cpp2IL.Core.Extensions;
using TMPro;
using UnityEngine;

namespace BetterAmongUs.Managers;

/// <summary>
/// Manages in-game notifications for BetterAmongUs and system messages.
/// </summary>
internal static class BetterNotificationManager
{
    private static GameObject? BAUNotificationManagerObj;
    private static TextMeshPro? NameText;
    private static TextMeshPro? TextArea;
    private readonly static Dictionary<string, float> NotifyQueue = [];
    private static float showTime = 0f;
    private static Camera? localCamera;
    private static bool Notifying = false;

    /// <summary>
    /// Initializes the BetterNotificationManager by creating and configuring a cloned instance of the game's chat notification system.
    /// </summary>
    internal static void Init()
    {
        if (BAUNotificationManagerObj == null)
        {
            var ChatNotifications = HudManager.Instance.Chat.chatNotification;
            if (ChatNotifications != null)
            {
                ChatNotifications.timeOnScreen = 1f;
                ChatNotifications.gameObject.SetActive(true);

                // Clone chat notification system for BAU notifications
                GameObject BAUNotification = UnityEngine.Object.Instantiate(ChatNotifications.gameObject);
                BAUNotification.name = "BAUNotification";
                BAUNotification.GetComponent<ChatNotification>().DestroyMono();

                // Remove unnecessary elements from the clone
                GameObject.Find($"{BAUNotification.name}/Sizer/PoolablePlayer").DestroyObj();
                GameObject.Find($"{BAUNotification.name}/Sizer/ColorText").DestroyObj();

                // Position notification at bottom-left corner
                BAUNotification.GetComponent<AspectPosition>().DistanceFromEdge = new Vector3(-1.57f, 5.3f, -15f);
                GameObject.Find($"{BAUNotification.name}/Sizer/NameText").transform.localPosition = new Vector3(-3.3192f, -0.0105f);

                // Cache TextMeshPro component for text updates
                NameText = GameObject.Find($"{BAUNotification.name}/Sizer/NameText").GetComponent<TextMeshPro>();
                UnityEngine.Object.DontDestroyOnLoad(BAUNotification);
                BAUNotificationManagerObj = BAUNotification;
                BAUNotification.SetActive(false);

                // Reset original chat notification settings
                ChatNotifications.timeOnScreen = 0f;
                ChatNotifications.gameObject.SetActive(false);

                // Configure text wrapping for multi-line notifications
                TextArea = BAUNotificationManagerObj.transform.Find("Sizer/ChatText (TMP)").GetComponent<TextMeshPro>();
                TextArea.enableWordWrapping = true;
                TextArea.m_firstOverflowCharacterIndex = 0;
                TextArea.overflowMode = TextOverflowModes.Overflow;
            }
        }
    }

    /// <summary>
    /// Cleans up and destroys the notification manager.
    /// </summary>
    internal static void Detach()
    {
        NotifyQueue.Clear();

        if (BAUNotificationManagerObj == null)
            return;

        BAUNotificationManagerObj.DestroyObj();
    }

    /// <summary>
    /// Displays a notification message in-game.
    /// </summary>
    /// <param name="text">The text to display in the notification.</param>
    /// <param name="time">The duration in seconds to show the notification.</param>
    /// /// <param name="force">If notification should be forced even if better notifications is disabled.</param>
    internal static void Notify(string text, float time = 5f, bool force = false)
    {
        if (!BAUConfigs.BetterNotifications.Value && !force)
            return;

        if (BAUNotificationManagerObj == null)
            return;

        if (Notifying)
        {
            if (text == TextArea.text)
                return;

            NotifyQueue[text] = time;
            return;
        }

        showTime = time;
        BAUNotificationManagerObj.SetActive(true);
        NameText.text = $"<color=#00ff44>{TranslationStrings.SystemNotification}</color>";
        TextArea.text = text;
        SoundManager.Instance.PlaySound(HudManager.Instance.TaskCompleteSound, false, 1f);
        Notifying = true;
    }

    /// <summary>
    /// Clears all pending and active notifications by resetting the notification queue, display timer, and notification state.
    /// </summary>
    internal static void ClearNotifications()
    {
        NotifyQueue.Clear();
        showTime = 0f;
        Notifying = false;
    }

    /// <summary>
    /// Updates the notification manager each frame.
    /// </summary>
    internal static void Update()
    {
        if (BAUNotificationManagerObj == null)
            return;

        if (TextArea == null)
            return;

        if (!localCamera)
        {
            if (HudManager.InstanceExists)
            {
                localCamera = HudManager.Instance.GetComponentInChildren<Camera>();
            }
            else
            {
                localCamera = Camera.main;
            }
        }

        BAUNotificationManagerObj.transform.position = AspectPosition.ComputeWorldPosition(localCamera, AspectPosition.EdgeAlignments.Bottom, new Vector3(-1.3f, 0.7f, localCamera.nearClipPlane + 0.1f));

        showTime -= Time.deltaTime;
        if (showTime <= 0f && GameState.IsInGame)
        {
            TextArea.text = "";
            BAUNotificationManagerObj.SetActive(false);
            Notifying = false;

            CheckNotifyQueue();
        }

        if (!GameState.IsInGame)
        {
            BAUNotificationManagerObj.SetActive(false);
            showTime = 0f;
        }
    }

    /// <summary>
    /// Checks and processes queued notifications.
    /// </summary>
    private static void CheckNotifyQueue()
    {
        if (NotifyQueue.Any())
        {
            var key = NotifyQueue.Keys.First();
            var value = NotifyQueue[key];
            Notify(key, value);
            NotifyQueue.Remove(key);
        }
    }
}