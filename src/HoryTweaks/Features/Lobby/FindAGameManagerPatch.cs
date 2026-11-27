using HoryTweaks.Utilities;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;
using HoryTweaks.Game;
using HoryTweaks.Infrastructure.UnityInterop;

namespace HoryTweaks.Features.Lobby;

[HarmonyPatch]
internal static class FindAGameManagerPatch
{
    public static Scroller? Scroller;

    [HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.Start))]
    [HarmonyPrefix]
    private static void FindAGameManager_Start_Prefix(FindAGameManager __instance)
    {
        // Apply custom UI colors to all Find Game menu buttons
        if (__instance.refreshButton != null)
            __instance.refreshButton.gameObject.SetUIColors();
        if (__instance.BackButton != null)
            __instance.BackButton.gameObject.SetUIColors();
        if (__instance.clearFilterButton != null)
            __instance.clearFilterButton.gameObject.SetUIColors("Disabled");
        if (__instance.serverButton != null)
        {
            __instance.serverButton.gameObject.SetUIColors("Inactive", "Disabled", "Background");
            __instance.serverButton.activeTextColor = Color.cyan * 0.3f;
        }

        if (__instance.gameContainers == null || __instance.gameContainers.Length == 0)
            return;

        // Modify game container hover colors
        foreach (var con in __instance.gameContainers)
        {
            if (con == null)
                continue;
            var roll = con.GetComponent<ButtonRolloverHandler>();
            if (roll != null)
                roll.OverColor = (roll.OverColor * 0.6f) + (Colors.Theme * 0.5f);
        }

        // Create scrollable container for game listings
        var prefab = __instance.gameContainers[^1];
        if (prefab == null)
            return;
        var list = new GameObject("GameListScroller");
        list.transform.SetParent(prefab.transform.parent);

        // Set up scroller component for vertical scrolling
        Scroller = list.AddComponent<Scroller>();
        Scroller.Inner = list.transform;
        Scroller.MouseMustBeOverToScroll = true;
        var box = prefab.transform.parent.gameObject.AddComponent<BoxCollider2D>();
        box.size = new Vector2(100f, 100f);
        Scroller.ClickMask = box;
        Scroller.ScrollWheelSpeed = 0.3f;
        Scroller.SetYBoundsMin(0f);
        Scroller.SetYBoundsMax(3.5f);
        Scroller.allowY = true;

        // Move existing game containers into scrollable list
        foreach (var con in __instance.gameContainers)
        {
            if (con == null)
                continue;
            con.transform.SetParent(list.transform);
            var oldPos = con.transform.position;
            con.transform.position = new Vector3(oldPos.x, oldPos.y, 25);
        }

        // Add more game containers to support longer server lists
        var oldGameContainers = __instance.gameContainers.ToList();

        for (int i = 0; i < 5; i++)
        {
            var GameContainer = UnityEngine.Object.Instantiate(prefab, list.transform);
            var oldPos = GameContainer.transform.position;
            GameContainer.transform.position = new Vector3(oldPos.x, oldPos.y - 0.75f * (i + 1), 25);
            oldGameContainers.Add(GameContainer);
        }

        __instance.gameContainers = oldGameContainers.ToArray();

        // Create visual cutoff for scrolling content
        var cutOffTop = CreateBlackSquareSprite();
        cutOffTop.transform.SetParent(list.transform.parent);
        cutOffTop.transform.localPosition = new Vector3(0, 3, 1);
        cutOffTop.transform.localScale = new Vector3(1500, 200, 100);
    }

    [HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.RefreshList))]
    [HarmonyPostfix]
    private static void FindAGameManager_RefreshList_Postfix(FindAGameManager __instance)
    {
        // Reset scroll position when refreshing game list
        Scroller?.ScrollRelative(new(0f, -100f));
    }

    [HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.HandleList))]
    [HarmonyPostfix]
    private static void FindAGameManager_HandleList_Postfix(FindAGameManager __instance, HttpMatchmakerManager.FindGamesListFilteredResponse response)
    {
        if (__instance == null || __instance.gameContainers == null || response == null || response.Games == null)
            return;

        // Clear existing containers before populating with new data
        __instance.ResetContainers();
        GameListing[] games = response.Games.ToArray();

        // Sort games: first by player count (highest first), then by host name
        games = [.. games.OrderByDescending(game => game.PlayerCount).ThenBy(game => game.TrueHostName)];
        int gameNum = 0;
        int count = 0;

        // Populate visible containers with available games
        while (count < __instance.gameContainers.Length && count < games.Count())
        {
            if (games[count].Options != null && __instance.gameContainers[gameNum] != null)
            {
                __instance.gameContainers[gameNum].gameObject.SetActive(true);
                __instance.gameContainers[gameNum].SetGameListing(games[count]);
                __instance.gameContainers[gameNum].SetupGameInfo();
                gameNum++;
            }
            count++;
        }

        // Add true host name and platform info to each game container
        foreach (var container in __instance.gameContainers)
        {
            if (container == null)
                continue;

            Transform child = container.transform.Find("Container");
            if (child == null)
                continue;

            Transform tmproObject = child.Find("TrueHostName_TMP");

            TMP_Text? tmpro = null;

            if (tmproObject != null)
            {
                tmpro = tmproObject.GetComponent<TextMeshPro>();
            }

            if (!container.gameObject.activeSelf || container.gameListing == null)
            {
                if (tmpro != null)
                    tmpro.text = string.Empty;
                continue;
            }

            if (tmpro == null)
            {
                tmpro = CreateNewTextMeshPro(child);
            }

            if (tmpro != null)
            {
                tmpro.font = container.capacity?.font;
                tmpro.fontSize = 3f;
                tmpro.text = FormatGameInfoText(container.gameListing);
            }
        }
    }

    private static TMP_Text CreateNewTextMeshPro(Transform parent)
    {
        // Create new TextMeshPro component for displaying host info
        var tmproObject = new GameObject("TrueHostName_TMP").transform;
        tmproObject.SetParent(parent, true);

        // Position text in container
        var aspectPos = tmproObject.gameObject.AddComponent<AspectPosition>();
        aspectPos.Alignment = AspectPosition.EdgeAlignments.Center;
        aspectPos.anchorPoint = new Vector2(0.2f, 0.5f);
        aspectPos.DistanceFromEdge = new Vector3(10.9f, -2.17f, -2f);
        aspectPos.AdjustPosition();

        return tmproObject.gameObject.AddComponent<TextMeshPro>();
    }

    private static string FormatGameInfoText(GameListing listing)
    {
        // Format game info text with host name, platform, and game code
        var hostStr = !string.IsNullOrEmpty(listing.TrueHostName) ? listing.TrueHostName : listing.HostName;
        return @$"{hostStr}{Environment.NewLine}<size=65%>{Utils.GetPlatformName(listing.Platform)} ({GameCode.IntToGameName(listing.GameId)})";
    }

    private static Sprite? _cachedBlackSquareSprite;

    private static SpriteRenderer CreateBlackSquareSprite()
    {
        // Reuse a single 1x1 black sprite instead of allocating and leaking a 100x100 Texture2D each time
        if (_cachedBlackSquareSprite == null)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.black);
            texture.Apply();
            texture.hideFlags |= HideFlags.HideAndDontSave;

            _cachedBlackSquareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 100f);
            _cachedBlackSquareSprite.hideFlags |= HideFlags.HideAndDontSave;
        }

        var square = new GameObject("CutOffTop");
        var renderer = square.AddComponent<SpriteRenderer>();
        renderer.sprite = _cachedBlackSquareSprite;
        square.transform.localScale = new Vector3(100, 100, 1);
        return renderer;
    }
}