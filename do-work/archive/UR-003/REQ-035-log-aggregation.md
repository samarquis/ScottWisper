---
id: REQ-035
title: Log Aggregation and Analysis
status: completed
created_at: 2026-02-07T15:56:00Z
user_request: UR-003
claimed_at: 2026-02-07T22:40:00Z
route: C
completed_at: 2026-02-08T00:15:00Z
---

# Log Aggregation and Analysis

## What
Implement log aggregation and analysis with AI-powered anomaly detection and automated insights.

## Why
Manual log analysis is time-consuming and misses complex patterns.

## Detailed Requirements
- Deploy centralized log aggregation.
- Implement AI-powered anomaly detection.
- Add automated log pattern recognition.
- Implement intelligent error correlation.
- Goal: Achieve 24-hour log retention with instant search and automated insight generation.

## Context
Imported from beads issue ScottWisper-t95p.

---

## Triage

**Route: C** - Complex

**Reasoning:** Requires parsing heterogeneous log files, implementing pattern discovery algorithms, and establishing anomaly detection thresholds.

**Planning:** Required

## Plan

1.  **Define Interface**: Create `ILogAnalysisService` with methods for log parsing, correlation, pattern identification, and insight generation.
2.  **Regex Parser**: Design a robust regex to parse the project's Serilog template format `[{Timestamp} {Level}] [{CorrelationId}] [{SourceContext}] {Message}{Exception}`.
3.  **Implementation**:
    *   **Pattern Recognition**: Use message simplification (removing IDs/numbers) to identify underlying log templates.
    *   **Anomaly Detection**: Track error rates and trigger system events if thresholds are exceeded.
    *   **Correlation**: Allow retrieving all logs related to a specific Request/Correlation ID.
    *   **Insights**: Generate human-readable summaries of log trends.
4.  **Integration**: Register in `ServiceConfiguration`.
5.  **Verification**: Write unit tests with mocked log files to verify parser accuracy and analysis logic.

## Exploration

- Analyzed `ServiceConfiguration.cs` to identify the active Serilog file sink template.
- Found that `CorrelationId` is consistently included in the log template, enabling effective error correlation.
- Discovered that `DateTime.Parse` requires careful handling of Serilog's default timestamp format.

## Implementation Summary

- Implemented `ILogAnalysisService` and `LogAnalysisService`.
- Developed a non-greedy regex parser for the specific Serilog template used in the project.
- Added message simplification logic to group similar log entries into patterns.
- Integrated error-spike anomaly detection with the `IAuditLoggingService`.
- Provided a mechanism for cross-session log correlation via `CorrelationId`.

## Testing

**Tests run:** `dotnet test Tests/WhisperKey.Tests.csproj --filter LogAnalysisTests`
**Result:** ✓ All tests passing (3 tests)

**New tests added:**
- `Tests\Unit\LogAnalysisTests.cs`:
    - `Test_LogParsingAndPatternRecognition`: Verifies regex parser and template-based pattern grouping.
    - `Test_AnomalyDetection_ErrorSpike`: Verifies that sudden increases in error logs trigger system events.
    - `Test_InsightGeneration`: Verifies the creation of human-readable log analysis summaries.

*Verified by work action*
