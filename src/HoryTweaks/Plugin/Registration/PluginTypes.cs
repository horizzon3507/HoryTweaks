namespace HoryTweaks.Plugin.Registration;

/// <summary>
/// Caches the plugin assembly's type list so reflection-driven registration scans it once per initialize pass.
/// </summary>
internal static class PluginTypes
{
    /// <summary>
    /// Gets every type declared in the HoryTweaks assembly, resolved once.
    /// </summary>
    internal static Type[] All { get; } = BAUPlugin.ModInfo.Assembly.GetTypes();
}
