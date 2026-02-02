using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace WhisperKey.Services
{
    /// <summary>
    /// Represents different categories of errors for user-facing messages
    /// </summary>
    public enum ErrorCategory
    {
        AudioDevice,
        Transcription,
        Network,
        Configuration,
        Permission,
        Injection,
        Unknown,
        Fatal
    }

    /// <summary>
    /// Severity level for errors
    /// </summary>
    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// User-friendly error information
    /// </summary>
    public class UserErrorMessage
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public ErrorCategory Category { get; set; } = ErrorCategory.Unknown;
        public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;
        public string? TechnicalDetails { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Interface for user-friendly error handling
    /// </summary>
    public interface IUserErrorService
    {
        /// <summary>
        /// Shows a user-friendly error message based on an exception
        /// </summary>
        void ShowError(Exception exception, ErrorCategory category, string? context = null);
        
        /// <summary>
        /// Shows a user-friendly error message asynchronously
        /// </summary>
        Task ShowErrorAsync(Exception exception, ErrorCategory category, string? context = null);
        
        /// <summary>
        /// Gets a user-friendly error message without showing it
        /// </summary>
        UserErrorMessage GetErrorMessage(Exception exception, ErrorCategory category, string? context = null);
        
        /// <summary>
        /// Logs technical error details (separate from user display)
        /// </summary>
        void LogTechnicalError(Exception exception, ErrorCategory category, string? context = null);
        
        /// <summary>
        /// Shows a user-friendly message for a specific error category
        /// </summary>
        void ShowCategoryError(ErrorCategory category, string title, string message, string action);
    }

    /// <summary>
    /// Service that provides user-friendly error messages while logging technical details separately
    /// </summary>
    public class UserErrorService : IUserErrorService
    {
        private readonly ILogger<UserErrorService>? _logger;
        private readonly Dictionary<ErrorCategory, ErrorMessageTemplate> _errorTemplates;

        public UserErrorService(ILogger<UserErrorService>? logger = null)
        {
            _logger = logger;
            _errorTemplates = InitializeErrorTemplates();
        }

        /// <summary>
        /// Shows error to user with friendly message, logs technical details separately
        /// </summary>
        public void ShowError(Exception exception, ErrorCategory category, string? context = null)
        {
            // Always log technical details first
            LogTechnicalError(exception, category, context);

            // Get user-friendly message
            var userMessage = GetErrorMessage(exception, category, context);

            // Show to user on UI thread
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var icon = userMessage.Severity switch
                {
                    ErrorSeverity.Critical => MessageBoxImage.Error,
                    ErrorSeverity.Error => MessageBoxImage.Error,
                    ErrorSeverity.Warning => MessageBoxImage.Warning,
                    _ => MessageBoxImage.Information
                };

                var fullMessage = userMessage.Message;
                if (!string.IsNullOrEmpty(userMessage.Action))
                {
                    fullMessage += $"\n\nWhat you can do:\n{userMessage.Action}";
                }

                MessageBox.Show(fullMessage, userMessage.Title, MessageBoxButton.OK, icon);
            });
        }

        /// <summary>
        /// Shows error asynchronously (non-blocking)
        /// </summary>
        public async Task ShowErrorAsync(Exception exception, ErrorCategory category, string? context = null)
        {
            await Task.Run(() => ShowError(exception, category, context));
        }

        /// <summary>
        /// Gets user-friendly error message for an exception
        /// </summary>
        public UserErrorMessage GetErrorMessage(Exception exception, ErrorCategory category, string? context = null)
        {
            var template = _errorTemplates.TryGetValue(category, out var t) ? t : _errorTemplates[ErrorCategory.Unknown];
            
            // Get specific message based on exception type
            var (userMessage, action) = GetSpecificErrorMessage(exception, category);

            return new UserErrorMessage
            {
                Title = template.Title,
                Message = userMessage,
                Action = action,
                Category = category,
                Severity = template.Severity,
                TechnicalDetails = FormatTechnicalDetails(exception, context),
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Logs technical error details to log file (not shown to user)
        /// </summary>
        public void LogTechnicalError(Exception exception, ErrorCategory category, string? context = null)
        {
            var technicalInfo = FormatTechnicalDetails(exception, context);
            
            _logger?.LogError(exception, 
                "Technical Error [{Category}] {Context}: {Message}\n{TechnicalDetails}", 
                category, 
                context ?? "No context", 
                exception.Message,
                technicalInfo);

            // Also log to file for debugging
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WhisperKey",
                    "logs",
                    $"technical_errors_{DateTime.Now:yyyyMM}.log");

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);

                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{category}] {context}\n" +
                              $"Exception: {exception.GetType().Name}: {exception.Message}\n" +
                              $"Stack Trace:\n{exception.StackTrace}\n" +
                              $"---\n\n";

                System.IO.File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // If we can't log, we can't do much about it
                _logger?.LogError("Failed to write technical error log");
            }
        }

        /// <summary>
        /// Shows a category-specific error with custom message
        /// </summary>
        public void ShowCategoryError(ErrorCategory category, string title, string message, string action)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var fullMessage = message;
                if (!string.IsNullOrEmpty(action))
                {
                    fullMessage += $"\n\nWhat you can do:\n{action}";
                }

                MessageBox.Show(fullMessage, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        /// <summary>
        /// Gets specific error message based on exception type and category
        /// </summary>
        private (string message, string action) GetSpecificErrorMessage(Exception exception, ErrorCategory category)
        {
            return exception switch
            {
                // Audio device errors
                System.IO.IOException ioEx when category == ErrorCategory.AudioDevice => (
                    "Could not access your microphone. It may be in use by another application.",
                    "Close other applications that might be using the microphone, then try again. If the problem persists, check your Windows microphone permissions in Settings > Privacy > Microphone."),
                
                UnauthorizedAccessException authEx when category == ErrorCategory.AudioDevice => (
                    "WhisperKey doesn't have permission to access your microphone.",
                    "Go to Windows Settings > Privacy > Microphone and make sure 'Allow apps to access your microphone' is turned on. Also ensure WhisperKey is allowed in the app list."),
                
                InvalidOperationException invEx when category == ErrorCategory.AudioDevice => (
                    "Your audio device is not responding or has been disconnected.",
                    "Check that your microphone is plugged in properly. You can also try selecting a different device in WhisperKey settings."),

                // Transcription errors
                System.Net.Http.HttpRequestException httpEx when category == ErrorCategory.Transcription => (
                    "Could not connect to the transcription service.",
                    "Check your internet connection. If using cloud transcription, verify your API key in settings. You can also switch to local transcription mode which works offline."),
                
                System.TimeoutException timeoutEx => (
                    "The operation took too long to complete.",
                    "Try again. If the problem persists, check your internet connection or switch to local transcription mode for faster processing."),

                // Network errors
                System.Net.Http.HttpRequestException httpEx when category == ErrorCategory.Network => (
                    "Network connection problem detected.",
                    "Check that you're connected to the internet. If you're on a corporate network, you may need to configure proxy settings in WhisperKey."),

                // Configuration errors
                System.Configuration.ConfigurationErrorsException configEx => (
                    "WhisperKey's settings file appears to be damaged.",
                    "Reset your settings to default by deleting or renaming the settings file. The app will create a new one on restart. Your settings are located in %APPDATA%\\WhisperKey\\settings.json"),

                // Permission errors
                UnauthorizedAccessException authEx when category == ErrorCategory.Permission => (
                    "WhisperKey doesn't have the necessary permissions to perform this action.",
                    "Try running WhisperKey as administrator. If that doesn't work, check your antivirus software isn't blocking the application."),

                // Injection errors
                InvalidOperationException invEx when category == ErrorCategory.Injection => (
                    "Could not insert text into the target application.",
                    "Make sure the application window you want to type into is active (click on it first). Some applications like games or secure password fields may not support text injection."),

                // Default/fallback
                _ => GetDefaultErrorMessage(category)
            };
        }

        /// <summary>
        /// Gets default error message for a category
        /// </summary>
        private (string message, string action) GetDefaultErrorMessage(ErrorCategory category)
        {
            return category switch
            {
                ErrorCategory.AudioDevice => (
                    "An audio device error occurred.",
                    "Check your microphone connection and permissions. Try selecting a different audio device in settings."),
                
                ErrorCategory.Transcription => (
                    "Transcription failed.",
                    "Try again or switch between cloud and local transcription modes in settings."),
                
                ErrorCategory.Network => (
                    "A network error occurred.",
                    "Check your internet connection and try again."),
                
                ErrorCategory.Configuration => (
                    "A configuration error occurred.",
                    "Check your settings or reset to defaults."),
                
                ErrorCategory.Permission => (
                    "A permission error occurred.",
                    "Check that WhisperKey has the necessary permissions and try running as administrator if needed."),
                
                ErrorCategory.Injection => (
                    "Could not insert text.",
                    "Ensure the target window is active and try again."),
                
                ErrorCategory.Fatal => (
                    "A serious error occurred that prevents WhisperKey from continuing.",
                    "Please restart the application. If the problem persists, reinstall WhisperKey or contact support."),
                
                _ => (
                    "An unexpected error occurred.",
                    "Try the operation again. If the problem persists, restart WhisperKey.")
            };
        }

        /// <summary>
        /// Formats technical error details for logging
        /// </summary>
        private string FormatTechnicalDetails(Exception exception, string? context)
        {
            var details = new System.Text.StringBuilder();
            details.AppendLine($"Context: {context ?? "N/A"}");
            details.AppendLine($"Exception Type: {exception.GetType().FullName}");
            details.AppendLine($"Message: {exception.Message}");
            details.AppendLine($"Source: {exception.Source}");
            details.AppendLine($"Stack Trace:\n{exception.StackTrace}");

            if (exception.InnerException != null)
            {
                details.AppendLine($"\nInner Exception: {exception.InnerException.GetType().Name}");
                details.AppendLine($"Inner Message: {exception.InnerException.Message}");
            }

            return details.ToString();
        }

        /// <summary>
        /// Initializes error message templates for each category
        /// </summary>
        private Dictionary<ErrorCategory, ErrorMessageTemplate> InitializeErrorTemplates()
        {
            return new Dictionary<ErrorCategory, ErrorMessageTemplate>
            {
                [ErrorCategory.AudioDevice] = new ErrorMessageTemplate("Microphone Error", ErrorSeverity.Error),
                [ErrorCategory.Transcription] = new ErrorMessageTemplate("Transcription Error", ErrorSeverity.Error),
                [ErrorCategory.Network] = new ErrorMessageTemplate("Network Error", ErrorSeverity.Warning),
                [ErrorCategory.Configuration] = new ErrorMessageTemplate("Settings Error", ErrorSeverity.Error),
                [ErrorCategory.Permission] = new ErrorMessageTemplate("Permission Error", ErrorSeverity.Error),
                [ErrorCategory.Injection] = new ErrorMessageTemplate("Text Insertion Error", ErrorSeverity.Warning),
                [ErrorCategory.Fatal] = new ErrorMessageTemplate("Critical Error", ErrorSeverity.Critical),
                [ErrorCategory.Unknown] = new ErrorMessageTemplate("Error", ErrorSeverity.Error)
            };
        }

        /// <summary>
        /// Template for error messages
        /// </summary>
        private class ErrorMessageTemplate
        {
            public string Title { get; }
            public ErrorSeverity Severity { get; }

            public ErrorMessageTemplate(string title, ErrorSeverity severity)
            {
                Title = title;
                Severity = severity;
            }
        }
    }
}
