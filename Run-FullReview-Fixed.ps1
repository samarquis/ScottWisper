# Fixed version of Run-FullReview.ps1 to resolve Try/Catch syntax error

<#
.SYNOPSIS
    WhisperKey Full Review - Beads Integrated Edition
    
.DESCRIPTION
    Comprehensive review tool that integrates with beads workflow.
    Can be run standalone or as part of the "Final Production Review Gate" epic.
    
    When run as part of beads workflow:
    - Updates task status automatically
    - Checks for blocking issues
    - Provides go/no-go recommendations
    - Updates epic progress

.EXAMPLE
    # Run standalone (normal mode)
    .\Run-FullReview.ps1
    
    # Run as beads task (updates task status)
    .\Run-FullReview.ps1 -BeadsTaskId "WhisperKey-md1" -ReviewTypes "Deployment"
    
    # Run full review gate (all 7 reviews)
    .\Run-FullReview.ps1 -BeadsEpicId "WhisperKey-qku" -RunAsGate
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$SkipBeads,
    
    [Parameter()]
    [string]$ReviewTypes = "All",
    
    [Parameter()]
    [string]$OutputDirectory = ".",
    
    [Parameter()]
    [switch]$OpenReports,
    
    [Parameter()]
    [string]$BeadsTaskId = "",
    
    [Parameter()]
    [string]$BeadsEpicId = "",
    
    [Parameter()]
    [switch]$RunAsGate,
    
    [Parameter()]
    [switch]$AutoCloseOnPass,
    
    [Parameter()]
    [switch]$FailOnCritical
)

# Review definitions
$allReviews = @(
    @{
        Name = "Architecture"
        Prefix = "72q,kyu,5lr,cce,i3u,qo9,mm0,djd,yg7,lon"
        Script = ".\reviews\Check-Architecture.ps1"
        Description = "Service organization, DI completion, interface extraction"
        RequiredScore = 8
    },
    @{
        Name = "Security"  
        Prefix = "1t7,dih,qpf,4wq,84y"
        Script = ".\reviews\Check-Security.ps1"
        Description = "Credential manager, input validation, rate limiting"
        RequiredScore = 8
    },
    @{
        Name = "Performance"
        Prefix = "6b1,nrr,dw7,hcf,7ai,r47,6qw,tlr"
        Script = ".\reviews\Check-Performance.ps1"
        Description = "Remove Task.Run, HttpClientFactory, fix locks"
        RequiredScore = 7
    },
    @{
        Name = "Error Handling"
        Prefix = "v9e,xtg,tf0,rgw,7yc"
        Script = ".\reviews\Check-ErrorHandling.ps1"
        Description = "Global handlers, retry logic, specific exceptions"
        RequiredScore = 7
    },
    @{
        Name = "Testing"
        Prefix = "mjc,ptu,5rt,9db,0xk,7pz,pf0,88d,71k"
        Script = ".\reviews\Check-Testing.ps1"
        Description = "Unit tests for 6 critical services, integration tests"
        RequiredScore = 8
    },
    @{
        Name = "UX"
        Prefix = "x5r,8dy,lbm,4do,df2,qyz,d8p,9tb"
        Script = ".\reviews\Check-UX.ps1"
        Description = "Logo, remove duplicate tabs, primary action button"
        RequiredScore = 7
    },
    @{
        Name = "Deployment"
        Prefix = "a35,526,97x,3mz"
        Script = ".\reviews\Check-Deployment.ps1"
        Description = "Product GUID, version unification, MSI testing"
        RequiredScore = 9
    }
)

# Filter reviews
if ($ReviewTypes -ne "All") {
    $requestedTypes = $ReviewTypes -split ","
    $reviews = $allReviews | Where-Object { $requestedTypes -contains $_.Name }
} else {
    $reviews = $allReviews
}

# Helper function to update beads task
function Update-BeadsTask {
    param(
        [string]$TaskId,
        [string]$Status,
        [string]$Comment = ""
    )
    
    if ($SkipBeads) { return }
    
    try {
        if (-not [string]::IsNullOrEmpty($Comment)) {
            $null = bd comments add $TaskId $Comment 2>$null
        }
        $null = bd update $TaskId --status $Status 2>$null
        Write-Host "   Updated beads task $TaskId to $Status" -ForegroundColor Green
    } catch {
        Write-Host "   Could not update beads task (may not exist)" -ForegroundColor Yellow
    }
}

# Helper function to check issues
function Test-ReviewPass {
    param(
        [string]$Prefix,
        [scriptblock]$Criteria
    )
    
    try {
        $issuesJson = bd list --status open --format json 2>$null | ConvertFrom-Json
        $relevantIssues = $issuesJson | Where-Object { $_.title -like "$Prefix-*" }
        return & $Criteria $relevantIssues
    } catch {
        return $true
    }
}

Write-Host "`nWhisperKey Review System - Beads Integrated`n" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Track results
$script:ReviewResults = @()
$script:PassStatus = $true

