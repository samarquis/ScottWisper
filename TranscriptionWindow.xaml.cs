using System;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using WhisperKey.Services;
using WhisperKey.ViewModels;

namespace WhisperKey
{
    public partial class TranscriptionWindow : Window
    {
        private TranscriptionViewModel? _viewModel;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public TranscriptionWindow()
        {
            InitializeComponent();
            Loaded += OnWindowLoaded;
            KeyDown += OnKeyDown;
            MouseMove += OnMouseMove;
        }

        public void SetViewModel(TranscriptionViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.RequestClose += OnViewModelRequestClose;
        }

        private void OnViewModelRequestClose(object? sender, EventArgs e)
        {
            Hide();
        }

        public void InitializeServices(IWhisperService whisperService, CostTrackingService costTrackingService)
        {
            if (_viewModel != null)
            {
                _viewModel.InitializeServices(whisperService, costTrackingService);
            }
            else
            {
                _viewModel = new TranscriptionViewModel(whisperService, costTrackingService);
                DataContext = _viewModel;
                _viewModel.RequestClose += OnViewModelRequestClose;
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            PositionWindowNearCursor();
            _viewModel?.ShowForDictation();
        }

        private void PositionWindowNearCursor()
        {
            try
            {
                GetCursorPos(out POINT cursorPos);
                var cursorX = cursorPos.X;
                var cursorY = cursorPos.Y;

                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;

                var left = cursorX + 20;
                var top = cursorY - (this.Height / 2);

                if (left + this.Width > screenWidth)
                    left = (int)(screenWidth - this.Width - 20);
                if (left < 0)
                    left = 20;
                if (top + this.Height > screenHeight)
                    top = (int)(screenHeight - this.Height - 20);
                if (top < 0)
                    top = 20;

                this.Left = left;
                this.Top = top;
            }
            catch
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            _viewModel?.ResetActivity();
        }

        public void AppendTranscriptionText(string text)
        {
            _viewModel?.AppendTranscriptionText(text);
        }

        public void SetStatus(ViewModels.TranscriptionStatus status)
        {
            if (_viewModel != null)
            {
                _viewModel.CurrentStatus = status;
            }
        }

        public void ShowForDictation()
        {
            _viewModel?.ShowForDictation();
            PositionWindowNearCursor();
            this.Show();
            this.Activate();
        }

        public void SetRecordingStatus()
        {
            _viewModel?.SetRecordingStatus();
        }

        public void SetProcessingStatus()
        {
            _viewModel?.SetProcessingStatus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.RequestClose -= OnViewModelRequestClose;
                _viewModel.Cleanup();
            }

            base.OnClosed(e);
        }
    }
}
