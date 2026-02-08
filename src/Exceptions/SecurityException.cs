using System;

namespace WhisperKey.Exceptions
{
    /// <summary>
    /// Exception thrown when a security or compliance violation is detected
    /// </summary>
    public class WhisperKeySecurityException : WhisperKeyException
    {
        public string? ViolationType { get; set; }

        public WhisperKeySecurityException(string message, string? violationType = null, Exception? innerException = null)
            : base(message, "SECURITY_VIOLATION", innerException)
        {
            ViolationType = violationType;
        }
    }

    /// <summary>
    /// Exception thrown when access to a restricted feature is denied
    /// </summary>
    public class AccessDeniedException : WhisperKeySecurityException
    {
        public AccessDeniedException(string resource)
            : base($"Access denied to resource: {resource}", "UNAUTHORIZED_ACCESS")
        {
        }
    }
}
