using System;
using System.Threading.Tasks;
namespace WhisperKey.Services
{
    public interface IWhisperService : IDisposable
    {
        event EventHandler? TranscriptionStarted;
        event EventHandler<int>? TranscriptionProgress;
        event EventHandler<string>? TranscriptionCompleted;
        event EventHandler<Exception>? TranscriptionError;
        event EventHandler<UsageStats>? UsageUpdated;

        Task<string> TranscribeAudioAsync(byte[] audioData, string? language = null);
        Task<string> TranscribeAudioFileAsync(string filePath, string? language = null);
        UsageStats GetUsageStats();
        void ResetUsageStats();
    }
}
