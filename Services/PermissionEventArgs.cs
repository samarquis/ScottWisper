using System;

namespace ScottWisper.Services
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
    }
}