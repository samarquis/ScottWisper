using Microsoft.Win32;
using System;

namespace WhisperKey.Services
{
    /// <summary>
    /// Registry hive options for registry operations.
    /// </summary>
    public enum RegistryHiveOption
    {
        /// <summary>
        /// HKEY_CURRENT_USER - Current user registry hive.
        /// </summary>
        CurrentUser,

        /// <summary>
        /// HKEY_LOCAL_MACHINE - Local machine registry hive (requires admin).
        /// </summary>
        LocalMachine,

        /// <summary>
        /// HKEY_CLASSES_ROOT - File associations and COM objects.
        /// </summary>
        ClassesRoot,

        /// <summary>
        /// HKEY_CURRENT_CONFIG - Current hardware configuration.
        /// </summary>
        CurrentConfig,

        /// <summary>
        /// HKEY_USERS - User profiles registry hive.
        /// </summary>
        Users
    }

    /// <summary>
    /// Interface for Windows Registry operations to enable testing without registry dependencies.
    /// This abstraction allows unit tests to use mock registry implementations for isolated testing.
    /// </summary>
    public interface IRegistryService
    {
        /// <summary>
        /// Reads a string value from the registry.
        /// </summary>
        /// <param name="hive">The registry hive to read from.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <param name="valueName">The name of the value to read.</param>
        /// <returns>The registry value as a string, or null if not found.</returns>
        string? ReadValue(RegistryHiveOption hive, string keyPath, string valueName);

        /// <summary>
        /// Reads a value from the registry with a specified type.
        /// </summary>
        /// <typeparam name="T">The type to convert the value to (string, int, byte[], etc.).</typeparam>
        /// <param name="hive">The registry hive to read from.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <param name="valueName">The name of the value to read.</param>
        /// <returns>The registry value converted to type T, or default if not found.</returns>
        T? ReadValue<T>(RegistryHiveOption hive, string keyPath, string valueName);

        /// <summary>
        /// Writes a string value to the registry.
        /// </summary>
        /// <param name="hive">The registry hive to write to.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <param name="valueName">The name of the value to write.</param>
        /// <param name="value">The value to write.</param>
        void WriteValue(RegistryHiveOption hive, string keyPath, string valueName, string value);

        /// <summary>
        /// Writes a value to the registry.
        /// </summary>
        /// <param name="hive">The registry hive to write to.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <param name="valueName">The name of the value to write.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="valueKind">The registry value kind.</param>
        void WriteValue(RegistryHiveOption hive, string keyPath, string valueName, object value, RegistryValueKind valueKind);

        /// <summary>
        /// Deletes a value from the registry.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <param name="valueName">The name of the value to delete.</param>
        /// <param name="throwOnMissingValue">Whether to throw if the value doesn't exist.</param>
        void DeleteValue(RegistryHiveOption hive, string keyPath, string valueName, bool throwOnMissingValue = false);

        /// <summary>
        /// Deletes a registry key and optionally its subkeys.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <param name="recursive">Whether to delete subkeys recursively.</param>
        void DeleteKey(RegistryHiveOption hive, string keyPath, bool recursive = false);

        /// <summary>
        /// Checks if a registry key exists.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <returns>True if the key exists, false otherwise.</returns>
        bool KeyExists(RegistryHiveOption hive, string keyPath);

        /// <summary>
        /// Checks if a registry value exists.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <param name="valueName">The name of the value.</param>
        /// <returns>True if the value exists, false otherwise.</returns>
        bool ValueExists(RegistryHiveOption hive, string keyPath, string valueName);

        /// <summary>
        /// Creates a registry key if it doesn't exist.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <returns>The full path to the created or existing key.</returns>
        string CreateKey(RegistryHiveOption hive, string keyPath);

        /// <summary>
        /// Gets the names of all subkeys under a registry key.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <returns>An array of subkey names.</returns>
        string[] GetSubKeyNames(RegistryHiveOption hive, string keyPath);

        /// <summary>
        /// Gets the names of all values under a registry key.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="keyPath">The path to the registry key.</param>
        /// <returns>An array of value names.</returns>
        string[] GetValueNames(RegistryHiveOption hive, string keyPath);

        /// <summary>
        /// Sets the application to start with Windows.
        /// </summary>
        /// <param name="appName">The name of the application.</param>
        /// <param name="appPath">The path to the application executable.</param>
        /// <param name="enable">True to enable startup, false to disable.</param>
        void SetStartupWithWindows(string appName, string appPath, bool enable);

        /// <summary>
        /// Checks if the application is set to start with Windows.
        /// </summary>
        /// <param name="appName">The name of the application.</param>
        /// <returns>True if startup is enabled, false otherwise.</returns>
        bool IsStartupWithWindowsEnabled(string appName);
    }

    /// <summary>
    /// Real implementation of IRegistryService that uses the Windows Registry.
    /// This is the production implementation that performs actual registry operations.
    /// </summary>
    public class RegistryService : IRegistryService
    {
        public string? ReadValue(RegistryHiveOption hive, string keyPath, string valueName)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new ArgumentException("Key path cannot be null or whitespace", nameof(keyPath));
            if (string.IsNullOrWhiteSpace(valueName))
                throw new ArgumentException("Value name cannot be null or whitespace", nameof(valueName));

