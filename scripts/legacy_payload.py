#!/usr/bin/env python3
"""Preserve local legacy files and package published legacy with isolated 1.7."""

from pathlib import Path
import hashlib
import json
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parent.parent
MUTABLE = {"About/About.xml", "About/Manifest.xml", "LoadFolders.xml"}


def ignored(path):
    return path.name == ".DS_Store" or path.name.startswith("._") or path.suffix in {".pdb", ".mdb"}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def legacy_files(baseline):
    baseline = baseline.resolve(strict=True)
    if baseline == ROOT:
        raise ValueError("Use the published Steam baseline, not the working checkout")
    if ET.parse(baseline / "About/About.xml").findtext("packageId") != "brrainz.nopausechallenge":
        raise ValueError("The legacy baseline is not No Pause Challenge")
    old_load = ET.parse(baseline / "LoadFolders.xml")
    new_load = ET.parse(ROOT / "LoadFolders.xml")
    for entry in old_load.getroot():
        if entry.tag == "v1.7":
            continue
        if [x.text for x in entry] != [x.text for x in new_load.findall(entry.tag + "/li")]:
            raise ValueError(f"Legacy load-folder order changed for {entry.tag}")
    result = {p.relative_to(baseline).as_posix(): p for p in baseline.rglob("*")
            if p.is_file() and not ignored(p) and p.relative_to(baseline).parts[0] != "1.7"
            and p.relative_to(baseline).as_posix() not in MUTABLE}
    pinned = json.loads((ROOT / "scripts/published-legacy.sha256.json").read_text())
    if result.keys() != pinned.keys() or any(digest(path) != pinned[name] for name, path in result.items()):
        raise ValueError("The public baseline differs from pinned published Steam 3.7.2.0")
    return result


def check(baseline, destination):
    expected = legacy_files(baseline)
    if destination.is_symlink() or destination.resolve().is_relative_to(baseline.resolve()) or baseline.resolve().is_relative_to(destination.resolve()):
        raise ValueError("Refusing to replace a symlink or the published baseline")
    if not destination.exists():
        return expected
    if not destination.is_dir():
        raise ValueError("Installed mod destination is not a directory")
    for p in destination.rglob("*"):
        if p.is_symlink() or not (p.is_file() or p.is_dir()):
            raise ValueError(f"Unsafe installed path: {p}")
    for name in expected:
        if not (destination / name).is_file():
            raise ValueError(f"Installed legacy file is missing: {name}")
    if ET.parse(destination / "About/About.xml").findtext("packageId") != "brrainz.nopausechallenge":
        raise ValueError("Installed mod identity is not No Pause Challenge")
    installed_load = ET.parse(destination / "LoadFolders.xml")
    source_load = ET.parse(ROOT / "LoadFolders.xml")
    installed_entries = {entry.tag: [x.text for x in entry] for entry in installed_load.getroot() if entry.tag != "v1.7"}
    source_entries = {entry.tag: [x.text for x in entry] for entry in source_load.getroot() if entry.tag != "v1.7"}
    if installed_entries != source_entries:
        raise ValueError("Installed legacy load-folder entries differ from the supported layout")
    return expected


def installed_legacy_snapshot(destination):
    result = {}
    if not destination.exists():
        return result
    for path in destination.rglob("*"):
        name = path.relative_to(destination).as_posix()
        if name in MUTABLE or path.relative_to(destination).parts[0] == "1.7" or name == "About":
            continue
        stat = path.stat()
        result[name] = (stat.st_ino, stat.st_mtime_ns, digest(path) if path.is_file() else None)
    return result


def install(baseline, destination):
    expected = check(baseline, destination)
    if destination.exists():
        return
    for name, source in expected.items():
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)


def public_files(baseline):
    result = legacy_files(baseline)
    for folder in ("1.7",):
        result.update({p.relative_to(ROOT).as_posix(): p for p in (ROOT / folder).rglob("*") if p.is_file() and not ignored(p)})
    for name in ("About/About.xml", "About/Manifest.xml"):
        result[name] = ROOT / name
    result["LoadFolders.xml"] = ROOT / "LoadFolders.xml"
    for name in result:
        p = Path(name)
        if (p.name == "0Harmony.dll" or p.name.startswith("RimBridgeServer") or "BridgeTools" in p.parts
                or p.name.endswith(".deps.json")):
            raise ValueError(f"Development file in public payload: {name}")
    return result


def verify_package(baseline, package):
    expected = public_files(baseline)
    with zipfile.ZipFile(package) as archive:
        if set(archive.namelist()) != set(expected):
            raise ValueError("ZIP file set differs from the preserved legacy and current 1.7 payloads")
        for name, source in expected.items():
            if archive.read(name) != source.read_bytes():
                raise ValueError(f"ZIP bytes differ from source: {name}")


def package(baseline, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as archive:
        for name, source in sorted(public_files(baseline).items()):
            archive.write(source, name)
    verify_package(baseline, destination)


if __name__ == "__main__":
    try:
        action, baseline, destination = sys.argv[1:]
        {"install": install, "check": check, "package": package}[action](Path(baseline), Path(destination))
    except (OSError, ValueError, KeyError, ET.ParseError) as error:
        print(f"error : {error}", file=sys.stderr)
        raise SystemExit(2)
