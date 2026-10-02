using BetterAmongUs.Attributes;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules;
using BetterAmongUs.Patches.Client.Managers;
using BetterAmongUs.Utilities;
using TMPro;
using UnityEngine;

namespace BetterAmongUs.MonoScripts;

/// <summary>
/// Adds a small icon mark button to each meeting vote area and shows a compact
/// picker for the player's role guess and status mark.
/// </summary>
[RegisterInIl2Cpp]
internal sealed class PlayerMarkMenu : MonoBehaviour
{
    private const float MarkButtonScale = 0.30f;
    private const float EntryScale = 0.34f;
    private const float EntryStepY = 0.34f;
    private const float ColumnWidth = 1.65f;
    private const int MaxRowsPerColumn = 12;
    private const float MenuX = 4.05f;
    private const float TitleY = 2.55f;
    private const float EntryStartY = TitleY - 0.55f;
    private const float ScreenTopY = 3.35f;
    private const float ScreenBottomY = -2.9f;
    private const float ScreenRightX = 5.2f;

    private static PlayerMarkMenu? _instance;
    private static Sprite? _panelSprite;
    private static Sprite? _markSprite;
    private static TextMeshPro? _textTemplate;

    private readonly List<GameObject> _entries = [];
    private SpriteRenderer? _panel;
    private TextMeshPro? _titleText;
    private byte _targetPlayerId;
    private float _targetRowY;
    private float _panelBottomLocal;
    private float _panelWidth = 1.75f;
    private int _columns = 1;

    /// <summary>
    /// Adds the mark button to a vote area, aligned to the right edge of the row.
    /// The local player never gets a button: self-marking is not meaningful.
    /// </summary>
    internal static void AttachMarkButton(PlayerVoteArea pva, byte playerId)
    {
        if (MainMenuManagerPatch.ButtonPrefab == null)
            return;

        var local = PlayerControl.LocalPlayer;
        if (local != null && local.PlayerId == playerId)
            return;

        _textTemplate ??= pva.NameText;

        var button = UnityEngine.Object.Instantiate(MainMenuManagerPatch.ButtonPrefab, pva.transform);
        button.name = "PlayerMarkButton";
        button.transform.localPosition = new Vector3(2.28f, -0.05f, -5f);
        button.transform.localScale = new Vector3(MarkButtonScale, MarkButtonScale, MarkButtonScale);
        button.gameObject.SetActive(true);
        button.gameObject.SetLayers("UI");
        button.gameObject.DestroyTextTranslators();

        var icon = GetMarkSprite();
        if (icon != null)
        {
            foreach (var iconRenderer in button.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (iconRenderer.name == "Icon")
                    iconRenderer.sprite = icon;
            }
        }

        var text = button.GetComponentInChildren<TextMeshPro>(true);
        if (text != null)
            text.gameObject.DestroyObj();

        button.OnClick = new();
        button.OnClick.AddListener((System.Action)(() => ToggleMenu(playerId, pva.transform.position.y)));
    }

    private static Sprite? GetMarkSprite() =>
        _markSprite ??= Utils.LoadSprite("BetterAmongUs.Resources.Images.Icons.Mark.png", 330f);

    /// <summary>Opens the picker for a player, or closes it when already open for them.</summary>
    private static void ToggleMenu(byte playerId, float rowY)
    {
        if (_instance != null && _instance._targetPlayerId == playerId)
        {
            Close();
            return;
        }

        Close();
        Open(playerId, rowY);
    }

    private static void Open(byte playerId, float rowY)
    {
        if (MeetingHud.Instance == null || MainMenuManagerPatch.ButtonPrefab == null)
            return;

        var local = PlayerControl.LocalPlayer;
        if (local != null && local.PlayerId == playerId)
            return;

        var root = new GameObject("PlayerMarkMenu");
        root.transform.SetParent(MeetingHud.Instance.transform, false);
        root.transform.localPosition = new Vector3(MenuX, 0f, -10f);
        root.SetLayers("UI");

        _instance = root.AddComponent<PlayerMarkMenu>();
        _instance._targetPlayerId = playerId;
        _instance._targetRowY = rowY;
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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    private void Build()
    {
        _panel = CreatePanel();
        _titleText = CreateTitle();
        Rebuild();
        FitOnScreen();
    }

    private TextMeshPro? CreateTitle()
    {
        if (_textTemplate == null)
            return null;

        var text = UnityEngine.Object.Instantiate(_textTemplate, transform);
        text.gameObject.DestroyTextTranslators();
        text.transform.localPosition = new Vector3(0f, TitleY, -0.6f);
        text.fontSize = 1.55f;
        text.alignment = TextAlignmentOptions.Center;

        string name = Utils.PlayerDataFromPlayerId(_targetPlayerId)?.PlayerName ?? "???";
        text.SetText(TranslationStrings.PlayerMark_Title.Format(name));
        text.gameObject.SetActive(true);
        return text;
    }

    private SpriteRenderer? CreatePanel()
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.5f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPanelSprite();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.color = new Color(0f, 0f, 0f, 0.68f);
        return renderer;
    }

