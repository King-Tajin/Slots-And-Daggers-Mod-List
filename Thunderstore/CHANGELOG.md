# Changelog

## 0.5.0

- Prepared for Thunderstore release.
- Created a build script for Thunderstore releases.

## 0.5.1

### Added
- Settings marked `ReadOnly` by their mod now show as disabled with no Reset button
- Mods that failed to load are labeled "Not loaded" instead of "Disabled"

### Changed
- Settings are no longer saved twice per change, and text fields only save when the value actually changed
- The mod list panel resizes if the screen size or aspect ratio changes between openings

### Fixed
- The Mods button no longer inherits the Progress button's tooltip
- Scroll momentum no longer carries over when switching between the list and a mod's settings
- Mod names, descriptions, and sections containing `</noparse>` in any letter case can no longer break the text formatting
- The `DumpCreditsHierarchy` option now takes effect without a restart, and is only included in Debug builds

## 0.5.2

### Added
- Number fields for settings with a minimum and maximum now block typing values above the maximum, and block minus signs, decimal points, and other characters the setting can't use
- Settings with a minimum and maximum now show their range

### Changed
- Values below the minimum, or above the maximum after deleting characters, are snapped to the nearest allowed value when you finish editing
