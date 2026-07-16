# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- Automatic GitHub releases via GitHub Actions.
- Season Strength display in Healing and Tanking meters.
- Player Imagines display in Healing and Tanking meters.
- Death counter in the Tanking meter.
- Separate SubProfession entries for independent color customization.
- `SubProfession_Unknown` color entry.
- VPN setup notes in the README and release notes.
- `TODO.md` to track planned features, improvements, and known tasks.
- **TODO** section in the README linking to `TODO.md`.

### Changed

#### Release & CI
- Refactored the GitHub Actions release workflow.
- Renamed `dotnet.yml` to `release.yml`.
- Improved build, artifact handling, and automatic release generation.

#### Default Settings
- Enabled **Show Season Strength** in meters.
- Enabled **Show Player Imagines** in meters.
- Enabled **Keep past encounter until next damage**.
- Enabled **Show channel line number in status**.
- Enabled **Show call wipe for encounter on main window**.
- Enabled **Show deaths** in the Tanking meter.
- Enabled **BPTimer** integration.
- Enabled **BPTimer field boss HP reports**.
- Disabled **Display active per second values** in meters.
- Enabled **NPC Taken → Show HP data**.
- Enabled **NPC Taken → Hide max HP**.
- Enabled **NPC Taken → Show HP percent bar**.

#### User Interface
- Updated progress bar colors.
- Updated profession and sub-profession colors.
- Fixed progress bars for **Profession_Lucy** and **Profession_Natsu**.
- Improved out-of-box experience with better default configuration.

#### Documentation
- Updated the README prerequisites section.
- Improved setup instructions for new users.
- Added clearer emphasis that **Npcap must be installed before running ZDPS**.
- Updated release notes template.
- Added VPN guidance for users.
- Added and updated project `TODO.md`.

### Removed
- Historical encounter warning message.
- **Viewing Historical Encounter Data** button/warning to reduce UI clutter.

### Merged
- Synced with the latest upstream changes from `Blue-Protocol-Source/master`.
```