    private static Sprite? GetPanelSprite()
    {
        if (_panelSprite != null)
            return _panelSprite;

        const int size = 48;
        const int radius = 14;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float qy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dist = Mathf.Sqrt(qx * qx + qy * qy) - radius;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - dist)));
            }
        }
        tex.Apply();

        _panelSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 1f, 0u, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return _panelSprite;
    }

    private void Rebuild()
    {
        foreach (var entry in _entries)
        {
            entry.DestroyObj();
        }
        _entries.Clear();

        var mark = PlayerMarks.Get(_targetPlayerId);
        var roles = PlayerMarks.EnabledRoles();
        var statuses = EnumUtils.GetAllValues<PlayerMarkStatus>() ?? [];

        int totalSlots = 1 + roles.Count + 1 + 1 + statuses.Length;
        _columns = Mathf.Clamp(Mathf.CeilToInt((float)totalSlots / MaxRowsPerColumn), 1, 4);

        int slot = 0;

        _entries.Add(CreateHeader(TranslationStrings.PlayerMark_Roles.LocalizedString, slot++));
        foreach (var role in roles)
        {
            var current = role;
            string check = mark.RoleId == (int)role ? "✓ " : string.Empty;
            string label = $"{check}<color={role.GetRoleHex()}>{role.GetRoleName()}</color>";
            _entries.Add(CreateButton(label, () =>
            {
                PlayerMarks.ToggleRole(_targetPlayerId, current);
                Rebuild();
            }, slot++));
        }

        slot++; // gap between sections
        _entries.Add(CreateHeader(TranslationStrings.PlayerMark_Status.LocalizedString, slot++));
        foreach (var status in statuses)
        {
            var current = status;
            string check = mark.Status == status ? "✓ " : string.Empty;
            string label = $"{check}<color={PlayerMarks.StatusHex(status)}>{PlayerMarks.StatusName(status)}</color>";
            _entries.Add(CreateButton(label, () =>
            {
                PlayerMarks.ToggleStatus(_targetPlayerId, current);
                Rebuild();
            }, slot++));
        }

        ResizePanel(Mathf.Min(totalSlots, MaxRowsPerColumn));
    }

    private Vector3 EntryPosition(int slot)
    {
        int col = slot / MaxRowsPerColumn;
        int row = slot % MaxRowsPerColumn;
        float x = _columns == 1 ? 0f : (col - (_columns - 1) * 0.5f) * ColumnWidth;
        return new Vector3(x, EntryStartY - row * EntryStepY, -0.6f);
    }

    private GameObject CreateHeader(string content, int slot)
    {
        if (_titleText == null)
            return new GameObject("Header");

        var go = UnityEngine.Object.Instantiate(_titleText, transform);
        go.transform.localPosition = EntryPosition(slot);
        go.fontSize = 1.25f;
        go.color = new Color(0.62f, 0.62f, 0.62f);
        go.SetText(content);
        go.gameObject.SetActive(true);
        return go.gameObject;
    }

    private GameObject CreateButton(string content, System.Action onClick, int slot)
    {
        var button = UnityEngine.Object.Instantiate(MainMenuManagerPatch.ButtonPrefab, transform);
        button.name = $"MarkEntry_{slot}";
        button.transform.localPosition = EntryPosition(slot);
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

    private void ResizePanel(int rows)
    {
        if (_panel == null)
            return;

        float top = TitleY + 0.4f;
        float bottom = EntryStartY - rows * EntryStepY - 0.1f;
        _panelBottomLocal = bottom;
        _panelWidth = _columns == 1 ? 1.75f : _columns * ColumnWidth + 0.3f;
        _panel.transform.localPosition = new Vector3(0f, (top + bottom) * 0.5f, 0.5f);
        _panel.size = new Vector2(_panelWidth, top - bottom);
    }

    /// <summary>
    /// Slides the menu so its top hugs the target's row when there is room, and
    /// clamps it inside the visible area when the list would run off-screen.
    /// </summary>
    private void FitOnScreen()
    {
        float topLocal = TitleY + 0.45f;
        float rootY = _targetRowY - topLocal;
        rootY = Mathf.Min(rootY, ScreenTopY - topLocal);
        rootY = Mathf.Max(rootY, ScreenBottomY - _panelBottomLocal);
        float rootX = Mathf.Min(MenuX, ScreenRightX - _panelWidth * 0.5f);
        transform.localPosition = new Vector3(rootX, rootY, -10f);
    }
}
