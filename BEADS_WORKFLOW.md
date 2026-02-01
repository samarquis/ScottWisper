# Beads Integrated Review Workflow

This guide shows how to use the review system as part of your beads workflow.

## 🚪 Production Review Gate Epic

**Epic ID:** `WhisperKey-qku`

This epic represents the final validation gate before production deployment. It contains 8 tasks that must all pass before release.

### Epic Structure

```
EPIC: Final Production Review Gate (WhisperKey-qku)
│
├── TASK: Run Deployment Review (WhisperKey-md1)
├── TASK: Run Security Review (WhisperKey-wfh)
├── TASK: Run Architecture Review (WhisperKey-svs)
├── TASK: Run UI/UX Review (WhisperKey-c35)
├── TASK: Run Performance Review (WhisperKey-e9w)
├── TASK: Run Error Handling Review (WhisperKey-ivi)
├── TASK: Run Testing Review (WhisperKey-qsw)
└── TASK: Generate Final Review Report (WhisperKey-b43)
```

## 🎯 Usage Patterns

### Pattern 1: Run Individual Review Task

Run a specific review and update its beads task automatically:

```powershell
# Run deployment review and update task status
.\Run-FullReview.ps1 `
    -BeadsTaskId "WhisperKey-md1" `
    -ReviewTypes "Deployment"

# The script will:
# 1. Set task status to "in_progress"
# 2. Run the deployment review
# 3. Check for critical issues
# 4. Update task to "completed" if pass, or keep "open" if fail
```

### Pattern 2: Run Full Production Gate

Run all 7 reviews as the final production gate:

```powershell
# Run full review gate
.\Run-FullReview.ps1 `
    -BeadsEpicId "WhisperKey-qku" `
    -RunAsGate `
    -FailOnCritical

# The script will:
# 1. Run all 7 reviews
# 2. Check each review's pass criteria
# 3. Display go/no-go decision
# 4. Update epic status if all pass
# 5. Exit with error code 1 if FailOnCritical and any review fails
```

### Pattern 3: CI/CD Integration

Use in GitHub Actions or Azure DevOps:

```yaml
# GitHub Actions example
- name: Production Review Gate
  shell: pwsh
  run: |
    .\Run-FullReview.ps1 `
      -BeadsEpicId "WhisperKey-qku" `
      -RunAsGate `
      -FailOnCritical
  continue-on-error: false
```

### Pattern 4: Automated Task Completion

Auto-close task on successful review:

```powershell
# Run and auto-close if pass
.\Run-FullReview.ps1 `
    -BeadsTaskId "WhisperKey-wfh" `
    -ReviewTypes "Security" `
    -AutoCloseOnPass
```

## 📊 Pass Criteria

Each review has specific criteria that must be met:

| Review | Pass Criteria | Required Score |
|--------|--------------|----------------|
| **Deployment** | Zero P0 deployment issues | ≥ 7/10 |
| **Security** | Zero P0 security issues | ≥ 8/10 |
| **Architecture** | Zero P0 architecture issues | ≥ 6/10 |
| **UI/UX** | Zero P0 UI issues | ≥ 7/10 |
| **Performance** | Zero P0 performance issues | ≥ 6/10 |
| **Error Handling** | Zero P0 error handling issues | ≥ 7/10 |
| **Testing** | Zero P0 issues + no untested critical services | ≥ 7/10 |

## 🔄 Typical Workflow

### Phase 1: Development

1. **Implement features/fixes**
   ```bash
   bd update FEATURE-001 --status in_progress
   # ... do work ...
   bd update FEATURE-001 --status completed
   ```

2. **Run individual reviews as needed**
   ```powershell
   # After security fix
   .\Run-FullReview.ps1 -ReviewTypes "Security" -BeadsTaskId "WhisperKey-wfh"
   ```

### Phase 2: Pre-Release Validation

3. **Start review gate epic**
   ```bash
   bd update WhisperKey-qku --status in_progress
   ```

4. **Run all reviews**
   ```powershell
   .\Run-FullReview.ps1 -BeadsEpicId "WhisperKey-qku" -RunAsGate
   ```

5. **Check results**
   - If **PASS**: Proceed to deployment
   - If **FAIL**: Fix issues and re-run

### Phase 3: Release

6. **Generate final report**
   ```powershell
   .\Run-FullReview.ps1 -BeadsTaskId "WhisperKey-b43" -OpenReports
   ```

7. **Close epic**
   ```bash
   bd update WhisperKey-qku --status completed
   bd close WhisperKey-qku
   ```

## 🎛️ Command Reference

### Standalone Mode (No Beads)
```powershell
# Just generate reports
.\Run-FullReview.ps1 -SkipBeads
```

### Task Mode
```powershell
# Update specific task
.\Run-FullReview.ps1 -BeadsTaskId "WhisperKey-md1" -ReviewTypes "Deployment"
```

### Gate Mode
```powershell
# Full production gate
.\Run-FullReview.ps1 -BeadsEpicId "WhisperKey-qku" -RunAsGate

