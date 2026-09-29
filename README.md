# HoryTweaks

<p align="center">
  <img width="700" height="500" alt="BetterAmongUs logo retained from upstream" src="/assets/BetterAmongUs-Logo.png" />
</p>

HoryTweaks is a client-side Among Us mod forked from D1GQ's BetterAmongUs. It adds client-side improvements, expanded host settings and local anti-cheat protections while remaining compatible with unmodified players.

> The existing BetterAmongUs logo and artwork are retained from upstream for now. No HoryTweaks artwork has been supplied.

## Compatibility

HoryTweaks 1.0.0 targets **Among Us v19.0.0 / 2026.9.29**. It is not intended for older or newer game versions unless a later release says otherwise.

Release downloads: [HoryTweaks releases](https://github.com/horizzon3507/HoryTweaks/releases)

## Installation

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

- Client-side anti-cheat checks for invalid actions and known cheat clients.
- Additional lobby and gameplay settings for hosts.
- Client-side improvements such as lobby information, customizable chat and minimap options.
- Chat commands for player information, log export and supported host actions.
- Preset and local anti-cheat data retained under the compatible `Better_Data` user-data folder.

## Commands

Use `/help` in chat for an overview and `/commands` for the available command list. The command prefix can be changed in settings or with `/setprefix`.

## Platform support

Install packages are produced for Steam, Epic Games, Microsoft Store and itch.io. Linux support is through Steam Proton. This release workflow does not produce Android packages; iOS and console installations are not supported.

## Credits and license

HoryTweaks is based on [D1GQ's BetterAmongUs](https://github.com/D1GQ/BetterAmongUs), including its code, artwork and GPL-licensed components. Thank you to D1GQ and the BetterAmongUs contributors, including [Nyx](https://github.com/DeveloperNyx) and [At0mBomba](https://github.com/At0mBomba). See [LICENSE](LICENSE) for the preserved GPL license and its terms.

## Disclaimer

HoryTweaks is an unofficial, fan-made mod for Among Us. It is not affiliated with, endorsed by or associated with Innersloth LLC or the official Among Us game. All Among Us trademarks and copyrights belong to Innersloth LLC. Use the mod at your own risk.
