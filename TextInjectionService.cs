using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace ScottWisper
{
    /// <summary>
    /// Interface for text injection methods
    /// </summary>
    public interface ITextInjection
    {
        /// <summary>
        /// Injects text at the current cursor position
        /// </summary>
        Task<bool> InjectTextAsync(string text, InjectionOptions? options = null);
        
        /// <summary>
        /// Initializes the injection service
        /// </summary>
        Task<bool> InitializeAsync();
        
        /// <summary>
        /// Cleanup resources
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// Options for text injection
    /// </summary>
    public class InjectionOptions
    {
        public bool UseClipboardFallback { get; set; } = false;
        public int RetryCount { get; set; } = 3;
        public int DelayBetweenRetriesMs { get; set; } = 100;
        public int DelayBetweenCharsMs { get; set; } = 5;
        public bool RespectExistingText { get; set; } = true;
    }

    /// <summary>
    /// Universal text injection service with multiple fallback mechanisms
    /// Currently implements SendInput-based text injection with clipboard fallback
    /// </summary>
    public class TextInjectionService : ITextInjection, IDisposable
    {
        private readonly object _lockObject = new object();
        private bool _isInitialized;
        private bool _disposed;
        
        // Windows API imports for text injection
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll")]
        private static extern bool GetCaretPos(out POINT lpPoint);
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        
        // Input type constants
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        
        // Virtual key codes
        private const int VK_RETURN = 0x0D;
        private const int VK_TAB = 0x09;
        private const int VK_BACK = 0x08;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion u;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct InputUnion
        {
            public MOUSEINPUT mi;
            public KEYBDINPUT ki;
            public HARDWAREINPUT hi;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public TextInjectionService()
        {
        }

        /// <summary>
        /// Initialize the injection service
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
                return true;

            try
            {
                // Initialize Windows API-based text injection
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize TextInjectionService: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Injects text at the current cursor position with multiple fallback methods
        /// </summary>
        public async Task<bool> InjectTextAsync(string text, InjectionOptions? options = null)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            if (_disposed)
                throw new ObjectDisposedException(nameof(TextInjectionService));

            options ??= new InjectionOptions();

            lock (_lockObject)
            {
                if (!_isInitialized)
                {
                    throw new InvalidOperationException("TextInjectionService not initialized. Call InitializeAsync first.");
                }
            }

            // Verify we have an active window to inject into
            if (!HasActiveWindow())
            {
                return false;
            }

            // Try different injection methods with retry logic
            var attempts = 0;
            var maxAttempts = options.RetryCount;

            while (attempts <= maxAttempts)
            {
                try
                {
                    bool success = false;

                    // Method 1: Windows API SendInput (primary method)
                    success = TrySendInput(text, options);
                    if (success) return true;

                    // Method 2: Clipboard-based injection (fallback)
                    if (options.UseClipboardFallback)
                    {
                        success = await TryClipboardInjectionAsync(text, options);
                        if (success) return true;
                    }

                    // All methods failed
                    if (attempts == maxAttempts)
                    {
                        return false;
                    }

                    attempts++;
                    await Task.Delay(options.DelayBetweenRetriesMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Text injection attempt {attempts + 1} failed: {ex.Message}");
                    attempts++;
                    if (attempts <= maxAttempts)
                    {
                        await Task.Delay(options.DelayBetweenRetriesMs);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Try injection using Windows API SendInput
        /// </summary>
        private bool TrySendInput(string text, InjectionOptions options)
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return false;

                // Brief pause to ensure focus is established
                Thread.Sleep(10);

                // Create input array for all characters
                var inputs = new List<INPUT>();
                
                foreach (char c in text)
                {
                    if (c == '\n')
                    {
                        // Newline: Send Enter key down and up
                        inputs.Add(CreateKeyDownInput(VK_RETURN));
                        inputs.Add(CreateKeyUpInput(VK_RETURN));
                    }
                    else if (c == '\t')
                    {
                        // Tab: Send Tab key down and up
                        inputs.Add(CreateKeyDownInput(VK_TAB));
                        inputs.Add(CreateKeyUpInput(VK_TAB));
                    }
                    else if (c == '\b')
                    {
                        // Backspace: Send Back key down and up
                        inputs.Add(CreateKeyDownInput(VK_BACK));
                        inputs.Add(CreateKeyUpInput(VK_BACK));
                    }
                    else
                    {
                        // Unicode character support
                        inputs.Add(CreateUnicodeInput(c));
                    }
                    
                    Thread.Sleep(options.DelayBetweenCharsMs);
                }

                // Send all inputs at once
                if (inputs.Count > 0)
                {
                    var result = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
                    return result == inputs.Count; // Return true if all inputs were processed
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendInput failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create key down input
        /// </summary>
        private INPUT CreateKeyDownInput(int keyCode)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)keyCode,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        /// <summary>
        /// Create key up input
        /// </summary>
        private INPUT CreateKeyUpInput(int keyCode)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)keyCode,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        /// <summary>
        /// Create Unicode character input
        /// </summary>
        private INPUT CreateUnicodeInput(char character)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = (ushort)character,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        /// <summary>
        /// Try clipboard-based injection (Ctrl+V)
        /// </summary>
        private async Task<bool> TryClipboardInjectionAsync(string text, InjectionOptions options)
        {
            try
            {
                // Save current clipboard content
                var originalClipboard = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
                
                // Set our text to clipboard
                Clipboard.SetText(text);
                
                // Small delay to ensure clipboard is set
                await Task.Delay(50);
                
                // Simulate Ctrl+V using SendInput
                var inputs = new INPUT[]
                {
                    CreateKeyDownInput(VK_CONTROL),
                    CreateKeyDownInput(0x56), // V key
                    CreateKeyUpInput(0x56),     // V key
                    CreateKeyUpInput(VK_CONTROL)
                };
                
                var result = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
                
                // Wait for paste operation
                await Task.Delay(100);
                
                // Restore original clipboard content
                if (!string.IsNullOrEmpty(originalClipboard))
                {
                    Clipboard.SetText(originalClipboard);
                }
                else
                {
                    Clipboard.Clear();
                }
                
                return result == inputs.Length;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard injection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if there's an active window to inject into
        /// </summary>
        private bool HasActiveWindow()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                return foregroundWindow != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get information about the current foreground window
        /// </summary>
        public WindowInfo GetCurrentWindowInfo()
        {
            try
            {
                var hWnd = GetForegroundWindow();
                GetWindowRect(hWnd, out var rect);
                GetWindowThreadProcessId(hWnd, out var processId);
                
                var process = Process.GetProcessById((int)processId);
                
                return new WindowInfo
                {
                    Handle = hWnd,
                    ProcessName = process.ProcessName,
                    ProcessId = (int)processId,
                    WindowRect = new WindowRect
                    {
                        Left = rect.Left,
                        Top = rect.Top,
                        Right = rect.Right,
                        Bottom = rect.Bottom
                    },
                    HasFocus = hWnd != IntPtr.Zero
                };
            }
            catch
            {
                return new WindowInfo { HasFocus = false };
            }
        }

        /// <summary>
        /// Check if the current application is likely to accept text injection
        /// </summary>
        public bool IsInjectionCompatible()
        {
            var windowInfo = GetCurrentWindowInfo();
            
            if (!windowInfo.HasFocus)
                return false;

            // Check for known incompatible applications
            var incompatibleProcesses = new[]
            {
                "cmd", "powershell", "conhost", "WindowsTerminal",
                "SecurityHealthSystray", "LockApp", "ApplicationFrameHost",
                "winlogon", "dwm"
            };

            foreach (var proc in incompatibleProcesses)
            {
                if (windowInfo.ProcessName?.Contains(proc, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get the current caret position in the active window
        /// </summary>
        public CaretPosition? GetCaretPosition()
        {
            try
            {
                if (GetCaretPos(out var point))
                {
                    return new CaretPosition
                    {
                        X = point.X,
                        Y = point.Y,
                        WindowHandle = GetForegroundWindow(),
                        HasCaret = true
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get caret position: {ex.Message}");
            }

            return null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                lock (_lockObject)
                {
                    _disposed = true;
                }
            }
        }
    }

    /// <summary>
    /// Information about the current foreground window
    /// </summary>
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string? ProcessName { get; set; }
        public int ProcessId { get; set; }
        public WindowRect WindowRect { get; set; }
        public bool HasFocus { get; set; }
    }

    /// <summary>
    /// Window rectangle structure
    /// </summary>
    public class WindowRect
    {
        public int Left { get; set; } = 0;
        public int Top { get; set; } = 0;
        public int Right { get; set; } = 0;
        public int Bottom { get; set; } = 0;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    /// <summary>
    /// Caret position information
    /// </summary>
    public class CaretPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public IntPtr WindowHandle { get; set; }
        public bool HasCaret { get; set; }
    }
}