            var registryHive = GetRegistryHive(hive);
            using var key = registryHive.OpenSubKey(keyPath);
            return key?.GetValue(valueName)?.ToString();
        }

        public T? ReadValue<T>(RegistryHiveOption hive, string keyPath, string valueName)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new ArgumentException("Key path cannot be null or whitespace", nameof(keyPath));
            if (string.IsNullOrWhiteSpace(valueName))
                throw new ArgumentException("Value name cannot be null or whitespace", nameof(valueName));

            var registryHive = GetRegistryHive(hive);
            using var key = registryHive.OpenSubKey(keyPath);
            var value = key?.GetValue(valueName);

            if (value == null)
                return default;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        public void WriteValue(RegistryHiveOption hive, string keyPath, string valueName, string value)
        {
            WriteValue(hive, keyPath, valueName, value, RegistryValueKind.String);
        }

        public void WriteValue(RegistryHiveOption hive, string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new ArgumentException("Key path cannot be null or whitespace", nameof(keyPath));
            if (string.IsNullOrWhiteSpace(valueName))
                throw new ArgumentException("Value name cannot be null or whitespace", nameof(valueName));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var registryHive = GetRegistryHive(hive);
            using var key = registryHive.CreateSubKey(keyPath);
            key?.SetValue(valueName, value, valueKind);
        }

        public void DeleteValue(RegistryHiveOption hive, string keyPath, string valueName, bool throwOnMissingValue = false)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new ArgumentException("Key path cannot be null or whitespace", nameof(keyPath));
            if (string.IsNullOrWhiteSpace(valueName))
                throw new ArgumentException("Value name cannot be null or whitespace", nameof(valueName));

            var registryHive = GetRegistryHive(hive);
            using var key = registryHive.OpenSubKey(keyPath, true);
            key?.DeleteValue(valueName, throwOnMissingValue);
        }

        public void DeleteKey(RegistryHiveOption hive, string keyPath, bool recursive = false)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new ArgumentException("Key path cannot be null or whitespace", nameof(keyPath));

            var registryHive = GetRegistryHive(hive);
            registryHive.DeleteSubKeyTree(keyPath, false);
        }

        public bool KeyExists(RegistryHiveOption hive, string keyPath)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new ArgumentException("Key path cannot be null or whitespace", nameof(keyPath));

            var registryHive = GetRegistryHive(hive);
            using var key = registryHive.OpenSubKey(keyPath);
            return key != null;
        }

        public bool ValueExists(RegistryHiveOption hive, string keyPath, string valueName)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new ArgumentException("Key path cannot be null or whitespace", nameof(keyPath));
            if (string.IsNullOrWhiteSpace(valueName))
                throw new ArgumentException("Value name cannot be null or whitespace", nameof(valueName));

            var registryHive = GetRegistryHive(hive);
            using var key = registryHive.OpenSubKey(keyPath);
            return key?.GetValue(valueName) != null;
        }

        public string CreateKey(RegistryHiveOption hive, string keyPath)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new ArgumentException("Key path cannot be null or whitespace", nameof(keyPath));

            var registryHive = GetRegistryHive(hive);
            using var key = registryHive.CreateSubKey(keyPath);
            return key?.Name ?? string.Empty;
        }

        public string[] GetSubKeyNames(RegistryHiveOption hive, string keyPath)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new ArgumentException("Key path cannot be null or whitespace", nameof(keyPath));

            var registryHive = GetRegistryHive(hive);
            using var key = registryHive.OpenSubKey(keyPath);
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }

        public string[] GetValueNames(RegistryHiveOption hive, string keyPath)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new ArgumentException("Key path cannot be null or whitespace", nameof(keyPath));

            var registryHive = GetRegistryHive(hive);
            using var key = registryHive.OpenSubKey(keyPath);
            return key?.GetValueNames() ?? Array.Empty<string>();
        }

        public void SetStartupWithWindows(string appName, string appPath, bool enable)
        {
            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentException("App name cannot be null or whitespace", nameof(appName));
            if (string.IsNullOrWhiteSpace(appPath))
                throw new ArgumentException("App path cannot be null or whitespace", nameof(appPath));

            const string startupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

            if (enable)
            {
                WriteValue(RegistryHiveOption.CurrentUser, startupKey, appName, appPath);
            }
            else
            {
                if (ValueExists(RegistryHiveOption.CurrentUser, startupKey, appName))
                {
                    DeleteValue(RegistryHiveOption.CurrentUser, startupKey, appName);
                }
            }
        }

        public bool IsStartupWithWindowsEnabled(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentException("App name cannot be null or whitespace", nameof(appName));

            const string startupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            return ValueExists(RegistryHiveOption.CurrentUser, startupKey, appName);
        }

        private static RegistryKey GetRegistryHive(RegistryHiveOption hive)
        {
            return hive switch
            {
                RegistryHiveOption.CurrentUser => Registry.CurrentUser,
                RegistryHiveOption.LocalMachine => Registry.LocalMachine,
                RegistryHiveOption.ClassesRoot => Registry.ClassesRoot,
                RegistryHiveOption.CurrentConfig => Registry.CurrentConfig,
                RegistryHiveOption.Users => Registry.Users,
                _ => throw new ArgumentOutOfRangeException(nameof(hive), $"Unknown registry hive: {hive}")
            };
        }
    }
}
