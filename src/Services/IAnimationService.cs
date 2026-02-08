using System;
using System.Windows;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for managing application UI animations and micro-interactions
    /// </summary>
    public interface IAnimationService
    {
        /// <summary>
        /// Fades an element in smoothly
        /// </summary>
        void FadeIn(UIElement element, TimeSpan duration);
        
        /// <summary>
        /// Fades an element out smoothly
        /// </summary>
        void FadeOut(UIElement element, TimeSpan duration);
        
        /// <summary>
        /// Starts a pulsing animation on an element (e.g., for recording status)
        /// </summary>
        void StartPulse(UIElement element);
        
        /// <summary>
        /// Stops all animations on an element
        /// </summary>
        void StopAnimations(UIElement element);
        
        /// <summary>
        /// Slides an element into view from a direction
        /// </summary>
        void SlideIn(UIElement element, Direction direction, TimeSpan duration);
    }

    public enum Direction
    {
        Left,
        Right,
        Top,
        Bottom
    }
}
