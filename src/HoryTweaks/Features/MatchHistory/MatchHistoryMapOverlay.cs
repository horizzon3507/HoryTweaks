using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Utilities;
using UnityEngine;

namespace HoryTweaks.Features.MatchHistory;

/// <summary>
/// Static decorations for the replay map: vent, task-console and emergency-button icons read
/// from the loaded <see cref="ShipStatus"/> prefab's own objects, placed in the same map-local
/// space the player markers use.
/// </summary>
internal static class MatchHistoryMapOverlay
{
    private const string LogTag = "MatchHistory";
    private const string VentIconPath = "HoryTweaks.Resources.Images.Icons.Vent.png";
    private const string MeetingIconPath = "HoryTweaks.Resources.Images.Icons.Meeting.png";

    private const float OverlayZ = -0.15f;
    private const float IconHeight = 0.3f;
    private const float IconAlpha = 0.35f;
    private const float DotHeight = 0.15f;
    private const float DotAlpha = 0.3f;

    private static Sprite? dotSprite;

    /// <summary>
    /// Draws vent and task-console icons onto <paramref name="mapRoot"/> using the ship prefab's
    /// own objects, placed in the same map-local space the player markers use. <paramref name="ship"/>
    /// is the loaded ship prefab, <paramref name="map"/> its <c>ShipStatus.MapPrefab</c>.
    /// <paramref name="mapScale"/>/<paramref name="flipX"/> are the recorded match values.
    /// Call after the map background is built; safe no-op on failure.
    /// </summary>
    internal static void Draw(Transform mapRoot, ShipStatus ship, MapBehaviour map, float mapScale, bool flipX, int sortingLayerId, int sortingOrder)
    {
        try
        {
            if (mapRoot == null || ship == null || map == null)
                return;

            var mapTransform = map.transform;

            // Marker space mirrors the frame the game's map uses for HerePoint children: an
            // origin at HereIndicatorParent inside the map, scaled so a unit is one
            // ship-local / MapScale step - the space recorded positions land in.
            var herePoint = mapTransform.Find("HereIndicatorParent");
            Vector3 origin = herePoint != null ? mapTransform.InverseTransformPoint(herePoint.position) : Vector3.zero;
            Vector3 unitScale = herePoint != null ? Ratio(herePoint.lossyScale, mapTransform.lossyScale) : Vector3.one;

            var overlay = new GameObject("MapOverlay")
            {
                layer = mapRoot.gameObject.layer
            };
            overlay.transform.SetParent(mapRoot, false);
            overlay.transform.localPosition = new Vector3(origin.x, origin.y, OverlayZ);
            overlay.transform.localScale = unitScale;
            var parent = overlay.transform;
            int order = sortingOrder + 2;

            int drawn = 0;
            drawn += DrawVents(parent, ship, mapTransform, mapScale, flipX, sortingLayerId, order);
            drawn += DrawConsoles(parent, ship, mapTransform, mapScale, flipX, sortingLayerId, order);
            drawn += DrawEmergencyButton(parent, ship, mapTransform, mapScale, flipX, sortingLayerId, order);

            if (drawn == 0)
            {
                drawn += DrawPrefabVents(parent, map, origin, unitScale, sortingLayerId, order);
            }

            if (parent.childCount == 0)
            {
                overlay.DestroyObj();
            }
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }
    }

    #region Ship overlays

    private static int DrawVents(Transform parent, ShipStatus ship, Transform mapTransform, float mapScale, bool flipX, int sortingLayerId, int sortingOrder)
    {
        var sprite = Utils.LoadSprite(VentIconPath, 380f);
        if (sprite == null)
            return 0;

        // ShipStatus.AllVents is only populated at runtime, so scan the prefab children.
        int drawn = 0;
        foreach (var vent in ship.GetComponentsInChildren<Vent>(true))
        {
            if (vent == null || vent.transform.IsChildOf(mapTransform))
                continue;

            if (CreateIcon(parent, $"Vent{drawn}", sprite, ShipToMap(vent.transform.position, ship, mapScale, flipX), IconHeight, IconAlpha, sortingLayerId, sortingOrder) != null)
            {
                drawn++;
            }
        }

        return drawn;
    }

