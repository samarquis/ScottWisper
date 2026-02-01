using System;

namespace WhisperKey
{
    /// <summary>
    /// Represents application compatibility information for text injection
    /// </summary>
    public class AppApplicationCompatibility
    {
        public string Name { get; set; } = string.Empty;
        public bool Supported { get; set; }
        public string InjectionMethod { get; set; } = string.Empty;
        public bool TestRequired { get; set; }
    }

    /// <summary>
    /// Represents microphone permission status
    /// </summary>
    public enum MicrophonePermissionStatus
    {
        Unknown,
        Granted,
        Denied,
        NotRequested,
        SystemError
    }
}
