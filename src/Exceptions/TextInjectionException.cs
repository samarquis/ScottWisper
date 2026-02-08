using System;

namespace WhisperKey.Exceptions
{
    /// <summary>
    /// Exception thrown when text injection fails
    /// </summary>
    public class TextInjectionException : WhisperKeyException
    {
        public string? TargetApplication { get; set; }

        public TextInjectionException(string message, string? targetApp = null, Exception? innerException = null)
            : base(message, "INJECTION_ERROR", innerException)
        {
            TargetApplication = targetApp;
        }
    }

    /// <summary>
    /// Exception thrown when the target window for injection cannot be found
    /// </summary>
    public class WindowNotFoundException : TextInjectionException
    {
        public WindowNotFoundException(string windowTitle)
            : base($"Could not find target window: {windowTitle}", windowTitle)
        {
        }
    }
}
