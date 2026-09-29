# Changelog

HoryTweaks follows [Semantic Versioning](https://semver.org/) and [Keep a Changelog](https://keepachangelog.com/).

<details>
<summary>Versioning details</summary>

The plugin and Git tag use a numeric version such as `0.1.0` and `v0.1.0`. Changelog headings append the Option release channel: `alpha`, `beta` or `stable`.

HoryTweaks is a single release surface, so it does not use the mixed-surface `m` marker.

</details>

## v0.1.0-alpha · 29/09/2026

The first HoryTweaks foundation separates the fork's identity and release channel while retaining compatibility with BetterAmongUs data and runtime packages. This version was prepared as an early HoryTweaks build on 29/09/2026 (`v0.1.0-alpha`).

### Added

- Complete Brazilian Portuguese catalog and localized chat command help, errors and results.
- Translation key and placeholder validation in CI.
- Store-specific installation packages and standalone DLL release artifacts.

### Changed

- Fork identity, plugin metadata, update feeds and download links now point to HoryTweaks.
- Build output is named `HoryTweaks.dll`.

### Fixed

- Existing presets are retained when the settings format changes.
- Legacy settings and BepInEx configuration are backed up and migrated without deleting the original data.
