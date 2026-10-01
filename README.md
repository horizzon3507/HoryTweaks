# HoryTweaks

<p align="center">
  <img width="700" alt="HoryTweaks logo" src="/assets/HoryTweaks-Logo.png" />
</p>

HoryTweaks is a client-side Among Us mod forked from D1GQ's BetterAmongUs. It adds client-side improvements, host gameplay validation and manual moderation tools while remaining compatible with unmodified players.

## Compatibility

HoryTweaks 0.1.2 targets **Among Us v19.0.0 / 2026.9.29**. It is not intended for older or newer game versions unless a later release says otherwise.

On startup HoryTweaks compares the running game version with the supported version and looks for a leftover `BepInEx/plugins/BetterAmongUs.dll`. If either check finds a problem, the sign-in screen shows a warning that explains what to do. Nothing is changed or deleted for you.

Release downloads: [HoryTweaks releases](https://github.com/horizzon3507/HoryTweaks/releases)

## Installation

### Guided installers

Download `HoryTweaks-Installers.zip` from a release that includes it and extract all files. On Windows, run `install-horytweaks.bat` with `Install-HoryTweaks.ps1` beside it. On Linux/Steam Deck, run `bash install-horytweaks.sh` (Python 3, curl and pgrep required).

The installers find Steam libraries, offer manual paths and store selection, support pinned versions and offline ZIPs, and back up replaced files with rollback on write failures. Windows also detects Epic installs. Settings and other plugins are preserved. See the [installer guide](installers/README.md) for dry runs, checksum verification, command-line options and recovery.

### Manual installation

1. Download the package for your game store from the [latest HoryTweaks release](https://github.com/horizzon3507/HoryTweaks/releases/latest).
   - Steam, Epic Games and Microsoft Store: `HoryTweaks-Steam-Epic-MsStore-<tag>.zip`
   - itch.io: `HoryTweaks-Itchio-<tag>.zip`
2. Close Among Us and extract the package into the game's installation folder, preserving its folders. Allow files to merge or overwrite when prompted.
3. If you are upgrading from BetterAmongUs, remove the old `BepInEx/plugins/BetterAmongUs.dll` so the fork does not load alongside it. Keep the `Better_Data` folder; HoryTweaks uses it to preserve existing settings and local data.
4. Start the game. The package includes the compatible BepInEx files based on the upstream BetterAmongUs v1.3.4 Steam/Epic/Microsoft Store or itch.io package.

For later updates, download `HoryTweaks.dll` from the release and replace `BepInEx/plugins/HoryTweaks.dll`. Use the matching full package for a fresh installation or if the bundled BepInEx files need to be restored.

### Finding the game folder

- **Steam:** Library → Among Us → Manage → Browse local files.
- **Epic Games:** Library → Among Us → menu → Manage → installation location.
- **Microsoft Store:** use the installed game's folder and copy files with the permissions Windows allows. If the folder is protected, use the store's supported file access rather than changing its ownership.
- **itch.io:** open the game's install folder from the itch app.

### Linux on Steam

Run the Windows game under Proton 9.0 or newer. Enable Proton in Steam's Compatibility settings and install the package into the Among Us directory. If BepInEx cannot start, try these launch options:

```text
WINEDLLOVERRIDES="winhttp=n,b" PROTON_NO_ESYNC=1 %command%
```

Proton and game updates can affect mod compatibility. HoryTweaks does not provide a separate native Linux build.

## Features

- Host-side gameplay validation for selected actions and invalid sabotage attempts.
- Manual host moderation, including ban lists, kick cooldown, minimum-level checks and banned-chat patterns.
- An in-game moderation center in the Tweaks options tab for kicking and banning the current players, opening the ban lists and reviewing this session's kicks and bans.
- Client-side improvements such as lobby information, customizable chat and minimap options.
- Chat commands for player information, log export and supported host actions.
- Presets and moderation lists retained under the compatible `Better_Data` user-data folder.

## Commands

Use `/help` in chat for an overview and `/commands` for the available command list. The command prefix can be changed in settings or with `/setprefix`.

`/dump` saves the full BepInEx log and a diagnostic report to `HoryTweaksLogDumps` on the desktop (or inside `Better_Data` on Starlight). The report lists the HoryTweaks version, game version, platform, compatibility checks, non-secret client options and the most recent log lines. Private log entries are left out and lobby codes, friend codes, addresses and user names are redacted, so the report can be attached to a bug report.

## Platform support

Install packages are produced for Steam, Epic Games, Microsoft Store and itch.io. Linux support is through Steam Proton. This release workflow does not produce Android packages; iOS and console installations are not supported.

## Credits and license

HoryTweaks is based on [D1GQ's BetterAmongUs](https://github.com/D1GQ/BetterAmongUs), including its code, artwork and GPL-licensed components. Thank you to D1GQ and the BetterAmongUs contributors, including [Nyx](https://github.com/DeveloperNyx) and [At0mBomba](https://github.com/At0mBomba). See [LICENSE](LICENSE) for the preserved GPL license and its terms.

HoryTweaks follows the Option versioning convention documented in [VERSIONING.md](VERSIONING.md). Release history is in [CHANGELOG.md](CHANGELOG.md).

## Disclaimer

HoryTweaks is an unofficial, fan-made mod for Among Us. It is not affiliated with, endorsed by or associated with Innersloth LLC or the official Among Us game. All Among Us trademarks and copyrights belong to Innersloth LLC. Use the mod at your own risk.
