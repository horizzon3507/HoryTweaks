namespace BetterAmongUs.Modules.Updater;

/// <summary>
/// Explicit result of an update step or of the whole update attempt.
/// </summary>
internal sealed class UpdateOutcome
{
    private UpdateOutcome(UpdateStatus status, string detail)
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
