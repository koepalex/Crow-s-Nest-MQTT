<#
.SYNOPSIS
    Publishes, packages, and installs CrowsNestMQTT locally for development testing.

.DESCRIPTION
    One-command convenience script that:
    1. Runs dotnet publish (self-contained, non-single-file)
    2. Prepares a loose MSIX layout (AppxManifest.xml, assets, resources.pri)
    3. Registers the app via Add-AppxPackage -Register (Developer Mode, no signing needed)

    Requires Windows Developer Mode to be enabled:
    Settings -> System -> For developers -> Developer Mode = On

.PARAMETER Architecture
    Target architecture. Defaults to the current machine architecture.

.PARAMETER Version
    Version string for the package. Defaults to "0.0.1".

.PARAMETER Configuration
    Build configuration. Defaults to "Release".

.EXAMPLE
    .\Install-MsixLocal.ps1

.EXAMPLE
    .\Install-MsixLocal.ps1 -Architecture arm64 -Version "1.2.3"
#>
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture,

    [Parameter(Mandatory = $false)]
    [string]$Version = "0.0.1",

    [Parameter(Mandatory = $false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

# Auto-detect architecture if not specified
if (-not $Architecture) {
    $Architecture = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' }
}

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent (Resolve-Path $MyInvocation.MyCommand.Path) }
$repoRoot = Split-Path $scriptDir -Parent
$runtime = "win-$Architecture"
$publishDir = Join-Path $repoRoot "publish\msix-local-$runtime"
$projectPath = Join-Path $repoRoot "src\MainApp\CrowsNestMqtt.App.csproj"
$manifestTemplate = Join-Path $repoRoot "src\MainApp\Package\AppxManifest.xml.template"
$assetsDir = Join-Path $repoRoot "src\MainApp\Package\Assets"

# Ensure version has 4 parts (MSIX requires x.y.z.w)
$versionParts = $Version -split '\.'
$cleanParts = @()
foreach ($part in $versionParts) {
    $cleanParts += ($part -split '-')[0]
}
while ($cleanParts.Count -lt 4) { $cleanParts += '0' }
$msixVersion = ($cleanParts[0..3]) -join '.'

Write-Host "=== Local MSIX Install (Developer Mode) ===" -ForegroundColor Cyan
Write-Host "Architecture:  $Architecture"
Write-Host "Version:       $Version -> MSIX: $msixVersion"
Write-Host "Configuration: $Configuration"
Write-Host "Layout dir:    $publishDir"
Write-Host ""

# 1. Uninstall existing version if present
$existing = Get-AppxPackage -Name "*CrowsNestMQTT*" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Removing existing installation..." -ForegroundColor Yellow
    $existing | Remove-AppxPackage
    Write-Host "  Removed: $($existing.PackageFullName)"
}

# 2. Publish
Write-Host "Publishing ($runtime)..." -ForegroundColor Yellow
dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $runtime `
    --self-contained `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit 1
}

# 3. Prepare loose MSIX layout (manifest + assets + PRI)
Write-Host ""
Write-Host "Preparing MSIX layout..." -ForegroundColor Yellow

# Generate AppxManifest.xml from template
$manifestContent = Get-Content -Path $manifestTemplate -Raw
$manifestContent = $manifestContent -replace '\{\{VERSION\}\}', $msixVersion
$manifestContent = $manifestContent -replace '\{\{ARCHITECTURE\}\}', $Architecture
Set-Content -Path (Join-Path $publishDir "AppxManifest.xml") -Value $manifestContent -Encoding UTF8
Write-Host "  Generated AppxManifest.xml"

# Copy visual assets
$layoutAssets = Join-Path $publishDir "Assets"
New-Item -ItemType Directory -Path $layoutAssets -Force | Out-Null
Copy-Item -Path "$assetsDir\*" -Destination $layoutAssets -Recurse -Force
Write-Host "  Copied visual assets"

# Generate resources.pri
$sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
$sdkVersion = Get-ChildItem -Path $sdkRoot -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
    Sort-Object { [Version]$_.Name } -Descending |
    Select-Object -First 1
$makePri = Join-Path $sdkVersion.FullName "x64\makepri.exe"

$priConfig = Join-Path $publishDir "priconfig.xml"
& $makePri createconfig /cf $priConfig /dq en-US /o 2>&1 | Out-Null
& $makePri new /pr $publishDir /cf $priConfig /of (Join-Path $publishDir "resources.pri") /o 2>&1 | Out-Null
Remove-Item $priConfig -ErrorAction SilentlyContinue
Write-Host "  Generated resources.pri"

# 4. Register the app (loose file deployment — no signing required)
Write-Host ""
Write-Host "Registering app (Developer Mode)..." -ForegroundColor Yellow
$manifestPath = Join-Path $publishDir "AppxManifest.xml"
Add-AppxPackage -Register $manifestPath

Write-Host ""
Write-Host "Installed successfully!" -ForegroundColor Green
Write-Host "Launch 'Crow's NestMQTT' from the Start menu." -ForegroundColor Green
Write-Host ""
Write-Host "Note: The app runs from: $publishDir" -ForegroundColor DarkGray
Write-Host "      Do NOT delete this directory while the app is installed." -ForegroundColor DarkGray
Write-Host "      To uninstall: .\tools\Uninstall-MsixLocal.ps1" -ForegroundColor DarkGray
