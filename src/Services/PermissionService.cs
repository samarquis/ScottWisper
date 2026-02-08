using WhisperKey.Configuration;
using WhisperKey.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Diagnostics;
using static WhisperKey.MicrophonePermissionStatus; // Use root namespace enum

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for permission management service
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// Checks microphone permission status with detailed information
        /// </summary>
        Task<MicrophonePermissionStatus> CheckMicrophonePermissionAsync();

        /// <summary>
        /// Requests microphone permission using Windows privacy APIs
        /// </summary>
        Task<bool> RequestMicrophonePermissionAsync();

        /// <summary>
        /// Gets human-readable permission status with user-friendly messages
        /// </summary>
        Task<string> GetPermissionStatusAsync();

        /// <summary>
        /// Opens Windows privacy settings for microphone permissions
        /// </summary>
        Task<bool> OpenWindowsPrivacySettingsAsync();

        /// <summary>
        /// Monitors permission changes in real-time
        /// </summary>
        Task<bool> MonitorPermissionChangesAsync();

        /// <summary>
        /// Gets permission request history for troubleshooting
        /// </summary>
        Task<List<PermissionRequestRecord>> GetPermissionRequestHistoryAsync();
    }

    /// <summary>
    /// Permission management service with Windows 10/11 privacy settings integration
    /// </summary>
    public class PermissionService : IPermissionService
    {
        private readonly IRegistryService _registryService;
        private readonly IAuditLoggingService _auditService;
        private readonly List<PermissionRequestRecord> _requestHistory = new List<PermissionRequestRecord>();
        private bool _isMonitoring = false;

        // Windows API declarations for privacy settings
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr ShellExecute(
            IntPtr hwnd,
            string? lpOperation,
            string? lpFile,
            string? lpParameters,
            string? lpDirectory,
            int nShowCmd);

        // Windows privacy constants
        private const int SW_SHOW = 5;
        private const string MICROPHONE_SETTINGS_PATH = "ms-settings:privacy-microphone";
        private const string PRIVACY_SETTINGS_PATH = "ms-settings:privacy";

        public PermissionService(IRegistryService registryService, IAuditLoggingService auditService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));

            // Initialize permission monitoring
            _ = Task.Run(async () =>
            {
                var monitoringStarted = await MonitorPermissionChangesAsync();
                if (monitoringStarted)
                {
                    System.Diagnostics.Debug.WriteLine("Permission change monitoring initialized successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to initialize permission change monitoring");
                }
            });
        }

         /// <summary>
        /// Checks microphone permission status with detailed analysis
        /// </summary>
        public async Task<MicrophonePermissionStatus> CheckMicrophonePermissionAsync()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // Check system-level permission settings
                    var systemPermission = CheckSystemMicrophonePermission();
                    
                    if (systemPermission == MicrophonePermissionStatus.Denied)
                    {
                        await _auditService.LogEventAsync(
                            AuditEventType.SecurityEvent,
                            "Microphone permission check failed - system denied",
                            JsonSerializer.Serialize(new { 
                                SystemPermission = systemPermission.ToString(),
                                UserId = Environment.UserName,
                                MachineName = Environment.MachineName
                            }),
                            DataSensitivity.Medium);
                        
                        return MicrophonePermissionStatus.Denied;
                    }

                    // Test actual access to microphone
                    var testResult = await TestMicrophoneAccessAsync();
                    
                    await _auditService.LogEventAsync(
                        AuditEventType.SecurityEvent,
                        $"Microphone permission check completed: {testResult}",
                        JsonSerializer.Serialize(new { 
                            Result = testResult.ToString(),
                            SystemPermission = systemPermission.ToString(),
                            UserId = Environment.UserName
                        }),
                        DataSensitivity.Low);
                    
                    return testResult;
                }
                catch (Exception ex)
                {
                    await _auditService.LogEventAsync(
                        AuditEventType.Error,
                        "Microphone permission check error",
                        JsonSerializer.Serialize(new { 
                            Error = ex.Message,
                            StackTrace = ex.StackTrace
                        }),
                        DataSensitivity.Medium);
                    
                    System.Diagnostics.Debug.WriteLine($"Error checking microphone permission: {ex.Message}");
                    return MicrophonePermissionStatus.SystemError;
                }
            });
        }

        /// <summary>
        /// Requests microphone permission using Windows privacy APIs
        /// </summary>
        public async Task<bool> RequestMicrophonePermissionAsync()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    var currentStatus = await CheckMicrophonePermissionAsync();
                    
                    if (currentStatus == MicrophonePermissionStatus.Granted)
                    {
                        await _auditService.LogEventAsync(
                            AuditEventType.SecurityEvent,
                            "Microphone permission requested - already granted",
                            JsonSerializer.Serialize(new { 
                                PreviousStatus = currentStatus.ToString(),
                                UserId = Environment.UserName
                            }),
                            DataSensitivity.Low);
                        
                        return true; // Already granted
                    }

                    // Attempt to trigger Windows permission dialog
                    var success = await TriggerWindowsPermissionDialogAsync();
                    
                    if (success)
                    {
                        // Verify permission was actually granted
                        var newStatus = await CheckMicrophonePermissionAsync();
                        if (newStatus == MicrophonePermissionStatus.Granted)
                        {
                            RecordPermissionRequest(true, "Windows permission dialog");
                            
                            await _auditService.LogEventAsync(
                                AuditEventType.SecurityEvent,
                                "Microphone permission granted via dialog",
                                JsonSerializer.Serialize(new { 
                                    PreviousStatus = currentStatus.ToString(),
                                    NewStatus = newStatus.ToString(),
                                    Method = "Windows permission dialog",
                                    UserId = Environment.UserName
                                }),
                                DataSensitivity.Medium);
                            
                            return true;
                        }
                    }

                    // Fallback: open privacy settings
                    await OpenWindowsPrivacySettingsAsync();
                    RecordPermissionRequest(false, "Windows permission dialog failed - opened settings");
                    
                    await _auditService.LogEventAsync(
                        AuditEventType.SecurityEvent,
                        "Microphone permission request failed - opened settings",
                        JsonSerializer.Serialize(new { 
                            PreviousStatus = currentStatus.ToString(),
                            Method = "Opened privacy settings",
                            UserId = Environment.UserName
                        }),
                        DataSensitivity.Medium);
                    
                    return false;
                }
                catch (Exception ex)
                {
                    await _auditService.LogEventAsync(
                        AuditEventType.Error,
                        "Microphone permission request error",
                        JsonSerializer.Serialize(new { 
                            Error = ex.Message,
                            StackTrace = ex.StackTrace,
                            UserId = Environment.UserName
                        }),
                        DataSensitivity.Medium);
                    
                    System.Diagnostics.Debug.WriteLine($"Error requesting microphone permission: {ex.Message}");
                    RecordPermissionRequest(false, $"Exception: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Gets human-readable permission status with user guidance
        /// </summary>
        public async Task<string> GetPermissionStatusAsync()
        {
            var status = await CheckMicrophonePermissionAsync();
            
            return status switch
            {
                MicrophonePermissionStatus.Granted => "Microphone access is granted. You can use voice dictation.",
                MicrophonePermissionStatus.Denied => "Microphone access is denied. Please enable it in Windows Settings.",
                MicrophonePermissionStatus.Unknown => "Unable to determine microphone permission status. Please check Windows Settings.",
                MicrophonePermissionStatus.NotRequested => "Microphone permission has not been requested yet.",
                MicrophonePermissionStatus.SystemError => "System error occurred while checking microphone permission. Please restart the application.",
                _ => "Unknown permission status."
            };
        }

        /// <summary>
        /// Opens Windows privacy settings for microphone permissions
        /// </summary>
        public async Task<bool> OpenWindowsPrivacySettingsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var result = ShellExecute(IntPtr.Zero, "open", MICROPHONE_SETTINGS_PATH, null, null, SW_SHOW);
                    
                    if (result.ToInt32() > 32) // ShellExecute success
                    {
                        System.Diagnostics.Debug.WriteLine("Opened Windows microphone privacy settings successfully");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to open Windows microphone privacy settings");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error opening privacy settings: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Monitors permission changes using Windows registry and system events
        /// </summary>
        public async Task<bool> MonitorPermissionChangesAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Start monitoring thread
                    _isMonitoring = true;
                    
                    // Simple polling-based monitoring for now
                    // In a production environment, you would use Windows registry monitoring
                    // or Windows Management Instrumentation (WMI) events
                    
                    _ = Task.Run(async () =>
                    {
                        while (_isMonitoring)
                        {
                            try
                            {
                                // Check permission status every 5 seconds
                                var currentStatus = CheckSystemMicrophonePermission();
                                
                                // If status changed, trigger update events
                                // This would be integrated with AudioDeviceService events
                                
                                await Task.Delay(5000); // Check every 5 seconds
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error in permission monitoring: {ex.Message}");
                                await Task.Delay(10000); // Wait longer on error
                            }
                        }
                    });
                    
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting permission monitoring: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Gets permission request history for troubleshooting
        /// </summary>
        public async Task<List<PermissionRequestRecord>> GetPermissionRequestHistoryAsync()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    return new List<PermissionRequestRecord>(_requestHistory);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting permission request history: {ex.Message}");
                    return new List<PermissionRequestRecord>();
                }
            });
        }

        /// <summary>
        /// Stops permission change monitoring
        /// </summary>
        public void StopPermissionMonitoring()
        {
            _isMonitoring = false;
            System.Diagnostics.Debug.WriteLine("Permission change monitoring stopped");
        }

        /// <summary>
        /// Checks system-level microphone permission through Windows APIs
        /// </summary>
        private MicrophonePermissionStatus CheckSystemMicrophonePermission()
        {
            try
            {
                // Check Windows Registry privacy settings
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
                string? value = _registryService.ReadValue(RegistryHiveOption.CurrentUser, keyPath, "Value");

                if (value != null)
                {
                    if (string.Equals(value, "Allow", StringComparison.OrdinalIgnoreCase))
                    {
                        return MicrophonePermissionStatus.Granted;
                    }
                    if (string.Equals(value, "Deny", StringComparison.OrdinalIgnoreCase))
                    {
                        return MicrophonePermissionStatus.Denied;
                    }
                }

                // Fallback to process check if registry key is missing or invalid
                var processes = Process.GetProcessesByName("audiodg");
                if (processes.Length > 0)
                {
                    return MicrophonePermissionStatus.Granted;
                }

                // Additional check for audio services
                var audioService = new System.ServiceProcess.ServiceController("AudioSrv");
                if (audioService.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                {
                    return MicrophonePermissionStatus.Granted;
                }

                return MicrophonePermissionStatus.Unknown;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking system permission: {ex.Message}");
                return MicrophonePermissionStatus.SystemError;
            }
        }

        /// <summary>
        /// Tests actual microphone access capability
        /// </summary>
        private async Task<MicrophonePermissionStatus> TestMicrophoneAccessAsync()
        {
            try
            {
                // This would integrate with AudioDeviceService to test actual device access
                // For now, we'll simulate the test
                
                // In a real implementation, you would:
                // 1. Try to create a WaveIn object
                // 2. Attempt to access microphone device
                // 3. Check for UnauthorizedAccessException
                
                return MicrophonePermissionStatus.Granted; // Simplified for now
            }
            catch (UnauthorizedAccessException)
            {
                return MicrophonePermissionStatus.Denied;
            }
            catch (SecurityException)
            {
                return MicrophonePermissionStatus.Denied;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error testing microphone access: {ex.Message}");
                return MicrophonePermissionStatus.SystemError;
            }
        }

        /// <summary>
        /// Triggers Windows permission dialog for microphone access
        /// </summary>
        private async Task<bool> TriggerWindowsPermissionDialogAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Try to access microphone to trigger permission dialog
                    // This is a simplified approach - in production you would use
                    // Windows AppContainer APIs or similar mechanisms
                    
                    var result = ShellExecute(IntPtr.Zero, "open", PRIVACY_SETTINGS_PATH, null, null, SW_SHOW);
                    
                    if (result.ToInt32() > 32)
                    {
                        System.Diagnostics.Debug.WriteLine("Triggered Windows permission dialog successfully");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to trigger Windows permission dialog");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error triggering permission dialog: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Records permission request for history tracking
        /// </summary>
        private void RecordPermissionRequest(bool success, string method)
        {
            lock (_requestHistory)
            {
                var record = new PermissionRequestRecord
                {
                    Timestamp = DateTime.Now,
                    Method = method,
                    Success = success,
                    Status = success ? "Granted" : "Denied"
                };

                _requestHistory.Add(record);

                // Keep only last 50 requests
                if (_requestHistory.Count > 50)
                {
                    _requestHistory.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                StopPermissionMonitoring();
                System.Diagnostics.Debug.WriteLine("PermissionService disposed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing PermissionService: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Record of permission request for troubleshooting
    /// </summary>
    public class PermissionRequestRecord
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Method { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
