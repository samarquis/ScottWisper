using System;
using System.Security;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for secure credential storage service
    /// </summary>
    public interface ICredentialService
    {
        /// <summary>
        /// Store a credential securely
        /// </summary>
        /// <param name="key">Unique identifier for the credential</param>
        /// <param name="value">The credential value to store</param>
        /// <returns>True if stored successfully, false otherwise</returns>
        Task<bool> StoreCredentialAsync(string key, string value);
        
        /// <summary>
        /// Retrieve a stored credential
        /// </summary>
        /// <param name="key">Unique identifier for the credential</param>
        /// <returns>The credential value if found, null otherwise</returns>
        Task<string?> RetrieveCredentialAsync(string key);
        
        /// <summary>
        /// Delete a stored credential
        /// </summary>
        /// <param name="key">Unique identifier for the credential</param>
        /// <returns>True if deleted successfully, false otherwise</returns>
        Task<bool> DeleteCredentialAsync(string key);
        
        /// <summary>
        /// Check if a credential exists
        /// </summary>
        /// <param name="key">Unique identifier for the credential</param>
        /// <returns>True if credential exists, false otherwise</returns>
        Task<bool> CredentialExistsAsync(string key);
    }
}
