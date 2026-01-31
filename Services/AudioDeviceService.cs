using NAudio.CoreAudioApi;
using NAudio.Wave;
using ScottWisper.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace ScottWisper.Services
{
    public interface IAudioDeviceService
    {
        event EventHandler<AudioDeviceEventArgs> DeviceConnected;
        event EventHandler<AudioDeviceEventArgs> DeviceDisconnected;
        event EventHandler<AudioDeviceEventArgs> DefaultDeviceChanged;
        
        // Permission events
        event EventHandler<PermissionEventArgs> PermissionDenied;
        event EventHandler<PermissionEventArgs> PermissionGranted;
        event EventHandler<PermissionEventArgs> PermissionRequestFailed;
        
        // Device change recovery events
        event EventHandler<DeviceRecoveryEventArgs> DeviceRecoveryAttempted;
        event EventHandler<DeviceRecoveryEventArgs> DeviceRecoveryCompleted;
        
        Task<List<AudioDevice>> GetInputDevicesAsync();
        Task<List<AudioDevice>> GetOutputDevicesAsync();
        Task<AudioDevice> GetDefaultInputDeviceAsync();
        Task<AudioDevice> GetDefaultOutputDeviceAsync();
        Task<bool> TestDeviceAsync(string deviceId);
        Task<AudioDeviceCapabilities> GetDeviceCapabilitiesAsync(string deviceId);
        Task<AudioDevice?> GetDeviceByIdAsync(string deviceId);
        bool IsDeviceCompatible(string deviceId);
        
        // Permission methods
        Task<MicrophonePermissionStatus> CheckMicrophonePermissionAsync();
        Task<bool> RequestMicrophonePermissionAsync();
        
        // Device switching
        Task<bool> SwitchDeviceAsync(string deviceId);
        
        // Enhanced testing and monitoring
        Task<AudioDeviceTestResult> PerformComprehensiveTestAsync(string deviceId);
        Task<AudioQualityMetrics> AnalyzeAudioQualityAsync(string deviceId, int durationMs = 3000);
        Task<DeviceCompatibilityScore> ScoreDeviceCompatibilityAsync(string deviceId);
        Task<bool> TestDeviceLatencyAsync(string deviceId);
        Task<List<DeviceRecommendation>> GetDeviceRecommendationsAsync();
        event EventHandler<AudioLevelEventArgs> AudioLevelUpdated;
        Task StartRealTimeMonitoringAsync(string deviceId);
        Task StopRealTimeMonitoringAsync();
        
        // Enhanced device change monitoring methods
        Task<bool> MonitorDeviceChangesAsync();
        void StopDeviceChangeMonitoring();
        Task<bool> ShowPermissionRequestDialogAsync();
        void GuideUserToSettings();
        Task ShowPermissionStatusNotifierAsync(MicrophonePermissionStatus status, string message);
        Task<bool> RetryPermissionRequestAsync(int maxAttempts = 3, int baseDelayMs = 1000);
        Task<string> GeneratePermissionDiagnosticReportAsync();
        void OpenWindowsMicrophoneSettings();
        Task EnterGracefulFallbackModeAsync(string reason);
        Task<bool> HandleDeviceChangeRecoveryAsync(string deviceId, bool isConnected);
        Task HandlePermissionDeniedEventAsync(string deviceId, Exception? error = null);
    }

    public class AudioDeviceService : IAudioDeviceService, IDisposable
    {
        private readonly MMDeviceEnumerator _enumerator;
        private readonly object _lockObject = new object();
        private bool _disposed = false;
        private WaveInEvent? _monitoringWaveIn;
        private System.Threading.Timer? _levelUpdateTimer;
        private float _currentAudioLevel = 0f;
        private bool _isMonitoring = false;
        
        // Device change monitoring
        private IntPtr _deviceNotificationHandle = IntPtr.Zero;
        private WinEventDelegate? _winEventDelegate;
        private IntPtr _winEventHook = IntPtr.Zero;
        private IntPtr _messageWindowHandle = IntPtr.Zero;
        private readonly object _deviceLock = new object();
        private int _permissionRetryCount = 0;
        private DateTime _lastPermissionRequest = DateTime.MinValue;

        public event EventHandler<AudioDeviceEventArgs>? DeviceConnected;
        public event EventHandler<AudioDeviceEventArgs>? DeviceDisconnected;
        public event EventHandler<AudioDeviceEventArgs>? DefaultDeviceChanged;
        public event EventHandler<PermissionEventArgs>? PermissionDenied;
        public event EventHandler<PermissionEventArgs>? PermissionGranted;
        public event EventHandler<PermissionEventArgs>? PermissionRequestFailed;
        public event EventHandler<AudioLevelEventArgs>? AudioLevelUpdated;
        public event EventHandler<DeviceRecoveryEventArgs>? DeviceRecoveryAttempted;
        public event EventHandler<DeviceRecoveryEventArgs>? DeviceRecoveryCompleted;

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

    public AudioDeviceService()
    {
        _enumerator = new MMDeviceEnumerator();
        
        // Initialize device change monitoring
        _ = Task.Run(async () =>
        {
            var monitoringStarted = await MonitorDeviceChangesAsync();
            if (monitoringStarted)
            {
                System.Diagnostics.Debug.WriteLine("Device change monitoring initialized successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Failed to initialize device change monitoring");
            }
        });
    }

        public async Task<List<AudioDevice>> GetInputDevicesAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return new List<AudioDevice>();

                    try
                    {
                        // Check microphone permission first
                        var permissionStatus = CheckMicrophonePermissionAsync().Result;
                        
                        if (permissionStatus == MicrophonePermissionStatus.Denied)
                        {
                            PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, 
                                "Microphone access is denied. Please enable microphone access in Windows Settings Privacy -> Microphone."));
                            return new List<AudioDevice>();
                        }

                        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                        var audioDevices = devices.Select(CreateAudioDevice).Where(d => d != null).ToList()!;

                        // Filter devices based on permission status
                        if (!permissionStatus.Equals(MicrophonePermissionStatus.Granted))
                        {
                            audioDevices = audioDevices.Where(d => d.PermissionStatus != MicrophonePermissionStatus.Denied).ToList();
                        }

                        return audioDevices;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, 
                            "Access to audio devices was denied. Please check Windows Privacy Settings.", ""));
                        System.Diagnostics.Debug.WriteLine($"Error enumerating input devices (permission denied): {ex.Message}");
                        return new List<AudioDevice>();
                    }
                    catch (SecurityException ex)
                    {
                        PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, 
                            "Security error accessing audio devices. Please check Windows Privacy Settings.", ""));
                        System.Diagnostics.Debug.WriteLine($"Error enumerating input devices (security): {ex.Message}");
                        return new List<AudioDevice>();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error enumerating input devices: {ex.Message}");
                        return new List<AudioDevice>();
                    }
                }
            });
        }

        public async Task<List<AudioDevice>> GetOutputDevicesAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return new List<AudioDevice>();

                    try
                    {
                        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                        return devices.Select(CreateAudioDevice).Where(d => d != null).ToList()!;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error enumerating output devices: {ex.Message}");
                        return new List<AudioDevice>();
                    }
                }
            });
        }

        public async Task<AudioDevice> GetDefaultInputDeviceAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(AudioDeviceService));

                    try
                    {
                        var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                        return CreateAudioDevice(device)!;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting default input device: {ex.Message}");
                        throw new InvalidOperationException("Unable to get default input device", ex);
                    }
                }
            });
        }

        public async Task<AudioDevice> GetDefaultOutputDeviceAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(AudioDeviceService));

                    try
                    {
                        var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                        return CreateAudioDevice(device)!;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting default output device: {ex.Message}");
                        throw new InvalidOperationException("Unable to get default output device", ex);
                    }
                }
            });
        }

        public async Task<bool> TestDeviceAsync(string deviceId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return false;

                    try
                    {
                        var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                            .FirstOrDefault(d => d.ID == deviceId);
                        
                        if (device == null) return false;

                        // Test basic device functionality
                        using (var waveIn = new WaveInEvent())
                        {
                            waveIn.DeviceNumber = GetDeviceNumber(device.ID);
                            
                            // Test if we can configure the device for basic recording
                            waveIn.WaveFormat = new WaveFormat(16000, 1); // 16kHz mono for speech recognition
                            
                            // Try to start recording briefly
                            waveIn.StartRecording();
                            Thread.Sleep(100); // Very brief test
                            waveIn.StopRecording();
                            
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error testing device {deviceId}: {ex.Message}");
                        return false;
                    }
                }
            });
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
                using (var waveIn = new WaveInEvent())
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
                    catch
                    {
                        result.BasicFunctionality = false;
                    }
                }

                // Test 2: Format support (sync, no lock needed)
                result.SupportedFormats = string.Join(", ", GetSupportedFormats(device) ?? new List<string>());

                // Test 3: Quality assessment (async, no lock needed)
                var qualityScore = await Task.Run(() => AssessDeviceQualityAsync(device));
                result.QualityScore = (int)qualityScore;

                // Test 4: Latency measurement (async, no lock needed)
                result.LatencyMs = await Task.Run(() => MeasureDeviceLatencyAsync(device));

                // Test 5: Noise floor measurement (async, no lock needed)
                result.NoiseFloorDb = await Task.Run(() => MeasureNoiseFloorAsync(device));

                result.Success = result.BasicFunctionality && result.QualityScore > 0.3f;
                result.TestCompleted = DateTime.Now;
                result.TestTime = result.TestCompleted - result.TestStarted;
                return result;
            }
            catch (Exception ex)
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
            return await Task.Run(async () =>
            {
                MMDevice? device;
                lock (_lockObject)
                {
                    if (_disposed) return new AudioQualityMetrics();

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

                    using (var waveIn = new WaveInEvent())
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
                        await Task.Delay(durationMs);
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error analyzing audio quality for {deviceId}: {ex.Message}");
                    return new AudioQualityMetrics { DeviceId = deviceId };
                }
            });
        }

        public async Task<DeviceCompatibilityScore> ScoreDeviceCompatibilityAsync(string deviceId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return new DeviceCompatibilityScore();

                    try
                    {
                        var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                            .FirstOrDefault(d => d.ID == deviceId);
                        
                        if (device == null) return new DeviceCompatibilityScore();

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
                                score.SampleRateScore = Math.Min(1.0f, 16000f / format.SampleRate);
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

                        return score;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error scoring device compatibility for {deviceId}: {ex.Message}");
                        return new DeviceCompatibilityScore { DeviceId = deviceId };
                    }
                }
            });
        }

        public async Task<bool> TestDeviceLatencyAsync(string deviceId)
        {
            return await Task.Run(async () =>
            {
                MMDevice? device;
                lock (_lockObject)
                {
                    if (_disposed) return false;

                    device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                        .FirstOrDefault(d => d.ID == deviceId);
                }
                
                if (device == null) return false;

                try
                {
                    using (var waveIn = new WaveInEvent())
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
                        await Task.Delay(1000);
                        
                        waveIn.StopRecording();

                        // Consider latency acceptable if < 200ms
                        return latencyStopwatch.ElapsedMilliseconds < 200;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error testing device latency for {deviceId}: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<List<DeviceRecommendation>> GetDeviceRecommendationsAsync()
        {
            var inputDevices = await GetInputDevicesAsync();
            var recommendations = new List<DeviceRecommendation>();

            foreach (var device in inputDevices)
            {
                var score = await ScoreDeviceCompatibilityAsync(device.Id);
                
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

        public async Task StartRealTimeMonitoringAsync(string deviceId)
        {
            await Task.Run(async () =>
            {
                if (_disposed || _isMonitoring) return;
                
                lock (_lockObject)
                {

                    try
                    {
                        var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                            .FirstOrDefault(d => d.ID == deviceId);
                        
                        if (device == null) return;

                        _monitoringWaveIn = new WaveInEvent();
                        _monitoringWaveIn.DeviceNumber = GetDeviceNumber(device.ID);
                        _monitoringWaveIn.WaveFormat = new WaveFormat(16000, 1);
                        _monitoringWaveIn.BufferMilliseconds = 50;

                        _monitoringWaveIn.DataAvailable += OnMonitoringDataAvailable;
                        
                        _levelUpdateTimer = new System.Threading.Timer(UpdateAudioLevel, null, 0, 50);
                        
                        _monitoringWaveIn.StartRecording();
                        _isMonitoring = true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error starting real-time monitoring for {deviceId}: {ex.Message}");
                    }
                }
            });
        }

        public async Task StopRealTimeMonitoringAsync()
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (!_isMonitoring) return;

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
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error stopping real-time monitoring: {ex.Message}");
                    }
                }
            });
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

        private List<string> GetSupportedFormats(MMDevice device)
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

                using (var waveIn = new WaveInEvent())
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
                        catch
                        {
                            // Format not supported
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting supported formats: {ex.Message}");
            }

            return formats;
        }

        private async Task<float> AssessDeviceQualityAsync(MMDevice device)
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
            catch
            {
                return 0.1f;
            }
        }

        private async Task<int> MeasureDeviceLatencyAsync(MMDevice device)
        {
            try
            {
                using (var waveIn = new WaveInEvent())
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
                    await Task.Delay(500); // Wait up to 500ms for first buffer
                    waveIn.StopRecording();

                    return (int)stopwatch.ElapsedMilliseconds;
                }
            }
            catch
            {
                return 999; // High latency value indicating measurement failed
            }
        }

        private async Task<float> MeasureNoiseFloorAsync(MMDevice device)
        {
            try
            {
                using (var waveIn = new WaveInEvent())
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
                    await Task.Delay(1000); // Measure for 1 second
                    waveIn.StopRecording();

                    // Calculate noise floor (minimum level during quiet period)
                    return minLevel;
                }
            }
            catch
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

        public async Task<AudioDeviceCapabilities> GetDeviceCapabilitiesAsync(string deviceId)
        {
            return await Task.Run(() =>
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

                        return capabilities;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting device capabilities for {deviceId}: {ex.Message}");
                        throw new InvalidOperationException("Unable to get device capabilities", ex);
                    }
                }
            });
        }

        public async Task<AudioDevice?> GetDeviceByIdAsync(string deviceId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return null;

                    try
                    {
                        var device = _enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)
                            .FirstOrDefault(d => d.ID == deviceId);
                        
                        return device != null ? CreateAudioDevice(device) : null;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting device by ID {deviceId}: {ex.Message}");
                        return null;
                    }
                }
            });
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking device compatibility for {deviceId}: {ex.Message}");
                    return false;
                }
            }
        }

        private AudioDevice? CreateAudioDevice(MMDevice device)
        {
            try
            {
                var audioDevice = new AudioDevice
                {
                    Id = device.ID,
                    Name = device.FriendlyName,
                    Description = device.DeviceFriendlyName,
                    DataFlow = device.DataFlow == DataFlow.Capture ? AudioDataFlow.Capture : AudioDataFlow.Render,
                    State = device.State == NAudio.CoreAudioApi.DeviceState.Active ? AudioDeviceState.Active : AudioDeviceState.Disabled,
                    IsDefault = false // Will be determined by context
                };

                // Check permission status for input devices
                if (audioDevice.DataFlow == AudioDataFlow.Capture)
                {
                    audioDevice.PermissionStatus = CheckMicrophonePermissionForDevice(device.ID).Result;
                }

                return audioDevice;
            }
            catch (Exception ex)
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

        public async Task<MicrophonePermissionStatus> CheckMicrophonePermissionAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return MicrophonePermissionStatus.SystemError;

                    try
                    {
                        // Try to access default audio input device to check permissions
                        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                        if (!devices.Any())
                        {
                            return MicrophonePermissionStatus.Denied;
                        }

                        // Test with first available device
                        var testDevice = devices.First();
                        return CheckMicrophonePermissionForDevice(testDevice.ID).Result;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return MicrophonePermissionStatus.Denied;
                    }
                    catch (SecurityException)
                    {
                        return MicrophonePermissionStatus.Denied;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error checking microphone permission: {ex.Message}");
                        return MicrophonePermissionStatus.SystemError;
                    }
                }
            });
        }

        public async Task<bool> RequestMicrophonePermissionAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return false;

                    try
                    {
                        // On Windows, we trigger the permission request by attempting to access the microphone
                        // This will show the Windows permission dialog if not already granted
                        var currentStatus = CheckMicrophonePermissionAsync().Result;
                        
                        if (currentStatus == MicrophonePermissionStatus.Granted)
                        {
                            PermissionGranted?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Granted, "Microphone permission already granted"));
                            return true;
                        }

                        // Try to trigger permission dialog by attempting device access
                        try
                        {
                            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                            if (!devices.Any())
                            {
                                PermissionRequestFailed?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, "No audio input devices available"));
                                return false;
                            }

                            // Attempt to access device to trigger permission dialog
                            var testDevice = devices.First();
                            using (var waveIn = new WaveInEvent())
                            {
                                waveIn.DeviceNumber = GetDeviceNumber(testDevice.ID);
                                waveIn.WaveFormat = new WaveFormat(16000, 1);
                                
                                // This should trigger the permission dialog if needed
                                waveIn.StartRecording();
                                Thread.Sleep(100);
                                waveIn.StopRecording();
                            }

                            // Check if permission was granted
                            var newStatus = CheckMicrophonePermissionForDevice(testDevice.ID).Result;
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
                        catch (UnauthorizedAccessException ex)
                        {
                            PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, "Access to microphone was denied. Please enable microphone access in Windows Settings.", ""));
                            return false;
                        }
                        catch (SecurityException ex)
                        {
                            PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.Denied, "Security error accessing microphone. Please check Windows Privacy Settings.", ""));
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error requesting microphone permission: {ex.Message}");
                        PermissionRequestFailed?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.SystemError, $"System error requesting permission: {ex.Message}"));
                        return false;
                    }
                }
            });
        }

        private async Task<MicrophonePermissionStatus> CheckMicrophonePermissionForDevice(string deviceId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Try to create and configure a WaveInEvent to test permission
                    using (var waveIn = new WaveInEvent())
                    {
                        waveIn.DeviceNumber = GetDeviceNumber(deviceId);
                        waveIn.WaveFormat = new WaveFormat(16000, 1); // 16kHz mono
                        
                        // Try to start recording briefly to test permission
                        waveIn.StartRecording();
                        Thread.Sleep(50); // Very brief test
                        waveIn.StopRecording();
                        
                        return MicrophonePermissionStatus.Granted;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    return MicrophonePermissionStatus.Denied;
                }
                catch (SecurityException)
                {
                    return MicrophonePermissionStatus.Denied;
                }
                catch (Exception ex)
                {
                    // Device not available or other system error
                    if (ex.Message.Contains("not found") || ex.Message.Contains("unavailable"))
                    {
                        return MicrophonePermissionStatus.SystemError;
                    }
                    return MicrophonePermissionStatus.Denied;
                }
            });
        }

        public void OpenWindowsMicrophoneSettings()
        {
            try
            {
                // Open Windows Privacy & Security -> Microphone settings
                const string settingsPath = "ms-settings:privacy-microphone";
                ShellExecute(IntPtr.Zero, "open", settingsPath, null, null, 1);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening Windows microphone settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts monitoring for device changes using WM_DEVICECHANGE
        /// </summary>
        public async Task<bool> MonitorDeviceChangesAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    lock (_deviceLock)
                    {
                        if (_isMonitoring) return true;

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
                            return false;
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
                            
                            // Start monitoring thread to process messages
                            _ = Task.Run(() => MonitorDeviceMessages());
                            return true;
                        }
                        else
                        {
                            if (_messageWindowHandle != IntPtr.Zero)
                            {
                                DestroyWindow(_messageWindowHandle);
                                _messageWindowHandle = IntPtr.Zero;
                            }
                            System.Diagnostics.Debug.WriteLine("Failed to register for device notifications");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting device monitoring: {ex.Message}");
                    return false;
                }
            });
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
                    await Task.Delay(1000);
                    await CheckForDeviceChangesAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in device message monitoring: {ex.Message}");
                    await Task.Delay(5000); // Wait longer if there's an error
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
                var currentDevices = await GetInputDevicesAsync();
                
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
            catch (Exception ex)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling device disconnection: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles device reconnection events
        /// </summary>
        private async void HandleDeviceReconnection(string deviceId)
        {
            try
            {
                var device = await GetDeviceByIdAsync(deviceId);
                if (device != null)
                {
                    var eventArgs = new AudioDeviceEventArgs { Device = device, DeviceId = device.Id, DeviceName = device.Name };
                    DeviceConnected?.Invoke(this, eventArgs);
                    System.Diagnostics.Debug.WriteLine($"Device reconnected: {deviceId}");

                    // Test the reconnected device
                    _ = Task.Run(async () => await TestDeviceAsync(deviceId));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling device reconnection: {ex.Message}");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping device monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows user-friendly permission request dialog
        /// </summary>
        public async Task<bool> ShowPermissionRequestDialogAsync()
        {
            return await Task.Run(() =>
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
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to open permission request dialog");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error showing permission request dialog: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Shows permission status notification in system tray
        /// </summary>
        public async Task ShowPermissionStatusNotifierAsync(MicrophonePermissionStatus status, string message)
        {
            await Task.Run(() =>
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error showing permission status notification: {ex.Message}");
                }
            });
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
                    await Task.Delay(delay);

                    var status = await CheckMicrophonePermissionAsync();
                    if (status == MicrophonePermissionStatus.Granted)
                    {
                        _permissionRetryCount = 0;
                        PermissionGranted?.Invoke(this, new PermissionEventArgs(status, "Permission granted after retry"));
                        return true;
                    }

                    if (attempt < maxAttempts)
                    {
                        await ShowPermissionRequestDialogAsync();
                    }
                }
                catch (Exception ex)
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
                var status = await CheckMicrophonePermissionAsync();
                report.AppendLine($"Current Permission Status: {status}");
                report.AppendLine($"Last Permission Request: {_lastPermissionRequest:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"Permission Retry Count: {_permissionRetryCount}");

                var devices = await GetInputDevicesAsync();
                report.AppendLine($"Available Audio Devices: {devices.Count}");
                
                foreach (var device in devices.Take(5)) // Limit to first 5 devices
                {
                    var isCompatible = IsDeviceCompatible(device.DeviceId);
                    report.AppendLine($"  - {device.DeviceName} ({device.DeviceId}) - Compatible: {isCompatible}");
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
                    report.AppendLine("- Ensure ScottWisper is listed and allowed to access microphone");
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
            catch (Exception ex)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guiding user to settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Enters graceful fallback mode when permission or device issues occur
        /// </summary>
        public async Task EnterGracefulFallbackModeAsync(string reason)
        {
            await Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Entering graceful fallback mode: {reason}");
                    
                    // Stop any active monitoring
                    StopRealTimeMonitoringAsync().Wait();
                    
                    // Notify about fallback mode
                    PermissionDenied?.Invoke(this, new PermissionEventArgs(MicrophonePermissionStatus.SystemError, reason)
                    {
                        RequiresUserAction = true,
                        GuidanceAction = "Check Windows Settings > Privacy > Microphone"
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error entering graceful fallback mode: {ex.Message}");
                }
            });
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
                    var device = await GetDeviceByIdAsync(deviceId);
                    if (device != null)
                    {
                        var testResult = await PerformComprehensiveTestAsync(deviceId);
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
            catch (Exception ex)
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
        public async Task HandlePermissionDeniedEventAsync(string deviceId, Exception? error = null)
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

                // Auto-open settings for user convenience
                await ShowPermissionRequestDialogAsync();
                
                // Show status notification
                await ShowPermissionStatusNotifierAsync(MicrophonePermissionStatus.Denied, 
                    "Microphone access denied. Please check Windows Settings.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling permission denied event: {ex.Message}");
            }
        }

        /// <summary>
        /// Switches to the specified audio device with validation and error handling
        /// </summary>
        public async Task<bool> SwitchDeviceAsync(string deviceId)
        {
            if (_disposed) return false;
            
            return await Task.Run(async () =>
            {
                lock (_lockObject)
                {

                    try
                    {
                        // Get the target device
                        var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                            .FirstOrDefault(d => d.ID == deviceId);
                        
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
                        using (var waveIn = new WaveInEvent())
                        {
                            waveIn.DeviceNumber = GetDeviceNumber(device.ID);
                            waveIn.WaveFormat = new WaveFormat(16000, 1);
                            
                            // Test if device can be configured and started
                            waveIn.StartRecording();
                            Thread.Sleep(100); // Brief test
                            waveIn.StopRecording();
                        }

                        // Check microphone permission for the device
                        var permissionStatus = CheckMicrophonePermissionForDevice(deviceId).Result;
                        if (permissionStatus != MicrophonePermissionStatus.Granted)
                        {
                            // Try to request permission
                            var permissionGranted = RequestMicrophonePermissionAsync().Result;
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
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error switching to device {deviceId}: {ex.Message}");
                        return false;
                    }
                }
            });
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
                    
                    System.Diagnostics.Debug.WriteLine("AudioDeviceService disposed successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing AudioDeviceService: {ex.Message}");
            }
        }
    }
}
