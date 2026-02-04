using System;

namespace WhisperKey.Exceptions
{
    /// <summary>
    /// Domain-specific exception for transcription-related errors
    /// </summary>
    public class TranscriptionException : Exception
    {
        public string? ErrorCode { get; }
        public string? AudioFormat { get; }

        public TranscriptionException() : base() { }

        public TranscriptionException(string message) : base(message) { }

        public TranscriptionException(string message, Exception innerException) : base(message, innerException) { }

        public TranscriptionException(string message, string errorCode, Exception? innerException = null) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public TranscriptionException(string message, string errorCode, string audioFormat, Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            AudioFormat = audioFormat;
        }
    }

    /// <summary>
    /// Exception thrown when transcription fails due to model or API issues
    /// </summary>
    public class TranscriptionModelException : TranscriptionException
    {
        public string? ModelName { get; }

        public TranscriptionModelException(string modelName, string message, Exception? innerException = null)
            : base(message, "MODEL_ERROR", innerException)
        {
            ModelName = modelName;
        }
    }

    /// <summary>
    /// Exception thrown when transcription fails due to audio format issues
    /// </summary>
    public class TranscriptionFormatException : TranscriptionException
    {
        public TranscriptionFormatException(string audioFormat, string message, Exception? innerException = null)
            : base(message, "FORMAT_ERROR", audioFormat, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when transcription network operations fail
    /// </summary>
    public class TranscriptionNetworkException : TranscriptionException
    {
        public TranscriptionNetworkException(string message, Exception? innerException = null)
            : base(message, "NETWORK_ERROR", innerException)
        {
        }
    }
}