# With auto-fail for CI/CD
.\Run-FullReview.ps1 -BeadsEpicId "WhisperKey-qku" -RunAsGate -FailOnCritical
```

### Combined Options
```powershell
# Complete workflow
.\Run-FullReview.ps1 `
    -BeadsEpicId "WhisperKey-qku" `
    -RunAsGate `
    -FailOnCritical `
    -AutoCloseOnPass `
    -OpenReports `
    -OutputDirectory "./reviews/$(Get-Date -Format 'yyyy-MM-dd')"
```

## 📈 Monitoring Progress

### Check Epic Status
```bash
# View epic details
bd show WhisperKey-qku

# List all tasks in epic
bd children WhisperKey-qku

# Check task statuses
bd list | grep "md1\|wfh\|svs\|c35\|e9w\|ivi\|qsw\|b43"
```

### Track Completion
```bash
# Count completed tasks
bd children WhisperKey-qku | grep "completed" | wc -l

# View epic progress
bd epic status WhisperKey-qku
```

## 🚨 Handling Failures

### When a Review Fails

1. **View failing issues**
   ```bash
   # Show P0 issues for specific review
   bd list | grep "SEC-" | grep "P0"
   ```

2. **Fix the issues**
   ```bash
   bd update SEC-001 --status in_progress
   # ... fix code ...
   bd update SEC-001 --status completed
   ```

3. **Re-run the review**
   ```powershell
   # Re-run just the failed review
   .\Run-FullReview.ps1 -BeadsTaskId "WhisperKey-wfh" -ReviewTypes "Security"
   ```

4. **Re-run full gate if needed**
   ```powershell
   # After all fixes, run full gate again
   .\Run-FullReview.ps1 -BeadsEpicId "WhisperKey-qku" -RunAsGate
   ```

## 🔄 Continuous Integration

### Pre-Commit Hook

```powershell
# .git/hooks/pre-commit
# Run quick security check before commit
.\Run-FullReview.ps1 -ReviewTypes "Security" -SkipBeads
if ($LASTEXITCODE -ne 0) {
    Write-Error "Security issues found. Commit blocked."
    exit 1
}
```

### Pre-Push Hook

```powershell
# .git/hooks/pre-push
# Run architecture and testing reviews before push
.\Run-FullReview.ps1 -ReviewTypes "Architecture,Testing" -SkipBeads
if ($LASTEXITCODE -ne 0) {
    Write-Error "Review failed. Push blocked."
    exit 1
}
```

### CI/CD Pipeline

```yaml
# .github/workflows/review-gate.yml
name: Production Review Gate
on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  review:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Run Review Gate
        shell: pwsh
        run: |
          .\Run-FullReview.ps1 `
            -BeadsEpicId "WhisperKey-qku" `
            -RunAsGate `
            -FailOnCritical `
            -OutputDirectory "./reviews"
      
      - name: Upload Reports
        uses: actions/upload-artifact@v3
        with:
          name: review-reports
          path: ./reviews/*.md
        if: always()
```

## 📊 Success Metrics

### Gate Pass Criteria

For the production gate to pass:

- ✅ All 7 reviews must pass their criteria
- ✅ Zero P0 issues across all categories
- ✅ Overall codebase score ≥ 7/10
- ✅ No untested critical services
- ✅ All reports generated successfully

### Tracking Over Time

```bash
# Show review history
bd list --status closed | grep "TASK: Run.*Review"

# Compare review runs
bd history WhisperKey-qku
```

## 🎓 Best Practices

1. **Run Individual Reviews During Development**
   - Don't wait until the end
   - Run relevant review after major changes
   - Example: Run Performance review after audio processing changes

2. **Fix Issues Immediately**
   - Don't let P0 issues accumulate
   - Fix security issues within 24 hours
   - Keep P1 issue count under 5

3. **Document Decisions**
   - Add comments to beads issues explaining fixes
   - Use bd comments add to document rationale
   - Link commits to issues

4. **Automate When Possible**
   - Use pre-commit hooks for quick checks
   - Set up CI/CD to run reviews automatically
   - Use -FailOnCritical in pipelines

5. **Review Regularly**
   - Weekly: Check P0/P1 issue counts
   - Monthly: Run full review gate
   - Quarterly: External security audit

## 🆘 Troubleshooting

### Beads Not Available

```powershell
# Run in standalone mode
.\Run-FullReview.ps1 -SkipBeads
```

### Task/Epic Not Found

The script will warn but continue:
```
⚠ Could not update beads task (may not exist)
```

### Review Script Fails

Check PowerShell execution policy:
```powershell
Get-ExecutionPolicy
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## 📞 Support

For issues with the review system:

1. Check this documentation
2. Review generated markdown reports for details
3. Check beads workflow: `bd --help`
4. Run with -SkipBeads to isolate issues

---

**Integration Version:** 2.0  
**Last Updated:** January 31, 2026  
**Beads Epic:** WhisperKey-qku
