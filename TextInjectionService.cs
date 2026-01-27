using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using ScottWisper.Services;

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
        private readonly ISettingsService? _settingsService;
        private readonly List<InjectionAttempt> _injectionHistory = new();
        private readonly Stopwatch _performanceStopwatch = new();
        private bool _debugMode = false;
        
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

        public TextInjectionService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            
            // Subscribe to settings changes
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged += OnSettingsChanged;
            }
        }

        /// <summary>
        /// Records injection attempt for performance monitoring
        /// </summary>
        private void RecordInjectionAttempt(string text, bool success, InjectionMethod method, TimeSpan duration)
        {
            var attempt = new InjectionAttempt
            {
                Timestamp = DateTime.Now,
                Text = text,
                Success = success,
                Method = method,
                Duration = duration,
                ApplicationInfo = GetCurrentWindowInfo()
            };

            _injectionHistory.Add(attempt);

            // Keep only last 100 attempts
            if (_injectionHistory.Count > 100)
            {
                _injectionHistory.RemoveAt(0);
            }

            if (_debugMode)
            {
                System.Diagnostics.Debug.WriteLine($"Injection Attempt: {method} {(success ? "SUCCESS" : "FAILED")} in {duration.TotalMilliseconds}ms for {attempt.ApplicationInfo.ProcessName}");
            }
        }

        /// <summary>
        /// Get injection performance metrics
        /// </summary>
        public InjectionMetrics GetPerformanceMetrics()
        {
            var recentAttempts = _injectionHistory
                .Where(a => a.Timestamp > DateTime.Now.AddMinutes(-5))
                .ToList();

            if (recentAttempts.Count == 0)
                return new InjectionMetrics { AverageLatency = TimeSpan.Zero, SuccessRate = 0, TotalAttempts = 0 };

            var successfulAttempts = recentAttempts.Where(a => a.Success).ToList();
            var averageLatency = successfulAttempts.Any() 
                ? TimeSpan.FromTicks((long)successfulAttempts.Average(a => a.Duration.Ticks))
                : TimeSpan.Zero;

            return new InjectionMetrics
            {
                AverageLatency = averageLatency,
                SuccessRate = (double)successfulAttempts.Count / recentAttempts.Count,
                TotalAttempts = recentAttempts.Count,
                RecentFailures = recentAttempts.Where(a => !a.Success).Take(5).ToList()
            };
        }

        /// <summary>
        /// Enable or disable debug mode for troubleshooting
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            _debugMode = enabled;
            System.Diagnostics.Debug.WriteLine($"TextInjection debug mode: {(enabled ? "ENABLED" : "DISABLED")}");
        }

        /// <summary>
        /// Test injection functionality in current application
        /// </summary>
        public async Task<InjectionTestResult> TestInjectionAsync()
        {
            var testText = "ScottWisper Test Injection - " + DateTime.Now.ToString("HH:mm:ss");
            var compatibility = GetApplicationCompatibility();
            
            var stopwatch = Stopwatch.StartNew();
            var success = await InjectTextAsync(testText, new InjectionOptions 
            { 
                UseClipboardFallback = true,
                RetryCount = 1,
                DelayBetweenCharsMs = 10
            });
            stopwatch.Stop();

            return new InjectionTestResult
            {
                Success = success,
                TestText = testText,
                Duration = stopwatch.Elapsed,
                ApplicationInfo = GetCurrentWindowInfo(),
                Compatibility = compatibility,
                MethodUsed = success ? "Primary" : "Fallback"
            };
        }

        /// <summary>
        /// Injects text at the current cursor position with enhanced compatibility and fallback handling
        /// </summary>
        public async Task<bool> InjectTextAsync(string text, InjectionOptions? options = null)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            if (_disposed)
                throw new ObjectDisposedException(nameof(TextInjectionService));

            options ??= new InjectionOptions();
            var stopwatch = Stopwatch.StartNew();

            lock (_lockObject)
            {
                if (!_isInitialized)
                {
                    throw new InvalidOperationException("TextInjectionService not initialized. Call InitializeAsync first.");
                }
            }

            // Get application compatibility information
            var compatibility = GetApplicationCompatibility();
            if (!compatibility.IsCompatible)
            {
                System.Diagnostics.Debug.WriteLine($"Application {compatibility.Category} not compatible for text injection");
                return false;
            }

            // Verify we have an active window to inject into
            if (!HasActiveWindow())
            {
                return false;
            }

            // Try different injection methods with retry logic
            var attempts = 0;
            var maxAttempts = options.RetryCount;
            InjectionMethod methodUsed = InjectionMethod.SendInput;

            while (attempts <= maxAttempts)
            {
                try
                {
                    bool success = false;

                    // Method 1: Windows API SendInput (primary method)
                    if (compatibility.PreferredMethod == InjectionMethod.SendInput)
                    {
                        success = TrySendInput(text, options, compatibility);
                        methodUsed = InjectionMethod.SendInput;
                    }
                    
                    // Method 2: Clipboard-based injection (fallback for Office apps or preferred)
                    if (!success && (options.UseClipboardFallback || compatibility.PreferredMethod == InjectionMethod.ClipboardFallback))
                    {
                        success = await TryClipboardInjectionAsync(text, options);
                        methodUsed = InjectionMethod.ClipboardFallback;
                    }

                    // Method 3: Fallback handling with compatibility adjustments
                    if (!success && compatibility.RequiresSpecialHandling.Length > 0)
                    {
                        success = await TryCompatibilityInjectionAsync(text, options, compatibility);
                        methodUsed = InjectionMethod.SendKeys; // Special handling
                    }

                    // Record attempt
                    stopwatch.Stop();
                    RecordInjectionAttempt(text, success, methodUsed, stopwatch.Elapsed);
                    stopwatch.Restart();

                    if (success) 
                    {
                        if (_debugMode)
                        {
                            System.Diagnostics.Debug.WriteLine($"Text injection successful using {methodUsed} for {compatibility.Category} in {stopwatch.ElapsedMilliseconds}ms");
                        }
                        return true;
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

        private async void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            // Handle text injection settings changes
            if (e.Category == "TextInjection" || e.Category == "UI")
            {
                // Settings like injection method, retry count, etc. would be applied here
                await Task.CompletedTask;
            }
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
        /// Try injection using Windows API SendInput
        /// </summary>
        private bool TrySendInput(string text, InjectionOptions options, ApplicationCompatibility? compatibility = null)
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
                        // Unicode character support - handle special cases based on application compatibility
                        if (compatibility?.RequiresSpecialHandling.Contains("unicode") == true)
                        {
                            // Use safer Unicode injection for applications that need it
                            inputs.Add(CreateUnicodeInput(c));
                        }
                        else
                        {
                            // Standard Unicode injection
                            inputs.Add(CreateUnicodeInput(c));
                        }
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
        /// Try application-specific compatibility injection
        /// </summary>
        private async Task<bool> TryCompatibilityInjectionAsync(string text, InjectionOptions options, ApplicationCompatibility compatibility)
        {
            try
            {
                var inputs = new List<INPUT>();
                
                foreach (char c in text)
                {
                    if (compatibility.RequiresSpecialHandling.Contains("unicode"))
                    {
                        // Use Unicode input with compatibility delays
                        inputs.Add(CreateUnicodeInput(c));
                        await Task.Delay(options.DelayBetweenCharsMs * 2); // Slower for compatibility
                    }
                    else if (compatibility.RequiresSpecialHandling.Contains("newline") && c == '\n')
                    {
                        // Special newline handling for certain applications
                        inputs.Add(CreateKeyDownInput(VK_RETURN));
                        inputs.Add(CreateKeyUpInput(VK_RETURN));
                        await Task.Delay(50); // Extra delay for newlines
                    }
                    else if (compatibility.RequiresSpecialHandling.Contains("tab") && c == '\t')
                    {
                        // Special tab handling for IDEs and editors
                        inputs.Add(CreateKeyDownInput(VK_TAB));
                        inputs.Add(CreateKeyUpInput(VK_TAB));
                        await Task.Delay(30); // Faster for tabs
                    }
                    else if (compatibility.RequiresSpecialHandling.Contains("syntax_chars") && IsSyntaxCharacter(c))
                    {
                        // Special handling for syntax-sensitive characters in code editors
                        inputs.Add(CreateUnicodeInput(c));
                        await Task.Delay(options.DelayBetweenCharsMs * 3); // Much slower for syntax
                    }
                    else
                    {
                        // Standard injection
                        inputs.Add(CreateUnicodeInput(c));
                        await Task.Delay(options.DelayBetweenCharsMs);
                    }
                }

                // Send all inputs
                if (inputs.Count > 0)
                {
                    var result = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
                    return result == inputs.Count;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Compatibility injection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if character needs special handling in code editors
        /// </summary>
        private bool IsSyntaxCharacter(char c)
        {
            return c is '{' or c is '}' or c is '[' or c is ']' || c is '(' || c is ')' || c is '<' || c is '>';
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
        /// Get application compatibility profile for the current window
        /// </summary>
        public ApplicationCompatibility GetApplicationCompatibility()
        {
            var windowInfo = GetCurrentWindowInfo();
            if (!windowInfo.HasFocus || string.IsNullOrEmpty(windowInfo.ProcessName))
                return new ApplicationCompatibility { Category = ApplicationCategory.Unknown, IsCompatible = false };

            var processName = windowInfo.ProcessName.ToLowerInvariant();

            // Browsers
            if (processName.Contains("chrome") || processName.Contains("firefox") || 
                processName.Contains("msedge") || processName.Contains("opera"))
            {
                return new ApplicationCompatibility 
                { 
                    Category = ApplicationCategory.Browser,
                    IsCompatible = true,
                    PreferredMethod = InjectionMethod.SendInput,
                    RequiresSpecialHandling = new[] { "unicode", "newline" }
                };
            }

            // Development tools
            if (processName.Contains("devenv") || processName.Contains("code") || 
                processName.Contains("sublime") || processName.Contains("notepad++"))
            {
                return new ApplicationCompatibility 
                { 
                    Category = ApplicationCategory.DevelopmentTool,
                    IsCompatible = true,
                    PreferredMethod = InjectionMethod.SendInput,
                    RequiresSpecialHandling = new[] { "unicode", "tab", "syntax_chars" }
                };
            }

            // Office applications
            if (processName.Contains("winword") || processName.Contains("excel") || 
                processName.Contains("powerpnt") || processName.Contains("outlook"))
            {
                return new ApplicationCompatibility 
                { 
                    Category = ApplicationCategory.Office,
                    IsCompatible = true,
                    PreferredMethod = InjectionMethod.ClipboardFallback,
                    RequiresSpecialHandling = new[] { "formatting", "unicode", "newline" }
                };
            }

            // Communication tools
            if (processName.Contains("slack") || processName.Contains("discord") || 
                processName.Contains("teams") || processName.Contains("zoom"))
            {
                return new ApplicationCompatibility 
                { 
                    Category = ApplicationCategory.Communication,
                    IsCompatible = true,
                    PreferredMethod = InjectionMethod.SendInput,
                    RequiresSpecialHandling = new[] { "newline", "emoji" }
                };
            }

            // Text editors
            if (processName.Contains("notepad") || processName.Contains("wordpad") || 
                processName.Contains("write"))
            {
                return new ApplicationCompatibility 
                { 
                    Category = ApplicationCategory.TextEditor,
                    IsCompatible = true,
                    PreferredMethod = InjectionMethod.SendInput,
                    RequiresSpecialHandling = new[] { "unicode", "newline", "tab" }
                };
            }

            // Default compatibility
            return new ApplicationCompatibility 
            { 
                Category = ApplicationCategory.Unknown,
                IsCompatible = IsInjectionCompatible(),
                PreferredMethod = InjectionMethod.SendInput,
                RequiresSpecialHandling = new string[0]
            };
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

    /// <summary>
    /// Application compatibility information
    /// </summary>
    public class ApplicationCompatibility
    {
        public ApplicationCategory Category { get; set; }
        public bool IsCompatible { get; set; }
        public InjectionMethod PreferredMethod { get; set; }
        public string[] RequiresSpecialHandling { get; set; } = Array.Empty<string>();
        public Dictionary<string, object> ApplicationSettings { get; set; } = new();
    }

    /// <summary>
    /// Categories of applications for compatibility handling
    /// </summary>
    public enum ApplicationCategory
    {
        Unknown,
        Browser,
        DevelopmentTool,
        Office,
        Communication,
        TextEditor,
        Terminal,
        Gaming
    }

    /// <summary>
    /// Text injection methods
    /// </summary>
    public enum InjectionMethod
    {
        SendInput,
        ClipboardFallback,
        SendKeys,
        SendMessage
    }

    /// <summary>
    /// Injection attempt record for performance tracking
    /// </summary>
    public class InjectionAttempt
    {
        public DateTime Timestamp { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool Success { get; set; }
        public InjectionMethod Method { get; set; }
        public TimeSpan Duration { get; set; }
        public WindowInfo ApplicationInfo { get; set; } = new();
    }

    /// <summary>
    /// Injection performance metrics
    /// </summary>
    public class InjectionMetrics
    {
        public TimeSpan AverageLatency { get; set; }
        public double SuccessRate { get; set; }
        public int TotalAttempts { get; set; }
        public List<InjectionAttempt> RecentFailures { get; set; } = new();
    }

    /// <summary>
    /// Result of injection test
    /// </summary>
    public class InjectionTestResult
    {
        public bool Success { get; set; }
        public string TestText { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public WindowInfo ApplicationInfo { get; set; } = new();
        public ApplicationCompatibility Compatibility { get; set; } = new();
        public string MethodUsed { get; set; } = string.Empty;
        public string[] Issues { get; set; } = Array.Empty<string>();
    }
}