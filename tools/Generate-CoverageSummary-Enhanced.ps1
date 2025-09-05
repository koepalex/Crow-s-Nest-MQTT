param(
    [Parameter(Mandatory = $true)]
    [string]$CoberturaFile,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputFile = "code-coverage-results.md",
    
    [Parameter(Mandatory = $false)]
    [int]$LowerThreshold = 60,
    
    [Parameter(Mandatory = $false)]
    [int]$UpperThreshold = 80,
    
    [Parameter(Mandatory = $false)]
    [switch]$FailBelowMin = $false,
    
    [Parameter(Mandatory = $false)]
    [switch]$GitHubActions = $false
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

function Write-GitHubOutput {
    param([string]$name, [string]$value)
    if ($GitHubActions) {
        Write-Output "$name=$value" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    }
}

function Write-GitHubSummary {
    param([string]$content)
    if ($GitHubActions -and $env:GITHUB_STEP_SUMMARY) {
        $content | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
    }
}

try {
    # Check if file exists
    if (-not (Test-Path $CoberturaFile)) {
        Write-Error "Cobertura file not found: $CoberturaFile"
        exit 1
    }

    Write-Host "Processing coverage file: $CoberturaFile"

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
    
    # Generate badge color and URL
    $badgeColor = Get-BadgeColor -percentage $lineRate -lower $LowerThreshold -upper $UpperThreshold
    $badgeUrl = "https://img.shields.io/badge/Code%20Coverage-$($lineRate.ToString("F0"))%25-$badgeColor?style=flat"
    
    # Console output
    Write-Host "═══════════════════════════════════════"
    Write-Host "           CODE COVERAGE SUMMARY        "
    Write-Host "═══════════════════════════════════════"
    Write-Host "Line Coverage:   $lineRate% ($linesCovered / $linesValid) $lineIndicator"
    Write-Host "Branch Coverage: $branchRate% ($branchesCovered / $branchesValid) $branchIndicator"
    Write-Host "Badge URL:       $badgeUrl"
    Write-Host "═══════════════════════════════════════"
    
    # Create markdown content
    $markdown = @"
## Code Coverage Summary

[![Code Coverage]($badgeUrl)]($badgeUrl)

| Package | Line Rate | Branch Rate | Health |
|---------|-----------|-------------|---------|
"@

    # Add package details if they exist
    if ($coverage.coverage.packages -and $coverage.coverage.packages.package) {
        foreach ($package in $coverage.coverage.packages.package) {
            $packageLineRate = [math]::Round([double]$package.'line-rate' * 100, 2)
            $packageBranchRate = [math]::Round([double]$package.'branch-rate' * 100, 2)
            $packageIndicator = Get-CoverageIndicator -percentage $packageLineRate -lower $LowerThreshold -upper $UpperThreshold
            
            $packageName = if ($package.name) { $package.name } else { "Unknown Package" }
            $markdown += "`n| $packageName | $($packageLineRate)% | $($packageBranchRate)% | $packageIndicator |"
            
            Write-Host "Package: $packageName - Line: $($packageLineRate)% Branch: $($packageBranchRate)% $packageIndicator"
        }
    }

    # Add summary row
    $markdown += "`n| **Summary** | **$($lineRate)% ($linesCovered / $linesValid)** | **$($branchRate)% ($branchesCovered / $branchesValid)** | **$lineIndicator** |"
    
    # Add threshold info
    $markdown += "`n`nMinimum allowed line rate is ``$LowerThreshold%```n"
    $markdown += "`nThresholds: $LowerThreshold% (min) / $UpperThreshold% (good)`n"
    
    # Write to file
    $markdown | Out-File -FilePath $OutputFile -Encoding utf8
    Write-Host "Coverage summary written to: $OutputFile"
    
    # GitHub Actions integration
    if ($GitHubActions) {
        Write-GitHubOutput -name "line-rate" -value $lineRate
        Write-GitHubOutput -name "branch-rate" -value $branchRate
        Write-GitHubOutput -name "lines-covered" -value $linesCovered
        Write-GitHubOutput -name "lines-valid" -value $linesValid
        Write-GitHubOutput -name "branches-covered" -value $branchesCovered
        Write-GitHubOutput -name "branches-valid" -value $branchesValid
        Write-GitHubOutput -name "badge-url" -value $badgeUrl
        
        # Write to GitHub Actions Summary
        Write-GitHubSummary -content $markdown
        
        Write-Host "GitHub Actions outputs and summary updated"
    }
    
    # Exit with error if below minimum threshold and FailBelowMin is set
    if ($FailBelowMin -and $lineRate -lt $LowerThreshold) {
        Write-Error "Line coverage ($lineRate%) is below minimum threshold ($LowerThreshold%)"
        exit 1
    }
    
    Write-Host "Coverage analysis completed successfully"
    exit 0
}
catch {
    Write-Error "Error generating coverage summary: $($_.Exception.Message)"
    Write-Error $_.ScriptStackTrace
    exit 1
}
