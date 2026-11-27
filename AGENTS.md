# No Pause Challenge

Read ../steam-mods/AGENTS.md for shared development and testing guidance.
Use `./scripts/build-quiet.sh build` for a fresh Release gameplay/companion build.
With `RIMWORLD_MOD_DIR` set, `./scripts/build-quiet.sh` builds, installs and verifies the ZIP.
Set `RIMWORLD_MOD_DIR` explicitly and use `./scripts/build-quiet.sh deploy` to
build, deploy through the existing guard, and verify both DLLs. Use
`bridge-deploy` to rebuild/deploy companion instrumentation against the existing
gameplay DLL. Full output goes to ignored artifacts/logs; success prints `ok`.
Preserve frozen historical version folders and tracked release DLLs during
ordinary source commits. Keep companion instrumentation out of player ZIPs.

Current development targets RimWorld 1.7, .NET 10, and AnyCPU.
`Directory.Build.props` owns the mod version. Every normal build synchronizes
`About/About.xml` and `About/Manifest.xml` and verifies their values.
Never rebuild or edit the frozen root `Assemblies`, `Defs`, `Textures`, or
1.2 through 1.6 payloads during 1.7 work.
Packaging and installation require `NPC_LEGACY_MOD_ROOT` pointing at the exact
published Steam 1.6 payload. The repository DLL differs. Do not substitute it.
Deployment must preserve every existing old/shared file, including locally
modified files. Verify their bytes, inode and mtime remain unchanged. Validate
required files and reject unsafe paths before writes. Public ZIPs use only the
separately pinned published baseline in `scripts/published-legacy.sha256.json`.
Only the current 1.7 payload, About.xml, Manifest.xml and LoadFolders.xml may
be updated.
Use `./scripts/build-quiet.sh stage` for an isolated verified build/install/ZIP.
Stage mode writes only under this repository's ignored artifacts directory.
Read `scripts/README.md` for dependency feeds and exact commands.
