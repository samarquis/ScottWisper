using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Services;

namespace WhisperKey.Validation
{
    /// <summary>
    /// Result of comprehensive Phase 04 validation.
    /// </summary>
    public class Phase04ValidationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        
        public bool Core03_TextInjection_Success { get; set; }
        public double Core03_SuccessRate { get; set; }
        
        public bool Sys02_SettingsManagement_Success { get; set; }
        public int Sys02_SettingsVerified { get; set; }
        
        public bool Sys03_AudioDeviceSelection_Success { get; set; }
        public int Sys03_DevicesTested { get; set; }
        public bool Sys03_PermissionsVerified { get; set; }
        
        public double OverallSuccessRate { get; set; }
        public bool IsPass { get; set; }
        public List<string> ValidationLog { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        
        public WhisperKey.CrossApplicationValidationResult? CrossAppResult { get; set; }
    }

    /// <summary>
    /// Implements comprehensive Phase 04 validation for gap closure requirements.
    /// Validates CORE-03, SYS-02, and SYS-03.
    /// </summary>
    public class Phase04Validator
    {
        private readonly ICrossApplicationValidator _crossAppValidator;
        private readonly IAudioDeviceService _audioDeviceService;
        private readonly ISettingsService _settingsService;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<Phase04Validator> _logger;

        public Phase04Validator(
            ICrossApplicationValidator crossAppValidator,
            IAudioDeviceService audioDeviceService,
            ISettingsService settingsService,
            IPermissionService permissionService,
            ILogger<Phase04Validator> logger)
        {
            _crossAppValidator = crossAppValidator;
            _audioDeviceService = audioDeviceService;
            _settingsService = settingsService;
            _permissionService = permissionService;
            _logger = logger;
        }

        /// <summary>
        /// Runs complete validation for all Phase 04 requirements.
        /// </summary>
        public async Task<Phase04ValidationResult> RunCompleteValidationAsync()
        {
            var result = new Phase04ValidationResult
            {
                StartTime = DateTime.UtcNow
            };

            _logger.LogInformation("Starting Phase 04 Comprehensive Validation");
            result.ValidationLog.Add($"Validation started at {result.StartTime}");

            try
            {
                // CORE-03: Text Injection
                await ValidateCore03_TextInjectionAsync(result);

                // SYS-02: Settings Management
                await ValidateSys02_SettingsManagementAsync(result);

                // SYS-03: Audio Device Selection & Permissions
                await ValidateSys03_AudioDeviceSelectionAsync(result);

                // Finalize metrics
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;
                
                int passed = 0;
                if (result.Core03_TextInjection_Success) passed++;
                if (result.Sys02_SettingsManagement_Success) passed++;
                if (result.Sys03_AudioDeviceSelection_Success) passed++;
                
                result.OverallSuccessRate = (double)passed / 3 * 100;
                result.IsPass = result.OverallSuccessRate >= 95 || (result.Core03_SuccessRate >= 95 && result.Sys02_SettingsManagement_Success && result.Sys03_AudioDeviceSelection_Success);

                _logger.LogInformation("Phase 04 Validation completed. Overall Success Rate: {Rate:F1}%", result.OverallSuccessRate);
                result.ValidationLog.Add($"Validation completed at {result.EndTime}. Overall Success: {result.IsPass}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error during Phase 04 validation");
                result.Errors.Add($"Fatal error: {ex.Message}");
                result.IsPass = false;
            }

            return result;
        }

