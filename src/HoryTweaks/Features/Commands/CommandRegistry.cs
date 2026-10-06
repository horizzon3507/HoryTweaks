using HoryTweaks.Plugin.Registration;

namespace HoryTweaks.Features.Commands;

/// <summary>
/// Owns the list of registered chat commands, built once from <see cref="RegisterCommandAttribute"/> instances.
/// </summary>
internal static class CommandRegistry
{
    private static IReadOnlyList<BaseCommand>? _commands;

    /// <summary>
    /// Gets all registered command instances.
    /// </summary>
    internal static IReadOnlyList<BaseCommand> Commands => _commands ??= Build();

    /// <summary>
    /// Initializes the command registry. Safe to call more than once.
    /// </summary>
    internal static void Initialize() => _commands ??= Build();

    private static IReadOnlyList<BaseCommand> Build()
    {
        RegisterCommandAttribute.RegisterAll();
        return [.. RegisterCommandAttribute.Instances];
    }
}
