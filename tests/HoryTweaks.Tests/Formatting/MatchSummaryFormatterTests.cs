using HoryTweaks.Core.Formatting;
using Xunit;

namespace HoryTweaks.Tests.Formatting;

public class MatchSummaryFormatterTests
{
    [Fact]
    public void Formats_discord_markdown_and_strips_rich_text_tags()
    {
        var players = new[]
        {
            new MatchSummaryPlayer("<color=#ff0000>Red\nBoss</color>", "Impostor", true, 3, 0, 0, "Alive"),
            new MatchSummaryPlayer("<b>Blue</b>", "Crewmate", false, 0, 5, 6, "Dead")
        };

        var labels = new MatchSummaryLabels("Impostors", "Crewmates", "Kills", "Tasks");
        string result = MatchSummaryFormatter.Format("Skeld", "Impostors", "Crew Outnumbered", 342_000, players, labels);

        Assert.Contains("**Among Us — Skeld** (05:42)", result);
        Assert.Contains("**Impostors** — Crew Outnumbered", result);
        Assert.Contains("- Red Boss (Impostor) · Kills: 3 · Alive", result);
        Assert.Contains("- Blue (Crewmate) · Tasks: 5/6 · Dead", result);
        Assert.DoesNotContain("<color", result);
        Assert.DoesNotContain("<b>", result);
    }
}
