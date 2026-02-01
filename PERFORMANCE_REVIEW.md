# Performance & Optimization Review - ScottWisper

**Date:** January 31, 2026  
**Scope:** Memory, CPU, Audio, File I/O, UI, and Network Performance  
**Overall Score:** 4/10 - Significant optimization needed

---

## Critical Performance Issues

### 🔴 PERF-001: Excessive Task.Run Usage (Thread Pool Exhaustion)
**Priority:** CRITICAL  
**Files:** SettingsViewModel.cs (28 instances), AudioDeviceService.cs (40+ instances), App.xaml.cs

**Issue:** Massive overuse of Task.Run wrapping already-async operations:
```csharp
// BAD: Unnecessary Task.Run
_ = Task.Run(() => _settingsService.SetSelectedInputDeviceAsync(value));
_ = Task.Run(() => _settingsService.SaveAsync());
```

**Impact:** Thread pool exhaustion, performance degradation  
**Fix:** Remove Task.Run - async methods already run efficiently:
```csharp
await _settingsService.SetSelectedInputDeviceAsync(value);
await _settingsService.SaveAsync();
```

---

### 🔴 PERF-002: HttpClient Not Singleton (Socket Exhaustion)
**Priority:** CRITICAL  
**Files:** WhisperService.cs:37, SettingsWindow.xaml.cs:722

**Issue:** Creating new HttpClient per instance/using statement:
```csharp
// BAD: Creates/disposes HttpClient repeatedly
using var client = new HttpClient();
```

**Impact:** Socket exhaustion under load, TCP port exhaustion  
**Fix:** Use IHttpClientFactory or singleton:
```csharp
// In DI configuration
services.AddHttpClient<IWhisperService, WhisperService>();
```

---

### 🔴 PERF-003: Lock Contention in Audio Callback (Audio Dropouts)
**Priority:** CRITICAL  
**File:** AudioCaptureService.cs:229-242

**Issue:** Real-time audio callback acquires lock:
```csharp
private void OnDataAvailable(object? sender, WaveInEventArgs e)
{
    lock (_lockObject) // DANGEROUS: Audio thread blocking
    {
        _audioStream.Write(e.Buffer, 0, e.BytesRecorded);
    }
}
```

**Impact:** Audio dropouts, latency issues  
**Fix:** Use lock-free circular buffer or ConcurrentQueue

---

### 🔴 PERF-004: Event Handler Memory Leaks
**Priority:** HIGH  
**Files:** App.xaml.cs:62-69, AudioCaptureService.cs:65-70

**Issue:** Events subscribed but never unsubscribed in Dispose()  
**Impact:** Memory leaks, objects not garbage collected  
**Fix:** Implement proper event unsubscription in Dispose()

---

### 🔴 PERF-005: Synchronous I/O in Async Methods
**Priority:** HIGH  
**Files:** WhisperService.cs:208, CostTrackingService.cs:224, 273

**Issue:** Using sync File.ReadAllText in async context:
```csharp
var encryptedKey = File.ReadAllText(keyPath); // Synchronous
```

**Impact:** Thread blocking, poor scalability  
**Fix:** Use async file operations:
```csharp
var encryptedKey = await File.ReadAllTextAsync(keyPath);
```

---

### 🔴 PERF-006: Dispatcher.Invoke Overuse (UI Thread Contention)
**Priority:** HIGH  
**Files:** App.xaml.cs (25+ instances), FeedbackService.cs, MainWindow.xaml.cs

**Issue:** Excessive Dispatcher.Invoke calls for UI updates  
**Impact:** UI thread contention, unresponsive UI  
**Fix:** Batch UI updates:
```csharp
await Dispatcher.InvokeAsync(() => {
    UpdateStatusDisplay(status);
    UpdateQuickStats();
}, DispatcherPriority.Background);
```

---

### 🟡 PERF-007: MemoryStream Allocations (GC Pressure)
**Priority:** MEDIUM  
**Files:** AudioCaptureService.cs:154, 277, FeedbackService.cs:442

**Issue:** New MemoryStream/byte array per recording/operation  
**Impact:** GC pressure, frequent garbage collection pauses  
**Fix:** Use ArrayPool<byte> for buffer reuse

---

### 🟡 PERF-008: Coarse-Grained Locking
**Priority:** MEDIUM  
**Files:** AudioDeviceService.cs (30+ lock usages)

**Issue:** Single lock object used for all operations  
**Impact:** Thread contention, reduced parallelism  
**Fix:** Use ConcurrentDictionary or fine-grained locks

---

### 🟡 PERF-009: Settings File Writes (Disk I/O Overhead)
**Priority:** MEDIUM  
**File:** SettingsService.cs:132

**Issue:** Every settings change triggers immediate file write:
```csharp
public async Task SaveAsync()
{
    await File.WriteAllTextAsync(_userSettingsPath, json); // Every call!
}
```

**Impact:** Excessive disk I/O  
**Fix:** Implement debounced/batched writes

---

### 🟡 PERF-010: Missing ConfigureAwait(false)
**Priority:** MEDIUM  
**Files:** Throughout codebase (0 instances found)

**Issue:** No ConfigureAwait(false) in library code  
**Impact:** Potential deadlocks, UI thread capture  
**Fix:** Add ConfigureAwait(false) to all library async methods

---

## Performance Score by Category

| Category | Score | Notes |
|----------|-------|-------|
| Memory Management | 3/10 | Leaks, LOH allocations, no pooling |
| CPU Usage | 4/10 | Excessive Task.Run, string operations |
| Audio Processing | 3/10 | Lock contention, buffer allocations |
| File I/O | 4/10 | Sync I/O, repeated writes |
| UI Performance | 4/10 | Dispatcher overuse |
| Network | 5/10 | HttpClient lifetime issues |
| **Overall** | **4/10** | **Needs significant optimization** |

---

## Quick Wins

1. **Remove Task.Run wrapping** (1 day) - Immediate thread pool relief
2. **Fix HttpClient lifetime** (2 hours) - Use IHttpClientFactory
3. **Add ConfigureAwait(false)** (4 hours) - Add to all async library methods
4. **Implement debounced saves** (4 hours) - Reduce disk I/O
5. **Fix event unsubscription** (1 day) - Prevent memory leaks

---

## Long-term Optimizations

1. Replace locks with concurrent collections
2. Implement buffer pooling (ArrayPool)
3. Add cancellation token support
4. Implement proper DI for service lifetimes
5. Add performance monitoring and metrics
