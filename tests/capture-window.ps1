#Requires -Version 5.1
<#
.SYNOPSIS
    Starts Explorer Organizer with an isolated data folder and saves a PNG of its main window (as composed by DWM,
    including Mica and rounded corners). Used to document/verify the UI; never touches the user's real settings.
.EXAMPLE
    .\tests\capture-window.ps1 -Exe .\artifacts\publish\ExplorerOrganizer.exe -DataDir $env:TEMP\eo-shot -Out docs\screenshots\home.png
#>
param(
    [Parameter(Mandatory)] [string]$Exe,
    [Parameter(Mandatory)] [string]$DataDir,
    [Parameter(Mandatory)] [string]$Out,
    [string[]]$Arguments = @('--show'),
    [int]$WaitSeconds = 6,
    [switch]$KeepRunning
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EoCapture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'@
[EoCapture]::SetProcessDPIAware() | Out-Null

New-Item -ItemType Directory -Force $DataDir | Out-Null
$env:EXPLORER_ORGANIZER_DATA = (Resolve-Path $DataDir).Path
$p = Start-Process -FilePath $Exe -ArgumentList $Arguments -PassThru
try {
    $deadline = (Get-Date).AddSeconds(30)
    while ($p.MainWindowHandle -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300; $p.Refresh() }
    if ($p.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Main window not found.' }
    Start-Sleep -Seconds $WaitSeconds   # let pages load and the async counters finish
    $h = $p.MainWindowHandle
    [EoCapture]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Milliseconds 400
    $r = New-Object EoCapture+RECT
    [EoCapture]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.R - $r.L; $hgt = $r.B - $r.T
    $bmp = New-Object System.Drawing.Bitmap $w, $hgt
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [EoCapture]::PrintWindow($h, $hdc, 2) | Out-Null   # PW_RENDERFULLCONTENT
    $g.ReleaseHdc($hdc); $g.Dispose()
    New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "saved $Out (${w}x${hgt})"
}
finally {
    if (-not $KeepRunning -and -not $p.HasExited) { $p.Kill(); $p.WaitForExit(5000) | Out-Null }
}
