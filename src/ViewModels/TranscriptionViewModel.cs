using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WhisperKey.Services;

namespace WhisperKey.ViewModels
{
    public class TranscriptionViewModel : INotifyPropertyChanged
    {
        private IWhisperService? _whisperService;
        private CostTrackingService? _costTrackingService;
        private readonly DispatcherTimer _autoHideTimer;
        private DateTime _lastActivity = DateTime.Now;

        public ICommand? CloseCommand { get; private set; }

        private string _transcriptionText = "Listening for your voice...";
        public string TranscriptionText
        {
            get => _transcriptionText;
            set => SetProperty(ref _transcriptionText, value);
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private Brush _statusIndicatorFill = Brushes.LightGreen;
        public Brush StatusIndicatorFill
        {
            get => _statusIndicatorFill;
            set => SetProperty(ref _statusIndicatorFill, value);
        }

        private string _usageText = "0 requests | $0.0000";
        public string UsageText
        {
            get => _usageText;
            set => SetProperty(ref _usageText, value);
        }

        private TranscriptionStatus _currentStatus = TranscriptionStatus.Ready;
        public TranscriptionStatus CurrentStatus
        {
            get => _currentStatus;
            set
            {
                if (SetProperty(ref _currentStatus, value))
                {
                    OnStatusChanged();
                }
            }
        }

        public event EventHandler? RequestClose;
        public event PropertyChangedEventHandler? PropertyChanged;

        public TranscriptionViewModel()
        {
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _autoHideTimer.Tick += OnAutoHideTimer;

            InitializeCommands();
        }

        public TranscriptionViewModel(IWhisperService whisperService, CostTrackingService costTrackingService)
        {
            _whisperService = whisperService;
            _costTrackingService = costTrackingService;
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _autoHideTimer.Tick += OnAutoHideTimer;

            InitializeCommands();
            SubscribeToEvents();
        }

        private void InitializeCommands()
        {
            CloseCommand = new RelayCommand(() =>
            {
                RequestClose?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            });
        }

        private void SubscribeToEvents()
        {
            if (_whisperService != null)
            {
                _whisperService.TranscriptionCompleted += OnTranscriptionCompleted;
                _whisperService.TranscriptionError += OnTranscriptionError;
            }

            if (_costTrackingService != null)
            {
                _costTrackingService.UsageUpdated += OnUsageUpdated;
            }
        }

        public void InitializeServices(IWhisperService whisperService, CostTrackingService costTrackingService)
        {
            _whisperService = whisperService;
            _costTrackingService = costTrackingService;
            SubscribeToEvents();
            UpdateUsageDisplay();
        }

        public void Initialize(IWhisperService? whisperService = null, CostTrackingService? costTrackingService = null)
        {
            if (whisperService != null) _whisperService = whisperService;
            if (costTrackingService != null) _costTrackingService = costTrackingService;
            SubscribeToEvents();
        }

        private void OnTranscriptionCompleted(object? sender, string transcriptionText)
        {
            AppendTranscriptionText(transcriptionText);
            CurrentStatus = TranscriptionStatus.Ready;
        }

        private void OnTranscriptionError(object? sender, Exception error)
        {
            AppendTranscriptionText($"[Error: {error.Message}]");
            CurrentStatus = TranscriptionStatus.Ready;
        }

        private void OnUsageUpdated(object? sender, UsageStats stats)
        {
            UpdateUsageDisplay();
        }

        public void AppendTranscriptionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (TranscriptionText == "Listening for your voice...")
            {
                TranscriptionText = text;
            }
            else
            {
                TranscriptionText = TranscriptionText + " " + text;
            }

            ResetActivity();
        }

        private void OnStatusChanged()
        {
            switch (CurrentStatus)
            {
                case TranscriptionStatus.Ready:
                    StatusIndicatorFill = Brushes.LightGreen;
                    StatusText = "Ready";
                    break;
                case TranscriptionStatus.Recording:
                    StatusIndicatorFill = Brushes.Red;
                    StatusText = "Recording";
                    break;
                case TranscriptionStatus.Processing:
                    StatusIndicatorFill = Brushes.Gold;
                    StatusText = "Processing";
                    break;
            }
        }

        public void UpdateUsageDisplay()
        {
            if (_costTrackingService == null)
            {
                UsageText = "0 requests | $0.0000";
                return;
            }

            var stats = _costTrackingService.GetUsageStats();
            UsageText = $"{stats.RequestCount} requests | ${stats.EstimatedCost:F4}";
        }

        public void ResetActivity()
        {
            _lastActivity = DateTime.Now;
            ResetAutoHideTimer();
        }

        private void StartAutoHideTimer()
        {
            _autoHideTimer.Start();
        }

        private void ResetAutoHideTimer()
        {
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        }

        private void OnAutoHideTimer(object? sender, EventArgs e)
        {
            if (DateTime.Now.Subtract(_lastActivity).TotalSeconds > 30)
            {
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ShowForDictation()
        {
            CurrentStatus = TranscriptionStatus.Ready;
            StartAutoHideTimer();
            ResetActivity();
        }

        public void SetRecordingStatus()
        {
            CurrentStatus = TranscriptionStatus.Recording;
        }

        public void SetProcessingStatus()
        {
            CurrentStatus = TranscriptionStatus.Processing;
        }

        public void Cleanup()
        {
            if (_whisperService != null)
            {
                _whisperService.TranscriptionCompleted -= OnTranscriptionCompleted;
                _whisperService.TranscriptionError -= OnTranscriptionError;
            }

            if (_costTrackingService != null)
            {
                _costTrackingService.UsageUpdated -= OnUsageUpdated;
            }

            _autoHideTimer?.Stop();
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

    public enum TranscriptionStatus
    {
        Ready,
        Recording,
        Processing
    }
}
