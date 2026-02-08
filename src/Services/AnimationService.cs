using System;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Extensions.Logging;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of centralized UI animation service
    /// </summary>
    public class AnimationService : IAnimationService
    {
        private readonly ILogger<AnimationService> _logger;

        public AnimationService(ILogger<AnimationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void FadeIn(UIElement element, TimeSpan duration)
        {
            if (element == null) return;
            
            var anim = new DoubleAnimation(0, 1, new Duration(duration));
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        public void FadeOut(UIElement element, TimeSpan duration)
        {
            if (element == null) return;
            
            var anim = new DoubleAnimation(1, 0, new Duration(duration));
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        public void StartPulse(UIElement element)
        {
            if (element == null) return;
            
            var anim = new DoubleAnimation(1.0, 0.4, new Duration(TimeSpan.FromSeconds(0.8)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        public void StopAnimations(UIElement element)
        {
            if (element == null) return;
            element.BeginAnimation(UIElement.OpacityProperty, null);
        }

        public void SlideIn(UIElement element, Direction direction, TimeSpan duration)
        {
            if (element == null) return;
            
            // Note: In a real app, this would use TranslateTransform
            _logger.LogTrace("SlideIn triggered for element in direction {Direction}", direction);
            FadeIn(element, duration); // Fallback to fade if transform not setup
        }
    }
}