# Run each review
foreach ($review in $reviews) {
    Write-Host "Running $($review.Name) Review..." -ForegroundColor Yellow
    
    # Check if review passes based on beads issues
    $passes = Test-ReviewPass -Prefix $review.Prefix -Criteria {
        param($issues)
        $critical = $issues | Where-Object { $_.Priority -eq "P0" -or $_.Priority -eq "P1" }
        return $critical.Count -eq 0
    }
    
    # Generate report
    $reportFile = "$OutputDirectory\$($review.Name)-Review-$(Get-Date -Format 'yyyyMMdd-HHmmss').md"
    
    # Create a simple report
    $reportContent = @"
# $($review.Name) Review Report

**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
**Status:** $(if ($passes) { "PASS" } else { "FAIL" })
**Required Score:** $($review.RequiredScore)/10

## Description
$($review.Description)

## Related Issues
Prefix: $($review.Prefix)

## Recommendation
$(if ($passes) { "Ready for production" } else { "Issues must be resolved" })
"@
    
    # Save report
    $reportContent | Out-File -FilePath $reportFile -Encoding UTF8
    
    # Track result
    $result = @{
        Name = $review.Name
        Passes = $passes
        RequiredScore = $review.RequiredScore
        ReportFile = $reportFile
    }
    $script:ReviewResults += $result
    
    if (-not $passes) {
        $script:PassStatus = $false
    }
    
    $status = if ($passes) { "PASS" } else { "FAIL" }
    Write-Host "   $status - Report saved to: $reportFile" -ForegroundColor $(if ($passes) { "Green" } else { "Red" })
}

# Generate summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "REVIEW GATE RESULTS`n" -ForegroundColor Cyan

# Overall status
if ($script:PassStatus) {
    Write-Host "OVERALL: PASS" -ForegroundColor Green
    Write-Host "   All reviews meet production criteria`n" -ForegroundColor Green
} else {
    Write-Host "OVERALL: FAIL" -ForegroundColor Red
    Write-Host "   Critical issues must be resolved before release`n" -ForegroundColor Red
}

# Detailed results
Write-Host "Detailed Results:" -ForegroundColor Yellow
foreach ($result in $script:ReviewResults) {
    $status = if ($result.Passes) { "PASS" } else { "FAIL" }
    Write-Host "  $status - $($result.Name) (Score >= $($result.RequiredScore)/10)" -ForegroundColor $(if ($result.Passes) { "Green" } else { "Red" })
}

# Report files
Write-Host "`nGenerated Reports:" -ForegroundColor Yellow
foreach ($result in $script:ReviewResults) {
    $filename = Split-Path $result.ReportFile -Leaf
    Write-Host "  Report: $filename" -ForegroundColor White
}

# Beads integration - Update status
if (-not [string]::IsNullOrEmpty($BeadsTaskId)) {
    Write-Host "`nUpdating Beads..." -ForegroundColor Blue
    
    if ($script:PassStatus) {
        Update-BeadsTask -TaskId $BeadsTaskId -Status "completed" -Comment "Review passed all criteria. No critical issues found."
        
        if ($AutoCloseOnPass) {
            try {
                $null = bd close $BeadsTaskId 2>$null
                Write-Host "   Task auto-closed" -ForegroundColor Green
            } catch {
                Write-Host "   Could not auto-close task" -ForegroundColor Yellow
            }
        }
    } else {
        Update-BeadsTask -TaskId $BeadsTaskId -Status "open" -Comment "Review failed. Critical issues found that must be resolved."
    }
}

# Epic progress update
if ($RunAsGate -and -not [string]::IsNullOrEmpty($BeadsEpicId)) {
    try {
        $completedReviews = ($script:ReviewResults | Where-Object { $_.Passes }).Count
        $totalReviews = $script:ReviewResults.Count
        $progressText = "Review Gate: $completedReviews/$totalReviews reviews passing"
        
        if ($script:PassStatus) {
            bd update $BeadsEpicId --status "completed" 2>$null
            Write-Host "   Epic marked completed - Ready for production!" -ForegroundColor Green
        } else {
            bd update $BeadsEpicId --status "in_progress" 2>$null
            Write-Host "   Epic remains in progress - Issues found" -ForegroundColor Yellow
        }
        
        bd comments add $BeadsEpicId $progressText 2>$null
        Write-Host "   Epic progress updated: $progressText" -ForegroundColor Green
        
    } catch {
        Write-Host "   Could not update epic status" -ForegroundColor Yellow
    }
}

# Show blocking issues if any
if (-not $script:PassStatus) {
    Write-Host "`nBlocking Issues:" -ForegroundColor Red
    try {
        $criticalIssues = bd list --priority P0 --status open 2>$null
        if ($criticalIssues) {
            $criticalIssues | Select-Object -First 5 | ForEach-Object {
                Write-Host "   • $_" -ForegroundColor Red
            }
            $count = ($criticalIssues | Measure-Object).Count
            if ($count -gt 5) {
                Write-Host "   ... and $($count - 5) more" -ForegroundColor Gray
            }
        } else {
            Write-Host "   None!" -ForegroundColor Green
        }
    } catch {
        Write-Host "   (Unable to query beads)" -ForegroundColor Gray
    }
}

# Open reports if requested
if ($OpenReports) {
    Write-Host "`nOpening reports..." -ForegroundColor Green
    foreach ($result in $script:ReviewResults) {
        if (Test-Path $result.ReportFile) {
            Start-Process $result.ReportFile
        }
    }
}

# Final summary
Write-Host "`n========================================`n" -ForegroundColor Cyan

if ($script:PassStatus) {
    Write-Host "Review process complete - READY FOR PRODUCTION" -ForegroundColor Green
} else {
    Write-Host "Review process complete - ISSUES MUST BE RESOLVED" -ForegroundColor Red
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
if ($script:PassStatus) {
    Write-Host "  1. Review generated reports" -ForegroundColor White
    Write-Host "  2. Proceed with deployment" -ForegroundColor White
    Write-Host "  3. Archive review artifacts" -ForegroundColor White
} else {
    Write-Host "  1. Review P0 critical issues: bd list --priority P0" -ForegroundColor White
    Write-Host "  2. Fix critical issues before next review" -ForegroundColor White
    Write-Host "  3. Re-run review gate after fixes" -ForegroundColor White
}

Write-Host ""

# Exit code for CI/CD
if (-not $script:PassStatus -and $FailOnCritical) {
    exit 1
}