<#
.SYNOPSIS
    Creates an MSIX bundle from multiple MSIX packages.

.DESCRIPTION
    Combines multiple architecture-specific MSIX packages into a single .msixbundle
    file suitable for Microsoft Store submission.

.PARAMETER MsixFiles
    Array of paths to .msix files to include in the bundle.

.PARAMETER OutputPath
    Path for the output .msixbundle file.

.EXAMPLE
    .\Create-MsixBundle.ps1 -MsixFiles @(".\x64.msix", ".\arm64.msix") -OutputPath .\CrowsNestMqtt.msixbundle
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]]$MsixFiles,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

function Resolve-AbsolutePath {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return [System.IO.Path]::GetFullPath((Join-Path $PWD $Path))
}

$OutputPath = Resolve-AbsolutePath $OutputPath

Write-Host "=== MSIX Bundle Creation ===" -ForegroundColor Cyan
Write-Host "Output: $OutputPath"
Write-Host "Input packages:"
foreach ($msix in $MsixFiles) {
    Write-Host "  - $msix"
}
Write-Host ""

# Validate inputs
foreach ($msix in $MsixFiles) {
    if (-not (Test-Path $msix)) {
        Write-Error "MSIX file not found: $msix"
        exit 1
    }
}

# Find Windows SDK makeappx.exe
function Find-MakeAppx {
    $sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (-not (Test-Path $sdkRoot)) {
        Write-Error "Windows SDK not found at $sdkRoot"
        exit 1
    }

    $sdkVersions = Get-ChildItem -Path $sdkRoot -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [Version]$_.Name } -Descending

    foreach ($sdkVersion in $sdkVersions) {
        $toolPath = Join-Path $sdkVersion.FullName "x64\makeappx.exe"
        if (Test-Path $toolPath) {
            return $toolPath
        }
    }

    Write-Error "makeappx.exe not found in Windows SDK at $sdkRoot"
    exit 1
}

$makeAppx = Find-MakeAppx
Write-Host "makeappx.exe: $makeAppx"
Write-Host ""

# Create a temp directory and copy MSIX files into it (makeappx bundle needs a flat dir)
$bundleDir = Join-Path ([System.IO.Path]::GetTempPath()) "msix-bundle-$([System.Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $bundleDir -Force | Out-Null

try {
    foreach ($msix in $MsixFiles) {
        $destName = Split-Path $msix -Leaf
        Copy-Item -Path $msix -Destination (Join-Path $bundleDir $destName)
    }

    $outputDir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }

    Write-Host "Creating MSIX bundle..." -ForegroundColor Yellow
    & $makeAppx bundle /d $bundleDir /p $OutputPath /o
    if ($LASTEXITCODE -ne 0) {
        Write-Error "makeappx bundle failed with exit code $LASTEXITCODE"
        exit 1
    }

    Write-Host ""
    Write-Host "MSIX bundle created successfully: $OutputPath" -ForegroundColor Green
    $fileSize = (Get-Item $OutputPath).Length / 1MB
    Write-Host "Size: $([math]::Round($fileSize, 2)) MB"
}
finally {
    if (Test-Path $bundleDir) {
        Remove-Item $bundleDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
