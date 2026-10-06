using System.IO.Compression;

namespace BetterAmongUs.Core.Updates;

/// <summary>
/// Validates a downloaded store release package before anything on disk is touched:
/// it must be a readable zip whose entries all stay inside the archive root and which
/// carries the payload the release pipeline guarantees (<c>winhttp.dll</c> at the root
/// and <c>BepInEx/plugins/HoryTweaks.dll</c>).
/// </summary>
internal static class UpdatePackageValidator
{
    /// <summary>
    /// Archive entry the mod payload is expected at inside a store release package.
    /// </summary>
    internal const string ModPayloadEntry = "BepInEx/plugins/HoryTweaks.dll";

    /// <summary>
    /// Root loader the release pipeline guarantees inside every store package.
    /// </summary>
    internal const string BootstrapEntry = "winhttp.dll";

    /// <summary>
    /// Checks that <paramref name="package"/> is a well-formed release zip.
    /// </summary>
    internal static UpdateOutcome Validate(byte[]? package, string fileName)
    {
        if (package == null || package.Length == 0)
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The downloaded package '{fileName}' is empty.");
        }

        bool hasModPayload = false;
        bool hasBootstrap = false;
        int entryCount = 0;
        try
        {
            using var stream = new MemoryStream(package, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                entryCount++;
                if (!IsSafeEntryName(entry.FullName))
                {
                    return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The package '{fileName}' contains the unsafe path '{entry.FullName}'.");
                }

                var normalized = NormalizeEntryName(entry.FullName);
                if (string.Equals(normalized, ModPayloadEntry, StringComparison.OrdinalIgnoreCase))
                {
                    hasModPayload = true;
                }
                else if (string.Equals(normalized, BootstrapEntry, StringComparison.OrdinalIgnoreCase))
                {
                    hasBootstrap = true;
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is NotSupportedException)
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The package '{fileName}' could not be read as a zip archive: {ex.Message}");
        }

        if (entryCount == 0)
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The package '{fileName}' is an empty archive.");
        }

        if (!hasBootstrap)
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The package '{fileName}' has no '{BootstrapEntry}' at its root and is not a full release package.");
        }

        if (!hasModPayload)
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The package '{fileName}' does not contain '{ModPayloadEntry}'.");
        }

        return UpdateOutcome.Success($"Package '{fileName}' has {entryCount} entries including '{ModPayloadEntry}'.");
    }

    /// <summary>
    /// Reports whether an archive entry name stays inside the archive root when extracted:
    /// no empty names, rooted paths, drive-qualified paths or <c>..</c> segments.
    /// </summary>
    internal static bool IsSafeEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Contains(':'))
        {
            return false;
        }

        foreach (var segment in normalized.Split('/'))
        {
            if (segment == "..")
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Normalizes an entry name for comparison: forward slashes, no leading slash, no
    /// trailing directory separator.
    /// </summary>
    internal static string NormalizeEntryName(string entryName)
    {
        return entryName.Replace('\\', '/').Trim('/').TrimEnd('/');
    }
}
