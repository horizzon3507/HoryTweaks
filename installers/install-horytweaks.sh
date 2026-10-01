#!/usr/bin/env bash
set -euo pipefail
command -v python3 >/dev/null 2>&1 || { echo 'Install Python 3 first (python3).' >&2; exit 1; }
exec python3 - "$@" <<'PYTHON'
import argparse
import hashlib
import json
import os
import re
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import uuid
import zipfile
from datetime import datetime, timezone
from pathlib import Path

RELEASES = "https://github.com/horizzon3507/HoryTweaks/releases"
LEGACY = "BepInEx/plugins/BetterAmongUs.dll"
LIMIT = 1024 * 1024 * 1024


def ask(message):
    try:
        with open("/dev/tty", "r+") as tty:
            tty.write(message)
            tty.flush()
            return tty.readline().strip()
    except OSError as error:
        raise RuntimeError("No terminal. Supply --game-dir, --store and --yes.") from error


def discover_games():
    roots = [Path.home() / name for name in (
        ".steam/steam", ".local/share/Steam", ".var/app/com.valvesoftware.Steam/data/Steam")]
    libraries = list(roots)
    for root in roots:
        vdf = root / "steamapps/libraryfolders.vdf"
        if vdf.is_file():
            libraries.extend(Path(p.replace("\\\\", "\\")) for p in
                             re.findall(r'"path"\s*"([^"]+)"', vdf.read_text()))
    games = {str((library / "steamapps/common/Among Us").resolve()) for library in libraries
             if (library / "steamapps/common/Among Us/Among Us.exe").is_file()}
    return sorted(games)


def download(url, destination, optional=False):
    if not shutil.which("curl"):
        raise RuntimeError("Install curl to download a release, or use --package and --sha256.")
    result = subprocess.run([
        "curl", "--proto", "=https", "--proto-redir", "=https", "--fail", "--location",
        "--silent", "--show-error", "--retry", "2", "--connect-timeout", "15",
        "--max-time", "300", "--max-filesize", str(LIMIT),
        "--output", str(destination), "--write-out", "%{http_code}", url,
    ], capture_output=True, text=True, timeout=950)
    if optional and result.returncode == 22 and result.stdout == "404":
        return False
    if result.returncode:
        raise RuntimeError(f"Download failed (HTTP {result.stdout}): {result.stderr.strip()}")
    return True


def resolve_version(version):
    if version == "latest":
        if not shutil.which("curl"):
            raise RuntimeError("Install curl to look up the latest release.")
        result = subprocess.run([
            "curl", "--proto", "=https", "--proto-redir", "=https", "--fail", "--location",
            "--silent", "--show-error", "--head", "--connect-timeout", "15", "--max-time", "60",
            "--output", os.devnull, "--write-out", "%{url_effective}", RELEASES + "/latest",
        ], capture_output=True, text=True, timeout=70)
        if result.returncode or not result.stdout.startswith(RELEASES + "/tag/"):
            raise RuntimeError("Cannot resolve latest release. Try --version v0.1.2.")
        version = result.stdout.rsplit("/", 1)[-1]
    version = version if version.startswith("v") else "v" + version
    if not re.fullmatch(r"v\d+\.\d+\.\d+(?:[.-][A-Za-z0-9.-]+)?", version):
        raise RuntimeError("Invalid release tag; use a version such as v0.1.2.")
    return version


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def check_game_closed():
    if not shutil.which("pgrep"):
        raise RuntimeError("Install pgrep (procps) so the running-game check can complete.")
    result = subprocess.run(["pgrep", "-if", "[A]mong Us[.]exe"], stdout=subprocess.DEVNULL)
    if result.returncode == 0:
        raise RuntimeError("Close Among Us before installing.")
    if result.returncode != 1:
        raise RuntimeError("Could not check whether Among Us is running.")


def safe_name(name):
    parts = name.rstrip("/").split("/")
    if not parts or any(not p or p in (".", "..") or p.endswith((".", " ")) or
                        re.search(r'[\\\x00-\x1f<>:"|?*]', p) or
                        re.fullmatch(r"(?i)(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?", p)
                        for p in parts):
        raise RuntimeError(f"Unsafe archive path: {name!r}")
    return "/".join(parts)


