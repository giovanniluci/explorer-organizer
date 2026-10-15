#Requires -Version 5.1
<#
.SYNOPSIS
    Checks the portable zip: expected files, the exe starts without installation from a temporary folder, shows
    its window, and keeps its data in the "data" folder next to the exe (not in %AppData%).
#>
param([Parameter(Mandatory)] [string]$Zip)
$ErrorActionPreference = 'Stop'
function Check($ok, $text) {
    if ($ok) { Write-Host "  [ok]   $text" -ForegroundColor Green }
    else { Write-Host "  [FAIL] $text" -ForegroundColor Red; $script:failed = $true }
}
$failed = $false
$dir = Join-Path ([IO.Path]::GetTempPath()) ('eo-portable-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
Expand-Archive -Path $Zip -DestinationPath $dir
try {
    foreach ($f in 'ExplorerOrganizer.exe', 'portable.txt', 'LICENSE.txt', 'README.md') { Check (Test-Path (Join-Path $dir $f)) "zip contains $f" }
    $exe = Join-Path $dir 'ExplorerOrganizer.exe'
    $psi = New-Object System.Diagnostics.ProcessStartInfo $exe, '--show'
    $psi.UseShellExecute = $false
    $psi.EnvironmentVariables.Remove('EXPLORER_ORGANIZER_DATA')   # real portable behaviour
    $p = [System.Diagnostics.Process]::Start($psi)
    $deadline = (Get-Date).AddSeconds(30)
    while ($p.MainWindowHandle -eq [IntPtr]::Zero -and -not $p.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300; $p.Refresh() }
    Check (-not $p.HasExited) 'process is running (no .NET runtime or installation needed)'
    Check ($p.MainWindowHandle -ne [IntPtr]::Zero) "a window is shown ('$($p.MainWindowTitle)')"
    Check (Test-Path (Join-Path $dir 'data\logs')) 'data folder created next to the exe (portable mode)'
    if (-not $p.HasExited) { $p.Kill(); $p.WaitForExit(5000) | Out-Null }
}
finally {
    Start-Sleep -Milliseconds 500
    Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
}
if ($failed) { exit 1 } else { Write-Host 'Portable edition OK'; exit 0 }
