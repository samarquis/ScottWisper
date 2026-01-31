using System;
using System.Collections.Generic;

namespace ScottWisper.Services
{
    /// <summary>
    /// Audio device information for device selection and management
    /// </summary>
    public class AudioDevice
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsEnabled { get; set; }
        public DeviceType DeviceType { get; set; }
        public string DriverVersion { get; set; } = string.Empty;
        public string DeviceState { get; set; } = string.Empty;
        
        // Missing properties
        public AudioDeviceState State { get; set; } = AudioDeviceState.Active;
        public MicrophonePermissionStatus PermissionStatus { get; set; } = MicrophonePermissionStatus.Unknown;
        public AudioDataFlow DataFlow { get; set; } = AudioDataFlow.Capture;
    }

    /// <summary>
    /// Audio device capabilities and performance characteristics
    /// </summary>
    public class AudioDeviceCapabilities
    {
        public string DeviceId { get; set; } = string.Empty;
        public int MinimumSampleRate { get; set; } = 8000;
        public int MaximumSampleRate { get; set; } = 192000;
        public int MinimumChannels { get; set; } = 1;
        public int MaximumChannels { get; set; } = 8;
        public bool SupportsLowLatency { get; set; }
        public bool SupportsExclusiveMode { get; set; }
        public string SupportedFormats { get; set; } = string.Empty;
        public double ExpectedLatencyMs { get; set; }
        public bool IsRecommended { get; set; }
        public int QualityScore { get; set; }
        public string[] SupportedFeatures { get; set; } = Array.Empty<string>();
        
        // Missing properties
        public int SampleRate { get; set; } = 16000;
        public int Channels { get; set; } = 1;
        public int BitsPerSample { get; set; } = 16;
        public string DeviceFriendlyName { get; set; } = string.Empty;
        public string DeviceDescription { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event arguments for audio device events
    /// </summary>
    public class AudioDeviceEventArgs : EventArgs
    {
        public AudioDevice Device { get; set; } = new AudioDevice();
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event arguments for audio level monitoring
    /// </summary>
    public class AudioLevelEventArgs : EventArgs
    {
        public string DeviceId { get; set; } = string.Empty;
        public float LeftLevel { get; set; }
        public float RightLevel { get; set; }
        public float PeakLevel { get; set; }
        public float Level { get; set; } // Missing property
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Constructor with 3 arguments
        public AudioLevelEventArgs(string deviceId, float level, DateTime timestamp)
        {
            DeviceId = deviceId;
            Level = level;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Event arguments for device recovery operations
    /// </summary>
    public class DeviceRecoveryEventArgs : EventArgs
    {
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string RecoveryAction { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Success => Status == "Success";
    }

    /// <summary>
    /// Result of audio device testing operations
    /// </summary>
    public class AudioDeviceTestResult
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        private bool _success = false;
        public bool Success 
        { 
            get => _success;
            set => _success = value;
        }
        
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
        public DateTime TestStarted { get; set; } = DateTime.UtcNow;
        public DateTime TestCompleted { get; set; } = DateTime.UtcNow;
        
        // Missing properties
        public TimeSpan TestTime { get; set; } = TimeSpan.Zero;
        public string ErrorMessage { get; set; } = string.Empty;
        public string SupportedFormats { get; set; } = string.Empty;
        public bool BasicFunctionality { get; set; }
        public double NoiseFloorDb { get; set; }
        public bool TestPassed { get; set; }
        
        public TimeSpan TestDuration => TestCompleted - TestStarted;
        public double LatencyMs { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public float SignalToNoiseRatio { get; set; }
        public bool SupportsLowLatency { get; set; }
        public int QualityScore { get; set; }
    }

    /// <summary>
    /// Device type enumeration
    /// </summary>
    public enum DeviceType
    {
        Input,
        Output,
        InputOutput
    }

    /// <summary>
    /// Audio device state enumeration
    /// </summary>
    public enum AudioDeviceState
    {
        Active,
        Disabled,
        Unplugged,
        NotPresent,
        PresentWithNoDriver
    }

    /// <summary>
    /// Audio data flow enumeration
    /// </summary>
    public enum AudioDataFlow
    {
        Render,
        Capture,
        All
    }

    // Use MicrophonePermissionStatus from root namespace to avoid conflicts
}