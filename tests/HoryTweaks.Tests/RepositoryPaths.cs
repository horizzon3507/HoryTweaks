namespace HoryTweaks.Tests;

internal static class RepositoryPaths
{
    internal static string Root { get; } = FindRoot();

    internal static string Src => Path.Combine(Root, "src");

    internal static string Api => Path.Combine(Root, "api");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "BetterAmongUs.csproj")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate the repository root from {AppContext.BaseDirectory}.");
    }
}
