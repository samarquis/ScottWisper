---
id: REQ-052
title: Technical Debt Cleanup
status: pending
created_at: 2026-02-07T15:56:00Z
user_request: UR-003
---

# Technical Debt Cleanup

## What
Clean up unused code and reduce technical debt identified during analysis.

## Issues Identified
1. **Unused Fields**: Multiple fields in `SettingsWindow`, `AudioDeviceService`, and `WhisperService`.
2. **Unused Variables**: Unused exception variables in `AudioDeviceService`.
3. **Unused Events**: `NullAuditLoggingService.EventLogged`.
4. **Unassigned Fields**: `SettingsViewModel._isDirty`.
5. **Inaccessible Methods**: Private methods that are never called.

## Detailed Requirements
1. Remove all unused fields and variables.
2. Remove unused events and delegates.
3. Clean up unassigned/unused properties.
4. Remove dead code paths.
5. Update code analysis rules to prevent future accumulation.

## Context
Imported from beads issue ScottWisper-3zad.

---
*Source: Beads issue ScottWisper-3zad*