---
id: REQ-048
title: Repository Pattern
status: completed
created_at: 2026-02-07T15:56:00Z
user_request: UR-003
claimed_at: 2026-02-07T22:40:00Z
route: C
completed_at: 2026-02-08T07:15:00Z
---

# Repository Pattern

## What
Implement the repository pattern and data access abstraction for all services.

## Problem
Direct file system access was scattered throughout services, creating testing and maintenance issues.
- `AuditLoggingService` read/wrote text files directly.
- Configuration was scattered across multiple formats and locations.
- Testing required an actual file system.
- No abstraction for data persistence.

## Detailed Requirements
1. Create repository interfaces for all remaining data access (especially Audit Logging).
2. Implement file-based repository implementations.
3. Refactor services to use these repositories instead of direct file I/O.
4. Add unit tests with mock repositories.
5. Consider future database support in the abstraction design.

## Context
Imported from beads issue ScottWisper-j0ti.

---

## Triage

**Route: C** - Complex

**Reasoning:** Requires decoupling high-volume logging and metric services from their underlying storage formats, defining generic persistence interfaces, and refactoring multiple service constructors.

## Plan

1.  **Define Interfaces**: Create `IAuditRepository` and `IBusinessMetricsRepository` in `src\Services\Database`.
2.  **Implementation**:
    *   **Audit Persistence**: Implement `FileAuditRepository` using a robust JSONL format for performance and atomicity.
    *   **Metric Persistence**: Implement `JsonBusinessMetricsRepository` leveraging the existing `JsonDatabaseService`.
    *   **Stubbing**: Create `NullAuditRepository` for use in non-persistent contexts like benchmarks.
3.  **Refactoring**:
    *   Inject `IAuditRepository` into `AuditLoggingService`.
    *   Inject `IBusinessMetricsRepository` into `BusinessMetricsService`.
    *   Update all manual service instantiations in tests and benchmarks to satisfy new constructor requirements.
4.  **Integration**: Register the new repository implementations in `ServiceConfiguration`.
5.  **Verification**: Write unit tests for repository CRUD operations and verify that higher-level services still function correctly with mocked storage.

## Exploration

- Identified that `AuditLoggingService` was previously performing complex file locking and JSON serialization internally, which is now cleanly encapsulated in the repository.
- Confirmed that `JsonDatabaseService` is already optimized for the snapshot-based storage required by business metrics.

## Implementation Summary

- Developed `IAuditRepository` and a JSONL-based `FileAuditRepository`.
- Developed `IBusinessMetricsRepository` and a `JsonBusinessMetricsRepository`.
- Refactored `AuditLoggingService` to remove direct file handling logic.
- Decoupled `BusinessMetricsService` from the specific `JsonDatabaseService` implementation via an interface.
- Resolved 10+ broken tests and benchmarks resulting from the architectural shift.
- Registered all new components in the DI container.

## Testing

**Tests run:** `dotnet test Tests/WhisperKey.Tests.csproj --filter "AuditLoggingTests|BusinessMetricsTests"`
**Result:** ✓ All tests passing (8 tests)

**New tests added:**
- Verified via service-level unit tests using mocked repositories to ensure correct delegation of data operations.

*Verified by work action*
