# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0] - 2026-10-15

First public release.

### Added
- IF … THEN rule engine with nested ALL/ANY groups and NOT on every condition.
- Conditions: name/path (wildcards, regex), extension, file type, size, dates (incl. EXIF date taken), attributes,
  metadata (EXIF, ID3, document properties, any Windows `System.*` property), text content (txt, Office, ODT, RTF, text PDFs).
- Actions: move, copy, rename, sort into subfolders, delete (Recycle Bin), zip, unzip, create shortcut, open with.
- Rename/path tokens (`{name}`, `{taken:yyyy/MM}`, `{id3:Artist}`, `{match:1}`, `{hash:sha256}`, …) with filters.
- Triggers: drag & drop on the window or a floating drop target, Explorer context menu, Send To, real-time folder
  monitoring, interval and daily schedules, command line.
- Preview before applying, simulation mode for automatic rules, persistent undo of the last 50 runs.
- Profiles with JSON import/export, ready-made presets (Downloads, Photos, Documents, Music), first-run wizard.
- Activity log with CSV/TXT export, statistics, tray icon, start with Windows.
- Fluent-style UI with Mica on Windows 11, light/dark/high-contrast themes, Italian and English.
- Per-user installer (no admin rights) and portable edition; self-contained, no .NET install needed.

[Unreleased]: https://github.com/giovanniluci/explorer-organizer/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/giovanniluci/explorer-organizer/releases/tag/v1.0.0
