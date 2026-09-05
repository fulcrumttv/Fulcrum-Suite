# Changelog

All notable changes to Fulcrum Suite will be documented in this file.

## [Unreleased]

### Updated

- Development snapshot: Plugin v4.1.57 and Core v4.1.42.
- Fulcrum Relatives: v6.6.98.
- Fulcrum Radar: v0.8.2.
- Fulcrum Pit Assistant: v0.1.33.

### Fixed

- Reworked Radar side detection and spotlight tracking so the indicator stays
  tied to a genuinely parallel car.
- Limited Pit Assistant visibility to valid pit-entry and pit-lane contexts.
- Corrected Relatives class positions through pits, tow, garage states and
  extended cautions.
- Restored per-class `+/-` updates in offline/AI and other active sessions.
- Added recovery of the original per-class starting reference after a mid-race
  SimHub restart when iRacing exposes current-session qualifying metadata.
- Corrected red/blue lapping highlights and separated them from gray pit/tow/
  garage priority.
- Corrected race-start and pit-exit stint status behavior for every driver.

### Validation status

- Source-derived Relatives regressions and the independent start-grid model
  pass locally and run automatically in GitHub Actions.
- Native Windows compilation and live SimHub/iRacing testing remain required.
- A late attach to a long-running endurance race still needs a trustworthy
  STINT anchor; this is a release blocker documented in `DEVELOPMENT.md`.

## [v1.0.0-beta.2] - 2026-08-30

### Fixed

- Relatives now detects and displays Slow Down penalties for other drivers using iRacing per-car session flags.
- Fixed LAST DELTA remaining at `0.000` after valid completed laps.
- Fixed the DigiFlags endurance-style triple flasher animation.
- Fixed DigiFlags so the flasher repeats while the control is held.
- Fixed Enable DigiFlags so disabling it hides the complete display instead of only the panel borders.
- Corrected DigiFlags version metadata.
- Increased the usable horizontal renderer width of Relatives for heavily customized column layouts.

### Updated

- Fulcrum Plugin: v4.1.33
- Fulcrum Relatives: v6.6.90
- Fulcrum Delta: v0.8.6.2
- Fulcrum DigiFlags: v0.2.4.12

### Unchanged

All other overlays remain unchanged from v1.0.0-beta.1.


## [v1.0.0-beta.1] - 2026-08-29

### Added

- First public beta release of Fulcrum Suite.
- Fulcrum Conditions
- Fulcrum Delta
- Fulcrum DigiFlags
- Fulcrum Pit Assistant
- Fulcrum Radar
- Fulcrum Rejoin Assistant
- Fulcrum Relatives
- Fulcrum Sectors Pop-Up
- Fulcrum Sectors Table
- Fulcrum Strategy Engineer
- Fulcrum Systems
- Fulcrum Wind
- Global timing reference selection:
  - MY BEST LAP
  - CLASS SESSION BEST
- Class-aware timing and multiclass support.
- Configurable DigiFlags, Relatives and Radar options.
- English and Spanish documentation.
- Public issue templates for bugs, feature requests and compatibility problems.

### Notes

This is the first public beta release.

Feedback, bug reports and compatibility reports are welcome through GitHub Issues.
