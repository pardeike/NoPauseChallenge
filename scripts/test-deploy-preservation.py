#!/usr/bin/env python3
"""Verify local preservation and separate published ZIP bytes through normal deploy."""

from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile
from legacy_payload import legacy_files, MUTABLE

root = Path(__file__).resolve().parent.parent
baseline = Path(sys.argv[1]).resolve(strict=True)
staged = root / "artifacts/1.7-port/staging/Mods/NoPauseChallenge"
log = root / "artifacts/logs/deploy-preservation.log"


def snapshot(folder, legacy_only=False):
    result = {}
    for path in folder.rglob("*"):
        name = path.relative_to(folder).as_posix()
        if legacy_only and (name in MUTABLE or name == "About" or path.relative_to(folder).parts[0] == "1.7"):
            continue
        stat = path.lstat()
        contents = os.readlink(path) if path.is_symlink() else hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
        result[name] = stat.st_ino, stat.st_mtime_ns, contents
    return result


with tempfile.TemporaryDirectory(prefix="deploy-preservation-", dir=root / "artifacts") as temp:
    directory = Path(temp)
    mods = directory / "Mods"
    installed = mods / "NoPauseChallenge"
    shutil.copytree(staged, installed)
    local_dll = installed / "1.6/Assemblies/NoPauseChallenge.dll"
    shutil.copy2(root / "1.6/Assemblies/NoPauseChallenge.dll", local_dll)
    artwork = installed / "About/Preview.png"
    artwork.write_bytes(artwork.read_bytes() + b"local artwork")
    (installed / "local-notes.txt").write_text("Unrelated state must survive.\n")
    (installed / "1.6/local-extra.txt").write_text("Local legacy additions must survive.\n")
    frozen = snapshot(installed, legacy_only=True)
    environment = {**os.environ, "NPC_LEGACY_MOD_ROOT": str(baseline), "RIMWORLD_MOD_DIR": str(mods)}
    command = [str(root / "scripts/build-quiet.sh"), "deploy"]

    def run(env=environment):
        with log.open("a") as output:
            return subprocess.run(command, cwd=root, env=env, stdout=output, stderr=subprocess.STDOUT).returncode

    log.write_text("")
    assert run() == 0, f"Differing-local deployment failed. Read {log}"
    shutil.copy2(root / "artifacts/logs/deploy.log", root / "artifacts/logs/deploy-differing-local.log")
    assert snapshot(installed, legacy_only=True) == frozen, "Existing legacy inode, mtime or bytes changed"
    with zipfile.ZipFile(mods / "NoPauseChallenge.zip") as archive:
        for name, source in legacy_files(baseline).items():
            assert archive.read(name) == source.read_bytes(), name
        assert archive.read("1.6/Assemblies/NoPauseChallenge.dll") != local_dll.read_bytes()
        assert archive.read("About/Preview.png") != artwork.read_bytes()
        public_17 = {name for name in archive.namelist() if name.startswith("1.7/")}
        installed_17 = {p.relative_to(installed).as_posix() for p in (installed / "1.7").rglob("*") if p.is_file()}
        assert installed_17 == public_17
        for name in public_17:
            assert archive.read(name) == (installed / name).read_bytes(), name
        assert "1.6/local-extra.txt" not in archive.namelist()
        assert "local-notes.txt" not in archive.namelist()

    local_dll.unlink()
    before = snapshot(directory)
    assert run() != 0, "Missing local legacy DLL was accepted"
    assert snapshot(directory) == before, "Missing-file rejection changed installed state"
    shutil.copy2(root / "1.6/Assemblies/NoPauseChallenge.dll", local_dll)

    assemblies = installed / "1.7/Assemblies"
    shutil.rmtree(assemblies)
    outside = directory / "outside"
    outside.mkdir()
    assemblies.symlink_to(outside, target_is_directory=True)
    before = snapshot(directory)
    assert run() != 0, "Unsafe 1.7 destination symlink was accepted"
    assert snapshot(directory) == before, "Unsafe-path rejection changed installed state"
    assemblies.unlink()
    shutil.copytree(staged / "1.7/Assemblies", assemblies)

    wrong_baseline = directory / "wrong-public-baseline"
    shutil.copytree(baseline, wrong_baseline)
    wrong = wrong_baseline / "Defs/KeyBindings.xml"
    wrong.write_bytes(wrong.read_bytes() + b" ")
    before = snapshot(directory)
    assert run({**environment, "NPC_LEGACY_MOD_ROOT": str(wrong_baseline)}) != 0, "Unpinned public baseline was accepted"
    assert snapshot(directory) == before, "Public-baseline rejection changed installed state"
    evidence = {"existingLegacyFilesPreserved": sum(state[2] is not None for state in frozen.values()),
                "localDllAndArtworkDifferFromPublished": True,
                "bytesInodesAndMtimesPreserved": True, "additionalLegacyFilesPreserved": True,
                "installed17MatchesZip17": True, "zipLegacyMatchesPinnedPublishedBaseline": True,
                "missingLegacyRejectedBeforeWrites": True, "unsafeDestinationRejectedBeforeWrites": True,
                "changedPublicBaselineRejectedBeforeWrites": True}
    (root / "artifacts/1.7-port/deploy-preservation.json").write_text(json.dumps(evidence, indent=2) + "\n")
print("ok")
