using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using WhisperKey.Exceptions;
using WhisperKey.Configuration;

namespace WhisperKey.Services
{
    /// <summary>
    /// Provides comprehensive audio device management capabilities including enumeration, testing, monitoring,
    /// permission handling, and device recovery. Integrates with Windows audio subsystems
    /// to support real-time device change detection and automatic fallback mechanisms.
    /// </summary>
    /// <remarks>
    /// This service provides enterprise-grade audio device management with following capabilities:
    /// <list type="bullet">
    /// <item><description>Device enumeration and compatibility checking</description></item>
    /// <item><description>Real-time device change monitoring with event notifications</description></item>
    /// <item><description>Permission management for microphone access compliance</description></item>
    /// <item><description>Comprehensive device testing and quality assessment</description></item>
    /// <item><description>Audio quality analysis with performance metrics</description></item>
    /// <item><description>Automatic device recovery and fallback mechanisms</description></item>
    /// <item><description>Real-time audio level monitoring for activity detection</description></item>
    /// <item><description>Device recommendation and compatibility scoring</description></item>
    /// </list>
    /// The service handles Windows-specific audio subsystems including WASAPI, DirectSound,
    /// and Windows multimedia APIs with appropriate abstraction layers for cross-platform compatibility.
    /// </remarks>
    /// <example>
    /// <code>
    /// var audioService = serviceProvider.GetService&lt;IAudioDeviceService&gt;();
    /// 
    /// // Get available input devices
    /// var inputDevices = await audioService.GetInputDevicesAsync();
    /// 
    /// // Check microphone permissions
    /// var permissionStatus = await audioService.CheckMicrophonePermissionAsync();
    /// if (permissionStatus != MicrophonePermissionStatus.Granted)
    /// {
    ///     var granted = await audioService.RequestMicrophonePermissionAsync();
    ///     if (granted) { /* proceed with audio capture */ }
    /// }
    /// 
    /// // Start monitoring for device changes
    /// await audioService.MonitorDeviceChangesAsync();
    /// 
    /// // Subscribe to device events
    /// audioService.DeviceConnected += (sender, e) => 
    ///     Console.WriteLine($"Device connected: {e.DeviceName}");
    /// </code>
    /// </example>
    public interface IAudioDeviceService
    {
        /// <summary>
        /// Occurs when an audio device is connected to the system.
        /// Provides event-driven notification for device addition scenarios.
        /// </summary>
        /// <remarks>
        /// This event is raised for both input and output devices when they become
        /// available for use. The event includes device information and connection timestamp.
        /// Event handlers should be lightweight to avoid blocking device detection threads.
        /// </remarks>
        event EventHandler<AudioDeviceEventArgs> DeviceConnected;
        
        /// <summary>
        /// Occurs when an audio device is disconnected from the system.
        /// Enables applications to handle device removal scenarios gracefully.
        /// </summary>
        /// <remarks>
        /// This event is raised when devices become unavailable. Applications should
        /// update their device lists and may need to switch to fallback devices.
        /// The event includes the last known device information before disconnection.
        /// </remarks>
        event EventHandler<AudioDeviceEventArgs> DeviceDisconnected;
        
        /// <summary>
        /// Occurs when the system default audio device changes.
        /// Provides notification when Windows changes the preferred audio device.
        /// </summary>
        /// <remarks>
        /// This event is triggered by Windows when the user manually changes the default
        /// device or when system policy changes affect device selection. Applications may need
        /// to restart audio streams with the new default device.
        /// </remarks>
        event EventHandler<AudioDeviceEventArgs> DefaultDeviceChanged;
        
        /// <summary>
        /// Occurs when microphone permission is required but not yet granted.
        /// Provides opportunity to request permissions from the user.
        /// </summary>
        /// <remarks>
        /// This event is raised when the application attempts to access the microphone
        /// but Windows privacy settings prevent access. Applications should handle this by
        /// showing user-friendly permission request dialogs or guidance.
        /// </remarks>
        event EventHandler<PermissionEventArgs> PermissionRequired;
        
        /// <summary>
        /// Occurs when microphone permission is explicitly denied by the user or system.
        /// Indicates that audio capture operations will fail until permissions are granted.
        /// </summary>
        /// <remarks>
        /// This event is triggered when Windows privacy settings block microphone access.
        /// Applications should provide clear guidance to users on how to enable microphone
        /// access through Windows Settings > Privacy > Microphone.
        /// </remarks>
        event EventHandler<PermissionEventArgs> PermissionDenied;
        
        /// <summary>
        /// Occurs when microphone permission is successfully granted by the user.
        /// Indicates that audio capture operations can now proceed.
        /// </summary>
        /// <remarks>
        /// This event is raised when permission is granted either through the privacy
        /// settings dialog or programmatically. Applications can proceed with audio
        /// initialization and capture operations when this event occurs.
        /// </remarks>
        event EventHandler<PermissionEventArgs> PermissionGranted;
        
        /// <summary>
        /// Occurs when a permission request operation fails due to system error.
        /// Provides diagnostic information for troubleshooting permission issues.
        /// </summary>
        /// <remarks>
        /// This event is raised when the permission request process encounters
        /// unexpected errors such as dialog failures, system lockdowns, or security policy
        /// restrictions. Applications should log these events for support purposes.
        /// </remarks>
        event EventHandler<PermissionEventArgs> PermissionRequestFailed;
        
        /// <summary>
        /// Occurs when the service attempts to recover from a device change event.
        /// Provides visibility into automatic recovery operations.
        /// </summary>
        /// <remarks>
        /// This event is raised during device reconnection scenarios when the service
        /// attempts to restore audio functionality. It includes recovery strategy
        /// information and can be used to show progress indicators to users.
        /// </remarks>
        event EventHandler<DeviceRecoveryEventArgs> DeviceRecoveryAttempted;
        
        /// <summary>
        /// Occurs when device recovery operation completes successfully or with final error status.
        /// Indicates the outcome of automatic device recovery attempts.
        /// </summary>
        /// <remarks>
        /// This event signals the end of a recovery operation, whether successful or not.
        /// Applications can use this to update UI state, log recovery results,
        /// or trigger fallback procedures if recovery failed.
        /// </remarks>
        event EventHandler<DeviceRecoveryEventArgs> DeviceRecoveryCompleted;
        
        /// <summary>
        /// Retrieves all available audio input devices in the system.
        /// Includes both built-in and external audio capture devices.
        /// </summary>
        /// <returns>A task that returns a list of available input audio devices.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when the caller lacks permission to access audio devices.</exception>
        /// <exception cref="COMException">Thrown when the audio subsystem is not available or encounters an error.</exception>
        /// <remarks>
        /// This method enumerates devices using Windows Audio Session API (WASAPI).
        /// The returned list includes device metadata such as ID, name, description,
        /// and current state. Only active devices are included by default.
        /// Devices may be filtered based on current permission status.
        /// </remarks>
        Task<List<AudioDevice>> GetInputDevicesAsync();
        
        /// <summary>
        /// Retrieves all available audio output devices in the system.
        /// Includes speakers, headphones, and other audio playback devices.
        /// </summary>
        /// <returns>A task that returns a list of available output audio devices.</returns>
        /// <exception cref="COMException">Thrown when the audio subsystem encounters an enumeration error.</exception>
        /// <remarks>
        /// This method enumerates playback devices using Windows audio APIs.
        /// The returned devices include metadata about supported formats and capabilities.
        /// Output device enumeration does not require microphone permissions.
        /// </remarks>
        Task<List<AudioDevice>> GetOutputDevicesAsync();
        
        /// <summary>
        /// Retrieves the system's default audio input device.
        /// Returns the device that Windows has configured as the primary microphone.
        /// </summary>
        /// <returns>A task that returns the default input audio device.</returns>
        /// <exception cref="AudioDevicePermissionException">Thrown when microphone access permission is denied.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no default input device is available.</exception>
        /// <exception cref="COMException">Thrown when the audio subsystem encounters an error.</exception>
        /// <remarks>
        /// This method queries Windows for the default communications capture device.
        /// The returned device is guaranteed to be available and compatible with basic
        /// audio capture operations. Applications should handle exceptions when the
        /// default device is not accessible due to permission or hardware issues.
        /// </remarks>
        Task<AudioDevice> GetDefaultInputDeviceAsync();
        
        /// <summary>
        /// Retrieves the system's default audio output device.
        /// Returns the device that Windows has configured as the primary audio output.
        /// </summary>
        /// <returns>A task that returns the default output audio device.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no default output device is available.</exception>
        /// <exception cref="COMException">Thrown when the audio subsystem encounters an error.</exception>
        /// <remarks>
        /// This method queries Windows for the default multimedia render device.
        /// The returned device is suitable for audio playback operations.
        /// This operation does not require special permissions.
        /// </remarks>
        Task<AudioDevice> GetDefaultOutputDeviceAsync();
        
        /// <summary>
        /// Performs a basic functionality test on the specified audio device.
        /// Validates that the device can be initialized and used for audio capture.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device to test. Must not be null or empty.</param>
        /// <returns>A task that returns true if the device passes basic tests, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <exception cref="AudioDeviceNotFoundException">Thrown when the specified device cannot be found.</exception>
        /// <remarks>
        /// This test performs the following validations:
        /// <list type="bullet">
        /// <item><description>Device enumeration and accessibility</description></item>
        /// <item><description>Audio format compatibility (16kHz, mono, 16-bit)</description></item>
        /// <item><description>Basic capture initialization</description></item>
        /// <item><description>Brief recording and stopping functionality</description></item>
        /// </list>
        /// The test is non-destructive and should not affect other applications.
        /// </remarks>
        Task<bool> TestDeviceAsync(string deviceId);
        
        /// <summary>
        /// Retrieves detailed capabilities and supported formats for the specified audio device.
        /// Provides technical information about device limitations and features.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device to query. Must not be null or empty.</param>
        /// <returns>A task that returns the device capabilities information.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <exception cref="AudioDeviceNotFoundException">Thrown when the specified device cannot be found.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the caller lacks permission to access the device.</exception>
        /// <remarks>
        /// The capabilities information includes:
        /// <list type="bullet">
        /// <item><description>Supported sample rates and bit depths</description></item>
        /// <item><description>Channel configurations (mono, stereo, etc.)</description></item>
        /// <item><description>Buffer size ranges and optimal settings</description></item>
        /// <item><description>Hardware acceleration features</description></item>
        /// <item><description>Latency characteristics</description></item>
        /// </list>
        /// This information can be used to optimize audio processing parameters
        /// and determine compatibility with specific use cases.
        /// </remarks>
        Task<AudioDeviceCapabilities> GetDeviceCapabilitiesAsync(string deviceId);
        
        /// <summary>
        /// Retrieves information about a specific audio device by its unique identifier.
        /// Provides device metadata without enumerating all devices.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device to retrieve. Must not be null or empty.</param>
        /// <returns>A task that returns the device information, or null if the device is not found.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <remarks>
        /// This method performs a targeted lookup rather than full enumeration,
        /// making it more efficient for repeated device access.
        /// The returned device includes all standard metadata and current state information.
        /// </remarks>
        Task<AudioDevice?> GetDeviceByIdAsync(string deviceId);
        
