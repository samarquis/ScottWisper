using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WhisperKey.Services;
using WhisperKey;

namespace WhisperKey.Bootstrap
{
    /// <summary>
    /// Coordinates and routes events between application components
    /// Replaces async void event handlers with proper async Task patterns
    /// </summary>
    public class EventCoordinator
    {
        private readonly ApplicationBootstrapper _bootstrapper;
        private readonly IBusinessMetricsService? _metricsService;
        private readonly Func<Task> _dictationToggleHandler;
        private readonly Func<Task> _startDictationHandler;
        private readonly Func<Task> _stopDictationHandler;
        private readonly Action _settingsHandler;
        private readonly Action _toggleWindowHandler;
        private readonly Action _exitHandler;
        
        public EventCoordinator(
            ApplicationBootstrapper bootstrapper,
            IBusinessMetricsService? metricsService,
            Func<Task> dictationToggleHandler,
            Func<Task> startDictationHandler,
            Func<Task> stopDictationHandler,
            Action settingsHandler,
            Action toggleWindowHandler,
            Action exitHandler)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _metricsService = metricsService;
            _dictationToggleHandler = dictationToggleHandler ?? throw new ArgumentNullException(nameof(dictationToggleHandler));
            _startDictationHandler = startDictationHandler ?? throw new ArgumentNullException(nameof(startDictationHandler));
            _stopDictationHandler = stopDictationHandler ?? throw new ArgumentNullException(nameof(stopDictationHandler));
            _settingsHandler = settingsHandler ?? throw new ArgumentNullException(nameof(settingsHandler));
            _toggleWindowHandler = toggleWindowHandler ?? throw new ArgumentNullException(nameof(toggleWindowHandler));
            _exitHandler = exitHandler ?? throw new ArgumentNullException(nameof(exitHandler));
        }
        
        /// <summary>
        /// Registers all event handlers
        /// </summary>
        public void RegisterEventHandlers()
        {
            // Hotkey service events
            if (_bootstrapper.HotkeyService != null)
            {
                _bootstrapper.HotkeyService.HotkeyPressed += OnHotkeyPressed;
            }
            
            // System tray service events
            if (_bootstrapper.SystemTrayService != null)
            {
                _bootstrapper.SystemTrayService.StartDictationRequested += OnSystemTrayStartDictation;
                _bootstrapper.SystemTrayService.StopDictationRequested += OnSystemTrayStopDictation;
                _bootstrapper.SystemTrayService.SettingsRequested += OnSystemTraySettings;
                _bootstrapper.SystemTrayService.WindowToggleRequested += OnSystemTrayToggleWindow;
                _bootstrapper.SystemTrayService.ExitRequested += OnSystemTrayExit;
            }
            
            // Core service events
            if (_bootstrapper.WhisperService != null)
            {
                _bootstrapper.WhisperService.TranscriptionError += OnTranscriptionError;
                _bootstrapper.WhisperService.TranscriptionCompleted += OnTranscriptionCompleted;
            }
            
            if (_bootstrapper.CostTrackingService != null)
            {
                _bootstrapper.CostTrackingService.FreeTierWarning += OnFreeTierWarning;
                _bootstrapper.CostTrackingService.FreeTierExceeded += OnFreeTierExceeded;
            }
            
            if (_bootstrapper.AudioCaptureService != null)
            {
                _bootstrapper.AudioCaptureService.AudioDataCaptured += OnAudioDataAvailable;
            }
            
            // Settings change events
            if (_bootstrapper.SettingsService != null)
            {
                _bootstrapper.SettingsService.SettingsChanged += OnSettingsChanged;
            }
        }
        
