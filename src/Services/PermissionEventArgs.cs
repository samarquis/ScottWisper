using System;

namespace WhisperKey.Services
{
    /// <summary>
    /// Event arguments for permission-related events
    /// </summary>
    public class PermissionEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public string? DeviceId { get; set; }
        public string PermissionType { get; set; } = "Microphone";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool RequiresUserAction { get; set; }
        public string? GuidanceAction { get; set; }
        public MicrophonePermissionStatus Status { get; set; } = MicrophonePermissionStatus.Unknown;

        // Default constructor
        public PermissionEventArgs() { }

        // 2-argument constructor
        public PermissionEventArgs(string message, Exception? exception = null)
        {
            Message = message;
            Exception = exception;
        }

        // 3-argument constructor
        public PermissionEventArgs(string message, string? deviceId, Exception? exception = null)
        {
            Message = message;
            DeviceId = deviceId;
            Exception = exception;
        }

        // Constructor for permission status
        public PermissionEventArgs(MicrophonePermissionStatus status, string message, string? deviceId = null, Exception? exception = null)
        {
            Status = status;
            Message = message;
            DeviceId = deviceId;
            Exception = exception;
            RequiresUserAction = status == MicrophonePermissionStatus.Denied;
        }
    }
}