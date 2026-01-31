using System;
using System.Threading.Tasks;

namespace ScottWisper
{
    public interface IAudioCaptureService : IDisposable
    {
        event EventHandler<byte[]>? AudioDataCaptured;
        event EventHandler<Exception>? CaptureError;
        event EventHandler? PermissionRequired;
        bool IsCapturing { get; }
        Task<bool> StartCaptureAsync();
        Task StopCaptureAsync();
    }
}
