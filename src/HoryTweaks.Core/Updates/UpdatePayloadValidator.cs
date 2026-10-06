using Semver;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace HoryTweaks.Core.Updates;

/// <summary>
/// Validates update feed links and downloaded assemblies before anything on disk is touched.
/// </summary>
internal static class UpdatePayloadValidator
{
    /// <summary>
    /// Parses a download link from the update feed, accepting absolute HTTPS URLs only.
    /// </summary>
    internal static bool TryGetDownloadUri(string? link, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        if (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    /// <summary>
    /// Checks that <paramref name="payload"/> is a managed assembly named
    /// <paramref name="expectedAssemblyName"/> whose assembly version matches the version advertised by the
    /// update feed and is not older than the installed one. Only major.minor.patch is compared, because
    /// pre-release tags are not part of the assembly version.
    /// </summary>
    internal static UpdateOutcome Validate(byte[]? payload, string expectedAssemblyName, SemVersion installedVersion, SemVersion advertisedVersion)
    {
        if (payload == null || payload.Length == 0)
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, "The downloaded file is empty.");
        }

        string assemblyName;
        Version assemblyVersion;
        try
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new PEReader(stream);
            if (!reader.HasMetadata)
            {
                return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, "The downloaded file is not a managed assembly.");
            }

            var metadata = reader.GetMetadataReader();
            var definition = metadata.GetAssemblyDefinition();
            assemblyName = metadata.GetString(definition.Name);
            assemblyVersion = definition.Version;
        }
        catch (Exception ex) when (ex is BadImageFormatException || ex is InvalidOperationException || ex is IOException)
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The downloaded file could not be read as an assembly: {ex.Message}");
        }

        if (!string.Equals(assemblyName, expectedAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The downloaded assembly is '{assemblyName}', expected '{expectedAssemblyName}'.");
        }

        var payloadVersion = new SemVersion(assemblyVersion.Major, assemblyVersion.Minor, Math.Max(assemblyVersion.Build, 0));
        var advertisedCore = advertisedVersion.WithoutPrereleaseOrMetadata();
        if (payloadVersion.ComparePrecedenceTo(advertisedCore) != 0)
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The downloaded assembly version {payloadVersion} does not match the advertised update version {advertisedVersion}.");
        }

        var installedCore = installedVersion.WithoutPrereleaseOrMetadata();
        if (payloadVersion.ComparePrecedenceTo(installedCore) < 0)
        {
            return UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The downloaded assembly version {payloadVersion} is older than the installed version {installedVersion}.");
        }

        return UpdateOutcome.Success($"Payload is {assemblyName} {payloadVersion}.");
    }
}
