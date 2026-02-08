---
id: REQ-020
title: Application Performance Monitoring
status: completed
created_at: 2026-02-07T15:56:00Z
user_request: UR-003
claimed_at: 2026-02-07T22:40:00Z
route: C
completed_at: 2026-02-07T23:15:00Z
---

# Application Performance Monitoring

## What
Deploy a comprehensive application performance monitoring (APM) solution with distributed tracing.

## Why
Production issues require detailed visibility into application performance and dependencies.

## Detailed Requirements
- Implement distributed tracing across all service boundaries.
- Add service dependency mapping.
- Establish performance baselines.
- Implement anomaly detection.
- Goal: Achieve complete request tracing and sub-second alerting.

## Context
Imported from beads issue ScottWisper-6uoq.

---

## Triage

**Route: C** - Complex

**Reasoning:** Requires new service architecture for distributed tracing using ActivitySource and performance baseline analytics.

**Planning:** Required

## Plan

1.  **Define Models**: Create `PerformanceMetric` and `TraceEntry` models in `src\Models\PerformanceMonitoring.cs`.
2.  **Define Interface**: Create `IPerformanceMonitoringService` with methods for starting traces, recording metrics, and getting dependency maps.
3.  **Implementation**: Create `PerformanceMonitoringService` using `ActivitySource` and `ActivityListener` to capture and analyze spans.
4.  **Integration**:
    *   Register in `ServiceConfiguration`.
    *   Hook into `WhisperService`, `AudioCaptureService`, and `TextInjectionService` to trace key operations.
    *   Update `ApplicationBootstrapper` to inject the monitoring service into core services.
5.  **Verification**: Write unit tests for trace creation, baseline updates, anomaly detection, and dependency mapping.

## Exploration

- Identified that `System.Diagnostics.Activity` is the appropriate standard for .NET tracing.
- Found key service methods that warrant tracing: `TranscribeAudioAsync`, `StartCaptureAsync`, and `InjectTextAsync`.
- Verified `AuditLoggingService` can be used to persist performance anomalies.

## Implementation Summary

- Created `src\Models\PerformanceMonitoring.cs` with metrics and baseline models.
- Implemented `IPerformanceMonitoringService` and `PerformanceMonitoringService`.
- Added `SystemEvent` to `AuditEventType` enum for tracking performance alerts.
- Integrated tracing into `WhisperService`, `AudioCaptureService`, and `TextInjectionService`.
- Updated `ApplicationBootstrapper.InitializeAsync` to wired up the new monitoring dependencies.

## Testing

**Tests run:** `dotnet test Tests/WhisperKey.Tests.csproj --filter PerformanceMonitoringTests`
**Result:** ✓ All tests passing (4 tests)

**New tests added:**
- `Tests\Unit\PerformanceMonitoringTests.cs`:
    - `Test_TraceCreationAndBaselineUpdate`: Verifies that activities are correctly captured and used to establish performance baselines.
    - `Test_AnomalyDetection`: Verifies that operations significantly exceeding baselines trigger audit log events.
    - `Test_MetricRecording`: Verifies recording of discrete performance metrics.
    - `Test_ServiceDependencyMap`: Verifies the generation of service dependency information.

*Verified by work action*
