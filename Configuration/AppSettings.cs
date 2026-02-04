using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace WhisperKey.Configuration
{
    public class AudioSettings
    {
        public string InputDeviceId { get; set; } = "default";
        public string SelectedInputDeviceId { get; set; } = "default";
        public string OutputDeviceId { get; set; } = "default";
        public string SelectedOutputDeviceId { get; set; } = "default";
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
        // Cloud provider settings (deprecated - kept for backwards compatibility)
        public string Provider { get; set; } = "OpenAI";
        public string Model { get; set; } = "whisper-1";
        public string Language { get; set; } = "auto";
        public string ApiKey { get; set; } = string.Empty;
        public bool EnableAutoPunctuation { get; set; } = true;
        public bool EnableRealTimeTranscription { get; set; } = false;
        public float ConfidenceThreshold { get; set; } = 0.8f;
        public int MaxRecordingDuration { get; set; } = 60; // seconds
        public string ApiEndpoint { get; set; } = string.Empty;
        public int RequestTimeout { get; set; } = 30; // seconds
        public bool UseProxy { get; set; } = false;
        public int CurrentUsage { get; set; } = 0;
        public int MonthlyLimit { get; set; } = 1000;
        public int FreeTierLimit { get; set; } = 100;

        // Local transcription settings (now default - works offline)
        public TranscriptionMode Mode { get; set; } = TranscriptionMode.Local;
        public string LocalModelPath { get; set; } = string.Empty;
        public bool AutoFallbackToCloud { get; set; } = false; // Disabled by default for offline-only operation
        
        // Local provider selection (Whisper, Vosk, etc.)
        public LocalProviderType LocalProvider { get; set; } = LocalProviderType.Whisper;
        
        // Rate limiting settings
        public bool EnableRateLimiting { get; set; } = true;
        public int MaxRequestsPerMinute { get; set; } = 60; // Default: 60 requests per minute
        public bool ApplyRateLimitToLocal { get; set; } = true; // Apply to both local and cloud
    }

    public enum TranscriptionMode
    {
        Cloud,
        Local
    }
    
    public enum LocalProviderType
    {
        Whisper,
        Vosk
    }

    public class HotkeySettings
    {
        public string ToggleRecording { get; set; } = "Ctrl+Alt+V";
        public string ShowSettings { get; set; } = "Ctrl+Alt+S";
        public bool EnableRecording { get; set; } = true;
        public string EmergencyStop { get; set; } = "Ctrl+Alt+Shift+X";
        public Dictionary<string, HotkeyDefinition> CustomHotkeys { get; set; } = new Dictionary<string, HotkeyDefinition>();
        public string CurrentProfile { get; set; } = "Default";
        public Dictionary<string, HotkeyProfile> Profiles { get; set; } = new Dictionary<string, HotkeyProfile>();
        public bool ShowConflictWarnings { get; set; } = true;
        public bool EnableAccessibilityOptions { get; set; } = false;
        public bool EnableKeyboardLayoutAwareness { get; set; } = true;
        public int ConflictCheckInterval { get; set; } = 5000; // ms
        public string BackupProfilePath { get; set; } = string.Empty;
    }

    public class HotkeyDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Combination { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public bool IsEmergency { get; set; } = false;
        public string Description { get; set; } = string.Empty;
        public string TargetApplication { get; set; } = string.Empty; // Application-specific
        public string Application 
        { 
            get => TargetApplication; 
            set => TargetApplication = value; 
        }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUsed { get; set; } = DateTime.MinValue;
        public int UsageCount { get; set; } = 0;
    }

    public class HotkeyProfile
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, HotkeyDefinition> Hotkeys { get; set; } = new Dictionary<string, HotkeyDefinition>();
        public bool IsDefault { get; set; } = false;
        public bool IsReadonly { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
        public string Version { get; set; } = "1.0";
        public List<string> Tags { get; set; } = new List<string>();
        public ProfileMetadata Metadata { get; set; } = new ProfileMetadata();
    }

    public class ProfileMetadata
    {
        public string Author { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public List<string> CompatibleApplications { get; set; } = new List<string>();
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
    }

    public class HotkeyConflict
    {
        public string ConflictingHotkey { get; set; } = string.Empty;
        public string Hotkey 
        { 
            get => ConflictingHotkey; 
            set => ConflictingHotkey = value; 
        }
        public string ConflictingApplication { get; set; } = string.Empty;
        public string Application 
        { 
            get => ConflictingApplication; 
            set => ConflictingApplication = value; 
        }
        public string ConflictType { get; set; } = string.Empty; // "system", "application", "profile"
        public string Status 
        { 
            get => ConflictType; 
            set => ConflictType = value; 
        }
        public string Resolution { get; set; } = string.Empty;
        public bool IsResolvable { get; set; } = true;
        public DateTime DetectedAt { get; set; } = DateTime.Now;
        public string SuggestedHotkey { get; set; } = string.Empty;
        public string ConflictingHotkeyName { get; set; } = string.Empty;
    }

    public class HotkeyValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string WarningMessage { get; set; } = string.Empty;
        public List<HotkeyConflict> Conflicts { get; set; } = new List<HotkeyConflict>();
        public List<string> Suggestions { get; set; } = new List<string>();
    }

    public class UISettings
    {
        public bool ShowVisualFeedback { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool ShowTranscriptionWindow { get; set; } = true;
        public double WindowOpacity { get; set; } = 1.0;
        public float FeedbackVolume { get; set; } = 0.5f;
        public bool StartMinimized { get; set; } = false;
        public bool CheckForUpdates { get; set; } = true;
        public int StartupDelay { get; set; } = 0;
        public string Theme { get; set; } = "Dark";
        public bool ShowChangeNotifications { get; set; } = true;
        
        // Text review settings
        public bool EnableTextReview { get; set; } = true;
        public bool AutoInsertAfterReview { get; set; } = false;
        public int ReviewWindowTimeoutSeconds { get; set; } = 30;
    }

    public class TextInjectionSettings
    {
        public bool Enabled { get; set; } = true;
        public bool UseClipboardFallback { get; set; } = true;
        public int RetryCount { get; set; } = 3;
        public int DelayBetweenRetriesMs { get; set; } = 100;
        public int DelayBetweenCharsMs { get; set; } = 5;
        public bool RespectExistingText { get; set; } = true;
        public string PreferredMethod { get; set; } = "SendInput"; // SendInput, ClipboardFallback, Auto
        public bool EnableDebugMode { get; set; } = false;
        public bool EnablePerformanceMonitoring { get; set; } = true;
        public int InjectionLatencyThresholdMs { get; set; } = 50; // Alert if injection takes longer than this
        public bool EnableCompatibilityMapping { get; set; } = true;
        public Dictionary<string, bool> ApplicationSpecificSettings { get; set; } = new Dictionary<string, bool>
        {
            ["browsers_use_unicode_fix"] = true,
            ["development_delay_syntax_chars"] = true,
            ["office_prefer_clipboard"] = true,
            ["communication_enhanced_emoji"] = true,
            ["text_editors_preserve_formatting"] = true
        };
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
        public TextInjectionSettings TextInjection { get; set; } = new();
        public List<DeviceTestingResult> DeviceTestHistory { get; set; } = new List<DeviceTestingResult>();
        public DateTime LastDeviceRefresh { get; set; } = DateTime.MinValue;
        public int MaxTestHistory { get; set; } = 50;
        
        // First-time setup wizard tracking
        public bool FirstTimeSetupCompleted { get; set; } = false;
        public bool ShowSetupWizardOnStartup { get; set; } = true;
        public DateTime FirstRunDate { get; set; } = DateTime.MinValue;
        
        // Enhanced device testing fields
        public List<AudioDeviceTestResult> AudioDeviceTestHistory { get; set; } = new List<AudioDeviceTestResult>();
        public List<AudioQualityMetrics> AudioQualityHistory { get; set; } = new List<AudioQualityMetrics>();
        public Dictionary<string, DeviceCompatibilityScore> DeviceCompatibilityScores { get; set; } = new Dictionary<string, DeviceCompatibilityScore>();
        public List<DeviceRecommendation> DeviceRecommendations { get; set; } = new List<DeviceRecommendation>();
        public int MaxQualityHistory { get; set; } = 100;
        public int MaxCompatibilityHistory { get; set; } = 20;
    }
}