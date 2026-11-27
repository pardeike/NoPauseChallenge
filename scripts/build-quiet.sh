#!/bin/sh
# Quiet build/deploy entry point. Deployment uses the existing guarded MSBuild path.
set -eu
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec python3 - "$script_dir" "$@" <<'PY'
import hashlib
import os
import signal
import subprocess
import sys
import shutil
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

root = Path(sys.argv[1]).parent
sys.path.insert(0, str(root / "scripts"))
from legacy_payload import check, public_files, verify_package, installed_legacy_snapshot, MUTABLE, ignored

if len(sys.argv) == 3:
    mode = sys.argv[2]
elif len(sys.argv) == 2:
    mode = "deploy" if os.environ.get("RIMWORLD_MOD_DIR") else "build"
else:
    mode = ""
if mode not in ("build", "stage", "deploy", "bridge-deploy"):
    raise SystemExit("Use scripts/build-quiet.sh [build|stage|deploy|bridge-deploy]")
project = root / "Source/NoPauseChallenge.csproj"
mods = os.environ.get("RIMWORLD_MOD_DIR", "")
if mode == "stage":
    mods = str(root / "artifacts/1.7-port/staging/Mods")
    Path(mods).mkdir(parents=True, exist_ok=True)
if mode != "build" and not mods:
    raise SystemExit("Set RIMWORLD_MOD_DIR to the destination Mods directory")
logs = root / "artifacts/logs"
logs.mkdir(parents=True, exist_ok=True)
log = logs / (mode + ".log")
command = ["dotnet", "build", str(project), "-c", "Release", "-p:Platform=AnyCPU", "--no-incremental", "--nologo", "--verbosity", "quiet"]
if mode in ("deploy", "stage"):
    command.append("-p:RIMWORLD_MOD_DIR=" + mods)
elif mode == "bridge-deploy":
    command = [sys.executable, str(root / "scripts/rimworld_deploy_guard.py"), "deploy", mods, str(project), "Release", "AnyCPU", "NoPauseChallenge", "true"]
process = None
environment = os.environ.copy()
environment.pop("RIMWORLD_MOD_DIR", None)


def interrupted(signum, frame):
    raise KeyboardInterrupt("Cancelled")


signal.signal(signal.SIGTERM, interrupted)
try:
    log.write_text("")
    legacy_before = None
    if mode != "build":
        if not environment.get("NPC_LEGACY_MOD_ROOT"):
            raise RuntimeError("Set NPC_LEGACY_MOD_ROOT to the pinned published Steam payload")
        baseline = Path(environment["NPC_LEGACY_MOD_ROOT"])
        installed = Path(mods) / "NoPauseChallenge"
        check(baseline, installed)
        if installed.exists():
            legacy_before = installed_legacy_snapshot(installed)
    with log.open("w") as output:
        process = subprocess.Popen(command, cwd=root, env=environment, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
        if process.wait():
            raise RuntimeError("Build/deploy command failed")
    version = ET.parse(root / "Directory.Build.props").findtext(".//ModVersion")
    if ET.parse(root / "About/About.xml").findtext("modVersion") != version or ET.parse(root / "About/Manifest.xml").findtext("version") != version:
        raise RuntimeError("Automatic metadata synchronization failed")
    if mode != "build":
        pairs = [
            (root / "1.7/Assemblies/NoPauseChallenge.dll", Path(mods) / "NoPauseChallenge/1.7/Assemblies/NoPauseChallenge.dll"),
        ]
        if environment.get("BuildBridgeTools", "true").lower() != "false":
            pairs.append((root / "artifacts/BridgeTools/NoPauseChallenge/NoPauseChallenge.BridgeTools.dll", Path(mods).parent / "BridgeTools/NoPauseChallenge/NoPauseChallenge.BridgeTools.dll"))
        for source, destination in pairs:
            if hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(destination.read_bytes()).digest():
                raise RuntimeError("Deployed DLL differs from the built DLL: " + destination.name)
        check(baseline, installed)
        if legacy_before is not None and installed_legacy_snapshot(installed) != legacy_before:
            raise RuntimeError("Installed legacy bytes, inode or mtime changed during deployment")
        for name, source in public_files(baseline).items():
            if (name.startswith("1.7/") or name in MUTABLE) and source.read_bytes() != (installed / name).read_bytes():
                raise RuntimeError("Installed payload differs from source: " + name)
        if mode in ("deploy", "stage"):
            package = Path(mods) / "NoPauseChallenge.zip"
            verify_package(baseline, package)
            with zipfile.ZipFile(package) as archive:
                public_17 = {name for name in archive.namelist() if name.startswith("1.7/")}
                installed_17 = {p.relative_to(installed).as_posix() for p in (installed / "1.7").rglob("*") if p.is_file() and not ignored(p)}
                if installed_17 != public_17:
                    raise RuntimeError("Installed 1.7 file set differs from the public ZIP")
            shutil.copy2(package, root / "artifacts/NoPauseChallenge.zip")
    print("ok")
except BaseException as error:
    if process and process.poll() is None:
        os.killpg(process.pid, signal.SIGTERM)
        process.wait()
    with log.open("a") as output:
        output.write(str(error) + "\n")
    print("Failed " + mode + ": " + str(error) + ". Full log: " + str(log))
    if log.exists():
        lines = log.read_text(errors="replace").splitlines()
        diagnostics = [line for line in lines if "error" in line.lower() or "failed" in line.lower()]
        print("\n".join((diagnostics or lines[-8:])[-8:]))
    raise SystemExit(1)
PY
