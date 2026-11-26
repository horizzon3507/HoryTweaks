# Changelog

HoryTweaks follows [Semantic Versioning](https://semver.org/) and [Keep a Changelog](https://keepachangelog.com/).

<details>
<summary>Versioning details</summary>

The plugin and Git tag use a numeric version such as `0.1.0` and `v0.1.0`. Changelog headings append the Option release channel: `alpha`, `beta` or `stable`.

HoryTweaks is a single release surface, so it does not use the mixed-surface `m` marker.

</details>

## v0.2.0-beta · 25/11/2026

A major stability, networking, and performance refactor that targets premature disconnects, ping spikes, GC pauses, and per-frame lag, while introducing 2D match replays, cross-platform real-POV `.mp4` video recording, local chat mute, unfocused Alt+Tab alerts, and Discord-ready match summaries. This version was prepared as a beta HoryTweaks release on 25/11/2026 (`v0.2.0-beta`).

### Added

- **Network Optimization** client toggle (page 3, enabled by default): tunes client-side Hazel UDP tolerances (`KeepAliveInterval`, `DisconnectTimeoutMs`, finite `ResendLimit` while preserving `0` unlimited retries, `ResendPingMultiplier`) and `SecondsSuspendedBeforeDisconnect`, restoring original baselines on toggle-off or switch-to-vanilla, while staying compatible with official Innersloth servers.
- Live connection health and frame-pacing diagnostics: tracks smoothed ping, jitter, reliable resend spikes, severe main-thread hitches (`>= 100ms`), and sustained low FPS (`< 30 FPS`), surfacing localized warnings on `BetterPingTracker` and English diagnostic summaries (including the last ended session after a disconnect) in logs and `/dump`.
- Smart `UnlockFPS` target policy (120–240 FPS based on monitor refresh rate) with interactive Page 3 advisories, HUD notifications, and logged warnings for Among Us VSync conflicts, >240 Hz safety clamping, and unknown display refresh rates.
- Guided Windows and Linux/Steam Deck installers with game discovery, store and release selection, offline packages, dry runs, backups and rollback on installation errors.
- Installer tests on Windows and Linux, downloadable installer scripts and SHA-256 manifests for future release assets.
- "Controls & Gestures" page (page 4) in the HoryTweaks options tab listing the mod's hidden shortcuts: scroll zoom, option value multipliers, chat autocomplete and chat history.
- "Scroll Zoom" client toggle that enables or disables the scroll-wheel zoom.
- `/transferhost` (`/th`) command, plus an automatic host transfer to the longest-connected player when the host stays AFK in the lobby.
- "Auto Rejoin" client option that rejoins the last online lobby after an involuntary disconnect, with a countdown on the disconnect popup and faster initial retry backoff.
- "Extended chat history" client option and a `/copy` (`/copylog`) command that copies the last 50 chat lines to the clipboard.
- "Hory-User" badge for HoryTweaks players, detected inside the mod handshake; upstream BetterAmongUs players keep the "Better-User" badge.
- Match history: finished matches are saved locally (newest 25, `Better_Data/MatchHistory`) and can be reviewed under Tweaks → Match History, with a timeline of kills, blocked attacks, protects, meetings, ejections, votes, vents, sabotages, disconnects, shapeshifts and task completions, and a 2D replay on the real map.
- Real-POV `.mp4` match video replay ("Record POV Video (.mp4)", page 3): captures downscaled game frames (480p @ 20fps, game view only — no desktop overlays) and encodes lightweight H.264 `.mp4` files off the main thread using Windows Media Foundation on native Windows and Cisco OpenH264 in-process (auto-provisioned & SHA-256 verified) with a built-in MP4 muxer on Linux/Steam Proton and Windows N, with `ffmpeg.exe` fallback.
- Unfocused window alerts ("Alt+Tab Window Alerts", page 1): flashes the taskbar window (`FlashWindowEx`) and plays a subtle chime when the lobby fills, a match starts, or a meeting opens while Among Us is unfocused.
- Local session chat mute commands (`/mute` and `/unmute`) that hide chat bubbles and chat notifications from muted players locally without affecting the lobby.
- Discord-ready match summary export via `/summary` (`/sum`), a "Copy Summary" button on the end-game screen, and a "Copy Summary" button inside Match History.
- Unit test suites covering network tuning, send-error resilience, connection health tracking, smart frame-rate policy, frame-pacing hitch detection, async console logging, match history/video encoding, window alerts, local mute, and Discord summary formatting.

### Changed

- Refactored `NetworkManager.HandleGameData` to dispatch `DataFlag`, `RpcFlag`, `DespawnFlag`, `ReadyFlag`, `SceneChangeFlag`, and `XboxDeclareXuid` synchronously on a zero-coroutine, zero-copy fast path, falling back to deferred buffer-cloned coroutines only for spawns or unspawned `netId`s.
- Absorbed isolated transient `SendErrors.Unknown` failures on unreliable packets instead of immediately disconnecting the client, and skipped outgoing packet buffer inspection on non-host clients.
- Replaced O(N) linear `RPCHandler` dispatch and per-RPC reflection/string allocations with pre-indexed 256-slot lookup tables, precomputed support-flag keys, and a 256-entry `KnownRpcCalls` table.
- Refactored `BetterPingTracker`, `PlayerInfoDisplay`, `MeetingInfoDisplay`, and per-frame patches (`MainMenuManagerPatch`, `ModManagerPatch`, `ZoomPatch`, `HudManagerPatch`, `BetterNotificationManager`, `MiniMapBehaviourPatch`, `LobbyPatch`, `ChatPatch`, `ChatCommandsPatch`, `CheckPlayerLevelPatch`) with time-based throttling, cached references, and dirty-checks to eliminate GC pressure and redundant UI/TMP rebuilds.
- Optimized `IMonoExtension.TryRemoveExtension` to O(1), replaced closure-allocating LINQ lookups in `Utils.PlayerLookup` with indexed loops, dispatched `PlayerControl.SetName` synchronously on the fast path, and cached the `BAU-Mod` header in `UnityWebRequestPatch`.
- `/forceskip` (without `true`) now closes the meeting immediately via `MeetingHud.RpcClose()` instead of waiting for the results countdown; `/forceskip true` continues to tally votes normally.
- Console logging now uses a bounded single-consumer background writer for atomic color/line output without stalling Unity's main thread, and disables Windows Console QuickEdit mode so clicking the console cannot freeze the game.
- The remaining hardcoded English strings are now localized across all 16 languages, including the new v0.2.0 options and in-game HUD/settings warning banners.
- The in-game updater installs the store-specific release package after verifying it against the release's `SHA256SUMS.txt`, and falls back to replacing only `HoryTweaks.dll` for releases without one.
- Code namespaces moved from `BetterAmongUs.*` to `HoryTweaks.*` and embedded resources from `BetterAmongUs.Resources.*` to `HoryTweaks.Resources.*`. Mods that reference HoryTweaks types, including the modded support examples, must update their `using` directives.
- The Discord link points to the HoryTweaks community server instead of the upstream BetterAmongUs one.

### Removed

- Old BetterAmongUs news posts no longer appear in the in-game announcements.

### Fixed

- Fixed pooled `MessageReader` leaks in `HandleSceneChange`, `StreamlineMessage`, `ReadData`, `BetterRpcRouter`, and `InnerNetClientUtils.MessagesToReaders`.
- Fixed repeated `CryptographicException` (0x80090029) storms on Wine/Proton by caching ECDH capability once per process and disposing `ECDiffieHellman` handles in `SharedSecretExchange.ClearData()`.
- Fixed an infinite coroutine leak in `PlayerJoinAndLeftPatch.CoCheckPlayerOnJoin` when a joining player disconnects before entering the scene.
- Fixed `Utils.LerpColor(..., reverse: true)` mutating the caller's color array in place and fixed a `100x100 Texture2D` leak in `FindAGameManagerPatch`.
- Switched `AutoRejoinManager` to `Time.unscaledTime` and `WaitForSecondsRealtime` so focus pauses cannot stall reconnect countdowns.
- Fixed startup config re-application (`BetterMinimapColors`, `BetterColorblindText`, `CompressSettingFiles`), canonical config migration, "?" help icon overlap, early Starlight logger crash, WAV chunk decoding, and `|` parsing inside JSON string values.

## v0.1.2-alpha · 30/09/2026

### Added

- Localized On/Off labels and question-mark descriptions for the Tweaks settings.
- Restore defaults, copy preset and paste preset rows on the Tweaks tab, each guarded by a second confirming click.
- Custom preset names through the `/presetname` command, stored inside the existing `Preset-N.json` files.
- Unit tests for the preset share code and preset name helpers.
- A host moderation center in the HoryTweaks options tab with the current player list, kick and ban actions that ask for confirmation, quick access to the player, name and chat ban lists and the kicks and bans of the current session.
- Automated tests for the game-independent moderation rules, history and list helpers under `tests/HoryTweaks.Tests`.
- A startup check that warns when a legacy `BetterAmongUs.dll` is still installed or loaded beside HoryTweaks, without touching the file.
- A privacy-conscious diagnostic report written by `/dump` with the HoryTweaks version, game version, platform, compatibility checks, non-secret client options and recent redacted log lines.
- Unit tests for the version comparison, legacy plugin detection and report redaction.
- Unity-independent quality gate tests for settings serialization, compression and migration, update manifest version decisions, legacy ban list migration and translation placeholder validation, run in CI before packaging.

### Changed

- Localized the unsupported Among Us version warning and combined it with the legacy plugin warning in a single popup.

### Removed

- The unfinished replay recording system and its unused Better_Data replays folder.

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
