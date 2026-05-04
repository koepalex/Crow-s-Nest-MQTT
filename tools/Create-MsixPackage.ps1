<#
.SYNOPSIS
    Creates an MSIX package from dotnet publish output.

.DESCRIPTION
    Takes the output of 'dotnet publish' and packages it into an MSIX file using
    makeappx.exe and makepri.exe from the Windows SDK.

.PARAMETER PublishDir
    Path to the dotnet publish output directory.

.PARAMETER Version
    Semantic version for the package (e.g., "1.2.3"). A ".0" revision is appended automatically.

.PARAMETER Architecture
    Target architecture: x64 or arm64.

.PARAMETER OutputPath
    Path for the output .msix file. If not specified, defaults to
    ./publish/CrowsNestMqtt-<arch>-<version>.msix

.PARAMETER ManifestTemplate
    Path to the AppxManifest.xml.template file.

.PARAMETER AssetsDir
    Path to the directory containing MSIX visual assets (PNGs).

.EXAMPLE
    .\Create-MsixPackage.ps1 -PublishDir .\publish\win-x64 -Version "1.0.0" -Architecture x64
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture,

    [Parameter(Mandatory = $false)]
    [string]$OutputPath,

    [Parameter(Mandatory = $false)]
    [string]$ManifestTemplate,

    [Parameter(Mandatory = $false)]
    [string]$AssetsDir
)

$ErrorActionPreference = 'Stop'

# Resolve script directory for default paths
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent (Resolve-Path $MyInvocation.MyCommand.Path) }
$repoRoot = Split-Path $scriptDir -Parent

if (-not $ManifestTemplate) { $ManifestTemplate = Join-Path $repoRoot "src\MainApp\Package\AppxManifest.xml.template" }
if (-not $AssetsDir) { $AssetsDir = Join-Path $repoRoot "src\MainApp\Package\Assets" }

# Resolve relative paths using PowerShell's $PWD (not .NET CurrentDirectory which can differ)
function Resolve-AbsolutePath {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return [System.IO.Path]::GetFullPath((Join-Path $PWD $Path))
}

# Resolve paths
$PublishDir = Resolve-AbsolutePath $PublishDir
if ($ManifestTemplate -and -not [System.IO.Path]::IsPathRooted($ManifestTemplate)) {
    $ManifestTemplate = Resolve-AbsolutePath $ManifestTemplate
}
if ($AssetsDir -and -not [System.IO.Path]::IsPathRooted($AssetsDir)) {
    $AssetsDir = Resolve-AbsolutePath $AssetsDir
}

# Ensure version has 4 parts (MSIX requires x.y.z.w)
$versionParts = $Version -split '\.'
# Strip any prerelease suffix from the last numeric part
$cleanParts = @()
foreach ($part in $versionParts) {
    $numericPart = ($part -split '-')[0]
    $cleanParts += $numericPart
}
while ($cleanParts.Count -lt 4) {
    $cleanParts += '0'
}
$msixVersion = ($cleanParts[0..3]) -join '.'

if (-not $OutputPath) {
    $OutputPath = Join-Path (Split-Path $PublishDir -Parent) "CrowsNestMqtt-$Architecture-$Version.msix"
} elseif (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Resolve-AbsolutePath $OutputPath
}

Write-Host "=== MSIX Package Creation ===" -ForegroundColor Cyan
Write-Host "Publish dir:  $PublishDir"
Write-Host "Version:      $Version -> MSIX version: $msixVersion"
Write-Host "Architecture: $Architecture"
Write-Host "Output:       $OutputPath"
Write-Host ""

# Validate inputs
if (-not (Test-Path $PublishDir)) {
    Write-Error "Publish directory not found: $PublishDir"
    exit 1
}
if (-not (Test-Path $ManifestTemplate)) {
    Write-Error "Manifest template not found: $ManifestTemplate"
    exit 1
}
if (-not (Test-Path $AssetsDir)) {
    Write-Error "Assets directory not found: $AssetsDir"
    exit 1
}

# Determine host architecture for SDK tool selection (tools must run on the host,
# regardless of the target architecture we're packaging for)
$hostArch = switch ($env:PROCESSOR_ARCHITECTURE) {
    'ARM64' { 'arm64' }
    default { 'x64' }
}

