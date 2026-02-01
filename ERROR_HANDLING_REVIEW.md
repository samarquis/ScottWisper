# Error Handling & Resilience Review - ScottWisper

**Date:** January 31, 2026  
**Scope:** Exception handling, recovery mechanisms, user experience  
**Overall Score:** 5/10 - Mixed quality, needs improvement

---

## Critical Error Handling Issues

### 🔴 ERR-001: Empty Catch Blocks (Silent Failures)
**Priority:** CRITICAL  
**Files:** ModelManagerService.cs:445, SettingsWindow.xaml.cs:727

**Issue:** Exceptions swallowed without logging:
```csharp
catch { }  // Silent failure - temp file may persist
catch { return false; }  // API validation fails silently
```

**Impact:** Silent failures, difficult debugging, resource leaks  
**Fix:** Always log errors even if continuing:
```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to delete temp file: {Path}", tempPath);
}
```

---

### 🔴 ERR-002: 100+ Generic catch (Exception ex) Anti-Patterns
**Priority:** CRITICAL  
**Files:** App.xaml.cs (47+ instances), AudioDeviceService.cs (12+ instances)

**Issue:** Generic exception catching hides bugs:
```csharp
catch (Exception ex)  // Catches everything including OutOfMemoryException!
{
    // Handle generically
}
```

**Impact:** Hidden bugs, unexpected behavior, security risks  
**Fix:** Catch specific exception types:
```csharp
catch (InvalidOperationException ex) when (ex.Message.Contains("tray"))
{
    _logger.LogWarning(ex, "System tray initialization failed");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error");
    throw; // Don't swallow unexpected exceptions
}
```

---

### 🔴 ERR-003: Missing ConfigureAwait(false) (Deadlock Risk)
**Priority:** CRITICAL  
**Files:** Throughout codebase (0 instances found)

**Issue:** Library code captures UI thread context:
```csharp
var response = await _httpClient.PostAsync(_baseUrl, content);
```

**Impact:** Deadlocks in UI applications, thread pool starvation  
**Fix:** Add ConfigureAwait(false):
```csharp
var response = await _httpClient.PostAsync(_baseUrl, content).ConfigureAwait(false);
```

---

### 🔴 ERR-004: No Global Exception Handler
**Priority:** HIGH  
**File:** App.xaml.cs

**Issue:** No centralized unhandled exception handling  
**Impact:** Unhandled exceptions crash application silently  
**Fix:** Implement global handlers:
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    DispatcherUnhandledException += OnDispatcherException;
}
```

---

### 🔴 ERR-005: No Retry Logic for OpenAI API
**Priority:** HIGH  
**File:** WhisperService.cs:121-130

**Issue:** Single attempt API calls with no retry:
```csharp
var response = await _httpClient.PostAsync(_baseUrl, content);
if (!response.IsSuccessStatusCode)
{
    throw new HttpRequestException($"API request failed: {response.StatusCode}");
}
```

**Impact:** Transient failures cause permanent failures  
**Fix:** Implement retry with Polly:
```csharp
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
```

---

### 🔴 ERR-006: No Circuit Breaker Pattern
**Priority:** HIGH  
**Files:** External service integrations

**Issue:** No circuit breaker for API calls  
**Impact:** Cascading failures, resource exhaustion  
**Fix:** Implement circuit breaker:
```csharp
var circuitBreaker = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));
```

---

### 🟡 ERR-007: Poor Error Messages for Users
**Priority:** MEDIUM  
**Files:** App.xaml.cs:542-544

**Issue:** Technical error details exposed to users:
```csharp
var errorMessage = recentFailures.Count > 0 
    ? $"Text injection failed ({recentFailures.Count} recent failures). Last error: {recentFailures.FirstOrDefault()?.ApplicationInfo?.ProcessName ?? "Unknown"}"
    : "Text injection failed.";
```

**Impact:** Confusing user experience  
**Fix:** User-friendly messages with technical details in logs

---

### 🟡 ERR-008: No Transaction Support for Settings
**Priority:** MEDIUM  
**File:** SettingsService.cs

**Issue:** Settings operations not atomic  
**Impact:** Corrupted settings on crash  
**Fix:** Implement transaction pattern with backup/restore

---

## Error Handling Score by Category

| Category | Score | Notes |
|----------|-------|-------|
| Exception Handling | 4/10 | Too many generic catches, empty blocks |
| Error Recovery | 6/10 | Good fallback for local→cloud, no circuit breaker |
| User Experience | 5/10 | Mix of good and poor error messages |
| Resource Cleanup | 6/10 | Good Dispose patterns in some services |
| Resilience Patterns | 3/10 | No retry, no circuit breaker |
| **Overall** | **5/10** | **Needs significant improvement** |

---

## Positive Findings

✅ **WebhookService** has excellent retry logic with exponential backoff  
✅ **AudioCaptureService** has proper Dispose implementation  
✅ **Graceful fallback** from local to cloud inference implemented  
✅ **SystemTrayService** shows appropriate error notifications  

---

## Immediate Actions

1. Replace all empty catch blocks with logging (2 days)
2. Add ConfigureAwait(false) to all async methods (1 day)
3. Implement global exception handlers (4 hours)
4. Add retry logic for API calls (1 day)
5. Create specific exception types (TranscriptionException, etc.) (2 days)

---

## Files Requiring Immediate Attention

| File | Issues | Priority |
|------|--------|----------|
| App.xaml.cs | 47 generic catches, no global handler | Critical |
| WhisperService.cs | No retry logic, missing ConfigureAwait | High |
| ModelManagerService.cs | Empty catch blocks | High |
| SettingsWindow.xaml.cs | Silent validation failures | Medium |
