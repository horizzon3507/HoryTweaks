# HoryTweaks installers

Install or repair the full HoryTweaks runtime from an official GitHub release. The scripts support the existing v0.1.2 packages and later releases. Close Among Us before installing.

## Windows

Extract **all** files from `HoryTweaks-Installers.zip` into one folder, then double-click `install-horytweaks.bat`. Keep `Install-HoryTweaks.ps1` beside it. The BAT runs the PowerShell engine with Windows PowerShell 5.1, already included with Windows 10/11. Its execution-policy override applies only to that process; it does not change your saved policy or request administrator access.

The wizard lists Steam installations, including additional libraries, and Epic installations. Choose a detected folder or paste the path from your store's **Browse local files** command. For Microsoft Store and itch.io, enter the folder manually. Select the store, review the destination and confirm with `y`.

From Command Prompt:

```bat
install-horytweaks.bat -GameDir "D:\SteamLibrary\steamapps\common\Among Us" -Store steam
install-horytweaks.bat -GameDir "D:\Games\Among Us" -Store itch -Version v0.1.2 -DryRun
install-horytweaks.bat -Help
```

Protected Microsoft Store folders must be accessible through the store's supported file access. The installer does not change ownership or permissions. Enterprise policies that block PowerShell still apply.

## Linux / Steam Deck

Requires Bash, Python 3.8+, curl and pgrep (usually supplied by `procps`). Download `install-horytweaks.sh` or extract the installer ZIP, then run:

```sh
bash install-horytweaks.sh
bash install-horytweaks.sh --game-dir "$HOME/.local/share/Steam/steamapps/common/Among Us" --store steam
bash install-horytweaks.sh --game-dir "/mnt/games/Among Us" --store steam --version v0.1.2 --dry-run
bash install-horytweaks.sh --help
```

Steam detection includes extra libraries and Flatpak Steam. Other store locations can be supplied manually, but Linux runtime support is Steam with Proton 9.0 or newer. Use the Windows version of Among Us. There is no native Linux or macOS game package.

The installer prints these Steam launch options; set them yourself in the game's properties:

```text
WINEDLLOVERRIDES="winhttp=n,b" PROTON_NO_ESYNC=1 %command%
```

## Version, offline installation and checksums

Both installers default to the latest published GitHub release. Set `--version v0.1.2` / `-Version v0.1.2` to pin a release. Steam, Epic and Microsoft Store share a package; itch.io uses its own package. `--yes` / `-Yes` skips the final confirmation, but supply the game folder and store to avoid the discovery prompts.

Online downloads use bounded HTTPS requests with retries. When the release contains `SHA256SUMS.txt`, the ZIP must match its SHA-256. Older releases, including v0.1.2, have no checksum manifest: the installer explicitly reports that it is using the official HTTPS download without a published checksum. `--sha256` / `-Sha256` supplies an expected hash directly. A checksum from the same release detects corruption, not a compromised release publisher.

For offline installation, download the full store ZIP and obtain its hash from a trusted source. A DLL alone is not a complete installation package.

```sh
bash install-horytweaks.sh --game-dir "/mnt/games/Among Us" --store steam \
  --package "/home/user/Downloads/HoryTweaks-Steam-Epic-MsStore-v0.1.2.zip" \
  --sha256 <64-character-sha256> --yes
```

```bat
install-horytweaks.bat -GameDir "D:\Games\Among Us" -Store steam -Package "C:\Downloads\package.zip" -Sha256 <64-character-sha256> -Yes
```

Replace the hash placeholder; do not paste it literally. Offline packages require a hash. The store and optional version are labels in offline mode: ensure the ZIP came from the right store's release asset.

## Backups and recovery

Before changing game files, the installer validates the full archive, rejects traversal paths, duplicate paths, links and unexpected files, stages the runtime and checks available space. `--dry-run` / `-DryRun` performs download and validation without writing into the game folder. It cannot guarantee that a later write will be permitted.

Each installation saves every replaced file in:

```text
<game>/.horytweaks-backups/<UTC timestamp>-<id>/files/
```

`manifest.json` records the destination, selected version, package hash and whether each file existed. `status.txt` records `installing`, `installed`, `rolled-back` or `rollback-failed`. Keep these backups until you have tested the game.

- `Better_Data`, existing `BepInEx/config` and other plugins are preserved. Packaged settings/config files are skipped.
- `BetterAmongUs.dll` is backed up and removed from the plugin folder after the new runtime is installed.
- A write error triggers rollback of changed files. Newly created files are removed; empty directories can remain.
- A directory lock prevents two installers from changing the same game folder simultaneously.

Power loss, killing the process or closing the terminal can interrupt recovery. Close the game and installer, inspect the newest backup's status, and restore files from its `files` directory into the game folder. For manifest entries with `false`, remove the newly installed file if it exists. Do not delete game data or other mods. If recovery fails because files are locked or protected, resolve that access issue before retrying. Remove the empty `.horytweaks-install.lock` directory only after confirming no installer is running and recovery is complete.

## Development checks

```sh
bash -n installers/install-horytweaks.sh
python3 -m unittest discover -s tests/installers -v
pwsh -NoProfile -File tests/installers/Test-InstallerTransaction.ps1
```

`INSTALLER_ENGINE=pwsh` exercises the PowerShell CLI on Unix; Windows CI defaults to the BAT and Windows PowerShell 5.1. Tests use temporary game folders and ZIP fixtures, cover failed writes and rollback, and do not launch Among Us. They do not establish runtime compatibility with the game.