def check_target(game, relative):
    current = game
    parts = relative.split("/")
    for index, part in enumerate(parts):
        current = current / part
        if current.is_symlink():
            raise RuntimeError(f"Refusing symbolic link: {current}")
        if current.exists() and (current.is_dir() if index == len(parts) - 1 else not current.is_dir()):
            raise RuntimeError(f"Unexpected file/directory: {current}")


def stage_package(archive, stage):
    files = []
    seen = set()
    total = 0
    with zipfile.ZipFile(archive) as bundle:
        if len(bundle.infolist()) > 10000:
            raise RuntimeError("Archive has too many entries.")
        for entry in bundle.infolist():
            name = safe_name(entry.filename)
            key = name.lower()
            mode = entry.external_attr >> 16
            if (stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR) or key in seen):
                raise RuntimeError(f"Unsupported or duplicate archive entry: {name}")
            seen.add(key)
            total += entry.file_size
            if total > LIMIT or entry.flag_bits & 1:
                raise RuntimeError("Archive is too large or encrypted.")
            if entry.is_dir():
                continue
            if key.startswith(("better_data/", "bepinex/config/")):
                continue
            if not (key.startswith(("bepinex/core/", "bepinex/patchers/", "dotnet/")) or
                    name in ("BepInEx/plugins/HoryTweaks.dll", "winhttp.dll", "doorstop_config.ini",
                             ".doorstop_version", "changelog.txt")):
                raise RuntimeError(f"Unexpected file in mod package: {name}")
            target = stage / name
            target.parent.mkdir(parents=True, exist_ok=True)
            with bundle.open(entry) as source, target.open("xb") as output:
                shutil.copyfileobj(source, output)
            files.append(name)
    required = {"BepInEx/plugins/HoryTweaks.dll", "winhttp.dll", "doorstop_config.ini"}
    if not required.issubset(files) or not any(n.startswith("BepInEx/core/") for n in files):
        raise RuntimeError("Not a full HoryTweaks package; runtime or plugin is missing.")
    for name in ("BepInEx/plugins/HoryTweaks.dll", "winhttp.dll"):
        with (stage / name).open("rb") as stream:
            if stream.read(2) != b"MZ":
                raise RuntimeError(f"Invalid Windows DLL: {name}")
    return sorted(files)


