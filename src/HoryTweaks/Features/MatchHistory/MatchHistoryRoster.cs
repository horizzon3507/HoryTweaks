using HoryTweaks.Core.MatchHistory;
using HoryTweaks.Features.ClientOptions;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Utilities;
using TMPro;
using UnityEngine;

namespace HoryTweaks.Features.MatchHistory;

/// <summary>
/// Static roster strip for the replay screen: one small chip per recorded player showing the
/// real crewmate look above a name label in the player's color. Impostors get a role icon or
/// a red name underline, dead players are dimmed and winners get a subtle highlight.
/// </summary>
internal static class MatchHistoryRoster
{
    private const string LogTag = "MatchHistory";
    private const int ChipsPerRow = 8;
    private const float ChipSpacing = 0.38f; // 8 chips per row: 7 gaps * spacing + chip stays under 4.6
    private const float RowHalfSpacing = 0.48f;
    private const float ChipWidth = 0.36f;
    private const float ChipHeight = 0.74f;
    private const float ChipCenterY = 0.02f;
    private const float MarkerHeight = 0.35f;
    private const float MarkerY = 0.16f;
    private const float NameY = -0.22f;
    private const float NameFontScale = 0.5f;
    private const float UnderlineY = -0.3f;
    private const float IconX = 0.15f;
    private const float IconY = 0.34f;
    private const float IconHeight = 0.15f;
    private const float DeadAlpha = 0.5f;
    private const float DisconnectedAlpha = 0.65f;

    private static readonly Vector2 NameSize = new(0.4f, 0.2f);
    private static readonly Color WinHighlightColor = new(1f, 0.82f, 0.3f, 0.15f);
    private static readonly Color ImpostorMarkColor = new(0.85f, 0.15f, 0.15f, 0.95f);

    private static Sprite? squareSprite;

    /// <summary>
    /// Builds the roster strip under <paramref name="parent"/> centered on (0,0): up to two
    /// rows of at most eight chips each. Returns the strip root, or null when there is
    /// nothing to show.
    /// </summary>
    internal static GameObject? Build(
        Transform parent,
        IReadOnlyList<MatchPlayer> players,
        ToggleButtonBehaviour textTemplate,
        int layer, int sortingLayerId, int sortingOrder)
    {
        if (parent == null || players is null || players.Count == 0)
            return null;

        var root = new GameObject("MatchHistoryRoster")
        {
            layer = layer
        };
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;

        int count = Math.Min(players.Count, ChipsPerRow * 2);
        int topCount = count <= ChipsPerRow ? count : (count + 1) / 2;
        bool twoRows = count > topCount;

        for (int i = 0; i < count; i++)
        {
            bool topRow = i < topCount;
            int col = topRow ? i : i - topCount;
            int rowCount = topRow ? topCount : count - topCount;
            float x = (col - (rowCount - 1) * 0.5f) * ChipSpacing;
            float y = twoRows ? (topRow ? RowHalfSpacing : -RowHalfSpacing) : 0f;

            BuildChip(root.transform, players[i], new Vector3(x, y, 0f), textTemplate, layer, sortingLayerId, sortingOrder);
        }

        return root;
    }

    private static void BuildChip(Transform parent, MatchPlayer player, Vector3 position, ToggleButtonBehaviour textTemplate, int layer, int sortingLayerId, int sortingOrder)
    {
        var chip = new GameObject($"Chip{player.Id}")
        {
            layer = layer
        };
        chip.transform.SetParent(parent, false);
        chip.transform.localPosition = position;
        chip.transform.localScale = Vector3.one;

        try
        {
            bool dead = player.FinalState == MatchPlayerState.Dead;
            bool gone = dead || player.FinalState == MatchPlayerState.Disconnected;

            if (player.Won)
            {
                var highlight = CreateSprite(chip.transform, "WinHighlight", GetSquareSprite(), WinHighlightColor, layer, sortingLayerId, sortingOrder);
                highlight.transform.localPosition = new Vector3(0f, ChipCenterY, 0f);
                highlight.transform.localScale = new Vector3(ChipWidth, ChipHeight, 1f);
            }

            var marker = MatchHistoryAssets.CreatePlayerMarker(chip.transform, player.Id, layer, sortingLayerId, sortingOrder + 1);
            if (marker != null)
            {
                MatchHistoryAssets.ApplyOutfit(marker, player, dead);
                MatchHistoryAssets.SetAlpha(marker, dead ? DeadAlpha : gone ? DisconnectedAlpha : 1f);
                marker.Root.transform.localPosition = new Vector3(0f, MarkerY, 0f);
                float scale = MarkerHeight / Mathf.Max(marker.NaturalHeight, 0.01f);
                marker.Root.transform.localScale = new Vector3(scale, scale, 1f);
            }

            var label = OptionsPageWidgets.CreateText(chip.transform, textTemplate, $"Name{player.Id}", new Vector3(0f, NameY, 0f), NameSize, NameFontScale, TextAlignmentOptions.Center);
            if (label != null)
            {
                string hex = Utils.Color32ToHex(MatchHistoryAssets.BodyColor(player.ColorId));
                if (gone)
                {
                    hex = $"{hex[..^2]}99";
                }

                label.text = $"<color={hex}>{player.Name ?? string.Empty}</color>";
            }

            if (!player.IsImpostor)
                return;

            var icon = MatchHistoryAssets.RoleIcon(player);
            if (icon != null)
            {
                var renderer = CreateSprite(chip.transform, "RoleIcon", icon, Color.white, layer, sortingLayerId, sortingOrder + 2);
                renderer.transform.localPosition = new Vector3(IconX, IconY, 0f);
                float iconScale = IconHeight / Mathf.Max(icon.bounds.size.y, 0.0001f);
                renderer.transform.localScale = new Vector3(iconScale, iconScale, 1f);
            }
            else
            {
                var underline = CreateSprite(chip.transform, "ImpostorMark", GetSquareSprite(), ImpostorMarkColor, layer, sortingLayerId, sortingOrder + 2);
                underline.transform.localPosition = new Vector3(0f, UnderlineY, 0f);
                underline.transform.localScale = new Vector3(ChipWidth * 0.9f, 0.03f, 1f);
            }
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
            chip.DestroyObj();
        }
    }

    private static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite, Color color, int layer, int sortingLayerId, int sortingOrder)
    {
        var go = new GameObject(name)
        {
            layer = layer
        };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.maskInteraction = SpriteMaskInteraction.None;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        squareSprite.hideFlags |= HideFlags.HideAndDontSave;
        return squareSprite;
    }
}
