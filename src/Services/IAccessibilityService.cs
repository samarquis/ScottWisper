using System;
using System.Windows;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for managing application accessibility and assistive technology integration
    /// </summary>
    public interface IAccessibilityService
    {
        /// <summary>
        /// Checks if high contrast mode is currently enabled
        /// </summary>
        bool IsHighContrastEnabled();
        
        /// <summary>
        /// Ensures a specific UI element is focused and announced by screen readers
        /// </summary>
        void FocusAndAnnounce(UIElement element, string announcement);
        
        /// <summary>
        /// Sets automation properties for an element to improve screen reader support
        /// </summary>
        void SetAccessibilityLabels(DependencyObject element, string name, string? description = null);
        
        /// <summary>
        /// Notifies when accessibility settings have changed
        /// </summary>
        event EventHandler? AccessibilitySettingsChanged;
        
        /// <summary>
        /// Validates that a window meets minimum accessibility requirements
        /// </summary>
        bool ValidateWindowAccessibility(Window window);
    }
}
