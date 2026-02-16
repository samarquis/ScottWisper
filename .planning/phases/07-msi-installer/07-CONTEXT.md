# Phase 07: MSI Installer - Context and Requirements

## User Vision

Create a **thorough MSI installer** for WhisperKey with comprehensive logging capabilities that can be used for testing and debugging installation issues.

### Key Requirements

1. **Professional MSI Package**
   - Built with WiX Toolset v5
   - Supports Windows 10/11 x64
   - Proper upgrade handling
   - Clean uninstallation

2. **Comprehensive Logging**
   - Verbose MSI logging enabled by default
   - Custom action logging
   - Log preservation on failure
   - Configurable log levels
   - Windows Event Log integration

3. **Testing Infrastructure**
   - Automated install/uninstall tests
   - ICE validation (Internal Consistency Evaluators)
   - Upgrade scenario testing
   - Log file validation
   - CI/CD integration

4. **Build Automation**
   - Centralized version management
   - GitHub Actions CI/CD pipeline
   - Code signing for releases
   - Local build script
   - Release packaging

## Decisions Made

- **WiX v5**: Latest stable version with best Windows 11 support
- **Per-machine install**: Requires admin, installs for all users
- **Major upgrades**: Standard Windows Installer upgrade path
- **MSI logging**: voicewarmupx! level (maximum verbosity)
- **Custom actions**: C# with DTF (Deployment Tools Foundation)
- **Test framework**: MSTest v3 with PowerShell automation

## Technical Approach

### Component Structure
- Files harvested from build output
- Registry entries for Windows integration
- Start Menu and Desktop shortcuts
- Component-per-file rule compliance

### Logging Strategy
- Built-in MSI logging via MsiLogging property
- Custom actions for extended diagnostics
- Log preservation custom action on failure
- Windows Event Log as secondary destination
- Silent install logging support

### Testing Approach
- Unit tests for MSI validation (ICE rules)
- Integration tests for install/uninstall
- PowerShell automation for CI/CD
- Log content validation
- Clean system verification

### Build Pipeline
- GitHub Actions for CI/CD
- Version managed in version.json
- Automatic versioning for CI builds
- Manual versioning for releases
- Code signing with certificate

## Out of Scope

- ARM64 support (future consideration)
- Per-user install mode (per-machine only)
- Web installer/bootstrapper (MSI only)
- Auto-update mechanism (future phase)
- Store packaging (MSIX) (future consideration)

## Success Criteria

- [ ] MSI installs successfully on clean Windows 10/11
- [ ] Verbose logs are produced during installation
- [ ] Logs are preserved when installation fails
- [ ] All ICE validation passes without errors
- [ ] Uninstall leaves system in clean state
- [ ] Upgrade from v1.0 to v1.1 works seamlessly
- [ ] CI/CD pipeline builds and tests automatically
- [ ] Documentation enables self-service usage

## Notes

- Ensure logs don't contain sensitive information
- Consider log rotation for multiple installs
- Test on various Windows configurations
- Document common installation issues
- Include troubleshooting guide

---
*Context created: 2026-02-15*
*Phase: 07-msi-installer*
