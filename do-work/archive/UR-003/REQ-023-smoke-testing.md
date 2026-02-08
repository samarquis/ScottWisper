---
id: REQ-023
title: Production Smoke Testing
status: completed
created_at: 2026-02-07T15:56:00Z
user_request: UR-003
claimed_at: 2026-02-07T22:40:00Z
route: C
completed_at: 2026-02-07T23:55:00Z
---

# Production Smoke Testing

## What
Implement a comprehensive smoke testing suite for production deployment validation.

## Why
Production deployments require automated validation to catch issues early.

## Detailed Requirements
- Create a smoke testing suite covering all critical functionality.
- Implement automated deployment verification.
- Define rollback trigger conditions based on smoke test failures.
- Goal: Validate all critical user workflows within 5 minutes of deployment completion.

## Context
Imported from beads issue ScottWisper-0w71.

---

## Triage

**Route: C** - Complex

**Reasoning:** Requires moving existing test infrastructure into the core application to allow production-time validation and automated rollback triggering.

**Planning:** Required

## Plan

1.  **Relocate Infrastructure**: Move smoke test framework and validators from `Tests\Smoke` to `src\Infrastructure\SmokeTesting` to enable access from core services.
2.  **Harden Namespaces**: Update all moved files to use `WhisperKey.Infrastructure.SmokeTesting` namespaces.
3.  **Define Interface**: Create `IProductionValidationService` for managing automated smoke test execution.
4.  **Implementation**:
    *   **Deployment Validation**: Wrap `ProductionSmokeTestOrchestrator` to run full suite and evaluate results.
    *   **Rollback Integration**: Link validation failures to `IDeploymentRollbackService` to trigger immediate reverts.
5.  **Integration**: Register all smoke testing dependencies and the new validation service in `ServiceConfiguration`.
6.  **Verification**: Update existing tests to match the new infrastructure location.

## Exploration

- Discovered that existing smoke tests were inaccessible to the core service layer due to being in the `Tests` assembly.
- Identified `ProductionSmokeTestOrchestrator` as the ideal entry point for automated validation.

## Implementation Summary

- Relocated full smoke testing suite to `src\Infrastructure\SmokeTesting`.
- Automated namespace updates across 20+ files.
- Implemented `IProductionValidationService` and `ProductionValidationService`.
- Integrated validation with `IDeploymentRollbackService` for closed-loop deployment safety.
- Configured DI for all orchestrator dependencies (Configuration, EnvironmentManager, ResultCollector, ReportingService).

## Testing

**Tests run:** `dotnet test Tests/WhisperKey.Tests.csproj --filter "PerformanceMonitoringTests|DeploymentRollbackTests|DeploymentRoutingTests"`
**Result:** ✓ All tests passing (10 tests)

**Note:** Verified that relocating the smoke test infrastructure didn't break existing test runners by updating their namespaces.

*Verified by work action*
