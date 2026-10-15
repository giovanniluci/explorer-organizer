#Requires -Version 5.1
<#
.SYNOPSIS
    Silent per-user install into a temporary folder, checks, silent uninstall, checks that nothing is left.

.DESCRIPTION
    Installed with the tasks "contextmenu" and "sendto" so their cleanup is verified too. User data in
    %AppData%\ExplorerOrganizer is never touched (a silent uninstall keeps it by design).
    If Explorer Organizer is already installed for this user the script stops: the check would upgrade and then
    remove that installation. Pass -AllowReplaceExisting to proceed anyway (you will have to reinstall it).
#>
param(
    [Parameter(Mandatory)] [string]$Setup,
    [switch]$AllowReplaceExisting
)
$ErrorActionPreference = 'Stop'
$AppId = '{8F3C2A5E-6B1D-4C7E-9A2F-3D5E7B9C1A40}_is1'
$UninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
$MenuKeys = 'HKCU:\Software\Classes\*\shell\ExplorerOrganizer', 'HKCU:\Software\Classes\Directory\shell\ExplorerOrganizer', 'HKCU:\Software\Classes\Directory\Background\shell\ExplorerOrganizer'
$StartLink = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Explorer Organizer.lnk'
$SendToLink = Join-Path $env:APPDATA 'Microsoft\Windows\SendTo\Explorer Organizer.lnk'

$failed = $false
function Check($ok, $text) {
    if ($ok) { Write-Host "  [ok]   $text" -ForegroundColor Green }
    else { Write-Host "  [FAIL] $text" -ForegroundColor Red; $script:failed = $true }
}

if ((Test-Path -LiteralPath $UninstallKey) -or (Test-Path -LiteralPath ('HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $AppId))) {
    if (-not $AllowReplaceExisting) {
        Write-Host 'Explorer Organizer is already installed on this PC: the check would replace and then uninstall it.' -ForegroundColor Yellow
        Write-Host 'Run again with -AllowReplaceExisting, or uninstall it first.' -ForegroundColor Yellow
        exit 2
    }
}

$dir = Join-Path ([IO.Path]::GetTempPath()) ('eo-install-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$log = "$dir.install.log"
Write-Host "Installing into $dir"
$p = Start-Process $Setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', "/DIR=`"$dir`"", '/TASKS="contextmenu,sendto"', "/LOG=`"$log`"" -Wait -PassThru
Check ($p.ExitCode -eq 0) "setup exit code 0 (got $($p.ExitCode))"
$exe = Join-Path $dir 'ExplorerOrganizer.exe'
Check (Test-Path $exe) 'ExplorerOrganizer.exe installed'
Check (Test-Path (Join-Path $dir 'LICENSE.txt')) 'LICENSE.txt installed'
Check (Test-Path (Join-Path $dir 'unins000.exe')) 'uninstaller present'
Check (Test-Path -LiteralPath $UninstallKey) 'registered in Apps & features (per user)'
Check (Test-Path $StartLink) 'Start menu shortcut'
Check (Test-Path $SendToLink) '"Send to" shortcut'
foreach ($k in $MenuKeys) {
    $cmd = (Get-ItemProperty -LiteralPath "$k\command" -ErrorAction SilentlyContinue).'(default)'
    Check ($cmd -like "*$exe*--process*") "context menu verb $($k.Split('\')[-3..-1] -join '\')"
}

# The installed exe starts (isolated data folder, the user's settings are not used)
$data = "$dir.data"
New-Item -ItemType Directory -Force $data | Out-Null
'{"firstRunCompleted":true,"language":"it","theme":"light","startMinimized":false,"showNotifications":false}' | Set-Content -Encoding ascii (Join-Path $data 'settings.json')
$psi = New-Object System.Diagnostics.ProcessStartInfo $exe, '--show'
$psi.UseShellExecute = $false
$psi.EnvironmentVariables['EXPLORER_ORGANIZER_DATA'] = $data
$app = [System.Diagnostics.Process]::Start($psi)
$deadline = (Get-Date).AddSeconds(30)
while ($app.MainWindowHandle -eq [IntPtr]::Zero -and -not $app.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300; $app.Refresh() }
Check ($app.MainWindowHandle -ne [IntPtr]::Zero) "installed app starts and shows '$($app.MainWindowTitle)'"
if (-not $app.HasExited) { $app.Kill(); $app.WaitForExit(5000) | Out-Null }

Write-Host 'Uninstalling'
$u = Start-Process (Join-Path $dir 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru
Check ($u.ExitCode -eq 0) "uninstaller exit code 0 (got $($u.ExitCode))"
# The uninstaller re-launches itself from %TEMP%: wait until it has finished removing files.
$deadline = (Get-Date).AddSeconds(60)
while (((Test-Path $exe) -or (Test-Path -LiteralPath $UninstallKey)) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
Start-Sleep -Seconds 2
Check (-not (Test-Path $exe)) 'program files removed'
Check (-not (Test-Path $dir) -or -not (Get-ChildItem $dir -Recurse -Force)) 'install folder removed or empty'
Check (-not (Test-Path -LiteralPath $UninstallKey)) 'Apps & features entry removed'
Check (-not (Test-Path $StartLink)) 'Start menu shortcut removed'
Check (-not (Test-Path $SendToLink)) '"Send to" shortcut removed'
foreach ($k in $MenuKeys) { Check (-not (Test-Path -LiteralPath $k)) "context menu key removed: $($k.Split('\')[-3..-1] -join '\')" }
$run = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).ExplorerOrganizer
Check (-not $run) 'no autostart entry left'

Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
if ($failed) { Write-Host "Install log: $log"; exit 1 }
Remove-Item $log -Force -ErrorAction SilentlyContinue
Write-Host 'Installer OK: clean install and uninstall'
exit 0
