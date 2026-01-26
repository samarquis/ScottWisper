using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ScottWisper.Configuration
{
    public class AudioSettings
    {
        public string InputDeviceId { get; set; } = "default";
        public string OutputDeviceId { get; set; } = "default";
        public int SampleRate { get; set; } = 16000;
        public int Channels { get; set; } = 1;
        public string FallbackInputDeviceId { get; set; } = "default";
        public string FallbackOutputDeviceId { get; set; } = "default";
        public bool AutoSwitchDevices { get; set; } = true;
        public bool PreferHighQualityDevices { get; set; } = true;
        public List<string> PreferredDevices { get; set; } = new List<string>();
        public Dictionary<string, DeviceSpecificSettings> DeviceSettings { get; set; } = new Dictionary<string, DeviceSpecificSettings>();
        public bool EnableRealTimeMonitoring { get; set; } = true;
        public int DeviceTestDuration { get; set; } = 1000; // ms
    }

    public class DeviceSpecificSettings
    {
        public string Name { get; set; } = string.Empty;
        public int SampleRate { get; set; } = 16000;
        public int Channels { get; set; } = 1;
        public int BufferSize { get; set; } = 1024;
        public bool IsEnabled { get; set; } = true;
        public bool IsCompatible { get; set; } = true;
        public DateTime LastTested { get; set; } = DateTime.MinValue;
        public bool LastTestPassed { get; set; } = false;
        public string Notes { get; set; } = string.Empty;
        public bool RealTimeMonitoringEnabled { get; set; } = false;
        public float QualityScore { get; set; } = 0f;
        public int LatencyMs { get; set; } = 0;
        public float NoiseFloorDb { get; set; } = -120f;
    }

    public class TranscriptionSettings
    {
        public string Provider { get; set; } = "OpenAI";
        public string Model { get; set; } = "whisper-1";
        public string Language { get; set; } = "auto";
        public string ApiKey { get; set; } = string.Empty;
    }

    public class HotkeySettings
    {
        public string ToggleRecording { get; set; } = "Ctrl+Alt+V";
        public string ShowSettings { get; set; } = "Ctrl+Alt+S";
    }

    public class UISettings
    {
        public bool ShowVisualFeedback { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool ShowTranscriptionWindow { get; set; } = true;
    }

    public class DeviceTestingResult
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public bool TestPassed { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime TestTime { get; set; } = DateTime.Now;
        public double SignalStrength { get; set; } = 0;
        public int NoiseLevel { get; set; } = 0;
        public Dictionary<string, object> TestMetrics { get; set; } = new Dictionary<string, object>();
    }

    // Enhanced device testing classes
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

    public class AudioQualityMetrics
    {
        public string DeviceId { get; set; } = string.Empty;
        public DateTime AnalysisTime { get; set; }
        public float AverageLevel { get; set; }
        public float RMSLevel { get; set; }
        public float PeakLevel { get; set; }
        public float PeakToRMSRatio { get; set; }
        public float DynamicRange { get; set; }
        public float SignalQuality { get; set; }
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

    public class AppSettings
    {
        public AudioSettings Audio { get; set; } = new();
        public TranscriptionSettings Transcription { get; set; } = new();
        public HotkeySettings Hotkeys { get; set; } = new();
        public UISettings UI { get; set; } = new();
        public List<DeviceTestingResult> DeviceTestHistory { get; set; } = new List<DeviceTestingResult>();
        public DateTime LastDeviceRefresh { get; set; } = DateTime.MinValue;
        public int MaxTestHistory { get; set; } = 50;
        
        // Enhanced device testing fields
        public List<AudioDeviceTestResult> AudioDeviceTestHistory { get; set; } = new List<AudioDeviceTestResult>();
        public List<AudioQualityMetrics> AudioQualityHistory { get; set; } = new List<AudioQualityMetrics>();
        public Dictionary<string, DeviceCompatibilityScore> DeviceCompatibilityScores { get; set; } = new Dictionary<string, DeviceCompatibilityScore>();
        public List<DeviceRecommendation> DeviceRecommendations { get; set; } = new List<DeviceRecommendation>();
        public int MaxQualityHistory { get; set; } = 100;
        public int MaxCompatibilityHistory { get; set; } = 20;
    }
}