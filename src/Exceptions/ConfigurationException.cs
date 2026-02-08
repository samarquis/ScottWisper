using System;

namespace WhisperKey.Exceptions
{
    /// <summary>
    /// Exception thrown when a configuration error or drift is detected
    /// </summary>
    public class ConfigurationException : WhisperKeyException
    {
        public ConfigurationException(string message, string errorCode = "CONFIG_ERROR", Exception? innerException = null)
            : base(message, errorCode, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when configuration drift is detected between environment and baseline
    /// </summary>
    public class ConfigurationDriftException : ConfigurationException
    {
        public int DriftCount { get; }

        public ConfigurationDriftException(int driftCount, string message)
            : base(message, "CONFIG_DRIFT")
        {
            DriftCount = driftCount;
        }
    }
}
