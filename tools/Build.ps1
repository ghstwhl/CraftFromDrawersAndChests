<#
.SYNOPSIS
    Builds, validates, and packages CraftFromDrawersAndChests in one command.

.DESCRIPTION
    Runs the whole release pipeline so it can be invoked (and approved) as a single step:

      1. Restores NuGet packages (packages.config) if needed.
      2. Validates:
           - ModInfo.Version is MAJOR.MINOR.PATCH and in sync with both manifest.json files,
           - every translation JSON file parses,
           - the local reference assemblies exist.
      3. Builds the project with MSBuild.
      4. For Release, assembles the Thunderstore-style package and two identical zips:
         CraftFromDrawersAndChests-release.zip and CraftFromDrawersAndChests-<version>.zip.

    The mod DLL is byte-identical on the server and the client, so a single build is
    deployable to both. (Portal networks live in the BepInEx config, not in a separate file.)

.PARAMETER Configuration
    'Release' (default) or 'Debug'. Debug builds but does not package.

.PARAMETER ReferencesRoot
    Folder containing the publicized Valheim + BepInEx reference assemblies.
    Defaults to <repo>\.references (created by tools/New-ValheimRefs.ps1).

.PARAMETER SkipRestore
    Skip the NuGet restore step.

.PARAMETER SkipValidation
    Skip the pre-build validation checks.

.PARAMETER NoPackage
    Build only; do not create the release zip (the release folder is still produced).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -SkipRestore

.NOTES
    Prerequisites:
      * Visual Studio (or Build Tools) with MSBuild and the .NET desktop workload.
      * Reference assemblies in .references (run tools/New-ValheimRefs.ps1 once).
      * nuget.exe (auto-downloaded to tools/.cache if not on PATH).

    Exit code is 0 on success and non-zero on failure (CI-friendly).

    See tools/README.md for full documentation.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$ReferencesRoot,

    [switch]$SkipRestore,
    [switch]$SkipValidation,
    [switch]$NoPackage
)

$ErrorActionPreference = 'Stop'

$RepoRoot         = Split-Path -Parent $PSScriptRoot
$ProjectPath      = Join-Path $RepoRoot 'CraftFromDrawersAndChests\CraftFromDrawersAndChests.csproj'
$ModInfoPath      = Join-Path $RepoRoot 'CraftFromDrawersAndChests\ModInfo.cs'
$RootManifestPath = Join-Path $RepoRoot 'manifest.json'
$GenManifestPath  = Join-Path $RepoRoot 'Docs\SolutionDir\Package\Release\manifest.json'
$TranslationsDir  = Join-Path $RepoRoot 'CraftFromDrawersAndChests\Translations'
$PackagesDir      = Join-Path $RepoRoot 'packages'
$CacheDir         = Join-Path $PSScriptRoot '.cache'
$NuGetExe         = Join-Path $CacheDir 'nuget.exe'
$ReleaseDir       = Join-Path $RepoRoot 'Release'
# Set from ModInfo.cs in the try block below (ThunderstoreTeam-ThunderstorePackage), so the staging
# folder mirrors the published package; it is also handed to MSBuild as /p:PackageFolder so the csproj's
# copy target agrees. Expect e.g. Release\NorCal_Nerds-CraftFromDrawersAndChests.
$PackageRoot      = $null
$ZipPath          = Join-Path $ReleaseDir 'CraftFromDrawersAndChests-release.zip'
$OutputDll        = Join-Path $RepoRoot "CraftFromDrawersAndChests\bin\$Configuration\CraftFromDrawersAndChests.dll"

function Write-Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Message) Write-Host "    $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message) Write-Host "    $Message" -ForegroundColor DarkGray }
function Fail       { param([string]$Message) Write-Host "!! $Message" -ForegroundColor Red; exit 1 }

function Get-MSBuildPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
        if ($found) { return $found }
    }

    $cmd = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    return $null
}

function Get-NuGetPath {
    if (Test-Path $NuGetExe) { return $NuGetExe }

    $cmd = Get-Command nuget -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    try {
        New-Item -ItemType Directory -Force -Path $CacheDir | Out-Null
        Write-Info "Downloading nuget.exe -> $NuGetExe"
        Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $NuGetExe
        if (Test-Path $NuGetExe) { return $NuGetExe }
    }
    catch {
        Write-Info "nuget.exe download failed: $($_.Exception.Message)"
    }

    return $null
}

