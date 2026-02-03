using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for enterprise deployment service
    /// </summary>
    public interface IEnterpriseDeploymentService
    {
        /// <summary>
        /// Detect if WhisperKey is installed
        /// </summary>
        Task<InstallationInfo> DetectInstallationAsync();
        
        /// <summary>
        /// Generate deployment configuration
        /// </summary>
        Task<EnterpriseDeploymentConfig> GenerateDeploymentConfigAsync(string organizationName, DeploymentType type);
        
        /// <summary>
        /// Install silently using MSI
        /// </summary>
        Task<DeploymentResult> InstallSilentlyAsync(string msiPath, EnterpriseDeploymentConfig? config = null);
        
        /// <summary>
        /// Uninstall using product code
        /// </summary>
        Task<DeploymentResult> UninstallAsync(string? productCode = null);
        
        /// <summary>
        /// Generate GPO deployment script
        /// </summary>
        Task<string> GenerateGpoScriptAsync(GpoDeploymentConfig config);
        
        /// <summary>
        /// Get installed version
        /// </summary>
        Task<string?> GetInstalledVersionAsync();
        
        /// <summary>
        /// Check if upgrade is needed
        /// </summary>
        Task<bool> IsUpgradeNeededAsync(string targetVersion);
        
        /// <summary>
        /// Configure enterprise settings after installation
        /// </summary>
        Task<bool> ConfigureEnterpriseSettingsAsync(EnterpriseDeploymentConfig config);
        
        /// <summary>
        /// Validate license key
        /// </summary>
        Task<bool> ValidateLicenseKeyAsync(string licenseKey);
        
        /// <summary>
        /// Get deployment log
        /// </summary>
        Task<string?> GetDeploymentLogAsync();
    }
    
    /// <summary>
    /// Enterprise deployment service for MSI/GPO deployment
    /// </summary>
    public class EnterpriseDeploymentService : IEnterpriseDeploymentService
    {
        private readonly ILogger<EnterpriseDeploymentService> _logger;
        private readonly string _msiProductCode = "{a3ef09de-38b1-4068-87c0-9a7d707d1a88}";
        
        public EnterpriseDeploymentService(ILogger<EnterpriseDeploymentService> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Detect if WhisperKey is installed
        /// </summary>
        public Task<InstallationInfo> DetectInstallationAsync()
        {
            var info = new InstallationInfo();
            
            try
            {
                // Check registry for installation
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WhisperKey");
                if (key != null)
                {
                    info.IsInstalled = true;
                    info.InstallPath = key.GetValue("InstallPath")?.ToString();
                    info.Version = key.GetValue("Version")?.ToString();
                    info.ProductCode = _msiProductCode;
                    
                    // Check if per-user or per-machine
                    info.Scope = File.Exists(Path.Combine(info.InstallPath ?? "", "WhisperKey.exe"))
                        ? InstallationScope.PerMachine
                        : InstallationScope.PerUser;
                }
                
                // Check uninstall registry for install date
                using var uninstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WhisperKey");
                if (uninstallKey != null)
                {
                    var installDateStr = uninstallKey.GetValue("InstallDate")?.ToString();
                    if (!string.IsNullOrEmpty(installDateStr) && DateTime.TryParseExact(installDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var installDate))
                    {
                        info.InstallDate = installDate;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting installation");
            }
            
            return Task.FromResult(info);
        }
        
        /// <summary>
        /// Generate deployment configuration
        /// </summary>
        public Task<EnterpriseDeploymentConfig> GenerateDeploymentConfigAsync(string organizationName, DeploymentType type)
        {
            var config = new EnterpriseDeploymentConfig
            {
                OrganizationName = organizationName,
                Type = type,
                Scope = InstallationScope.PerMachine,
                AllUsers = true,
                CreateDesktopShortcut = false,
                CreateStartMenuShortcut = true,
                SilentOptions = new SilentInstallOptions
                {
                    Enabled = true,
                    SuppressUI = true,
                    SuppressReboot = true
                },
                Settings = new PreconfiguredSettings
                {
                    EnableAutoPunctuation = true,
                    EnableVoiceCommands = true,
                    EnableAuditLogging = true,
                    ComplianceFramework = "General",
                    RetentionDays = 30
                }
            };
            
            _logger.LogInformation("Generated {DeploymentType} deployment config for {Organization}", type, organizationName);
            
            return Task.FromResult(config);
        }
        
        /// <summary>
        /// Install silently using MSI
        /// </summary>
        public async Task<DeploymentResult> InstallSilentlyAsync(string msiPath, EnterpriseDeploymentConfig? config = null)
        {
            var result = new DeploymentResult();
            var startTime = DateTime.Now;
            
            try
            {
                if (!File.Exists(msiPath))
                {
                    result.Success = false;
                    result.ErrorMessage = $"MSI file not found: {msiPath}";
                    return result;
                }
                
                // Build MSI arguments using ArgumentList to prevent command injection
                var logPath = Path.Combine(Path.GetTempPath(), $"WhisperKey_Install_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                
                _logger.LogInformation("Starting silent installation from: {MsiPath}", msiPath);
                
                // Execute MSI
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "msiexec.exe",
                        Arguments = null, // Use ArgumentList instead
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                
                // Add arguments safely using ArgumentList (prevents command injection)
                process.StartInfo.ArgumentList.Add("/i");
                process.StartInfo.ArgumentList.Add(msiPath);
                process.StartInfo.ArgumentList.Add("/qn");
                process.StartInfo.ArgumentList.Add("/norestart");
                
                // Add properties from config (safely escaped by ArgumentList)
                if (config != null)
                {
                    if (!string.IsNullOrEmpty(config.LicenseKey))
                        process.StartInfo.ArgumentList.Add($"LICENSEKEY={config.LicenseKey}");
                    
                    if (!string.IsNullOrEmpty(config.OrganizationName))
                        process.StartInfo.ArgumentList.Add($"ORGANIZATION={config.OrganizationName}");
                    
                    if (!string.IsNullOrEmpty(config.Settings.ApiKey))
                        process.StartInfo.ArgumentList.Add($"APIKEY={config.Settings.ApiKey}");
                    
                    if (!string.IsNullOrEmpty(config.Webhook.EndpointUrl))
                        process.StartInfo.ArgumentList.Add($"WEBHOOKURL={config.Webhook.EndpointUrl}");
                    
                    process.StartInfo.ArgumentList.Add($"ENABLEAUDITLOGGING={(config.Settings.EnableAuditLogging ? "1" : "0")}");
                    process.StartInfo.ArgumentList.Add($"COMPLIANCEFRAMEWORK={config.Settings.ComplianceFramework}");
                    process.StartInfo.ArgumentList.Add($"DESKTOPSHORTCUT={(config.CreateDesktopShortcut ? "1" : "0")}");
                }
                
                // Add logging
                process.StartInfo.ArgumentList.Add("/l*v");
                process.StartInfo.ArgumentList.Add(logPath);
                
                process.Start();
                await process.WaitForExitAsync();
                
                result.ExitCode = process.ExitCode;
                result.Success = process.ExitCode == 0;
                result.Duration = DateTime.Now - startTime;
                result.LogFile = logPath;
                
                if (result.Success)
                {
                    var installInfo = await DetectInstallationAsync();
                    result.InstallPath = installInfo.InstallPath;
                    result.Version = installInfo.Version;
                    
                    _logger.LogInformation("Installation completed successfully in {Duration}", result.Duration);
                }
                else
                {
                    result.ErrorMessage = $"Installation failed with exit code: {process.ExitCode}";
                    _logger.LogError("Installation failed with exit code {ExitCode}", process.ExitCode);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Error during installation");
            }
            
            return result;
        }
        
        /// <summary>
        /// Uninstall using product code
        /// </summary>
        public async Task<DeploymentResult> UninstallAsync(string? productCode = null)
        {
            var result = new DeploymentResult();
            var startTime = DateTime.Now;
            
            try
            {
                productCode ??= _msiProductCode;
                
                var arguments = $"/x \"{productCode}\" /qn /norestart";
                
                // Add logging
                var logPath = Path.Combine(Path.GetTempPath(), $"WhisperKey_Uninstall_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                arguments += $" /l*v \"{logPath}\"";
                
                _logger.LogInformation("Starting uninstallation with product code: {ProductCode}", productCode);
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "msiexec.exe",
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                
                process.Start();
                await process.WaitForExitAsync();
                
                result.ExitCode = process.ExitCode;
                result.Success = process.ExitCode == 0 || process.ExitCode == 1605; // 1605 = product not installed
                result.Duration = DateTime.Now - startTime;
                result.LogFile = logPath;
                
                if (result.Success)
                {
                    _logger.LogInformation("Uninstallation completed successfully in {Duration}", result.Duration);
                }
                else
                {
                    result.ErrorMessage = $"Uninstallation failed with exit code: {process.ExitCode}";
                    _logger.LogError("Uninstallation failed with exit code {ExitCode}", process.ExitCode);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Error during uninstallation");
            }
            
            return result;
        }
        
        /// <summary>
        /// Generate GPO deployment script
        /// </summary>
        public Task<string> GenerateGpoScriptAsync(GpoDeploymentConfig config)
        {
            // Build PowerShell script using StringBuilder to avoid quote issues
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# WhisperKey GPO Deployment Script");
            sb.AppendLine("# Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("param(");
            sb.AppendLine("    [Parameter(Mandatory=$true)]");
            sb.AppendLine("    [string]$MsiPath,");
            sb.AppendLine("    ");
            sb.AppendLine("    [Parameter(Mandatory=$false)]");
            sb.AppendLine("    [string]$OrganizationalUnit,");
            sb.AppendLine("    ");
            sb.AppendLine("    [Parameter(Mandatory=$false)]");
            sb.AppendLine("    [string]$TargetComputers,");
            sb.AppendLine("    ");
            sb.AppendLine("    [Parameter(Mandatory=$false)]");
            sb.AppendLine("    [string]$TargetUsers,");
            sb.AppendLine("    ");
            sb.AppendLine("    [Parameter(Mandatory=$false)]");
            sb.AppendLine("    [switch]$ForceReinstall,");
            sb.AppendLine("    ");
            sb.AppendLine("    [Parameter(Mandatory=$false)]");
            sb.AppendLine("    [switch]$UninstallExisting");
            sb.AppendLine(")");
            sb.AppendLine();
            sb.AppendLine("# Create GPO");
            sb.AppendLine("$GPOName = 'WhisperKey-Deployment'");
            sb.AppendLine();
            sb.AppendLine("Write-Host \"Creating GPO: $GPOName\" -ForegroundColor Green");
            sb.AppendLine();
            sb.AppendLine("try {");
            sb.AppendLine("    # Import GroupPolicy module");
            sb.AppendLine("    Import-Module GroupPolicy -ErrorAction Stop");
            sb.AppendLine("    ");
            sb.AppendLine("    # Create new GPO");
            sb.AppendLine("    $GPO = New-GPO -Name $GPOName -ErrorAction Stop");
            sb.AppendLine("    ");
            sb.AppendLine("    Write-Host \"GPO created successfully\" -ForegroundColor Green");
            sb.AppendLine("    Write-Host \"GPO ID: $($GPO.Id)\" -ForegroundColor Cyan");
            sb.AppendLine("}");
            sb.AppendLine("catch {");
            sb.AppendLine("    Write-Error \"Failed to create GPO: $_\"");
            sb.AppendLine("    exit 1");
            sb.AppendLine("}");
            
            return Task.FromResult(sb.ToString());
        }
        
        /// <summary>
        /// Get installed version
        /// </summary>
        public async Task<string?> GetInstalledVersionAsync()
        {
            var info = await DetectInstallationAsync();
            return info.Version;
        }
        
        /// <summary>
        /// Check if upgrade is needed
        /// </summary>
        public async Task<bool> IsUpgradeNeededAsync(string targetVersion)
        {
            var currentVersion = await GetInstalledVersionAsync();
            
            if (string.IsNullOrEmpty(currentVersion))
                return true; // Not installed, need to install
            
            try
            {
                var current = Version.Parse(currentVersion);
                var target = Version.Parse(targetVersion);
                
                return target > current;
            }
            catch
            {
                return true; // Parse error, assume upgrade needed
            }
        }
        
        /// <summary>
        /// Configure enterprise settings after installation
        /// </summary>
        public async Task<bool> ConfigureEnterpriseSettingsAsync(EnterpriseDeploymentConfig config)
        {
            try
            {
                var info = await DetectInstallationAsync();
                if (!info.IsInstalled)
                {
                    _logger.LogError("Cannot configure settings - application not installed");
                    return false;
                }
                
                // Write configuration to ProgramData
                var configDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "WhisperKey",
                    "Enterprise");
                
                Directory.CreateDirectory(configDir);
                
                var configPath = Path.Combine(configDir, "deployment-config.json");
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(configPath, json).ConfigureAwait(false);
                
                _logger.LogInformation("Enterprise settings configured at {ConfigPath}", configPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring enterprise settings");
                return false;
            }
        }
        
        /// <summary>
        /// Validate license key
        /// </summary>
        public Task<bool> ValidateLicenseKeyAsync(string licenseKey)
        {
            // Basic validation - in production, this would call a license server
            if (string.IsNullOrEmpty(licenseKey))
                return Task.FromResult(false);
            
            // Check format: XXXX-XXXX-XXXX-XXXX
            var isValid = licenseKey.Length == 19 && 
                         licenseKey.Split('-').Length == 4;
            
            return Task.FromResult(isValid);
        }
        
        /// <summary>
        /// Get deployment log
        /// </summary>
        public Task<string?> GetDeploymentLogAsync()
        {
            // Find the most recent installation log
            var tempPath = Path.GetTempPath();
            var logFiles = Directory.GetFiles(tempPath, "WhisperKey_Install_*.log")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();
            
            if (logFiles.Any())
            {
                return Task.FromResult<string?>(logFiles.First());
            }
            
            return Task.FromResult<string?>(null);
        }
    }
}