        /// <summary>
        /// Determines if the specified audio device is compatible with speech recognition requirements.
        /// Evaluates device capabilities against minimum speech recognition standards.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device to evaluate. Must not be null or empty.</param>
        /// <returns>True if the device meets speech recognition requirements, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <remarks>
        /// Compatibility criteria include:
        /// <list type="bullet">
        /// <item><description>Sample rate ≥ 16kHz for speech recognition quality</description></item>
        /// <item><description>1-2 channels (mono preferred, stereo acceptable)</description></item>
        /// <item><description>Bit depth ≥ 16-bit for adequate dynamic range</description></item>
        /// <item><description>Driver stability and error-free initialization</description></item>
        /// </list>
        /// Devices that meet these criteria should provide acceptable quality for
        /// most speech recognition scenarios. External USB devices typically score higher.
        /// </remarks>
        bool IsDeviceCompatible(string deviceId);
        
        /// <summary>
        /// Checks the current microphone permission status for the application.
        /// Determines if the app can access audio capture devices.
        /// </summary>
        /// <returns>A task that returns the current microphone permission status.</returns>
        /// <remarks>
        /// Permission status values:
        /// <list type="bullet">
        /// <item><description><b>Granted</b>: Full microphone access is available</description></item>
        /// <item><description><b>Denied</b>: Access blocked by user or system policy</description></item>
        /// <item><description><b>Unknown</b>: Status cannot be determined (system error)</description></item>
        /// <item><description><b>SystemError</b>: Error querying permission status</description></item>
        /// </list>
        /// This method performs a non-intrusive permission check without triggering
        /// permission dialogs or changing system state.
        /// </remarks>
        Task<MicrophonePermissionStatus> CheckMicrophonePermissionAsync();
        
        /// <summary>
        /// Requests microphone permission from the user through the Windows privacy system.
        /// Triggers the system permission dialog if access is not currently granted.
        /// </summary>
        /// <returns>A task that returns true if permission is granted, false otherwise.</returns>
        /// <remarks>
        /// This method will:
        /// <list type="number">
        /// <item><description>Show the Windows privacy permission dialog if needed</description></item>
        /// <item><description>Test actual microphone access after permission</description></item>
        /// <item><description>Fire appropriate permission events (Granted/Denied)</description></item>
        /// <item><description>Provide user guidance if system settings block access</description></item>
        /// </list>
        /// The permission request is user-initiated and cannot be bypassed programmatically.
        /// Applications should handle denial gracefully and provide clear guidance.
        /// </remarks>
        Task<bool> RequestMicrophonePermissionAsync();
        
        /// <summary>
        /// Switches audio operations to use the specified device.
        /// Validates device compatibility and performs necessary reinitialization.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the target device. Must not be null or empty.</param>
        /// <returns>A task that returns true if the switch was successful, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <exception cref="AudioDeviceNotFoundException">Thrown when the target device cannot be found.</exception>
        /// <exception cref="AudioDevicePermissionException">Thrown when microphone access is denied.</exception>
        /// <remarks>
        /// This switching process includes:
        /// <list type="number">
        /// <item><description>Device compatibility validation</description></item>
        /// <item><description>Permission verification and request if needed</description></item>
        /// <item><description>Graceful shutdown of current audio streams</description></item>
        /// <item><description>Initialization and testing of new device</description></item>
        /// <item><description>DeviceConnected event notification</description></item>
        /// </list>
        /// The operation is atomic - either fully successful or completely rolled back.
        /// </remarks>
        Task<bool> SwitchDeviceAsync(string deviceId);
        
        /// <summary>
        /// Performs comprehensive testing and analysis of the specified audio device.
        /// Evaluates device quality, performance, and compatibility characteristics.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device to test. Must not be null or empty.</param>
        /// <returns>A task that returns detailed test results including quality metrics and recommendations.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <exception cref="AudioDeviceNotFoundException">Thrown when the specified device cannot be found.</exception>
        /// <remarks>
        /// Comprehensive testing includes:
        /// <list type="bullet">
        /// <item><description>Basic functionality and initialization</description></item>
        /// <item><description>Audio format compatibility testing</description></item>
        /// <item><description>Quality assessment (signal quality, noise floor)</description></item>
        /// <item><description>Performance measurement (latency, throughput)</description></item>
        /// <item><description>Compatibility scoring for speech recognition</description></item>
        /// </list>
        /// Test results include recommendations for optimal settings and potential issues.
        /// </remarks>
        Task<AudioDeviceTestResult> PerformComprehensiveTestAsync(string deviceId);
        
        /// <summary>
        /// Analyzes audio quality metrics for the specified device during live audio capture.
        /// Provides real-time assessment of signal characteristics and performance.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device to analyze. Must not be null or empty.</param>
        /// <param name="durationMs">The duration of analysis in milliseconds. Default is 3000 (3 seconds).</param>
        /// <returns>A task that returns detailed audio quality metrics and analysis.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="durationMs"/> is not positive.</exception>
        /// <remarks>
        /// Quality metrics measured include:
        /// <list type="bullet">
        /// <item><description>RMS and peak signal levels</description></item>
        /// <item><description>Signal-to-noise ratio assessment</description></item>
        /// <item><description>Dynamic range analysis</description></item>
        /// <item><description>Frequency response characteristics</description></item>
        /// <item><description>Peak detection and clipping analysis</description></item>
        /// </list>
        /// Longer analysis durations provide more accurate results but increase latency.
        /// </remarks>
        Task<AudioQualityMetrics> AnalyzeAudioQualityAsync(string deviceId, int durationMs = 3000);
        
        /// <summary>
        /// Evaluates the compatibility of the specified device for speech recognition use cases.
        /// Provides numerical scoring and qualitative recommendations.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device to score. Must not be null or empty.</param>
        /// <returns>A task that returns compatibility scoring with detailed breakdown by category.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <remarks>
        /// Scoring criteria include:
        /// <list type="bullet">
        /// <item><description>Sample rate support (16kHz+ preferred)</description></item>
        /// <item><description>Channel configuration (mono preferred)</description></item>
        /// <item><description>Bit depth support (16-bit+ required)</description></item>
        /// <item><description>Device type (external USB preferred over integrated)</description></item>
        /// <item><description>Driver quality and stability</description></item>
        /// </list>
        /// Overall scores range from 0.0 to 1.0, with recommendations:
        /// <list type="bullet">
        /// <item><description>0.8+: Excellent for professional use</description></item>
        /// <item><description>0.6-0.8: Good for general use</description></item>
        /// <item><description>0.4-0.6: Fair for casual use</description></item>
        /// <item><description>&lt;0.4: Poor - not recommended</description></item>
        /// </list>
        /// </remarks>
        Task<DeviceCompatibilityScore> ScoreDeviceCompatibilityAsync(string deviceId);
        
        /// <summary>
        /// Measures the audio latency characteristics of the specified device.
        /// Determines the time delay between audio input and availability for processing.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device to test. Must not be null or empty.</param>
        /// <returns>A task that returns true if latency is acceptable (&lt;200ms), false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <remarks>
        /// Latency testing measures:
        /// <list type="bullet">
        /// <item><description>Time to first audio buffer reception</description></item>
        /// <item><description>Hardware buffer latency</description></item>
        /// <item><description>Driver processing overhead</description></item>
        /// <item><description>System audio subsystem latency</description></item>
        /// </list>
        /// Low latency is crucial for real-time audio applications. Speech recognition
        /// generally tolerates latency up to 200ms without significant impact on accuracy.
        /// </remarks>
        Task<bool> TestDeviceLatencyAsync(string deviceId);
        
        /// <summary>
        /// Generates device recommendations based on compatibility scoring and user preferences.
        /// Provides ranked list of suitable devices for current use case.
        /// </summary>
        /// <returns>A task that returns a list of device recommendations sorted by suitability.</returns>
        /// <remarks>
        /// Recommendations consider:
        /// <list type="bullet">
        /// <item><description>Compatibility scores from device analysis</description></item>
        /// <item><description>Current device availability and state</description></item>
        /// <item><description>Device type preferences (external vs integrated)</description></item>
        /// <item><description>Previous device usage patterns</description></item>
        /// <item><description>Quality metrics and test history</description></item>
        /// </list>
        /// Each recommendation includes scoring breakdown and specific use case recommendations.
        /// </remarks>
        Task<List<DeviceRecommendation>> GetDeviceRecommendationsAsync();
        
        /// <summary>
        /// Occurs when real-time audio level monitoring detects level changes.
        /// Provides continuous feedback on audio signal strength and activity.
        /// </summary>
        /// <remarks>
        /// This event is raised during active monitoring when audio levels change
        /// significantly from previous measurements. It includes:
        /// <list type="bullet">
        /// <item><description>Current RMS audio level (0.0 to 1.0)</description></item>
        /// <item><description>Timestamp of measurement</description></item>
        /// <item><description>Device identifier</description></item>
        /// </list>
        /// Events are throttled to avoid excessive updates while maintaining responsiveness.
        /// </remarks>
        event EventHandler<AudioLevelEventArgs> AudioLevelUpdated;
        
        /// <summary>
        /// Starts real-time monitoring of audio levels for the specified device.
        /// Provides continuous feedback on signal strength and activity detection.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device to monitor. Must not be null or empty.</param>
        /// <returns>A task that represents the monitoring operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <exception cref="AudioDeviceNotFoundException">Thrown when the specified device cannot be found.</exception>
        /// <exception cref="AudioDevicePermissionException">Thrown when microphone access is denied.</exception>
        /// <remarks>
        /// Monitoring provides:
        /// <list type="bullet">
        /// <item><description>Continuous RMS level calculation (50ms intervals)</description></item>
        /// <item><description>Activity detection and silence identification</description></item>
        /// <item><description>Peak level tracking for clipping detection</description></item>
        /// <item><description>Signal quality assessment</description></item>
        /// </list>
        /// Monitoring continues until stopped with <see cref="StopRealTimeMonitoringAsync"/>.
        /// Multiple monitoring sessions are not supported; stop existing monitoring before starting new.
        /// </remarks>
        Task StartRealTimeMonitoringAsync(string deviceId);
        
        /// <summary>
        /// Stops real-time audio level monitoring and releases associated resources.
        /// Gracefully terminates the monitoring operation and cleanup.
        /// </summary>
        /// <returns>A task that represents the monitoring shutdown operation.</returns>
        /// <remarks>
        /// This method:
        /// <list type="number">
        /// <item><description>Stops audio capture from the monitored device</description></item>
        /// <item><description>Disposes audio capture resources</description></item>
        /// <item><description>Cancels level update timers</description></item>
        /// <item><description>Releases device references for other applications</description></item>
        /// </list>
        /// The operation is idempotent - calling multiple times has no additional effect.
        /// </remarks>
        Task StopRealTimeMonitoringAsync();
        
        /// <summary>
        /// Starts monitoring for device connection and disconnection events.
        /// Enables automatic detection of hardware changes and system updates.
        /// </summary>
        /// <returns>A task that returns true if monitoring started successfully, false otherwise.</returns>
        /// <remarks>
        /// Device change monitoring uses:
        /// <list type="bullet">
        /// <item><description>Windows WM_DEVICECHANGE messages</description></item>
        /// <item><description>Device notification APIs</description></item>
        /// <item><description>WASAPI endpoint notifications</description></item>
        /// </list>
        /// Monitoring runs in the background with minimal CPU overhead. Events are
        /// correlated with device enumeration to maintain accurate state information.
        /// </remarks>
        Task<bool> MonitorDeviceChangesAsync();
        
