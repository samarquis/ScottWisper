using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using WhisperKey.Configuration;
using WhisperKey.Services;
using WhisperKey.Repositories;
using WhisperKey;

namespace WhisperKey.Bootstrap
{
    /// <summary>
    /// Configures dependency injection services for the application
    /// </summary>
    public static class ServiceConfiguration
    {
        /// <summary>
        /// Configures and returns the service provider
        /// </summary>
        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            
            // Configure configuration
            ConfigureConfiguration(services);
            
            // Register application services
            RegisterApplicationServices(services);
            
            return services.BuildServiceProvider();
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
            // Core services
            services.AddSingleton<ISettingsRepository, FileSettingsRepository>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ITextInjection, TextInjectionService>();
            services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
            
            // Validation services
            services.AddSingleton<ValidationService>();
            services.AddSingleton<VocabularyService>();
            services.AddSingleton<UserErrorService>();
            
            // Management services
            services.AddSingleton<ApplicationBootstrapper, ApplicationBootstrapper>();
            services.AddSingleton<SystemTrayService, SystemTrayService>();
            
            // Permission services
            services.AddSingleton<IPermissionService, PermissionService>();
            services.AddSingleton<IRegistryService, RegistryService>();
            services.AddSingleton<IFileSystemService>(sp => new FileSystemService()); 
            
            // Feedback
            services.AddSingleton<IFeedbackService, FeedbackService>();
            
            // Command Processing
            services.AddSingleton<ICommandProcessingService, CommandProcessingService>();
            
            // Cost Tracking
            services.AddSingleton<CostTrackingService, CostTrackingService>();
            
            // Hotkey services
            services.AddSingleton<HotkeyRegistrationService, HotkeyRegistrationService>();
            services.AddSingleton<HotkeyProfileManager, HotkeyProfileManager>();
            services.AddSingleton<HotkeyConflictDetector, HotkeyConflictDetector>();
            services.AddSingleton<Win32HotkeyRegistrar, Win32HotkeyRegistrar>();
            services.AddSingleton<HotkeyService, HotkeyService>();
            
            // Configure HttpClient for Whisper API
            services.AddHttpClient("WhisperApi", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });
        }
    }
}