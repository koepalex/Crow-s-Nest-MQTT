<#
.SYNOPSIS
    Generates MSIX visual assets and an ICO file from a source PNG image.

.DESCRIPTION
    Uses Python with Pillow to generate:
    - An ICO file for the application (16, 32, 48, 64, 128, 256 px)
    - MSIX visual assets at required sizes (44x44, 50x50, 150x150, 310x150, 310x310)
    - Target-size variants for taskbar icons (16, 24, 32, 48, 256 px)

.PARAMETER SourceImage
    Path to the source PNG image (should be at least 310x310, ideally 1024x1024).

.PARAMETER IcoOutputPath
    Path where the generated ICO file will be written.

.PARAMETER AssetsOutputDir
    Directory where MSIX PNG assets will be written.

.EXAMPLE
    .\Generate-MsixAssets.ps1 -SourceImage .\doc\images\CrowsNestMQTT.png
#>
param(
    [Parameter(Mandatory = $false)]
    [string]$SourceImage,

    [Parameter(Mandatory = $false)]
    [string]$IcoOutputPath,

    [Parameter(Mandatory = $false)]
    [string]$AssetsOutputDir
)

$ErrorActionPreference = 'Stop'

# Resolve script directory for default paths
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent (Resolve-Path $MyInvocation.MyCommand.Path) }
$repoRoot = Split-Path $scriptDir -Parent

if (-not $SourceImage) { $SourceImage = Join-Path $repoRoot "doc\images\CrowsNestMQTT.png" }
if (-not $IcoOutputPath) { $IcoOutputPath = Join-Path $repoRoot "src\MainApp\Assets\icon.ico" }
if (-not $AssetsOutputDir) { $AssetsOutputDir = Join-Path $repoRoot "src\MainApp\Package\Assets" }

# Resolve relative paths using PowerShell's $PWD (not .NET CurrentDirectory which can differ)
function Resolve-AbsolutePath {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return [System.IO.Path]::GetFullPath((Join-Path $PWD $Path))
}

$SourceImage = Resolve-AbsolutePath $SourceImage
$IcoOutputPath = Resolve-AbsolutePath $IcoOutputPath
$AssetsOutputDir = Resolve-AbsolutePath $AssetsOutputDir

if (-not (Test-Path $SourceImage)) {
    Write-Error "Source image not found: $SourceImage"
    exit 1
}

if (-not (Test-Path $AssetsOutputDir)) {
    New-Item -ItemType Directory -Path $AssetsOutputDir -Force | Out-Null
}

$pythonScript = @"
import sys
from PIL import Image

source_path = sys.argv[1]
ico_output = sys.argv[2]
assets_dir = sys.argv[3]

import os

img = Image.open(source_path).convert('RGBA')
print(f"Source image: {img.size[0]}x{img.size[1]} {img.mode}")

# Generate ICO with multiple sizes
ico_sizes = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
img.save(ico_output, format='ICO', sizes=ico_sizes)
print(f"Generated ICO: {ico_output}")

# MSIX square assets
square_assets = {
    'Square44x44Logo.png': (44, 44),
    'Square150x150Logo.png': (150, 150),
    'StoreLogo.png': (50, 50),
    'LargeTile.png': (310, 310),
}

for name, size in square_assets.items():
    resized = img.resize(size, Image.LANCZOS)
    out_path = os.path.join(assets_dir, name)
    resized.save(out_path, format='PNG')
    print(f"Generated: {name} ({size[0]}x{size[1]})")

# Wide tile (310x150) - center the icon on the left third, leave room for text
wide_width, wide_height = 310, 150
wide_img = Image.new('RGBA', (wide_width, wide_height), (0, 0, 0, 0))
icon_size = int(wide_height * 0.7)
icon_resized = img.resize((icon_size, icon_size), Image.LANCZOS)
x_offset = (wide_height - icon_size) // 2
y_offset = (wide_height - icon_size) // 2
wide_img.paste(icon_resized, (x_offset, y_offset), icon_resized)
wide_path = os.path.join(assets_dir, 'Wide310x150Logo.png')
wide_img.save(wide_path, format='PNG')
print(f"Generated: Wide310x150Logo.png ({wide_width}x{wide_height})")

# Target-size variants for taskbar (unplated)
target_sizes = [16, 24, 32, 48, 256]
for ts in target_sizes:
    resized = img.resize((ts, ts), Image.LANCZOS)
    name = f"Square44x44Logo.targetsize-{ts}_altform-unplated.png"
    out_path = os.path.join(assets_dir, name)
    resized.save(out_path, format='PNG')
    print(f"Generated: {name} ({ts}x{ts})")

print("Asset generation complete.")
"@

$tempScript = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.py'
Set-Content -Path $tempScript -Value $pythonScript -Encoding UTF8

try {
    Write-Host "Generating MSIX assets and ICO from: $SourceImage"
    Write-Host "ICO output: $IcoOutputPath"
    Write-Host "Assets output: $AssetsOutputDir"
    Write-Host ""

    python $tempScript $SourceImage $IcoOutputPath $AssetsOutputDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Python script failed with exit code $LASTEXITCODE"
        exit 1
    }

    Write-Host ""
    Write-Host "All assets generated successfully." -ForegroundColor Green
}
finally {
    Remove-Item $tempScript -ErrorAction SilentlyContinue
}