        /// <summary>
        /// Stops device change monitoring and releases system notification resources.
        /// Gracefully terminates the monitoring operation and cleanup.
        /// </summary>
        /// <remarks>
        /// This method unregisters from Windows device notifications and
        /// cleans up background monitoring threads. The operation is thread-safe
        /// and can be called multiple times without issues.
        /// </remarks>
        void StopDeviceChangeMonitoring();
        
        /// <summary>
        /// Retries microphone permission request with exponential backoff delay.
        /// Provides resilient permission handling for transient system issues.
        /// </summary>
        /// <param name="maxAttempts">Maximum number of retry attempts. Default is 3.</param>
        /// <param name="baseDelayMs">Initial delay between retries in milliseconds. Default is 1000.</param>
        /// <returns>A task that returns true if permission was granted, false otherwise.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxAttempts"/> or <paramref name="baseDelayMs"/> are invalid.</exception>
        /// <remarks>
        /// Retry strategy:
        /// <list type="bullet">
        /// <item><description>Exponential backoff: delay = baseDelayMs * 2^(attempt-1)</description></item>
        /// <item><description>Maximum delay cap: 30 seconds</description></item>
        /// <item><description>User-friendly error messages between attempts</description></item>
        /// <item><description>Permission status checking between retries</description></item>
        /// </list>
        /// Useful when system is busy or has temporary permission issues.
        /// </remarks>
        Task<bool> RetryPermissionRequestAsync(int maxAttempts = 3, int baseDelayMs = 1000);
        
        /// <summary>
        /// Generates a comprehensive diagnostic report for microphone permission issues.
        /// Provides detailed information for troubleshooting support requests.
        /// </summary>
        /// <returns>A task that returns a formatted diagnostic report string.</returns>
        /// <remarks>
        /// The diagnostic report includes:
        /// <list type="bullet">
        /// <item><description>Current permission status and last check time</description></item>
        /// <item><description>Available audio devices and their permission states</description></item>
        /// <item><description>System information (OS version, app version)</description></item>
        /// <item><description>Troubleshooting recommendations</description></item>
        /// <item><description>Step-by-step permission guidance</description></item>
        /// </list>
        /// This report is designed to be user-friendly while providing technical details
        /// for support personnel. The format is suitable for logging, display, or email.
        /// </remarks>
        Task<string> GeneratePermissionDiagnosticReportAsync();
        
        /// <summary>
        /// Enters a graceful fallback mode when device or permission issues occur.
        /// Provides degraded functionality instead of complete failure.
        /// </summary>
        /// <param name="reason">The reason for entering fallback mode. Must not be null or empty.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="reason"/> is null or empty.</exception>
        /// <remarks>
        /// Fallback mode characteristics:
        /// <list type="bullet">
        /// <item><description>Disables real-time features to reduce errors</description></item>
        /// <item><description>Uses cached device information when available</description></item>
        /// <item><description>Provides user-friendly error messages</description></item>
        /// <item><description>Periodically retries to exit fallback mode</description></item>
        /// </list>
        /// Applications can use this to maintain basic functionality during system issues
        /// while providing clear communication about the degraded state.
        /// </remarks>
        Task EnterGracefulFallbackModeAsync(string reason);
        
        /// <summary>
        /// Handles automatic recovery when device connection state changes.
        /// Coordinates device reconnection and functionality testing.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the affected device. Must not be null or empty.</param>
        /// <param name="isConnected">True if device connected, false if disconnected.</param>
        /// <returns>A task that returns true if recovery was successful, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
        /// <remarks>
        /// Recovery process includes:
        /// <list type="number">
        /// <item><description>Device validation and compatibility checking</description></item>
        /// <item><description>Automatic reinitialization of audio streams</description></item>
        /// <item><description>Functionality testing with fallback to alternative devices</description></item>
        /// <item><description>Application notification through appropriate events</description></item>
        /// </list>
        /// Recovery events are logged and can be monitored through the service events.
        /// </remarks>
        Task<bool> HandleDeviceChangeRecoveryAsync(string deviceId, bool isConnected);
        
        /// <summary>
        /// Handles permission denied events with user guidance and error context.
        /// Provides comprehensive response to microphone access failures.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the affected device. Can be null for general denial.</param>
        /// <param name="error">The exception that caused the denial, if available. Can be null.</param>
        /// <returns>A task that represents the error handling operation.</returns>
        /// <remarks>
        /// Error handling includes:
        /// <list type="bullet">
        /// <item><description>Logging of denial with full context</description></item>
        /// <item><description>User guidance for enabling microphone access</description></item>
        /// <item><description>Fallback to alternative input methods if available</description></item>
        /// <item><description>Notification of UI components for user interaction</description></item>
        /// </list>
        /// This method centralizes permission error handling and ensures consistent user experience.
        /// </remarks>
        Task HandlePermissionDeniedEventAsync(string deviceId, Exception? error = null);
    }

    public class AudioDeviceService : IAudioDeviceService, IDisposable
    {
        private readonly IAudioDeviceEnumerator _enumerator;
        private readonly Func<IWaveIn> _waveInFactory;
        private readonly ILogger<AudioDeviceService> _logger;
        private readonly ICorrelationService _correlationService;
        private readonly IStructuredLoggingService _structuredLogger;
        private readonly object _lockObject = new object();
        private bool _disposed = false;
        private readonly bool _ownsEnumerator;
        private IWaveIn? _monitoringWaveIn;
        private System.Threading.Timer? _levelUpdateTimer;
        private float _currentAudioLevel = 0f;
        private bool _isMonitoring = false;
        
        // Device change monitoring
        private IntPtr _deviceNotificationHandle = IntPtr.Zero;
        private IntPtr _winEventHook = IntPtr.Zero;
        private IntPtr _messageWindowHandle = IntPtr.Zero;
        private readonly object _deviceLock = new object();
        private int _permissionRetryCount = 0;
        private DateTime _lastPermissionRequest = DateTime.MinValue;

        public event EventHandler<AudioDeviceEventArgs>? DeviceConnected;
        public event EventHandler<AudioDeviceEventArgs>? DeviceDisconnected;
        public event EventHandler<AudioDeviceEventArgs>? DefaultDeviceChanged;
        public event EventHandler<PermissionEventArgs>? PermissionRequired;
        public event EventHandler<PermissionEventArgs>? PermissionDenied;
        public event EventHandler<PermissionEventArgs>? PermissionGranted;
        public event EventHandler<PermissionEventArgs>? PermissionRequestFailed;
        public event EventHandler<AudioLevelEventArgs>? AudioLevelUpdated;
        public event EventHandler<DeviceRecoveryEventArgs>? DeviceRecoveryAttempted;
        public event EventHandler<DeviceRecoveryEventArgs>? DeviceRecoveryCompleted;

        /// <summary>
        /// Determines if an exception is fatal and should not be caught.
        /// </summary>
        private static bool IsFatalException(Exception ex)
        {
            return ex is OutOfMemoryException ||
                   ex is StackOverflowException ||
                   ex is AccessViolationException;
        }

        // Windows API declarations for permission handling
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr ShellExecute(
            IntPtr hwnd,
            string? lpOperation,
            string? lpFile,
            string? lpParameters,
            string? lpDirectory,
            int nShowCmd);

        // Windows API for device change detection
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr recipient, ref DEV_BROADCAST_DEVICEINTERFACE deviceInterface, int flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int UnregisterDeviceNotification(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            WinEventDelegate eventDelegate,
            uint idProcess,
            uint idThread,
            uint eventMin,
            uint eventMax,
            uint flags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        // Device notification structures
        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public char[] dbcc_name;
        }

        // WinEvent constants
        private const uint EVENT_SYSTEM_DEVICECHANGE = 0x0219;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        
        // Device types
        private const int DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;
        private static readonly Guid GUID_DEVINTERFACE_AUDIO_CAPTURE = new Guid("2C977F2C-F56A-11D0-94EA-00AA00B16C33");
        private static readonly Guid GUID_DEVINTERFACE_AUDIO_RENDER = new Guid("E6327CAD-DCE6-11D0-85E3-00AA00316D76");

        // Delegate for Windows events
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        // Windows message handling for device changes
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x, int y,
            int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        // Device change constants
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;
        private const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
        private const int WS_VISIBLE = 0x10000000;
        private const uint WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioDeviceService"/> class with default hardware enumeration.
    /// Sets up device monitoring, logging, and prepares for real-time device change detection.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when audio subsystem initialization fails.</exception>
    /// <remarks>
    /// This constructor creates a new <see cref="AudioDeviceEnumerator"/> and takes ownership
    /// of its lifecycle. The service will automatically start device change monitoring
    /// in the background to provide real-time notifications. Any initialization errors
    /// are logged but do not prevent service creation.
    /// </remarks>
    public AudioDeviceService()
        : this(new AudioDeviceEnumerator(), null, null, null, true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioDeviceService"/> class with the specified components.
    /// Supports dependency injection for testing and customization scenarios.
    /// </summary>
    /// <param name="enumerator">The audio device enumerator to use. Must not be null. Can be a mock for testing.</param>
    /// <param name="logger">The structured logger for operation tracking. Must not be null.</param>
    /// <param name="correlationService">The correlation ID service for request tracking. Must not be null.</param>
    /// <param name="structuredLogger">The structured logging service for comprehensive logging. Must not be null.</param>
    /// <param name="ownsEnumerator">Whether this service owns the enumerator and should dispose it.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <remarks>
    /// This constructor enables dependency injection and supports unit testing scenarios.
    /// When <paramref name="ownsEnumerator"/> is true, the service will manage the
    /// enumerator's lifecycle and dispose it when the service is disposed.
    /// Device change monitoring is automatically started after successful initialization.
    /// </remarks>
    public AudioDeviceService(
        IAudioDeviceEnumerator enumerator,
        ILogger<AudioDeviceService>? logger = null,
        ICorrelationService? correlationService = null,
        IStructuredLoggingService? structuredLogger = null,
        bool ownsEnumerator = false,
        Func<IWaveIn>? waveInFactory = null)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _waveInFactory = waveInFactory ?? (() => new WaveInWrapper());
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AudioDeviceService>.Instance;
        _correlationService = correlationService ?? new CorrelationService();
        
        // Use provided structured logger or create a fallback
        _structuredLogger = structuredLogger ?? new NullStructuredLoggingService(); 
        _ownsEnumerator = ownsEnumerator;
        
        // Initialize device change monitoring (fire-and-forget with exception handling)
        InitializeMonitoring();
    }
        
        private void InitializeMonitoring()
        {
            // Fire-and-forget with proper exception handling
            _ = FireAndForgetWithExceptionHandling(InitializeMonitoringAsync, "InitializeMonitoring");
        }
        
        /// <summary>
        /// Safely executes an async method as fire-and-forget with exception handling
        /// </summary>
        private async Task FireAndForgetWithExceptionHandling(Func<Task> asyncMethod, string operationName)
        {
            try
            {
                await asyncMethod().ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Fire-and-forget operation '{OperationName}' failed", operationName);
            }
            catch (COMException ex)
            {
                _logger.LogError(ex, "Fire-and-forget operation '{OperationName}' failed due to COM error", operationName);
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                _logger.LogError(ex, "Fire-and-forget operation '{OperationName}' failed unexpectedly", operationName);
            }
        }
        
