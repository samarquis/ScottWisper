using NAudio.CoreAudioApi;
using NAudio.Wave;
using ScottWisper.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
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
        
        // Enhanced testing and monitoring
        Task<AudioDeviceTestResult> PerformComprehensiveTestAsync(string deviceId);
        Task<AudioQualityMetrics> AnalyzeAudioQualityAsync(string deviceId, int durationMs = 3000);
        Task<DeviceCompatibilityScore> ScoreDeviceCompatibilityAsync(string deviceId);
        Task<bool> TestDeviceLatencyAsync(string deviceId);
        Task<List<DeviceRecommendation>> GetDeviceRecommendationsAsync();
        event EventHandler<AudioLevelEventArgs> AudioLevelUpdated;
        Task StartRealTimeMonitoringAsync(string deviceId);
        Task StopRealTimeMonitoringAsync();
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

        public event EventHandler<AudioDeviceEventArgs>? DeviceConnected;
        public event EventHandler<AudioDeviceEventArgs>? DeviceDisconnected;
        public event EventHandler<AudioDeviceEventArgs>? DefaultDeviceChanged;
        public event EventHandler<PermissionEventArgs>? PermissionDenied;
        public event EventHandler<PermissionEventArgs>? PermissionGranted;
        public event EventHandler<PermissionEventArgs>? PermissionRequestFailed;
        public event EventHandler<AudioLevelEventArgs>? AudioLevelUpdated;

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

    public AudioDeviceService()
    {
        _enumerator = new MMDeviceEnumerator();
        // Note: For now, not using real-time device monitoring due to NAudio API complexity
        // This can be enhanced later with proper event handling
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
                        if (permissionStatus != MicrophonePermissionStatus.Granted)
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
            return await Task.Run(() =>
            {
                lock (_lockObject)
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
                            TestTime = DateTime.Now
                        };

                        // Test 1: Basic functionality
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

                        // Test 2: Format support
                        result.SupportedFormats = GetSupportedFormats(device);

                        // Test 3: Quality assessment
                        result.QualityScore = await AssessDeviceQualityAsync(device);

                        // Test 4: Latency measurement
                        result.LatencyMs = await MeasureDeviceLatencyAsync(device);

                        // Test 5: Noise floor measurement
                        result.NoiseFloorDb = await MeasureNoiseFloorAsync(device);

                        result.Success = result.BasicFunctionality && result.QualityScore > 0.3f;
                        return result;
                    }
                    catch (Exception ex)
                    {
                        return new AudioDeviceTestResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message,
                            DeviceId = deviceId,
                            TestTime = DateTime.Now
                        };
                    }
                }
            });
        }

        public async Task<AudioQualityMetrics> AnalyzeAudioQualityAsync(string deviceId, int durationMs = 3000)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return new AudioQualityMetrics();

                    try
                    {
                        var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                            .FirstOrDefault(d => d.ID == deviceId);
                        
                        if (device == null) return new AudioQualityMetrics();

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
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed || _isMonitoring) return;

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
                        
                        _levelUpdateTimer = new Timer(UpdateAudioLevel, null, 0, 50);
                        
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
                        _levelUpdateTimer?.Change(null, Timeout.Infinite, Timeout.Infinite, 0);
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

        private void OnMonitoringDataAvailable(object sender, WaveInEventArgs e)
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
                AudioLevelUpdated?.Invoke(this, new AudioLevelEventArgs
                {
                    DeviceId = "current",
                    Level = _currentAudioLevel,
                    Timestamp = DateTime.Now
                });
            }
        }

        private void UpdateAudioLevel(object state)
        {
            AudioLevelUpdated?.Invoke(this, new AudioLevelEventArgs
            {
                DeviceId = "current",
                Level = _currentAudioLevel,
                Timestamp = DateTime.Now
            });
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

                    return stopwatch.ElapsedMilliseconds;
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
                    DataFlow = device.DataFlow == DataFlow.Capture ? DeviceType.Input : DeviceType.Output,
                    State = device.State == NAudio.CoreAudioApi.DeviceState.Active ? AudioDeviceState.Active : AudioDeviceState.Disabled,
                    IsDefault = false // Will be determined by context
                };

                // Check permission status for input devices
                if (audioDevice.DataFlow == DeviceType.Input)
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

        public void Dispose()
        {
            lock (_lockObject)
            {
                if (!_disposed)
                {
                    // Stop real-time monitoring
                    _ = StopRealTimeMonitoringAsync();
                    
                    _levelUpdateTimer?.Stop();
                    _levelUpdateTimer?.Dispose();
                    _levelUpdateTimer = null;
                    
                    _monitoringWaveIn?.Dispose();
                    _monitoringWaveIn = null;
                    
                    _enumerator?.Dispose();
                    _disposed = true;
                }
            }
        }
    }

    // Supporting classes
    public class AudioDevice
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DeviceType DataFlow { get; set; }
        public AudioDeviceState State { get; set; }
        public bool IsDefault { get; set; }
        public MicrophonePermissionStatus PermissionStatus { get; set; } = MicrophonePermissionStatus.NotDetermined;
    }

    public enum DeviceType
    {
        Input,
        Output
    }

    public enum AudioDeviceState
    {
        Active,
        Disabled,
        Unplugged,
        NotPresent
    }

    public enum MicrophonePermissionStatus
    {
        NotDetermined,
        Granted,
        Denied,
        SystemError
    }

    public class AudioDeviceCapabilities
    {
        public int SampleRate { get; set; } = 44100;
        public int Channels { get; set; } = 2;
        public int BitsPerSample { get; set; } = 16;
        public string DeviceFriendlyName { get; set; } = string.Empty;
        public string DeviceDescription { get; set; } = string.Empty;
        public List<WaveFormat> SupportedFormats { get; set; } = new List<WaveFormat>();
    }

    public class AudioDeviceEventArgs : EventArgs
    {
        public AudioDevice Device { get; }

        public AudioDeviceEventArgs(AudioDevice device)
        {
            Device = device;
        }
    }

    public class PermissionEventArgs : EventArgs
    {
        public MicrophonePermissionStatus Status { get; }
        public string Message { get; }
        public string DeviceId { get; }

        public PermissionEventArgs(MicrophonePermissionStatus status, string message, string deviceId = "")
        {
            Status = status;
            Message = message;
            DeviceId = deviceId;
        }
    }

    // Enhanced testing and monitoring classes
    public class AudioDeviceTestResult
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public DateTime TestTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public bool BasicFunctionality { get; set; }
        public List<string> SupportedFormats { get; set; } = new List<string>();
        public float QualityScore { get; set; }
        public int LatencyMs { get; set; }
        public float NoiseFloorDb { get; set; }
    }

    public class DeviceCompatibilityScore
    {
        public string DeviceId { get; set; } = string.Empty;
        public DateTime ScoreTime { get; set; }
        public float SampleRateScore { get; set; }
        public float ChannelScore { get; set; }
        public float BitDepthScore { get; set; }
        public float DeviceTypeScore { get; set; }
        public float OverallScore { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public class DeviceRecommendation
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public float Score { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    public class AudioLevelEventArgs : EventArgs
    {
        public string DeviceId { get; set; } = string.Empty;
        public float Level { get; set; }
        public DateTime Timestamp { get; set; }

        public AudioLevelEventArgs(string deviceId, float level, DateTime timestamp)
        {
            DeviceId = deviceId;
            Level = level;
            Timestamp = timestamp;
        }
    }
}