        /// <summary>
        /// Unregisters all event handlers to prevent memory leaks
        /// </summary>
        public void UnregisterEventHandlers()
        {
            if (_bootstrapper.HotkeyService != null)
            {
                _bootstrapper.HotkeyService.HotkeyPressed -= OnHotkeyPressed;
            }
            
            if (_bootstrapper.SystemTrayService != null)
            {
                _bootstrapper.SystemTrayService.StartDictationRequested -= OnSystemTrayStartDictation;
                _bootstrapper.SystemTrayService.StopDictationRequested -= OnSystemTrayStopDictation;
                _bootstrapper.SystemTrayService.SettingsRequested -= OnSystemTraySettings;
                _bootstrapper.SystemTrayService.WindowToggleRequested -= OnSystemTrayToggleWindow;
                _bootstrapper.SystemTrayService.ExitRequested -= OnSystemTrayExit;
            }
            
            if (_bootstrapper.WhisperService != null)
            {
                _bootstrapper.WhisperService.TranscriptionError -= OnTranscriptionError;
                _bootstrapper.WhisperService.TranscriptionCompleted -= OnTranscriptionCompleted;
            }
            
            if (_bootstrapper.CostTrackingService != null)
            {
                _bootstrapper.CostTrackingService.FreeTierWarning -= OnFreeTierWarning;
                _bootstrapper.CostTrackingService.FreeTierExceeded -= OnFreeTierExceeded;
            }
            
            if (_bootstrapper.AudioCaptureService != null)
            {
                _bootstrapper.AudioCaptureService.AudioDataCaptured -= OnAudioDataAvailable;
            }
            
            if (_bootstrapper.SettingsService != null)
            {
                _bootstrapper.SettingsService.SettingsChanged -= OnSettingsChanged;
            }
        }
        
        #region Hotkey Events
        
        private async void OnHotkeyPressed(object? sender, EventArgs e)
        {
            try
            {
                await _dictationToggleHandler().ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await HandleExceptionAsync("Hotkey handling error", ex).ConfigureAwait(false);
            }
            catch (System.IO.IOException ex)
            {
                await HandleExceptionAsync("Hotkey I/O error", ex).ConfigureAwait(false);
            }
        }
        
        #endregion
        
        #region System Tray Events
        
        private async void OnSystemTrayStartDictation(object? sender, EventArgs e)
        {
            try
            {
                await _startDictationHandler().ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await HandleExceptionAsync("Start dictation error", ex).ConfigureAwait(false);
            }
            catch (System.IO.IOException ex)
            {
                await HandleExceptionAsync("Start dictation I/O error", ex).ConfigureAwait(false);
            }
        }
        
        private async void OnSystemTrayStopDictation(object? sender, EventArgs e)
        {
            try
            {
                await _stopDictationHandler().ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await HandleExceptionAsync("Stop dictation error", ex).ConfigureAwait(false);
            }
            catch (System.IO.IOException ex)
            {
                await HandleExceptionAsync("Stop dictation I/O error", ex).ConfigureAwait(false);
            }
        }
        
        private void OnSystemTraySettings(object? sender, EventArgs e)
        {
            try
            {
                _settingsHandler();
            }
            catch (InvalidOperationException ex)
            {
                HandleException("Settings error", ex);
            }
            catch (System.IO.IOException ex)
            {
                HandleException("Settings I/O error", ex);
            }
        }
        
        private void OnSystemTrayToggleWindow(object? sender, EventArgs e)
        {
            try
            {
                _toggleWindowHandler();
            }
            catch (InvalidOperationException ex)
            {
                HandleException("Toggle window error", ex);
            }
            catch (System.IO.IOException ex)
            {
                HandleException("Toggle window I/O error", ex);
            }
        }
        
        private void OnSystemTrayExit(object? sender, EventArgs e)
        {
            try
            {
                _exitHandler();
            }
            catch (InvalidOperationException ex)
            {
                HandleException("Exit error", ex);
            }
            catch (System.IO.IOException ex)
            {
                HandleException("Exit I/O error", ex);
            }
        }
        
        #endregion
        
        #region Core Service Events
        
