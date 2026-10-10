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
from pathlib import Path

root = Path(sys.argv[1]).parent
mode = sys.argv[2] if len(sys.argv) == 3 else "build" if len(sys.argv) == 2 else ""
if mode not in ("build", "deploy", "bridge-deploy"):
    raise SystemExit("Use scripts/build-quiet.sh [build|deploy|bridge-deploy]")
project = root / "Source/NoPauseChallenge.csproj"
mods = os.environ.get("RIMWORLD_MOD_DIR", "")
if mode != "build" and not mods:
    raise SystemExit("Set RIMWORLD_MOD_DIR to the destination Mods directory")
logs = root / "artifacts/logs"
logs.mkdir(parents=True, exist_ok=True)
log = logs / (mode + ".log")
command = ["dotnet", "build", str(project), "-c", "Release", "--no-incremental", "--nologo", "--verbosity", "quiet"]
if mode == "deploy":
    command.append("-p:RIMWORLD_MOD_DIR=" + mods)
elif mode == "bridge-deploy":
    command = [sys.executable, str(root / "scripts/rimworld_deploy_guard.py"), "deploy", mods, str(project), "Release", "AnyCPU", "NoPauseChallenge", "true"]
process = None
environment = os.environ.copy()
environment.pop("RIMWORLD_MOD_DIR", None)
try:
    with log.open("w") as output:
        process = subprocess.Popen(command, cwd=root, env=environment, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
        if process.wait():
            raise RuntimeError("Build/deploy command failed")
    if mode != "build":
        pairs = [
            (root / "1.6/Assemblies/NoPauseChallenge.dll", Path(mods) / "NoPauseChallenge/1.6/Assemblies/NoPauseChallenge.dll"),
            (root / "artifacts/BridgeTools/NoPauseChallenge/NoPauseChallenge.BridgeTools.dll", Path(mods).parent / "BridgeTools/NoPauseChallenge/NoPauseChallenge.BridgeTools.dll")
        ]
        for source, destination in pairs:
            if hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(destination.read_bytes()).digest():
                raise RuntimeError("Deployed DLL differs from the built DLL: " + destination.name)
    print("ok")
except BaseException as error:
    if process and process.poll() is None:
        os.killpg(process.pid, signal.SIGTERM)
        process.wait()
    print("Failed " + mode + ": " + str(error) + ". Full log: " + str(log))
    if log.exists():
        lines = log.read_text(errors="replace").splitlines()
        diagnostics = [line for line in lines if "error" in line.lower() or "failed" in line.lower()]
        print("\n".join((diagnostics or lines[-8:])[-8:]))
    raise SystemExit(1)
PY
