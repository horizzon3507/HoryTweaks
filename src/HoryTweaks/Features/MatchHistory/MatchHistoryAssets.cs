using HoryTweaks.Core.MatchHistory;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Utilities;
using UnityEngine;

namespace HoryTweaks.Features.MatchHistory;

/// <summary>
/// Real Among Us visuals for the replay: player markers cloned from a loaded
/// <see cref="PoolablePlayer"/> (or the player prefab's cosmetics layer), real dead-body and
/// role-icon sprites, and embedded wiki artwork as a fallback when game assets are unavailable.
/// </summary>
internal static class MatchHistoryAssets
{
    private const string LogTag = "MatchHistory";
    private const string ResourcePrefix = "HoryTweaks.Resources.Images.MatchHistory.";

    private const float SearchRetrySeconds = 2f;
    private const float ScanRetrySeconds = 30f;

    private static PoolablePlayer? poolableTemplate;
    private static float poolableNextSearch;
    private static GameObject? cosmeticsTemplate;
    private static float cosmeticsNextSearch;
    private static Sprite? deadBodySprite;
    private static float deadBodyNextSearch;
    private static Sprite? crewmateFallback;
    private static Sprite? deadBodyFallback;
    private static Sprite? ghostFallback;
    private static Material? bodyMaterial;
    private static float bodyMaterialNextSearch;
    private static readonly Dictionary<int, Sprite> roleIcons = new();
    private static float roleIconsNextSearch;
    private static readonly Dictionary<byte, Sprite?> mapFallbacks = new();
    private static readonly HashSet<byte> mapSearched = [];

    /// <summary>
    /// One replay marker: a cloned crewmate when game cosmetics are available, else a tinted sprite.
    /// </summary>
    internal sealed class PlayerMarker
    {
        internal GameObject Root = null!;
        internal PoolablePlayer? Poolable;
        internal CosmeticsLayer? Cosmetics;
        internal SpriteRenderer? Fallback;
        internal SpriteRenderer[] Renderers = [];
        internal float NaturalHeight = 1f;
        internal int AppliedKey = -1;
        internal int SortingLayerId;
        internal int SortingOrder;
    }

    /// <summary>
    /// Forces the next call to re-scan the scene for live templates. Scene searches otherwise
    /// stay cached between matches, so a match opened at the main menu no longer sticks to
    /// fallback art for the whole session.
    /// </summary>
    internal static void RefreshSceneAssets()
    {
        poolableNextSearch = 0f;
        cosmeticsNextSearch = 0f;
        deadBodyNextSearch = 0f;
    }

    /// <summary>
    /// Creates a player marker under <paramref name="parent"/>. Prefers a real cloned crewmate
    /// (full outfit via <see cref="PoolablePlayer.UpdateFromPlayerOutfit"/>), then the player
    /// prefab's cosmetics layer, and finally an embedded white crewmate sprite.
    /// </summary>
    internal static PlayerMarker? CreatePlayerMarker(Transform parent, byte playerId, int layer, int sortingLayerId, int sortingOrder)
    {
        var root = new GameObject($"Player{playerId}")
        {
            layer = layer
        };
        root.transform.SetParent(parent, false);

        var marker = new PlayerMarker { Root = root, SortingLayerId = sortingLayerId, SortingOrder = sortingOrder };
        try
        {
            var template = PoolableTemplate();
            if (template != null)
            {
                var clone = UnityEngine.Object.Instantiate(template, root.transform);
                clone.gameObject.SetActive(true);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale = Vector3.one;
                clone.ToggleName(false);
                clone.TogglePet(false);
                foreach (var pet in clone.GetComponentsInChildren<PetBehaviour>(true))
                {
                    pet.gameObject.DestroyObj();
                }

                marker.Poolable = clone;
            }
            else if (CosmeticsTemplate() != null)
            {
                var clone = UnityEngine.Object.Instantiate(cosmeticsTemplate, root.transform);
                clone!.SetActive(true);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale = Vector3.one;
                var cosmetics = clone.GetComponent<CosmeticsLayer>();
                if (cosmetics != null)
                {
                    cosmetics.ToggleName(false);
                    cosmetics.TogglePet(false);
                    cosmetics.SetNameMask(false);
                    cosmetics.SetEnabledColorblind(false);
                }

                marker.Cosmetics = cosmetics;
            }
            else
            {
                var sprite = CrewmateFallback();
                if (sprite == null)
                {
                    root.DestroyObj();
                    return null;
                }

                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                if (BodyMaterial() != null)
                {
                    renderer.sharedMaterial = bodyMaterial;
                }

                marker.Fallback = renderer;
            }
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
            root.DestroyObj();
            return null;
        }

        RefreshRenderers(marker);
        marker.NaturalHeight = MeasureHeight(marker);
        return marker;
    }

