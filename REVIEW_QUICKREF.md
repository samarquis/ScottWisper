# ScottWisper Review Quick Reference

## ⚡ One-Liners

```bash
# Full review (everything)
.\Run-FullReview.ps1

# Just reports, no beads issues
.\Run-FullReview.ps1 -SkipBeads

# Security check only
.\Run-FullReview.ps1 -ReviewTypes "Security"

# Quick performance check
.\Run-FullReview.ps1 -ReviewTypes "Performance"

# Run and open all reports
.\Run-FullReview.ps1 -OpenReports

# Pre-release check (critical only)
.\Run-FullReview.ps1 -ReviewTypes "Security,Deployment,Performance"
```

## 📋 Common Scenarios

### Before Release
```powershell
# Complete pre-release review
.\Run-FullReview.ps1 -ReviewTypes "All" -OpenReports

# Check critical issues only
bd list --priority P0
```

### Security Audit
```powershell
# Full security scan
.\Run-FullReview.ps1 -ReviewTypes "Security"

# View security issues
bd list | findstr "SEC-"
```

### Performance Check
```powershell
# Performance review only
.\Run-FullReview.ps1 -ReviewTypes "Performance,ErrorHandling"

# Check performance issues
bd list | findstr "PERF-"
```

### Code Quality
```powershell
# Architecture and testing
.\Run-FullReview.ps1 -ReviewTypes "Architecture,Testing"

# View all issues
bd list --status open
```

### UI/UX Review
```powershell
# Interface review only
.\Run-FullReview.ps1 -ReviewTypes "Interface"

# Check UI issues  
bd list | findstr "UI-"
```

## 🔍 Viewing Results

### View All Issues
```bash
bd list --status open

# With priority
bd list --priority P0
bd list --priority P1

# Count only
bd list --status open | wc -l
```

### View Specific Review Issues
```bash
# Deployment
bd list | grep "DEPLOY-"

# Security
bd list | grep "SEC-"

# Architecture  
bd list | grep "ARCH-"

# UI/UX
bd list | grep "UI-"

# Performance
bd list | grep "PERF-"

# Error Handling
bd list | grep "ERR-"

# Testing
bd list | grep "TEST-"
```

### View Issue Details
```bash
# Show specific issue
bd show SEC-001

# Show with comments
bd show SEC-001 --comments
```

### Filter by Status
```bash
# Open issues
bd list --status open

# In progress
bd list --status in_progress

# Recently closed
bd list --status closed --since 7d
```

## 🏃‍♂️ Working with Issues

### Start Work
```bash
# Claim issue
bd update SEC-001 --status in_progress

# Add comment
bd comments add SEC-001 "Starting fix for command injection"
```

### Track Progress
```bash
# View activity
bd activity

# Check stale issues
bd stale

# View history
bd history SEC-001
```

### Complete Issue
```bash
# Close when done
bd close SEC-001

# Or with comment
bd close SEC-001 --comment "Fixed in commit abc123"
```

## 📊 Reports

### Open Generated Reports
```powershell
# Open specific report
start DEPLOYMENT_REVIEW.md
start SECURITY_SCAN_REPORT.md
start ARCHITECTURE_REVIEW.md
start INTERFACE_REVIEW.md
start PERFORMANCE_REVIEW.md
start ERROR_HANDLING_REVIEW.md
start TESTING_REVIEW.md
```

### Find Reports
```powershell
# List all review reports
Get-ChildItem *.md | Where-Object { $_.Name -like "*REVIEW*" }

# Find by date
Get-ChildItem *.md | Where-Object { $_.LastWriteTime -gt (Get-Date).AddDays(-1) }
```

## 🎯 Priority Guide

### P0 - Critical (Fix First)
- Security vulnerabilities
- Deployment blockers
- Crash risks
- Data loss risks

**Check:**
```bash
bd list --priority P0
```

### P1 - High (Fix Next)
- Performance issues
- Major bugs
- UX problems
- Reliability issues

**Check:**
```bash
bd list --priority P1
```

### P2 - Medium (Fix When Convenient)
- Code quality
- Maintainability
- Minor UX
- Optimizations

**Check:**
```bash
bd list --priority P2
```

### P3 - Low (Nice to Have)
- Refactoring
- Enhancements
- Documentation
- Polish

**Check:**
```bash
bd list --priority P3
```

## 🔧 Advanced Usage

### Custom Output Directory
```powershell
# Save to dated folder
$date = Get-Date -Format "yyyy-MM-dd"
.\Run-FullReview.ps1 -OutputDirectory "./reviews/$date"

# Save to specific path
.\Run-FullReview.ps1 -OutputDirectory "C:\Reviews\ScottWisper"
```

### Multiple Review Types
```powershell
# Security + Performance
.\Run-FullReview.ps1 -ReviewTypes "Security,Performance"

# Architecture + Testing
.\Run-FullReview.ps1 -ReviewTypes "Architecture,Testing"

# Pre-release (critical types)
.\Run-FullReview.ps1 -ReviewTypes "Security,Deployment,Performance,ErrorHandling"
```

### Skip Beads for Quick Check
```powershell
# Generate reports only
.\Run-FullReview.ps1 -SkipBeads

# Quick architecture check
.\Run-FullReview.ps1 -ReviewTypes "Architecture" -SkipBeads
```

## 🆘 Troubleshooting

### PowerShell Won't Run Script
```powershell
# Bypass execution policy
powershell -ExecutionPolicy Bypass -File .\Run-FullReview.ps1

# Or set for user
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Beads Commands Not Found
```bash
# Verify installation
which bd
bd --version

# If missing, install or navigate to project
cd C:\Programming\ScottWisper
bd onboard
```

### Reports Not Opening
```powershell
# Check if files exist
Test-Path DEPLOYMENT_REVIEW.md

# List all reports
Get-ChildItem *.md

# Open manually
notepad DEPLOYMENT_REVIEW.md
```

## 📈 Quick Stats

### Issue Counts
```bash
# Total open
bd list --status open | wc -l

# By priority
bd list --priority P0 | wc -l
bd list --priority P1 | wc -l
bd list --priority P2 | wc -l

# By type (grep)
bd list | grep -c "SEC-"
bd list | grep -c "PERF-"
bd list | grep -c "UI-"
```

### Recent Activity
```bash
# Last 24 hours
bd list --since 1d

# Last week
bd list --since 7d

# Recently updated
bd activity --limit 10
```

## 🎓 Tips

### Before Running Reviews
1. Ensure code compiles: `dotnet build`
2. Restore dependencies: `dotnet restore`
3. Check beads is ready: `bd status`

### After Running Reviews
1. Check P0 issues first
2. Review markdown reports for details
3. Prioritize by business impact
4. Create tasks/assign issues

### Regular Maintenance
```bash
# Weekly: Check P0/P1
bd list --priority P0,P1

# Monthly: Full review
.\Run-FullReview.ps1

# Quarterly: Archive old issues
bd list --status closed --since 90d
```

## 🚀 Shortcuts

Create PowerShell aliases for common commands:

```powershell
# Add to PowerShell profile
function review-all { .\Run-FullReview.ps1 }
function review-security { .\Run-FullReview.ps1 -ReviewTypes "Security" }
function review-perf { .\Run-FullReview.ps1 -ReviewTypes "Performance" }
function issues-p0 { bd list --priority P0 }
function issues-open { bd list --status open }

# Then use:
review-all
review-security
issues-p0
```

---

**Pro Tip:** Bookmark this file for quick reference!
