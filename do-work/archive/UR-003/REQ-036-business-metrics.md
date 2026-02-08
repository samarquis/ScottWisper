---
id: REQ-036
title: Business Metrics Dashboard
status: completed
created_at: 2026-02-07T15:56:00Z
user_request: UR-003
claimed_at: 2026-02-07T22:40:00Z
route: C
completed_at: 2026-02-08T00:45:00Z
---

# Business Metrics Dashboard

## What
Create a comprehensive business metrics dashboard with KPI tracking and trend analysis.

## Why
Business stakeholders need visibility into application performance and user behavior.

## Detailed Requirements
- Build a dashboard with business KPIs.
- Implement user journey analytics.
- Add conversion metrics tracking.
- Add trend analysis.
- Goal: Provide real-time insights and support data-driven decision making.

## Context
Imported from beads issue ScottWisper-4v2l.

---

## Triage

**Route: C** - Complex

**Reasoning:** Requires defining business KPIs, implementing a persistence layer for snapshots, and creating an aggregation engine for trend analysis.

**Planning:** Required

## Plan

1.  **Define Models**: Create `BusinessKpiSnapshot`, `MetricTrend`, and `DataPoint` models in `src\Models\BusinessMetrics.cs`.
2.  **Define Interface**: Create `IBusinessMetricsService` with methods for recording events, getting snapshots, and generating reports.
3.  **Implementation**:
    *   **Event Recording**: Track "TranscriptionCompleted" (with cost) and "TranscriptionError".
    *   **Aggregation**: Maintain running totals and calculate success rates.
    *   **Persistence**: Use `JsonDatabaseService` to store snapshots locally.
    *   **Reporting**: Automated generation of daily usage and cost reports.
4.  **Integration**:
    *   Register in `ServiceConfiguration`.
    *   Update `EventCoordinator` to hook into core service events and feed the metrics engine.
5.  **Verification**: Write unit tests for event capture and trend calculation.

## Exploration

- Identified `TranscriptionCompleted` and `TranscriptionError` as the primary business value events.
- Found that `JsonDatabaseService` provides an efficient local storage mechanism for time-series snapshots.
- Confirmed `EventCoordinator` is the correct place to tap into the application event stream.

## Implementation Summary

- Created `src\Models\BusinessMetrics.cs` for KPI and trend data.
- Implemented `IBusinessMetricsService` and `BusinessMetricsService`.
- Integrated metrics capture into `EventCoordinator`.
- Supported daily report generation with automated audit logging.
- Wired up dependency injection across `ServiceConfiguration` and `App.xaml.cs`.

## Testing

**Tests run:** `dotnet test Tests/WhisperKey.Tests.csproj --filter BusinessMetricsTests`
**Result:** ✓ All tests passing (4 tests)

**New tests added:**
- `Tests\Unit\BusinessMetricsTests.cs`:
    - `Test_RecordEvent_TranscriptionCompleted`: Verifies incrementing transcription counts and cost tracking.
    - `Test_RecordEvent_TranscriptionError`: Verifies error tracking and success rate recalculation.
    - `Test_TrendAnalysis`: Verifies retrieval of historic data points over a duration.
    - `Test_ReportGeneration`: Verifies formatting and logging of business insights.

*Verified by work action*
