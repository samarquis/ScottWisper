using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using ScottWisper.Configuration;

namespace ScottWisper.Services
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }
        Task SaveAsync();
        Task<T> GetValueAsync<T>(string key);
        Task SetValueAsync<T>(string key, T value);
        Task<string> GetEncryptedValueAsync(string key);
        Task SetEncryptedValueAsync(string key, string value);
    }

    public class SettingsService : ISettingsService
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<AppSettings> _options;
        private readonly string _userSettingsPath;
        private readonly string _encryptionKey;
        private AppSettings _currentSettings;

        public AppSettings Settings => _currentSettings;

        public SettingsService(IConfiguration configuration, IOptionsMonitor<AppSettings> options)
        {
            _configuration = configuration;
            _options = options;
            _currentSettings = options.CurrentValue;
            
            // Initialize user settings path in %APPDATA%
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScottWisper");
            Directory.CreateDirectory(appFolder);
            _userSettingsPath = Path.Combine(appFolder, "usersettings.json");
            
            // Initialize encryption key based on machine info
            _encryptionKey = GenerateMachineSpecificKey();
            
            // Load user-specific settings
            _ = LoadUserSettingsAsync();
        }

        public async Task SaveAsync()
        {
            try
            {
                // Validate settings before saving
                ValidateSettings(_currentSettings);
                
                var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_userSettingsPath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
            }
        }

        public async Task<T> GetValueAsync<T>(string key)
        {
            var value = _configuration[key];
            if (value != null)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(value);
                }
                catch
                {
                    return default(T)!;
                }
            }
            return default(T)!;
        }

        public async Task SetValueAsync<T>(string key, T value)
        {
            // For now, this will update the in-memory settings
            // In a full implementation, you'd want to update specific properties
            var json = JsonSerializer.Serialize(value);
            _configuration[key] = json;
        }

        public async Task<string> GetEncryptedValueAsync(string key)
        {
            try
            {
                var encryptedData = await File.ReadAllTextAsync(GetEncryptedFilePath(key));
                return DecryptString(encryptedData);
            }
            catch (FileNotFoundException)
            {
                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public async Task SetEncryptedValueAsync(string key, string value)
        {
            try
            {
                var encryptedData = EncryptString(value);
                var filePath = GetEncryptedFilePath(key);
                await File.WriteAllTextAsync(filePath, encryptedData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save encrypted value for {key}: {ex.Message}", ex);
            }
        }

        private async Task LoadUserSettingsAsync()
        {
            try
            {
                if (File.Exists(_userSettingsPath))
                {
                    var json = await File.ReadAllTextAsync(_userSettingsPath);
                    var userSettings = JsonSerializer.Deserialize<AppSettings>(json);
                    
                    if (userSettings != null)
                    {
                        // Merge user settings with default settings
                        MergeSettings(_currentSettings, userSettings);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with default settings
                System.Diagnostics.Debug.WriteLine($"Failed to load user settings: {ex.Message}");
            }
        }

        private void MergeSettings(AppSettings target, AppSettings source)
        {
            if (source.Audio != null)
            {
                target.Audio = source.Audio;
            }
            
            if (source.Transcription != null)
            {
                target.Transcription = source.Transcription;
            }
            
            if (source.Hotkeys != null)
            {
                target.Hotkeys = source.Hotkeys;
            }
            
            if (source.UI != null)
            {
                target.UI = source.UI;
            }
        }

        private void ValidateSettings(AppSettings settings)
        {
            // Audio settings validation
            if (settings.Audio.SampleRate <= 0)
            {
                throw new ValidationException("Sample rate must be greater than 0");
            }
            
            if (settings.Audio.Channels < 1 || settings.Audio.Channels > 2)
            {
                throw new ValidationException("Channels must be 1 or 2");
            }

            // Transcription settings validation
            if (string.IsNullOrWhiteSpace(settings.Transcription.Provider))
            {
                throw new ValidationException("Transcription provider is required");
            }

            if (string.IsNullOrWhiteSpace(settings.Transcription.Model))
            {
                throw new ValidationException("Transcription model is required");
            }

            // Hotkey settings validation
            if (string.IsNullOrWhiteSpace(settings.Hotkeys.ToggleRecording))
            {
                throw new ValidationException("Toggle recording hotkey is required");
            }
        }

        private string GenerateMachineSpecificKey()
        {
            // Use a combination of machine name and user name for encryption
            var machineKey = $"{Environment.MachineName}_{Environment.UserName}";
            var keyBytes = Encoding.UTF8.GetBytes(machineKey);
            
            // Use SHA256 to create a consistent 32-byte key
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(keyBytes));
        }

        private string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                using var aes = Aes.Create();
                var key = Convert.FromBase64String(_encryptionKey);
                aes.Key = key;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                using var msEncrypt = new MemoryStream();
                
                // Write IV to the beginning of the stream
                msEncrypt.Write(aes.IV, 0, aes.IV.Length);
                
                using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                using var swEncrypt = new StreamWriter(csEncrypt);
                swEncrypt.Write(plainText);
                
                return Convert.ToBase64String(msEncrypt.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }

        private string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                var fullCipher = Convert.FromBase64String(encryptedText);
                
                using var aes = Aes.Create();
                var key = Convert.FromBase64String(_encryptionKey);
                aes.Key = key;
                
                // Extract IV from the beginning of the cipher text
                var iv = new byte[aes.BlockSize / 8];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                aes.IV = iv;
                
                // Extract the actual cipher text
                var cipherText = new byte[fullCipher.Length - iv.Length];
                Array.Copy(fullCipher, iv.Length, cipherText, 0, cipherText.Length);
                
                using var decryptor = aes.CreateDecryptor();
                using var msDecrypt = new MemoryStream(cipherText);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);
                
                return srDecrypt.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetEncryptedFilePath(string key)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScottWisper");
            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, $"{key}.encrypted");
        }
    }
}