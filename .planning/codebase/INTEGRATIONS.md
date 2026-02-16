# External Integrations

**Analysis Date:** 2026-02-15

## APIs & External Services

**Primary Transcription API:**
- **OpenAI Whisper API** - Cloud speech-to-text transcription
  - Endpoint: `https://api.openai.com/v1/audio/transcriptions` (configurable)
  - Auth: Bearer token via API key
  - Models: `whisper-1` (default)
  - Rate limiting: Built-in (60 requests/minute default)
  - Fallback: Local inference when offline

**Alternative Providers:**
- Supports custom API endpoints for:
  - Azure OpenAI Service
  - Self-hosted Whisper instances
  - Compatible transcription APIs
- Endpoint validation enforced (HTTPS required)

## Data Storage

**Databases:**
- **None** - No traditional database
- Uses JSON-based file storage:
  - `%APPDATA%/WhisperKey/usersettings.json` - User preferences
  - `%APPDATA%/WhisperKey/vocabulary.json` - Custom vocabulary
  - `%APPDATA%/WhisperKey/audit/` - Audit logs (JSONL format)
  - Local cache for model metadata

**File Storage:**
- **Local filesystem** - Primary storage mechanism
- Model files stored in: `%LOCALAPPDATA%/WhisperKey/models/`
- Log files: `%APPDATA%/WhisperKey/logs/`
- Temporary audio: `%TEMP%/WhisperKey/`

**Caching:**
- In-memory caching via `MemoryCacheService`
- Model download cache
- Audio device enumeration cache
- No distributed cache

## Authentication & Identity

**API Key Management:**
- **Windows Credential Manager** - Secure API key storage
  - Uses `CredWrite`/`CredRead` Win32 APIs
  - Encryption: Windows DPAPI
  - Target name: `OpenAI_ApiKey`
  - No plaintext storage (IA-5 compliance)

**Authentication Flow:**
- API keys retrieved from secure storage at runtime
- Keys never logged or exposed in error messages
- Automatic key rotation support via `ApiKeyRotationService`
- Key validation on startup (optional)

**User Identity:**
- Windows user context only
- No separate user accounts
- Audit logging includes hashed user identifiers
- Role-based access not implemented

## Monitoring & Observability

**Structured Logging:**
- **Serilog** with multiple sinks:
  - Console (development)
  - Rolling file (30-day retention)
  - **Seq** (optional, localhost:5341)
- Enrichers: CorrelationId, Environment, Process, MachineName
- Log levels: Debug → Fatal
- Sensitive data redaction in logs

**Metrics & Telemetry:**
- Performance monitoring via `PerformanceMonitoringService`
- Business metrics via `BusinessMetricsService`
- Custom metrics: transcription duration, audio quality, injection latency
- No external APM (Application Performance Monitoring) integration

**Error Tracking:**
- Structured error logging with correlation IDs
- `ErrorReportingService` for error aggregation
- No external error tracking service (Sentry, Raygun, etc.)

## CI/CD & Deployment

**Build Pipeline:**
- PowerShell scripts for automation:
  - `build-msi.ps1` - MSI generation
  - `Run-FullReview.ps1` - Quality gates
- WiX Toolset for installer creation
- No cloud CI detected (local builds)

**Hosting:**
- Desktop application (not hosted)
- MSI distribution
- No containerization

**Version Management:**
- Git-based versioning
- `version.props` for assembly versions
- Semantic versioning (1.0.0 format)

## Environment Configuration

**Required Configuration:**
- No hard-required external services (works offline)
- Optional: OpenAI API key for cloud mode
- Optional: Seq URL for centralized logging
- Optional: Webhook endpoint for integrations

**Secrets Location:**
- Windows Credential Manager (production)
- Environment variables (development only, deprecated)
- User secrets (not implemented)

**Configuration Hierarchy:**
1. `appsettings.json` (base)
2. `%APPDATA%/WhisperKey/usersettings.json` (user overrides)
3. Environment variables (if enabled)
4. Windows Credential Manager (secrets)

## Webhooks & Callbacks

**Outgoing Webhooks:**
- **WebhookService** - Event notification system
  - Configurable endpoint URL
  - Event types: TranscriptionCompleted, TextInjected, SettingsChanged, Error
  - Authentication: HMAC signature or Bearer token
  - Retry policy: Exponential backoff (3 attempts)
  - Circuit breaker: Opens after 5 consecutive failures
  - Payload sanitization for PII removal

**Webhook Configuration:**
```csharp
{
  EndpointUrl: "https://api.example.com/webhooks",
  Secret: "webhook-signing-secret",
  AuthToken: "optional-bearer-token",
  TimeoutSeconds: 30,
  Enabled: true,
  TriggerEvents: ["TranscriptionCompleted", "Error"]
}
```

**Incoming Webhooks:**
- Not implemented
- No webhook receiver endpoints

**Callbacks:**
- WPF events for UI updates
- Service-to-service events via EventHandler
- No external callback URLs registered

## Local AI Model Infrastructure

**Model Sources:**
- Whisper models downloaded from HuggingFace (indirectly via Whisper.net)
- Vosk models from alphacephei.com (optional)
- Model verification via SHA-256 checksums

**Model Management:**
- `ModelManagerService` handles downloads
- `LocalInferenceService` for execution
- CPU-only inference (GPU acceleration configurable but disabled by default)
- Model formats: GGML (Whisper), binary (Vosk)

## Security Integrations

**Windows Security:**
- DPAPI for data protection
- Windows Credential Manager for secrets
- Windows Event Log for audit events (optional)
- UAC compliance for installer

**Network Security:**
- HTTPS enforcement for API endpoints
- SSL certificate validation (configurable)
- No proxy authentication required (basic proxy support)

---

*Integration audit: 2026-02-15*
