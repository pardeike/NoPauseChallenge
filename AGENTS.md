# No Pause Challenge

Read ../steam-mods/AGENTS.md for shared development and testing guidance.
Use `./scripts/build-quiet.sh build` for a fresh Release gameplay/companion build.
Set `RIMWORLD_MOD_DIR` explicitly and use `./scripts/build-quiet.sh deploy` to
build, deploy through the existing guard, and verify both DLLs. Use
`bridge-deploy` to rebuild/deploy companion instrumentation against the existing
gameplay DLL. Full output goes to ignored artifacts/logs; success prints `ok`.
Preserve frozen historical version folders and tracked release DLLs during
ordinary source commits. Keep companion instrumentation out of player ZIPs.