def atomic_copy(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=".horytweaks-", dir=target.parent)
    os.close(fd)
    try:
        shutil.copy2(source, temporary)
        os.replace(temporary, target)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def install(game, stage, files, version, digest):
    lock = game / ".horytweaks-install.lock"
    lock.mkdir()
    backup = None
    touched = []
    originals = {}
    try:
        for name in files + [LEGACY]:
            check_target(game, name)
        check_target(game, ".horytweaks-backups/probe")
        backup = game / ".horytweaks-backups" / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid.uuid4().hex[:8])
        backup.mkdir(parents=True)
        for name in files + [LEGACY]:
            originals[name] = (game / name).exists()
            if originals[name]:
                target = backup / "files" / name
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(game / name, target)
        manifest = {"version": version, "sha256": digest, "game": str(game), "originals": originals}
        (backup / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
        (backup / "status.txt").write_text("installing\n")
        for name in files:
            touched.append(name)
            atomic_copy(stage / name, game / name)
        if originals[LEGACY]:
            touched.append(LEGACY)
            (game / LEGACY).unlink()
        (backup / "status.txt").write_text("installed\n")
        print(f"Installed {version}. Backup and file manifest: {backup}")
    except BaseException:
        failures = []
        for name in reversed(touched):
            try:
                if originals[name]:
                    atomic_copy(backup / "files" / name, game / name)
                elif (game / name).exists():
                    (game / name).unlink()
            except OSError as error:
                failures.append(f"{name}: {error}")
        if backup is not None and backup.is_dir():
            (backup / "status.txt").write_text("rollback-failed\n" if failures else "rolled-back\n")
            print(f"Installation failed. Backup: {backup}", file=sys.stderr)
        if failures:
            print("Manual recovery required:\n" + "\n".join(failures), file=sys.stderr)
        raise
    finally:
        lock.rmdir()


def main():
    parser = argparse.ArgumentParser(description="Install HoryTweaks for Steam/Proton (close Among Us first).")
    parser.add_argument("--game-dir", type=Path, help="folder containing Among Us.exe")
    parser.add_argument("--store", choices=("steam", "epic", "msstore", "itch"))
    parser.add_argument("--version", default="latest", help="latest or a release tag, e.g. v0.1.2")
    parser.add_argument("--package", type=Path, help="offline full ZIP (requires --sha256)")
    parser.add_argument("--sha256", help="expected ZIP SHA-256, overrides published checksum")
    parser.add_argument("--dry-run", action="store_true", help="validate without changing game files")
    parser.add_argument("--yes", action="store_true", help="skip final confirmation")
    args = parser.parse_args()
    if args.sha256 and not re.fullmatch(r"[0-9a-fA-F]{64}", args.sha256):
        parser.error("--sha256 must contain 64 hexadecimal characters")
    if args.package and not args.sha256:
        parser.error("--package requires --sha256 from a trusted source")
    if args.game_dir is None:
        games = discover_games()
        for index, game in enumerate(games, 1):
            print(f"{index}. {game}")
        answer = ask("Select a game number or enter its full folder path: ")
        if answer.isdigit() and 1 <= int(answer) <= len(games):
            answer = games[int(answer) - 1]
        args.game_dir = Path(answer)
    game = args.game_dir.expanduser().resolve(strict=True)
    if not (game / "Among Us.exe").is_file() or not (game / "Among Us_Data").is_dir():
        raise RuntimeError("Select the game folder containing Among Us.exe and Among Us_Data.")
    check_game_closed()
    if args.store is None:
        args.store = ask("Store [steam/epic/msstore/itch]: ").lower()
        if args.store not in ("steam", "epic", "msstore", "itch"):
            raise RuntimeError("Unknown store.")
    with tempfile.TemporaryDirectory(prefix="horytweaks-") as temporary:
        work = Path(temporary)
        expected = args.sha256
        if args.package:
            archive = args.package.resolve(strict=True)
            version = "offline" if args.version == "latest" else resolve_version(args.version)
        else:
            version = resolve_version(args.version)
            platform = "Itchio" if args.store == "itch" else "Steam-Epic-MsStore"
            asset = f"HoryTweaks-{platform}-{version}.zip"
            base = f"{RELEASES}/download/{version}/"
            archive = work / asset
            print(f"Downloading {asset}...")
            download(base + asset, archive)
            if expected is None:
                checksums = work / "SHA256SUMS.txt"
                if download(base + checksums.name, checksums, optional=True):
                    matches = re.findall(r"^([0-9a-fA-F]{64})  " + re.escape(asset) + r"$", checksums.read_text(), re.M)
                    if len(matches) != 1:
                        raise RuntimeError("Release checksum manifest does not identify this package.")
                    expected = matches[0]
                else:
                    print("This older release has no checksum manifest; using the official HTTPS download.")
        digest = sha256(archive)
        if expected and digest.lower() != expected.lower():
            raise RuntimeError("SHA-256 mismatch. Nothing was installed.")
        print(f"ZIP SHA-256: {digest}")
        stage = work / "stage"
        files = stage_package(archive, stage)
        for name in files + [LEGACY]:
            check_target(game, name)
        needed = sum((stage / name).stat().st_size + ((game / name).stat().st_size if (game / name).exists() else 0) for name in files)
        if shutil.disk_usage(game).free < needed + 64 * 1024 * 1024:
            raise RuntimeError("Not enough free space for installation and backup.")
        print(f"Target: {game}\nStore: {args.store}\nRelease: {version}\nFiles: {len(files)}")
        print("Better_Data, BepInEx/config and other plugins are preserved. BetterAmongUs.dll is backed up and removed.")
        if args.dry_run:
            print("Dry run complete. No game files changed.")
            return
        if not args.yes and ask("Install? [y/N]: ").lower() not in ("y", "yes"):
            print("Cancelled. No game files changed.")
            return
        check_game_closed()
        install(game, stage, files, version, digest)
        print('Steam/Proton launch options: WINEDLLOVERRIDES="winhttp=n,b" PROTON_NO_ESYNC=1 %command%')


if __name__ == "__main__":
    signal.signal(signal.SIGTERM, lambda signum, frame: sys.exit(130))
    try:
        main()
    except (Exception, KeyboardInterrupt) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        sys.exit(1)
PYTHON
