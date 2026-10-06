namespace HoryTweaks.Core.Commands;

/// <summary>
/// A command as seen by <see cref="CommandMatcher"/>: its primary name, all matchable
/// names (primary name plus aliases), and whether it is currently enabled.
/// </summary>
internal readonly record struct CommandDescriptor(string Name, string[] Names, bool IsEnabled);

/// <summary>
/// Pure command-token matching: an exact name/alias match wins over prefix matching,
/// which picks the enabled command with the alphabetically first primary name.
/// </summary>
internal static class CommandMatcher
{
    /// <summary>
    /// Finds the closest enabled command for the typed token.
    /// </summary>
    /// <param name="token">The command token typed by the user (without the prefix).</param>
    /// <param name="commands">The registered commands, in registration order.</param>
    /// <returns>The index of the matching command in <paramref name="commands"/>, or -1 when none matches.</returns>
    internal static int Match(string token, IReadOnlyList<CommandDescriptor> commands)
    {
        // Exact match on primary name or any alias wins, in registration order.
        for (int i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            if (!command.IsEnabled) continue;
            for (int j = 0; j < command.Names.Length; j++)
            {
                if (string.Equals(command.Names[j], token, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        // Prefix match picks the alphabetically first primary name.
        int best = -1;
        for (int i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            if (!command.IsEnabled) continue;

            bool startsWith = false;
            for (int j = 0; j < command.Names.Length; j++)
            {
                if (command.Names[j].StartsWith(token, StringComparison.OrdinalIgnoreCase))
                {
                    startsWith = true;
                    break;
                }
            }
            if (!startsWith) continue;

            if (best < 0 || string.Compare(commands[i].Name, commands[best].Name, StringComparison.Ordinal) < 0)
                best = i;
        }

        return best;
    }
}
