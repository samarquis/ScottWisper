using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Configuration;

namespace WhisperKey.Services
{
    public interface IAudioQualityService
    {
        Task<AudioQualityMetrics> AnalyzeAudioQualityAsync(string deviceId, int durationMs = 3000);
        Task<DeviceCompatibilityScore> ScoreDeviceCompatibilityAsync(string deviceId);
        Task<bool> TestDeviceLatencyAsync(string deviceId);
        Task<List<DeviceRecommendation>> GetDeviceRecommendationsAsync();
    }

    public interface IAudioMonitoringService
    {
        event EventHandler<AudioDeviceEventArgs>? DeviceConnected;
        event EventHandler<AudioDeviceEventArgs>? DeviceDisconnected;
        event EventHandler<AudioDeviceEventArgs>? DefaultDeviceChanged;
        
        Task StartRealTimeMonitoringAsync(string deviceId);
        Task StopRealTimeMonitoringAsync();
        Task<bool> MonitorDeviceChangesAsync();
        void StopDeviceChangeMonitoring();
    }
}
