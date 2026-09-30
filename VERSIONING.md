# Versioning

HoryTweaks uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html) with an explicit release channel and [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). This follows the Option family convention.

## Surface

HoryTweaks has one release surface: the client-side mod distributed as `HoryTweaks.dll` and the store-specific installation packages.

The plugin assembly, update feeds and Git tags keep the numeric version (`0.1.0`, `v0.1.0`). Changelog headings add the release channel, for example `v0.1.0-alpha`.

## Release channels

| Channel | Changelog example | Meaning |
| --- | --- | --- |
| **alpha** | `v0.1.0-alpha` | Early builds that still need in-game validation. Features may be incomplete and bugs are expected. |
| **beta** | `v0.1.0-beta` | Feature-complete builds that still need broader testing. |
| **stable** | `v0.1.0-stable` | Tested builds ready for normal installation and automatic updates. |

Do not label a version `stable` until both installation packages, settings migration and automatic updates have been tested in Among Us.

Alpha and beta cuts are development artifacts by default. Publish the numeric tag and enable the update feed when promoting the matching version to stable.

## Release checklist

1. Update `VERSION_NUMBER` in `src/BAUPlugin.ModInfo.cs`, `Version` in `src/BetterAmongUs.csproj`, both update feeds and the README compatibility version.
2. Add the channel-labelled entry to `CHANGELOG.md`.
3. Validate translations and build `HoryTweaks.dll`.
4. Test a clean install and an upgrade from BetterAmongUs with both store packages.
5. For a stable release, create and push the annotated numeric tag, such as `v0.1.0`.
