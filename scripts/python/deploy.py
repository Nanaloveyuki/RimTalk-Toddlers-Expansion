#!/usr/bin/env python3
"""Build, stage, and deploy RTE Continue."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
RELEASE = ROOT / "artifacts/rte_continue/release"
IS_WSL = sys.platform == "linux" and "microsoft" in platform.release().lower()
GAME_NAMES = {"rimworld", "rimworldwin", "rimworldwin64", "rimworldwin64steam", "rimworldlinux", "rimworldmac"}
DEPENDENCIES = {
    "HarmonyModDir": (("2009463077", "1.7/Assemblies/0Harmony.dll"),),
    "ToddlersDir": (
        ("2903359152", "1.7/Assemblies/Toddlers.dll"),
        ("3832132406", "1.7/Assemblies/Toddlers.dll"),
        ("2903359152", "1.6/Assemblies/Toddlers.dll"),
    ),
}
PATH_PROPERTIES = {*DEPENDENCIES, "HarmonyAssemblyPath", "ToddlersAssemblyPath"}


def local_path(value):
    if IS_WSL and (re.match(r"^[A-Za-z]:", value) or value.startswith("\\\\")):
        value = subprocess.check_output(["wslpath", "-u", value], text=True).strip()
    return Path(os.path.abspath(Path(value).expanduser()))


def powershell(script):
    return subprocess.check_output(
        ["powershell.exe", "-NoProfile", "-NonInteractive", "-Command", script], text=True
    ).strip()


def reject_links(path):
    for part in (path, *path.parents):
        try:
            info = part.lstat()
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
            raise RuntimeError(f"Refusing symlink or reparse point: {part}")
        if part != path and not stat.S_ISDIR(info.st_mode):
            raise RuntimeError(f"Ancestor is not a directory: {part}")


def reject_windows_reparse_points(target):
    if not IS_WSL or not re.match(r"^/mnt/[a-zA-Z]/", str(target)):
        return
    windows_target = subprocess.check_output(["wslpath", "-w", str(target)], text=True).strip()
    escaped = windows_target.replace("'", "''")
    powershell(
        "$ErrorActionPreference='Stop'; $target='" + escaped + "'; "
        "function Check($item) { if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) "
        "{ throw ('Unsafe reparse path: ' + $item.FullName) } }; "
        "for ($path=$target; $path; ) { "
        "if (Test-Path -LiteralPath $path) { Check (Get-Item -LiteralPath $path -Force) }; "
        "$parent=[IO.Directory]::GetParent($path); $path=$parent.FullName }; "
        "if (Test-Path -LiteralPath $target) { "
        "Get-ChildItem -LiteralPath $target -Recurse -Force | ForEach-Object { Check $_ } }"
    )


def reject_running_game():
    if os.name == "nt" or IS_WSL:
        names = ",".join(f"'{name}'" for name in sorted(GAME_NAMES))
        processes = powershell(
            f"$ErrorActionPreference='Stop'; $names=@({names}); "
            "@(Get-Process | Where-Object { $names -contains $_.ProcessName.ToLowerInvariant() }).Count"
        )
        if processes != "0":
            raise RuntimeError("Exit RimWorld before deploying.")
    if os.name != "nt":
        processes = subprocess.check_output(["ps", "-A", "-o", "comm="], text=True)
        if any(name.strip().lower().removesuffix(".exe") in GAME_NAMES for name in processes.splitlines()):
            raise RuntimeError("Exit RimWorld before deploying.")


def game_version(game):
    reject_links(game)
    version_file = game / "Version.txt"
    version = re.match(r"^\s*(1\.[67])(?=\.|\s|$)", version_file.read_text(encoding="utf-8-sig"))
    if not version:
        raise RuntimeError(f"Expected RimWorld 1.6 or 1.7: {version_file}")
    if version[1] == "1.7":
        if not all((game / name).is_file() for name in ("Assembly-CSharp.dll", "UnityStubs.dll")):
            raise RuntimeError(f"RimWorld 1.7 assemblies missing: {game}")
    elif not any(
        (managed / "Assembly-CSharp.dll").is_file() and (managed / "UnityEngine.CoreModule.dll").is_file()
        for managed in game.glob("RimWorld*_Data/Managed")
    ):
        raise RuntimeError(f"RimWorld 1.6 managed assemblies missing: {game}")
    return version[1]


def verify_identity(directory, package_id):
    about = directory / "About/About.xml"
    reject_links(about)
    metadata = ET.parse(about).getroot()
    ids = [(node.text or "").strip().casefold() for node in metadata.findall("packageId")]
    if metadata.tag != "ModMetaData" or ids != [package_id.casefold()]:
        raise RuntimeError(f"Expected {package_id}: {about}")


def guard_target(target, game, config):
    reject_links(target)
    if target.parent != game / "Mods" or not target.parent.is_dir():
        raise RuntimeError(f"Target must be a direct child of the game's Mods directory: {target}")
    if target == ROOT or ROOT in target.parents or target in ROOT.parents:
        raise RuntimeError(f"Target must be separate from the repository: {target}")
    if target.name == "3659064387":
        raise RuntimeError(f"Refusing to deploy over the original mod: {target}")
    reject_windows_reparse_points(target)
    if target.exists() and (not target.is_dir() or any(target.iterdir())):
        verify_identity(target, config["package_id"])
    elif target.name != config["target"]:
        raise RuntimeError(f"New install directory must be named {config['target']}: {target}")


def dependency_dir(game, candidates):
    workshop = game.parent.parent / "workshop/content/294100"
    for workshop_id, assembly in candidates:
        for root in (game / "Mods", workshop):
            folder = root / workshop_id
            if (folder / assembly).is_file():
                return folder
    return game / "Mods" / candidates[0][0]


def build_17(game, args, config, output):
    properties = {name: dependency_dir(game, candidates) for name, candidates in DEPENDENCIES.items()}
    for name, value in args.property:
        properties[name] = local_path(value) if name in PATH_PROPERTIES else value
    properties.update(RimWorldDir=game, GameVersion="1.7")
    subprocess.run(
        [args.dotnet, "build", str(ROOT / config["project"]), "-c", args.configuration, "--nologo",
         "-o", str(output), *(f"-p:{name}={value}" for name, value in properties.items())],
        cwd=ROOT,
        check=True,
    )


def release_files(config, build_output):
    files = {}
    for entry in map(Path, config["files"]):
        source = ROOT / entry
        if build_output and entry.parts[0] == "1.7":
            source = build_output / entry.name
        reject_links(source)
        if source.is_dir():
            for item in sorted(source.rglob("*")):
                reject_links(item)
                if item.is_file():
                    files[entry / item.relative_to(source)] = item
        elif source.is_file() and source.stat().st_size:
            files[entry] = source
        else:
            raise RuntimeError(f"Missing release input: {source}")
    return files


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.digest()


def copy_verified(files, target):
    for relative in files:
        destination = target / relative
        reject_links(destination)
        if destination.exists() and not destination.is_file():
            raise RuntimeError(f"Destination is not a file: {destination}")
    for relative, source in files.items():
        destination = target / relative
        expected = sha256(source)
        if destination.is_file() and sha256(destination) == expected:
            continue
        destination.parent.mkdir(parents=True, exist_ok=True)
        descriptor, name = tempfile.mkstemp(prefix=".deploy-", dir=destination.parent)
        os.close(descriptor)
        temporary = Path(name)
        try:
            shutil.copy2(source, temporary)
            if sha256(temporary) != expected:
                raise RuntimeError(f"SHA-256 mismatch: {destination}")
            temporary.replace(destination)
        finally:
            temporary.unlink(missing_ok=True)


def publish_release(staging, package_id):
    reject_links(RELEASE)
    previous = staging.parent / "previous"
    if RELEASE.exists():
        verify_identity(RELEASE, package_id)
        RELEASE.rename(previous)
    try:
        staging.rename(RELEASE)
    except OSError:
        if previous.exists():
            previous.rename(RELEASE)
        raise


def property_pair(value):
    name, separator, setting = value.partition("=")
    if not separator or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
        raise argparse.ArgumentTypeError("Expected MSBuild property NAME=VALUE")
    return name, setting


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rimworld-dir", default=os.environ.get("RIMWORLD_DIR") or os.environ.get("RimWorldDir"))
    parser.add_argument("--target", help="Install directory directly under the game's Mods directory")
    parser.add_argument("--build-only", action="store_true", help="Stage a release without writing to Mods")
    parser.add_argument("--skip-build", action="store_true", help="Package the checked-in assemblies")
    parser.add_argument("--preflight", action="store_true", help="Check game installation and target without building")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET", "dotnet"))
    parser.add_argument("--property", type=property_pair, action="append", default=[], metavar="NAME=VALUE")
    args = parser.parse_args()

    if not args.rimworld_dir and not (args.build_only and args.skip_build):
        parser.error("Select an installed game with --rimworld-dir or RIMWORLD_DIR")
    if args.build_only and args.target:
        parser.error("--target requires deployment")
    game = local_path(args.rimworld_dir) if args.rimworld_dir else None
    version = game_version(game) if game else None
    config = json.loads((ROOT / "scripts/deploy/rte_continue.json").read_text(encoding="utf-8"))
    verify_identity(ROOT, config["package_id"])
    if not args.build_only:
        target = local_path(args.target) if args.target else game / "Mods" / config["target"]
        guard_target(target, game, config)
        reject_running_game()
    if args.preflight:
        print(f"Preflight passed for {target if not args.build_only else game or ROOT}")
        return

    reject_links(RELEASE)
    RELEASE.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".release-", dir=RELEASE.parent) as path:
        temporary = Path(path)
        build_output = None
        if not args.skip_build and version == "1.7":
            build_output = temporary / "build"
            build_17(game, args, config, build_output)
        files = release_files(config, build_output)
        staging = temporary / "release"
        copy_verified(files, staging)
        verify_identity(staging, config["package_id"])
        if not args.build_only:
            guard_target(target, game, config)
            reject_running_game()
            copy_verified({relative: staging / relative for relative in files}, target)
            print(f"Deployed and SHA-256 verified {len(files)} files: {target}")
        publish_release(staging, config["package_id"])
        print(f"Staged and SHA-256 verified {len(files)} files: {RELEASE}")


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, ValueError, ET.ParseError, subprocess.SubprocessError) as error:
        sys.exit(f"Deployment failed: {error}")
