# Security policy

## Supported versions

Only the latest release receives security fixes.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub's private reporting instead:
[Report a vulnerability](https://github.com/giovanniluci/explorer-organizer/security/advisories/new).

Include the version, Windows version, steps to reproduce and the impact. You will get a first answer within 7 days.

## Scope

Explorer Organizer runs locally and makes no network connections. Relevant issues include, for example:
path traversal when extracting archives, rules that could delete or overwrite files without going through the
Recycle Bin, unsafe handling of the "Open with" action, or installer/uninstaller problems.

## Verifying downloads

Every release lists the SHA-256 checksum of each file in `SHA256SUMS.txt`. Check it with PowerShell:

```powershell
Get-FileHash .\ExplorerOrganizer-Setup.exe -Algorithm SHA256
```

The executables are currently **not code-signed**, so Windows SmartScreen may show a warning on first launch.
