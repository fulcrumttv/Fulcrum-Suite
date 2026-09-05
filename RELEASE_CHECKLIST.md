# Release checklist

Use this checklist for every public Fulcrum Suite beta.

## Code and behavior

- [ ] `VERSION.json` contains the final component versions and `releaseReady`
      is `true`.
- [ ] The STINT late-attach blocker in `DEVELOPMENT.md` is fixed or explicitly
      accepted for the release.
- [ ] Source-derived validation passes.
- [ ] The guarded Windows build passes with the installed SimHub dependencies.
- [ ] Both generated DLLs are tested together in SimHub.
- [ ] Live iRacing checks cover formation, green flag, pits, tow/garage,
      reconnect, multiclass, extended cautions, lapping highlights and radar.

## Package

- [ ] The install ZIP contains only the two DLLs, 12 overlays, English/Spanish
      guides, license/notices, release notes, `README_FIRST.txt` and checksums.
- [ ] No source kits, test files, scripts, diagnostics, backups or older
      overlays are present in the install ZIP.
- [ ] Every packaged filename and SHA-256 value is verified.
- [ ] Installation is tested from a clean extraction of the final ZIP.

## GitHub

- [ ] `CHANGELOG.md`, `NOTICE`, documentation and version labels are final.
- [ ] The release commit is reviewed on a branch before merging into `main`.
- [ ] The annotated tag targets the exact release commit.
- [ ] The GitHub Release is marked as a prerelease while Fulcrum Suite is beta.
- [ ] The install ZIP and corresponding source are available from the release.
