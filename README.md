<p align="center">
  <img width="700" alt="HoryTweaks logo" src="/assets/HoryTweaks-Logo.png" />
</p>

<h1 align="center">HoryTweaks</h1>

<p align="center">
  A client-side <b>Among Us</b> mod — forked from
  <a href="https://github.com/D1GQ/BetterAmongUs">D1GQ's BetterAmongUs</a> —
  with host gameplay validation, manual moderation tools and client improvements
  that stay compatible with unmodified players.
</p>

<p align="center">
  <a href="https://github.com/horizzon3507/HoryTweaks/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/horizzon3507/HoryTweaks?include_prereleases&display_name=tag" /></a>
  <a href="https://github.com/horizzon3507/HoryTweaks/actions/workflows/build.yml"><img alt="Build" src="https://github.com/horizzon3507/HoryTweaks/actions/workflows/build.yml/badge.svg" /></a>
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-GPL-blue" /></a>
  <a href="https://discord.gg/dzuhVMfVXU"><img alt="Discord community" src="https://img.shields.io/badge/discord-join%20the%20community-5865F2?logo=discord&logoColor=white" /></a>
</p>

<p align="center">
  <a href="#installation">Installation</a> •
  <a href="#features">Features</a> •
  <a href="#commands">Commands</a> •
  <a href="#community">Community</a> •
  <a href="#faq">FAQ</a> •
  <a href="#credits-and-license">License</a>
</p>

