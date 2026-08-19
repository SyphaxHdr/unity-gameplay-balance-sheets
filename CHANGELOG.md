# Changelog

All notable changes to this package will be documented in this file.

## [0.3.0]

### Added
- Added ScriptableObject assets as balance sheet targets.
- Added Editor tests for ScriptableObject scanning, apply/refresh round trips, and GameObject scan regression coverage.

### Changed
- Generalized target selection, grouping, scanning, refresh, apply, save, and ping workflows to support both GameObjects and ScriptableObjects.
- Preserved existing `targetRoot` data through Unity serialization migration and a source-compatible GameObject accessor.
- Updated the in-editor tutorial and English/French documentation for both target types.
- Fixed the English documentation link in package metadata and the README.

## [0.2.0]

### Added
- Added reference sheet and designer variant workflow.
- Added Sheet Application page.
- Added target-based application hierarchy.
- Added RPG-style gameplay balance sheet concept.
- Added French and English user guides.
- Added in-editor tutorial page.
- Added settings page with local EditorPrefs preferences.
- Added designer variant creation and deletion workflow.
- Added option to lock reference sheets for designers.
- Added application workflow to promote a validated sheet as a new reference.

### Changed
- Reworked Designer page around reference sheets and designer variants.
- Reworked Developer page to focus on reference sheet creation and configuration.
- Reordered main tabs to: Designer, Sheet Application, Developer, Tutorial, Settings.
- Improved status labels: Reference, Pending, Modified, Missing.
- Improved documentation and README.

### Notes
- Missing fields do not block sheet application. Valid fields can still be applied.
- Editor preferences are stored locally with Unity EditorPrefs and do not need to be added to .gitignore.

## [0.1.0]

### Added
- Initial package structure.
- Basic runtime balance sheet profile.
- Basic editor window.
- Initial scan and apply workflow.
