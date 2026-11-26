using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime;
using System.Collections;
using UnityEngine;

namespace HoryTweaks.Infrastructure.UnityInterop;

/// <summary>
/// Interface for MonoBehavior extensions.
/// Provides a standardized way to extend MonoBehaviour functionality
/// </summary>
internal interface IMonoExtension
{
    /// <summary>
    /// Gets or sets the base MonoBehavior this extension is attached to.
    /// </summary>
    /// <value>The base MonoBehaviour instance this extension extends.</value>
    MonoBehaviour? BaseMono { get; set; }

    /// <summary>
    /// Called when the extension is awakened. Initializes the extension with its base MonoBehaviour.
    /// </summary>
    /// <param name="baseMono">The base MonoBehaviour this extension is attached to.</param>
    void OnExtensionAwake(MonoBehaviour baseMono);

    /// <summary>
    /// Called when the extension is being destroyed.
    /// </summary>
    void OnDestroy();

    /// <summary>
    /// Direct index mapping (owner pointer, extension type) to their extension pairs.
    /// Lookup is O(1) and does not scan per access.
    /// </summary>
    private static readonly Dictionary<(nint Owner, Type ExtensionType), ExtensionPair> _extensions = [];

    /// <summary>
    /// Reverse index mapping an extension instance back to its registration key for O(1) removal.
    /// </summary>
    private static readonly Dictionary<IMonoExtension, (nint Owner, Type ExtensionType)> _extensionKeys = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Represents a pairing between a base MonoBehavior and its extension.
    /// </summary>
    private struct ExtensionPair
    {
        /// <summary>
        /// The base MonoBehavior.
        /// </summary>
        internal MonoBehaviour? Base;

        /// <summary>
        /// The extension attached to the base.
        /// </summary>
        internal IMonoExtension? Extension;
    }

    /// <summary>
    /// Checks whether the stored extension is a live Unity object of the requested type.
    /// </summary>
    private static bool IsLiveExtension<T>(ExtensionPair pair, out T? extension) where T : IMonoExtension
    {
        if (pair.Extension is T typed &&
            typed is MonoBehaviour mono &&
            !mono.IsDestroyedOrNull())
        {
            extension = typed;
            return true;
        }

        extension = default;
        return false;
    }

    /// <summary>
    /// Determines whether a MonoBehaviour has an extension of the specified type attached to it.
    /// </summary>
    /// <typeparam name="T">The type of IMonoExtension to check for.</typeparam>
    /// <param name="monoBehaviour">The MonoBehaviour to check for the extension.</param>
    /// <returns>true if an extension of type T exists on the MonoBehaviour; otherwise, false.</returns>
    internal static bool HasExtension<T>(MonoBehaviour monoBehaviour) where T : IMonoExtension
    {
        return GetExtension<T>(monoBehaviour) != null;
    }

    /// <summary>
    /// Gets an existing extension of the specified type attached to a MonoBehaviour.
    /// Only the invalid entry found is removed; no full sweep is performed.
    /// </summary>
    /// <typeparam name="T">The type of IMonoExtension to retrieve.</typeparam>
    /// <param name="monoBehaviour">The MonoBehaviour to get the extension from.</param>
    /// <returns>The extension instance if found, otherwise default(T).</returns>
    internal static T? GetExtension<T>(MonoBehaviour monoBehaviour) where T : IMonoExtension
    {
        if (monoBehaviour == null || monoBehaviour.IsDestroyedOrNull())
            return default;

        var key = (monoBehaviour.Pointer, typeof(T));
        if (_extensions.TryGetValue(key, out var pair))
        {
            bool ownerAlive = pair.Base != null && !pair.Base.IsDestroyedOrNull();
            if (ownerAlive && IsLiveExtension(pair, out T? extension))
            {
                return extension;
            }

            if (pair.Extension != null)
            {
                _extensionKeys.Remove(pair.Extension);
            }
            _extensions.Remove(key);
        }

        return default;
    }

