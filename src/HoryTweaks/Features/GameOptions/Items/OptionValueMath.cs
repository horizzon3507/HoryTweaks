namespace BetterAmongUs.Modules.OptionItems;

/// <summary>
/// Conversion and numeric helpers shared by option items.
/// </summary>
internal static class OptionValueMath
{
    /// <summary>
    /// Converts an imported raw value to the option's storage type.
    /// Accepts the value when it already is a <typeparamref name="T"/>,
    /// and additionally maps <see cref="int"/> to <see cref="float"/>
    /// (imported presets may store integral numbers for float options).
    /// </summary>
    /// <returns>The converted value, or null when it cannot be applied.</returns>
    internal static object? NormalizeImportValue<T>(object? value) => value switch
    {
        T typed => typed,
        int intValue when typeof(T) == typeof(float) => (float)intValue,
        _ => null
    };
}
