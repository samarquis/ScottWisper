using System;

namespace WhisperKey.Exceptions
{
    /// <summary>
    /// Domain-specific exception for hotkey registration-related errors
    /// </summary>
    public class HotkeyRegistrationException : Exception
    {
        public string? HotkeyCombination { get; }
        public string? ErrorCode { get; }

        public HotkeyRegistrationException() : base() { }

        public HotkeyRegistrationException(string message) : base(message) { }

        public HotkeyRegistrationException(string message, Exception innerException) : base(message, innerException) { }

        public HotkeyRegistrationException(string message, string hotkeyCombination, Exception? innerException = null)
            : base(message, innerException)
        {
            HotkeyCombination = hotkeyCombination;
        }

        public HotkeyRegistrationException(string message, string hotkeyCombination, string errorCode, Exception? innerException = null)
            : base(message, innerException)
        {
            HotkeyCombination = hotkeyCombination;
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Exception thrown when hotkey is already registered by another application
    /// </summary>
    public class HotkeyAlreadyRegisteredException : HotkeyRegistrationException
    {
        public HotkeyAlreadyRegisteredException(string hotkeyCombination, string message = "Hotkey is already registered by another application")
            : base(message, hotkeyCombination, "ALREADY_REGISTERED")
        {
        }
    }

    /// <summary>
    /// Exception thrown when hotkey combination is invalid
    /// </summary>
    public class InvalidHotkeyException : HotkeyRegistrationException
    {
        public InvalidHotkeyException(string hotkeyCombination, string message = "Invalid hotkey combination")
            : base(message, hotkeyCombination, "INVALID_COMBINATION")
        {
        }
    }

    /// <summary>
    /// Exception thrown when hotkey registration fails due to insufficient permissions
    /// </summary>
    public class HotkeyPermissionException : HotkeyRegistrationException
    {
        public HotkeyPermissionException(string hotkeyCombination, string message = "Insufficient permissions to register hotkey")
            : base(message, hotkeyCombination, "PERMISSION_DENIED")
        {
        }
    }

    /// <summary>
    /// Exception thrown when hotkey registration fails due to system limitations
    /// </summary>
    public class HotkeySystemException : HotkeyRegistrationException
    {
        public HotkeySystemException(string hotkeyCombination, string message, Exception? innerException = null)
            : base(message, hotkeyCombination, "SYSTEM_ERROR", innerException)
        {
        }
    }
}