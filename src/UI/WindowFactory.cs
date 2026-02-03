using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WhisperKey.Services;

namespace WhisperKey.UI
{
    /// <summary>
    /// Factory interface for creating windows - abstracts direct window instantiation
    /// </summary>
    public interface IWindowFactory
    {
        /// <summary>
        /// Creates the main application window
        /// </summary>
        Window CreateMainWindow();
        
        /// <summary>
        /// Creates the settings window
        /// </summary>
        Window CreateSettingsWindow(ISettingsService settingsService, IAudioDeviceService audioDeviceService);
        
        /// <summary>
        /// Creates the transcription window
        /// </summary>
        Window CreateTranscriptionWindow();
        
        /// <summary>
        /// Creates the listening indicator window
        /// </summary>
        Window CreateListeningIndicator();
        
        /// <summary>
        /// Creates the profile dialog
        /// </summary>
        Window CreateProfileDialog(ISettingsService settingsService);
    }

    /// <summary>
    /// Concrete implementation of window factory using DI
    /// </summary>
    public class WindowFactory : IWindowFactory
    {
        private readonly IServiceProvider _serviceProvider;
        
        public WindowFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }
        
        public Window CreateMainWindow()
        {
            return new MainWindow();
        }
        
        public Window CreateSettingsWindow(ISettingsService settingsService, IAudioDeviceService audioDeviceService)
        {
            return new SettingsWindow(settingsService, audioDeviceService);
        }
        
        public Window CreateTranscriptionWindow()
        {
            var window = new TranscriptionWindow();
            
            // Initialize services if available
            var whisperService = _serviceProvider.GetService<IWhisperService>();
            var costService = _serviceProvider.GetService<CostTrackingService>();
            
            if (whisperService != null && costService != null)
            {
                window.InitializeServices(whisperService, costService);
            }
            
            return window;
        }
        
        public Window CreateListeningIndicator()
        {
            throw new NotSupportedException("ListeningIndicator is a UserControl, not a Window. Use CreateListeningIndicatorControl() instead.");
        }
        
        public Window CreateProfileDialog(ISettingsService settingsService)
        {
            return new ProfileDialog("Profile Management");
        }
    }

    /// <summary>
    /// Extension methods for registering window factory in DI container
    /// </summary>
    public static class WindowFactoryExtensions
    {
        public static IServiceCollection AddWindowFactory(this IServiceCollection services)
        {
            services.AddSingleton<IWindowFactory, WindowFactory>();
            return services;
        }
    }
}
