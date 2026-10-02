namespace BetterAmongUs.Modules.Updater;

/// <summary>
/// Explicit result of an update step or of the whole update attempt.
/// </summary>
internal sealed class UpdateOutcome
{
    internal UpdateOutcome(UpdateStatus status, string detail)
    {
        Status = status;
        Detail = detail;
    }

    /// <summary>
    /// Gets the terminal status.
    /// </summary>
    internal UpdateStatus Status { get; }

    /// <summary>
    /// Gets a log-oriented description of what happened. Never shown to players directly.
    /// </summary>
    internal string Detail { get; }

    /// <summary>
    /// Gets whether the update was fully installed.
    /// </summary>
    internal bool IsSuccess => Status == UpdateStatus.Succeeded;

    /// <summary>
    /// Gets whether the update payload was obtained for the install to proceed: applied
    /// now, staged for apply on exit, or saved for a manual install.
    /// </summary>
    internal bool Obtained => Status is UpdateStatus.Succeeded or UpdateStatus.Staged or UpdateStatus.SavedToDisk;

    /// <summary>
    /// Creates a successful outcome.
    /// </summary>
    internal static UpdateOutcome Success(string detail)
    {
        return new UpdateOutcome(UpdateStatus.Succeeded, detail);
    }

    /// <summary>
    /// Creates a failed outcome.
    /// </summary>
    internal static UpdateOutcome Failure(UpdateStatus status, string detail)
    {
        if (status == UpdateStatus.Succeeded)
        {
            throw new ArgumentException("A failure cannot use the Succeeded status.", nameof(status));
        }

        return new UpdateOutcome(status, detail);
    }

    public override string ToString()
    {
        return $"{Status}: {Detail}";
    }
}
