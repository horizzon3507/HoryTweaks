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
    /// Wraps a string in size HTML tags.
    /// </summary>
    /// <param name="str">The string to format.</param>
    /// <param name="size">The size percentage.</param>
    /// <returns>The formatted string with size tags.</returns>
    internal static string Size(this string str, float size) => $"<size={size}%>{str}</size>";


    /// <summary>
    /// Removes size HTML tags from a string.
    /// </summary>
    /// <param name="text">The text to clean.</param>
    /// <returns>The text without size tags.</returns>
    internal static string RemoveSizeHtmlText(string text)
    {
        text = Regex.Replace(text, "<size=[^>]*>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</size>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "{[^}]*}", "");
        text = text.Replace("\n", " ").Replace("\r", " ").Trim();

        return text;
    }


    /// <summary>
    /// Removes all HTML tags and formatting from a string.
    /// </summary>
    /// <param name="text">The text to clean.</param>
    /// <returns>The plain text without HTML.</returns>
    internal static string RemoveHtmlText(string text)
    {
        text = Regex.Replace(text, "<[^>]*>", "");
        text = Regex.Replace(text, "{[^}]*}", "");
        text = text.Replace("\n", " ").Replace("\r", " ");
        text = text.Trim();

        return text;
    }


    /// <summary>
    /// Checks if a string contains HTML or special formatting.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>True if the text contains HTML or formatting, false otherwise.</returns>
    internal static bool IsHtmlText(string text)
    {
        return Regex.IsMatch(text, "<[^>]*>") ||
               Regex.IsMatch(text, "{[^}]*}") ||
               text.Contains("\n") ||
               text.Contains("\r");
    }

    // Player lookup methods


    /// <summary>
    /// Computes a truncated SHA256 hash of a string.
    /// </summary>
    /// <param name="str">The string to hash.</param>
    /// <returns>A 9-character hash (first 5 + last 4 characters of SHA256).</returns>
    internal static string GetHashStr(this string str)
    {
        if (string.IsNullOrEmpty(str)) return "";

        using var sha256 = SHA256.Create();
        var sha256Bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(str));
        var sha256Hash = BitConverter.ToString(sha256Bytes).Replace("-", "").ToLower();
        return sha256Hash[..5] + sha256Hash[^4..];
    }


    /// <summary>
    /// Computes a 16-bit hash of a string using SHA256.
    /// </summary>
    /// <param name="input">The string to hash.</param>
    /// <returns>A 16-bit hash value.</returns>
    internal static ushort GetHashUInt16(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0;
        return (ushort)(BitConverter.ToUInt16(SHA256.HashData(Encoding.UTF8.GetBytes(input)), 0) % 65536);
    }

    // Color utilities
}
