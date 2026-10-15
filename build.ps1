#Requires -Version 5.1
<#
.SYNOPSIS
    Builds Explorer Organizer: unit tests, self-contained single-file exe, UI tests on that exe, portable zip and
    Inno Setup installer.

.DESCRIPTION
    Output:
      artifacts\publish\ExplorerOrganizer.exe                (self-contained, win-x64, no runtime needed)
      artifacts\ExplorerOrganizer-Portable.zip                (portable edition)
      installer\Output\ExplorerOrganizer-Setup.exe           (installer)
      docs\test-report.txt                                    (output of every test run)
      docs\screenshots\*.png                                  (captured by the UI tests)

    Order: unit tests -> publish -> UI tests against the published exe -> portable zip -> installer
    -> (optional, -Verify) silent install/uninstall check and portable start check.

    Toolchain lookup order: .tools\ (local, created by -BootstrapTools), then the system PATH / default locations.
    Nothing is installed globally unless you do it yourself.

.PARAMETER SkipUiTests
    Skips the UI tests (they start the app and drive it with UI Automation: they need an interactive desktop).

.PARAMETER Verify
    After building: installs the setup silently into a temporary folder for the current user, checks files,
    shortcut and registry, uninstalls and checks that nothing is left; then starts the portable exe from a
    temporary copy and checks that its window appears and that it keeps its data next to the exe.

.PARAMETER BootstrapTools
    Downloads .NET SDK 8 (official dotnet-install.ps1) and Inno Setup 6 (official installer, portable mode)
    into .tools\ inside this repository. No system-wide installation, no admin rights.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1 -BootstrapTools
.EXAMPLE
    .\build.ps1 -SkipTests -SkipInstaller
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$SkipInstaller,
    [switch]$SkipUiTests,
    [switch]$Verify,
    [switch]$BootstrapTools
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$Root = $PSScriptRoot
$Tools = Join-Path $Root '.tools'
$Artifacts = Join-Path $Root 'artifacts'
$PublishDir = Join-Path $Artifacts 'publish'
$InnoVersion = '6.7.3'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }
function Fail($text) { Write-Host "ERROR: $text" -ForegroundColor Red; exit 1 }

function Get-Dotnet {
    $local = Join-Path $Tools 'dotnet\dotnet.exe'
    if (Test-Path $local) { return $local }
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) {
        $sdks = & $cmd.Source --list-sdks 2>$null
        if ($sdks -match '^(8|9|10)\.') { return $cmd.Source }
    }
    return $null
}

