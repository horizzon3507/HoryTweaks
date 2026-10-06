
using System.Reflection;
using HoryTweaks.Features.Commands;
using HoryTweaks.Networking.Rpc;

namespace HoryTweaks.Plugin.Registration;

/// <summary>
/// Generic attribute for automatically registering static instances of a specified base type or interface.
/// </summary>
/// <typeparam name="T">The base type or interface that attributed classes must implement.</typeparam>
[AttributeUsage(AttributeTargets.Class)]
internal abstract class AutoRegisterAttribute<T> : Attribute where T : class
{
    private static bool _scanned;

    /// <summary>
    /// The collection of all discovered and registered instances of type <typeparamref name="T"/>.
    /// </summary>
    private static readonly List<T> _instances = [];

    /// <summary>
    /// Gets a read-only collection of all registered instances of type <typeparamref name="T"/>.
    /// </summary>
    internal static IReadOnlyList<T> Instances => _instances.AsReadOnly();

    /// <summary>
    /// Scans the assembly once for classes marked with the given attribute type and registers an instance of each.
    /// </summary>
    /// <param name="attributeType">The concrete attribute type marking classes for registration.</param>
    protected static void ScanAndRegister(Type attributeType)
    {
        if (_scanned)
            return;
        _scanned = true;

        foreach (var type in PluginTypes.All)
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            if (type.GetCustomAttribute(attributeType) == null)
                continue;

            if (!typeof(T).IsAssignableFrom(type))
                continue;

            var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (constructor != null && constructor.Invoke(null) is T instance)
            {
                _instances.Add(instance);
            }
        }
    }
}

// Class instances
internal sealed class RegisterCommandAttribute : AutoRegisterAttribute<BaseCommand>
{
    /// <summary>
    /// Scans the assembly and registers all command instances.
    /// </summary>
    internal static void RegisterAll() => ScanAndRegister(typeof(RegisterCommandAttribute));
}

internal sealed class RegisterRPCHandlerAttribute : AutoRegisterAttribute<RPCHandler>
{
    /// <summary>
    /// Scans the assembly and registers all RPC handler instances.
    /// </summary>
    internal static void RegisterAll() => ScanAndRegister(typeof(RegisterRPCHandlerAttribute));
}
