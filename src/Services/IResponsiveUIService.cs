using System;
using System.Windows;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for responsive UI management and DPI-aware scaling
    /// </summary>
    public interface IResponsiveUIService
    {
        /// <summary>
        /// Gets the current DPI scale for the primary screen
        /// </summary>
        double GetCurrentScale();
        
        /// <summary>
        /// Calculates a scaled value based on current DPI
        /// </summary>
        double GetScaledValue(double baseValue);
        
        /// <summary>
        /// Scales a Thickness (Margin/Padding) for current DPI
        /// </summary>
        Thickness GetScaledThickness(Thickness baseThickness);
        
        /// <summary>
        /// Notifies when the DPI scaling has changed
        /// </summary>
        event EventHandler<double>? ScalingChanged;
        
        /// <summary>
        /// Manages DPI change events for a specific window
        /// </summary>
        void RegisterWindow(Window window);
    }
}