    /// <summary>
    /// Adds a new extension component of the specified type to the MonoBehaviour's GameObject.
    /// </summary>
    /// <typeparam name="T">The type of MonoBehaviour and IMonoExtension to add.</typeparam>
    /// <param name="monoBehaviour">The MonoBehaviour to attach the extension to.</param>
    /// <returns>The newly created extension instance, or null if creation failed.</returns>
    internal static T? AddExtension<T>(MonoBehaviour monoBehaviour) where T : MonoBehaviour, IMonoExtension
    {
        if (monoBehaviour == null || monoBehaviour.IsDestroyedOrNull())
            return null;

        var key = (monoBehaviour.Pointer, typeof(T));
        if (_extensions.TryGetValue(key, out var existing))
        {
            bool ownerAlive = existing.Base != null && !existing.Base.IsDestroyedOrNull();
            if (ownerAlive && IsLiveExtension(existing, out T? alive))
            {
                return alive;
            }

            if (existing.Extension != null)
            {
                _extensionKeys.Remove(existing.Extension);
            }
            _extensions.Remove(key);
        }

        // Only the same GameObject may supply the extension; never adopt a
        // component living on a child object with a different owner.
        T? component = monoBehaviour.GetComponent<T>();
        component ??= monoBehaviour.gameObject.AddComponent<T>();
        if (component == null)
            return null;

        _extensions[key] = new ExtensionPair
        {
            Base = monoBehaviour,
            Extension = component
        };
        _extensionKeys[component] = key;

        try
        {
            component.BaseMono = monoBehaviour;
            component.OnExtensionAwake(monoBehaviour);
        }
        catch (Exception ex)
        {
            _extensions.Remove(key);
            _extensionKeys.Remove(component);
            BAUPlugin.Logger?.Error($"Failed to attach {typeof(T).Name} to {monoBehaviour.name}: {ex}");
            return null;
        }

        return component;
    }

    /// <summary>
    /// Removes an extension from its attached MonoBehaviour in O(1) using the reverse index.
    /// </summary>
    /// <param name="monoExtension">The extension instance to remove.</param>
    internal static void TryRemoveExtension(IMonoExtension monoExtension)
    {
        if (monoExtension == null)
            return;

        if (_extensionKeys.Remove(monoExtension, out var key) &&
            _extensions.TryGetValue(key, out var pair) &&
            ReferenceEquals(pair.Extension, monoExtension))
        {
            _extensions.Remove(key);
        }
    }

    /// <summary>
    /// Runs a callback when a MonoBehavior extension becomes available.
    /// </summary>
    /// <typeparam name="T">The type of extension to wait for.</typeparam>
    /// <param name="mono">The base MonoBehavior.</param>
    /// <param name="getExtension">Function to retrieve the extension.</param>
    /// <param name="callback">Callback to execute when extension is available.</param>
    internal static void RunWhenNotNull<T>(MonoBehaviour mono, Func<T?> getExtension, Action<T> callback) where T : class, IMonoExtension
    {
        mono.StartCoroutine(CoWaitForExtension(getExtension, callback));
    }

    /// <summary>
    /// Coroutine that waits for an extension to become available.
    /// </summary>
    private static IEnumerator CoWaitForExtension<T>(Func<T?> getExtension, Action<T> callback) where T : class, IMonoExtension
    {
        T? extension;
        while ((extension = getExtension()) == null)
        {
            yield return null;
        }
        callback(extension);
    }
}

/// <summary>
/// Generic interface for MonoBehavior extensions with specific base type.
/// </summary>
/// <typeparam name="T">The type of MonoBehavior this extension attaches to. Must inherit from MonoBehaviour.</typeparam>
internal interface IMonoExtension<T> : IMonoExtension where T : MonoBehaviour
{
    /// <summary>
    /// Gets or sets the base MonoBehavior of type T.
    /// </summary>
    /// <value>The strongly-typed base MonoBehaviour instance.</value>
    new T? BaseMono { get; set; }

    /// <summary>
    /// Explicit interface implementation for non-generic BaseMono.
    /// Casts the base MonoBehaviour to type T.
    /// </summary>
    MonoBehaviour? IMonoExtension.BaseMono
    {
        get => BaseMono;
        set => BaseMono = value as T;
    }

    /// <summary>
    /// Called when the extension is awakened and attached to its base MonoBehaviour.
    /// </summary>
    /// <param name="baseMono">The base MonoBehaviour instance of type T.</param>
    void OnExtensionAwake(T baseMono);

    /// <summary>
    /// Explicit implementation of IMonoExtension.OnExtensionAwake that calls
    /// the strongly-typed OnExtensionAwake method with the correct type.
    /// </summary>
    /// <param name="baseMono">The base MonoBehaviour to cast to type T.</param>
    void IMonoExtension.OnExtensionAwake(MonoBehaviour baseMono)
    {
        OnExtensionAwake((T)baseMono);
    }
}