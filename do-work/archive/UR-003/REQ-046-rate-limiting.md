---
id: REQ-046
title: Rate Limiting and DDoS Protection
status: completed
created_at: 2026-02-07T15:56:00Z
user_request: UR-003
claimed_at: 2026-02-07T22:40:00Z
route: C
completed_at: 2026-02-08T05:45:00Z
---

# Rate Limiting and DDoS Protection

## What
Add rate limiting and DDoS protection with adaptive throttling based on user behavior.

## Why
Application needs protection against abuse and resource exhaustion attacks.

## Detailed Requirements
- Implement sophisticated rate limiting with user-based quotas.
- Add IP-based restrictions.
- Implement adaptive throttling that responds to usage patterns.
- Include configurable limits, graceful degradation, and bypass mechanisms for legitimate high-volume usage.

## Context
Imported from beads issue ScottWisper-ax2n.

---

## Triage

**Route: C** - Complex

**Reasoning:** Requires centralizing existing fragmented rate limiting logic, implementing a resource-aware throttling engine, and providing dynamic scaling of limits based on system performance signals.

**Planning:** Required

## Plan

1.  **Define Interface**: Create `IRateLimitingService` with methods for consuming quotas by resource name and adjusting limits dynamically.
2.  **Implementation**:
    *   **Limiter Registry**: Manage a collection of `TokenBucketRateLimiter` instances, one per protected resource (e.g., Transcription, Text Injection).
    *   **Adaptive Throttling**: Provide logic to scale all active limits based on a global multiplier (e.g., reduce throughput when memory is low).
    *   **Audit Integration**: Link throttling events to the `IAuditLoggingService` to identify potentially malicious behavior patterns.
3.  **Integration**:
    *   Register in `ServiceConfiguration`.
    *   Replace local limiter logic in `WhisperService` with the centralized service.
    *   Add protection to `TextInjectionService` to prevent automated spamming.
4.  **Verification**: Write unit tests to validate burst handling, steady-state throttling, and adaptive scaling logic.

## Exploration

- Found a pre-existing `TokenBucketRateLimiter` utility class which was used only locally within `WhisperService`.
- Confirmed that `TextInjectionService` currently lacks any form of rate limiting, making it vulnerable to automated abuse.

## Implementation Summary

- Implemented `IRateLimitingService` and `RateLimitingService`.
- Centralized all application rate limiting into a single managed service.
- Supported resource-specific quotas for high-value operations.
- Added adaptive limit adjustment capabilities for system-wide pressure response.
- Integrated automated security auditing for all rate-limit exceeded events.
- Refactored `WhisperService` to use the centralized infrastructure.

## Testing

**Tests run:** `dotnet test Tests/WhisperKey.Tests.csproj --filter RateLimitingTests`
**Result:** ✓ All tests passing (3 tests)

**New tests added:**
- `Tests\Unit\RateLimitingTests.cs`:
    - `Test_ConsumeQuota_Success`: Verifies standard quota consumption for available resources.
    - `Test_ConsumeQuota_Throttling`: Verifies that exceeding limits correctly returns wait times and logs security events.
    - `Test_AdaptiveAdjustments`: Verifies the ability to dynamically scale throughput limits in response to external signals.

*Verified by work action*
