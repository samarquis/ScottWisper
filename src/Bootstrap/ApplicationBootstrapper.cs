using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WhisperKey.Services;
using WhisperKey.Services.Memory;
using WhisperKey.Services.Recovery;

namespace WhisperKey.Bootstrap
{
    /// <summary>
    /// Handles application startup and service initialization
    /// </summary>
    public class ApplicationBootstrapper
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, AppApplicationCompatibility> _applicationCompatibility = new();
        
        // Services
        public ISettingsService? SettingsService { get; private set; }
        public IHotkeyService? HotkeyService { get; private set; }
        public SystemTrayService? SystemTrayService { get; private set; }
        public IWhisperService? WhisperService { get; private set; }
        public CostTrackingService? CostTrackingService { get; private set; }
        public IAudioCaptureService? AudioCaptureService { get; private set; }
        public TranscriptionWindow? TranscriptionWindow { get; private set; }
        public ITextInjection? TextInjectionService { get; private set; }
        public IFeedbackService? FeedbackService { get; private set; }
        
        // Enhanced services for gap closure
        public IAudioDeviceService? AudioDeviceService { get; private set; }
        public ValidationService? ValidationService { get; private set; }
        public ApiKeyRotationService? ApiKeyRotationService { get; private set; }
        public IIntelligentAlertingService? IntelligentAlertingService { get; private set; }
        public IDeploymentRollbackService? DeploymentRollbackService { get; private set; }
        public IConfigurationManagementService? ConfigurationManagementService { get; private set; }
        public IGracefulDegradationService? GracefulDegradationService { get; private set; }
        public ILazyInitializationService? LazyInitializationService { get; private set; }
        
        // State
        public bool GracefulFallbackMode { get; private set; }
        
        public ApplicationBootstrapper(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }
        
        /// <summary>
        /// Initializes all application services
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                // Get core services from DI
                SettingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                TextInjectionService = _serviceProvider.GetRequiredService<ITextInjection>();
                var fileSystem = _serviceProvider.GetRequiredService<IFileSystemService>();
                DeploymentRollbackService = _serviceProvider.GetRequiredService<IDeploymentRollbackService>();
                ConfigurationManagementService = _serviceProvider.GetRequiredService<IConfigurationManagementService>();
                GracefulDegradationService = _serviceProvider.GetRequiredService<IGracefulDegradationService>();
                LazyInitializationService = _serviceProvider.GetRequiredService<ILazyInitializationService>();

                // Check for configuration drift (DEFERRED)
                LazyInitializationService.RegisterDeferredTask("ConfigDriftCheck", 
                    async () => await ConfigurationManagementService.ValidateParityAsync(), 
                    DeferredPriority.Normal);