    // Outfit application can lazily create hat/skin/visor renderers, so the fixup runs again
    // after every ApplyOutfit rather than only at creation.
    private static void RefreshRenderers(PlayerMarker marker)
    {
        marker.Renderers = marker.Root.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var renderer in marker.Renderers)
        {
            if (renderer == null)
                continue;

            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.sortingLayerID = marker.SortingLayerId;
            renderer.sortingOrder = marker.SortingOrder;
        }
    }

    /// <summary>
    /// Applies a recorded outfit (color, hat, skin, visor) and ghost state to a marker.
    /// </summary>
    internal static void ApplyOutfit(PlayerMarker marker, MatchPlayer appearance, bool dead)
    {
        try
        {
            if (marker.Poolable != null)
            {
                marker.Poolable.UpdateFromPlayerOutfit(ToOutfit(appearance), PlayerMaterial.MaskType.None, dead, false);
                marker.Poolable.ToggleName(false);
                marker.Poolable.TogglePet(false);
            }
            else if (marker.Cosmetics != null)
            {
                var cosmetics = marker.Cosmetics;
                cosmetics.SetColor(appearance.ColorId);
                if (!string.IsNullOrEmpty(appearance.HatId))
                {
                    cosmetics.SetHat(appearance.HatId, appearance.ColorId);
                }

                if (!string.IsNullOrEmpty(appearance.SkinId))
                {
                    cosmetics.SetSkin(appearance.SkinId, appearance.ColorId);
                }

                if (!string.IsNullOrEmpty(appearance.VisorId))
                {
                    cosmetics.SetVisor(appearance.VisorId, appearance.ColorId);
                }

                if (dead)
                {
                    cosmetics.SetBodyAsGhost();
                }

                cosmetics.ToggleName(false);
                cosmetics.TogglePet(false);
            }
            else if (marker.Fallback != null)
            {
                var body = dead ? GhostFallback() ?? CrewmateFallback() : CrewmateFallback();
                if (body != null)
                {
                    marker.Fallback.sprite = body;
                }

                if (BodyMaterial() != null)
                {
                    PlayerMaterial.SetColors(appearance.ColorId, marker.Fallback);
                    marker.Fallback.color = Color.white;
                }
                else
                {
                    marker.Fallback.color = BodyColor(appearance.ColorId);
                }
            }

            RefreshRenderers(marker);
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }
    }

    /// <summary>
    /// Tints every renderer of a marker toward <paramref name="alpha"/> (ghosts, vents, invisibility).
    /// </summary>
    internal static void SetAlpha(PlayerMarker marker, float alpha)
    {
        foreach (var renderer in marker.Renderers)
        {
            if (renderer == null)
                continue;

            var color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    /// <summary>
    /// The real dead-body sprite from a loaded <see cref="DeadBody"/> prefab, else embedded art.
    /// </summary>
    internal static Sprite? DeadBodySprite()
    {
        if (deadBodySprite == null && Time.unscaledTime >= deadBodyNextSearch)
        {
            deadBodyNextSearch = Time.unscaledTime + ScanRetrySeconds;
            try
            {
                var prefab = UnityEngine.Object.FindObjectsOfType<DeadBody>(true)
                    .FirstOrDefault(body => body != null && body.bodyRenderers != null && body.bodyRenderers.Length > 0);
                var renderer = prefab?.bodyRenderers[0];
                if (renderer != null && renderer.sprite != null)
                {
                    deadBodySprite = renderer.sprite;
                }
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Error(ex, LogTag);
            }
        }

        if (deadBodySprite != null)
            return deadBodySprite;

        if (deadBodyFallback == null)
        {
            deadBodyFallback = Utils.LoadSprite($"{ResourcePrefix}DeadBody.png", 100f);
        }

        return deadBodyFallback;
    }

    /// <summary>
    /// The game's own role icon for an impostor-role player; null for crewmates or missing icons.
    /// </summary>
    internal static Sprite? RoleIcon(MatchPlayer player)
    {
        if (!player.IsImpostor)
            return null;

        if (roleIcons.Count == 0 && Time.unscaledTime >= roleIconsNextSearch)
        {
            roleIconsNextSearch = Time.unscaledTime + SearchRetrySeconds;
            try
            {
                var manager = RoleManager.Instance;
                if (manager != null && manager.AllRoles != null)
                {
                    foreach (var role in manager.AllRoles)
                    {
                        if (role != null && role.RoleIconWhite != null)
                        {
                            roleIcons[(int)role.Role] = role.RoleIconWhite;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Error(ex, LogTag);
            }
        }

        return roleIcons.TryGetValue(player.RoleType, out var icon) ? icon : null;
    }

    /// <summary>
    /// Embedded top-down map render for a map id; null when the map has no bundled image.
    /// </summary>
    internal static Sprite? EmbeddedMap(byte mapId)
    {
        if (!mapSearched.Contains(mapId))
        {
            mapSearched.Add(mapId);
            string? name = mapId switch
            {
                0 => "Skeld",
                1 => "MiraHQ",
                2 => "Polus",
                4 => "Airship",
                5 => "Fungle",
                _ => null
            };

            mapFallbacks[mapId] = name == null ? null : Utils.LoadSprite($"{ResourcePrefix}{name}.jpg", 100f);
        }

        return mapFallbacks.TryGetValue(mapId, out var sprite) ? sprite : null;
    }

    /// <summary>
    /// The material the game uses on player body sprites, so <see cref="PlayerMaterial.SetColors"/>
    /// produces a properly colored body on the embedded white artwork.
    /// </summary>
    internal static Material? BodyMaterial()
    {
        if (bodyMaterial != null || Time.unscaledTime < bodyMaterialNextSearch)
            return bodyMaterial;

        bodyMaterialNextSearch = Time.unscaledTime + SearchRetrySeconds;
        try
        {
            bodyMaterial = AmongUsClient.Instance?.PlayerPrefab?.cosmetics?.currentBodySprite.BodySprite?.sharedMaterial;
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }

        return bodyMaterial;
    }

    internal static Color32 BodyColor(int colorId)
    {
        return colorId >= 0 && colorId < Palette.PlayerColors.Length
            ? Palette.PlayerColors[colorId]
            : new Color32(255, 255, 255, 255);
    }

    private static NetworkedPlayerInfo.PlayerOutfit ToOutfit(MatchPlayer player)
    {
        return new NetworkedPlayerInfo.PlayerOutfit
        {
            ColorId = player.ColorId,
            HatId = string.IsNullOrEmpty(player.HatId) ? HatData.EmptyId : player.HatId,
            SkinId = string.IsNullOrEmpty(player.SkinId) ? SkinData.EmptyId : player.SkinId,
            VisorId = string.IsNullOrEmpty(player.VisorId) ? VisorData.EmptyId : player.VisorId,
            PetId = PetData.EmptyId,
            NamePlateId = NamePlateData.EmptyId,
            PlayerName = player.Name ?? string.Empty
        };
    }

    private static PoolablePlayer? PoolableTemplate()
    {
        if (poolableTemplate != null || Time.unscaledTime < poolableNextSearch)
            return poolableTemplate;

        poolableNextSearch = Time.unscaledTime + ScanRetrySeconds;
        try
        {
            poolableTemplate = UnityEngine.Object.FindObjectsOfType<PoolablePlayer>(true)
                .FirstOrDefault(candidate => candidate != null && candidate.Cosmetics != null);
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }

        return poolableTemplate;
    }

    private static GameObject? CosmeticsTemplate()
    {
        if (cosmeticsTemplate != null || Time.unscaledTime < cosmeticsNextSearch)
            return cosmeticsTemplate;

        cosmeticsNextSearch = Time.unscaledTime + ScanRetrySeconds;
        try
        {
            var cosmetics = AmongUsClient.Instance?.PlayerPrefab?.cosmetics;
            if (cosmetics != null)
            {
                cosmeticsTemplate = UnityEngine.Object.Instantiate(cosmetics.gameObject);
                cosmeticsTemplate!.name = "MatchHistoryCosmeticsTemplate";
                cosmeticsTemplate.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(cosmeticsTemplate);
            }
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }

        return cosmeticsTemplate;
    }

    private static Sprite? CrewmateFallback()
    {
        if (crewmateFallback == null)
        {
            crewmateFallback = Utils.LoadSprite($"{ResourcePrefix}Crewmate.png", 100f);
        }

        return crewmateFallback;
    }

    private static Sprite? GhostFallback()
    {
        if (ghostFallback == null)
        {
            ghostFallback = Utils.LoadSprite($"{ResourcePrefix}Ghost.png", 100f);
        }

        return ghostFallback;
    }

    private static float MeasureHeight(PlayerMarker marker)
    {
        try
        {
            var body = marker.Poolable != null
                ? marker.Poolable.Cosmetics?.currentBodySprite.BodySprite
                : marker.Cosmetics?.currentBodySprite.BodySprite;
            if (body != null && body.sprite != null)
            {
                return Mathf.Max(body.sprite.bounds.size.y, 0.01f);
            }

            float height = 0f;
            foreach (var renderer in marker.Renderers)
            {
                if (renderer != null && renderer.sprite != null)
                {
                    height = Mathf.Max(height, renderer.sprite.bounds.size.y);
                }
            }

            return Mathf.Max(height, 0.01f);
        }
        catch
        {
            return 1f;
        }
    }
}
