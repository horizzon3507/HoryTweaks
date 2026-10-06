using BepInEx.Unity.IL2CPP.Utils;
using BetterAmongUs.Core.Audio;
using BetterAmongUs.Generated;
using BetterAmongUs.Game;
using BetterAmongUs.Features.Chat;
using BetterAmongUs.Infrastructure.UnityInterop;
using InnerNet;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BetterAmongUs.Utilities;

/// <summary>
/// Provides utility methods for string manipulation, network operations, player lookups, and game utilities.
/// </summary>
internal static partial class Utils
{
    /// <summary>Sprite cache keyed by resource path plus pixels-per-unit.</summary>
    internal static Dictionary<string, Sprite> CachedSprites = [];

    /// <summary>
    /// Loads a sprite from an embedded resource with caching.
    /// </summary>
    /// <param name="path">The resource path.</param>
    /// <param name="pixelsPerUnit">The pixels per unit for the sprite.</param>
    /// <returns>The loaded sprite, or null if loading fails.</returns>
    internal static Sprite? LoadSprite(string path, float pixelsPerUnit = 1f)
    {
        try
        {
            var cacheKey = path + pixelsPerUnit;
            if (CachedSprites.TryGetValue(cacheKey, out var sprite))
                return sprite;

            var texture = LoadTextureFromResources(path);
            if (texture == null)
                return null;

            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;

            return CachedSprites[cacheKey] = sprite;
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex);
            return null;
        }
    }


    /// <summary>
    /// Loads a Texture2D from an embedded resource.
    /// </summary>
    /// <param name="path">The resource path.</param>
    /// <returns>The loaded texture, or null if loading fails.</returns>
    internal static Texture2D? LoadTextureFromResources(string path)
    {
        try
        {
            var stream = BAUPlugin.ModInfo.Assembly.GetManifestResourceStream(path);
            if (stream == null)
                return null;

            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                if (!texture.LoadImage(ms.ToArray(), false))
                    return null;
            }

            return texture;
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex);
            return null;
        }
    }


    /// <summary>
    /// Loads a WAV audio clip from a file on disk.
    /// </summary>
    /// <param name="filePath">The full path to the WAV file.</param>
    /// <returns>An AudioClip containing the loaded WAV data, or null if loading fails.</returns>
    internal static AudioClip? LoadWavFromDisk(string filePath)
    {
        if (!File.Exists(filePath))
        {
            BAUPlugin.Logger.Error($"File not found: {filePath}");
            return null;
        }

        if (Path.GetExtension(filePath).ToLower() != ".wav")
        {
            BAUPlugin.Logger.Error("Only .wav files are supported.");
            return null;
        }

        try
        {
            byte[] wavBytes = File.ReadAllBytes(filePath);
            if (wavBytes.Length == 0)
            {
                return null;
            }
            var audio = ToAudioClip(wavBytes);
            return audio;
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error($"Failed to load WAV: {ex}");
            return null;
        }
    }


    /// <summary>
    /// Converts raw WAV byte data into an AudioClip.
    /// </summary>
    /// <param name="wavBytes">The raw WAV file bytes.</param>
    /// <returns>An AudioClip containing the converted WAV data.</returns>
    internal static AudioClip ToAudioClip(byte[] wavBytes)
    {
        if (!WavDecoder.TryDecode(wavBytes, out var audio))
        {
            throw new InvalidDataException("Unsupported or malformed WAV data (PCM16 RIFF/WAVE required).");
        }

        float[] floatData = new float[audio.Samples.Length];
        for (int i = 0; i < audio.Samples.Length; i++)
        {
            floatData[i] = audio.Samples[i] / 32768f;
        }

        AudioClip clip = AudioClip.Create("LoadedWav", audio.Samples.Length / audio.Channels, audio.Channels, audio.SampleRate, false);
        clip.SetData(floatData, 0);
        clip.hideFlags = HideFlags.HideAndDontSave;

        return clip;
    }

    /// <summary>
    /// Generates a normalized directory path by combining folder names from a delimited string.
    /// </summary>
    /// <param name="path">A string containing folder names separated by the specified separator character. Folder names must not be empty.</param>
    /// <param name="separator">The character used to separate folder names in the input string. Defaults to '/'.</param>
    /// <returns>A directory path constructed by combining the non-empty folder names from the input string, using the system's
    /// directory separator character.</returns>
    internal static string GenerateDirectoryPath(string path, char separator = '/')
    {
        string[] folders = path.Split(separator);
        string currentPath = "";
        foreach (string folder in folders)
        {
            if (!string.IsNullOrEmpty(folder))
            {
                currentPath = Path.Combine(currentPath, folder);
            }
        }
        return currentPath;
    }

    // Platform utilities
}
