using System;

namespace WhisperKey.Exceptions
{
    /// <summary>
    /// Domain-specific exception for audio capture-related errors
    /// </summary>
    public class AudioCaptureException : WhisperKeyException
    {
        public string? DeviceId { get; }
        public string? AudioFormat { get; }

        public AudioCaptureException(string message, string errorCode = "AUDIO_ERROR") 
            : base(message, errorCode) { }

        public AudioCaptureException(string message, string deviceId, string errorCode, Exception? innerException = null)
            : base(message, errorCode, innerException)
        {
            DeviceId = deviceId;
        }
    }

    /// <summary>
    /// Exception thrown when no audio devices are available
    /// </summary>
    public class NoAudioDeviceException : AudioCaptureException
    {
        public NoAudioDeviceException(string message, Exception? innerException = null)
            : base(message, "NO_DEVICE", "NO_DEVICE_ERROR", innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when audio device permissions are denied
    /// </summary>
    public class AudioDevicePermissionException : AudioCaptureException
    {
        public string? RequiredPermission { get; }

        public AudioDevicePermissionException(string deviceId, string requiredPermission, string message, Exception? innerException = null)
            : base(message, deviceId, "PERMISSION_DENIED", innerException)
        {
            RequiredPermission = requiredPermission;
        }
    }

    /// <summary>
    /// Exception thrown when audio device is already in use
    /// </summary>
    public class AudioDeviceBusyException : AudioCaptureException
    {
        public AudioDeviceBusyException(string deviceId, string message, Exception? innerException = null)
            : base(message, deviceId, "DEVICE_BUSY", innerException)
        {
        }
    }
}