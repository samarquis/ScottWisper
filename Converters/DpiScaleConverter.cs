using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WhisperKey
{
    /// <summary>
    /// Converts pixel values to DPI-scaled values for responsive layouts.
    /// This ensures UI elements scale correctly on high-DPI displays.
    /// </summary>
    public class DpiScaleConverter : IValueConverter
    {
        private static double? _cachedScaleFactor;
        
        /// <summary>
        /// Gets the current DPI scale factor for the primary screen.
        /// Uses DpiScale from .NET Core 3.0+ for accurate per-monitor DPI awareness.
        /// </summary>
        public static double GetScaleFactor()
        {
            if (_cachedScaleFactor.HasValue)
                return _cachedScaleFactor.Value;
            
            try
            {
                // Get the DPI scale from the current presentation source
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    var presentationSource = PresentationSource.FromVisual(mainWindow);
                    if (presentationSource?.CompositionTarget != null)
                    {
                        var transform = presentationSource.CompositionTarget.TransformFromDevice;
                        _cachedScaleFactor = transform.M11;
                        return _cachedScaleFactor.Value;
                    }
                }
                
                // Fallback: use the system's DPI scale (available in .NET Core 3.0+)
                var dpiScale = VisualTreeHelper.GetDpi(mainWindow ?? new Window());
                _cachedScaleFactor = dpiScale.DpiScaleX;
                return _cachedScaleFactor.Value;
            }
            catch
            {
                // Default to 1.0 (96 DPI) if anything fails
                return 1.0;
            }
        }
        
        /// <summary>
        /// Clears the cached scale factor, forcing recalculation on next access.
        /// Call this when the window moves to a different monitor with different DPI.
        /// </summary>
        public static void ClearCache()
        {
            _cachedScaleFactor = null;
        }
        
        /// <summary>
        /// Converts a base pixel value to a DPI-scaled value.
        /// </summary>
        /// <param name="value">The base pixel value (typically at 96 DPI)</param>
        /// <param name="targetType">The target type (should be double)</param>
        /// <param name="parameter">Optional scale factor override</param>
        /// <param name="culture">The culture info</param>
        /// <returns>The scaled value</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double baseValue = 0;
            
            if (value is double d)
                baseValue = d;
            else if (value is int i)
                baseValue = i;
            else if (value is string s && double.TryParse(s, out double parsed))
                baseValue = parsed;
            
            // Get scale factor - either from parameter or calculated
            double scaleFactor = 1.0;
            if (parameter is double paramDouble)
                scaleFactor = paramDouble;
            else if (parameter is string paramString && double.TryParse(paramString, out double paramParsed))
                scaleFactor = paramParsed;
            else
                scaleFactor = GetScaleFactor();
            
            // Return the scaled value
            var result = baseValue * scaleFactor;
            
            // If target type is GridLength (for row/column definitions), wrap it
            if (targetType == typeof(GridLength))
                return new GridLength(result);
            
            return result;
        }
        
        /// <summary>
        /// Converts back is not supported for DPI scaling.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("DpiScaleConverter does not support ConvertBack");
        }
    }
    
    /// <summary>
    /// Converts a base font size to a DPI-scaled font size.
    /// </summary>
    public class FontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double baseSize = 12; // Default font size
            
            if (value is double d)
                baseSize = d;
            else if (value is int i)
                baseSize = i;
            
            // Font sizes scale slightly less aggressively than other elements
            // This maintains readability without making text too large
            double scaleFactor = DpiScaleConverter.GetScaleFactor();
            double adjustedScale = 1.0 + (scaleFactor - 1.0) * 0.5; // Scale at 50% rate
            
            return baseSize * adjustedScale;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("FontSizeConverter does not support ConvertBack");
        }
    }
}
