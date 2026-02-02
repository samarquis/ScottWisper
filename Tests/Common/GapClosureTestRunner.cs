using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Services;
using WhisperKey.Validation;

namespace WhisperKey.Tests.Common
{
    /// <summary>
    /// Orchestrates systematic validation of Phase 04 gap closure requirements.
    /// </summary>
    public class GapClosureTestRunner
    {
        private readonly Phase04Validator _validator;
        private readonly ILogger<GapClosureTestRunner> _logger;

        public GapClosureTestRunner(Phase04Validator validator, ILogger<GapClosureTestRunner> logger)
        {
            _validator = validator;
            _logger = logger;
        }

        /// <summary>
        /// Executes full Phase 04 validation and generates report.
        /// </summary>
        public async Task<bool> RunPhase04ValidationAsync()
        {
            _logger.LogInformation("Starting Systematic Phase 04 Gap Closure Validation");

            try
            {
                // Run complete validation
                var result = await _validator.RunCompleteValidationAsync();

                // Generate Markdown report
                var report = _validator.GeneratePhase04Report(result);
                
                // Save report to file
                string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Phase04ValidationReport.md");
                await File.WriteAllTextAsync(reportPath, report);
                
                _logger.LogInformation("Phase 04 Validation Report generated at: {Path}", reportPath);
                
                // Log summary to console/logger
                _logger.LogInformation("Validation Result: {Status}", result.IsPass ? "PASSED" : "FAILED");
                _logger.LogInformation("Overall Success Rate: {Rate:F1}%", result.OverallSuccessRate);
                
                return result.IsPass;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during systematic validation execution");
                return false;
            }
        }

        /// <summary>
        /// Runs validation for a specific requirement area.
        /// </summary>
        public async Task<bool> RunRequirementValidationAsync(string requirementId)
        {
            var result = new Phase04ValidationResult { StartTime = DateTime.UtcNow };
            
            _logger.LogInformation("Running validation for requirement: {Req}", requirementId);

            switch (requirementId.ToUpper())
            {
                case "CORE-03":
                    await _validator.ValidateCore03_TextInjectionAsync(result);
                    return result.Core03_TextInjection_Success;
                
                case "SYS-02":
                    await _validator.ValidateSys02_SettingsManagementAsync(result);
                    return result.Sys02_SettingsManagement_Success;
                
                case "SYS-03":
                    await _validator.ValidateSys03_AudioDeviceSelectionAsync(result);
                    return result.Sys03_AudioDeviceSelection_Success;
                
                default:
                    _logger.LogWarning("Unknown requirement ID: {Req}", requirementId);
                    return false;
            }
        }
    }
}
