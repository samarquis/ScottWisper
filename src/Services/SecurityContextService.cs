using System;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;
using System.Runtime.InteropServices;
using System.Management;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for security context collection service
    /// </summary>
    public interface ISecurityContextService
    {
        /// <summary>
        /// Get comprehensive security context for audit events
        /// </summary>
        Task<SecurityContext> GetSecurityContextAsync();
        
        /// <summary>
        /// Get device fingerprint for identification
        /// </summary>
        Task<string> GetDeviceFingerprintAsync();
        
        /// <summary>
        /// Get current IP address (hashed for privacy)
        /// </summary>
        Task<string?> GetHashedIpAddressAsync();
        
        /// <summary>
        /// Get geolocation information
        /// </summary>
        Task<GeoLocation?> GetGeoLocationAsync();
    }

    /// <summary>
    /// Security context information for comprehensive audit logging
    /// </summary>
    public class SecurityContext
    {
        /// <summary>
        /// Device fingerprint (hashed)
        /// </summary>
        public string DeviceFingerprint { get; set; } = string.Empty;
        
        /// <summary>
        /// IP address (hashed for privacy)
        /// </summary>
        public string? HashedIpAddress { get; set; }
        
        /// <summary>
        /// Geolocation data
        /// </summary>
        public GeoLocation? Location { get; set; }
        
        /// <summary>
        /// User agent or client information
        /// </summary>
        public string UserAgent { get; set; } = string.Empty;
        
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Process ID
        /// </summary>
        public int ProcessId { get; set; }
        
        /// <summary>
        /// Thread ID
        /// </summary>
        public int ThreadId { get; set; }
        
        /// <summary>
        /// Machine name (hashed)
        /// </summary>
        public string HashedMachineName { get; set; } = string.Empty;
        
        /// <summary>
        /// Timestamp when context was captured
        /// </summary>
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Geolocation information
    /// </summary>
    public class GeoLocation
    {
        /// <summary>
        /// Country code
        /// </summary>
        public string? CountryCode { get; set; }
        
        /// <summary>
        /// Country name
        /// </summary>
        public string? CountryName { get; set; }
        
        /// <summary>
        /// Region/state
        /// </summary>
        public string? Region { get; set; }
        
        /// <summary>
        /// City
        /// </summary>
        public string? City { get; set; }
        
        /// <summary>
        /// Latitude
        /// </summary>
        public double? Latitude { get; set; }
        
        /// <summary>
        /// Longitude
        /// </summary>
        public double? Longitude { get; set; }
        
        /// <summary>
        /// ISP
        /// </summary>
        public string? ISP { get; set; }
        
        /// <summary>
        /// Organization
        /// </summary>
        public string? Organization { get; set; }
    }

    /// <summary>
    /// Implementation of security context service with device fingerprinting and location detection
    /// </summary>
    public class SecurityContextService : ISecurityContextService
    {
        private readonly ILogger<SecurityContextService> _logger;
        private readonly string _cachedDeviceFingerprint;
        private readonly object _lock = new object();

        public SecurityContextService(ILogger<SecurityContextService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cachedDeviceFingerprint = GenerateDeviceFingerprintAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get comprehensive security context for audit events
        /// </summary>
        public async Task<SecurityContext> GetSecurityContextAsync()
        {
            try
            {
                var context = new SecurityContext
                {
                    DeviceFingerprint = await GetDeviceFingerprintAsync(),
                    HashedIpAddress = await GetHashedIpAddressAsync(),
                    Location = await GetGeoLocationAsync(),
                    UserAgent = GetUserAgent(),
                    SessionId = GetSessionId(),
                    ProcessId = Environment.ProcessId,
                    ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                    HashedMachineName = HashValue(Environment.MachineName)
                };

                _logger.LogDebug("Security context captured: {DeviceFingerprint}, {ProcessId}", 
                    context.DeviceFingerprint.Substring(0, 8) + "...", context.ProcessId);

                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing security context");
                // Return minimal context on error
                return new SecurityContext
                {
                    SessionId = GetSessionId(),
                    ProcessId = Environment.ProcessId,
                    ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                    CapturedAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Get device fingerprint for identification
        /// </summary>
        public async Task<string> GetDeviceFingerprintAsync()
        {
            return await Task.FromResult(_cachedDeviceFingerprint);
        }

        /// <summary>
        /// Get current IP address (hashed for privacy)
        /// </summary>
        public async Task<string?> GetHashedIpAddressAsync()
        {
            try
            {
                var ipAddress = await GetPublicIpAddressAsync();
                return !string.IsNullOrEmpty(ipAddress) ? HashValue(ipAddress) : null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to get IP address for fingerprinting");
                return null;
            }
        }

        /// <summary>
        /// Get geolocation information based on IP address
        /// </summary>
        public async Task<GeoLocation?> GetGeoLocationAsync()
        {
            try
            {
                // For SOC 2 compliance, we'll use a basic geolocation service
                // In production, this would integrate with a proper geolocation API
                var ipAddress = await GetPublicIpAddressAsync();
                if (string.IsNullOrEmpty(ipAddress))
                    return null;

                // For now, return basic info - in production would call geolocation API
                return new GeoLocation
                {
                    // These would be populated by actual geolocation service
                    CountryCode = "US", // Placeholder
                    CountryName = "United States", // Placeholder
                    ISP = "Unknown" // Placeholder
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to get geolocation information");
                return null;
            }
        }

        /// <summary>
        /// Generate device fingerprint using hardware and system information
        /// </summary>
        private async Task<string> GenerateDeviceFingerprintAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var fingerprintData = new StringBuilder();
                    
                    // Add hardware identifiers
                    fingerprintData.Append(GetProcessorId());
                    fingerprintData.Append(GetMotherboardSerialNumber());
                    fingerprintData.Append(GetDiskDriveSerialNumber());
                    
                    // Add system information
                    fingerprintData.Append(Environment.OSVersion.ToString());
                    fingerprintData.Append(Environment.UserName);
                    fingerprintData.Append(Environment.MachineName);
                    
                    // Add MAC address
                    var macAddress = GetMacAddress();
                    if (!string.IsNullOrEmpty(macAddress))
                    {
                        fingerprintData.Append(macAddress);
                    }
                    
                    var fingerprint = HashValue(fingerprintData.ToString());
                    _logger.LogDebug("Device fingerprint generated: {Fingerprint}", fingerprint.Substring(0, 8) + "...");
                    
                    return fingerprint;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating device fingerprint");
                    // Fallback to simple fingerprint
                    return HashValue($"{Environment.UserName}_{Environment.MachineName}_{Environment.OSVersion}");
                }
            });
        }

        /// <summary>
        /// Get public IP address
        /// </summary>
        private async Task<string?> GetPublicIpAddressAsync()
        {
            try
            {
                // Try multiple services for redundancy
                var services = new[]
                {
                    "https://api.ipify.org",
                    "https://icanhazip.com",
                    "https://checkip.amazonaws.com"
                };

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                foreach (var service in services)
                {
                    try
                    {
                        var response = await httpClient.GetStringAsync(service);
                        var ipAddress = response.Trim();
                        
                        if (IPAddress.TryParse(ipAddress, out _) && !IsPrivateIP(ipAddress))
                        {
                            return ipAddress;
                        }
                    }
                    catch
                    {
                        // Try next service
                        continue;
                    }
                }

                // Fallback to local IP if public IP not available
                return GetLocalIpAddress();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting public IP address");
                return GetLocalIpAddress();
            }
        }

        /// <summary>
        /// Get local IP address
        /// </summary>
        private string? GetLocalIpAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if IP address is private
        /// </summary>
        private bool IsPrivateIP(string ip)
        {
            try
            {
                var address = IPAddress.Parse(ip);
                var bytes = address.GetAddressBytes();
                
                // 10.0.0.0 - 10.255.255.255
                if (bytes[0] == 10) return true;
                
                // 172.16.0.0 - 172.31.255.255
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                
                // 192.168.0.0 - 192.168.255.255
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                
                return false;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Get MAC address
        /// </summary>
        private string? GetMacAddress()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up)
                        {
                            return ni.GetPhysicalAddress().ToString();
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get processor ID
        /// </summary>
        private string GetProcessorId()
        {
            try
            {
                using var mc = new ManagementClass("win32_processor");
                var moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    var processorId = mo["processorId"]?.ToString();
                    if (!string.IsNullOrEmpty(processorId))
                    {
                        return processorId;
                    }
                }
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Get motherboard serial number
        /// </summary>
        private string GetMotherboardSerialNumber()
        {
            try
            {
                using var mc = new ManagementClass("win32_baseboard");
                var moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    var serialNumber = mo["SerialNumber"]?.ToString();
                    if (!string.IsNullOrEmpty(serialNumber) && serialNumber != "To be filled by O.E.M.")
                    {
                        return serialNumber;
                    }
                }
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Get disk drive serial number
        /// </summary>
        private string GetDiskDriveSerialNumber()
        {
            try
            {
                using var mc = new ManagementClass("win32_diskdrive");
                var moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    var serialNumber = mo["SerialNumber"]?.ToString();
                    if (!string.IsNullOrEmpty(serialNumber))
                    {
                        return serialNumber;
                    }
                }
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Get user agent or client information
        /// </summary>
        private string GetUserAgent()
        {
            return $"ScottWisper/{Environment.Version} (Windows NT {Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}; Win64; x64)";
        }

        /// <summary>
        /// Get session identifier
        /// </summary>
        private string GetSessionId()
        {
            // Use a combination of process start time and user for session ID
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var sessionData = $"{Environment.UserName}_{process.StartTime:yyyyMMdd_HHmmss}";
            return HashValue(sessionData).Substring(0, 16);
        }

        /// <summary>
        /// Hash a value for privacy
        /// </summary>
        private string HashValue(string value)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
