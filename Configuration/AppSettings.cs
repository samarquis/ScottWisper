using System.ComponentModel.DataAnnotations;

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

    public class AppSettings
    {
        public AudioSettings Audio { get; set; } = new();
        public TranscriptionSettings Transcription { get; set; } = new();
        public HotkeySettings Hotkeys { get; set; } = new();
        public UISettings UI { get; set; } = new();
        public List<DeviceTestingResult> DeviceTestHistory { get; set; } = new List<DeviceTestingResult>();
        public DateTime LastDeviceRefresh { get; set; } = DateTime.MinValue;
        public int MaxTestHistory { get; set; } = 50;
    }
}