function Get-ModVersion {
    if (-not (Test-Path $ModInfoPath)) { Fail "ModInfo.cs not found at '$ModInfoPath'." }

    $match = [regex]::Match((Get-Content $ModInfoPath -Raw), 'Version\s*=\s*"([^"]+)"')
    if (-not $match.Success) { Fail 'Could not read ModInfo.Version.' }

    return $match.Groups[1].Value
}

function Get-ModString {
    # Reads a `public const string <Name> = "<value>";` declaration out of ModInfo.cs.
    param([string]$Name)

    if (-not (Test-Path $ModInfoPath)) { Fail "ModInfo.cs not found at '$ModInfoPath'." }

    $match = [regex]::Match((Get-Content $ModInfoPath -Raw), "public const string $Name\s*=\s*""([^""]*)""")
    if (-not $match.Success) { Fail "Could not read ModInfo.$Name." }

    return $match.Groups[1].Value
}

function Get-ValheimGameVersion {
    # Single source of truth is Directory.Build.props (also used by the csproj).
    $propsPath = Join-Path $RepoRoot 'Directory.Build.props'
    if (Test-Path $propsPath) {
        $match = [regex]::Match((Get-Content $propsPath -Raw), '<ValheimGameVersion[^>]*>([^<]+)</ValheimGameVersion>')
        if ($match.Success) { return $match.Groups[1].Value.Trim() }
    }

    return '1.0.16'
}

function Resolve-Refs {
    $root = $ReferencesRoot
    if (-not $root) { $root = $env:ReferencesRoot }
    if (-not $root) { $root = Join-Path $RepoRoot '.references' }
    return $root
}

function Invoke-Validation {
    param([string]$Version)

    Write-Step 'Validating'

    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        Fail "ModInfo.Version '$Version' is not MAJOR.MINOR.PATCH."
    }

    $root = (Get-Content $RootManifestPath -Raw | ConvertFrom-Json).version_number
    if ($root -ne $Version) {
        Fail "manifest.json version_number '$root' != ModInfo.Version '$Version'. Bump them together."
    }
    Write-Ok "manifest.json = $Version"

    if (Test-Path $GenManifestPath) {
        $gen = (Get-Content $GenManifestPath -Raw | ConvertFrom-Json).version_number
        if ($gen -ne $Version) {
            Fail "Docs/SolutionDir/Package/Release/manifest.json version_number '$gen' != '$Version'. Bump them together."
        }
        Write-Ok "generated manifest.json = $Version"
    }
    else {
        Write-Info 'generated manifest.json not present (skipped)'
    }

    $jsonFiles = @()
    if (Test-Path $TranslationsDir) {
        $jsonFiles += Get-ChildItem -Path $TranslationsDir -Recurse -Filter '*.json' -File | Select-Object -ExpandProperty FullName
    }

    foreach ($file in $jsonFiles) {
        try {
            Get-Content $file -Raw | ConvertFrom-Json | Out-Null
        }
        catch {
            Fail "Invalid JSON: $file -> $($_.Exception.Message)"
        }
    }
    Write-Ok "JSON OK ($($jsonFiles.Count) files)"
}

function Invoke-Restore {
    Write-Step 'Restoring NuGet packages'

    $nuget = Get-NuGetPath
    if (-not $nuget) {
        Fail 'nuget.exe not found and could not be downloaded. Install it and run: nuget restore CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj'
    }

    & $nuget restore $ProjectPath -PackagesDirectory $PackagesDir -Verbosity quiet
    if ($LASTEXITCODE -ne 0) { Fail "NuGet restore failed (exit $LASTEXITCODE)." }

    Write-Ok 'Packages restored'
}

function New-DocStage {
    # The Release "Copy" target stages packaging docs from $(ModRoot)/$(ProjectName); build
    # them a temp root so the build works without the original author's local layout.
    $stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('craftfromdrawersandchests-stage-' + [System.Guid]::NewGuid().ToString('N'))
    $stageProj = Join-Path $stageRoot 'CraftFromDrawersAndChests'
    New-Item -ItemType Directory -Force -Path $stageProj | Out-Null

    foreach ($name in @('LICENSE.md', 'manifest.json', 'CHANGELOG.md', 'README.md', 'icon.png')) {
        $src = Join-Path $RepoRoot $name
        if (Test-Path $src) { Copy-Item $src $stageProj }
    }

    return $stageRoot
}

