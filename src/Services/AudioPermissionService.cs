using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Services
{
    public interface IAudioPermissionService
    {
        Task<MicrophonePermissionStatus> CheckMicrophonePermissionAsync();
        Task<bool> RequestMicrophonePermissionAsync();
        Task<string> GetPermissionStatusAsync();
        Task<bool> OpenWindowsPrivacySettingsAsync();
        Task<bool> MonitorPermissionChangesAsync();
        Task<List<PermissionRequestRecord>> GetPermissionRequestHistoryAsync();
    }

    public class AudioPermissionService : IAudioPermissionService
    {
        private readonly IPermissionService _permissionService;

        public AudioPermissionService(IPermissionService permissionService)
        {
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        }

        public Task<MicrophonePermissionStatus> CheckMicrophonePermissionAsync()
            => _permissionService.CheckMicrophonePermissionAsync();

        public Task<bool> RequestMicrophonePermissionAsync()
            => _permissionService.RequestMicrophonePermissionAsync();

        public Task<string> GetPermissionStatusAsync()
            => _permissionService.GetPermissionStatusAsync();

        public Task<bool> OpenWindowsPrivacySettingsAsync()
            => _permissionService.OpenWindowsPrivacySettingsAsync();

        public Task<bool> MonitorPermissionChangesAsync()
            => _permissionService.MonitorPermissionChangesAsync();

        public Task<List<PermissionRequestRecord>> GetPermissionRequestHistoryAsync()
            => _permissionService.GetPermissionRequestHistoryAsync();
    }
}
