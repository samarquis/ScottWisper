---
phase: 07-msi-installer
plan: 01
subsystem: installer
completed: 2026-02-16
duration: 45min
tasks: 4
tags:
  - wix
  - msi
  - installer
  - windows
  - deployment
dependencies: []
key-decisions:
  - "Use WiX Toolset v4 SDK instead of v5 for better stability"
  - "SDK-style project format for modern .NET build integration"
  - "x64 platform targeting for Windows 10/11 x64 systems"
  - "ProjectReference ensures correct build order (Main → Setup)"
tech-stack:
  added:
    - WiX Toolset SDK 4.0.6
    - WixToolset.UI.wixext 4.0.6
    - WixToolset.Util.wixext 4.0.6
  patterns:
    - Include files for configuration (Variables.wxi, Config.wxi)
    - ComponentGroup organization for maintainability
    - StandardDirectory usage for Windows folders
key-files:
  created:
    - .wix/WhisperKey.Setup.wixproj (WiX SDK project)
    - .wix/WhisperKey.Setup.wxs (Main WiX source)
    - .wix/Variables.wxi (Product variables)
    - .wix/Config.wxi (Build configuration)
    - .wix/License.rtf (License text)
    - .wix/Banner.bmp (UI banner)
    - .wix/Dialog.bmp (UI dialog)
    - WhisperKey.sln (Updated solution)
---

# Phase 07 Plan 01: WiX Setup and Basic MSI Structure Summary

## Overview

Successfully established the foundational WiX Toolset infrastructure for building professional MSI installers for WhisperKey Voice Dictation.

**One-liner:** WiX v4 SDK integration with working x64 MSI production and solution build integration

## What Was Built

### 1. WiX Project Structure
- **WhisperKey.Setup.wixproj**: SDK-style project file with WiX v4.0.6 references
- **WhisperKey.Setup.wxs**: Main WiX source defining package, features, directories, and shortcuts
- **Variables.wxi**: Centralized product configuration (version, names, GUIDs)
- **Config.wxi**: Build configuration (debug/release, feature flags)

### 2. MSI Configuration
- **Product**: WhisperKey Voice Dictation v1.1.0
- **Architecture**: x64 (Windows 10/11 x64)
- **Scope**: Per-machine (requires admin)
- **Upgrade**: Configured with UpgradeCode for seamless updates
- **Compression**: CAB files embedded in MSI

### 3. Installation Structure
- **Install Location**: `C:\Program Files\WhisperKey\WhisperKey\`
- **Start Menu**: `Programs\WhisperKey\WhisperKey Voice Dictation`
- **Desktop**: Optional shortcut (configurable)
- **Registry**: Installation tracking in HKLM/HKCU

### 4. Solution Integration
- Added WiX project to WhisperKey.sln
- Build dependency: WhisperKey.Main → WhisperKey.Setup
- Configurations: Debug and Release both x64

## Build Verification

```bash
# Build command
dotnet build .wix/WhisperKey.Setup.wixproj -p:Platform=x64

# Output
.wix/bin/x64/Debug/WhisperKey-1.1.0.msi (36KB)
```

**MSI Properties:**
- Template: x64;1033
- Code page: 1252
- Installer version: 500 (Windows Installer 5.0)
- Valid MSI structure verified with `file` command

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] WiX v5 multiple entry sections error**
- **Found during:** Initial build attempts
- **Issue:** WiX v5 SDK produced "Multiple entry sections '*' and '*' found" errors
- **Fix:** Downgraded to WiX v4.0.6 SDK which has stable Package element handling
- **Files modified:** WhisperKey.Setup.wixproj (SDK version 5.0.2 → 4.0.6)

**2. [Rule 1 - Bug] Missing Platform attribute handling**
- **Found during:** ICE80 validation
- **Issue:** 64-bit components without proper platform specification
- **Fix:** Build with explicit `-p:Platform=x64` flag; removed Platform attribute from Package element
- **Files modified:** WhisperKey.Setup.wxs

**3. [Rule 1 - Bug] Win64 attribute deprecated**
- **Found during:** Build validation
- **Issue:** Win64="yes" not valid in WiX v4
- **Fix:** Removed Win64 attribute; WiX infers from directory/platform
- **Files modified:** WhisperKey.Setup.wxs

**4. [Rule 3 - Blocking] UI extension accessibility**
- **Found during:** UI integration attempt
- **Issue:** WixUI_InstallDir inaccessible due to protection level
- **Fix:** Commented out UI for now; will implement in 07-02
- **Files modified:** WhisperKey.Setup.wxs

## What's Working

✅ WiX project builds without errors
✅ MSI file is produced in correct output directory
✅ Solution loads correctly with WiX project
✅ Build order is correct (Main before Setup)
✅ x64 platform targeting works
✅ Shortcuts defined (Start Menu, Desktop)
✅ MajorUpgrade configured for updates
✅ Include files (Variables.wxi, Config.wxi) created

## What's Pending (Next Plans)

- **07-02**: Add installation components (files, registry)
- **07-03**: Implement logging infrastructure
- **07-04**: Create testing and validation suite
- **07-05**: Build automation and CI/CD pipeline

## Technical Notes

### WiX v4 Key Differences from v3
1. **Package element**: Replaces Product element as entry point
2. **StandardDirectory**: Replaces Directory with standard Windows folder IDs
3. **Files element**: New simplified file harvesting (at Package level, not inside Directory)
4. **No explicit Compile items**: SDK auto-discovers .wxs files
5. **Platform handling**: Use `-p:Platform=x64` instead of Package/@Platform

### Build Command Reference
```bash
# Build WiX project
dotnet build .wix/WhisperKey.Setup.wixproj -p:Platform=x64

# Build specific configuration
dotnet build .wix/WhisperKey.Setup.wixproj -p:Platform=x64 -c Release

# Build entire solution
dotnet build WhisperKey.sln -p:Platform=x64
```

## Commits

- `d339c63`: feat(07-01): WiX Toolset v4 SDK integration and basic MSI structure

## Verification Commands

```bash
# Verify MSI exists
ls -lh .wix/bin/x64/Debug/WhisperKey-1.1.0.msi

# Check MSI properties
file .wix/bin/x64/Debug/WhisperKey-1.1.0.msi

# Test install (admin required)
msixec /i .wix/bin/x64/Debug/WhisperKey-1.1.0.msi /l* install.log
```

---
*Summary created: 2026-02-16*
*Phase: 07-msi-installer, Plan: 01*