function Get-Iscc {
    $candidates = @(
        (Join-Path $Tools 'innosetup\ISCC.exe'),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { return $c } }
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

if ($BootstrapTools) {
    Step 'Bootstrapping local toolchain into .tools\ (no system changes)'
    New-Item -ItemType Directory -Force (Join-Path $Tools 'dl') | Out-Null
    if (-not (Test-Path (Join-Path $Tools 'dotnet\dotnet.exe'))) {
        $script = Join-Path $Tools 'dl\dotnet-install.ps1'
        Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script
        & $script -Channel 8.0 -InstallDir (Join-Path $Tools 'dotnet') -NoPath
    }
    if (-not (Test-Path (Join-Path $Tools 'innosetup\ISCC.exe'))) {
        $tag = 'is-' + $InnoVersion.Replace('.', '_')
        $exe = Join-Path $Tools "dl\innosetup-$InnoVersion.exe"
        Invoke-WebRequest "https://github.com/jrsoftware/issrc/releases/download/$tag/innosetup-$InnoVersion.exe" -OutFile $exe
        $sig = Get-AuthenticodeSignature $exe
        if ($sig.Status -ne 'Valid') { Fail "Inno Setup download has an invalid signature ($($sig.Status))." }
        $p = Start-Process $exe -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/PORTABLE=1', '/CURRENTUSER', "/DIR=`"$(Join-Path $Tools 'innosetup')`"" -Wait -PassThru
        if ($p.ExitCode -ne 0) { Fail "Inno Setup extraction failed ($($p.ExitCode))." }
    }
}

$dotnet = Get-Dotnet
if (-not $dotnet) {
    Fail ".NET SDK 8+ not found. Run:  powershell -ExecutionPolicy Bypass -File .\build.ps1 -BootstrapTools`n       or install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0"
}
Write-Host "dotnet: $dotnet ($(& $dotnet --version))"

[xml]$props = Get-Content (Join-Path $Root 'Directory.Build.props')
$Version = $props.Project.PropertyGroup.Version
Write-Host "version: $Version"

$Report = Join-Path $Root 'docs\test-report.txt'
$TestFailures = @()

function Run-Tests($project, $title) {
    Step "Running $title"
    $header = "`r`n==================== $title ====================`r`n" + `
              "date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  os: $([Environment]::OSVersion.VersionString)  configuration: $Configuration`r`n"
    # stderr lines must not stop the script (PS 5.1 wraps them in ErrorRecords); keep the report in UTF-8.
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    $out = & $dotnet test $project -c $Configuration --nologo --logger 'console;verbosity=normal' 2>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    $ErrorActionPreference = $old
    $out | Out-Host
    Add-Content -Path $Report -Value ($header + ($out -join "`r`n")) -Encoding utf8
    if ($code -ne 0) { $script:TestFailures += $title }
}

if (-not $SkipTests) {
    New-Item -ItemType Directory -Force (Split-Path $Report) | Out-Null
    Set-Content -Path $Report -Encoding utf8 -Value "Explorer Organizer $Version - test report (generated by build.ps1)"
    Run-Tests (Join-Path $Root 'tests\ExplorerOrganizer.Tests\ExplorerOrganizer.Tests.csproj') 'unit and integration tests (Core)'
    if ($TestFailures.Count -gt 0) { Fail "Unit tests failed (see $Report)." }
}

Step 'Publishing self-contained single-file exe (win-x64)'
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
# Antivirus real-time scanning can lock the freshly bundled exe ("being used by another process"): retry with back-off.
$published = $false
for ($attempt = 1; $attempt -le 4 -and -not $published; $attempt++) {
    & $dotnet publish (Join-Path $Root 'src\ExplorerOrganizer.App\ExplorerOrganizer.App.csproj') `
        -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none -p:DebugSymbols=false -p:SatelliteResourceLanguages=en `
        -o $PublishDir --nologo
    if ($LASTEXITCODE -eq 0) { $published = $true }
    else { Write-Host "dotnet publish attempt $attempt failed, retrying..." -ForegroundColor Yellow; Start-Sleep -Seconds (4 * $attempt) }
}
if (-not $published) { Fail 'dotnet publish failed.' }
$exe = Join-Path $PublishDir 'ExplorerOrganizer.exe'
if (-not (Test-Path $exe)) { Fail "Missing $exe" }
Get-ChildItem $PublishDir -Exclude 'ExplorerOrganizer.exe' | Remove-Item -Recurse -Force   # single file only

if (-not $SkipTests -and -not $SkipUiTests) {
    # The UI tests start the published single-file exe (isolated data folders) and save screenshots into docs\screenshots.
    $env:EO_EXE = $exe
    $env:EO_SCREENSHOTS_DIR = Join-Path $Root 'docs\screenshots'
    Run-Tests (Join-Path $Root 'tests\ExplorerOrganizer.UiTests\ExplorerOrganizer.UiTests.csproj') 'UI tests (published exe)'
    Remove-Item Env:\EO_EXE, Env:\EO_SCREENSHOTS_DIR -ErrorAction SilentlyContinue
    if ($TestFailures.Count -gt 0) { Fail "UI tests failed (see $Report)." }
}

Step 'Creating portable zip'
$portableDir = Join-Path $Artifacts 'portable'
if (Test-Path $portableDir) { Remove-Item $portableDir -Recurse -Force }
New-Item -ItemType Directory -Force $portableDir | Out-Null
Copy-Item $exe $portableDir
Set-Content -Path (Join-Path $portableDir 'portable.txt') -Encoding ascii -Value 'This file makes Explorer Organizer store its settings in the "data" folder next to the exe.'
Copy-Item (Join-Path $Root 'LICENSE') (Join-Path $portableDir 'LICENSE.txt')
Copy-Item (Join-Path $Root 'README.md') $portableDir
$zip = Join-Path $Artifacts 'ExplorerOrganizer-Portable.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath $zip

if (-not $SkipInstaller) {
    Step 'Building installer with Inno Setup'
    $iscc = Get-Iscc
    if (-not $iscc) { Fail "Inno Setup 6 (ISCC.exe) not found. Run .\build.ps1 -BootstrapTools or install it from https://jrsoftware.org/isdl.php" }
    # Antivirus real-time scanning can lock the setup exe while ISCC writes its resources
    # ("EndUpdateResource failed (110)"): compile in a fresh temp folder, retry with back-off, then copy.
    $outDir = Join-Path $Root 'installer\Output'
    New-Item -ItemType Directory -Force $outDir | Out-Null
    $ok = $false
    for ($attempt = 1; $attempt -le 6 -and -not $ok; $attempt++) {
        $tmpOut = Join-Path ([IO.Path]::GetTempPath()) ("eo-setup-" + [guid]::NewGuid().ToString('N'))
        & $iscc /Q "/O$tmpOut" "/DMyAppVersion=$Version" "/DSourceDir=$PublishDir" (Join-Path $Root 'installer\setup.iss')
        if ($LASTEXITCODE -eq 0) {
            Copy-Item (Join-Path $tmpOut 'ExplorerOrganizer-Setup.exe') $outDir -Force
            $ok = $true
        } else {
            Write-Host "Inno Setup attempt $attempt failed, retrying..." -ForegroundColor Yellow
            Start-Sleep -Seconds (3 * $attempt)
        }
        Remove-Item $tmpOut -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (-not $ok) { Fail 'Inno Setup compilation failed (see above; antivirus exclusions for %TEMP% may help).' }
}

if ($Verify) {
    Step 'Verifying portable edition'
    & (Join-Path $Root 'tests\verify-portable.ps1') -Zip $zip
    if ($LASTEXITCODE -ne 0) { Fail 'Portable verification failed.' }
    if (-not $SkipInstaller) {
        Step 'Verifying installer (silent install + uninstall)'
        & (Join-Path $Root 'tests\verify-installer.ps1') -Setup (Join-Path $Root 'installer\Output\ExplorerOrganizer-Setup.exe')
        if ($LASTEXITCODE -ne 0) { Fail 'Installer verification failed.' }
    }
}

Step 'Done'
$outputs = @($exe, $zip)
if (-not $SkipInstaller) { $outputs += (Join-Path $Root 'installer\Output\ExplorerOrganizer-Setup.exe') }
foreach ($o in $outputs) {
    if (Test-Path $o) { '{0,8:N1} MB  {1}' -f ((Get-Item $o).Length / 1MB), $o | Write-Host }
}
