using BetterAmongUs.Attributes;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules;
using BetterAmongUs.Patches.Client.Managers;
using BetterAmongUs.Utilities;
using TMPro;
using UnityEngine;

namespace BetterAmongUs.MonoScripts;

/// <summary>
/// Adds a small mark button to each meeting vote area and shows a compact picker
/// for the player's role guess and status mark.
/// </summary>
[RegisterInIl2Cpp]
internal sealed class PlayerMarkMenu : MonoBehaviour
{
    private const float MarkButtonScale = 0.38f;
    private const float EntryScale = 0.4f;
    private const float EntryStepY = 0.37f;
    private const float MenuX = 4.05f;
    private const float TitleY = 2.8f;
    private const float EntryStartY = TitleY - 0.55f;

    private static PlayerMarkMenu? _instance;
    private static Sprite? _panelSprite;
    private static TextMeshPro? _textTemplate;

    private readonly List<GameObject> _entries = [];
    private SpriteRenderer? _panel;
    private TextMeshPro? _titleText;
    private byte _targetPlayerId;

    /// <summary>
    /// Adds the mark button to a vote area. Kept to the side of the row so the
    /// normal vote click keeps working.
    /// </summary>
    internal static void AttachMarkButton(PlayerVoteArea pva, byte playerId)
    {
        if (MainMenuManagerPatch.ButtonPrefab == null)
            return;

        _textTemplate ??= pva.NameText;

        var button = UnityEngine.Object.Instantiate(MainMenuManagerPatch.ButtonPrefab, pva.transform);
        button.name = "PlayerMarkButton";
        button.transform.localPosition = new Vector3(-2.03f, -0.27f, -5f);
        button.transform.localScale = new Vector3(MarkButtonScale, MarkButtonScale, MarkButtonScale);
        button.gameObject.SetActive(true);
        button.gameObject.SetLayers("UI");
        button.transform.Find("Highlight/Icon")?.gameObject.DestroyObj();
        button.transform.Find("Inactive/Icon")?.gameObject.DestroyObj();
        button.gameObject.DestroyTextTranslators();

        var text = button.GetComponentInChildren<TextMeshPro>();
        if (text != null)
        {
            text.enableAutoSizing = true;
            text.SetText(TranslationStrings.PlayerMark_Button.LocalizedString);
        }

        button.OnClick = new();
        button.OnClick.AddListener((System.Action)(() => ToggleMenu(playerId)));
    }

    /// <summary>Opens the picker for a player, or closes it when already open for them.</summary>
    private static void ToggleMenu(byte playerId)
    {
        if (_instance != null && _instance._targetPlayerId == playerId)
        {
            Close();
            return;
        }

        Close();
        Open(playerId);
    }

    private static void Open(byte playerId)
    {
        if (MeetingHud.Instance == null || MainMenuManagerPatch.ButtonPrefab == null)
            return;

        var root = new GameObject("PlayerMarkMenu");
        root.transform.SetParent(MeetingHud.Instance.transform, false);
        root.transform.localPosition = new Vector3(MenuX, 0f, -10f);
        root.SetLayers("UI");

        _instance = root.AddComponent<PlayerMarkMenu>();
        _instance._targetPlayerId = playerId;
        _instance.Build();
    }

