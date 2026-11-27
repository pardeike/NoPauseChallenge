# Build and delivery

Run `./scripts/build-quiet.sh build` for a local Release build of the main mod
and companion. The projects target RimWorld 1.7, .NET 10 and AnyCPU. The pinned
.NET SDK is 10.0.400. Build logs are under `artifacts/logs`. Success prints
exactly `ok`. Failure reports the failed mode and full log path.

`Directory.Build.props` owns the version. Normal builds synchronize both
`About/About.xml` and `About/Manifest.xml`. The wrapper verifies both values.

The projects use Krafs.Rimworld.Ref 1.7.4990-unstable, Lib.Harmony.Ref
3.0.0-rc.4, CrossPromotion 1.1.2 and TaskPubliciser 1.0.3. CrossPromotion only
provides a .NET Framework assembly. Its package-scoped NU1701 suppression
retains the helper pending 1.7 startup verification.

The companion uses published RimBridgeServer.Sdk 2.0.0 from NuGet.org.
No local SDK package or feed is required.
The companion excludes the SDK from runtime output. Other packages also use NuGet.org.

For a verified installation and ZIP without touching a game install:

```sh
export NPC_LEGACY_MOD_ROOT=/path/to/Steam/steamapps/workshop/content/294100/1517419925
./scripts/build-quiet.sh stage
```

Stage mode installs into `artifacts/1.7-port/staging/Mods/NoPauseChallenge`.
Its companion goes into the sibling `BridgeTools/NoPauseChallenge` folder.
The verified public ZIP is also copied to `artifacts/NoPauseChallenge.zip`.

For an authorized deployment, set `RIMWORLD_MOD_DIR` to the selected Mods
folder and run `./scripts/build-quiet.sh deploy`. Keep the same explicit
`NPC_LEGACY_MOD_ROOT`. The guard refuses deployment while RimWorld runs.
`bridge-deploy` rebuilds and installs companion instrumentation against the
existing gameplay DLL.

The published Steam 3.7.2.0 baseline supplies all older and shared files.
Its 1.6 DLL SHA-256 is
`8e52bf0f7248f54984e4c2820cf4cbd5bbac838ba5a9b08b8a42fbae42b3bce4`.
The rebuilt repository DLL is different and must remain untouched.

An existing installation keeps its complete local old/shared payload, even when
its bytes differ from Steam. Deployment verifies that those local files retain
their bytes, inode and mtime. Additional local legacy files are also preserved.
Missing required files, symlinks, wrong mod identity and incompatible legacy
load-folder entries stop deployment before installed files change. A fresh
installation initializes its older payload from the published baseline.

The public ZIP always uses the separately pinned Steam baseline in
`scripts/published-legacy.sha256.json`. It does not use the installed local
legacy files. The wrapper verifies that installed 1.7 files match ZIP 1.7 files
and that ZIP legacy files match the published baseline. The version metadata
and load-folder definition are the explicit shared exceptions. The 1.7 load
entry uses only `1.7/`. Public ZIPs exclude companions, SDK DLLs, private
Harmony, debug symbols and Finder files.
