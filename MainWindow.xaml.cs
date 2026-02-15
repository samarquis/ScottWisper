using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WhisperKey.Configuration;
using WhisperKey.Services;
using WhisperKey.ViewModels;

namespace WhisperKey
{
    public partial class MainWindow : Window
    {
        private MainViewModel? _viewModel;
        private bool _isHidden = false;

        public event EventHandler? StartDictationRequested;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int GWL_EX_STYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
        }

        public void SetViewModel(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.StartDictationRequested += OnViewModelStartDictationRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelStartDictationRequested(object? sender, EventArgs e)
        {
            StartDictationRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_viewModel == null) return;

            Dispatcher.Invoke(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(MainViewModel.IsHidden):
                        if (_viewModel.IsHidden)
                        {
                            HideWindowFromAltTab();
                            Hide();
                        }
                        else
                        {
                            ShowWindowInAltTab();
                            Show();
                            WindowState = WindowState.Normal;
                            Activate();
                            Focus();
                        }
                        break;
                }
            });
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                await _viewModel.InitializeAsync();
            }
            else
            {
                await InitializeDefaultAsync();
            }

            HideWindowFromAltTab();
            Hide();
            _isHidden = true;
        }

        private async Task InitializeDefaultAsync()
        {
            var feedbackService = Application.Current.Properties["FeedbackService"] as FeedbackService;
            var settingsService = Application.Current.Properties["SettingsService"] as ISettingsService;
            var responsiveService = (Application.Current as App)?.Properties["ResponsiveService"] as IResponsiveUIService;
            var onboardingService = (Application.Current as App)?.Properties["OnboardingService"] as IOnboardingService;
            var textInjectionService = Application.Current.Properties["TextInjectionService"] as ITextInjection;

            _viewModel = new MainViewModel(
                feedbackService!,
                settingsService!,
                responsiveService!,
                onboardingService!,
                textInjectionService!);

            DataContext = _viewModel;
            _viewModel.StartDictationRequested += OnViewModelStartDictationRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            await _viewModel.InitializeAsync();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                HideWindowFromAltTab();
                Hide();
                _isHidden = true;
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            HideWindowFromAltTab();
            Hide();
            _isHidden = true;
        }

        private void HideWindowFromAltTab()
        {
            var hWnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hWnd != IntPtr.Zero)
            {
                int extendedStyle = GetWindowLong(hWnd, GWL_EX_STYLE);
                SetWindowLong(hWnd, GWL_EX_STYLE, extendedStyle | WS_EX_TOOLWINDOW);
            }
        }

        private void ShowWindowInAltTab()
        {
            var hWnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hWnd != IntPtr.Zero)
            {
                int extendedStyle = GetWindowLong(hWnd, GWL_EX_STYLE);
                SetWindowLong(hWnd, GWL_EX_STYLE, extendedStyle & ~WS_EX_TOOLWINDOW);
            }
        }

        public void ShowFromTray()
        {
            ShowWindowInAltTab();
            Show();
            WindowState = WindowState.Normal;
            _isHidden = false;
            Activate();
            Focus();
            _viewModel?.ShowFromTray();
        }

        public void HideToTray()
        {
            HideWindowFromAltTab();
            Hide();
            _isHidden = true;
            _viewModel?.HideToTray();
        }

        public void ToggleVisibility()
        {
            if (_isHidden)
            {
                ShowFromTray();
            }
            else
            {
                HideToTray();
            }
        }

        public bool IsWindowHidden => _isHidden;

        public IFeedbackService? FeedbackService => _viewModel?.FeedbackService;

        public async Task SetTextInjectionServiceAsync(ITextInjection textInjectionService)
        {
            if (_viewModel != null)
            {
                await _viewModel.SetTextInjectionServiceAsync(textInjectionService);
            }
        }

        public async Task UpdateFeedbackStatus(IFeedbackService.DictationStatus status, string? message = null)
        {
            if (_viewModel != null)
            {
                await _viewModel.SetStatusAsync(status, message);
            }
        }

        protected override async void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.StartDictationRequested -= OnViewModelStartDictationRequested;
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                await _viewModel.CleanupAsync();
            }

            base.OnClosed(e);
        }
    }
}
