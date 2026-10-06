using Semver;
using System.Reflection;

namespace HoryTweaks;

internal partial class BAUPlugin
{
    /// <summary>
    /// Contains metadata and constants for the BetterAmongUs mod.
    /// </summary>
    internal static class ModInfo
    {
        /// <summary>
        /// Gets the full version string for the current build configuration.
        /// The value is generated from the csproj Version property by BuildInfoGenerator.
        /// </summary>
        internal const string VERSION = BuildInfo.Version;

        /// <summary>
        /// Gets the full version string with a v prefix for the current build configuration.
        /// </summary>
        internal const string VERSION_STRING = "v" + VERSION;

        /// <summary>
        /// Gets the parsed semantic version of the mod.
        /// </summary>
        internal static readonly SemVersion SemVersion = SemVersion.Parse(VERSION);

        /// <summary>
        /// Gets the Git commit hash from assembly metadata.
        /// </summary>
        public static string CommitHash = ThisAssembly.Git.Commit;

        /// <summary>
        /// Gets the build date from assembly metadata.
        /// </summary>
        public static string BuildDate = ThisAssembly.Metadata.BuildDate;

        /// <summary>
        /// The name of BAU.
        /// </summary>
        internal const string PLUGIN_NAME = "HoryTweaks";

        /// <summary>
        /// The GUID (Globally Unique Identifier) of BAU.
        /// </summary>
        internal const string PLUGIN_GUID = "com.horizzon3507.horytweaks";

        /// <summary>
        /// Gets the list of supported Among Us versions.
        /// </summary>
        internal static string[] SupportedAmongUsVersions =
        [
            "2026.9.29"
        ];

        /// <summary>
        /// The GitHub repository URL for BAU.
        /// </summary>
        internal const string GITHUB = "https://github.com/horizzon3507/HoryTweaks";

        /// <summary>
        /// The Discord invite URL for the HoryTweaks community.
        /// </summary>
        internal const string DISCORD = "https://discord.gg/dzuhVMfVXU";

        /// <summary>
        /// Indicator rather that BAU is running on Starlight for Android.
        /// </summary>
        internal static readonly bool Starlight = OperatingSystem.IsAndroid();

        /// <summary>
        /// The assembly associated to this mod.
        /// </summary>
        internal static Assembly Assembly
        {
            get
            {
                if (field == null)
                {
                    field = Assembly.GetExecutingAssembly();
                }
                return field;
            }
        }

        /// <summary>
        /// Contains constants for Among Us.
        /// </summary>
        internal static class AmongUs
        {
            /// <summary>
            /// The process name of the Among Us executable.
            /// </summary>
            internal const string PROCESS_NAME = "Among Us.exe";
        }
    }
}