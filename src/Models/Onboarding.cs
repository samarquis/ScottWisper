using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Tracks the user's progress through application onboarding
    /// </summary>
    public class OnboardingState
    {
        public string UserId { get; set; } = Environment.UserName;
        public bool WelcomeCompleted { get; set; }
        public bool HotkeyTutorialCompleted { get; set; }
        public bool TranscriptionTutorialCompleted { get; set; }
        public DateTime? LastStartedAt { get; set; }
        public Dictionary<string, bool> CompletedModules { get; set; } = new();
    }
}
