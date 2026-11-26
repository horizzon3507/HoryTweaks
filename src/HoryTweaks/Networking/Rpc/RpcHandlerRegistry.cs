using HoryTweaks.Modules.Support;
using HoryTweaks.Networking.Rpc.Handlers.NetObjectHandlers;
using HoryTweaks.Plugin.Registration;

namespace HoryTweaks.Networking.Rpc;

/// <summary>
/// Precomputed metadata for a registered <see cref="RPCHandler"/> so hot-path dispatch
/// never allocates strings or calls reflection on every incoming RPC.
/// </summary>
internal readonly struct RpcHandlerEntry
{
    internal readonly RPCHandler Handler;
    internal readonly string DisableAllKey;
    internal readonly string DisableHandleKey;
    internal readonly string DisableBetterHostKey;
    internal readonly string DisableGameDataTagKey;

    internal RpcHandlerEntry(RPCHandler handler)
    {
        Handler = handler;
        string baseKey = BAUModdedSupportFlags.Disable_RPCHandler + handler.GetType().Name;
        DisableAllKey = baseKey;
        DisableHandleKey = baseKey + ":" + nameof(HandlerFlag.Handle);
        DisableBetterHostKey = baseKey + ":" + nameof(HandlerFlag.BetterHost);
        DisableGameDataTagKey = baseKey + ":" + nameof(HandlerFlag.HandleGameDataTag);
    }

    internal bool IsDisabled(HandlerFlag flag)
    {
        if (BAUModdedSupportFlags.HasFlag(DisableAllKey))
            return true;

        string flagKey = flag switch
        {
            HandlerFlag.Handle => DisableHandleKey,
            HandlerFlag.BetterHost => DisableBetterHostKey,
            HandlerFlag.HandleGameDataTag => DisableGameDataTagKey,
            _ => DisableAllKey
        };

        return BAUModdedSupportFlags.HasFlag(flagKey);
    }
}

/// <summary>
/// Owns the list and O(1) lookup tables of registered RPC handlers, built once from
/// <see cref="RegisterRPCHandlerAttribute"/> instances.
/// </summary>
internal static class RpcHandlerRegistry
{
    private static IReadOnlyList<RPCHandler>? _handlers;
    private static RpcHandlerEntry[]?[]? _byCallId;
    private static RpcHandlerEntry[]?[]? _byGameDataTag;
    private static bool _hasAnyGameDataTagHandler;
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
    /// Initializes the RPC handler registry and O(1) dispatch tables. Safe to call more than once.
    /// </summary>
    internal static void Initialize()
    {
        if (_handlers == null)
        {
            _handlers = Build();
        }
    }

    /// <summary>
    /// Returns the pre-indexed handler entries registered for an RPC call ID, or null if none exist.
    /// </summary>
    internal static RpcHandlerEntry[]? GetCallIdHandlers(byte callId)
    {
        if (_byCallId == null)
        {
            Initialize();
        }

        return _byCallId![callId];
    }

    /// <summary>
    /// Returns whether any handler is registered for the given GameData tag.
    /// </summary>
    internal static bool HasGameDataTagHandler(byte tag)
    {
        if (_byGameDataTag == null)
        {
            Initialize();
        }

        return _hasAnyGameDataTagHandler && _byGameDataTag![tag] != null;
    }

    /// <summary>
    /// Returns the pre-indexed handler entries registered for a GameData tag, or null if none exist.
    /// </summary>
    internal static RpcHandlerEntry[]? GetGameDataTagHandlers(byte tag)
    {
        if (_byGameDataTag == null)
        {
            Initialize();
        }

        return _byGameDataTag![tag];
    }

    private static IReadOnlyList<RPCHandler> Build()
    {
        RegisterRPCHandlerAttribute.RegisterAll();
        RPCHandler[] handlers = [.. RegisterRPCHandlerAttribute.Instances];

        var callIdBuckets = new List<RpcHandlerEntry>?[256];
        var tagBuckets = new List<RpcHandlerEntry>?[256];
        bool hasAnyTag = false;

        foreach (var handler in handlers)
        {
            var entry = new RpcHandlerEntry(handler);

            if (handler.CallId != byte.MaxValue)
            {
                (callIdBuckets[handler.CallId] ??= []).Add(entry);
            }

            if (handler.GameDataTag != byte.MaxValue)
            {
                (tagBuckets[handler.GameDataTag] ??= []).Add(entry);
                hasAnyTag = true;
            }
        }

        var byCallId = new RpcHandlerEntry[]?[256];
        var byTag = new RpcHandlerEntry[]?[256];

        for (int i = 0; i < 256; i++)
        {
            if (callIdBuckets[i] != null)
            {
                byCallId[i] = callIdBuckets[i]!.ToArray();
            }

            if (tagBuckets[i] != null)
            {
                byTag[i] = tagBuckets[i]!.ToArray();
            }
        }

        _byCallId = byCallId;
        _byGameDataTag = byTag;
        _hasAnyGameDataTagHandler = hasAnyTag;

        return handlers;
    }
}
