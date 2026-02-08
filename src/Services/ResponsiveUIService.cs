using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of responsive UI and DPI scaling service
    /// </summary>
    public class ResponsiveUIService : IResponsiveUIService
    {
        private readonly ILogger<ResponsiveUIService> _logger;
        private double _currentScale = 1.0;

        public event EventHandler<double>? ScalingChanged;

        public ResponsiveUIService(ILogger<ResponsiveUIService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            UpdateCurrentScale();
        }

        public double GetCurrentScale() => _currentScale;

        public double GetScaledValue(double baseValue) => baseValue * _currentScale;

        public Thickness GetScaledThickness(Thickness baseThickness)
        {
            return new Thickness(
                baseThickness.Left * _currentScale,
                baseThickness.Top * _currentScale,
                baseThickness.Right * _currentScale,
                baseThickness.Bottom * _currentScale);
        }

        public void RegisterWindow(Window window)
        {
            if (window == null) return;
            
            window.DpiChanged += (s, e) =>
            {
                _currentScale = e.NewDpi.DpiScaleX;
                _logger.LogInformation("DPI Change detected for window {Title}. New Scale: {Scale}", window.Title, _currentScale);
                ScalingChanged?.Invoke(this, _currentScale);
                
                // Clear the global converter cache if it exists
                DpiScaleConverter.ClearCache();
            };
        }

        private void UpdateCurrentScale()
        {
            try
            {
                if (Application.Current?.MainWindow != null)
                {
                    var dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow);
                    _currentScale = dpi.DpiScaleX;
                }
                else
                {
                    // Fallback to primary screen DPI
                    _currentScale = DpiScaleConverter.GetScaleFactor();
                }
            }
            catch
            {
                _currentScale = 1.0;
            }
        }
    }
}
