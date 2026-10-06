namespace BetterAmongUs.Core.Options;

/// <summary>
/// Pure numeric rules shared by int/float option items: modifier-key step
/// multipliers, range clamping and float rounding to one decimal.
/// </summary>
internal static class NumberOptionRules
{
    /// <summary>
    /// Gets the step multiplier for the current modifier keys:
    /// Shift = 5, Ctrl = 10, both = 25, neither = 1.
    /// </summary>
    internal static int StepMultiplier(bool shift, bool control)
    {
        if (shift && control) return 25;
        if (control) return 10;
        if (shift) return 5;
        return 1;
    }

    /// <summary>
    /// Clamps an integer value to the inclusive range.
    /// </summary>
    internal static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);

    /// <summary>
    /// Clamps a float value to the inclusive range and rounds to one decimal.
    /// </summary>
    internal static float ClampAndRound(float value, float min, float max)
        => (float)Math.Round(Math.Clamp(value, min, max), 1);
}
