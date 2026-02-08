using System;

namespace WhisperKey.Exceptions
{
    /// <summary>
    /// Base class for all domain-specific exceptions in WhisperKey
    /// </summary>
    public abstract class WhisperKeyException : Exception
    {
        public string ErrorCode { get; }

        protected WhisperKeyException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        protected WhisperKeyException(string message, string errorCode, Exception? innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}