import hashlib
import os
import stat
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[2]
ENGINE = os.environ.get("INSTALLER_ENGINE", "bat" if os.name == "nt" else "sh")
SCRIPT = ROOT / "installers/install-horytweaks.sh"
PLUGIN = "BepInEx/plugins/HoryTweaks.dll"
LEGACY = "BepInEx/plugins/BetterAmongUs.dll"
PAYLOAD = {
    PLUGIN: b"MZ-new-mod",
    "BepInEx/core/runtime.dll": b"runtime",
    "dotnet/runtime.dll": b"dotnet",
    "winhttp.dll": b"MZ-winhttp",
    "doorstop_config.ini": b"new-doorstop",
    "Better_Data/Preset-0.json": b"must-not-replace-settings",
    "BepInEx/config/BepInEx.cfg": b"must-not-replace-config",
}


def load_unix_engine():
    source = SCRIPT.read_text().split("<<'PYTHON'\n", 1)[1].rsplit("\nPYTHON", 1)[0]
    namespace = {"__name__": "installer_test"}
    exec(compile(source, str(SCRIPT), "exec"), namespace)
    return namespace


class InstallerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="hory-installer-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.game = self.root / "Among Us (test) & unicode-é"
        self.game.mkdir()
        self.put("Among Us.exe", b"game-do-not-touch")
        (self.game / "Among Us_Data").mkdir()
        self.put("Among Us_Data/original", b"original")
        self.put("Better_Data/Preset-0.json", b"settings")
        self.put("BepInEx/config/BepInEx.cfg", b"config")
        self.put("BepInEx/plugins/OtherMod.dll", b"other-mod")
        self.archive = self.root / "package with spaces.zip"
        self.bundle()

    def put(self, name, content):
        path = self.game / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)

    def bundle(self, extra=None, removed=()):
        entries = dict(PAYLOAD)
        entries.update(extra or {})
        with zipfile.ZipFile(self.archive, "w") as archive:
            for name, content in entries.items():
                if name not in removed:
                    archive.writestr(name, content)

    def snapshot(self):
        return {str(p.relative_to(self.game)): p.read_bytes() for p in self.game.rglob("*")
                if p.is_file() and ".horytweaks-backups" not in p.parts}

    def run_installer(self, extra=(), digest=None, success=True, store="steam"):
        digest = digest or hashlib.sha256(self.archive.read_bytes()).hexdigest()
        if ENGINE == "sh":
            command = ["bash", str(SCRIPT), "--game-dir", str(self.game), "--store", store,
                       "--package", str(self.archive), "--sha256", digest, "--yes"]
        else:
            arguments = ["-GameDir", str(self.game), "-Store", store, "-Package", str(self.archive),
                         "-Sha256", digest, "-Yes"]
            if ENGINE == "bat":
                command = [str(ROOT / "installers/install-horytweaks.bat")] + arguments
            else:
                command = ["pwsh", "-NoProfile", "-File", str(ROOT / "installers/Install-HoryTweaks.ps1")] + arguments
        result = subprocess.run(command + list(extra), capture_output=True, text=True, timeout=40)
        if success:
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        else:
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        return result

    def test_clean_install_and_preserved_user_files(self):
        before = self.snapshot()
        self.run_installer()
        for name, content in before.items():
            self.assertEqual((self.game / name).read_bytes(), content, name)
        self.assertEqual((self.game / PLUGIN).read_bytes(), PAYLOAD[PLUGIN])
        self.assertFalse((self.game / ".horytweaks-install.lock").exists())
        self.assertEqual(len(list(self.game.glob(".horytweaks-backups/*/manifest.json"))), 1)

    def test_upgrade_backup_including_legacy_and_repeat_install(self):
        self.put(PLUGIN, b"old-mod")
        self.put(LEGACY, b"legacy")
        self.put("winhttp.dll", b"old-loader")
        self.run_installer(store="itch")
        backup = next(self.game.glob(".horytweaks-backups/*"))
        self.assertEqual((backup / "files" / PLUGIN).read_bytes(), b"old-mod")
        self.assertEqual((backup / "files" / LEGACY).read_bytes(), b"legacy")
        self.assertEqual((backup / "files/winhttp.dll").read_bytes(), b"old-loader")
        self.assertFalse((self.game / LEGACY).exists())
        self.assertEqual((backup / "status.txt").read_text().strip(), "installed")
        self.run_installer()
        self.assertEqual(len(list(self.game.glob(".horytweaks-backups/*/manifest.json"))), 2)

    def test_dry_run_changes_nothing(self):
        before = self.snapshot()
        self.run_installer(extra=["--dry-run" if ENGINE == "sh" else "-DryRun"])
        self.assertEqual(self.snapshot(), before)
        self.assertFalse((self.game / ".horytweaks-backups").exists())

    def test_bad_checksum_changes_nothing(self):
        before = self.snapshot()
        result = self.run_installer(digest="0" * 64, success=False)
        self.assertIn("SHA-256 mismatch", result.stdout + result.stderr)
        self.assertEqual(self.snapshot(), before)

    def test_invalid_zip_changes_nothing(self):
        before = self.snapshot()
        self.archive.write_bytes(b"<html>error</html>")
        self.run_installer(success=False)
        self.assertEqual(self.snapshot(), before)

    def test_unsafe_archives_are_rejected_without_writes(self):
        before = self.snapshot()
        for path in ("../escaped", "/absolute", "BepInEx/../../escape", "BepInEx\\escape",
                     "C:/escaped", "dotnet/name:stream", "dotnet/CON.txt", "dotnet/foo.",
                     "BepInEx/plugins/horytweaks.dll", "BepInEx/plugins/Unexpected.dll"):
            with self.subTest(path=path):
                self.bundle({path: b"bad"})
                self.run_installer(success=False)
                self.assertEqual(self.snapshot(), before)
        self.assertFalse((self.root / "escaped").exists())

    def test_partial_package_and_non_dll_are_rejected(self):
        before = self.snapshot()
        self.bundle(removed=("winhttp.dll",))
        self.run_installer(success=False)
        self.bundle({PLUGIN: b"not a DLL"})
        self.run_installer(success=False)
        self.assertEqual(self.snapshot(), before)

    def test_archive_symlink_is_rejected(self):
        before = self.snapshot()
        with zipfile.ZipFile(self.archive, "a") as archive:
            link = zipfile.ZipInfo("dotnet/link")
            link.create_system = 3
            link.external_attr = (stat.S_IFLNK | 0o777) << 16
            archive.writestr(link, "../../outside")
        self.run_installer(success=False)
        self.assertEqual(self.snapshot(), before)

    def test_existing_lock_is_not_removed(self):
        lock = self.game / ".horytweaks-install.lock"
        lock.mkdir()
        before = self.snapshot()
        self.run_installer(success=False)
        self.assertTrue(lock.is_dir())
        self.assertEqual(self.snapshot(), before)

    def test_wrong_game_path_and_directory_collision(self):
        (self.game / "Among Us.exe").unlink()
        self.run_installer(success=False)
        self.put("Among Us.exe", b"game-do-not-touch")
        (self.game / "winhttp.dll").mkdir()
        before = self.snapshot()
        self.run_installer(success=False)
        self.assertEqual(self.snapshot(), before)

    @unittest.skipIf(os.name == "nt", "Windows junction tested separately in PowerShell")
    def test_destination_symlink_is_rejected(self):
        outside = self.root / "outside"
        outside.mkdir()
        (self.game / "dotnet").symlink_to(outside, target_is_directory=True)
        self.run_installer(success=False)
        self.assertEqual(list(outside.iterdir()), [])

    @unittest.skipUnless(ENGINE == "sh", "Python transaction only")
    def test_rollback_restores_files_after_partial_copy(self):
        engine = load_unix_engine()
        self.put(PLUGIN, b"old-mod")
        self.put(LEGACY, b"legacy")
        self.put("winhttp.dll", b"old-loader")
        before = self.snapshot()
        stage = self.root / "stage"
        files = engine["stage_package"](self.archive, stage)
        real_copy = engine["atomic_copy"]
        failed = False

        def copy_with_failure(source, target):
            nonlocal failed
            if not failed and target.name == "winhttp.dll":
                failed = True
                raise OSError("Injected write failure")
            real_copy(source, target)

        engine["atomic_copy"] = copy_with_failure
        with self.assertRaisesRegex(OSError, "Injected write failure"):
            engine["install"](self.game, stage, files, "test", "test")
        self.assertTrue(failed)
        self.assertEqual(self.snapshot(), before)
        self.assertFalse((self.game / ".horytweaks-install.lock").exists())
        self.assertEqual(next(self.game.glob(".horytweaks-backups/*/status.txt")).read_text().strip(), "rolled-back")

    @unittest.skipUnless(ENGINE == "sh", "Python discovery only")
    def test_custom_steam_library_detection(self):
        home = self.root / "home"
        steam = home / ".var/app/com.valvesoftware.Steam/data/Steam/steamapps"
        steam.mkdir(parents=True)
        library = self.root / "External Steam Library"
        installed = library / "steamapps/common/Among Us"
        installed.mkdir(parents=True)
        (installed / "Among Us.exe").write_bytes(b"game")
        (steam / "libraryfolders.vdf").write_text('"libraryfolders" { "1" { "path" "' + str(library) + '" } }')
        with patch.dict(os.environ, {"HOME": str(home)}):
            self.assertEqual(load_unix_engine()["discover_games"](), [str(installed)])


if __name__ == "__main__":
    unittest.main()