        public async Task ValidateCore03_TextInjectionAsync(Phase04ValidationResult result)
        {
            _logger.LogInformation("Validating CORE-03: Cross-Application Text Injection");
            result.ValidationLog.Add("Validating CORE-03...");

            try
            {
                var crossAppResult = await _crossAppValidator.ValidateCrossApplicationInjectionAsync();
                result.CrossAppResult = crossAppResult;
                result.Core03_SuccessRate = crossAppResult.OverallSuccessRate;
                result.Core03_TextInjection_Success = crossAppResult.OverallSuccessRate >= 95;
                
                result.ValidationLog.Add($"CORE-03 Result: {crossAppResult.SuccessfulApplications}/{crossAppResult.TotalApplicationsTested} apps passed ({crossAppResult.OverallSuccessRate:F1}%)");
                
                if (!result.Core03_TextInjection_Success)
                {
                    foreach (var appResult in crossAppResult.ApplicationResults.Where(r => !r.IsSuccess))
                    {
                        result.Errors.Add($"CORE-03: Injection failed for {appResult.DisplayName}: {appResult.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating CORE-03");
                result.Core03_TextInjection_Success = false;
                result.Errors.Add($"CORE-03 Error: {ex.Message}");
            }
        }

        public async Task ValidateSys02_SettingsManagementAsync(Phase04ValidationResult result)
        {
            _logger.LogInformation("Validating SYS-02: Settings Management");
            result.ValidationLog.Add("Validating SYS-02...");

            try
            {
                var settings = _settingsService.Settings;
                int verified = 0;

                // Verify basic settings existence
                if (settings != null) verified++;
                if (settings?.UI != null) verified++;
                
                // Test settings persistence
                const string testKey = "Validation_Test_Key";
                const string testValue = "Validation_Test_Value";
                
                await _settingsService.SetValueAsync(testKey, testValue);
                var retrieved = await _settingsService.GetValueAsync<string>(testKey);
                
                if (retrieved == testValue) verified++;
                
                // Test hotkey profile management
                var profiles = await _settingsService.GetHotkeyProfilesAsync();
                if (profiles != null) verified++;

                result.Sys02_SettingsVerified = verified;
                result.Sys02_SettingsManagement_Success = verified >= 3;
                result.ValidationLog.Add($"SYS-02 Result: {verified} settings criteria verified.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating SYS-02");
                result.Sys02_SettingsManagement_Success = false;
                result.Errors.Add($"SYS-02 Error: {ex.Message}");
            }
        }

        public async Task ValidateSys03_AudioDeviceSelectionAsync(Phase04ValidationResult result)
        {
            _logger.LogInformation("Validating SYS-03: Audio Device Selection & Permissions");
            result.ValidationLog.Add("Validating SYS-03...");

            try
            {
                // Verify device enumeration
                var inputDevices = await _audioDeviceService.GetInputDevicesAsync();
                result.Sys03_DevicesTested = inputDevices.Count;
                
                // Verify default device
                var defaultDevice = await _audioDeviceService.GetDefaultInputDeviceAsync();
                bool hasDefault = defaultDevice != null;
                
                // Verify permissions
                var permissionStatus = await _permissionService.CheckMicrophonePermissionAsync();
                result.Sys03_PermissionsVerified = permissionStatus == MicrophonePermissionStatus.Granted;
                
                // If there are devices, try to "switch" to the first one (as a test)
                bool switchSuccess = true;
                if (inputDevices.Count > 0)
                {
                    switchSuccess = await _audioDeviceService.SwitchDeviceAsync(inputDevices[0].Id);
                }

                result.Sys03_AudioDeviceSelection_Success = hasDefault && result.Sys03_PermissionsVerified && switchSuccess;
                result.ValidationLog.Add($"SYS-03 Result: {inputDevices.Count} devices found. Permissions: {permissionStatus}. Switch Test: {switchSuccess}");
                
                if (!result.Sys03_AudioDeviceSelection_Success)
                {
                    if (!hasDefault) result.Errors.Add("SYS-03: No default input device found.");
                    if (!result.Sys03_PermissionsVerified) result.Errors.Add($"SYS-03: Microphone permission not granted ({permissionStatus}).");
                    if (!switchSuccess) result.Errors.Add("SYS-03: Failed to switch to available audio device.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating SYS-03");
                result.Sys03_AudioDeviceSelection_Success = false;
                result.Errors.Add($"SYS-03 Error: {ex.Message}");
            }
        }

        public string GeneratePhase04Report(Phase04ValidationResult result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Phase 04 Validation Report");
            sb.AppendLine($"**Date:** {DateTime.Now}");
            sb.AppendLine($"**Status:** {(result.IsPass ? "PASSED" : "FAILED")}");
            sb.AppendLine($"**Overall Success Rate:** {result.OverallSuccessRate:F1}%");
            sb.AppendLine($"**Duration:** {result.Duration}");
            sb.AppendLine();

            sb.AppendLine("## Requirement Status");
            sb.AppendLine($"| Requirement | Description | Status | Success Rate |");
            sb.AppendLine($"| --- | --- | --- | --- |");
            sb.AppendLine($"| CORE-03 | Text Injection | {(result.Core03_TextInjection_Success ? "PASS" : "FAIL")} | {result.Core03_SuccessRate:F1}% |");
            sb.AppendLine($"| SYS-02 | Settings Management | {(result.Sys02_SettingsManagement_Success ? "PASS" : "FAIL")} | {(result.Sys02_SettingsVerified >= 3 ? 100 : 0)}% |");
            sb.AppendLine($"| SYS-03 | Audio & Permissions | {(result.Sys03_AudioDeviceSelection_Success ? "PASS" : "FAIL")} | {(result.Sys03_AudioDeviceSelection_Success ? 100 : 0)}% |");
            sb.AppendLine();

            if (result.CrossAppResult != null)
            {
                sb.AppendLine("## CORE-03 Detailed Results");
                sb.AppendLine($"| Application | Process | Status | Latency |");
                sb.AppendLine($"| --- | --- | --- | --- |");
                foreach (var app in result.CrossAppResult.ApplicationResults)
                {
                    sb.AppendLine($"| {app.DisplayName} | {app.ProcessName} | {(app.IsSuccess ? "PASS" : "FAIL")} | {app.AverageLatency.TotalMilliseconds:F0}ms |");
                }
                sb.AppendLine();
            }

            if (result.Errors.Count > 0)
            {
                sb.AppendLine("## Identified Issues");
                foreach (var error in result.Errors)
                {
                    sb.AppendLine($"- {error}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## Validation Log");
            foreach (var log in result.ValidationLog)
            {
                sb.AppendLine($"- {log}");
            }

            return sb.ToString();
        }
    }
}
