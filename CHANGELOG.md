# Changelog

HoryTweaks follows [Semantic Versioning](https://semver.org/) and [Keep a Changelog](https://keepachangelog.com/).

<details>
<summary>Versioning details</summary>

The plugin and Git tag use a numeric version such as `0.1.0` and `v0.1.0`. Changelog headings append the Option release channel: `alpha`, `beta` or `stable`.

HoryTweaks is a single release surface, so it does not use the mixed-surface `m` marker.

</details>

## Unreleased

### Added

- A startup check that warns when a legacy `BetterAmongUs.dll` is still installed or loaded beside HoryTweaks, without touching the file.
- A privacy-conscious diagnostic report written by `/dump` with the HoryTweaks version, game version, platform, compatibility checks, non-secret client options and recent redacted log lines.
- Unit tests for the version comparison, legacy plugin detection and report redaction.

### Changed

- Localized the unsupported Among Us version warning and combined it with the legacy plugin warning in a single popup.

## v0.1.1-alpha · 29/09/2026

### Added

- HoryTweaks artwork in the splash screen, main menu and repository.
- A client option to hide the HoryTweaks console after restarting.
- Titles for each page of the HoryTweaks options.

### Changed

- Replaced the console banner and green branding accents with HoryTweaks branding.
- Moved the upper colorblind label closer to the player and kept the player-color formatting used below.
- Limited chat for living players to lobbies, meetings and exile sequences.
- Grouped the client options into Interface, Gameplay and Performance pages.
- Replaced the mod stamp artwork with the HoryTweaks icon.

### Fixed

- Prevented the custom logger from recursively forwarding its own BepInEx output.

## v0.1.0-alpha · 29/09/2026

The first HoryTweaks foundation separates the fork's identity and release channel while retaining compatibility with BetterAmongUs data and runtime packages. This version was prepared as an early HoryTweaks build on 29/09/2026 (`v0.1.0-alpha`).

### Added

- Complete Brazilian Portuguese catalog and localized chat command help, errors and results.
- Translation key and placeholder validation in CI.
- Store-specific installation packages and standalone DLL release artifacts.

### Changed

- Fork identity, plugin metadata, update feeds and download links now point to HoryTweaks.
- Build output is named `HoryTweaks.dll`.
- Automatic cheat detection, its database and detection settings were removed while RPC routing, host gameplay validation, rate limiting, manual moderation and host commands remain.

### Fixed

- Existing presets are retained when the settings format changes.
- Legacy settings and BepInEx configuration are backed up and migrated without deleting the original data.
