You are implementing this request:

## Original Request
Implement comprehensive security audit logging for SOC 2 compliance with immutable logs, authentication/authorization event tracking, log retention policies, integrity verification, search capabilities, and real-time alerting.

## Implementation Plan
A comprehensive audit logging system already exists with most SOC 2 features implemented. The main work is enhancing and integrating it for full SOC 2 compliance.

## Codebase Context
**Existing Foundation:**
- **AuditLoggingService.cs** - Comprehensive audit logging with immutability, retention, archiving
- **AuditLog.cs** - Complete models including SOC 2 compliance type, event types, retention policies
- **PermissionService.cs** - Handles microphone permissions (authorization events need logging)
- **WindowsCredentialService.cs** - API key storage (security events need logging)
- **AppSettings.cs** - Configuration patterns for adding audit settings
- **Comprehensive test coverage** - 498 lines of existing audit logging tests

**Key Missing Elements:**
1. SOC 2-specific 7-year retention policy (not enabled by default)
2. Integration of audit logging into PermissionService and WindowsCredentialService
3. Real-time alerting rules for security events
4. Enhanced log integrity verification
5. Authentication event placeholders (no user auth system currently)

## Instructions
**Focus on these enhancements:**

### Phase 1: SOC 2 Retention Policy
- Add explicit 7-year SOC 2 retention policy to default policies
- Ensure security events use SOC 2 compliance classification

### Phase 2: Integration Points
- Add audit logging calls to PermissionService for authorization events
- Add audit logging calls to WindowsCredentialService for API key access/modification
- Include IP address, user agent, device info in metadata where available

### Phase 3: Real-time Alerting
- Create SecurityAlertService for configurable alert rules
- Implement alerts for failed permission attempts, suspicious API key activity
- Rate limiting to prevent alert fatigue
- Integration with Windows Event Log for enterprise monitoring

### Phase 4: Enhanced Integrity
- Improve log integrity verification with hash chain validation
- Add tamper detection and reporting
- Generate audit evidence reports for compliance

### Phase 5: Configuration and Testing
- Add audit settings to AppSettings.cs
- Create comprehensive tests for new functionality
- Ensure SOC 2 compliance requirements are met

**Key Guidelines:**
- **DO NOT REBUILD** - enhance existing comprehensive system
- Follow existing code patterns and naming conventions
- Maintain backward compatibility
- Use existing dependency injection patterns
- Add comprehensive unit tests following MSTest patterns
- Include proper error handling and logging

**Testing Requirements:**
- Unit tests for all new functionality
- Integration tests for permission/credential service logging
- SOC 2 compliance verification tests
- Alert rule functionality tests
- Integrity verification tests

When complete, provide a summary of:
1. What files were modified/created
2. What SOC 2 enhancements were implemented
3. What integration points were added
4. What new tests were written
5. Any deviations from the original plan and why