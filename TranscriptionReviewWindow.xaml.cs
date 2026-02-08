using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WhisperKey
{
    /// <summary>
    /// Interaction logic for TranscriptionReviewWindow.xaml
    /// Allows users to review and edit transcribed text before injection.
    /// </summary>
    public partial class TranscriptionReviewWindow : Window
    {
        private DispatcherTimer? _countdownTimer;
        private int _remainingSeconds;
        private readonly bool _autoInsertEnabled;
        private string _reviewedText = string.Empty;
        private bool _isEditingMode = false;
        
        /// <summary>
        /// Result of the review operation
        /// </summary>
        public ReviewResult Result { get; private set; } = ReviewResult.Cancelled;
        
        /// <summary>
        /// The final reviewed text (may be edited by user)
        /// </summary>
        public string ReviewedText => _reviewedText;
        
        /// <summary>
        /// Creates a new transcription review window
        /// </summary>
        /// <param name="originalText">The transcribed text to review</param>
        /// <param name="autoInsertTimeout">Seconds before auto-insert (0 to disable)</param>
        public TranscriptionReviewWindow(string originalText, int autoInsertTimeout = 0)
        {
            InitializeComponent();
            
            // Set up the text
            ReviewTextBox.Text = originalText ?? string.Empty;
            ReviewTextBox.CaretIndex = ReviewTextBox.Text.Length;
            
            // Focus the text box for immediate editing
            Loaded += (s, e) =>
            {
                ReviewTextBox.Focus();
                ReviewTextBox.SelectAll();
            };
            
            // Update character count
            UpdateCharacterCount();
            ReviewTextBox.TextChanged += (s, e) => UpdateCharacterCount();
            
            // Set up auto-insert timer if enabled
            _autoInsertEnabled = autoInsertTimeout > 0;
            if (_autoInsertEnabled)
            {
                _remainingSeconds = autoInsertTimeout;
                TimerText.Visibility = Visibility.Visible;
                UpdateTimerDisplay();
                
                _countdownTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _countdownTimer.Tick += OnCountdownTimerTick;
                _countdownTimer.Start();
            }
            
            // Handle key presses
            KeyDown += OnKeyDown;
        }
        
        /// <summary>
        /// Shows the review window modally and returns the result
        /// </summary>
        public static ReviewResult ShowReview(string text, out string? reviewedText, int timeoutSeconds = 0)
        {
            var window = new TranscriptionReviewWindow(text, timeoutSeconds);
            window.ShowDialog();
            reviewedText = window.Result == ReviewResult.Inserted ? window.ReviewedText : null;
            return window.Result;
        }
        
        /// <summary>
        /// Shows the review window non-modally with callback
        /// </summary>
        public static void ShowReviewAsync(string text, int timeoutSeconds, Action<ReviewResult, string?> callback)
        {
            var window = new TranscriptionReviewWindow(text, timeoutSeconds);
            window.Closed += (s, e) =>
            {
                callback(window.Result, window.Result == ReviewResult.Inserted ? window.ReviewedText : null);
            };
            window.Show();
        }
        
        private void UpdateCharacterCount()
        {
            var count = ReviewTextBox.Text?.Length ?? 0;
            CharacterCountText.Text = $"{count} character{(count == 1 ? "" : "s")}";
        }
        
        private void UpdateTimerDisplay()
        {
            if (_autoInsertEnabled && _remainingSeconds > 0)
            {
                TimerText.Text = $"Auto-insert in {_remainingSeconds}s";
                TimerText.Foreground = _remainingSeconds <= 5 
                    ? System.Windows.Media.Brushes.Red 
                    : System.Windows.Media.Brushes.Gray;
            }
            else
            {
                TimerText.Visibility = Visibility.Collapsed;
            }
        }
        
        private void OnCountdownTimerTick(object? sender, EventArgs e)
        {
            _remainingSeconds--;
            UpdateTimerDisplay();
            
            if (_remainingSeconds <= 0)
            {
                _countdownTimer?.Stop();
                if (!_isEditingMode)
                {
                    // Auto-insert
                    Result = ReviewResult.Inserted;
                    _reviewedText = ReviewTextBox.Text ?? string.Empty;
                    Close();
                }
            }
        }
        
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    // Cancel on Escape
                    CancelButton_Click(sender, e);
                    e.Handled = true;
                    break;
                    
                case Key.Enter when Keyboard.Modifiers == ModifierKeys.Control:
                    // Ctrl+Enter to insert
                    InsertButton_Click(sender, e);
                    e.Handled = true;
                    break;
                    
                case Key.S when Keyboard.Modifiers == ModifierKeys.Control:
                    // Ctrl+S to save (insert)
                    InsertButton_Click(sender, e);
                    e.Handled = true;
                    break;
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _countdownTimer?.Stop();
            Result = ReviewResult.Cancelled;
            _reviewedText = string.Empty;
            Close();
        }
        
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch back from editing mode to normal mode
            _isEditingMode = false;
            
            // Hide edit button, show insert button
            EditButton.Visibility = Visibility.Collapsed;
            InsertButton.Visibility = Visibility.Visible;
            
            // Restart countdown if auto-insert was enabled
            if (_autoInsertEnabled && _countdownTimer != null && !_countdownTimer.IsEnabled)
            {
                _countdownTimer.Start();
            }
        }
        
        private void InsertButton_Click(object sender, RoutedEventArgs e)
        {
            _countdownTimer?.Stop();
            Result = ReviewResult.Inserted;
            _reviewedText = ReviewTextBox.Text ?? string.Empty;
            Close();
        }
        
        /// <summary>
        /// Enables editing mode (pauses auto-insert timer)
        /// </summary>
        public void EnableEditingMode()
        {
            _isEditingMode = true;
            _countdownTimer?.Stop();
            
            // Show edit button to return to normal mode
            EditButton.Visibility = Visibility.Visible;
            InsertButton.Visibility = Visibility.Collapsed;
            
            TimerText.Text = "Editing - timer paused";
            TimerText.Foreground = System.Windows.Media.Brushes.Orange;
        }
        
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _countdownTimer?.Stop();
            base.OnClosing(e);
        }
        
        protected override void OnClosed(EventArgs e)
        {
            _countdownTimer?.Stop();
            base.OnClosed(e);
        }
    }
    
    /// <summary>
    /// Result of the transcription review
    /// </summary>
    public enum ReviewResult
    {
        /// <summary>
        /// User cancelled - do not insert text
        /// </summary>
        Cancelled,
        
        /// <summary>
        /// User approved - insert the reviewed text
        /// </summary>
        Inserted,
        
        /// <summary>
        /// User chose to continue editing
        /// </summary>
        Editing
    }
}
