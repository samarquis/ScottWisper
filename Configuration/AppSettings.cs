using System.ComponentModel.DataAnnotations;

namespace ScottWisper.Configuration
{
    public class AudioSettings
    {
        public string InputDeviceId { get; set; } = "default";
        public string OutputDeviceId { get; set; } = "default";
        public int SampleRate { get; set; } = 16000;
        public int Channels { get; set; } = 1;
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

    public class AppSettings
    {
        public AudioSettings Audio { get; set; } = new();
        public TranscriptionSettings Transcription { get; set; } = new();
        public HotkeySettings Hotkeys { get; set; } = new();
        public UISettings UI { get; set; } = new();
    }
}