    private static int DrawConsoles(Transform parent, ShipStatus ship, Transform mapTransform, float mapScale, bool flipX, int sortingLayerId, int sortingOrder)
    {
        var sprite = GetDotSprite();
        if (sprite == null)
            return 0;

        IEnumerable<Console>? consoles = ship.AllConsoles;
        if (consoles == null || !consoles.Any())
        {
            consoles = ship.GetComponentsInChildren<Console>(true);
        }

        if (consoles == null)
            return 0;

        int drawn = 0;
        foreach (var console in consoles)
        {
            if (console == null || console.transform.IsChildOf(mapTransform))
                continue;

            if (CreateIcon(parent, $"Console{drawn}", sprite, ShipToMap(console.transform.position, ship, mapScale, flipX), DotHeight, DotAlpha, sortingLayerId, sortingOrder) != null)
            {
                drawn++;
            }
        }

        return drawn;
    }

    private static int DrawEmergencyButton(Transform parent, ShipStatus ship, Transform mapTransform, float mapScale, bool flipX, int sortingLayerId, int sortingOrder)
    {
        var button = ship.EmergencyButton;
        if (button == null || button.transform.IsChildOf(mapTransform))
            return 0;

        var sprite = Utils.LoadSprite(MeetingIconPath, 500f);
        var image = button.Image;
        if (sprite == null && image != null)
        {
            sprite = image.sprite;
        }

        if (sprite == null)
            return 0;

        return CreateIcon(parent, "EmergencyButton", sprite, ShipToMap(button.transform.position, ship, mapScale, flipX), IconHeight, IconAlpha, sortingLayerId, sortingOrder) != null ? 1 : 0;
    }

    #endregion

    #region Prefab fallback

    // Fallback when the MapPrefab is detached from its ShipStatus: vent-shaped children kept
    // inside the map prefab itself. Their positions are already map-local, so they convert
    // from mapTransform-local into the overlay's marker space.
    private static int DrawPrefabVents(Transform parent, MapBehaviour map, Vector3 origin, Vector3 unitScale, int sortingLayerId, int sortingOrder)
    {
        var sprite = Utils.LoadSprite(VentIconPath, 380f);
        if (sprite == null)
            return 0;

        var mapTransform = map.transform;
        int drawn = 0;
        foreach (var child in map.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || !child.name.Contains("vent", StringComparison.OrdinalIgnoreCase))
                continue;

            Vector3 local = mapTransform.InverseTransformPoint(child.position);
            var point = new Vector2(
                Mathf.Abs(unitScale.x) > 0.0001f ? (local.x - origin.x) / unitScale.x : 0f,
                Mathf.Abs(unitScale.y) > 0.0001f ? (local.y - origin.y) / unitScale.y : 0f);

            if (CreateIcon(parent, $"Vent{drawn}", sprite, point, IconHeight, IconAlpha, sortingLayerId, sortingOrder) != null)
            {
                drawn++;
            }
        }

        return drawn;
    }

    #endregion

    #region Unity helpers

    // The game's own map-icon math: ship-space offset divided by MapScale with x mirrored when
    // the match was played on a flipped ship - the same convention recorded positions use.
    private static Vector2 ShipToMap(Vector3 position, ShipStatus ship, float mapScale, bool flipX)
    {
        float scale = Mathf.Abs(mapScale) > 0.0001f ? mapScale : 1f;
        var offset = (position - ship.transform.position) / scale;
        return new Vector2(offset.x * (flipX ? -1f : 1f), offset.y);
    }

    private static SpriteRenderer? CreateIcon(Transform parent, string name, Sprite sprite, Vector2 point, float height, float alpha, int sortingLayerId, int sortingOrder)
    {
        try
        {
            var go = new GameObject(name)
            {
                layer = parent.gameObject.layer
            };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(point.x, point.y, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, alpha);
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;

            float scale = height / Mathf.Max(sprite.bounds.size.y, 0.0001f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            return renderer;
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
            return null;
        }
    }

    private static Sprite? GetDotSprite()
    {
        if (dotSprite != null)
            return dotSprite;

        var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        dotSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        dotSprite.hideFlags |= HideFlags.HideAndDontSave;
        return dotSprite;
    }

    private static Vector3 Ratio(Vector3 a, Vector3 b)
    {
        return new Vector3(
            Mathf.Abs(b.x) > 0.0001f ? a.x / b.x : 1f,
            Mathf.Abs(b.y) > 0.0001f ? a.y / b.y : 1f,
            Mathf.Abs(b.z) > 0.0001f ? a.z / b.z : 1f);
    }

    #endregion
}
