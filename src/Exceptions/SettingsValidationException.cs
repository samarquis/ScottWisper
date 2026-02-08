using System;

namespace WhisperKey.Exceptions
{
    /// <summary>
    /// Exception thrown when settings validation fails.
    /// </summary>
    public class SettingsValidationException : Exception
    {
        public SettingsValidationException() : base() { }
        public SettingsValidationException(string message) : base(message) { }
        public SettingsValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
