# WhisperKey Review System

A comprehensive automated review system for the WhisperKey codebase covering 7 critical dimensions.

## 📋 Available Reviews

| Review | Focus Area | Output File | Beads Prefix |
|--------|-----------|-------------|--------------|
| **Deployment** | MSI configuration, build process, versioning | `DEPLOYMENT_REVIEW.md` | DEPLOY-XXX |
| **Security** | Vulnerabilities, encryption, injection risks | `SECURITY_SCAN_REPORT.md` | SEC-XXX |
| **Architecture** | Design patterns, dependencies, code organization | `ARCHITECTURE_REVIEW.md` | ARCH-XXX |
| **Interface** | UI/UX, settings navigation, user experience | `INTERFACE_REVIEW.md` | UI-XXX |
| **Performance** | Memory, CPU, audio processing optimization | `PERFORMANCE_REVIEW.md` | PERF-XXX |
| **Error Handling** | Exception handling, resilience, recovery | `ERROR_HANDLING_REVIEW.md` | ERR-XXX |
| **Testing** | Test coverage, test quality, missing tests | `TESTING_REVIEW.md` | TEST-XXX |

## 🚀 Quick Start

### Run All Reviews

```powershell
# PowerShell
.\Run-FullReview.ps1

# Or Command Prompt
Run-FullReview.bat
```

### Run Specific Reviews Only

```powershell
# Security and Performance only
.\Run-FullReview.ps1 -ReviewTypes "Security,Performance"

# Testing review only
.\Run-FullReview.ps1 -ReviewTypes "Testing"
```

### Generate Reports Without Creating Issues

```powershell
# Create reports but don't add to beads
.\Run-FullReview.ps1 -SkipBeads
```

### Open Reports After Generation

```powershell
# Generate and immediately open all reports
.\Run-FullReview.ps1 -OpenReports
```

## 📖 Detailed Usage

### PowerShell Script Options

```powershell
Run-FullReview.ps1 [
    -SkipBeads                    # Don't create beads issues
    -ReviewTypes <string>         # Comma-separated list of reviews
    -OutputDirectory <path>       # Where to save reports
    -OpenReports                  # Auto-open reports when done
]
```

### Examples

```powershell
# Full review with all options
.\Run-FullReview.ps1 -ReviewTypes "All" -OutputDirectory "./reviews" -OpenReports

# Quick security check
.\Run-FullReview.ps1 -ReviewTypes "Security" -SkipBeads

# Architecture and performance focus
.\Run-FullReview.ps1 -ReviewTypes "Architecture,Performance,ErrorHandling"

# Generate reports in specific directory
.\Run-FullReview.ps1 -OutputDirectory "C:\Reviews\WhisperKey-$(Get-Date -Format 'yyyy-MM-dd')"
```

### Command Prompt (Batch File)

```cmd
:: Run all reviews
Run-FullReview.bat

:: Reports only, no beads issues
Run-FullReview.bat -SkipBeads

:: Specific reviews
Run-FullReview.bat -ReviewTypes "Security,Testing"

:: Show help
Run-FullReview.bat --help
```

## 📊 Understanding the Output

### Generated Reports

Each review generates a detailed markdown report:

- **Executive Summary** - Overall score and key findings
- **Critical Issues** - P0/P1 priority items requiring immediate attention
- **Detailed Findings** - Specific code locations with line numbers
- **Recommendations** - Concrete fixes with code examples
- **Action Plan** - Prioritized next steps

### Beads Issue Creation

Critical findings are automatically tracked in beads with:
- Unique ID (e.g., `SEC-001`, `PERF-003`)
- Priority level (P0=Critical, P1=High, P2=Medium, P3=Low)
- Detailed description
- File locations and line numbers
- Recommended fixes

### Viewing Issues

```bash
# List all open issues
bd list --status open

# View specific issue
bd show SEC-001

# Filter by priority
bd list --priority P0

# Filter by type
bd list | grep "Security"
```

## 🎯 Review Priorities

### P0 - Critical (Fix Before Release)
Security vulnerabilities, deployment blockers, crashes, data loss risks

### P1 - High (Fix Soon)
Performance issues, major UX problems, significant bugs

### P2 - Medium (Important)
Code quality, maintainability, minor UX issues

### P3 - Low (Nice to Have)
Refactoring, optimization opportunities, enhancements

