---
id: REQ-043
title: Error Reporting System
status: completed
created_at: 2026-02-07T15:56:00Z
user_request: UR-003
claimed_at: 2026-02-07T22:40:00Z
route: C
completed_at: 2026-02-08T04:15:00Z
---

# Error Reporting System

## What
Create a comprehensive error reporting and alerting system with intelligent error classification.

## Why
Error overload makes it difficult to identify critical issues.

## Detailed Requirements
- Implement an intelligent error classification system.
- Add automated error grouping and deduplication.
- Implement severity-based alerting with escalation policies.
- Goal: Reduce false positive alerts by 90% and provide actionable error insights.

## Context
Imported from beads issue ScottWisper-uk33.

---

## Triage

**Route: C** - Complex

**Reasoning:** Requires hooking into global application exception handlers, implementing a deduplication engine based on stack trace hashing, and establishing classification rules based on exception types.

**Planning:** Required

## Plan

1.  **Define Models**: Create `ErrorReport`, `ErrorGroup`, and `ErrorReportSeverity` in `src\Models\ErrorReporting.cs`.
2.  **Define Interface**: Create `IErrorReportingService` with methods for reporting exceptions and managing error groups.
3.  **Implementation**:
    *   **Hashing**: Use SHA256 hashing of exception type and stack trace to uniquely identify and group recurring errors.
    *   **Classification**: Map exception types (OOM, Security, I/O) to appropriate severity levels.
    *   **Deduplication**: Implement logic to only trigger alerts on the 1st, 10th, 100th, etc. occurrence of a known error group.
4.  **Integration**:
    *   Register in `ServiceConfiguration`.
    *   Update `App.xaml.cs` to route all unhandled UI and background exceptions through the service.
5.  **Verification**: Write unit tests for grouping accuracy, severity mapping, and group resolution.

## Exploration

- Identified a naming conflict between `WhisperKey.Models.ErrorSeverity` and an internal enum in `UserErrorService.cs`. Resolved by using `ErrorReportSeverity`.
- Verified that `Serilog` can coexist with the new reporting service, allowing both structured file logging and intelligent error grouping.

## Implementation Summary

- Implemented `IErrorReportingService` and `ErrorReportingService`.
- Developed a robust stack-trace hashing algorithm for error deduplication.
- Integrated automated classification based on exception hierarchy.
- Established a logarithmic alerting policy to prevent alert fatigue.
- Hooked the service into the global `AppDomain` and `Dispatcher` exception handlers.

## Testing

**Tests run:** `dotnet test Tests/WhisperKey.Tests.csproj --filter ErrorReportingTests`
**Result:** ✓ All tests passing (3 tests)

**New tests added:**
- `Tests\Unit\ErrorReportingTests.cs`:
    - `Test_ErrorGroupingAndDeduplication`: Verifies that identical exceptions are correctly hashed and grouped.
    - `Test_ErrorClassification`: Verifies that system-level vs user-level exceptions are assigned correct severities.
    - `Test_ResolveErrorGroup`: Verifies the ability to clear identified error patterns from the tracking engine.

*Verified by work action*
