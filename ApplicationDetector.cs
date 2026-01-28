using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ScottWisper.Services
{
    /// <summary>
    /// Application detection and profiling service for compatibility testing
    /// Provides comprehensive application information for universal text injection
    /// </summary>
    public class ApplicationDetector
    {
        private readonly Dictionary<string, ApplicationProfile> _applicationProfiles;
        private string _lastActiveApplication = string.Empty;
        private ApplicationProfile? _currentProfile;

        public ApplicationDetector()
        {
            _applicationProfiles = new Dictionary<string, ApplicationProfile>();
            InitializeCommonProfiles();
        }

        #region Windows API Declarations

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(
            IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const int MAX_PATH = 260;

        #endregion

        /// <summary>
        /// Gets the currently active window's application profile
        /// </summary>
        public ApplicationProfile GetCurrentApplication()
        {
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return ApplicationProfile.Unknown;
            }

            GetWindowThreadProcessId(foregroundWindow, out uint processId);
            if (processId == 0)
            {
                return ApplicationProfile.Unknown;
            }

            var process = Process.GetProcessById((int)processId);
            var profile = CreateApplicationProfile(process);
            _currentProfile = profile;
            return profile;
        }

        /// <summary>
        /// Detects application changes and notifies of profile changes
        /// </summary>
        public event EventHandler<ApplicationChangedEventArgs>? ApplicationChanged;

        /// <summary>
        /// Starts monitoring application changes
        /// </summary>
        public async Task StartMonitoringAsync()
        {
            await Task.Run(async () =>
            {
                while (true)
                {
                    var currentApp = GetCurrentApplication();
                    if (currentApp.ApplicationName != _lastActiveApplication)
                    {
                        var oldApp = _lastActiveApplication;
                        _lastActiveApplication = currentApp.ApplicationName;
                        
                        ApplicationChanged?.Invoke(this, new ApplicationChangedEventArgs
                        {
                            OldApplication = oldApp,
                            NewApplication = currentApp.ApplicationName,
                            Profile = currentApp
                        });
                    }

                    await Task.Delay(500); // Check every 500ms
                }
            });
        }

        /// <summary>
        /// Gets application signature for identification
        /// </summary>
        public ApplicationSignature GetApplicationSignature(IntPtr windowHandle)
        {
            try
            {
                GetWindowThreadProcessId(windowHandle, out uint processId);
                if (processId == 0) return new ApplicationSignature();

                var process = Process.GetProcessById((int)processId);
                var signature = new ApplicationSignature
                {
                    ProcessId = processId,
                    ProcessName = process.ProcessName,
                    ExecutablePath = GetExecutablePath(process),
                    WindowTitle = GetWindowTitle(windowHandle),
                    WindowClass = GetWindowClass(windowHandle)
                };

                // Extract file version information
                try
                {
                    var fileVersionInfo = FileVersionInfo.GetVersionInfo(process.MainModule?.FileName ?? "");
                    signature.FileVersion = fileVersionInfo.FileVersion;
                    signature.ProductName = fileVersionInfo.ProductName;
                    signature.CompanyName = fileVersionInfo.CompanyName;
                }
                catch
                {
                    // Handle cases where version info cannot be accessed
                }

                return signature;
            }
            catch
            {
                return new ApplicationSignature();
            }
        }

        /// <summary>
        /// Assesses application capabilities for text injection
        /// </summary>
        public ApplicationCapabilities AssessApplicationCapabilities(ApplicationProfile profile)
        {
            var capabilities = new ApplicationCapabilities();

            switch (profile.Category)
            {
                case ApplicationCategory.WebBrowser:
                    capabilities.SupportsUnicode = true;
                    capabilities.SpecialCharacterSupport = SpecialCharacterSupport.Full;
                    capabilities.PreferredInjectionMethod = InjectionMethod.SendInput;
                    capabilities.RequiresSpecialHandling = false;
                    break;

                case ApplicationCategory.TextEditor:
                    capabilities.SupportsUnicode = true;
                    capabilities.SpecialCharacterSupport = SpecialCharacterSupport.Full;
                    capabilities.PreferredInjectionMethod = InjectionMethod.SendInput;
                    capabilities.RequiresSpecialHandling = false;
                    break;

                case ApplicationCategory.IDE:
                    capabilities.SupportsUnicode = true;
                    capabilities.SpecialCharacterSupport = SpecialCharacterSupport.Full;
                    capabilities.PreferredInjectionMethod = InjectionMethod.SendInput;
                    capabilities.RequiresSpecialHandling = true; // IDEs may have complex input handling
                    capabilities.MaxTextLength = 10000; // Larger text support
                    break;

                case ApplicationCategory.Office:
                    capabilities.SupportsUnicode = true;
                    capabilities.SpecialCharacterSupport = SpecialCharacterSupport.Full;
                    capabilities.PreferredInjectionMethod = InjectionMethod.Clipboard; // Office apps often work better with clipboard
                    capabilities.RequiresSpecialHandling = true;
                    break;

                case ApplicationCategory.Terminal:
                    capabilities.SupportsUnicode = false; // Many terminals have limited Unicode support
                    capabilities.SpecialCharacterSupport = SpecialCharacterSupport.Basic;
                    capabilities.PreferredInjectionMethod = InjectionMethod.SendInput;
                    capabilities.RequiresSpecialHandling = true;
                    break;

                default:
                    capabilities.SupportsUnicode = true;
                    capabilities.SpecialCharacterSupport = SpecialCharacterSupport.Standard;
                    capabilities.PreferredInjectionMethod = InjectionMethod.SendInput;
                    capabilities.RequiresSpecialHandling = false;
                    break;
            }

            return capabilities;
        }

        /// <summary>
        /// Categorizes application based on its characteristics
        /// </summary>
        public ApplicationCategory CategorizeApplication(string processName, string windowTitle, string executablePath)
        {
            var name = processName.ToLowerInvariant();
            var title = windowTitle.ToLowerInvariant();
            var exe = executablePath.ToLowerInvariant();

            // Web browsers
            if (name.Contains("chrome") || name.Contains("firefox") || name.Contains("msedge") || 
                name.Contains("opera") || name.Contains("brave") || name.Contains("safari"))
            {
                return ApplicationCategory.WebBrowser;
            }

            // Development tools
            if (name.Contains("devenv") || name.Contains("code") || name.Contains("idea") || 
                name.Contains("eclipse") || name.Contains("androidstudio") || name.Contains("xamarin"))
            {
                return ApplicationCategory.IDE;
            }

            // Office applications
            if (name.Contains("winword") || name.Contains("excel") || name.Contains("powerpnt") ||
                name.Contains("outlook") || name.Contains("teams") || name.Contains("onenote"))
            {
                return ApplicationCategory.Office;
            }

            // Text editors
            if (name.Contains("notepad++") || name.Contains("sublime") || name.Contains("atom") ||
                name.Contains("notepad") && !name.Contains("++"))
            {
                return ApplicationCategory.TextEditor;
            }

            // Communication tools
            if (name.Contains("slack") || name.Contains("discord") || name.Contains("telegram") ||
                name.Contains("zoom") || name.Contains("teams"))
            {
                return ApplicationCategory.Communication;
            }

            // Terminal applications
            if (name.Contains("cmd") || name.Contains("powershell") || name.Contains("wt") ||
                name.Contains("conhost") || name.Contains("terminal"))
            {
                return ApplicationCategory.Terminal;
            }

            return ApplicationCategory.Other;
        }

        private ApplicationProfile CreateApplicationProfile(Process process)
        {
            var profile = new ApplicationProfile
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                ExecutablePath = GetExecutablePath(process)
            };

            // Get window information
            var foregroundWindow = GetForegroundWindow();
            profile.WindowTitle = GetWindowTitle(foregroundWindow);
            profile.WindowClass = GetWindowClass(foregroundWindow);

            // Categorize application
            profile.Category = CategorizeApplication(
                profile.ProcessName, 
                profile.WindowTitle, 
                profile.ExecutablePath
            );

            // Check if we have a cached profile
            var profileKey = $"{profile.ProcessName}_{profile.Category}";
            if (_applicationProfiles.TryGetValue(profileKey, out var cachedProfile))
            {
                profile.IsKnownApplication = true;
                profile.Capabilities = cachedProfile.Capabilities;
                profile.CompatibilityMode = cachedProfile.CompatibilityMode;
            }
            else
            {
                profile.IsKnownApplication = false;
                profile.Capabilities = AssessApplicationCapabilities(profile);
                profile.CompatibilityMode = DetermineCompatibilityMode(profile);
            }

            return profile;
        }

        private string GetExecutablePath(Process process)
        {
            try
            {
                if (process.MainModule?.FileName != null)
                {
                    return process.MainModule.FileName;
                }

                // Fallback method
                var handle = OpenProcess(PROCESS_QUERY_INFORMATION, false, (uint)process.Id);
                if (handle != IntPtr.Zero)
                {
                    var buffer = new StringBuilder(MAX_PATH);
                    var size = MAX_PATH;
                    
                    if (QueryFullProcessImageName(handle, 0, buffer, ref size))
                    {
                        CloseHandle(handle);
                        return buffer.ToString();
                    }
                    
                    CloseHandle(handle);
                }
            }
            catch
            {
                // Handle access denied or other exceptions
            }

            return string.Empty;
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            var builder = new StringBuilder(256);
            var length = GetWindowText(hWnd, builder, builder.Capacity);
            return length > 0 ? builder.ToString() : string.Empty;
        }

        private string GetWindowClass(IntPtr hWnd)
        {
            var builder = new StringBuilder(256);
            var length = GetClassName(hWnd, builder, builder.Capacity);
            return length > 0 ? builder.ToString() : string.Empty;
        }

        private CompatibilityMode DetermineCompatibilityMode(ApplicationProfile profile)
        {
            return profile.Category switch
            {
                ApplicationCategory.WebBrowser => CompatibilityMode.Browser,
                ApplicationCategory.IDE => CompatibilityMode.IDE,
                ApplicationCategory.Office => CompatibilityMode.Office,
                ApplicationCategory.Terminal => CompatibilityMode.Terminal,
                ApplicationCategory.TextEditor => CompatibilityMode.Standard,
                ApplicationCategory.Communication => CompatibilityMode.Communication,
                _ => CompatibilityMode.Standard
            };
        }

        private void InitializeCommonProfiles()
        {
            // Chrome browser profile
            _applicationProfiles["chrome_browser"] = new ApplicationProfile
            {
                ProcessName = "chrome",
                Category = ApplicationCategory.WebBrowser,
                Capabilities = new ApplicationCapabilities
                {
                    SupportsUnicode = true,
                    SpecialCharacterSupport = SpecialCharacterSupport.Full,
                    PreferredInjectionMethod = InjectionMethod.SendInput,
                    RequiresSpecialHandling = false
                },
                CompatibilityMode = CompatibilityMode.Browser
            };

            // Visual Studio IDE profile
            _applicationProfiles["devenv_ide"] = new ApplicationProfile
            {
                ProcessName = "devenv",
                Category = ApplicationCategory.IDE,
                Capabilities = new ApplicationCapabilities
                {
                    SupportsUnicode = true,
                    SpecialCharacterSupport = SpecialCharacterSupport.Full,
                    PreferredInjectionMethod = InjectionMethod.SendInput,
                    RequiresSpecialHandling = true,
                    MaxTextLength = 10000
                },
                CompatibilityMode = CompatibilityMode.IDE
            };

            // Microsoft Word profile
            _applicationProfiles["winword_office"] = new ApplicationProfile
            {
                ProcessName = "WINWORD",
                Category = ApplicationCategory.Office,
                Capabilities = new ApplicationCapabilities
                {
                    SupportsUnicode = true,
                    SpecialCharacterSupport = SpecialCharacterSupport.Full,
                    PreferredInjectionMethod = InjectionMethod.Clipboard,
                    RequiresSpecialHandling = true
                },
                CompatibilityMode = CompatibilityMode.Office
            };

            // Notepad++ profile
            _applicationProfiles["notepad++_editor"] = new ApplicationProfile
            {
                ProcessName = "notepad++",
                Category = ApplicationCategory.TextEditor,
                Capabilities = new ApplicationCapabilities
                {
                    SupportsUnicode = true,
                    SpecialCharacterSupport = SpecialCharacterSupport.Full,
                    PreferredInjectionMethod = InjectionMethod.SendInput,
                    RequiresSpecialHandling = false
                },
                CompatibilityMode = CompatibilityMode.Standard
            };
        }
    }

    #region Data Models

    /// <summary>
    /// Application profile for text injection compatibility
    /// </summary>
    public class ApplicationProfile
    {
        public static ApplicationProfile Unknown { get; } = new ApplicationProfile 
        { 
            Category = ApplicationCategory.Unknown,
            ProcessName = "Unknown",
            WindowTitle = "Unknown",
            IsKnownApplication = false
        };

        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string WindowClass { get; set; } = string.Empty;
        public ApplicationCategory Category { get; set; }
        public ApplicationCapabilities Capabilities { get; set; } = new ApplicationCapabilities();
        public CompatibilityMode CompatibilityMode { get; set; }
        public bool IsKnownApplication { get; set; }
        public DateTime LastDetected { get; set; } = DateTime.Now;

        public string ApplicationName => $"{ProcessName} ({WindowTitle})";
    }

    /// <summary>
    /// Application signature for unique identification
    /// </summary>
    public class ApplicationSignature
    {
        public uint ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string WindowClass { get; set; } = string.Empty;
        public string FileVersion { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Application capabilities for text injection
    /// </summary>
    public class ApplicationCapabilities
    {
        public bool SupportsUnicode { get; set; } = true;
        public SpecialCharacterSupport SpecialCharacterSupport { get; set; } = SpecialCharacterSupport.Standard;
        public InjectionMethod PreferredInjectionMethod { get; set; } = InjectionMethod.SendInput;
        public bool RequiresSpecialHandling { get; set; } = false;
        public int MaxTextLength { get; set; } = 1000;
        public bool SupportsLineBreaks { get; set; } = true;
        public bool SupportsTabs { get; set; } = true;
        public int RecommendedDelayBetweenChars { get; set; } = 5;
    }

    /// <summary>
    /// Event arguments for application change events
    /// </summary>
    public class ApplicationChangedEventArgs : EventArgs
    {
        public string OldApplication { get; set; } = string.Empty;
        public string NewApplication { get; set; } = string.Empty;
        public ApplicationProfile Profile { get; set; } = new ApplicationProfile();
    }

    /// <summary>
    /// Application categories for compatibility handling
    /// </summary>
    public enum ApplicationCategory
    {
        Unknown,
        Other,
        WebBrowser,
        Browser = WebBrowser, // Alias for backward compatibility
        IDE,
        DevelopmentTool = IDE, // Alias for backward compatibility
        Office,
        TextEditor,
        Communication,
        Terminal
    }

    /// <summary>
    /// Special character support levels
    /// </summary>
    public enum SpecialCharacterSupport
    {
        Basic,      // Only ASCII characters
        Standard,   // Most common special characters
        Full        // Full Unicode support
    }

    /// <summary>
    /// Text injection methods
    /// </summary>
    public enum InjectionMethod
    {
        SendInput,
        Clipboard,
        Unicode,
        PostMessage
    }

    /// <summary>
    /// Compatibility modes for different application types
    /// </summary>
    public enum CompatibilityMode
    {
        Standard,
        Browser,
        IDE,
        Office,
        Terminal,
        Communication
    }

    #endregion
}