try {
    Write-Host ''
    Write-Host "CraftFromDrawersAndChests build - $Configuration" -ForegroundColor White

    $version = Get-ModVersion
    Write-Info "Version $version"

    # Thunderstore identity from ModInfo.cs: names the package folder and tells MSBuild where to stage.
    $packageFolder = "$(Get-ModString 'ThunderstoreTeam')-$(Get-ModString 'ThunderstorePackage')"
    $PackageRoot = Join-Path $ReleaseDir $packageFolder
    Write-Info "Package folder $packageFolder"

    if (-not $SkipValidation) { Invoke-Validation -Version $version }

    $refs = (Resolve-Refs)
    $gameVersion = Get-ValheimGameVersion
    $probe = Join-Path $refs "Valheim\$gameVersion\assembly_valheim_publicized.dll"
    if (-not (Test-Path $probe)) {
        Fail "Reference assemblies for Valheim $gameVersion not found at '$refs'. Run tools/New-ValheimRefs.ps1 first."
    }
    Write-Ok "References: $refs (Valheim $gameVersion)"

    $msbuild = Get-MSBuildPath
    if (-not $msbuild) {
        Fail 'MSBuild not found. Install Visual Studio (or Build Tools) with the .NET desktop workload.'
    }

    if (-not $SkipRestore) { Invoke-Restore }

    Write-Step "Building ($Configuration)"

    $msbuildArgs = @(
        $ProjectPath
        "/p:Configuration=$Configuration"
        "/p:ReferencesRoot=$refs"
        "/p:PackageFolder=$packageFolder"
        '/p:DevPluginsFolder=/tmp/craftfromdrawersandchests-devplugins'   # non-existent -> dev-deploy target skipped
        '/nologo'
        '/v:minimal'
    )

    if ($Configuration -eq 'Release') {
        $stageRoot = New-DocStage
        $msbuildArgs += "/p:ModRoot=$stageRoot"
        $msbuildArgs += "/p:ReleaseRoot=$ReleaseDir"
    }

    & $msbuild @msbuildArgs
    if ($LASTEXITCODE -ne 0) { Fail "Build failed (exit $LASTEXITCODE)." }

    if (-not (Test-Path $OutputDll)) {
        Fail "Build reported success but '$OutputDll' was not produced."
    }
    Write-Ok ("Built {0} ({1:N0} bytes)" -f $OutputDll, (Get-Item $OutputDll).Length)

    if ($Configuration -eq 'Release') {
        $packagedDll = Join-Path $PackageRoot 'plugins\CraftFromDrawersAndChests.dll'
        if (-not (Test-Path $packagedDll)) {
            Fail "Packaging did not produce '$packagedDll'."
        }
        Write-Ok "Package: $PackageRoot"

        if (-not $NoPackage) {
            Write-Step 'Packaging zip'
            if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
            Compress-Archive -Path (Join-Path $PackageRoot '*') -DestinationPath $ZipPath -Force
            if (-not (Test-Path $ZipPath)) { Fail 'Failed to create the release zip.' }
            Write-Ok ("Zip: {0} ({1:N0} bytes)" -f $ZipPath, (Get-Item $ZipPath).Length)

            # Second artifact: the same zip under the version being released, so a download can be pinned
            # to an exact build while CraftFromDrawersAndChests-release.zip stays the stable "latest" name.
            $versionedZipPath = Join-Path $ReleaseDir "CraftFromDrawersAndChests-$version.zip"
            if (Test-Path $versionedZipPath) { Remove-Item $versionedZipPath -Force }
            Copy-Item -Path $ZipPath -Destination $versionedZipPath -Force
            if (-not (Test-Path $versionedZipPath)) { Fail "Failed to create '$versionedZipPath'." }
            Write-Ok ("Zip: {0} ({1:N0} bytes)" -f $versionedZipPath, (Get-Item $versionedZipPath).Length)
        }
    }

    Write-Host ''
    Write-Host "BUILD OK - CraftFromDrawersAndChests $version ($Configuration)" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ''
    Write-Host "BUILD FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