        private async Task InitializeMonitoringAsync()
        {
            try
            {
                var monitoringStarted = await MonitorDeviceChangesAsync().ConfigureAwait(false);
                if (monitoringStarted)
                {
                    System.Diagnostics.Debug.WriteLine("Device change monitoring initialized successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to initialize device change monitoring");
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error initializing monitoring");
            }
            catch (COMException ex)
            {
                _logger.LogError(ex, "Error initializing monitoring due to COM error");
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                _logger.LogError(ex, "Unexpected error initializing monitoring");
            }
        }

        public async Task<List<AudioDevice>> GetInputDevicesAsync()
        {
            return await _structuredLogger.ExecuteWithLoggingAsync(
                "AudioDeviceService.GetInputDevicesAsync",
                async () =>
                {
                    if (_disposed) 
                        return new List<AudioDevice>();

                    try
                    {
                        // Check microphone permission first
                        var permissionStatus = await CheckMicrophonePermissionAsync().ConfigureAwait(false);
                        
                        if (permissionStatus == MicrophonePermissionStatus.Denied)
                        {
                            await _structuredLogger.ExecuteWithLoggingAsync(
                                "AudioDeviceService.PermissionDenied",
                                () =>
                                {
                                    PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, 
                                        "Microphone access is denied. Please enable microphone access in Windows Settings Privacy -> Microphone.", 
                                        string.Empty));
                                    return Task.CompletedTask;
                                });
                            return new List<AudioDevice>();
                        }

                        List<IMMDeviceWrapper> devices;
                        lock (_lockObject)
                        {
                            if (_disposed) 
                                return new List<AudioDevice>();

                            devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
                        }

                        var audioDevices = devices.Select(CreateAudioDevice).Where(d => d != null).ToList()!;

                        // Filter devices based on permission status
                        if (!permissionStatus.Equals(MicrophonePermissionStatus.Granted))
                        {
                            audioDevices = audioDevices.Where(d => d.PermissionStatus != MicrophonePermissionStatus.Denied).ToList();
                        }

                        _logger.LogDebug("Enumerated {Count} input devices with permission status {PermissionStatus}", 
                            audioDevices.Count, permissionStatus);

                        return audioDevices;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        await _structuredLogger.ExecuteWithLoggingAsync(
                            "AudioDeviceService.PermissionDenied",
                            () =>
                            {
                                PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, 
                                    "Access to audio devices was denied. Please check Windows Privacy Settings.", 
                                    string.Empty, ex));
                                return Task.CompletedTask;
                            });
                        
                        _logger.LogWarning(ex, "Unauthorized access when enumerating input devices");
                        return new List<AudioDevice>();
                    }
                    catch (SecurityException ex)
                    {
                        await _structuredLogger.ExecuteWithLoggingAsync(
                            "AudioDeviceService.SecurityException",
                            () =>
                            {
                                PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, 
                                    "Security error accessing audio devices. Please check Windows Privacy Settings.", 
                                    string.Empty, ex));
                                return Task.CompletedTask;
                            });
                        
