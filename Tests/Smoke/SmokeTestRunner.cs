using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using WhisperKey.Tests.Smoke;

namespace WhisperKey.Tests.Smoke
{
    /// <summary>
    /// Smoke test runner for production deployment validation
    /// </summary>
    public class SmokeTestRunner
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                // Setup configuration
                var configuration = BuildConfiguration();
                
                // Setup logging
                var logger = SetupLogging(configuration);
                
                logger.Information("Starting WhisperKey Production Smoke Test Runner");
                logger.Information("Environment: {Environment}", Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Unknown");
                logger.Information("Build Version: {BuildVersion}", Environment.GetEnvironmentVariable("BUILD_VERSION") ?? "Unknown");

                // Setup dependency injection
                var services = new ServiceCollection();
                ConfigureServices(services, configuration, logger);
                var serviceProvider = services.BuildServiceProvider();

                // Create smoke test configuration
                var smokeTestConfig = CreateSmokeTestConfiguration(configuration);

                // Run smoke tests
                var orchestrator = serviceProvider.GetRequiredService<ProductionSmokeTestOrchestrator>();
                var report = await orchestrator.RunProductionSmokeTestsAsync();

                // Export results
                var outputDirectory = configuration["SmokeTest:OutputDirectory"] ?? "./smoke-test-results";
                await orchestrator.ExportSmokeTestResultsAsync(report, outputDirectory);

                // Validate production readiness
                var isProductionReady = orchestrator.IsProductionReady(report);
                
                logger.Information("Smoke test execution completed");
                logger.Information("Results: {PassedTests}/{TotalTests} passed ({SuccessRate:F1}%)", 
                    report.TestResults.PassedTests, report.TestResults.TotalTests, report.TestResults.SuccessRate);
                logger.Information("Production Ready: {IsProductionReady}", isProductionReady);
                logger.Information("Reports exported to: {OutputDirectory}", outputDirectory);

                // Return appropriate exit code
                return isProductionReady ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Smoke test runner failed: {ex.Message}");
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                return 2;
            }
        }

        private static IConfigurationRoot BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(Environment.GetCommandLineArgs());

            return builder.Build();
        }

        private static ILogger<SmokeTestRunner> SetupLogging(IConfiguration configuration)
        {
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "WhisperKeySmokeTests")
                .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Unknown")
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "logs/smoke-tests-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

            // Configure log level from environment
            var logLevel = configuration["Logging:LogLevel:Default"] ?? "Information";
            logConfig.MinimumLevel = Enum.Parse<Serilog.Events.LogEventLevel>(logLevel, true);

            var serilogLogger = logConfig.CreateLogger();
            Log.Logger = serilogLogger;

            var loggerFactory = new LoggerFactory()
                .AddSerilog(serilogLogger);

            return loggerFactory.CreateLogger<SmokeTestRunner>();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration, ILogger<SmokeTestRunner> logger)
        {
            // Add configuration
            services.AddSingleton(configuration);
            
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddSerilog();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Add WhisperKey services (simplified for smoke tests)
            services.AddSingleton<WhisperKey.Services.ISettingsService, WhisperKey.Services.SettingsService>();
            services.AddSingleton<WhisperKey.Services.IAuthenticationService, WhisperKey.Services.AuthenticationService>();
            services.AddSingleton<WhisperKey.Services.IAudioDeviceService, WhisperKey.Services.AudioDeviceService>();
            services.AddSingleton<WhisperKey.Services.ITextInjection, WhisperKey.Services.TextInjectionService>();

            // Add smoke test services
            var smokeTestConfig = CreateSmokeTestConfiguration(configuration);
            services.AddSingleton(smokeTestConfig);
            services.AddSingleton<SmokeTestEnvironmentManager>();
            services.AddSingleton<SmokeTestResultCollector>();
            services.AddSingleton<SmokeTestReportingService>();
            services.AddSingleton<ProductionSmokeTestOrchestrator>();
        }

        private static SmokeTestConfiguration CreateSmokeTestConfiguration(IConfiguration configuration)
        {
            var config = new SmokeTestConfiguration();

            // Load timeouts from configuration
            if (int.TryParse(configuration["SmokeTest:DefaultTestTimeoutSeconds"], out var defaultTimeout))
            {
                config.DefaultTestTimeoutSeconds = defaultTimeout;
            }

            if (int.TryParse(configuration["SmokeTest:HealthCheckTimeoutSeconds"], out var healthTimeout))
            {
                config.HealthCheckTimeoutSeconds = healthTimeout;
            }

            if (int.TryParse(configuration["SmokeTest:WorkflowTestTimeoutSeconds"], out var workflowTimeout))
            {
                config.WorkflowTestTimeoutSeconds = workflowTimeout;
            }

            // Load retry settings
            if (int.TryParse(configuration["SmokeTest:MaxRetryAttempts"], out var maxRetries))
            {
                config.MaxRetryAttempts = maxRetries;
            }

            if (int.TryParse(configuration["SmokeTest:RetryDelayMs"], out var retryDelay))
            {
                config.RetryDelayMs = retryDelay;
            }

            // Load parallel execution settings
            if (bool.TryParse(configuration["SmokeTest:EnableParallelExecution"], out var enableParallel))
            {
                config.EnableParallelExecution = enableParallel;
            }

            if (int.TryParse(configuration["SmokeTest:MaxParallelism"], out var maxParallelism))
            {
                config.MaxParallelism = maxParallelism;
            }

            // Load performance thresholds
            var performanceSection = configuration.GetSection("SmokeTest:PerformanceThresholds");
            if (performanceSection.Exists())
            {
                if (int.TryParse(performanceSection["MaxAudioProcessingMs"], out var maxAudioProcessing))
                {
                    config.PerformanceThresholds.MaxAudioProcessingMs = maxAudioProcessing;
                }

                if (int.TryParse(performanceSection["MaxTextInjectionMs"], out var maxTextInjection))
                {
                    config.PerformanceThresholds.MaxTextInjectionMs = maxTextInjection;
                }

                if (int.TryParse(performanceSection["MaxSettingsLoadMs"], out var maxSettingsLoad))
                {
                    config.PerformanceThresholds.MaxSettingsLoadMs = maxSettingsLoad;
                }

                if (int.TryParse(performanceSection["MaxAuthenticationMs"], out var maxAuth))
                {
                    config.PerformanceThresholds.MaxAuthenticationMs = maxAuth;
                }

                if (int.TryParse(performanceSection["MaxMemoryUsageMb"], out var maxMemory))
                {
                    config.PerformanceThresholds.MaxMemoryUsageMb = maxMemory;
                }

                if (double.TryParse(performanceSection["MaxCpuUsagePercent"], out var maxCpu))
                {
                    config.PerformanceThresholds.MaxCpuUsagePercent = maxCpu;
                }
            }

            // Load security settings
            var securitySection = configuration.GetSection("SmokeTest:SecuritySettings");
            if (securitySection.Exists())
            {
                if (bool.TryParse(securitySection["RequireSOC2Compliance"], out var requireSOC2))
                {
                    config.SecuritySettings.RequireSOC2Compliance = requireSOC2;
                }

                if (bool.TryParse(securitySection["RequireAuditLogging"], out var requireAudit))
                {
                    config.SecuritySettings.RequireAuditLogging = requireAudit;
                }

                if (bool.TryParse(securitySection["RequireSecureCredentialStorage"], out var requireCredentials))
                {
                    config.SecuritySettings.RequireSecureCredentialStorage = requireCredentials;
                }

                if (bool.TryParse(securitySection["RequirePermissionSystem"], out var requirePermissions))
                {
                    config.SecuritySettings.RequirePermissionSystem = requirePermissions;
                }

                if (bool.TryParse(securitySection["RequireApiKeyRotation"], out var requireRotation))
                {
                    config.SecuritySettings.RequireApiKeyRotation = requireRotation;
                }

                if (bool.TryParse(securitySection["RequireSecurityAlerts"], out var requireAlerts))
                {
                    config.SecuritySettings.RequireSecurityAlerts = requireAlerts;
                }
            }

            // Load enabled categories from configuration
            var enabledCategories = configuration["SmokeTest:EnabledCategories"];
            if (!string.IsNullOrEmpty(enabledCategories))
            {
                config.EnabledCategories.Clear();
                var categories = enabledCategories.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var category in categories)
                {
                    if (Enum.TryParse<SmokeTestCategory>(category.Trim(), true, out var smokeCategory))
                    {
                        config.EnabledCategories.Add(smokeCategory);
                    }
                }
            }

            // Load service endpoints
            var endpointsSection = configuration.GetSection("SmokeTest:ServiceEndpoints");
            if (endpointsSection.Exists())
            {
                foreach (var endpoint in endpointsSection.GetChildren())
                {
                    config.ServiceEndpoints[endpoint.Key] = endpoint.Value;
                }
            }

            return config;
        }
    }
}