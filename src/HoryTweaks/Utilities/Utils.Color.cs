using BepInEx.Unity.IL2CPP.Utils;
using HoryTweaks.Generated;
using HoryTweaks.Game;
using HoryTweaks.Features.Chat;
using HoryTweaks.Infrastructure.UnityInterop;
using InnerNet;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HoryTweaks.Utilities;

/// <summary>
/// Provides utility methods for string manipulation, network operations, player lookups, and game utilities.
/// </summary>
internal static partial class Utils
{
    /// <summary>
    /// Gets the hexadecimal color code for a role team.
    /// </summary>
    /// <param name="team">The role team type.</param>
    /// <returns>The hex color string for the team.</returns>
    internal static string GetTeamHexColor(RoleTeamTypes team)
    {
        return team == RoleTeamTypes.Impostor ? "#f00202" : "#8cffff";
    }


    /// <summary>
    /// Converts a Color32 to a hexadecimal string.
    /// </summary>
    /// <param name="color">The Color32 to convert.</param>
    /// <returns>The hexadecimal color string.</returns>
    internal static string Color32ToHex(Color32 color) => $"#{color.r:X2}{color.g:X2}{color.b:X2}{255:X2}";


    /// <summary>
    /// Converts a hexadecimal string to a Color32.
    /// </summary>
    /// <param name="hex">The hexadecimal color string.</param>
    /// <returns>The Color32 representation.</returns>
    internal static Color HexToColor32(string hex)
    {
        if (hex.StartsWith("#"))
        {
            hex = hex[1..];
        }

        byte r = byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

        return new Color32(r, g, b, 255);
    }


    /// <summary>
    /// Linearly interpolates between multiple colors based on a value within a range.
    /// </summary>
    /// <param name="colors">The array of colors to interpolate between.</param>
    /// <param name="lerpRange">The minimum and maximum range for interpolation.</param>
    /// <param name="t">The interpolation value within the range.</param>
    /// <param name="reverse">Whether to reverse the color array order.</param>
    /// <returns>The interpolated color.</returns>
    internal static Color LerpColor(Color[] colors, (float min, float max) lerpRange, float t, bool reverse = false)
    {
        float normalizedT = Mathf.InverseLerp(lerpRange.min, lerpRange.max, t);

        if (colors.Length == 1)
            return colors[0];

        if (reverse)
        {
            Array.Reverse(colors);
        }

        if (normalizedT <= 0f)
            return colors[0];
        if (normalizedT >= 1f)
            return colors[^1];

        float segmentSize = 1f / (colors.Length - 1);
        int segmentIndex = (int)(normalizedT / segmentSize);
        float segmentT = (normalizedT - segmentIndex * segmentSize) / segmentSize;

        return Color.Lerp(colors[segmentIndex], colors[segmentIndex + 1], segmentT);
    }
}