    private static void Close()
    {
        if (_instance != null)
        {
            _instance.gameObject.DestroyObj();
            _instance = null;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Build()
    {
        _panel = CreatePanel();
        _titleText = CreateTitle();
        Rebuild();
    }

    private TextMeshPro? CreateTitle()
    {
        if (_textTemplate == null)
            return null;

        var text = UnityEngine.Object.Instantiate(_textTemplate, transform);
        text.gameObject.DestroyTextTranslators();
        text.transform.localPosition = new Vector3(0f, TitleY, -0.6f);
        text.fontSize = 1.7f;
        text.alignment = TextAlignmentOptions.Center;

        string name = Utils.PlayerDataFromPlayerId(_targetPlayerId)?.PlayerName ?? "???";
        text.SetText(TranslationStrings.PlayerMark_Title.Format(name));
        text.gameObject.SetActive(true);
        return text;
    }

    private SpriteRenderer? CreatePanel()
    {
        if (_panelSprite == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _panelSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        var go = new GameObject("Panel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.5f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = _panelSprite;
        renderer.color = new Color(0f, 0f, 0f, 0.6f);
        return renderer;
    }

    private void Rebuild()
    {
        foreach (var entry in _entries)
        {
            entry.DestroyObj();
        }
        _entries.Clear();

        var mark = PlayerMarks.Get(_targetPlayerId);
        int index = 0;

        _entries.Add(CreateHeader(TranslationStrings.PlayerMark_Roles.LocalizedString, index++));
        foreach (var role in PlayerMarks.EnabledRoles())
        {
            var current = role;
            string check = mark.RoleId == (int)role ? "✓ " : string.Empty;
            string label = $"{check}<color={role.GetRoleHex()}>{role.GetRoleName()}</color>";
            _entries.Add(CreateButton(label, () =>
            {
                PlayerMarks.ToggleRole(_targetPlayerId, current);
                Rebuild();
            }, index++));
        }

        index++; // gap between sections
        _entries.Add(CreateHeader(TranslationStrings.PlayerMark_Status.LocalizedString, index++));
        foreach (var status in EnumUtils.GetAllValues<PlayerMarkStatus>() ?? [])
        {
            var current = status;
            string check = mark.Status == status ? "✓ " : string.Empty;
            string label = $"{check}<color={PlayerMarks.StatusHex(status)}>{PlayerMarks.StatusName(status)}</color>";
            _entries.Add(CreateButton(label, () =>
            {
                PlayerMarks.ToggleStatus(_targetPlayerId, current);
                Rebuild();
            }, index++));
        }

        ResizePanel(index);
    }

    private GameObject CreateHeader(string content, int index)
    {
        if (_titleText == null)
            return new GameObject("Header");

        var go = UnityEngine.Object.Instantiate(_titleText, transform);
        go.transform.localPosition = new Vector3(0f, EntryStartY - index * EntryStepY, -0.6f);
        go.fontSize = 1.4f;
        go.color = new Color(0.62f, 0.62f, 0.62f);
        go.SetText(content);
        go.gameObject.SetActive(true);
        return go.gameObject;
    }

    private GameObject CreateButton(string content, System.Action onClick, int index)
    {
        var button = UnityEngine.Object.Instantiate(MainMenuManagerPatch.ButtonPrefab, transform);
        button.name = $"MarkEntry_{index}";
        button.transform.localPosition = new Vector3(0f, EntryStartY - index * EntryStepY, -0.6f);
        button.transform.localScale = new Vector3(EntryScale, EntryScale, EntryScale);
        button.gameObject.SetActive(true);
        button.gameObject.SetLayers("UI");
        button.transform.Find("Highlight/Icon")?.gameObject.DestroyObj();
        button.transform.Find("Inactive/Icon")?.gameObject.DestroyObj();
        button.gameObject.DestroyTextTranslators();

        var text = button.GetComponentInChildren<TextMeshPro>();
        if (text != null)
        {
            text.enableAutoSizing = true;
            text.SetText(content);
        }

        button.OnClick = new();
        button.OnClick.AddListener(onClick);
        return button.gameObject;
    }

    private void ResizePanel(int entryCount)
    {
        if (_panel == null)
            return;

        float top = TitleY + 0.35f;
        float bottom = EntryStartY - entryCount * EntryStepY - 0.05f;
        _panel.transform.localPosition = new Vector3(0f, (top + bottom) * 0.5f, 0.5f);
        _panel.transform.localScale = new Vector3(1.7f, top - bottom, 1f);
    }
}
