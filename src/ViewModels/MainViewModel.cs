using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using WhisperKey.Services;

namespace WhisperKey.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IFeedbackService? _feedbackService;
        private readonly ISettingsService? _settingsService;
        private readonly IResponsiveUIService? _responsiveService;
        private readonly IOnboardingService? _onboardingService;
        private ITextInjection? _textInjectionService;

        private bool _isHidden;
        private int _todayRecordings;
        private DateTime _sessionStartTime = DateTime.Now;

        public ICommand? StartDictationCommand { get; private set; }
        public ICommand? SettingsCommand { get; private set; }
        public ICommand? HelpCommand { get; private set; }
        public ICommand? ClearHistoryCommand { get; private set; }
        public ICommand? ToggleVisibilityCommand { get; private set; }
        public ICommand? TestInjectionCommand { get; private set; }

        private IFeedbackService.DictationStatus _currentStatus = IFeedbackService.DictationStatus.Idle;
        public IFeedbackService.DictationStatus CurrentStatus
        {
            get => _currentStatus;
            set
            {
                if (SetProperty(ref _currentStatus, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusDescription));
                    OnPropertyChanged(nameof(MainStatusMessage));
                    OnPropertyChanged(nameof(FooterStatus));
                    OnPropertyChanged(nameof(FooterMessage));
                    OnPropertyChanged(nameof(IsRecordingOrProcessing));
                }
            }
        }

        public string StatusText => CurrentStatus switch
        {
            IFeedbackService.DictationStatus.Idle => "● Idle - Ready to start recording",
            IFeedbackService.DictationStatus.Ready => "● Ready - Press hotkey to begin recording",
            IFeedbackService.DictationStatus.Recording => "● Recording - In progress",
            IFeedbackService.DictationStatus.Processing => "● Processing - Transcribing audio",
            IFeedbackService.DictationStatus.Complete => "● Complete - Ready for review",
            IFeedbackService.DictationStatus.Error => "● Error - Please check settings",
            _ => CurrentStatus.ToString()
        };

        public string StatusDescription => CurrentStatus switch
        {
            IFeedbackService.DictationStatus.Idle => "Application is idle and waiting for input",
            IFeedbackService.DictationStatus.Ready => "Ready to record with keyboard shortcut",
            IFeedbackService.DictationStatus.Recording => "Recording in progress",
            IFeedbackService.DictationStatus.Processing => "Processing audio and transcribing",
            IFeedbackService.DictationStatus.Complete => "Recording complete, ready for review",
            IFeedbackService.DictationStatus.Error => "Error occurred, check settings",
            _ => "Unknown status"
        };

        public string MainStatusMessage => CurrentStatus switch
        {
            IFeedbackService.DictationStatus.Idle => "Waiting for activation",
            IFeedbackService.DictationStatus.Ready => "Ready for voice dictation",
            IFeedbackService.DictationStatus.Recording => "Listening... Speak clearly",
            IFeedbackService.DictationStatus.Processing => "Converting speech to text",
            IFeedbackService.DictationStatus.Complete => "Text ready for insertion",
            IFeedbackService.DictationStatus.Error => "Please try again",
            _ => "Unknown status"
        };

        public string FooterStatus => StatusText;

        public string FooterMessage
        {
            get
            {
                var sessionDuration = DateTime.Now - _sessionStartTime;
                return CurrentStatus switch
                {
                    IFeedbackService.DictationStatus.Idle => $"Application idle | Session: {sessionDuration:h\\:mm}",
                    IFeedbackService.DictationStatus.Ready => $"Ready to record | Session: {sessionDuration:h\\:mm}",
                    IFeedbackService.DictationStatus.Recording => "Recording speech... | Click to stop",
                    IFeedbackService.DictationStatus.Processing => "Processing with AI | Please wait",
                    IFeedbackService.DictationStatus.Complete => "Transcription completed successfully",
                    IFeedbackService.DictationStatus.Error => "Error occurred | Check microphone and connection",
                    _ => "Status unknown"
                };
            }
        }

        public Brush StatusColor => CurrentStatus switch
        {
            IFeedbackService.DictationStatus.Idle => Brushes.Gray,
            IFeedbackService.DictationStatus.Ready => new SolidColorBrush(Color.FromRgb(40, 167, 69)),
            IFeedbackService.DictationStatus.Recording => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            IFeedbackService.DictationStatus.Processing => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            IFeedbackService.DictationStatus.Complete => new SolidColorBrush(Color.FromRgb(40, 167, 69)),
            IFeedbackService.DictationStatus.Error => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            _ => Brushes.Gray
        };

        public bool IsRecordingOrProcessing =>
            CurrentStatus == IFeedbackService.DictationStatus.Recording ||
            CurrentStatus == IFeedbackService.DictationStatus.Processing;

        private string _statusTime = string.Empty;
        public string StatusTime
        {
            get => _statusTime;
            set => SetProperty(ref _statusTime, value);
        }

        private string _progressOperation = string.Empty;
        public string ProgressOperation
        {
            get => _progressOperation;
            set => SetProperty(ref _progressOperation, value);
        }

        private string _progressDetails = string.Empty;
        public string ProgressDetails
        {
            get => _progressDetails;
            set => SetProperty(ref _progressDetails, value);
        }

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private string _progressPercentage = "0%";
        public string ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        private bool _isProgressActive;
        public bool IsProgressActive
        {
            get => _isProgressActive;
            set => SetProperty(ref _isProgressActive, value);
        }

        public string TodayRecordings => _todayRecordings.ToString();

        public string UsageTime
        {
            get
            {
                var sessionDuration = DateTime.Now - _sessionStartTime;
                return sessionDuration.TotalHours < 1
                    ? $"{sessionDuration.TotalMinutes:F0}m"
                    : $"{sessionDuration.TotalHours:F1}h";
            }
        }

        public string ApiUsage
        {
            get
            {
                var estimatedUsage = _todayRecordings * 0.002;
                return $"${estimatedUsage:F3}";
            }
        }

        private int _historyCount;
        public int HistoryCount
        {
            get => _historyCount;
            set => SetProperty(ref _historyCount, value);
        }

        public ObservableCollection<StatusHistoryItem> StatusHistory { get; } = new();

        public bool IsHidden
        {
            get => _isHidden;
            set => SetProperty(ref _isHidden, value);
        }

        public IFeedbackService? FeedbackService => _feedbackService;

        public event EventHandler? StartDictationRequested;
        public event PropertyChangedEventHandler? PropertyChanged;

        public MainViewModel() { }

        public MainViewModel(
            IFeedbackService feedbackService,
            ISettingsService settingsService,
            IResponsiveUIService responsiveService,
            IOnboardingService onboardingService,
            ITextInjection textInjectionService)
        {
            _feedbackService = feedbackService;
            _settingsService = settingsService;
            _responsiveService = responsiveService;
            _onboardingService = onboardingService;
            _textInjectionService = textInjectionService;

            InitializeCommands();
        }

        private void InitializeCommands()
        {
            StartDictationCommand = new RelayCommand(async () => await StartDictationAsync());
            SettingsCommand = new RelayCommand(async () => await ShowSettingsAsync());
            HelpCommand = new RelayCommand(async () => await ShowHelpAsync());
            ClearHistoryCommand = new RelayCommand(async () => await ClearHistoryAsync());
            ToggleVisibilityCommand = new RelayCommand(async () => await ToggleVisibilityAsync());
            TestInjectionCommand = new RelayCommand(async () => await TestTextInjectionAsync());
        }

        public async Task InitializeAsync()
        {
            if (_feedbackService != null)
            {
                await _feedbackService.InitializeAsync();
                _feedbackService.StatusChanged += OnStatusChanged;
                if (_feedbackService is FeedbackService enhancedFeedback)
                {
                    enhancedFeedback.StatusHistoryUpdated += OnStatusHistoryUpdated;
                    enhancedFeedback.ProgressUpdated += OnProgressUpdated;
                }
            }

            if (_settingsService != null)
            {
                _settingsService.SettingsChanged += OnSettingsChanged;
            }

            if (_onboardingService != null && _onboardingService.IsOnboardingRequired())
            {
                await _onboardingService.StartWelcomeAsync();
            }

            if (_textInjectionService != null)
            {
                await _textInjectionService.InitializeAsync();
            }

            await SetStatusAsync(IFeedbackService.DictationStatus.Ready, "Application ready");
        }

        private void OnStatusChanged(object? sender, IFeedbackService.DictationStatus status)
        {
            CurrentStatus = status;
            StatusTime = DateTime.Now.ToString("HH:mm:ss");

            if (status == IFeedbackService.DictationStatus.Recording)
            {
                _todayRecordings++;
                OnPropertyChanged(nameof(TodayRecordings));
                OnPropertyChanged(nameof(UsageTime));
                OnPropertyChanged(nameof(ApiUsage));
            }
        }

        private void OnStatusHistoryUpdated(object? sender, StatusHistoryEntry entry)
        {
            var historyItem = new StatusHistoryItem
            {
                Status = entry.Status,
                Timestamp = entry.Timestamp,
                Message = entry.Message,
                Duration = entry.Duration
            };

            StatusHistory.Insert(0, historyItem);

            while (StatusHistory.Count > 20)
            {
                StatusHistory.RemoveAt(StatusHistory.Count - 1);
            }

            HistoryCount = StatusHistory.Count;
        }

        private void OnProgressUpdated(object? sender, ProgressState progress)
        {
            ProgressOperation = progress.Operation;
            ProgressDetails = progress.Details ?? "";
            ProgressValue = (int)progress.Progress;
            ProgressPercentage = $"{progress.Progress:F0}%";
            IsProgressActive = progress.IsActive;
        }

        private async void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            try
            {
                switch (e.Category)
                {
                    case "UI":
                        await ApplyUISettingsAsync();
                        break;
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException && ex is not AccessViolationException)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply settings change in MainViewModel: {ex.Message}");
            }
        }

        private async Task ApplyUISettingsAsync()
        {
            try
            {
                if (_settingsService?.Settings?.UI == null)
                    return;

                var uiSettings = _settingsService.Settings.UI;

                if (uiSettings.MinimizeToTray && !IsHidden)
                {
                    IsHidden = true;
                }
                else if (!uiSettings.MinimizeToTray && IsHidden)
                {
                    IsHidden = false;
                }

                await Task.CompletedTask;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException && ex is not AccessViolationException)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply UI settings: {ex.Message}");
            }
        }

        public async Task SetStatusAsync(IFeedbackService.DictationStatus status, string? message = null)
        {
            if (_feedbackService != null)
            {
                await _feedbackService.SetStatusAsync(status, message);
            }
            CurrentStatus = status;
        }

        public async Task SetTextInjectionServiceAsync(ITextInjection textInjectionService)
        {
            _textInjectionService = textInjectionService;
            if (_textInjectionService != null)
            {
                await _textInjectionService.InitializeAsync();
            }
        }

        private Task StartDictationAsync()
        {
            StartDictationRequested?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        private async Task ShowSettingsAsync()
        {
            if (_textInjectionService == null || _settingsService == null)
            {
                await ShowNotificationAsync("Settings Error", "Text injection service not available");
                return;
            }

            try
            {
                var settings = _settingsService.Settings;
                var textSettings = settings.TextInjection;
                var message = $"Text Injection Settings:\n\n" +
                              $"Enabled: {textSettings.Enabled}\n" +
                              $"Method: {textSettings.PreferredMethod}\n" +
                              $"Clipboard Fallback: {textSettings.UseClipboardFallback}\n" +
                              $"Retry Count: {textSettings.RetryCount}\n" +
                              $"Char Delay: {textSettings.DelayBetweenCharsMs}ms\n" +
                              $"Debug Mode: {textSettings.EnableDebugMode}";

                System.Windows.MessageBox.Show(message, "Text Injection Settings",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException && ex is not AccessViolationException)
            {
                await ShowNotificationAsync("Settings Error", $"Error showing settings: {ex.Message}");
            }
        }

        private Task ShowHelpAsync()
        {
            _ = ShowNotificationAsync("Help", "Help documentation coming soon!");
            return Task.CompletedTask;
        }

        private Task ClearHistoryAsync()
        {
            StatusHistory.Clear();
            HistoryCount = 0;
            return Task.CompletedTask;
        }

        private Task ToggleVisibilityAsync()
        {
            IsHidden = !IsHidden;
            return Task.CompletedTask;
        }

        public void ShowFromTray()
        {
            IsHidden = false;
        }

        public void HideToTray()
        {
            IsHidden = true;
        }

        private async Task TestTextInjectionAsync()
        {
            if (_textInjectionService == null)
            {
                await ShowNotificationAsync("Error", "Text injection service not available");
                return;
            }

            try
            {
                if (_feedbackService != null)
                {
                    await _feedbackService.StartProgressAsync("Testing Injection", TimeSpan.FromSeconds(10));
                    await _feedbackService.UpdateProgressAsync(20, "Preparing test injection...");
                }

                var testResult = await _textInjectionService.TestInjectionAsync();

                if (_feedbackService != null)
                {
                    await _feedbackService.UpdateProgressAsync(80, "Analyzing test result...");
                }

                var resultMessage = testResult.Success
                    ? $"Injection test successful in {testResult.Duration.TotalMilliseconds}ms using {testResult.MethodUsed}"
                    : $"Injection test failed: {string.Join(", ", testResult.Issues)}";

                await ShowNotificationAsync(testResult.Success ? "Test Success" : "Test Failed", resultMessage);

                if (_feedbackService != null)
                {
                    await _feedbackService.CompleteProgressAsync(testResult.Success ? "Injection test completed" : "Injection test failed");
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException && ex is not AccessViolationException)
            {
                await ShowNotificationAsync("Test Error", $"Injection test error: {ex.Message}");
                if (_feedbackService != null)
                {
                    await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Error, $"Test failed: {ex.Message}");
                }
            }
        }

        private async Task ShowNotificationAsync(string message, string title = "WhisperKey")
        {
            if (_feedbackService != null)
            {
                await _feedbackService.ShowNotificationAsync(title, message);
            }
        }

        public async Task CleanupAsync()
        {
            if (_feedbackService != null)
            {
                _feedbackService.StatusChanged -= OnStatusChanged;
                if (_feedbackService is FeedbackService enhancedFeedback)
                {
                    enhancedFeedback.StatusHistoryUpdated -= OnStatusHistoryUpdated;
                    enhancedFeedback.ProgressUpdated -= OnProgressUpdated;
                }
                await _feedbackService.DisposeAsync();
            }

            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class StatusHistoryItem : INotifyPropertyChanged
    {
        private IFeedbackService.DictationStatus _status;
        private DateTime _timestamp;
        private string? _message;
        private TimeSpan _duration;

        public IFeedbackService.DictationStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); OnPropertyChanged(nameof(TimeString)); }
        }

        public string? Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(nameof(Message)); OnPropertyChanged(nameof(HasMessage)); }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(nameof(Duration)); OnPropertyChanged(nameof(DurationString)); }
        }

        public string StatusText => Status.ToString();
        public string TimeString => Timestamp.ToString("HH:mm:ss");
        public string DurationString => Duration.TotalSeconds < 60 ? $"{Duration.TotalSeconds:F1}s" : $"{Duration.TotalMinutes:F1}m";
        public bool HasMessage => !string.IsNullOrEmpty(Message);

        public Brush StatusColor => Status switch
        {
            IFeedbackService.DictationStatus.Idle => Brushes.Gray,
            IFeedbackService.DictationStatus.Ready => new SolidColorBrush(Color.FromRgb(40, 167, 69)),
            IFeedbackService.DictationStatus.Recording => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            IFeedbackService.DictationStatus.Processing => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            IFeedbackService.DictationStatus.Complete => new SolidColorBrush(Color.FromRgb(40, 167, 69)),
            IFeedbackService.DictationStatus.Error => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            _ => Brushes.Gray
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