# Find Windows SDK tools
function Find-SdkTool {
    param([string]$ToolName)

    $sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (-not (Test-Path $sdkRoot)) {
        Write-Error "Windows SDK not found at $sdkRoot"
        exit 1
    }

    # Find the latest SDK version
    $sdkVersions = Get-ChildItem -Path $sdkRoot -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [Version]$_.Name } -Descending

    foreach ($sdkVersion in $sdkVersions) {
        $toolPath = Join-Path $sdkVersion.FullName "$hostArch\$ToolName"
        if (Test-Path $toolPath) {
            return $toolPath
        }
        # Fallback to x64 if host arch tool not found
        if ($hostArch -ne 'x64') {
            $toolPathX64 = Join-Path $sdkVersion.FullName "x64\$ToolName"
            if (Test-Path $toolPathX64) {
                return $toolPathX64
            }
        }
    }

    Write-Error "$ToolName not found in Windows SDK at $sdkRoot"
    exit 1
}

$makeAppx = Find-SdkTool "makeappx.exe"
$makePri = Find-SdkTool "makepri.exe"
Write-Host "makeappx.exe: $makeAppx"
Write-Host "makepri.exe:  $makePri"
Write-Host ""

# Create staging directory
$stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) "msix-staging-$([System.Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

try {
    # 1. Copy publish output to staging
    Write-Host "Copying publish output to staging directory..." -ForegroundColor Yellow
    $sourceFiles = Get-ChildItem -Path $PublishDir -Recurse -File
    $fileCount = ($sourceFiles | Measure-Object).Count
    if ($fileCount -eq 0) {
        Write-Error "Publish directory is empty: $PublishDir"
        exit 1
    }
    Write-Host "  Found $fileCount files in publish directory"
    Copy-Item -Path "$PublishDir\*" -Destination $stagingDir -Recurse -Force

    # Verify the executable was copied
    $exePath = Join-Path $stagingDir "CrowsNestMqtt.App.exe"
    if (-not (Test-Path $exePath)) {
        Write-Error "CrowsNestMqtt.App.exe not found in staging directory after copy. Check that the publish directory contains the app output: $PublishDir"
        exit 1
    }
    $stagedCount = (Get-ChildItem -Path $stagingDir -Recurse -File | Measure-Object).Count
    Write-Host "  Copied $stagedCount files to staging"

    # 2. Generate AppxManifest.xml from template
    Write-Host "Generating AppxManifest.xml..." -ForegroundColor Yellow
    $manifestContent = Get-Content -Path $ManifestTemplate -Raw
    $manifestContent = $manifestContent -replace '\{\{VERSION\}\}', $msixVersion
    $manifestContent = $manifestContent -replace '\{\{ARCHITECTURE\}\}', $Architecture
    Set-Content -Path (Join-Path $stagingDir "AppxManifest.xml") -Value $manifestContent -Encoding UTF8

    # 3. Copy visual assets
    Write-Host "Copying visual assets..." -ForegroundColor Yellow
    $stagingAssets = Join-Path $stagingDir "Assets"
    New-Item -ItemType Directory -Path $stagingAssets -Force | Out-Null
    Copy-Item -Path "$AssetsDir\*" -Destination $stagingAssets -Recurse -Force

    # 4. Generate PRI file
    Write-Host "Generating resources.pri..." -ForegroundColor Yellow
    $priConfigPath = Join-Path $stagingDir "priconfig.xml"

    & $makePri createconfig /cf $priConfigPath /dq en-US /o
    if ($LASTEXITCODE -ne 0) {
        Write-Error "makepri createconfig failed with exit code $LASTEXITCODE"
        exit 1
    }

    & $makePri new /pr $stagingDir /cf $priConfigPath /of (Join-Path $stagingDir "resources.pri") /o
    if ($LASTEXITCODE -ne 0) {
        Write-Error "makepri new failed with exit code $LASTEXITCODE"
        exit 1
    }

    # Remove priconfig.xml from staging (not needed in package)
    Remove-Item $priConfigPath -ErrorAction SilentlyContinue

    # 5. Create MSIX package
    Write-Host "Creating MSIX package..." -ForegroundColor Yellow
    $outputDir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }

    & $makeAppx pack /d $stagingDir /p $OutputPath /o
    if ($LASTEXITCODE -ne 0) {
        Write-Error "makeappx pack failed with exit code $LASTEXITCODE"
        exit 1
    }

    Write-Host ""
    Write-Host "MSIX package created successfully: $OutputPath" -ForegroundColor Green
    $fileSize = (Get-Item $OutputPath).Length / 1MB
    Write-Host "Size: $([math]::Round($fileSize, 2)) MB"
}
finally {
    # Clean up staging directory
    if (Test-Path $stagingDir) {
        Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
