# CI/CD Pipeline Documentation

## Overview

The WhisperKey CI/CD pipeline automates the build, test, and release process using GitHub Actions. This ensures consistent builds, automated testing, and streamlined deployment.

## Workflow Triggers

The pipeline runs on the following events:

- **Push** to `main`, `master`, or `develop` branches (excluding markdown/docs changes)
- **Pull requests** targeting `main`, `master`, or `develop` branches
- **Manual trigger** via GitHub UI (`workflow_dispatch`) with optional version override

## Jobs

### 1. Build & Test (`build-and-test`)

**Purpose:** Compile the application and run all unit tests

**Steps:**
- Checkout repository with full history
- Setup .NET 8.x SDK
- Cache NuGet packages for faster builds
- Restore dependencies for main project and tests
- Build application in Release configuration
- Build test project
- Run all unit tests with MSTest framework
- Upload test results as artifacts

**Artifacts:**
- `test-results` - TRX test result files

### 2. Publish Application (`publish`)

**Purpose:** Create a self-contained, single-file executable

**Conditions:**
- Runs only after successful build-and-test
- Only on `main`/`master` branch or manual trigger

**Configuration:**
- Self-contained deployment (includes .NET runtime)
- Single-file executable
- Target runtime: `win-x64`
- Native libraries embedded

**Artifacts:**
- `whisperkey-publish` - Complete publish folder with all dependencies

### 3. Build MSI Installer (`build-installer`)

**Purpose:** Create Windows Installer package using WiX Toolset

**Steps:**
- Download publish artifacts
- Install WiX Toolset v4.0.5
- Generate WiX components from publish folder
- Build MSI using `build-msi.ps1` script
- Version is read from `WhisperKey.csproj`

**Artifacts:**
- `whisperkey-installer` - The final `WhisperKey.msi` file

### 4. Code Quality Checks (`code-quality`)

**Purpose:** Enforce code standards and catch potential issues

**Steps:**
- Run `dotnet format` to verify code formatting
- Build with Roslyn analyzers enabled
- Treat warnings as errors during analysis

### 5. Security Scan (`security-scan`)

**Purpose:** Identify vulnerable dependencies

**Steps:**
- Run `dotnet list package --vulnerable` to check for known vulnerabilities
- Scan includes transitive dependencies
- Results uploaded as artifacts for review

### 6. Create Release (`release`)

**Purpose:** Automatically create GitHub releases with MSI installer

**Conditions:**
- Only on `main`/`master` branch or manual trigger with version specified
- Runs after successful installer build

**Features:**
- Creates GitHub release with tag `v{version}`
- Generates release notes with installation instructions
- Uploads MSI as release asset
- Marks as pre-release for non-main branches

## Environment Variables

| Variable | Value | Description |
|----------|-------|-------------|
| `DOTNET_VERSION` | `8.0.x` | .NET SDK version to use |
| `CONFIGURATION` | `Release` | Build configuration |
| `PUBLISH_DIR` | `./publish` | Output directory for published app |

## Manual Workflow Trigger

To manually run the pipeline with custom settings:

1. Go to **Actions** tab in GitHub repository
2. Select **CI/CD Pipeline**
3. Click **Run workflow**
4. Optionally specify:
   - **Version:** Override the version from .csproj
   - **Publish artifacts:** Whether to create artifacts

## Artifact Retention

| Artifact Type | Retention Period |
|---------------|------------------|
| Test results | 30 days |
| Published application | 30 days |
| MSI installer | 90 days |
| Security scan results | 30 days |

## Required Secrets

- `GITHUB_TOKEN` - Automatically provided by GitHub, used for creating releases

## Troubleshooting

### Build Failures

1. **NuGet restore errors:** Check `WhisperKey.csproj` for package reference issues
2. **Compilation errors:** Review build logs for specific file/line errors
3. **Test failures:** Download `test-results` artifact to see detailed TRX files

### MSI Build Failures

1. **WiX not found:** Ensure `build-msi.ps1` is executable and WiX toolset installs correctly
2. **File component errors:** Check for files with special characters in names

### Release Creation Failures

1. **Permission denied:** Ensure repository has Actions permissions enabled for creating releases
2. **Tag conflict:** Manually delete existing tag if re-releasing same version

## Versioning

The pipeline reads the version from `WhisperKey.csproj`:

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
</PropertyGroup>
```

When manually triggering, you can override this version.

## Future Enhancements

Potential improvements to consider:

1. **Multi-architecture builds** - Add ARM64 support for Windows on ARM
2. **Code coverage reporting** - Integrate with Coverlet and publish to Codecov
3. **SAST scanning** - Add GitHub Advanced Security or SonarQube
4. **Automatic changelog generation** - Parse conventional commits for release notes
5. **Staging environment** - Deploy to a test environment before production release
6. **Signed builds** - Code sign the MSI installer with a certificate
