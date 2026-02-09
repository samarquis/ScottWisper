using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using WhisperKey.Configuration;
using WhisperKey.Services;
using WhisperKey.Repositories;
using WhisperKey.Services.Validation;
using WhisperKey.Services.Memory;
using WhisperKey.Services.Database;
using WhisperKey.Services.Recovery;
using WhisperKey.Infrastructure.SmokeTesting;
using WhisperKey.Infrastructure.SmokeTesting.Reporting;
using WhisperKey.Infrastructure.SmokeTesting.HealthChecks;
using Microsoft.Extensions.Logging.Console;
using Serilog.Extensions.Logging;
using Serilog.Core;

namespace WhisperKey.Bootstrap
{
    /// <summary>
    /// Configures dependency injection services for the application
    /// </summary>
    public static class ServiceConfiguration
    {
        /// <summary>
        /// Configures and returns the service provider with structured logging and correlation services
        /// </summary>
        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            
            // Configure Serilog for structured logging with correlation IDs
            ConfigureSerilog(services);
            
            // Configure configuration
            ConfigureConfiguration(services);
            
            // Register correlation service for request tracking (must be registered early)
            services.AddSingleton<ICorrelationService, CorrelationService>();
            
            // Register structured logging service (depends on correlation service)
            services.AddSingleton<IStructuredLoggingService, StructuredLoggingService>();
            
            // Register application services
            RegisterApplicationServices(services);
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Initialize Serilog logger
            Log.Logger = ConfigureSerilogLogger().CreateLogger();
            
            return serviceProvider;
        }
        
