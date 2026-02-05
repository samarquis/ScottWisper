using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Service for secure credential storage using Windows Credential Manager
    /// Uses CredWrite/CredRead APIs for encrypted storage
    /// </summary>
    public class WindowsCredentialService : ICredentialService
    {
        private readonly ILogger<WindowsCredentialService> _logger;
        private readonly IAuditLoggingService _auditService;
        private const string TargetPrefix = "WhisperKey_";
        
        public WindowsCredentialService(ILogger<WindowsCredentialService>? logger = null, IAuditLoggingService? auditService = null)
        {
            _logger = logger ?? new LoggerFactory().CreateLogger<WindowsCredentialService>();
            _auditService = auditService ?? new NullAuditLoggingService();
        }
        
        /// <summary>
        /// Store a credential securely in Windows Credential Manager
        /// </summary>
        public async Task<bool> StoreCredentialAsync(string key, string value)
        {
            try
            {
                var targetName = TargetPrefix + key;
                
                // Convert password to secure byte array
                var passwordBytes = Encoding.Unicode.GetBytes(value);
                
                // Allocate unmanaged memory for the credential
                var credential = new CREDENTIAL
                {
                    Type = CRED_TYPE.GENERIC,
                    TargetName = Marshal.StringToCoTaskMemUni(targetName),
                    Comment = Marshal.StringToCoTaskMemUni("WhisperKey API Key"),
                    CredentialBlob = Marshal.AllocCoTaskMem(passwordBytes.Length),
                    CredentialBlobSize = passwordBytes.Length,
                    Persist = CRED_PERSIST.LOCAL_MACHINE,
                    UserName = Marshal.StringToCoTaskMemUni(Environment.UserName)
                };
                
                try
                {
                    // Copy password bytes to unmanaged memory
                    Marshal.Copy(passwordBytes, 0, credential.CredentialBlob, passwordBytes.Length);
                    
                    // Write credential to Windows Credential Manager
                    bool result = CredWrite(ref credential, 0);
                    
                    if (result)
                    {
                        await _auditService.LogEventAsync(
                            AuditEventType.ApiKeyAccessed,
                            $"API key stored successfully for key: {key}",
                            System.Text.Json.JsonSerializer.Serialize(new { 
                                Key = key,
                                Action = "Stored",
                                Success = true,
                                UserId = Environment.UserName
                            }),
                            DataSensitivity.High);
                        
                        _logger.LogInformation("Credential stored successfully for key: {Key}", key);
                        return true;
                    }
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        
                        await _auditService.LogEventAsync(
                            AuditEventType.SecurityEvent,
                            $"Failed to store API key for key: {key}",
                            System.Text.Json.JsonSerializer.Serialize(new { 
                                Key = key,
                                Action = "StoreFailed",
                                ErrorCode = error,
                                Success = false,
                                UserId = Environment.UserName
                            }),
                            DataSensitivity.Medium);
                        
                        _logger.LogError("Failed to store credential. Error code: {ErrorCode}", error);
                        return false;
                    }
                }
                finally
                {
                    // Clean up unmanaged resources
                    if (credential.TargetName != IntPtr.Zero)
                        Marshal.FreeCoTaskMem(credential.TargetName);
                    if (credential.Comment != IntPtr.Zero)
                        Marshal.FreeCoTaskMem(credential.Comment);
                    if (credential.CredentialBlob != IntPtr.Zero)
                        Marshal.FreeCoTaskMem(credential.CredentialBlob);
                    if (credential.UserName != IntPtr.Zero)
                        Marshal.FreeCoTaskMem(credential.UserName);
                    
                    // Clear password bytes from memory
                    Array.Clear(passwordBytes, 0, passwordBytes.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing credential for key: {Key}", key);
                return false;
            }
        }
        
        /// <summary>
        /// Retrieve a stored credential from Windows Credential Manager
        /// </summary>
        public async Task<string?> RetrieveCredentialAsync(string key)
        {
            try
            {
                var targetName = TargetPrefix + key;
                IntPtr credentialPtr = IntPtr.Zero;
                
                try
                {
                    // Read credential from Windows Credential Manager
                    bool result = CredRead(targetName, CRED_TYPE.GENERIC, 0, out credentialPtr);
                    
                    if (!result || credentialPtr == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != 1168) // ERROR_NOT_FOUND
                        {
                            _logger.LogWarning("Credential not found for key: {Key}. Error code: {ErrorCode}", key, error);
                        }
                        return null;
                    }
                    
                    // Marshal the credential structure
                    var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
                    
                    // Extract the password from the credential blob
                    if (credential.CredentialBlob != IntPtr.Zero && credential.CredentialBlobSize > 0)
                    {
                        var passwordBytes = new byte[credential.CredentialBlobSize];
                        Marshal.Copy(credential.CredentialBlob, passwordBytes, 0, credential.CredentialBlobSize);
                        
                        try
                        {
                            var password = Encoding.Unicode.GetString(passwordBytes);
                        
                        await _auditService.LogEventAsync(
                            AuditEventType.ApiKeyAccessed,
                            $"API key retrieved for key: {key}",
                            System.Text.Json.JsonSerializer.Serialize(new { 
                                Key = key,
                                Action = "Retrieved",
                                Success = true,
                                UserId = Environment.UserName
                            }),
                            DataSensitivity.High);
                        
                        _logger.LogDebug("Credential retrieved successfully for key: {Key}", key);
                        return password;
                        }
                        finally
                        {
                            // Clear password bytes from memory
                            Array.Clear(passwordBytes, 0, passwordBytes.Length);
                        }
                    }
                    
                    return null;
                }
                finally
                {
                    // Free the credential structure
                    if (credentialPtr != IntPtr.Zero)
                    {
                        CredFree(credentialPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving credential for key: {Key}", key);
                return null;
            }
        }
        
        /// <summary>
        /// Delete a stored credential from Windows Credential Manager
        /// </summary>
        public async Task<bool> DeleteCredentialAsync(string key)
        {
            try
            {
                var targetName = TargetPrefix + key;
                
                // Delete credential from Windows Credential Manager
                bool result = CredDelete(targetName, CRED_TYPE.GENERIC, 0);
                
                if (result)
                    {
                        await _auditService.LogEventAsync(
                            AuditEventType.ApiKeyAccessed,
                            $"API key deleted for key: {key}",
                            System.Text.Json.JsonSerializer.Serialize(new { 
                                Key = key,
                                Action = "Deleted",
                                Success = true,
                                UserId = Environment.UserName
                            }),
                            DataSensitivity.High);
                        
                        _logger.LogInformation("Credential deleted successfully for key: {Key}", key);
                        return true;
                    }
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        
                        await _auditService.LogEventAsync(
                            AuditEventType.SecurityEvent,
                            $"Failed to delete API key for key: {key}",
                            System.Text.Json.JsonSerializer.Serialize(new { 
                                Key = key,
                                Action = "DeleteFailed",
                                ErrorCode = error,
                                ErrorName = error == 1168 ? "NOT_FOUND" : "UNKNOWN",
                                Success = false,
                                UserId = Environment.UserName
                            }),
                            DataSensitivity.Medium);
                        
                        if (error == 1168) // ERROR_NOT_FOUND
                        {
                            _logger.LogInformation("Credential already deleted or not found for key: {Key}", key);
                            return true;
                        }
                        
                        _logger.LogError("Failed to delete credential. Error code: {ErrorCode}", error);
                        return false;
                    }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting credential for key: {Key}", key);
                return false;
            }
        }
        
        /// <summary>
        /// Check if a credential exists in Windows Credential Manager
        /// </summary>
        public Task<bool> CredentialExistsAsync(string key)
        {
            try
            {
                var targetName = TargetPrefix + key;
                IntPtr credentialPtr = IntPtr.Zero;
                
                try
                {
                    // Try to read the credential
                    bool result = CredRead(targetName, CRED_TYPE.GENERIC, 0, out credentialPtr);
                    
                    if (result && credentialPtr != IntPtr.Zero)
                    {
                        return Task.FromResult(true);
                    }
                    
                    return Task.FromResult(false);
                }
                finally
                {
                    // Free the credential structure if it was allocated
                    if (credentialPtr != IntPtr.Zero)
                    {
                        CredFree(credentialPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking credential existence for key: {Key}", key);
                return Task.FromResult(false);
            }
        }
        
        #region Windows Credential Manager P/Invoke
        
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);
        
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredRead(string targetName, CRED_TYPE type, uint flags, out IntPtr credential);
        
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredDelete(string targetName, CRED_TYPE type, uint flags);
        
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern void CredFree(IntPtr buffer);
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public CRED_FLAGS Flags;
            public CRED_TYPE Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public FILETIME LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public CRED_PERSIST Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }
        
        [Flags]
        private enum CRED_FLAGS : uint
        {
            NONE = 0,
            PROMPT_NOW = 0x2,
            USERNAME_TARGET = 0x4
        }
        
        private enum CRED_TYPE : uint
        {
            GENERIC = 1,
            DOMAIN_PASSWORD = 2,
            DOMAIN_CERTIFICATE = 3,
            DOMAIN_VISIBLE_PASSWORD = 4,
            GENERIC_CERTIFICATE = 5,
            DOMAIN_EXTENDED = 6,
            MAXIMUM = 7,
            MAXIMUM_EX = MAXIMUM + 1000
        }
        
        private enum CRED_PERSIST : uint
        {
            SESSION = 1,
            LOCAL_MACHINE = 2,
            ENTERPRISE = 3
        }
        
        #endregion
    }
}