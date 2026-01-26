using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;

namespace ScottWisper
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

        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isDisposed = false;
        private bool _isDictating = false;
        private TrayStatus _currentStatus = TrayStatus.Idle;
        private readonly Dictionary<TrayStatus, Icon> _statusIcons;
        private DateTime _lastStatusChange;
        private string _statusMessage = "ScottWisper - Ready";

        public event EventHandler? StartDictationRequested;
        public event EventHandler? StopDictationRequested;
        public event EventHandler? SettingsRequested;
        public event EventHandler? WindowToggleRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler<TrayStatus>? StatusChanged;

        public SystemTrayService()
        {
            _statusIcons = new Dictionary<TrayStatus, Icon>();
            _lastStatusChange = DateTime.Now;
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
                Icon = _statusIcons[TrayStatus.Ready],
                Text = "ScottWisper - Ready"
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
        }

        private void CreateStatusIcons()
        {
            // Create different colored icons for each status
            _statusIcons[TrayStatus.Idle] = CreateStatusIcon(Color.Gray, "Idle");
            _statusIcons[TrayStatus.Ready] = CreateStatusIcon(Color.Green, "Ready");
            _statusIcons[TrayStatus.Recording] = CreateStatusIcon(Color.Red, "Recording");
            _statusIcons[TrayStatus.Processing] = CreateStatusIcon(Color.FromArgb(255, 165, 0), "Processing"); // Orange
            _statusIcons[TrayStatus.Error] = CreateStatusIcon(Color.FromArgb(220, 53, 69), "Error"); // Red
            _statusIcons[TrayStatus.Offline] = CreateStatusIcon(Color.DarkGray, "Offline");
        }

        private Icon CreateStatusIcon(Color statusColor, string status)
        {
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                // Set high quality rendering
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                
                // Professional microphone design with status color
                using (var brush = new SolidBrush(statusColor))
                using (var pen = new Pen(brush, 1))
                {
                    // Microphone head (rounded top)
                    graphics.FillEllipse(brush, 6, 3, 4, 3);
                    
                    // Microphone body
                    graphics.FillRectangle(brush, 7, 5, 2, 4);
                    
                    // Microphone base
                    graphics.FillRectangle(brush, 7, 9, 2, 2);
                    
                    // Microphone stand
                    graphics.FillRectangle(brush, 4, 11, 8, 1);
                }

                // Add status-specific details
                switch (status)
                {
                    case "Recording":
                        // Add recording indicator (pulsing red dot)
                        using (var recordingBrush = new SolidBrush(Color.White))
                        {
                            graphics.FillEllipse(recordingBrush, 12, 2, 2, 2);
                        }
                        break;
                    
                    case "Processing":
                        // Add processing indicator (small gear)
                        using (var processingBrush = new SolidBrush(Color.White))
                        {
                            graphics.FillRectangle(processingBrush, 11, 1, 3, 3);
                            graphics.FillRectangle(processingBrush, 13, 3, 3, 3);
                            graphics.FillRectangle(processingBrush, 11, 5, 3, 3);
                        }
                        break;
                    
                    case "Error":
                        // Add error indicator (X)
                        using (var errorBrush = new SolidBrush(Color.White))
                        {
                            graphics.DrawLine(new Pen(errorBrush, 1), 11, 1, 14, 4);
                            graphics.DrawLine(new Pen(errorBrush, 1), 14, 1, 11, 4);
                        }
                        break;
                }
            }

            return Icon.FromHandle(bitmap.GetHicon());
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

        public void UpdateStatus(TrayStatus status)
        {
            if (_currentStatus == status)
                return; // No change needed

            var oldStatus = _currentStatus;
            _currentStatus = status;
            _lastStatusChange = DateTime.Now;

            // Update status message
            _statusMessage = status switch
            {
                TrayStatus.Idle => "ScottWisper - Idle",
                TrayStatus.Ready => "ScottWisper - Ready",
                TrayStatus.Recording => "ScottWisper - Recording",
                TrayStatus.Processing => "ScottWisper - Processing",
                TrayStatus.Error => "ScottWisper - Error",
                TrayStatus.Offline => "ScottWisper - Offline",
                _ => "ScottWisper - Unknown"
            };

            if (_notifyIcon != null)
            {
                // Update icon
                if (_statusIcons.ContainsKey(status))
                {
                    _notifyIcon.Icon = _statusIcons[status];
                }

                // Update tooltip with status and timing info
                var timeSinceChange = DateTime.Now - _lastStatusChange;
                var tooltip = $"{_statusMessage}\nStatus for: {timeSinceChange:mm\\:ss}";
                _notifyIcon.Text = tooltip.Trim();
            }

            // Update context menu
            UpdateContextMenu();

            // Show notification for important status changes
            ShowStatusChangeNotification(oldStatus, status);

            // Trigger status changed event
            StatusChanged?.Invoke(this, status);
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
                    ShowNotification("ScottWisper is ready to use", "Ready");
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

        public void ShowNotification(string message, string title = "ScottWisper", int durationMs = 5000)
        {
            if (_notifyIcon != null && !_isDisposed)
            {
                try
                {
                    // Determine icon based on title content
                    var icon = title.Contains("Error") ? ToolTipIcon.Error : 
                               title.Contains("Warning") ? ToolTipIcon.Warning : 
                               ToolTipIcon.Info;
                    
                    _notifyIcon.ShowBalloonTip(durationMs, title, message, icon);
                    System.Diagnostics.Debug.WriteLine($"Notification shown: {title} - {message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to show notification: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

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