        /// <summary>
        /// Configures Serilog for structured logging with correlation ID enrichment
        /// </summary>
        private static void ConfigureSerilog(IServiceCollection services)
        {
            var logger = ConfigureSerilogLogger().CreateLogger();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConsole();
            });
            services.AddSerilog(logger, dispose: true);
        }
        
        /// <summary>
        /// Creates and configures the Serilog logger with structured logging sinks and enrichers
        /// </summary>
        private static LoggerConfiguration ConfigureSerilogLogger()
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WhisperKey",
                "logs",
                "whisperkey-.log");
            
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("WhisperKey", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .Enrich.WithProperty("ProcessId", Environment.ProcessId)
                .Enrich.WithProperty("ThreadId", Environment.CurrentManagedThreadId)
                .Enrich.WithProperty("Application", "WhisperKey")
                .Enrich.WithProperty("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown")
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{CorrelationId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: 100 * 1024 * 1024, // 100MB
                    rollOnFileSizeLimit: true)
                .WriteTo.Seq(
                    serverUrl: "http://localhost:5341",
                    controlLevelSwitch: new LoggingLevelSwitch(LogEventLevel.Information))
                .Filter.ByIncludingOnly(logEvent => 
                    logEvent.Level >= LogEventLevel.Information || 
                    logEvent.Properties.ContainsKey("Error"));
        }
        
        /// <summary>
        /// Configures the configuration system
        /// </summary>
        private static void ConfigureConfiguration(IServiceCollection services)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "WhisperKey", 
                    "usersettings.json"), optional: true, reloadOnChange: true);

            var configuration = builder.Build();

            services.Configure<AudioSettings>(options => configuration.GetSection("Audio").Bind(options));
            services.Configure<TranscriptionSettings>(options => configuration.GetSection("Transcription").Bind(options));
            services.Configure<HotkeySettings>(options => configuration.GetSection("Hotkeys").Bind(options));
            services.Configure<UISettings>(options => configuration.GetSection("UI").Bind(options));
            services.Configure<TextInjectionSettings>(options => configuration.GetSection("TextInjection").Bind(options));
            services.Configure<AppSettings>(options => configuration.Bind(options));
            
            // Also make configuration available for legacy use
            services.AddSingleton<IConfiguration>(configuration);
        }
        
        /// <summary>
        /// Registers application-specific services
        /// </summary>
        private static void RegisterApplicationServices(IServiceCollection services)
        {
            // Core infrastructure services (order is important for dependency resolution)
            services.AddSingleton<ICorrelationService, CorrelationService>();
            services.AddSingleton<IStructuredLoggingService, StructuredLoggingService>();
            services.AddSingleton<IStartupPerformanceService, StartupPerformanceService>();
            services.AddSingleton<IFileSystemService>(sp => new FileSystemService());
            
            // Core services (immediate initialization - lightweight dependencies)
            services.AddSingleton<ISettingsRepository, FileSettingsRepository>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ITextInjection, TextInjectionService>();
            
            // Lazy-loaded heavy services to improve startup performance
            services.AddLazy<IAudioDeviceService, AudioDeviceService>();
            services.AddLazy<IWebhookService, WebhookService>();
            
            // Validation and utility services (lightweight)
            services.AddSingleton<ValidationService>();
            services.AddSingleton<IInputValidationService, InputValidationService>();
            services.AddSingleton<IAudioValidationProvider, AudioValidationProvider>();
            services.AddSingleton<VocabularyService>();
            services.AddSingleton<UserErrorService>();
            services.AddSingleton<ApplicationBootstrapper, ApplicationBootstrapper>();
            services.AddSingleton<SystemTrayService, SystemTrayService>();
            
            // Permission and system services (lightweight)
            services.AddSingleton<IAuditRepository, FileAuditRepository>();
            services.AddSingleton<IAuditLoggingService, AuditLoggingService>();
            services.AddSingleton<ISecurityAlertService, SecurityAlertService>();
            services.AddSingleton<IIntelligentAlertingService, IntelligentAlertingService>();
            services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();
            services.AddSingleton<IDeploymentRollbackService, DeploymentRollbackService>();
            services.AddSingleton<IDeploymentRoutingService, DeploymentRoutingService>();
            services.AddSingleton<IProductionValidationService, ProductionValidationService>();
            
            // Smoke Testing Infrastructure
            services.AddSingleton<SmokeTestConfiguration>();
            services.AddSingleton<SmokeTestEnvironmentManager>();
            services.AddSingleton<SmokeTestResultCollector>();
            services.AddSingleton<SmokeTestReportingService>();
            services.AddSingleton<ProductionSmokeTestOrchestrator>();
            services.AddSingleton<ILogAnalysisService, LogAnalysisService>();
            services.AddSingleton<IBusinessMetricsRepository, JsonBusinessMetricsRepository>();
            services.AddSingleton<IBusinessMetricsService, BusinessMetricsService>();
            services.AddSingleton<IConfigurationManagementService, ConfigurationManagementService>();
            services.AddSingleton<IResponsiveUIService, ResponsiveUIService>();
            services.AddSingleton<IAccessibilityService, AccessibilityService>();
            services.AddSingleton<IUITestAutomationService, UITestAutomationService>();
            services.AddSingleton<ILoadTestingService, LoadTestingService>();
            services.AddSingleton<IGracefulDegradationService, GracefulDegradationService>();
            services.AddSingleton<IErrorReportingService, ErrorReportingService>();
            services.AddSingleton<ILazyInitializationService, LazyInitializationService>();
            services.AddSingleton<IRateLimitingService, RateLimitingService>();
            services.AddSingleton<ICentralizedHealthService, CentralizedHealthService>();
            services.AddSingleton<IAnimationService, AnimationService>();
            services.AddSingleton<IOnboardingService, OnboardingService>();
            
            // Register Health Checkers
            services.AddSingleton<SystemHealthChecker>();
            services.AddSingleton<DatabaseHealthChecker>();
            services.AddSingleton<ExternalServiceHealthChecker>();
            
            services.AddSingleton<ICredentialService, WindowsCredentialService>();
            services.AddSingleton<IApiKeyManagementService, ApiKeyManagementService>();
            services.AddSingleton<ApiKeyRotationService>();
            services.AddSingleton<JsonDatabaseService>();
            services.AddSingleton<IRecoveryPolicyService, RecoveryPolicyService>();
            services.AddSingleton<IInputValidationService, InputValidationService>();
            services.AddSingleton<ISanitizationService, SanitizationService>();
            services.AddSingleton<IByteArrayPool, ByteArrayPool>();
            services.AddSingleton(typeof(IObjectPool<>), typeof(GenericObjectPool<>));
            services.AddSingleton<IPermissionService, PermissionService>();
            services.AddSingleton<IRegistryService, RegistryService>();
            services.AddSingleton<IFeedbackService, FeedbackService>();
            services.AddSingleton<ICommandProcessingService, CommandProcessingService>();
            
            // Lazy-loaded specialized services (heavy initialization)
            services.AddLazy<CostTrackingService, CostTrackingService>();
            services.AddLazy<HotkeyRegistrationService, HotkeyRegistrationService>();
            services.AddLazy<HotkeyProfileManager, HotkeyProfileManager>();
            services.AddLazy<HotkeyConflictDetector, HotkeyConflictDetector>();
            services.AddLazy<Win32HotkeyRegistrar, Win32HotkeyRegistrar>();
            services.AddLazy<HotkeyService, HotkeyService>();
            
            // Configure HttpClient for Whisper API (lightweight, no dependencies)
            services.AddHttpClient("WhisperApi", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "WhisperKey/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // SEC-004: Implement server certificate validation to ensure secure communication
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    if (errors == System.Net.Security.SslPolicyErrors.None)
                    {
                        return true;
                    }

                    // Log certificate validation failures for security auditing
                    Log.Error("SSL Certificate validation failed for {Url}: {Errors}. Subject: {Subject}", 
                        message.RequestUri, errors, cert?.Subject);
                    
                    return false;
                }
            });
        }
        
        /// <summary>
        /// Extension method to register lazy-loaded singleton services.
        /// Improves startup performance by deferring service construction until first use.
        /// </summary>
        /// <typeparam name="TInterface">The service interface type.</typeparam>
        /// <typeparam name="TImplementation">The service implementation type.</typeparam>
        /// <param name="services">The service collection to register with.</param>
        /// <remarks>
        /// This pattern reduces startup time by:
        /// <list type="bullet">
        /// <item><description>Deferring heavy initialization until service is first accessed</description></item>
        /// <item><description>Reducing memory footprint during application startup</description></item>
        /// <item><description>Allowing parallel service construction when dependencies allow</description></item>
        /// </list>
        /// Services are constructed on first access and cached as singletons for subsequent use.
        /// </remarks>
        private static void AddLazy<TInterface, TImplementation>(this IServiceCollection services)
            where TImplementation : class, TInterface
            where TInterface : class
        {
            services.AddSingleton<TInterface>(provider =>
            {
                var lazy = new Lazy<TInterface>(() => (TInterface)provider.GetRequiredService<TImplementation>());
                return lazy.Value;
            });
            
            services.AddSingleton<TImplementation>();
        }
    }
}