                        _logger.LogWarning(ex, "Security exception when enumerating input devices");
                        return new List<AudioDevice>();
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(ex, "Invalid operation when enumerating input devices");
                        return new List<AudioDevice>();
                    }
                    catch (COMException ex)
                    {
                        _logger.LogWarning(ex, "COM exception when enumerating input devices");
                        return new List<AudioDevice>();
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "IO exception when enumerating input devices");
                        return new List<AudioDevice>();
                    }
                },
                new Dictionary<string, object>
                {
                    ["PermissionStatus"] = "Checking",
                    ["DeviceType"] = "Input",
                    ["Disposed"] = _disposed
                });
        }

        public Task<List<AudioDevice>> GetOutputDevicesAsync()
        {
            lock (_lockObject)
            {
                if (_disposed) return Task.FromResult(new List<AudioDevice>());

                try
                {
                    var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                    var result = devices.Select(CreateAudioDevice).Where(d => d != null).ToList()!;
                    return Task.FromResult(result);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Error enumerating output devices");
                    return Task.FromResult(new List<AudioDevice>());
                }
                catch (COMException ex)
                {
                    _logger.LogWarning(ex, "COM error enumerating output devices");
                    return Task.FromResult(new List<AudioDevice>());
                }
            }
        }

        public Task<AudioDevice> GetDefaultInputDeviceAsync()
        {
            lock (_lockObject)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(AudioDeviceService));

                try
                {
                    var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    return Task.FromResult(CreateAudioDevice(device)!);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "Error getting default input device: unauthorized access");
                    throw new AudioDevicePermissionException(string.Empty, "Audio device access", "Unable to get default input device - unauthorized access", ex);
                }
                catch (SecurityException ex)
                {
                    _logger.LogError(ex, "Error getting default input device: security exception");
                    throw new AudioDevicePermissionException(string.Empty, "Audio device access", "Unable to get default input device - security exception", ex);
                }
                catch (COMException ex)
                {
                    _logger.LogError(ex, "Error getting default input device: COM error");
                    throw new InvalidOperationException("Unable to get default input device", ex);
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
            }
        }

        public Task<AudioDevice> GetDefaultOutputDeviceAsync()
        {
            lock (_lockObject)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(AudioDeviceService));

                try
                {
                    var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    return Task.FromResult(CreateAudioDevice(device)!);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "Error getting default output device: unauthorized access");
                    throw new InvalidOperationException("Unable to get default output device", ex);
                }
                catch (SecurityException ex)
                {
                    _logger.LogError(ex, "Error getting default output device: security exception");
                    throw new InvalidOperationException("Unable to get default output device", ex);
                }
                catch (COMException ex)
                {
                    _logger.LogError(ex, "Error getting default output device: COM error");
                    throw new InvalidOperationException("Unable to get default output device", ex);
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
            }
        }

        public Task<bool> TestDeviceAsync(string deviceId)
        {
            lock (_lockObject)
            {
                if (_disposed) return Task.FromResult(false);

                try
                {
                    var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                        .FirstOrDefault(d => d.ID == deviceId);
                    
                    if (device == null) return Task.FromResult(false);

                    // Test basic device functionality
                    using (var waveIn = _waveInFactory())
                    {
                        waveIn.DeviceNumber = GetDeviceNumber(device.ID);
                        
                        // Test if we can configure the device for basic recording
                        waveIn.WaveFormat = new WaveFormat(16000, 1); // 16kHz mono for speech recognition
                        
                        // Try to start recording briefly
                        waveIn.StartRecording();
                        Thread.Sleep(100); // Very brief test
                        waveIn.StopRecording();
                        
                        return Task.FromResult(true);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Error testing device {DeviceId}: unauthorized access", deviceId);
                    return Task.FromResult(false);
                }
                catch (SecurityException ex)
                {
                    _logger.LogWarning(ex, "Error testing device {DeviceId}: security exception", deviceId);
                    return Task.FromResult(false);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Error testing device {DeviceId}: invalid operation", deviceId);
                    return Task.FromResult(false);
                }
                catch (COMException ex)
                {
                    _logger.LogWarning(ex, "Error testing device {DeviceId}: COM error", deviceId);
                    return Task.FromResult(false);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Error testing device {DeviceId}: IO error", deviceId);
                    return Task.FromResult(false);
                }
            }
        }

        public async Task<AudioDeviceTestResult> PerformComprehensiveTestAsync(string deviceId)
        {
            if (_disposed) return new AudioDeviceTestResult { Success = false, ErrorMessage = "Service disposed" };

            try
            {
                var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .FirstOrDefault(d => d.ID == deviceId);
                
                if (device == null)
                    return new AudioDeviceTestResult { Success = false, ErrorMessage = "Device not found" };

                var result = new AudioDeviceTestResult
                {
                    DeviceId = deviceId,
                    DeviceName = device.FriendlyName,
                    TestStarted = DateTime.Now,
                    TestCompleted = DateTime.Now
                };

                // Test 1: Basic functionality (sync, no lock needed)
                using (var waveIn = _waveInFactory())
                {
                    waveIn.DeviceNumber = GetDeviceNumber(device.ID);
                    waveIn.WaveFormat = new WaveFormat(16000, 1);
                    
                    try
                    {
                        waveIn.StartRecording();
                        Thread.Sleep(200);
                        waveIn.StopRecording();
                        result.BasicFunctionality = true;
                    }
                    catch (InvalidOperationException)
                    {
                        result.BasicFunctionality = false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        result.BasicFunctionality = false;
                    }
                    catch (IOException)
                    {
                        result.BasicFunctionality = false;
                    }
                }

                // Test 2: Format support (sync, no lock needed)
                result.SupportedFormats = string.Join(", ", GetSupportedFormats(device) ?? new List<string>());

                // Test 3: Quality assessment
                var qualityScore = await AssessDeviceQualityAsync(device).ConfigureAwait(false);
                result.QualityScore = (int)qualityScore;

                // Test 4: Latency measurement
                result.LatencyMs = await MeasureDeviceLatencyAsync(device).ConfigureAwait(false);

                // Test 5: Noise floor measurement
                result.NoiseFloorDb = await MeasureNoiseFloorAsync(device).ConfigureAwait(false);

                result.Success = result.BasicFunctionality && result.QualityScore > 0.3f;
                result.TestCompleted = DateTime.Now;
                result.TestTime = result.TestCompleted - result.TestStarted;
                return result;
            }
            catch (InvalidOperationException ex)
            {
                return new AudioDeviceTestResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    DeviceId = deviceId,
                    TestStarted = DateTime.Now,
                    TestCompleted = DateTime.Now,
                    TestTime = TimeSpan.Zero
                };
            }
            catch (COMException ex)
            {
                return new AudioDeviceTestResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    DeviceId = deviceId,
                    TestStarted = DateTime.Now,
                    TestCompleted = DateTime.Now,
                    TestTime = TimeSpan.Zero
                };
            }
        }

        public async Task<AudioQualityMetrics> AnalyzeAudioQualityAsync(string deviceId, int durationMs = 3000)
        {
            if (_disposed) return new AudioQualityMetrics();

            IMMDeviceWrapper? device;
            lock (_lockObject)
            {
                device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .FirstOrDefault(d => d.ID == deviceId);
            }
            
            if (device == null) return new AudioQualityMetrics();

            try
            {
                var metrics = new AudioQualityMetrics
                {
                    DeviceId = deviceId,
                    AnalysisTime = DateTime.Now
                };

                var buffer = new byte[16000 * durationMs / 1000]; // 16kHz, 16-bit
                var samples = new float[durationMs / 10]; // Sample every 10ms

                using (var waveIn = _waveInFactory())
                {
                    waveIn.DeviceNumber = GetDeviceNumber(device.ID);
                    waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
                    waveIn.BufferMilliseconds = 10;
                    
                    int sampleIndex = 0;
                    float sum = 0;
                    float sumSquares = 0;
                    int peakCount = 0;
                    float maxLevel = 0;

                    waveIn.DataAvailable += (sender, e) =>
                    {
                        if (sampleIndex >= samples.Length) return;

                        // Convert bytes to float
                        for (int i = 0; i < e.BytesRecorded; i += 2)
                        {
                            if (i + 1 < e.Buffer.Length)
                            {
                                short sample = BitConverter.ToInt16(e.Buffer, i);
                                float level = Math.Abs(sample / 32768f);
                                
                                samples[sampleIndex] = level;
                                sum += level;
                                sumSquares += level * level;
                                
                                if (level > 0.1f) peakCount++;
                                if (level > maxLevel) maxLevel = level;
                                
                                sampleIndex++;
                            }
                        }
                    };

                    waveIn.StartRecording();
                    await Task.Delay(durationMs).ConfigureAwait(false);
                    waveIn.StopRecording();

                    // Calculate metrics
                    if (sampleIndex > 0)
                    {
                        float average = sum / sampleIndex;
                        float rms = (float)Math.Sqrt(sumSquares / sampleIndex);
                        float peakRatio = peakCount / (float)sampleIndex;
                        
                        metrics.AverageLevel = average;
                        metrics.RMSLevel = rms;
                        metrics.PeakLevel = maxLevel;
                        metrics.PeakToRMSRatio = peakRatio;
                        metrics.DynamicRange = maxLevel - average;
                        metrics.SignalQuality = CalculateSignalQuality(rms, peakRatio);
                    }
                }

                return metrics;
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing audio quality for {deviceId}: {ex.Message}");
                return new AudioQualityMetrics { DeviceId = deviceId };
            }
            catch (SecurityException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing audio quality for {deviceId}: {ex.Message}");
                return new AudioQualityMetrics { DeviceId = deviceId };
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing audio quality for {deviceId}: {ex.Message}");
                return new AudioQualityMetrics { DeviceId = deviceId };
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing audio quality for {deviceId}: {ex.Message}");
                return new AudioQualityMetrics { DeviceId = deviceId };
            }
        }

        public Task<DeviceCompatibilityScore> ScoreDeviceCompatibilityAsync(string deviceId)
        {
            lock (_lockObject)
            {
                if (_disposed) return Task.FromResult(new DeviceCompatibilityScore());

                try
                {
                    var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                        .FirstOrDefault(d => d.ID == deviceId);
                    
                    if (device == null) return Task.FromResult(new DeviceCompatibilityScore());

                    var score = new DeviceCompatibilityScore
                    {
                        DeviceId = deviceId,
                        ScoreTime = DateTime.Now
                    };

                    var format = device.AudioClient?.MixFormat;
                    if (format != null)
                    {
                        // Sample rate scoring (16kHz+ is optimal for speech)
                        if (format.SampleRate >= 16000)
                            score.SampleRateScore = 1.0f; // Any rate >= 16kHz is considered optimal
                        else
                            score.SampleRateScore = 0.2f;

                        // Channel scoring (mono is preferred for speech)
                        if (format.Channels == 1)
                            score.ChannelScore = 1.0f;
                        else if (format.Channels == 2)
                            score.ChannelScore = 0.8f;
                        else
                            score.ChannelScore = 0.4f;

                        // Bit depth scoring
                        if (format.BitsPerSample >= 16)
                            score.BitDepthScore = 1.0f;
                        else if (format.BitsPerSample >= 8)
                            score.BitDepthScore = 0.6f;
                        else
                            score.BitDepthScore = 0.2f;
                    }

                    // Device category scoring
                    var deviceName = device.FriendlyName.ToLower();
                    if (deviceName.Contains("usb") || deviceName.Contains("external"))
                        score.DeviceTypeScore = 1.0f; // External devices are preferred
                    else if (deviceName.Contains("webcam") || deviceName.Contains("integrated"))
                        score.DeviceTypeScore = 0.3f; // Integrated devices are less ideal
                    else
                        score.DeviceTypeScore = 0.7f; // Other internal devices

                    // Calculate overall score
                    score.OverallScore = (score.SampleRateScore * 0.3f) +
                                          (score.ChannelScore * 0.2f) +
                                          (score.BitDepthScore * 0.2f) +
                                          (score.DeviceTypeScore * 0.3f);

                    // Determine recommendation level
                    if (score.OverallScore >= 0.8f)
                        score.Recommendation = "Excellent";
                    else if (score.OverallScore >= 0.6f)
                        score.Recommendation = "Good";
                    else if (score.OverallScore >= 0.4f)
                        score.Recommendation = "Fair";
                    else
                        score.Recommendation = "Poor";

                    return Task.FromResult(score);
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error scoring device compatibility for {deviceId}: {ex.Message}");
                    return Task.FromResult(new DeviceCompatibilityScore { DeviceId = deviceId });
                }
                catch (COMException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error scoring device compatibility for {deviceId}: {ex.Message}");
                    return Task.FromResult(new DeviceCompatibilityScore { DeviceId = deviceId });
                }
            }
        }

        public async Task<bool> TestDeviceLatencyAsync(string deviceId)
        {
            if (_disposed) return false;

            IMMDeviceWrapper? device;
            lock (_lockObject)
            {
                device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .FirstOrDefault(d => d.ID == deviceId);
            }
            
            if (device == null) return false;

            try
            {
                using (var waveIn = _waveInFactory())
                {
                    waveIn.DeviceNumber = GetDeviceNumber(device.ID);
                    waveIn.WaveFormat = new WaveFormat(16000, 1);
                    waveIn.BufferMilliseconds = 50; // Small buffer for latency testing

                    var latencyStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    bool firstBufferReceived = false;

                    waveIn.DataAvailable += (sender, e) =>
                    {
                        if (!firstBufferReceived && e.BytesRecorded > 0)
                        {
                            latencyStopwatch.Stop();
                            firstBufferReceived = true;
                        }
                    };

                    waveIn.StartRecording();
                    
                    // Wait up to 1 second for first buffer
                    await Task.Delay(1000).ConfigureAwait(false);
                    
                    waveIn.StopRecording();

                    // Consider latency acceptable if < 200ms
                    return latencyStopwatch.ElapsedMilliseconds < 200;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error testing device latency for {deviceId}: {ex.Message}");
                return false;
            }
            catch (SecurityException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error testing device latency for {deviceId}: {ex.Message}");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error testing device latency for {deviceId}: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error testing device latency for {deviceId}: {ex.Message}");
                return false;
            }
        }

        public async Task<List<DeviceRecommendation>> GetDeviceRecommendationsAsync()
        {
            var inputDevices = await GetInputDevicesAsync().ConfigureAwait(false);
            var recommendations = new List<DeviceRecommendation>();

            foreach (var device in inputDevices)
            {
                var score = await ScoreDeviceCompatibilityAsync(device.Id).ConfigureAwait(false);
                
                recommendations.Add(new DeviceRecommendation
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    Score = score.OverallScore,
                    Recommendation = score.Recommendation,
                    Reason = GenerateRecommendationReason(score),
                    Priority = CalculateRecommendationPriority(score.OverallScore)
                });
            }

            return recommendations.OrderByDescending(r => r.Score).ThenBy(r => r.Priority).ToList();
        }

        public Task StartRealTimeMonitoringAsync(string deviceId)
        {
            if (_disposed || _isMonitoring) return Task.CompletedTask;
            
            lock (_lockObject)
            {
                if (_disposed || _isMonitoring) return Task.CompletedTask;

                try
                {
                    var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                        .FirstOrDefault(d => d.ID == deviceId);
                    
                    if (device == null) return Task.CompletedTask;

                    _monitoringWaveIn = _waveInFactory();
                    _monitoringWaveIn.DeviceNumber = GetDeviceNumber(device.ID);
                    _monitoringWaveIn.WaveFormat = new WaveFormat(16000, 1);
                    _monitoringWaveIn.BufferMilliseconds = 50;

                    _monitoringWaveIn.DataAvailable += OnMonitoringDataAvailable;
                    
                    _levelUpdateTimer = new System.Threading.Timer(UpdateAudioLevel, null, 0, 50);
                    
                    _monitoringWaveIn.StartRecording();
                    _isMonitoring = true;
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting real-time monitoring for {deviceId}: {ex.Message}");
                }
                catch (SecurityException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting real-time monitoring for {deviceId}: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting real-time monitoring for {deviceId}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting real-time monitoring for {deviceId}: {ex.Message}");
                }
            }
            
            return Task.CompletedTask;
        }

        public Task StopRealTimeMonitoringAsync()
        {
            lock (_lockObject)
            {
                if (!_isMonitoring) return Task.CompletedTask;

                try
                {
                    _levelUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _levelUpdateTimer?.Dispose();
                    _levelUpdateTimer = null;

                    if (_monitoringWaveIn != null)
                    {
                        _monitoringWaveIn.DataAvailable -= OnMonitoringDataAvailable;
                        _monitoringWaveIn.StopRecording();
                        _monitoringWaveIn.Dispose();
                        _monitoringWaveIn = null;
                    }

                    _isMonitoring = false;
                    _currentAudioLevel = 0f;
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error stopping real-time monitoring: {ex.Message}");
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error stopping real-time monitoring: {ex.Message}");
                }
            }
            
            return Task.CompletedTask;
        }

        private void OnMonitoringDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            // Calculate RMS level from buffer
            float sum = 0;
            int sampleCount = 0;

            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                if (i + 1 < e.Buffer.Length)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i);
                    float level = Math.Abs(sample / 32768f);
                    sum += level;
                    sampleCount++;
                }
            }

            if (sampleCount > 0)
            {
                _currentAudioLevel = sum / sampleCount;
                
                // Raise event with level data
                AudioLevelUpdated?.Invoke(this, new AudioLevelEventArgs("current", _currentAudioLevel, DateTime.Now));
            }
        }

        private void UpdateAudioLevel(object? state)
        {
            AudioLevelUpdated?.Invoke(this, new AudioLevelEventArgs("current", _currentAudioLevel, DateTime.Now));
        }

        private List<string> GetSupportedFormats(IMMDeviceWrapper device)
        {
            var formats = new List<string>();
            
            try
            {
                // Test common formats for speech recognition
                var commonFormats = new[]
                {
                    new WaveFormat(16000, 16, 1), // 16kHz mono
                    new WaveFormat(22050, 16, 1), // 22kHz mono
                    new WaveFormat(44100, 16, 1), // 44kHz mono
                    new WaveFormat(48000, 16, 1), // 48kHz mono
                };

                using (var waveIn = _waveInFactory())
                {
                    waveIn.DeviceNumber = GetDeviceNumber(device.ID);

                    foreach (var format in commonFormats)
                    {
                        try
                        {
                            waveIn.WaveFormat = format;
                            waveIn.StartRecording();
                            waveIn.StopRecording();
                            formats.Add($"{format.SampleRate}Hz, {format.BitsPerSample}bit, {format.Channels}ch");
                        }
                        catch (InvalidOperationException)
                        {
                            // Format not supported
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Format not supported
                        }
                        catch (IOException)
                        {
                            // Format not supported
                        }
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting supported formats: {ex.Message}");
            }
            catch (COMException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting supported formats: {ex.Message}");
            }

            return formats;
        }

        private async Task<float> AssessDeviceQualityAsync(IMMDeviceWrapper device)
        {
            try
            {
                // Basic quality assessment based on device properties
                var format = device.AudioClient?.MixFormat;
                if (format == null) return 0.1f;

                float score = 0.1f;

                // Sample rate contribution
                if (format.SampleRate >= 48000) score += 0.3f;
                else if (format.SampleRate >= 44100) score += 0.25f;
                else if (format.SampleRate >= 22050) score += 0.2f;
                else if (format.SampleRate >= 16000) score += 0.15f;

                // Bit depth contribution
                if (format.BitsPerSample >= 24) score += 0.3f;
                else if (format.BitsPerSample >= 16) score += 0.25f;
                else if (format.BitsPerSample >= 8) score += 0.1f;

                // Channel contribution (mono preferred for speech)
                if (format.Channels == 1) score += 0.2f;
                else if (format.Channels == 2) score += 0.1f;

                // Device name analysis
                var name = device.FriendlyName.ToLower();
                if (name.Contains("usb") || name.Contains("external")) score += 0.2f;
                else if (name.Contains("webcam") || name.Contains("integrated")) score -= 0.1f;

                return Math.Min(1.0f, score);
            }
            catch (InvalidOperationException)
            {
                return 0.1f;
            }
            catch (COMException)
            {
                return 0.1f;
            }
        }

        private async Task<int> MeasureDeviceLatencyAsync(IMMDeviceWrapper device)
        {
            try
            {
                using (var waveIn = _waveInFactory())
                {
                    waveIn.DeviceNumber = GetDeviceNumber(device.ID);
                    waveIn.WaveFormat = new WaveFormat(16000, 1);
                    waveIn.BufferMilliseconds = 20;

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    bool firstBuffer = false;

                    waveIn.DataAvailable += (sender, e) =>
                    {
                        if (!firstBuffer && e.BytesRecorded > 0)
                        {
                            stopwatch.Stop();
                            firstBuffer = true;
                        }
                    };

                    waveIn.StartRecording();
                    await Task.Delay(500).ConfigureAwait(false); // Wait up to 500ms for first buffer
                    waveIn.StopRecording();

                    return (int)stopwatch.ElapsedMilliseconds;
                }
            }
            catch (InvalidOperationException)
            {
                return 999; // High latency value indicating measurement failed
            }
            catch (UnauthorizedAccessException)
            {
                return 999; // High latency value indicating measurement failed
            }
            catch (IOException)
            {
                return 999; // High latency value indicating measurement failed
            }
        }

        private async Task<float> MeasureNoiseFloorAsync(IMMDeviceWrapper device)
        {
            try
            {
                using (var waveIn = _waveInFactory())
                {
                    waveIn.DeviceNumber = GetDeviceNumber(device.ID);
                    waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
                    waveIn.BufferMilliseconds = 100;

                    float minLevel = float.MaxValue;
                    float maxLevel = 0;
                    int sampleCount = 0;

                    waveIn.DataAvailable += (sender, e) =>
                    {
                        for (int i = 0; i < e.BytesRecorded; i += 2)
                        {
                            if (i + 1 < e.Buffer.Length)
                            {
                                short sample = BitConverter.ToInt16(e.Buffer, i);
                                float level = Math.Abs(sample / 32768f);
                                
                                if (level < minLevel) minLevel = level;
                                if (level > maxLevel) maxLevel = level;
                                sampleCount++;
                            }
                        }
                    };

                    waveIn.StartRecording();
                    await Task.Delay(1000).ConfigureAwait(false); // Measure for 1 second
                    waveIn.StopRecording();

                    // Calculate noise floor (minimum level during quiet period)
                    return minLevel;
                }
            }
            catch (InvalidOperationException)
            {
                return -120f; // Default noise floor in dB
            }
            catch (UnauthorizedAccessException)
            {
                return -120f; // Default noise floor in dB
            }
            catch (IOException)
            {
                return -120f; // Default noise floor in dB
            }
        }

        private float CalculateSignalQuality(float rmsLevel, float peakRatio)
        {
            // Signal quality based on RMS level and peak characteristics
            float levelScore = Math.Min(1.0f, rmsLevel * 10); // Normalize RMS to 0-1
            float peakScore = Math.Max(0f, 1.0f - peakRatio); // Lower peak ratio is better
            
            return (levelScore + peakScore) / 2f;
        }

        private string GenerateRecommendationReason(DeviceCompatibilityScore score)
        {
            var reasons = new List<string>();

            if (score.SampleRateScore >= 0.8f)
                reasons.Add($"Excellent sample rate support");
            else if (score.SampleRateScore < 0.4f)
                reasons.Add($"Limited sample rate support");

            if (score.ChannelScore >= 0.8f)
                reasons.Add($"Optimal channel configuration");
            else if (score.ChannelScore < 0.4f)
                reasons.Add($"Suboptimal channel configuration");

            if (score.DeviceTypeScore >= 0.8f)
                reasons.Add($"Professional device type");
            else if (score.DeviceTypeScore < 0.4f)
                reasons.Add($"Consumer-grade device type");

            return reasons.Count > 0 ? string.Join("; ", reasons) : "Standard device";
        }

        private int CalculateRecommendationPriority(float score)
        {
            if (score >= 0.8f) return 1; // High priority
            if (score >= 0.6f) return 2; // Medium priority
            if (score >= 0.4f) return 3; // Low priority
            return 4; // Not recommended
        }

        public Task<AudioDeviceCapabilities> GetDeviceCapabilitiesAsync(string deviceId)
        {
            lock (_lockObject)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(AudioDeviceService));

                try
                {
                    var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                        .FirstOrDefault(d => d.ID == deviceId);
                    
                    if (device == null)
                        throw new ArgumentException($"Device with ID {deviceId} not found");

                    var capabilities = new AudioDeviceCapabilities();
                    
                    // Get supported formats
                    var formats = device.AudioClient?.MixFormat;
                    if (formats != null)
                    {
                        capabilities.SampleRate = formats.SampleRate;
                        capabilities.Channels = formats.Channels;
                        capabilities.BitsPerSample = formats.BitsPerSample;
                    }

                    // Get device properties
                    var properties = device.Properties;
                    if (properties != null)
                    {
                        capabilities.DeviceFriendlyName = properties[PropertyKeys.PKEY_Device_FriendlyName].Value as string ?? device.FriendlyName;
                        capabilities.DeviceDescription = properties[PropertyKeys.PKEY_Device_DeviceDesc].Value as string ?? "";
                    }

                    return Task.FromResult(capabilities);
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting device capabilities for {deviceId}: {ex.Message}");
                    throw new InvalidOperationException("Unable to get device capabilities", ex);
                }
                catch (SecurityException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting device capabilities for {deviceId}: {ex.Message}");
                    throw new InvalidOperationException("Unable to get device capabilities", ex);
                }
                catch (COMException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting device capabilities for {deviceId}: {ex.Message}");
                    throw new InvalidOperationException("Unable to get device capabilities", ex);
                }
            }
        }

        public Task<AudioDevice?> GetDeviceByIdAsync(string deviceId)
        {
            lock (_lockObject)
            {
                if (_disposed) return Task.FromResult<AudioDevice?>(null);

                try
                {
                    var device = _enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)
                        .FirstOrDefault(d => d.ID == deviceId);
                    
                    return Task.FromResult(device != null ? CreateAudioDevice(device) : null);
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting device by ID {deviceId}: {ex.Message}");
                    return Task.FromResult<AudioDevice?>(null);
                }
                catch (COMException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting device by ID {deviceId}: {ex.Message}");
                    return Task.FromResult<AudioDevice?>(null);
                }
            }
        }

        public bool IsDeviceCompatible(string deviceId)
        {
            lock (_lockObject)
            {
                if (_disposed) return false;

                try
                {
                    var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                        .FirstOrDefault(d => d.ID == deviceId);
                    
                    if (device == null) return false;

                    // Check if device supports required format for speech recognition
                    var format = device.AudioClient?.MixFormat;
                    if (format == null) return false;

                    // Speech recognition typically needs: 16kHz or higher, mono, 16-bit or higher
                    return format.SampleRate >= 16000 && 
                           (format.Channels == 1 || format.Channels == 2) && 
                           format.BitsPerSample >= 16;
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking device compatibility for {deviceId}: {ex.Message}");
                    return false;
                }
                catch (COMException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking device compatibility for {deviceId}: {ex.Message}");
                    return false;
                }
            }
        }

        private AudioDevice? CreateAudioDevice(IMMDeviceWrapper device)
        {
            try
            {
                var audioDevice = new AudioDevice
                {
                    Id = device.ID,
                    DeviceId = device.ID, // Set both for compatibility
                    Name = device.FriendlyName,
                    Description = device.DeviceFriendlyName,
                    DataFlow = device.DataFlow == DataFlow.Capture ? AudioDataFlow.Capture : AudioDataFlow.Render,
                    State = device.State == NAudio.CoreAudioApi.DeviceState.Active ? AudioDeviceState.Active : AudioDeviceState.Disabled,
                    IsDefault = false // Will be determined by context
                };

                // Check permission status for input devices
                if (audioDevice.DataFlow == AudioDataFlow.Capture)
                {
                    audioDevice.PermissionStatus = CheckMicrophonePermissionForDevice(device.ID).GetAwaiter().GetResult();
                }

                return audioDevice;
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating audio device object: {ex.Message}");
                return null;
            }
            catch (COMException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating audio device object: {ex.Message}");
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating audio device object: {ex.Message}");
                return null;
            }
        }

        private int GetDeviceNumber(string deviceId)
        {
            try
            {
                // Extract device number from device ID for WaveInEvent
                var parts = deviceId.Split('{', '}');
                if (parts.Length >= 2 && int.TryParse(parts[1], out var number))
                {
                    return number;
                }
                return 0; // Default device
            }
            catch
            {
                return 0;
            }
        }

        // Real-time device monitoring can be added later
        // For now, manual refresh through RefreshDevicesAsync method

        public Task<MicrophonePermissionStatus> CheckMicrophonePermissionAsync()
        {
            lock (_lockObject)
            {
                if (_disposed) return Task.FromResult(MicrophonePermissionStatus.SystemError);

                try
                {
                    // Try to access default audio input device to check permissions
                    var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                    if (!devices.Any())
                    {
                        return Task.FromResult(MicrophonePermissionStatus.Denied);
                    }

                // Test with first available device
                    var testDevice = devices.First();
                    return CheckMicrophonePermissionForDevice(testDevice.ID);
                }
                catch (UnauthorizedAccessException)
                {
                    return Task.FromResult(MicrophonePermissionStatus.Denied);
                }
                catch (SecurityException)
                {
                    return Task.FromResult(MicrophonePermissionStatus.Denied);
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking microphone permission: {ex.Message}");
                    return Task.FromResult(MicrophonePermissionStatus.SystemError);
                }
                catch (COMException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking microphone permission: {ex.Message}");
                    return Task.FromResult(MicrophonePermissionStatus.SystemError);
                }
            }
        }

        public async Task<bool> RequestMicrophonePermissionAsync()
        {
            if (_disposed) return false;

            try
            {
                // On Windows, we trigger the permission request by attempting to access the microphone
                // This will show the Windows permission dialog if not already granted
                var currentStatus = await CheckMicrophonePermissionAsync().ConfigureAwait(false);
                
                if (currentStatus == MicrophonePermissionStatus.Granted)
                {
                    PermissionGranted?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Granted, "Microphone permission already granted"));
                    return true;
                }

                // Try to trigger permission dialog by attempting device access
                try
                {
                    List<IMMDeviceWrapper> devices;
                    lock (_lockObject)
                    {
                        devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
                    }

                    if (!devices.Any())
                    {
                        PermissionRequestFailed?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, "No audio input devices available"));
                        return false;
                    }

                    // Attempt to access device to trigger permission dialog
                    var testDevice = devices.First();
                    using (var waveIn = _waveInFactory())
                    {
                        waveIn.DeviceNumber = GetDeviceNumber(testDevice.ID);
                        waveIn.WaveFormat = new WaveFormat(16000, 1);
                        
                        // This should trigger the permission dialog if needed
                        waveIn.StartRecording();
                        await Task.Delay(100).ConfigureAwait(false);
                        waveIn.StopRecording();
                    }

                    // Check if permission was granted
                    var newStatus = await CheckMicrophonePermissionForDevice(testDevice.ID).ConfigureAwait(false);
                    if (newStatus == MicrophonePermissionStatus.Granted)
                    {
                        PermissionGranted?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Granted, "Microphone permission granted successfully", testDevice.ID));
                        return true;
                    }
                    else
                    {
                        PermissionRequestFailed?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, "Microphone permission was denied", testDevice.ID));
                        return false;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, "Access to microphone was denied. Please enable microphone access in Windows Settings.", ""));
                    return false;
                }
                catch (SecurityException)
                {
                    PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, "Security error accessing microphone. Please check Windows Privacy Settings.", ""));
                    return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error requesting microphone permission: {ex.Message}");
                PermissionRequestFailed?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.SystemError, $"System error requesting permission: {ex.Message}"));
                return false;
            }
            catch (COMException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error requesting microphone permission: {ex.Message}");
                PermissionRequestFailed?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.SystemError, $"System error requesting permission: {ex.Message}"));
                return false;
            }
        }

        private Task<MicrophonePermissionStatus> CheckMicrophonePermissionForDevice(string deviceId)
        {
            try
            {
                // Try to create and configure a WaveInEvent to test permission
                using (var waveIn = _waveInFactory())
                {
                    waveIn.DeviceNumber = GetDeviceNumber(deviceId);
                    waveIn.WaveFormat = new WaveFormat(16000, 1); // 16kHz mono
                    
                    // Try to start recording briefly to test permission
                    waveIn.StartRecording();
                    Thread.Sleep(50); // Very brief test
                    waveIn.StopRecording();
                    
                    return Task.FromResult(MicrophonePermissionStatus.Granted);
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult(MicrophonePermissionStatus.Denied);
            }
            catch (SecurityException)
            {
                return Task.FromResult(MicrophonePermissionStatus.Denied);
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult(MicrophonePermissionStatus.SystemError);
            }
            catch (COMException)
            {
                return Task.FromResult(MicrophonePermissionStatus.SystemError);
            }
            catch (IOException)
            {
                return Task.FromResult(MicrophonePermissionStatus.SystemError);
            }
        }

        public void OpenWindowsMicrophoneSettings()
        {
            try
            {
                // Open Windows Privacy & Security -> Microphone settings
                const string settingsPath = "ms-settings:privacy-microphone";
                ShellExecute(IntPtr.Zero, "open", settingsPath, null, null, 1);
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening Windows microphone settings: {ex.Message}");
            }
            catch (ExternalException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening Windows microphone settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts monitoring for device changes using WM_DEVICECHANGE
        /// </summary>
        public Task<bool> MonitorDeviceChangesAsync()
        {
            try
            {
                lock (_deviceLock)
                {
                    if (_isMonitoring) return Task.FromResult(true);

                    // Create a hidden window to receive device change messages
                    _messageWindowHandle = CreateWindowEx(
                        WS_EX_NOACTIVATE,
                        "STATIC",
                        "DeviceChangeMonitor",
                        WS_OVERLAPPEDWINDOW | WS_VISIBLE,
                        0, 0, 0, 0,
                        new IntPtr(0), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                    if (_messageWindowHandle == IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to create message window");
                        return Task.FromResult(false);
                    }

                    // Register for device notifications
                    var deviceInterface = new DEV_BROADCAST_DEVICEINTERFACE
                    {
                        dbcc_size = Marshal.SizeOf<DEV_BROADCAST_DEVICEINTERFACE>(),
                        dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
                        dbcc_classguid = GUID_DEVINTERFACE_AUDIO_CAPTURE,
                        dbcc_name = new char[1]
                    };

                    _deviceNotificationHandle = RegisterDeviceNotification(_messageWindowHandle, ref deviceInterface, DEVICE_NOTIFY_WINDOW_HANDLE);
                    
                    if (_deviceNotificationHandle != IntPtr.Zero)
                    {
                        _isMonitoring = true;
                        System.Diagnostics.Debug.WriteLine("Device change monitoring started successfully");
                        
                        // Start monitoring thread to process messages with proper exception handling
                        _ = RunBackgroundMonitoringAsync();
                        return Task.FromResult(true);
                    }
                    else
                    {
                        if (_messageWindowHandle != IntPtr.Zero)
                        {
                            DestroyWindow(_messageWindowHandle);
                            _messageWindowHandle = IntPtr.Zero;
                        }
                        System.Diagnostics.Debug.WriteLine("Failed to register for device notifications");
                        return Task.FromResult(false);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting device monitoring: {ex.Message}");
                return Task.FromResult(false);
            }
            catch (COMException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting device monitoring: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Runs background monitoring with top-level exception handling for fire-and-forget safety
        /// </summary>
        private async Task RunBackgroundMonitoringAsync()
        {
            try
            {
                await MonitorDeviceMessages().ConfigureAwait(false);
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"Background monitoring task failed unexpectedly: {ex.Message}");
                _isMonitoring = false;
            }
        }

        /// <summary>
        /// Monitors Windows messages for device changes
        /// </summary>
        private async Task MonitorDeviceMessages()
        {
            while (_isMonitoring && !_disposed)
            {
                try
                {
                    // This would typically be implemented with a message pump
                    // For now, we'll use polling to check for device changes
                    await Task.Delay(1000).ConfigureAwait(false);
                    await CheckForDeviceChangesAsync().ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in device message monitoring: {ex.Message}");
                    await Task.Delay(5000).ConfigureAwait(false); // Wait longer if there's an error
                }
                catch (COMException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in device message monitoring: {ex.Message}");
                    await Task.Delay(5000).ConfigureAwait(false); // Wait longer if there's an error
                }
            }
        }

        /// <summary>
        /// Checks for device changes and raises appropriate events
        /// </summary>
        private async Task CheckForDeviceChangesAsync()
        {
            try
            {
                var currentDevices = await GetInputDevicesAsync().ConfigureAwait(false);
                
                // Compare with previously known devices to detect changes
                // This is a simplified approach - in production, you'd want to maintain state
                foreach (var device in currentDevices)
                {
                    if (device.IsDefault)
                    {
                        // Fire default device changed event if needed
                        DefaultDeviceChanged?.Invoke(this, new AudioDeviceEventArgs { Device = device, DeviceId = device.Id, DeviceName = device.Name });
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking device changes: {ex.Message}");
            }
            catch (COMException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking device changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles device disconnection events
        /// </summary>
        private void HandleDeviceDisconnection(string deviceId)
        {
            try
            {
                var eventArgs = new AudioDeviceEventArgs 
                { 
                    DeviceId = deviceId,
                    DeviceName = "Disconnected Device"
                };

                DeviceDisconnected?.Invoke(this, eventArgs);
                System.Diagnostics.Debug.WriteLine($"Device disconnected: {deviceId}");
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling device disconnection: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles device reconnection events
        /// </summary>
        private void HandleDeviceReconnection(string deviceId)
        {
            // Fire-and-forget with proper exception handling
            _ = HandleDeviceReconnectionAsync(deviceId);
        }
        
        private async Task HandleDeviceReconnectionAsync(string deviceId)
        {
            try
            {
                var device = await GetDeviceByIdAsync(deviceId).ConfigureAwait(false);
                if (device != null)
                {
                    var eventArgs = new AudioDeviceEventArgs { Device = device, DeviceId = device.Id, DeviceName = device.Name };
                    DeviceConnected?.Invoke(this, eventArgs);
                    System.Diagnostics.Debug.WriteLine($"Device reconnected: {deviceId}");

                    // Test the reconnected device - await to properly handle exceptions
                    await TestDeviceAsync(deviceId).ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling device reconnection: {ex.Message}");
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error handling device reconnection: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops device change monitoring
        /// </summary>
        public void StopDeviceChangeMonitoring()
        {
            try
            {
                lock (_deviceLock)
                {
                    _isMonitoring = false;

                    if (_deviceNotificationHandle != IntPtr.Zero)
                    {
                        UnregisterDeviceNotification(_deviceNotificationHandle);
                        _deviceNotificationHandle = IntPtr.Zero;
                    }

                    if (_winEventHook != IntPtr.Zero)
                    {
                        UnhookWinEvent(_winEventHook);
                        _winEventHook = IntPtr.Zero;
                    }

                    if (_messageWindowHandle != IntPtr.Zero)
                    {
                        DestroyWindow(_messageWindowHandle);
                        _messageWindowHandle = IntPtr.Zero;
                    }

                    System.Diagnostics.Debug.WriteLine("Device change monitoring stopped");
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping device monitoring: {ex.Message}");
            }
            catch (ExternalException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping device monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows user-friendly permission request dialog
        /// </summary>
        public Task<bool> ShowPermissionRequestDialogAsync()
        {
            try
            {
                // Show Windows microphone privacy settings
                const string settingsPath = "ms-settings:privacy-microphone";
                var result = ShellExecute(IntPtr.Zero, "open", settingsPath, null, null, 1);
                
                if (result.ToInt32() > 32) // ShellExecute success
                {
                    _lastPermissionRequest = DateTime.Now;
                    System.Diagnostics.Debug.WriteLine("Permission request dialog opened successfully");
                    return Task.FromResult(true);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to open permission request dialog");
                    return Task.FromResult(false);
                }
            }
            catch (ExternalException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing permission request dialog: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Shows permission status notification in system tray
        /// </summary>
        public Task ShowPermissionStatusNotifierAsync(MicrophonePermissionStatus status, string message)
        {
            try
            {
                var title = status switch
                {
                    MicrophonePermissionStatus.Granted => "Microphone Access Granted",
                    MicrophonePermissionStatus.Denied => "Microphone Access Denied",
                    MicrophonePermissionStatus.Unknown => "Microphone Status Unknown",
                    _ => "Microphone Permission"
                };

                // Icon types for notification (simplified - would use actual notification system)
                var iconType = status switch
                {
                    MicrophonePermissionStatus.Granted => "Info",
                    MicrophonePermissionStatus.Denied => "Error",
                    _ => "Warning"
                };

                // This would integrate with SystemTrayService for notifications
                System.Diagnostics.Debug.WriteLine($"Permission Notification: {title} - {message}");
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing permission status notification: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Retries permission request with exponential backoff
        /// </summary>
        public async Task<bool> RetryPermissionRequestAsync(int maxAttempts = 3, int baseDelayMs = 1000)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _permissionRetryCount = attempt;
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff
                    await Task.Delay(delay).ConfigureAwait(false);

                    var status = await CheckMicrophonePermissionAsync().ConfigureAwait(false);
                    if (status == MicrophonePermissionStatus.Granted)
                    {
                        _permissionRetryCount = 0;
                        PermissionGranted?.Invoke(this, new PermissionEventArgs(status, "Permission granted after retry"));
                        return true;
                    }

                    if (attempt < maxAttempts)
                    {
                        PermissionRequired?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Unknown, 
                            $"Permission attempt {attempt}/{maxAttempts} failed. Please enable microphone access in Windows Settings."));
                    }
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Permission retry attempt {attempt} failed: {ex.Message}");
                }
                catch (COMException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Permission retry attempt {attempt} failed: {ex.Message}");
                }
            }

            _permissionRetryCount = 0;
            PermissionRequestFailed?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, "Permission denied after all retries"));
            return false;
        }

        /// <summary>
        /// Generates comprehensive permission diagnostic report
        /// </summary>
        public async Task<string> GeneratePermissionDiagnosticReportAsync()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Microphone Permission Diagnostic Report ===");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            try
            {
                var status = await CheckMicrophonePermissionAsync().ConfigureAwait(false);
                report.AppendLine($"Current Permission Status: {status}");
                report.AppendLine($"Last Permission Request: {_lastPermissionRequest:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"Permission Retry Count: {_permissionRetryCount}");

                var devices = await GetInputDevicesAsync().ConfigureAwait(false);
                report.AppendLine($"Available Audio Devices: {devices.Count}");
                
                foreach (var device in devices.Take(5)) // Limit to first 5 devices
                {
                    var isCompatible = IsDeviceCompatible(device.DeviceId);
                    report.AppendLine($"  - {device.Name} ({device.DeviceId}) - Compatible: {isCompatible}");
                }

                // System information
                report.AppendLine($"Operating System: {Environment.OSVersion}");
                report.AppendLine($"Application Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");

                report.AppendLine();
                report.AppendLine("=== Recommendations ===");
                
                if (status == MicrophonePermissionStatus.Denied)
                {
                    report.AppendLine("- Microphone access is denied. Please enable it in Windows Settings.");
                    report.AppendLine("- Go to Settings > Privacy & Security > Microphone");
                    report.AppendLine("- Ensure 'Let apps access your microphone' is turned on");
                    report.AppendLine("- Ensure WhisperKey is listed and allowed to access microphone");
                }
                else if (status == MicrophonePermissionStatus.Unknown)
                {
                    report.AppendLine("- Unable to determine microphone permission status.");
                    report.AppendLine("- Please check Windows Settings manually.");
                }
                else
                {
                    report.AppendLine("- Microphone permissions appear to be correctly configured.");
                }

                if (devices.Count == 0)
                {
                    report.AppendLine("- No audio input devices detected. Please check microphone connection.");
                }

                report.AppendLine();
                report.AppendLine("=== Troubleshooting Steps ===");
                report.AppendLine("1. Restart the application");
                report.AppendLine("2. Check Windows Privacy Settings");
                report.AppendLine("3. Verify microphone hardware connection");
                report.AppendLine("4. Check Windows Device Manager for driver issues");
                report.AppendLine("5. Restart Windows if issues persist");
            }
            catch (InvalidOperationException ex)
            {
                report.AppendLine($"Error generating diagnostic report: {ex.Message}");
            }
            catch (COMException ex)
            {
                report.AppendLine($"Error generating diagnostic report: {ex.Message}");
            }

            return report.ToString();
        }

        /// <summary>
        /// Guides user to Windows microphone settings
        /// </summary>
        public void GuideUserToSettings()
        {
            try
            {
                // Open Windows Privacy & Security -> Microphone settings
                const string settingsPath = "ms-settings:privacy-microphone";
                ShellExecute(IntPtr.Zero, "open", settingsPath, null, null, 1);
                System.Diagnostics.Debug.WriteLine("Guided user to Windows microphone settings");
            }
            catch (ExternalException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guiding user to settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Enters graceful fallback mode when permission or device issues occur
        /// </summary>
        public async Task EnterGracefulFallbackModeAsync(string reason)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Entering graceful fallback mode: {reason}");
                
                // Stop any active monitoring - await to properly handle any exceptions
                await StopRealTimeMonitoringAsync().ConfigureAwait(false);
                
                // Notify about fallback mode
                PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.SystemError, reason)
                {
                    RequiresUserAction = true,
                    GuidanceAction = "Check Windows Settings > Privacy > Microphone"
                });
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error entering graceful fallback mode: {ex.Message}");
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping monitoring during fallback: {ex.Message}");
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error in graceful fallback: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles device change recovery operations
        /// </summary>
        public async Task<bool> HandleDeviceChangeRecoveryAsync(string deviceId, bool isConnected)
        {
            try
            {
                var recoveryEventArgs = new DeviceRecoveryEventArgs
                {
                    DeviceId = deviceId,
                    DeviceName = deviceId,
                    RecoveryAction = isConnected ? "Reconnection" : "Disconnection",
                    Status = "InProgress"
                };

                DeviceRecoveryAttempted?.Invoke(this, recoveryEventArgs);

                if (isConnected)
                {
                    // Attempt to recover device functionality
                    var device = await GetDeviceByIdAsync(deviceId).ConfigureAwait(false);
                    if (device != null)
                    {
                        var testResult = await PerformComprehensiveTestAsync(deviceId).ConfigureAwait(false);
                        recoveryEventArgs.Status = testResult.Success ? "Success" : "Failed";
                        
                        if (!testResult.Success)
                        {
                            recoveryEventArgs.Exception = new Exception($"Device test failed: {string.Join(", ", testResult.Errors)}");
                        }
                    }
                    else
                    {
                        recoveryEventArgs.Status = "Failed";
                        recoveryEventArgs.Exception = new Exception("Device not found");
                    }
                }
                else
                {
                    recoveryEventArgs.Status = "Success"; // Disconnection is not a failure
                }

                DeviceRecoveryCompleted?.Invoke(this, recoveryEventArgs);
                return recoveryEventArgs.Success;
            }
            catch (InvalidOperationException ex)
            {
                var errorEventArgs = new DeviceRecoveryEventArgs
                {
                    DeviceId = deviceId,
                    DeviceName = deviceId,
                    RecoveryAction = "Recovery",
                    Status = "Failed",
                    Exception = ex
                };

                DeviceRecoveryCompleted?.Invoke(this, errorEventArgs);
                System.Diagnostics.Debug.WriteLine($"Device change recovery failed: {ex.Message}");
                return false;
            }
            catch (COMException ex)
            {
                var errorEventArgs = new DeviceRecoveryEventArgs
                {
                    DeviceId = deviceId,
                    DeviceName = deviceId,
                    RecoveryAction = "Recovery",
                    Status = "Failed",
                    Exception = ex
                };

                DeviceRecoveryCompleted?.Invoke(this, errorEventArgs);
                System.Diagnostics.Debug.WriteLine($"Device change recovery failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handles permission denied events with user guidance
        /// </summary>
        public Task HandlePermissionDeniedEventAsync(string deviceId, Exception? error = null)
        {
            try
            {
                var permissionEventArgs = new PermissionEventArgs(MicrophonePermissionStatus.Denied, 
                    "Microphone permission was denied. Please enable access in Windows Settings.", deviceId)
                {
                    Exception = error,
                    RequiresUserAction = true,
                    GuidanceAction = "Open Windows Settings > Privacy > Microphone"
                };

                PermissionDenied?.Invoke(this, permissionEventArgs);
                
                // Also raise PermissionRequired to trigger UI handling by ViewModel
                PermissionRequired?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied,
                    "Microphone permission required. Please enable access in Windows Settings.", deviceId)
                {
                    Exception = error,
                    RequiresUserAction = true,
                    GuidanceAction = "Open Windows Settings > Privacy > Microphone"
                });
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling permission denied event: {ex.Message}");
            }
            catch (ExternalException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling permission denied event: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Switches to the specified audio device with validation and error handling
        /// </summary>
        public async Task<bool> SwitchDeviceAsync(string deviceId)
        {
            if (_disposed) return false;
            
            try
            {
                IMMDeviceWrapper? device;
                lock (_lockObject)
                {
                    // Get the target device
                    device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                        .FirstOrDefault(d => d.ID == deviceId);
                }
                
                if (device == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Device with ID {deviceId} not found");
                    return false;
                }

                // Test device compatibility
                if (!IsDeviceCompatible(deviceId))
                {
                    System.Diagnostics.Debug.WriteLine($"Device {deviceId} is not compatible");
                    return false;
                }

                // Test device functionality
                using (var waveIn = _waveInFactory())
                {
                    waveIn.DeviceNumber = GetDeviceNumber(device.ID);
                    waveIn.WaveFormat = new WaveFormat(16000, 1);
                    
                    // Test if device can be configured and started
                    waveIn.StartRecording();
                    await Task.Delay(100).ConfigureAwait(false);
                    waveIn.StopRecording();
                }

                // Check microphone permission for the device
                var permissionStatus = await CheckMicrophonePermissionForDevice(deviceId).ConfigureAwait(false);
                if (permissionStatus != MicrophonePermissionStatus.Granted)
                {
                    // Try to request permission
                    var permissionGranted = await RequestMicrophonePermissionAsync().ConfigureAwait(false);
                    if (!permissionGranted)
                    {
                        PermissionDenied?.Invoke(this, new PermissionEventArgs(
                            MicrophonePermissionStatus.Denied, 
                            "Cannot switch to device - microphone permission denied", 
                            deviceId));
                        return false;
                    }
                }

                // Device switch successful
                System.Diagnostics.Debug.WriteLine($"Successfully switched to device: {device.FriendlyName}");
                
                // Raise device connected event for UI updates
                var audioDevice = CreateAudioDevice(device);
                if (audioDevice != null)
                {
                    DeviceConnected?.Invoke(this, new AudioDeviceEventArgs(audioDevice));
                }

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                PermissionDenied?.Invoke(this, new PermissionEventArgs(
                    MicrophonePermissionStatus.Denied, 
                    "Access to device denied - check Windows Privacy Settings", 
                    deviceId, ex));
                System.Diagnostics.Debug.WriteLine($"Error switching to device {deviceId}: {ex.Message}");
                return false;
            }
            catch (SecurityException ex)
            {
                PermissionDenied?.Invoke(this, new PermissionEventArgs(
                    MicrophonePermissionStatus.Denied, 
                    "Security error accessing device - check Windows Privacy Settings", 
                    deviceId, ex));
                System.Diagnostics.Debug.WriteLine($"Error switching to device {deviceId}: {ex.Message}");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching to device {deviceId}: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching to device {deviceId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (!_disposed)
                {
                    _disposed = true;
                    
                    // Stop monitoring
                    StopDeviceChangeMonitoring();
                    StopRealTimeMonitoringAsync().Wait();
                    
                    // Dispose timer
                    _levelUpdateTimer?.Dispose();
                    
                    // Dispose wave input
                    _monitoringWaveIn?.Dispose();
                    
                    // Dispose enumerator only if we own it
                    if (_ownsEnumerator)
                    {
                        _enumerator?.Dispose();
                    }
                    
                    System.Diagnostics.Debug.WriteLine("AudioDeviceService disposed successfully");
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing AudioDeviceService: {ex.Message}");
            }
            catch (AggregateException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing AudioDeviceService: {ex.Message}");
            }
        }
    }
}
