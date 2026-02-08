# ADR 0001: Service-Based Architecture and Dependency Injection

## Status
Accepted

## Context
The ScottWisper (WhisperKey) application was originally built with tightly coupled components and direct file system access. This made the codebase difficult to test, maintain, and scale for enterprise deployment requirements.

## Decision
We will transition the application to a Service-Oriented Architecture (SOA) leveraging Microsoft.Extensions.DependencyInjection.

### Key Patterns:
1.  **Interface Segregation**: Every major capability must be defined behind an interface (e.g., `IAuditLoggingService`, `IWhisperService`).
2.  **Repository Pattern**: Data persistence (Audit logs, Metrics, Settings) must be decoupled from business logic via repositories.
3.  **Cross-Cutting Concerns**: Capabilities like Alerting, APM, and Rate Limiting are implemented as specialized services injected into core workflows.
4.  **Graceful Degradation**: Non-critical services are wrapped in fallback handlers to ensure core transcription remains operational during partial failures.

## Consequences
- **Pros**:
    - Significantly improved unit testability via mocking.
    - Clear boundaries between application domains (Audio, Transcription, Injection).
    - Easier implementation of enterprise features like SOC 2 compliance.
- **Cons**:
    - Increased number of boilerplate interfaces and registration logic.
    - Complexity in managing service lifecycles (Singleton vs. Lazy).
