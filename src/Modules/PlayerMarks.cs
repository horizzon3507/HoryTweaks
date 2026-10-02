using AmongUs.GameOptions;
using BetterAmongUs.Generated;
using BetterAmongUs.Utilities;
using HarmonyLib;
using System.Text;

namespace BetterAmongUs.Modules;

/// <summary>
/// Game-facing facade over <see cref="PlayerMarkBook"/>: role/status toggles,
/// the roles enabled in the current match, and colored mark text for the info lanes.
/// </summary>
internal static class PlayerMarks
{
    private static readonly PlayerMarkBook _book = new();

    /// <summary>Gets the marks for a player, or an empty mark.</summary>
    internal static PlayerMark Get(byte playerId) => _book.Get(playerId);

    /// <summary>Applies or clears a role guess on a player.</summary>
    internal static void ToggleRole(byte playerId, RoleTypes role) => _book.ToggleRole(playerId, (int)role);

    /// <summary>Applies or clears a status mark on a player.</summary>
    internal static void ToggleStatus(byte playerId, PlayerMarkStatus status) => _book.ToggleStatus(playerId, status);

    /// <summary>
    /// Roles the guess picker can offer: Crewmate and Impostor always, plus every
    /// special role the current game options can actually assign.
    /// </summary>
    internal static IReadOnlyList<RoleTypes> EnabledRoles()
    {
        List<RoleTypes> roles = [RoleTypes.Crewmate, RoleTypes.Impostor];

        var options = GameOptionsManager.Instance?.CurrentGameOptions?.RoleOptions;
        if (options == null)
            return roles;

        foreach (var role in EnumUtils.GetAllValues<RoleTypes>() ?? [])
        {
            if (role is RoleTypes.Crewmate or RoleTypes.Impostor
                or RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost)
                continue;

            try
            {
                if (options.GetNumPerGame(role) > 0 && options.GetChancePerGame(role) > 0)
                    roles.Add(role);
            }
            catch
            {
                // Role not represented in the options collection for this game mode.
            }
        }

        return roles;
    }

    /// <summary>Localized name of a status mark.</summary>
    internal static string StatusName(PlayerMarkStatus status) => status switch
    {
        PlayerMarkStatus.Suspect => TranslationStrings.PlayerMark_Status_Suspect.LocalizedString,
        PlayerMarkStatus.Cleared => TranslationStrings.PlayerMark_Status_Cleared.LocalizedString,
        _ => TranslationStrings.PlayerMark_Status_Investigate.LocalizedString,
    };

    /// <summary>Display color of a status mark.</summary>
    internal static string StatusHex(PlayerMarkStatus status) => status switch
    {
        PlayerMarkStatus.Suspect => "#ff5a5a",
        PlayerMarkStatus.Cleared => "#4dd24d",
        _ => "#ffd829",
    };

    /// <summary>
    /// Colored "role status" text for the meeting and nameplate info lanes,
    /// or an empty string when the player carries no marks.
    /// </summary>
    internal static string FormatMarks(byte playerId)
    {
        var mark = _book.Get(playerId);
        if (mark.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder(64);

        if (mark.RoleId is int roleId)
        {
            var role = (RoleTypes)roleId;
            sb.Append($"<color={role.GetRoleHex()}>{role.GetRoleName()}</color>");
        }

        if (mark.Status is PlayerMarkStatus status)
        {
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append($"<color={StatusHex(status)}>{StatusName(status)}</color>");
        }

        return sb.ToString();
    }

    // Marks are per-match memory: every fresh ship wipes them.
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Awake))]
    [HarmonyPostfix]
    private static void ShipStatus_Awake_Postfix() => _book.Reset();
}
