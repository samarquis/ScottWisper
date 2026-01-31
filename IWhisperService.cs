using System;
using System.Threading.Tasks;

namespace ScottWisper
{
    public interface IWhisperService : IDisposable
    {
        event EventHandler? TranscriptionStarted;
        event EventHandler<int>? TranscriptionProgress;
        event EventHandler<string>? TranscriptionCompleted;
        event EventHandler<Exception>? TranscriptionError;
        Task<string> TranscribeAudioAsync(byte[] audioData, string? language = null);
    }
}