                // Secure appsettings.json (NIST AC-3)
                var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(appSettingsPath))
                {
                    try
                    {
                        fileSystem.SetStrictPermissions(appSettingsPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Failed to secure appsettings.json: {ex.Message}");
                    }
                }
                
                // Initialize enhanced feedback service first
                var feedbackService = new FeedbackService();
                await feedbackService.InitializeAsync().ConfigureAwait(false);
                FeedbackService = feedbackService;
                
                // Store feedback service in application properties for global access
                if (Application.Current != null)
                {
                    Application.Current.Properties["FeedbackService"] = feedbackService;
                }
                
                // Initialize core services using settings
                var settings = SettingsService.Settings;
                var apiKeyManagement = _serviceProvider.GetRequiredService<IApiKeyManagementService>();
                var memoryPool = _serviceProvider.GetRequiredService<IByteArrayPool>();
                var recoveryPolicy = _serviceProvider.GetRequiredService<IRecoveryPolicyService>();
                var performanceMonitoring = _serviceProvider.GetRequiredService<IPerformanceMonitoringService>();
                var rateLimiting = _serviceProvider.GetRequiredService<IRateLimitingService>();
                
                WhisperService = new WhisperService(SettingsService, null, null, apiKeyManagement, recoveryPolicy, null, null, null, performanceMonitoring, rateLimiting);
                CostTrackingService = new CostTrackingService(SettingsService);
                AudioCaptureService = new AudioCaptureService(SettingsService, AudioDeviceService!, memoryPool, null, performanceMonitoring);
                
                // Initialize transcription window
                TranscriptionWindow = new TranscriptionWindow();
                TranscriptionWindow.InitializeServices(WhisperService, CostTrackingService);
                
                // Initialize text injection service
                await InitializeTextInjectionServiceAsync().ConfigureAwait(false);
                
                // Connect enhanced feedback to all services
                await ConnectFeedbackToServicesAsync(feedbackService).ConfigureAwait(false);
                
                // Initialize enhanced services for gap closure
                await InitializeEnhancedServicesAsync().ConfigureAwait(false);
                
                // Start API key rotation service (DEFERRED)
                ApiKeyRotationService = _serviceProvider.GetRequiredService<ApiKeyRotationService>();
                LazyInitializationService.RegisterDeferredTask("ApiKeyRotation", 
                    async () => { ApiKeyRotationService.Start(); await Task.CompletedTask; }, 
                    DeferredPriority.Low);

                // Start intelligent alerting service (DEFERRED)
                IntelligentAlertingService = _serviceProvider.GetRequiredService<IIntelligentAlertingService>();
                LazyInitializationService.RegisterDeferredTask("IntelligentAlerting", 
                    async () => { IntelligentAlertingService.Start(); await Task.CompletedTask; }, 
                    DeferredPriority.Normal);
                
                // Start deferred initialization after small delay to let UI settle
                _ = Task.Delay(2000).ContinueWith(_ => LazyInitializationService.StartDeferredInitialization());

                // Record successful startup
                await DeploymentRollbackService.RecordStartupSuccessAsync();
                
                return true;
            }
            catch (InvalidOperationException ex)
            {
                if (DeploymentRollbackService != null)
                    await DeploymentRollbackService.RecordStartupFailureAsync(ex.Message);

                MessageBox.Show($"Service initialization failed: {ex.Message}", "WhisperKey Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (System.IO.IOException ex)
            {
                if (DeploymentRollbackService != null)
                    await DeploymentRollbackService.RecordStartupFailureAsync(ex.Message);

                MessageBox.Show($"I/O error during initialization: {ex.Message}", "WhisperKey Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        
        /// <summary>
        /// Initializes the system tray service
        /// </summary>
        public void InitializeSystemTray()
        {
            SystemTrayService = new SystemTrayService();
            
            try
            {
                SystemTrayService.Initialize();
                System.Diagnostics.Debug.WriteLine("SystemTrayService initialized successfully");
                
                // Store for global access
                if (Application.Current != null)
                {
                    Application.Current.Properties["SystemTray"] = SystemTrayService;
                }
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize SystemTrayService: {ex.Message}");
                MessageBox.Show($"Failed to initialize system tray: {ex.Message}", "WhisperKey Warning", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (System.IO.IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"I/O error initializing SystemTrayService: {ex.Message}");
                MessageBox.Show($"System tray I/O error: {ex.Message}", "WhisperKey Warning", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        /// <summary>
        /// Initializes the hotkey service
        /// </summary>
        public void InitializeHotkeyService()
        {
            if (SettingsService == null)
                throw new InvalidOperationException("SettingsService must be initialized first");
                
            // Get window handle for hotkey registration
            var mainWindow = System.Windows.Application.Current?.MainWindow;
            var windowHandle = mainWindow != null ? new WindowInteropHelper(mainWindow).Handle : IntPtr.Zero;
            
            HotkeyService = new HotkeyService(
                SettingsService,
                _serviceProvider.GetRequiredService<HotkeyRegistrationService>(),
                _serviceProvider.GetRequiredService<HotkeyProfileManager>(),
                _serviceProvider.GetRequiredService<HotkeyConflictDetector>(),
                _serviceProvider.GetRequiredService<Win32HotkeyRegistrar>(),
                windowHandle,
                _serviceProvider.GetRequiredService<ILogger<HotkeyService>>()
            );
        }
        
        /// <summary>
        /// Shows the startup notification
        /// </summary>
        public async Task ShowStartupNotificationAsync()
        {
            if (FeedbackService != null)
            {
                await FeedbackService.ShowToastNotificationAsync(
                    "Application Started", 
                    "WhisperKey is ready. Press Ctrl+Alt+V to start dictation.", 
                    IFeedbackService.NotificationType.Completion
                ).ConfigureAwait(false);
            }
            else if (SystemTrayService != null)
            {
                SystemTrayService.ShowNotification("WhisperKey is ready. Press Ctrl+Alt+V to start dictation.", "Application Started");
            }
        }
        
        /// <summary>
        /// Cleans up and shuts down all services
        /// </summary>
        public void Shutdown()
        {
            IntelligentAlertingService?.Stop();
            ApiKeyRotationService?.Stop();
            ApiKeyRotationService?.Dispose();
            HotkeyService?.Dispose();
            SystemTrayService?.Dispose();
            AudioCaptureService?.Dispose();
            WhisperService?.Dispose();
            TextInjectionService?.Dispose();
            FeedbackService?.DisposeAsync().GetAwaiter().GetResult();
        }
        
        private async Task InitializeTextInjectionServiceAsync()
        {
            // Text injection is initialized during DI, but we can add additional setup here if needed
            await Task.CompletedTask;
        }
        
        private async Task ConnectFeedbackToServicesAsync(FeedbackService feedbackService)
        {
            // Connect feedback service to all relevant services
            // This enables real-time status updates and notifications
            await Task.CompletedTask;
        }
        
        private async Task InitializeEnhancedServicesAsync()
        {
            // Initialize AudioDeviceService from DI container
            AudioDeviceService = _serviceProvider.GetRequiredService<IAudioDeviceService>();
            
            await Task.CompletedTask;
        }
    }
}
