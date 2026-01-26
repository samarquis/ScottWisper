using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.IO;

namespace ScottWisper
{
    /// <summary>
    /// System tray service for background application management
    /// </summary>
    public class SystemTrayService : IDisposable
    {
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isDisposed = false;
        private bool _isDictating = false;

        public event EventHandler? StartDictationRequested;
        public event EventHandler? StopDictationRequested;
        public event EventHandler? SettingsRequested;
        public event EventHandler? ExitRequested;

        public void Initialize()
        {
            if (_notifyIcon != null)
                return;

            // Create notify icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = CreateApplicationIcon(),
                Text = "ScottWisper - Ready"
            };

            // Create context menu
            CreateContextMenu();

            // Show icon
            _notifyIcon.Visible = true;

            // Handle mouse events
            _notifyIcon.MouseClick += OnNotifyIconClick;
        }

        private Icon CreateApplicationIcon()
        {
            // Create a professional 16x16 microphone icon
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                // Set high quality rendering
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                
                // Professional microphone design
                using (var brush = new SolidBrush(Color.FromArgb(51, 51, 51)))
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

                // Add subtle highlight for depth
                using (var highlightBrush = new SolidBrush(Color.FromArgb(100, 100, 100)))
                {
                    graphics.FillEllipse(highlightBrush, 7, 4, 2, 1);
                }
            }

            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void CreateContextMenu()
        {
            if (_notifyIcon == null)
                return;

            var contextMenu = new ContextMenuStrip();

            // Start/Stop Dictation item
            var dictationItem = new ToolStripMenuItem
            {
                Text = "Start Dictation"
            };
            dictationItem.Click += OnDictationClick;
            contextMenu.Items.Add(dictationItem);

            // Separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // Settings item
            var settingsItem = new ToolStripMenuItem
            {
                Text = "Settings"
            };
            settingsItem.Click += OnSettingsClick;
            contextMenu.Items.Add(settingsItem);

            // Separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // Exit item
            var exitItem = new ToolStripMenuItem
            {
                Text = "Exit"
            };
            exitItem.Click += OnExitClick;
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void OnNotifyIconClick(object? sender, MouseEventArgs e)
        {
            // Handle left-click - could show/hide a main window or status
            // For now, toggle dictation on left-click
            ToggleDictation();
        }

        private void OnDictationClick(object? sender, EventArgs e)
        {
            ToggleDictation();
        }

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
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

            if (_notifyIcon != null)
            {
                // Update tooltip
                _notifyIcon.Text = isDictating 
                    ? "ScottWisper - Recording" 
                    : "ScottWisper - Ready";

                // Update context menu
                UpdateContextMenu();
            }
        }

        private void UpdateContextMenu()
        {
            if (_notifyIcon?.ContextMenuStrip == null)
                return;

            // Find and update dictation menu item
            foreach (ToolStripItem item in _notifyIcon.ContextMenuStrip.Items)
            {
                if (item is ToolStripMenuItem menuItem && 
                    (menuItem.Text == "Start Dictation" || 
                     menuItem.Text == "Stop Dictation"))
                {
                    menuItem.Text = _isDictating ? "Stop Dictation" : "Start Dictation";
                    break;
                }
            }
        }

        public void ShowBalloonTip(string title, string message)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
            }
        }

        public void ShowNotification(string message, string title = "ScottWisper")
        {
            if (_notifyIcon != null)
            {
                // Determine icon based on title content
                var icon = title.Contains("Error") ? ToolTipIcon.Error : 
                           title.Contains("Warning") ? ToolTipIcon.Warning : 
                           ToolTipIcon.Info;
                
                _notifyIcon.ShowBalloonTip(5000, title, message, icon);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}