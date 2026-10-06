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
    /// Checks if a SystemTypes value represents a sabotage type.
    /// </summary>
    /// <param name="type">The SystemTypes value to check.</param>
    /// <returns>True if the system type is a sabotage, false otherwise.</returns>
    internal static bool SystemTypeIsSabotage(SystemTypes type) => type is
        SystemTypes.Reactor or SystemTypes.Laboratory or SystemTypes.Comms or
        SystemTypes.LifeSupp or SystemTypes.MushroomMixupSabotage or
        SystemTypes.HeliSabotage or SystemTypes.Electrical;


    /// <summary>
    /// Checks if an integer value represents a sabotage SystemTypes.
    /// </summary>
    /// <param name="typeNum">The integer value to check.</param>
    /// <returns>True if the value represents a sabotage system type.</returns>
    internal static bool SystemTypeIsSabotage(int typeNum) =>
        SystemTypeIsSabotage((SystemTypes)typeNum);

    // Hashing utilities
}