> **Join the community:** [discord.gg/dzuhVMfVXU](https://discord.gg/dzuhVMfVXU) — support, announcements, presets and feedback for the mod live there.

## Compatibility

The latest release targets **Among Us v19.0.0 / 2026.9.29** only — older or newer game versions are not supported unless a release says otherwise.

On startup HoryTweaks compares the running game version with the supported version and looks for a leftover `BepInEx/plugins/BetterAmongUs.dll`. If either check finds a problem, the sign-in screen shows a warning that explains what to do. Nothing is changed or deleted for you.

Store packages are produced for Steam, Epic Games, Microsoft Store and itch.io. Linux works through Steam Proton; Android, iOS and consoles are not supported.

## Installation

### Guided installers (recommended)

Download `HoryTweaks-Installers.zip` from a release that includes it and extract all files.

- **Windows:** run `install-horytweaks.bat` with `Install-HoryTweaks.ps1` beside it. Detects Steam and Epic installs.
- **Linux / Steam Deck:** run `bash install-horytweaks.sh` (Python 3, curl and pgrep required). Detects Steam libraries, including Flatpak Steam.

The installers offer manual paths and store selection, support pinned versions and offline ZIPs, verify checksums and back up replaced files with rollback on write failures. Settings and other plugins are preserved. See the [installer guide](installers/README.md) for dry runs, command-line options and recovery.

### Manual installation

1. Download the package for your game store from the [latest HoryTweaks release](https://github.com/horizzon3507/HoryTweaks/releases/latest).
   - Steam, Epic Games and Microsoft Store: `HoryTweaks-Steam-Epic-MsStore-<tag>.zip`
   - itch.io: `HoryTweaks-Itchio-<tag>.zip`
2. Close Among Us and extract the package into the game's installation folder, preserving its folders. Allow files to merge or overwrite when prompted.
3. If you are upgrading from BetterAmongUs, remove the old `BepInEx/plugins/BetterAmongUs.dll` so the fork does not load alongside it. **Keep the `Better_Data` folder** — HoryTweaks uses it to preserve your existing settings and local data.
4. Start the game. The package already includes the compatible BepInEx files.

For later updates, download `HoryTweaks.dll` from the release and replace `BepInEx/plugins/HoryTweaks.dll`. Use the matching full package for a fresh installation or if the bundled BepInEx files need to be restored.

<details>
<summary>Finding the game folder</summary>

- **Steam:** Library → Among Us → Manage → Browse local files.
- **Epic Games:** Library → Among Us → menu → Manage → installation location.
- **Microsoft Store:** use the installed game's folder and copy files with the permissions Windows allows. If the folder is protected, use the store's supported file access rather than changing its ownership.
- **itch.io:** open the game's install folder from the itch app.

</details>

<details>
<summary>Linux on Steam (Proton)</summary>

Run the Windows game under Proton 9.0 or newer. Enable Proton in Steam's Compatibility settings and install the package into the Among Us directory. If BepInEx cannot start, try these launch options:

```text
WINEDLLOVERRIDES="winhttp=n,b" PROTON_NO_ESYNC=1 %command%
```

Proton and game updates can affect mod compatibility. HoryTweaks does not provide a separate native Linux build.

</details>

## Features

<table>
<tr>
<td width="50%" valign="top">

### Host tools

- **Gameplay validation** — host-side checks for selected actions and invalid sabotage attempts.
- **Moderation center** — in-game panel with the current player list, confirmed kick/ban actions, quick access to ban lists and this session's kicks and bans.
- **Moderation rules** — ban lists, kick cooldown, minimum-level checks and banned-chat patterns.
- **Host commands** — chat commands for supported host actions such as `/kick`, `/endgame`, `/forceskip` and `/transferhost`.

</td>
<td width="50%" valign="top">

### Client improvements

- **Interface** — lobby information, customizable chat and minimap options, colorblind-friendly labels and scroll-wheel zoom.
- **Chat commands** — player info (`/players`, `/player`), log export (`/dump`) and an adjustable prefix.
- **Presets** — named settings presets you can restore, copy, paste and share.
- **Localization** — [16 languages](src/HoryTweaks/Resources/Lang) including full Brazilian Portuguese.

</td>
</tr>
</table>

All settings and moderation lists are kept under the compatible `Better_Data` user-data folder, so migrating from BetterAmongUs keeps your configuration.

<details>
<summary>Screenshots</summary>

<p align="center">
  <img width="700" alt="Meeting screen with HoryTweaks" src="/assets/meeting-promo.png" />
</p>
<p align="center">
  <img width="700" alt="Freeplay with HoryTweaks" src="/assets/freeplay-promo.png" />
</p>

</details>

## Commands

Use `/help` in chat for an overview and `/commands` for the available command list. The command prefix can be changed in settings or with `/setprefix`.

`/dump` saves the full BepInEx log and a diagnostic report to `HoryTweaksLogDumps` on the desktop (or inside `Better_Data` on Starlight). The report lists the HoryTweaks version, game version, platform, compatibility checks, non-secret client options and the most recent log lines. Private log entries are left out and lobby codes, friend codes, addresses and user names are redacted, so the report can be attached to a bug report.

## Community

The HoryTweaks community lives on Discord:

<h3 align="center"><a href="https://discord.gg/dzuhVMfVXU">💬 discord.gg/dzuhVMfVXU</a></h3>

- **Support** — install help and troubleshooting.
- **Announcements** — new releases, game-version compatibility notices.
- **Feedback** — feature requests, presets and bug reports.

For code issues and pull requests, use the [GitHub repository](https://github.com/horizzon3507/HoryTweaks).

## FAQ

**Do other players need the mod?**
No. HoryTweaks is client-side and stays compatible with unmodified players. Host tools only apply when you are the host.

**Where does it work?**
HoryTweaks is designed for the official servers and public lobbies. Features that depend on being host only take effect in lobbies you host.

**I came from BetterAmongUs — do I lose my settings?**
No. Keep the `Better_Data` folder and your presets, moderation lists and options carry over. Remove only the old `BepInEx/plugins/BetterAmongUs.dll`.

**Something broke — where do I get help?**
Run `/dump` in chat, then post the generated report in the [Discord community](https://discord.gg/dzuhVMfVXU) or attach it to a [GitHub issue](https://github.com/horizzon3507/HoryTweaks/issues).

## Building from source

Requires the .NET SDK from `global.json`. Building `HoryTweaks.csproj` produces `HoryTweaks.dll`; set `CopyToGame=false` or create `build/mod_folder_path` with your plugins folder to control where the DLL is copied. Quality-gate tests live under `tests/` and run in CI before packaging.

## Credits and license

HoryTweaks is based on [D1GQ's BetterAmongUs](https://github.com/D1GQ/BetterAmongUs), including its code, artwork and GPL-licensed components. Thank you to D1GQ and the BetterAmongUs contributors, including [Nyx](https://github.com/DeveloperNyx) and [At0mBomba](https://github.com/At0mBomba). See [LICENSE](LICENSE) for the preserved GPL license and its terms.

HoryTweaks follows the Option versioning convention documented in [VERSIONING.md](VERSIONING.md). Release history is in [CHANGELOG.md](CHANGELOG.md).

## Disclaimer

HoryTweaks is an unofficial, fan-made mod for Among Us. It is not affiliated with, endorsed by or associated with Innersloth LLC or the official Among Us game. All Among Us trademarks and copyrights belong to Innersloth LLC. Use the mod at your own risk.
