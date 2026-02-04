using System;

namespace WhisperKey.Exceptions
{
    /// <summary>
    /// Domain-specific exception for audio capture-related errors
    /// </summary>
    public class AudioCaptureException : Exception
    {
        public string? DeviceId { get; }
        public string? AudioFormat { get; }

        public AudioCaptureException() : base() { }

        public AudioCaptureException(string message) : base(message) { }

        public AudioCaptureException(string message, Exception innerException) : base(message, innerException) { }

        public AudioCaptureException(string message, string deviceId, Exception? innerException = null)
            : base(message, innerException)
        {
            DeviceId = deviceId;
        }

        public AudioCaptureException(string message, string deviceId, string audioFormat, Exception? innerException = null)
            : base(message, innerException)
        {
            DeviceId = deviceId;
            AudioFormat = audioFormat;
        }
    }

    /// <summary>
    /// Exception thrown when no audio devices are available
    /// </summary>
    public class NoAudioDeviceException : AudioCaptureException
    {
        public NoAudioDeviceException(string message, Exception? innerException = null)
            : base(message, innerException)
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
            : base(message, deviceId, innerException)
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
            : base(message, deviceId, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when audio device is not found
    /// </summary>
    public class AudioDeviceNotFoundException : AudioCaptureException
    {
        public AudioDeviceNotFoundException(string deviceId, string message, Exception? innerException = null)
            : base(message, deviceId, innerException)
        {
        }
    }
}