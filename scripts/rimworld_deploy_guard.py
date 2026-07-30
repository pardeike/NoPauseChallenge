#!/usr/bin/env python3
"""Validate and serialize a RimWorld mod deployment on every desktop OS."""

from __future__ import annotations

import argparse
import hashlib
import re
import subprocess
from pathlib import Path


SAFE_MOD_NAME = re.compile(r"^[A-Za-z][A-Za-z0-9]*$")
LOCK_DIRECTORY: Path | None = None


class DeployError(RuntimeError):
    pass


def physical_directory(path: Path) -> Path:
    if not path.is_dir():
        raise DeployError(f"RimWorld Mods directory does not exist: {path}")
    return path.resolve(strict=True)


def validate_mod_directory(path: Path, mod_name: str) -> Path:
    resolved = physical_directory(path)
    home = Path.home().resolve()
    if (
        resolved == Path(resolved.anchor)
        or resolved == home
        or resolved.parent == Path(resolved.anchor)
    ):
        raise DeployError(f"Refusing unsafe RimWorld Mods directory: {resolved}")
    if resolved.name.casefold() != "mods":
        raise DeployError(
            f"RimWorld deploy root must be a directory named Mods: {resolved}"
        )
    if not SAFE_MOD_NAME.fullmatch(mod_name):
        raise DeployError(f"Refusing unsafe mod name: {mod_name}")

    destination = resolved / mod_name
    if destination.is_symlink():
        raise DeployError(f"Refusing symlinked mod destination: {destination}")
    if destination.exists() and not destination.is_dir():
        raise DeployError(
            f"Mod destination exists but is not a directory: {destination}"
        )
    destination_resolved = destination.resolve(strict=False)
    if (
        destination_resolved.parent != resolved
        or destination_resolved.name != mod_name
    ):
        raise DeployError(
            f"Mod destination must resolve to a direct child of {resolved}: "
            f"{destination_resolved}"
        )
    return resolved


def lock_path(repo_root: Path, mods_dir: Path, mod_name: str) -> Path:
    digest = hashlib.sha256(f"{mods_dir}\n{mod_name}\n".encode()).hexdigest()
    return repo_root / "obj/deploy-locks" / f"{digest}.lock.dir"


def run_inner(
    mods_dir: Path,
    project: Path,
    configuration: str,
    platform: str,
    mod_name: str,
    build_bridge_tools: str,
) -> None:
    command = [
        "dotnet",
        "msbuild",
        str(project),
        "-nologo",
        "-verbosity:quiet",
        "-consoleloggerparameters:ErrorsOnly",
        "/target:_CopyToRimworldUnlocked",
        f"/property:RIMWORLD_MOD_DIR={mods_dir}",
        f"/property:Configuration={configuration}",
        f"/property:Platform={platform}",
        "/property:RimworldDeployGuardInner=true",
        f"/property:ModFileName={mod_name}",
        f"/property:BuildBridgeTools={build_bridge_tools}",
    ]
    result = subprocess.run(command)
    if result.returncode != 0:
        raise DeployError(
            f"Guarded deployment failed with exit code {result.returncode}"
        )


def deploy(args: argparse.Namespace) -> None:
    global LOCK_DIRECTORY
    resolved = validate_mod_directory(args.mods_dir, args.mod_name)
    project = args.project.resolve(strict=True)
    repo_root = project.parent.parent
    lock = lock_path(repo_root, resolved, args.mod_name)
    lock.parent.mkdir(parents=True, exist_ok=True)
    try:
        lock.mkdir()
    except FileExistsError as exc:
        raise DeployError(f"Another deploy already holds {lock}") from exc
    LOCK_DIRECTORY = lock
    try:
        run_inner(
            resolved,
            project,
            args.configuration,
            args.platform,
            args.mod_name,
            args.build_bridge_tools,
        )
    finally:
        lock.rmdir()
        LOCK_DIRECTORY = None


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser()
    sub = result.add_subparsers(dest="command", required=True)
    validate = sub.add_parser("validate")
    validate.add_argument("mods_dir", type=Path)
    validate.add_argument("project", type=Path)
    validate.add_argument("mod_name")

    deploy_parser = sub.add_parser("deploy")
    deploy_parser.add_argument("mods_dir", type=Path)
    deploy_parser.add_argument("project", type=Path)
    deploy_parser.add_argument("configuration")
    deploy_parser.add_argument("platform")
    deploy_parser.add_argument("mod_name")
    deploy_parser.add_argument("build_bridge_tools")
    return result


def main() -> int:
    args = parser().parse_args()
    try:
        if args.command == "validate":
            validate_mod_directory(args.mods_dir, args.mod_name)
        else:
            deploy(args)
        return 0
    except (DeployError, OSError) as exc:
        print(f"error: {exc}", file=__import__("sys").stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
