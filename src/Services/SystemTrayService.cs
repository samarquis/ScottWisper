using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace WhisperKey.Services
{
    /// <summary>
    /// System tray service for background application management
    /// </summary>
    public class SystemTrayService : IDisposable
    {
        public enum TrayStatus
        {
            Idle,       // Gray - Application ready but not in use
            Ready,      // Green - Ready to start dictation
            Recording,   // Red - Currently recording
            Processing,  // Yellow - Processing transcription
            Error,       // Gray/Red - Error state
            Offline      // Gray - Disconnected or unavailable
        }

        public enum IconTheme
        {
            Light,      // For dark taskbars
            Dark        // For light taskbars
        }

        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isDisposed = false;
        private bool _isDictating = false;
        private TrayStatus _currentStatus = TrayStatus.Idle;
        private IconTheme _currentTheme = IconTheme.Light;
        private readonly Dictionary<(TrayStatus, IconTheme), Icon> _statusIcons;
        private DateTime _lastStatusChange;
        private string _statusMessage = "WhisperKey - Ready";
        
        // Performance optimization fields
        private readonly System.Timers.Timer _memoryMonitorTimer;
        private long _lastGcMemory = 0;
        private int _notificationQueue = 0;
        private readonly object _lockObject = new();
        private bool _isCleaningUp = false;

        public event EventHandler? StartDictationRequested;
        public event EventHandler? StopDictationRequested;
        public event EventHandler? SettingsRequested;
        public event EventHandler? WindowToggleRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler<TrayStatus>? StatusChanged;

        public SystemTrayService()
        {
            _statusIcons = new Dictionary<(TrayStatus, IconTheme), Icon>();
            _lastStatusChange = DateTime.Now;
            _lastGcMemory = GC.GetTotalMemory(false);
            
            // Auto-detect system theme
            _currentTheme = DetectSystemTheme();
            
            // Initialize memory monitoring timer (checks every 30 seconds)
            _memoryMonitorTimer = new System.Timers.Timer(30000);
            _memoryMonitorTimer.Elapsed += OnMemoryMonitorTimer;
            _memoryMonitorTimer.AutoReset = true;
        }

        /// <summary>
        /// Auto-detects the system theme based on OS settings
        /// </summary>
        private IconTheme DetectSystemTheme()
        {
            try
            {
                // Check Windows theme using registry
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("SystemUsesLightTheme");
                        if (value is int lightTheme)
                        {
                            return lightTheme == 1 ? IconTheme.Dark : IconTheme.Light;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to light theme if detection fails
            }
            
            return IconTheme.Light;
        }

        /// <summary>
        /// Sets the icon theme explicitly
        /// </summary>
        public void SetIconTheme(IconTheme theme)
        {
            if (_currentTheme == theme)
                return;

            _currentTheme = theme;
            
            // Recreate icons with new theme
            CreateStatusIcons();
            
            // Update current icon
            if (_notifyIcon != null && _statusIcons.TryGetValue((_currentStatus, _currentTheme), out var icon))
            {
                _notifyIcon.Icon = icon;
            }
        }

        public void Initialize()
        {
            if (_notifyIcon != null)
                return;

            // Create status icons
            CreateStatusIcons();

            // Create notify icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = _statusIcons[(TrayStatus.Ready, _currentTheme)],
                Text = "WhisperKey - Ready"
            };

            // Create context menu
            CreateContextMenu();

            // Show icon
            _notifyIcon.Visible = true;

            // Handle mouse events
            _notifyIcon.MouseClick += OnNotifyIconClick;
            _notifyIcon.MouseDoubleClick += OnNotifyIconDoubleClick;

            // Set initial status
            UpdateStatus(TrayStatus.Ready);
            
            // Start memory monitoring for long-term stability
            _memoryMonitorTimer.Start();
        }

        private void CreateStatusIcons()
        {
            // Clear existing icons to support theme switching
            foreach (var icon in _statusIcons.Values)
            {
                icon?.Dispose();
            }
            _statusIcons.Clear();

            // Create themed icons for each status
            foreach (IconTheme theme in Enum.GetValues(typeof(IconTheme)))
            {
                _statusIcons[(TrayStatus.Idle, theme)] = CreateStatusIcon(TrayStatus.Idle, theme);
                _statusIcons[(TrayStatus.Ready, theme)] = CreateStatusIcon(TrayStatus.Ready, theme);
                _statusIcons[(TrayStatus.Recording, theme)] = CreateStatusIcon(TrayStatus.Recording, theme);
                _statusIcons[(TrayStatus.Processing, theme)] = CreateStatusIcon(TrayStatus.Processing, theme);
                _statusIcons[(TrayStatus.Error, theme)] = CreateStatusIcon(TrayStatus.Error, theme);
                _statusIcons[(TrayStatus.Offline, theme)] = CreateStatusIcon(TrayStatus.Offline, theme);
            }
        }

        private Icon CreateStatusIcon(TrayStatus status, IconTheme theme)
        {
            // Use 32x32 for higher quality, system will scale down
            var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                // Set high quality rendering
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.Clear(Color.Transparent);

                // Define colors based on status and theme
                var (primaryColor, secondaryColor, accentColor) = GetStatusColors(status, theme);
                
                // Draw the distinctive WhisperKey icon - stylized sound wave "W"
                DrawWhisperKeyIcon(graphics, primaryColor, secondaryColor, accentColor);
                
                // Draw status indicator overlay
                DrawStatusIndicator(graphics, status, accentColor);
            }

            return Icon.FromHandle(bitmap.GetHicon());
        }

        private (Color primary, Color secondary, Color accent) GetStatusColors(TrayStatus status, IconTheme theme)
        {
            // Base colors for light theme (dark icons on light taskbar)
            // Base colors for dark theme (light icons on dark taskbar)
            var isDarkTheme = theme == IconTheme.Dark;
            
            return status switch
            {
                TrayStatus.Idle => isDarkTheme 
                    ? (Color.FromArgb(160, 160, 160), Color.FromArgb(120, 120, 120), Color.FromArgb(140, 140, 140))
                    : (Color.FromArgb(128, 128, 128), Color.FromArgb(160, 160, 160), Color.FromArgb(100, 100, 100)),
                
                TrayStatus.Ready => isDarkTheme
                    ? (Color.FromArgb(76, 175, 80), Color.FromArgb(129, 199, 132), Color.FromArgb(46, 125, 50))
                    : (Color.FromArgb(46, 125, 50), Color.FromArgb(76, 175, 80), Color.FromArgb(27, 94, 32)),
                
                TrayStatus.Recording => isDarkTheme
                    ? (Color.FromArgb(244, 67, 54), Color.FromArgb(255, 138, 128), Color.FromArgb(198, 40, 40))
                    : (Color.FromArgb(198, 40, 40), Color.FromArgb(244, 67, 54), Color.FromArgb(183, 28, 28)),
                
                TrayStatus.Processing => isDarkTheme
                    ? (Color.FromArgb(255, 152, 0), Color.FromArgb(255, 204, 128), Color.FromArgb(245, 124, 0))
                    : (Color.FromArgb(245, 124, 0), Color.FromArgb(255, 152, 0), Color.FromArgb(230, 81, 0)),
                
                TrayStatus.Error => isDarkTheme
                    ? (Color.FromArgb(220, 53, 69), Color.FromArgb(255, 205, 210), Color.FromArgb(176, 0, 32))
                    : (Color.FromArgb(176, 0, 32), Color.FromArgb(220, 53, 69), Color.FromArgb(136, 14, 79)),
                
                TrayStatus.Offline => isDarkTheme
                    ? (Color.FromArgb(158, 158, 158), Color.FromArgb(189, 189, 189), Color.FromArgb(117, 117, 117))
                    : (Color.FromArgb(117, 117, 117), Color.FromArgb(158, 158, 158), Color.FromArgb(97, 97, 97)),
                
                _ => isDarkTheme
                    ? (Color.FromArgb(128, 128, 128), Color.FromArgb(160, 160, 160), Color.FromArgb(100, 100, 100))
                    : (Color.FromArgb(100, 100, 100), Color.FromArgb(128, 128, 128), Color.FromArgb(80, 80, 80))
            };
        }

        private void DrawWhisperKeyIcon(Graphics graphics, Color primary, Color secondary, Color accent)
        {
            // Draw a distinctive sound wave / "W" shape that represents WhisperKey
            // This is a unique, memorable design unlike generic microphone icons
            
            using (var primaryBrush = new SolidBrush(primary))
            using (var secondaryBrush = new SolidBrush(secondary))
            using (var accentBrush = new SolidBrush(accent))
            using (var primaryPen = new Pen(primary, 2.5f))
            using (var secondaryPen = new Pen(secondary, 2f))
            {
                // Main "W" shape - stylized sound wave
                // Center the design in the 32x32 canvas
                var points = new PointF[]
                {
                    new PointF(6, 12),   // Left start
                    new PointF(10, 22),  // First valley
                    new PointF(16, 8),   // Peak center (higher for prominence)
                    new PointF(22, 22),  // Second valley
                    new PointF(26, 12)   // Right end
                };
                
                // Draw the main wave with rounded caps
                primaryPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                primaryPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                primaryPen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                
                // Draw thick wave line
                graphics.DrawCurve(primaryPen, points, 0.3f);
                
                // Add subtle echo/secondary wave behind
                var echoPoints = new PointF[]
                {
                    new PointF(6, 16),
                    new PointF(11, 24),
                    new PointF(16, 12),
                    new PointF(21, 24),
                    new PointF(26, 16)
                };
                
                secondaryPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                secondaryPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                graphics.DrawCurve(secondaryPen, echoPoints, 0.3f);
                
                // Add small accent dots at wave peaks (sound pulses)
                graphics.FillEllipse(accentBrush, 14, 6, 4, 4);   // Center peak dot
                graphics.FillEllipse(accentBrush, 8, 20, 3, 3);   // Left valley dot
                graphics.FillEllipse(accentBrush, 21, 20, 3, 3);  // Right valley dot
            }
        }

        private void DrawStatusIndicator(Graphics graphics, TrayStatus status, Color accentColor)
        {
            using (var indicatorBrush = new SolidBrush(accentColor))
            using (var whiteBrush = new SolidBrush(Color.White))
            using (var indicatorPen = new Pen(indicatorBrush, 1.5f))
            {
                // Position indicators in the bottom-right corner
                var indicatorX = 24;
                var indicatorY = 24;
                var indicatorSize = 7;
                
                switch (status)
                {
                    case TrayStatus.Recording:
                        // Recording: Solid red circle with inner white dot
                        graphics.FillEllipse(indicatorBrush, indicatorX, indicatorY, indicatorSize, indicatorSize);
                        graphics.FillEllipse(whiteBrush, indicatorX + 2, indicatorY + 2, 3, 3);
                        break;
                    
                    case TrayStatus.Processing:
                        // Processing: Small rotating-like indicator (dashed circle effect)
                        graphics.DrawArc(indicatorPen, indicatorX, indicatorY, indicatorSize, indicatorSize, 0, 270);
                        graphics.FillEllipse(indicatorBrush, indicatorX + 2, indicatorY + 1, 3, 3);
                        break;
                    
                    case TrayStatus.Error:
                        // Error: Small X mark
                        var x1 = indicatorX + 1;
                        var y1 = indicatorY + 1;
                        var x2 = indicatorX + indicatorSize - 1;
                        var y2 = indicatorY + indicatorSize - 1;
                        graphics.DrawLine(indicatorPen, x1, y1, x2, y2);
                        graphics.DrawLine(indicatorPen, x2, y1, x1, y2);
                        break;
                    
                    case TrayStatus.Ready:
                        // Ready: Small checkmark or dot
                        graphics.FillEllipse(indicatorBrush, indicatorX + 2, indicatorY + 2, 3, 3);
                        break;
                    
                    case TrayStatus.Idle:
                        // Idle: Hollow circle
                        graphics.DrawEllipse(indicatorPen, indicatorX + 1, indicatorY + 1, indicatorSize - 2, indicatorSize - 2);
                        break;
                    
                    case TrayStatus.Offline:
                        // Offline: Small dash/line
                        graphics.DrawLine(indicatorPen, indicatorX + 1, indicatorY + 3, indicatorX + indicatorSize - 1, indicatorY + 3);
                        break;
                }
            }
        }

        private void CreateContextMenu()
        {
            if (_notifyIcon == null)
                return;

            var contextMenu = new ContextMenuStrip();

            // Status item (disabled, shows current status)
            var statusItem = new ToolStripMenuItem
            {
                Text = $"Status: {_currentStatus}",
                Enabled = false
            };
            contextMenu.Items.Add(statusItem);

            // Separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // Start/Stop Dictation item
            var dictationItem = new ToolStripMenuItem
            {
                Text = _isDictating ? "Stop Dictation" : "Start Dictation"
            };
            dictationItem.Click += OnDictationClick;
            contextMenu.Items.Add(dictationItem);

            // Show/Hide Window item
            var windowItem = new ToolStripMenuItem
            {
                Text = "Show Window"
            };
            windowItem.Click += OnWindowClick;
            contextMenu.Items.Add(windowItem);

            // Separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // Settings item
            var settingsItem = new ToolStripMenuItem
            {
                Text = "Settings"
            };
            settingsItem.Click += OnSettingsClick;
            contextMenu.Items.Add(settingsItem);

            // Help item
            var helpItem = new ToolStripMenuItem
            {
                Text = "Help & Documentation"
            };
            helpItem.Click += OnHelpClick;
            contextMenu.Items.Add(helpItem);

            // Separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // Exit item
            var exitItem = new ToolStripMenuItem
            {
                Text = "Exit Application"
            };
            exitItem.Click += OnExitClick;
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void OnNotifyIconClick(object? sender, MouseEventArgs e)
        {
            // Handle left-click - toggle window visibility
            if (e.Button == MouseButtons.Left)
            {
                WindowToggleRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnNotifyIconDoubleClick(object? sender, MouseEventArgs e)
        {
            // Handle double-click - show settings
            if (e.Button == MouseButtons.Left)
            {
                SettingsRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnDictationClick(object? sender, EventArgs e)
        {
            ToggleDictation();
        }

        private void OnWindowClick(object? sender, EventArgs e)
        {
            WindowToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnHelpClick(object? sender, EventArgs e)
        {
            // Open help documentation or show about dialog
            ShowNotification("Help documentation coming soon!", "Help");
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ToggleDictation()
        {
            if (_isDictating)
            {
                StopDictationRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StartDictationRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void UpdateDictationStatus(bool isDictating)
        {
            _isDictating = isDictating;

            // Update status based on dictation state
            var newStatus = isDictating ? TrayStatus.Recording : TrayStatus.Ready;
            UpdateStatus(newStatus);
        }

        public void UpdateFromFeedbackService(IFeedbackService.DictationStatus status)
        {
            var newStatus = status switch
            {
                IFeedbackService.DictationStatus.Idle => TrayStatus.Idle,
                IFeedbackService.DictationStatus.Ready => TrayStatus.Ready,
                IFeedbackService.DictationStatus.Recording => TrayStatus.Recording,
                IFeedbackService.DictationStatus.Processing => TrayStatus.Processing,
                IFeedbackService.DictationStatus.Complete => TrayStatus.Ready, // Complete transitions back to Ready
                IFeedbackService.DictationStatus.Error => TrayStatus.Error,
                _ => TrayStatus.Idle
            };

            // Update dictation state for internal tracking
            _isDictating = (status == IFeedbackService.DictationStatus.Recording);
            
            UpdateStatus(newStatus);
        }

        public void ShowEnhancedNotification(string title, string message, string? iconEmoji = null)
        {
            var enhancedMessage = iconEmoji != null ? $"{iconEmoji} {message}" : message;
            ShowNotification(enhancedMessage, title);
        }

        public void UpdateStatus(TrayStatus status)
        {
            if (_currentStatus == status)
                return; // No change needed

            var oldStatus = _currentStatus;
            _currentStatus = status;
            _lastStatusChange = DateTime.Now;

            // Update status message (optimized with pre-built messages)
            _statusMessage = GetStatusMessage(status);

            if (_notifyIcon != null)
            {
                // Update icon efficiently with theme
                if (_statusIcons.TryGetValue((status, _currentTheme), out var icon))
                {
                    _notifyIcon.Icon = icon;
                }

                // Update tooltip with status and timing info (optimized string formatting)
                var timeSinceChange = DateTime.Now - _lastStatusChange;
                var tooltip = $"{_statusMessage}\nStatus for: {timeSinceChange:mm\\:ss}";
                _notifyIcon.Text = tooltip.Trim();
            }

            // Update context menu (only if needed)
            UpdateContextMenuEfficiently();

            // Show notification for important status changes
            ShowStatusChangeNotification(oldStatus, status);

            // Trigger status changed event
            StatusChanged?.Invoke(this, status);
        }

        private string GetStatusMessage(TrayStatus status)
        {
            return status switch
            {
                TrayStatus.Idle => "WhisperKey - Idle",
                TrayStatus.Ready => "WhisperKey - Ready",
                TrayStatus.Recording => "WhisperKey - Recording",
                TrayStatus.Processing => "WhisperKey - Processing",
                TrayStatus.Error => "WhisperKey - Error",
                TrayStatus.Offline => "WhisperKey - Offline",
                _ => "WhisperKey - Unknown"
            };
        }

        private void UpdateContextMenuEfficiently()
        {
            if (_notifyIcon?.ContextMenuStrip == null || _isCleaningUp)
                return;

            // Find and update only the status menu item and dictation item
            foreach (ToolStripItem item in _notifyIcon.ContextMenuStrip.Items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    var text = menuItem.Text;
                    if (text.StartsWith("Status:") && text != $"Status: {_currentStatus}")
                    {
                        menuItem.Text = $"Status: {_currentStatus}";
                    }
                    else if ((text == "Start Dictation" || text == "Stop Dictation") && 
                             text != (_isDictating ? "Stop Dictation" : "Start Dictation"))
                    {
                        menuItem.Text = _isDictating ? "Stop Dictation" : "Start Dictation";
                    }
                }
            }
        }

        private void UpdateContextMenu()
        {
            if (_notifyIcon?.ContextMenuStrip == null)
                return;

            // Find and update status menu item
            foreach (ToolStripItem item in _notifyIcon.ContextMenuStrip.Items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    if (menuItem.Text.StartsWith("Status:"))
                    {
                        menuItem.Text = $"Status: {_currentStatus}";
                    }
                    else if (menuItem.Text == "Start Dictation" || menuItem.Text == "Stop Dictation")
                    {
                        menuItem.Text = _isDictating ? "Stop Dictation" : "Start Dictation";
                    }
                    else if (menuItem.Text == "Show Window")
                    {
                        // This will be handled by MainWindow state tracking
                        // Could be enhanced to show current visibility state
                    }
                }
            }
        }

        private void ShowStatusChangeNotification(TrayStatus oldStatus, TrayStatus newStatus)
        {
            // Only show notifications for important status changes
            switch (oldStatus, newStatus)
            {
                case (TrayStatus.Idle, TrayStatus.Ready):
                    ShowNotification("WhisperKey is ready to use", "Ready");
                    break;
                
                case (TrayStatus.Ready, TrayStatus.Recording):
                    ShowNotification("Recording started", "Dictation");
                    break;
                
                case (TrayStatus.Recording, TrayStatus.Processing):
                    ShowNotification("Processing transcription...", "Processing");
                    break;
                
                case (TrayStatus.Processing, TrayStatus.Ready):
                    ShowNotification("Transcription completed", "Complete");
                    break;
                
                case (_, TrayStatus.Error):
                    ShowNotification("An error occurred. Check the application.", "Error");
                    break;
                
                case (TrayStatus.Error, TrayStatus.Ready):
                    ShowNotification("Error resolved. Ready to continue.", "Recovered");
                    break;
            }
        }

        public void ShowBalloonTip(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            if (_notifyIcon != null && !_isDisposed)
            {
                try
                {
                    _notifyIcon.ShowBalloonTip(3000, title, message, icon);
                    System.Diagnostics.Debug.WriteLine($"Balloon tip shown: {title} - {message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to show balloon tip: {ex.Message}");
                }
            }
        }

        public void ShowNotification(string message, string title = "WhisperKey", int durationMs = 5000)
        {
            if (_notifyIcon != null && !_isDisposed)
            {
                lock (_lockObject)
                {
                    // Limit notification queue to prevent overwhelming the user
                    if (_notificationQueue > 5)
                    {
                        System.Diagnostics.Debug.WriteLine("Notification queue full, skipping notification");
                        return;
                    }
                    
                    _notificationQueue++;
                }

                try
                {
                    // Determine icon efficiently
                    var icon = GetNotificationIcon(title);
                    
                    _notifyIcon.ShowBalloonTip(durationMs, title, message, icon);
                    System.Diagnostics.Debug.WriteLine($"Notification shown: {title} - {message}");
                    
                    // Auto-decrement notification queue after reasonable time
                    Task.Delay(durationMs).ContinueWith(_ => 
                    {
                        lock (_lockObject)
                        {
                            _notificationQueue = Math.Max(0, _notificationQueue - 1);
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to show notification: {ex.Message}");
                    lock (_lockObject)
                    {
                        _notificationQueue = Math.Max(0, _notificationQueue - 1);
                    }
                }
            }
        }

        private ToolTipIcon GetNotificationIcon(string title)
        {
            // Efficient icon determination with minimal string operations
            if (title.Contains("Error"))
                return ToolTipIcon.Error;
            if (title.Contains("Warning"))
                return ToolTipIcon.Warning;
            return ToolTipIcon.Info;
        }

        private void OnMemoryMonitorTimer(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_isDisposed || _isCleaningUp)
                return;

            try
            {
                var currentMemory = GC.GetTotalMemory(false);
                var memoryIncrease = currentMemory - _lastGcMemory;
                
                // If memory increased significantly, trigger cleanup
                if (memoryIncrease > 5 * 1024 * 1024) // 5MB threshold
                {
                    PerformMemoryCleanup();
                }
                
                _lastGcMemory = currentMemory;
                System.Diagnostics.Debug.WriteLine($"Memory monitor: {currentMemory / 1024.0 / 1024.0:F2}MB (increase: {memoryIncrease / 1024.0 / 1024.0:F2}MB)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Memory monitor error: {ex.Message}");
            }
        }

        private void PerformMemoryCleanup()
        {
            if (_isCleaningUp)
                return;

            lock (_lockObject)
            {
                if (_isCleaningUp)
                    return;
                
                _isCleaningUp = true;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("Performing system tray memory cleanup...");
                
                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // Clean up any unused status icons
                var currentKey = (_currentStatus, _currentTheme);
                foreach (var kvp in _statusIcons.ToList())
                {
                    if (!kvp.Key.Equals(currentKey) && kvp.Value != null)
                    {
                        // Only dispose icons that aren't currently in use
                        // Note: We keep all icons for reuse, so no disposal here
                    }
                }
                
                // Clean up notification queue
                if (_notificationQueue > 10)
                {
                    _notificationQueue = 0;
                }
                
                System.Diagnostics.Debug.WriteLine("System tray memory cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Memory cleanup error: {ex.Message}");
            }
            finally
            {
                _isCleaningUp = false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            // Stop memory monitor timer
            _memoryMonitorTimer?.Stop();
            _memoryMonitorTimer?.Dispose();

            // Hide icon before disposal
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                
                // Dispose context menu first
                _notifyIcon.ContextMenuStrip?.Dispose();
                
                // Dispose all status icons
                foreach (var icon in _statusIcons.Values)
                {
                    icon?.Dispose();
                }
                _statusIcons.Clear();

                // Dispose notify icon
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            System.Diagnostics.Debug.WriteLine("SystemTrayService disposed successfully");
        }
    }
}