        private async void OnTranscriptionError(object? sender, Exception ex)
        {
            try
            {
                if (_metricsService != null)
                {
                    await _metricsService.RecordEventAsync("TranscriptionError", new Dictionary<string, object> { ["Error"] = ex.Message });
                }

                if (_bootstrapper.FeedbackService != null)
                {
                    await _bootstrapper.FeedbackService.ShowToastNotificationAsync(
                        "Transcription Error",
                        ex.Message,
                        IFeedbackService.NotificationType.Error
                    ).ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException handlerEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing transcription error: {handlerEx.Message}");
            }
        }
        
        private async void OnTranscriptionCompleted(object? sender, string transcription)
        {
                try
                {
                    if (_metricsService != null)
                    {
                        await _metricsService.RecordEventAsync("TranscriptionCompleted");
                    }

                    string? textToInject = transcription;
                    
                    // Check if text review is enabled
                    if (_bootstrapper.SettingsService?.Settings.UI.EnableTextReview == true)
                    {
                        // Show review window on UI thread and wait for result
                        var reviewTask = await Application.Current.Dispatcher.InvokeAsync<(ReviewResult result, string? reviewedText)>(() =>
                        {
                            var timeout = _bootstrapper.SettingsService.Settings.UI.AutoInsertAfterReview 
                                ? _bootstrapper.SettingsService.Settings.UI.ReviewWindowTimeoutSeconds 
                                : 0;
                            
                            var result = TranscriptionReviewWindow.ShowReview(transcription, out string? reviewedText, timeout);
                            return (result, reviewedText);
                        });
                        
                        if (reviewTask.result == ReviewResult.Inserted)
                        {
                            textToInject = reviewTask.reviewedText;
                        }
                        else
                        {
                            // User cancelled - don't inject
                            System.Diagnostics.Debug.WriteLine("Transcription review cancelled by user");
                            return;
                        }
                    }
                    
                    // Inject the text (original or reviewed)
                    if (_bootstrapper.TextInjectionService != null && !string.IsNullOrEmpty(textToInject))
                    {
                        await _bootstrapper.TextInjectionService.InjectTextAsync(textToInject).ConfigureAwait(false);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    await HandleExceptionAsync("Text injection error", ex).ConfigureAwait(false);
                }
                catch (System.IO.IOException ex)
                {
                    await HandleExceptionAsync("Text injection I/O error", ex).ConfigureAwait(false);
                }
        }
        
        private async void OnFreeTierWarning(object? sender, FreeTierWarning e)
        {
            try
            {
                if (_bootstrapper.FeedbackService != null)
                {
                    var message = $"You've used {e.UsagePercentage:F1}% of your ${e.Limit:F2} free tier limit";
                    await _bootstrapper.FeedbackService.ShowToastNotificationAsync(
                        "Usage Warning",
                        message,
                        IFeedbackService.NotificationType.Warning
                    ).ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing free tier warning: {ex.Message}");
            }
        }
        
        private async void OnFreeTierExceeded(object? sender, FreeTierExceeded e)
        {
            try
            {
                if (_bootstrapper.FeedbackService != null)
                {
                    var message = $"Free tier limit of ${e.Limit:F2} exceeded. Total cost: ${e.MonthlyUsage.Cost:F2}";
                    await _bootstrapper.FeedbackService.ShowToastNotificationAsync(
                        "Usage Limit Exceeded",
                        message,
                        IFeedbackService.NotificationType.Error
                    ).ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing free tier exceeded: {ex.Message}");
            }
        }
        
        private async void OnAudioDataAvailable(object? sender, byte[] audioData)
        {
            try
            {
                if (_bootstrapper.WhisperService != null)
                {
                    await _bootstrapper.WhisperService.TranscribeAudioAsync(audioData).ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException ex)
            {
                await HandleExceptionAsync("Audio transcription error", ex).ConfigureAwait(false);
            }
            catch (System.IO.IOException ex)
            {
                await HandleExceptionAsync("Audio transcription I/O error", ex).ConfigureAwait(false);
            }
        }
        
        private async void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            try
            {
                // Handle settings changes
                if (e.Key == "Hotkeys:ToggleRecording" && _bootstrapper.HotkeyService != null)
                {
                    // Reinitialize hotkeys when the hotkey configuration changes
                    _bootstrapper.HotkeyService.Dispose();
                    _bootstrapper.InitializeHotkeyService();
                }
                
                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await HandleExceptionAsync("Settings change error", ex).ConfigureAwait(false);
            }
        }
        
        #endregion
        
        #region Exception Handling
        
        private async Task HandleExceptionAsync(string context, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{context}: {ex.Message}");
            
            if (_bootstrapper.FeedbackService != null)
            {
                await _bootstrapper.FeedbackService.ShowToastNotificationAsync(
                    "Error",
                    $"{context}: {ex.Message}",
                    IFeedbackService.NotificationType.Error
                ).ConfigureAwait(false);
            }
        }
        
        private async void HandleException(string context, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{context}: {ex.Message}");
            
            if (_bootstrapper.FeedbackService != null)
            {
                await _bootstrapper.FeedbackService.ShowToastNotificationAsync(
                    "Error",
                    $"{context}: {ex.Message}",
                    IFeedbackService.NotificationType.Error
                ).ConfigureAwait(false);
            }
        }
        
        #endregion
    }
}
