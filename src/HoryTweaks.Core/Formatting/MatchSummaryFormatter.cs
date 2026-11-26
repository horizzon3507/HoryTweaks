using System.Text;

namespace HoryTweaks.Core.Formatting;

/// <summary>
/// One player's summary line for Discord-ready plain Markdown export.
/// </summary>
internal sealed record MatchSummaryPlayer(
    string Name,
    string RoleName,
    bool IsImpostor,
    int Kills,
    int TasksDone,
    int TasksTotal,
    string Status);

/// <summary>
/// Localized labels passed into <see cref="MatchSummaryFormatter.Format"/> so formatting stays Unity-free.
/// </summary>
internal sealed record MatchSummaryLabels(
    string ImpostorsHeading,
    string CrewmatesHeading,
    string KillsLabel,
    string TasksLabel);

/// <summary>
/// Formats finished match data into clean, tag-free Markdown suitable for pasting into Discord.
/// </summary>
internal static class MatchSummaryFormatter
{
    internal static string Format(
        string mapName,
        string winningTeam,
        string winReason,
        int? durationMs,
        IReadOnlyList<MatchSummaryPlayer> players,
        MatchSummaryLabels labels)
    {
        var sb = new StringBuilder();

        string cleanMap = Sanitize(mapName);
        string cleanTeam = Sanitize(winningTeam);
        string cleanReason = Sanitize(winReason);

        sb.Append("**Among Us — ").Append(cleanMap).Append("**");
        if (durationMs.HasValue && durationMs.Value > 0)
        {
            sb.Append(" (").Append(FormatDuration(durationMs.Value)).Append(')');
        }
        sb.AppendLine();

        sb.Append("**").Append(cleanTeam).Append("**");
        if (cleanReason.Length > 0)
        {
            sb.Append(" — ").Append(cleanReason);
        }
        sb.AppendLine();

        var impostors = players.Where(p => p.IsImpostor).ToList();
        var crewmates = players.Where(p => !p.IsImpostor).ToList();

        if (impostors.Count > 0)
        {
            sb.AppendLine();
            sb.Append("**").Append(Sanitize(labels.ImpostorsHeading)).AppendLine("**");
            foreach (var player in impostors)
            {
                sb.Append("- ")
                  .Append(Sanitize(player.Name))
                  .Append(" (").Append(Sanitize(player.RoleName)).Append(") · ")
                  .Append(Sanitize(labels.KillsLabel)).Append(": ").Append(player.Kills)
                  .Append(" · ").Append(Sanitize(player.Status))
                  .AppendLine();
            }
        }

        if (crewmates.Count > 0)
        {
            sb.AppendLine();
            sb.Append("**").Append(Sanitize(labels.CrewmatesHeading)).AppendLine("**");
            foreach (var player in crewmates)
            {
                sb.Append("- ")
                  .Append(Sanitize(player.Name))
                  .Append(" (").Append(Sanitize(player.RoleName)).Append(") · ")
                  .Append(Sanitize(labels.TasksLabel)).Append(": ").Append(player.TasksDone).Append('/').Append(player.TasksTotal)
                  .Append(" · ").Append(Sanitize(player.Status))
                  .AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    internal static string FormatDuration(int ms)
    {
        int seconds = Math.Max(ms, 0) / 1000;
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string stripped = ChatFormat.StripRichText(text);
        return stripped.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