## 📁 File Structure

```
WhisperKey/
├── Run-FullReview.ps1          # Main PowerShell script
├── Run-FullReview.bat          # Windows batch wrapper
├── REVIEW_SYSTEM.md            # This documentation
├── DEPLOYMENT_REVIEW.md        # Generated reports
├── SECURITY_SCAN_REPORT.md
├── ARCHITECTURE_REVIEW.md
├── INTERFACE_REVIEW.md
├── PERFORMANCE_REVIEW.md
├── ERROR_HANDLING_REVIEW.md
└── TESTING_REVIEW.md
```

## 🔧 Customization

### Adding New Review Types

Edit `Run-FullReview.ps1` and add to the `$allReviews` array:

```powershell
@{
    Name = "YourReview"
    Description = "What this review checks"
    ReportFile = "YOUR_REVIEW.md"
    BeadsPrefix = "YOUR"
}
```

### Excluding Specific Checks

Modify the review logic in the PowerShell script to skip specific files or patterns.

### Custom Output Format

The script generates markdown, but you can modify the `Write-Host` sections to output JSON, XML, or other formats.

## 📈 Review Scoring

Each review provides an overall score (0-10):

| Score | Meaning |
|-------|---------|
| 9-10 | Excellent - Minor improvements only |
| 7-8 | Good - Some issues to address |
| 5-6 | Fair - Needs work but functional |
| 3-4 | Poor - Major issues, risky to deploy |
| 0-2 | Critical - Do not deploy |

## 🏃‍♂️ CI/CD Integration

### GitHub Actions

```yaml
name: Code Review
on: [push, pull_request]
jobs:
  review:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run Full Review
        shell: pwsh
        run: |
          ./Run-FullReview.ps1 -SkipBeads -OutputDirectory ./reviews
      - name: Upload Reports
        uses: actions/upload-artifact@v3
        with:
          name: review-reports
          path: ./reviews/*.md
```

### Azure DevOps

```yaml
trigger:
  - main

steps:
  - task: PowerShell@2
    inputs:
      filePath: 'Run-FullReview.ps1'
      arguments: '-ReviewTypes "Security,Performance" -SkipBeads'
```

## 🆘 Troubleshooting

### PowerShell Execution Policy

If you get execution policy errors:

```powershell
# Temporary bypass for this session
powershell -ExecutionPolicy Bypass -File .\Run-FullReview.ps1

# Or set policy for current user
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Beads Not Found

Ensure beads (bd) is installed and in PATH:

```bash
# Verify installation
bd --version

# If not found, check installation
cd C:\Programming\WhisperKey
bd onboard
```

### Reports Not Generated

Check:
1. Write permissions in output directory
2. PowerShell version (requires 5.1 or later)
3. Available disk space

## 📝 Review Checklist

Before running reviews:

- [ ] Code compiles without errors
- [ ] All dependencies restored (dotnet restore)
- [ ] Beads CLI installed and configured
- [ ] Sufficient disk space for reports
- [ ] PowerShell execution policy allows scripts

## 🎓 Best Practices

### When to Run Reviews

- **Before major releases** - Full review (all 7 types)
- **After significant changes** - Architecture and Testing reviews
- **Security incidents** - Security review
- **Performance complaints** - Performance review
- **New features** - Interface and Testing reviews

### Review Cadence

- **Daily development** - Let CI/CD run automated reviews
- **Weekly** - Manual review of P0/P1 issues
- **Monthly** - Full comprehensive review
- **Quarterly** - External security audit

### Working with Issues

```bash
# Start working on issue
bd update SEC-001 --status in_progress

# View issue details
bd show SEC-001

# Add comment
bd comments add SEC-001 "Starting work on encryption fix"

# Close when done
bd close SEC-001
```

## 📞 Support

For issues with the review system:

1. Check this documentation
2. Review generated reports for specific errors
3. Check beads documentation: `bd --help`
4. Create an issue in the project tracker

## 🏆 Success Metrics

A healthy codebase should have:

- **Zero P0 issues** before production
- **< 5 P1 issues** at any time
- **80%+ test coverage** (from Testing review)
- **Performance score > 7**
- **Security score > 8**
- **All critical paths tested**

---

**Version:** 1.0  
**Last Updated:** January 31, 2026  
**Compatible with:** WhisperKey v1.0+
