# Contributing to Explorer Organizer

Thanks for your interest! Bug reports, rule ideas, translations and pull requests are all welcome.

## Ground rules

- **Offline by design.** No network calls, accounts, cloud sync or telemetry.
- **Never lose a file silently.** Deletions and replacements go to the Recycle Bin and every run stays undoable.
- **Small, focused pull requests** are reviewed fastest. For larger changes, open an issue first.

## Getting started

Requirements: Windows 10/11 x64, PowerShell 5.1+. The .NET 8 SDK and Inno Setup 6 can be installed locally
into `.tools\` (no system changes):

```powershell
git clone https://github.com/giovanniluci/explorer-organizer.git
cd explorer-organizer
powershell -ExecutionPolicy Bypass -File .\build.ps1 -BootstrapTools
```

Faster loops:

```powershell
dotnet test tests\ExplorerOrganizer.Tests          # engine (no UI)
dotnet test tests\ExplorerOrganizer.UiTests        # UI automation, needs an interactive desktop
.\build.ps1 -SkipUiTests -SkipInstaller            # exe + portable zip only
```

## Project layout

| Path | Content |
|---|---|
| `src/ExplorerOrganizer.Core` | Rule engine, conditions, actions, tokens, undo journal (no UI) |
| `src/ExplorerOrganizer.App` | WPF app (views, windows, tray, shell integration) |
| `tests/` | xUnit tests for the engine and the UI |
| `installer/setup.iss` | Inno Setup script |
| `docs/` | Feature matrix, test matrix, screenshots |
| `assets/` | Logo (SVG/PNG/ICO) and social preview |

## Code style

- Follow the style of the surrounding code (C# 12, nullable enabled).
- Add or update a test for every engine change; see [docs/test-matrix.md](docs/test-matrix.md).
- User-visible strings live in `UiStrings.cs` / `CoreStrings.cs`: add both Italian and English.

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/): `feat: add {week} token`, `fix: keep both files on conflict`, `docs: …`.

## Releasing (maintainers)

1. Update `Version` in `Directory.Build.props` and move "Unreleased" in `CHANGELOG.md` under the new version.
2. Tag and push: `git tag v1.2.3` then `git push origin v1.2.3`.
3. The **Release** workflow runs the tests, builds the installer and the portable zip and publishes them with SHA-256 checksums.
