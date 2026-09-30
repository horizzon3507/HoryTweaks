using System.Text;

namespace BetterAmongUs.Diagnostics;

/// <summary>
/// Assembles a plain-text diagnostic report made of titled sections with key/value entries and free lines.
/// The rendered text is passed through <see cref="DiagnosticRedactor"/> so nothing identifying leaks through values.
/// </summary>
internal sealed class DiagnosticReportBuilder(string title)
{
    private readonly List<(string Name, List<string> Lines)> _sections = [];
    private readonly List<string> _sensitiveValues = [];

    internal DiagnosticReportBuilder Section(string name)
    {
        _sections.Add((name, []));
        return this;
    }

    internal DiagnosticReportBuilder Entry(string key, object? value)
    {
        var text = value switch
        {
            null => "-",
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? "-"
        };

        CurrentSection().Add($"{key}: {text}");
        return this;
    }

    internal DiagnosticReportBuilder Line(string text)
    {
        CurrentSection().Add(text);
        return this;
    }

    internal DiagnosticReportBuilder Lines(IEnumerable<string> lines)
    {
        CurrentSection().AddRange(lines);
        return this;
    }

    /// <summary>
    /// Registers a literal value (such as the local account name) that must never appear in the output.
    /// </summary>
    internal DiagnosticReportBuilder Sensitive(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _sensitiveValues.Add(value);
        return this;
    }

    internal string Build()
    {
        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine(new string('=', title.Length));

        foreach (var (name, lines) in _sections)
        {
            builder.AppendLine();
            builder.Append('[').Append(name).AppendLine("]");
            foreach (var line in lines)
                builder.AppendLine(line);
        }

        return DiagnosticRedactor.Redact(builder.ToString(), _sensitiveValues);
    }

    private List<string> CurrentSection()
    {
        if (_sections.Count == 0)
            _sections.Add(("General", []));
        return _sections[^1].Lines;
    }
}
