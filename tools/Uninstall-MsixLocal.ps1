<#
.SYNOPSIS
    Uninstalls the locally installed CrowsNestMQTT MSIX package.

.DESCRIPTION
    Finds and removes the CrowsNestMQTT AppxPackage. Optionally cleans up
    local MSIX build artifacts from the publish directory.

.PARAMETER CleanArtifacts
    If set, also deletes local .msix files and publish output from the publish directory.

.EXAMPLE
    .\Uninstall-MsixLocal.ps1

.EXAMPLE
    .\Uninstall-MsixLocal.ps1 -CleanArtifacts
#>
param(
    [switch]$CleanArtifacts
)

$ErrorActionPreference = 'Stop'

Write-Host "=== Uninstall CrowsNestMQTT ===" -ForegroundColor Cyan

$packages = Get-AppxPackage -Name "*CrowsNestMQTT*" -ErrorAction SilentlyContinue

if (-not $packages) {
    Write-Host "CrowsNestMQTT is not installed." -ForegroundColor Yellow
} else {
    foreach ($pkg in $packages) {
        Write-Host "Removing: $($pkg.PackageFullName)" -ForegroundColor Yellow
        $pkg | Remove-AppxPackage
        Write-Host "  Removed." -ForegroundColor Green
    }
}

if ($CleanArtifacts) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent (Resolve-Path $MyInvocation.MyCommand.Path) }
    $repoRoot = Split-Path $scriptDir -Parent
    $publishDir = Join-Path $repoRoot "publish"

    if (Test-Path $publishDir) {
        # Remove MSIX files
        $msixFiles = Get-ChildItem -Path $publishDir -Filter "*.msix" -ErrorAction SilentlyContinue
        foreach ($f in $msixFiles) {
            Remove-Item $f.FullName -Force
            Write-Host "Deleted: $($f.Name)" -ForegroundColor DarkGray
        }

        # Remove msix-local-* publish directories
        $localDirs = Get-ChildItem -Path $publishDir -Directory -Filter "msix-local-*" -ErrorAction SilentlyContinue
        foreach ($d in $localDirs) {
            Remove-Item $d.FullName -Recurse -Force
            Write-Host "Deleted: $($d.Name)\" -ForegroundColor DarkGray
        }
    }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
