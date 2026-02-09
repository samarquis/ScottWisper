using System;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    public interface IAudioCaptureService : IDisposable
    {
        event EventHandler<byte[]>? AudioDataCaptured;
        event EventHandler<Exception>? CaptureError;
        event EventHandler? PermissionRequired;
        event EventHandler? PermissionRetry;
        bool IsCapturing { get; }
        Task<bool> StartCaptureAsync();
        Task StopCaptureAsync();
        void OpenWindowsMicrophoneSettings();
        Task<bool> RetryWithPermissionAsync();
        byte[]? GetCapturedAudio();
        void ClearCapturedAudio();
    }
}
