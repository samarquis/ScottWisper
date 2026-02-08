using System;

namespace WhisperKey.Exceptions
{
    /// <summary>
    /// Domain-specific exception for transcription-related errors
    /// </summary>
    public class TranscriptionException : WhisperKeyException
    {
        public string? AudioFormat { get; }

        public TranscriptionException(string message, string errorCode = "TRANSCRIPTION_ERROR") 
            : base(message, errorCode) { }

        public TranscriptionException(string message, string errorCode, Exception innerException) 
            : base(message, errorCode, innerException) { }

        public TranscriptionException(string message, string errorCode, string audioFormat, Exception? innerException = null)
            : base(message, errorCode, innerException ?? new Exception("None"))
        {
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
            : base(message, "MODEL_ERROR", innerException ?? new Exception("None"))
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
            : base(message, "NETWORK_ERROR", innerException ?? new Exception("None"))
        {
        }
    }
}