using HoryTweaks.Networking.Rpc.Handlers.NetObjectHandlers;
using HoryTweaks.Plugin.Registration;

namespace HoryTweaks.Networking.Rpc;

/// <summary>
/// Owns the list of registered RPC handlers, built once from <see cref="RegisterRPCHandlerAttribute"/> instances.
/// </summary>
internal static class RpcHandlerRegistry
{
    private static IReadOnlyList<RPCHandler>? _handlers;
    private static UpdateSystemHandler? _updateSystemHandler;

    /// <summary>
    /// Gets all registered RPC handler instances.
    /// </summary>
    internal static IReadOnlyList<RPCHandler> Handlers => _handlers ??= Build();

    /// <summary>
    /// Gets the registered <see cref="UpdateSystemHandler"/> instance used by <see cref="BetterRpcRouter"/>.
    /// </summary>
    internal static UpdateSystemHandler UpdateSystemHandler =>
        _updateSystemHandler ??= Handlers.OfType<UpdateSystemHandler>().FirstOrDefault()!;

    /// <summary>
    /// Initializes the RPC handler registry. Safe to call more than once.
    /// </summary>
    internal static void Initialize() => _handlers ??= Build();

    private static IReadOnlyList<RPCHandler> Build()
    {
        RegisterRPCHandlerAttribute.RegisterAll();
        return [.. RegisterRPCHandlerAttribute.Instances];
    }
}
