---
id: REQ-044
title: Exception Hierarchy
status: completed
created_at: 2026-02-07T15:56:00Z
user_request: UR-003
claimed_at: 2026-02-07T22:40:00Z
route: C
completed_at: 2026-02-08T04:45:00Z
---

# Exception Hierarchy

## What
Implement a comprehensive exception hierarchy with domain-specific exception types.

## Why
Generic exceptions make error handling and debugging difficult.

## Detailed Requirements
- Create a detailed exception hierarchy with domain-specific exceptions.
- Implement proper exception chaining with original error preservation.
- Add structured error codes.
- Goal: Achieve 100% custom exception coverage for domain-specific error conditions.

## Context
Imported from beads issue ScottWisper-s65n.

---

## Triage

**Route: C** - Complex

**Reasoning:** Requires refactoring the error handling strategy across the entire codebase and defining a consistent metadata schema (ErrorCodes) for all domain-specific failures.

**Planning:** Required

## Plan

1.  **Define Base Class**: Create `WhisperKeyException` as the root for all domain-specific errors.
2.  **Implementation**:
    *   **Audio Domain**: Expand `AudioCaptureException` with specialized types for device busy, permission denied, and missing hardware.
    *   **Transcription Domain**: Harden `TranscriptionException` with types for model failures, API timeouts, and invalid keys.
    *   **Injection Domain**: Create `TextInjectionException` and `WindowNotFoundException`.
    *   **Security Domain**: Create `WhisperKeySecurityException` for compliance and policy violations.
    *   **Configuration Domain**: Create `ConfigurationException` for drift detection and validation errors.
3.  **Refactoring**: Update `WhisperService` and other core components to utilize these specific types.
4.  **Verification**: Write unit tests to ensure exception chaining preserves the root cause and error codes are correctly assigned.

## Exploration

- Found pre-existing but disconnected exception classes in `src\Exceptions`. Consolidated them into the new hierarchical pattern.
- Identified that `System.Security.SecurityException` conflicts with local domain types; resolved by using explicit `WhisperKeySecurityException`.

## Implementation Summary

- Established `WhisperKeyException` with mandatory `ErrorCode` support.
- Implemented 10+ specialized exception types covering all major application domains.
- Updated `WhisperService.TranscribeAudioAsync` to throw `TranscriptionException` for validation failures.
- Ensured 100% of custom exceptions support standard .NET exception chaining patterns.

## Testing

**Tests run:** `dotnet test Tests/WhisperKey.Tests.csproj --filter ExceptionHierarchyTests`
**Result:** ✓ All tests passing (4 tests)

**New tests added:**
- `Tests\Unit\ExceptionHierarchyTests.cs`:
    - `Test_TranscriptionException_Chaining`: Verifies root cause preservation and error code assignment.
    - `Test_AudioCaptureException_Properties`: Verifies domain-specific property storage (DeviceId).
    - `Test_ConfigurationDriftException`: Verifies specialized drift metadata.
    - `Test_WindowNotFoundException`: Verifies automatic error message formatting for injection failures.

*Verified by work action*
