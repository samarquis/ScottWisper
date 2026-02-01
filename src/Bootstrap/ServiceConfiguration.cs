using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using ScottWisper.Configuration;
using ScottWisper.Services;

namespace ScottWisper.Bootstrap
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
                    "ScottWisper", 
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
            // Core services - register interfaces with implementations
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ITextInjection, TextInjectionService>();
            
            // Concrete services - all dependencies injected via constructors
            services.AddSingleton<WhisperService>();
            services.AddSingleton<CostTrackingService>();
            services.AddSingleton<AudioCaptureService>();
            services.AddSingleton<HotkeyService>();
            services.AddSingleton<SystemTrayService>();
            services.AddSingleton<TranscriptionWindow>();
            
            // Register FeedbackService with its interface and concrete type
            // SystemTrayService dependency is injected via constructor
            services.AddSingleton<FeedbackService>();
            services.AddSingleton<IFeedbackService>(sp => sp.GetRequiredService<FeedbackService>());
            
            // Bootstrapper - all dependencies injected via constructor
            services.AddSingleton<ApplicationBootstrapper>();
        }
    }
}
