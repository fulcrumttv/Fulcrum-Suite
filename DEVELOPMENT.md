# Fulcrum Suite development

This repository is the canonical development location for Fulcrum Suite.
Published beta history remains available through Git tags and GitHub Releases;
the working tree may contain a newer, unreleased test candidate.

## Current snapshot

- Base public release: `v1.0.0-beta.2`
- Plugin: `4.1.57`
- Core: `4.1.42`
- Relatives: `6.6.98`
- Radar: `0.8.2`
- Pit Assistant: `0.1.33`
- Status: unreleased test candidate

`VERSION.json` is the machine-readable source of truth for component versions.

## Repository layout

- `Fulcrum.Core/`: telemetry and race-state logic
- `Fulcrum.Plugin/`: SimHub plugin, publishers and settings UI
- `Overlays/`: the complete candidate set of 12 importable dashboards
- `Tests/`: native C# regression and integration harnesses
- `VALIDACION/`: source-derived Linux/Node regression harnesses
- `BUILD_FULCRUM_*.bat`: guarded Windows build entry point
- `Build-Fulcrum-*.ps1`: native build and test implementation

Compiled DLLs, PDBs and release ZIP files are intentionally not committed.
They are build outputs or GitHub Release assets.

## Fast source-derived validation

Requirements: Node.js and an `unzip` executable.

```bash
node VALIDACION/simulate_start_grid_triplicate.mjs
node VALIDACION/test_class_positions.mjs
```

The first command is an independent randomized model. The second command reads
the production C# control flow and the actual Relatives dashboard. It is a
regression gate, but it is not a native C# build and does not replace a live
SimHub/iRacing test.

## Native Windows validation

On a Windows machine with SimHub and .NET Framework 4.8 installed, run:

```text
BUILD_FULCRUM_v4.1.57_START_GRID_RECOVERY.bat
```

The build stops before producing distribution DLLs if the Core regressions or
the Relative module/publisher integration tests fail. Set `SIMHUB_PATH` if
SimHub is installed outside its normal Program Files directory.

## Release gate

The current candidate must not be tagged as beta 3 yet. A fresh late attach to
a long-running endurance race can initialize the displayed stint lap from the
session lap (for example, show `L100`) until a newly observed pit cycle gives
the tracker a trustworthy local stint anchor. This does not affect the restored
per-class starting reference used by `+/-`, but it remains an open STINT issue.

Complete `RELEASE_CHECKLIST.md` before merging a release commit and creating a
tag.
