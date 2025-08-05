param(
    [Parameter(Mandatory = $true)]
    [string]$CoberturaFile,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputFile = "code-coverage-results.md",
    
    [Parameter(Mandatory = $false)]
    [int]$LowerThreshold = 60,
    
    [Parameter(Mandatory = $false)]
    [int]$UpperThreshold = 80
)

function Get-CoverageIndicator {
    param([double]$percentage, [int]$lower, [int]$upper)
    
    if ($percentage -lt $lower) { return "❌" }
    elseif ($percentage -lt $upper) { return "➖" }
    else { return "✔" }
}

function Get-BadgeColor {
    param([double]$percentage, [int]$lower, [int]$upper)
    
    if ($percentage -lt $lower) { return "red" }
    elseif ($percentage -lt $upper) { return "orange" }
    else { return "green" }
}

try {
    # Check if file exists
    if (-not (Test-Path $CoberturaFile)) {
        Write-Error "Cobertura file not found: $CoberturaFile"
        exit 1
    }

    # Load XML
    [xml]$coverage = Get-Content $CoberturaFile
    
    # Extract coverage data
    $lineRate = [math]::Round([double]$coverage.coverage.'line-rate' * 100, 2)
    $branchRate = [math]::Round([double]$coverage.coverage.'branch-rate' * 100, 2)
    
    $linesValid = [int]$coverage.coverage.'lines-valid'
    $linesCovered = [int]$coverage.coverage.'lines-covered'
    $branchesValid = [int]$coverage.coverage.'branches-valid'
    $branchesCovered = [int]$coverage.coverage.'branches-covered'
    
    # Generate indicators
    $lineIndicator = Get-CoverageIndicator -percentage $lineRate -lower $LowerThreshold -upper $UpperThreshold
    $branchIndicator = Get-CoverageIndicator -percentage $branchRate -lower $LowerThreshold -upper $UpperThreshold
    
    # Generate badge color
    $badgeColor = Get-BadgeColor -percentage $lineRate -lower $LowerThreshold -upper $UpperThreshold
    
    # Create markdown content
    $markdown = @"
## Code Coverage Summary

[![Code Coverage](https://img.shields.io/badge/Code%20Coverage-$($lineRate.ToString("F0"))%25-$badgeColor?style=flat)](https://shields.io/)

| Package | Line Rate | Branch Rate | Health |
|---------|-----------|-------------|---------|
"@

    # Add package details
    foreach ($package in $coverage.coverage.packages.package) {
        $packageLineRate = [math]::Round([double]$package.'line-rate' * 100, 2)
        $packageBranchRate = [math]::Round([double]$package.'branch-rate' * 100, 2)
        $packageIndicator = Get-CoverageIndicator -percentage $packageLineRate -lower $LowerThreshold -upper $UpperThreshold
        
        $markdown += "`n| $($package.name) | $($packageLineRate)% | $($packageBranchRate)% | $packageIndicator |"
    }

    # Add summary row
    $markdown += "`n| **Summary** | **$($lineRate)% ($linesCovered / $linesValid)** | **$($branchRate)% ($branchesCovered / $branchesValid)** | **$lineIndicator** |"
    
    # Add threshold info
    $markdown += "`n`nMinimum allowed line rate is ``$LowerThreshold%```n"
    
    # Write to file
    $markdown | Out-File -FilePath $OutputFile -Encoding utf8
    
    Write-Host "Coverage summary generated: $OutputFile"
    Write-Host "Line Coverage: $lineRate%"
    Write-Host "Branch Coverage: $branchRate%"
    
    # Exit with error if below minimum threshold
    if ($lineRate -lt $LowerThreshold) {
        Write-Error "Line coverage ($lineRate%) is below minimum threshold ($LowerThreshold%)"
        exit 1
    }
    
    exit 0
}
catch {
    Write-Error "Error generating coverage summary: $($_.Exception.Message)"
    exit 1
}
