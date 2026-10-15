#Requires -Version 5.1
<#
.SYNOPSIS
    Creates an isolated demo data folder (settings + one profile + a "Downloads" folder with sample files) for
    screenshots and manual checks. Point the app at it with $env:EXPLORER_ORGANIZER_DATA.
#>
param(
    [Parameter(Mandatory)] [string]$DataDir,
    [ValidateSet('it', 'en')] [string]$Language = 'it',
    [ValidateSet('light', 'dark', 'system')] [string]$Theme = 'light'
)
$ErrorActionPreference = 'Stop'
if (Test-Path $DataDir) { Remove-Item $DataDir -Recurse -Force }
$demo = Join-Path $DataDir 'demo-downloads'
New-Item -ItemType Directory -Force $demo | Out-Null
foreach ($f in 'foto vacanze.jpg', 'scansione.png', 'fattura 2026-10.pdf', 'contratto.docx', 'report.xlsx',
               'setup-tool.exe', 'archivio.zip', 'brano.mp3', 'video.mp4', 'note.txt', 'Città di notte.jpg', 'preventivo.pdf') {
    Set-Content -Path (Join-Path $demo $f) -Value 'demo' -Encoding ascii
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
$settings = @{ firstRunCompleted = $true; language = $Language; theme = $Theme; startMinimized = $false; showNotifications = $false } | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $DataDir 'settings.json'), $settings, $utf8)

$id = [guid]::NewGuid().ToString()
$demoJson = ($demo -replace '\\', '\\')
$profiles = @"
{
  "schemaVersion": 1,
  "activeProfileId": "$id",
  "profiles": [ {
    "id": "$id", "name": "Download", "enabled": true,
    "rules": [
      { "name": "Immagini nella cartella Immagini", "enabled": true,
        "condition": { "type": "group", "operator": "all", "children": [ { "type": "fileType", "category": "image" } ] },
        "actions": [ { "type": "move", "destination": "{folder}\\{category}" } ] },
      { "name": "Documenti per anno", "enabled": true,
        "condition": { "type": "group", "operator": "any", "children": [ { "type": "extension", "extensions": "pdf;docx;xlsx" } ] },
        "actions": [ { "type": "move", "destination": "{folder}\\Documenti\\{modified:yyyy}" } ] },
      { "name": "Archivi e installer", "enabled": true,
        "condition": { "type": "group", "operator": "any", "children": [ { "type": "fileType", "category": "archive" }, { "type": "fileType", "category": "executable" } ] },
        "actions": [ { "type": "sort", "baseFolder": "{folder}", "mode": "byType" } ] },
      { "name": "Media per tipo", "enabled": false,
        "condition": { "type": "group", "operator": "any", "children": [ { "type": "fileType", "category": "video" }, { "type": "fileType", "category": "audio" } ] },
        "actions": [ { "type": "sort", "baseFolder": "{folder}", "mode": "byType" } ] }
    ],
    "folders": [ { "path": "$demoJson", "enabled": true, "includeSubfolders": false, "trigger": "manual" } ]
  } ]
}
"@
[IO.File]::WriteAllText((Join-Path $DataDir 'profiles.json'), $profiles, $utf8)
Write-Host "demo data ready in $DataDir"
