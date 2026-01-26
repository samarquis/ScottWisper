using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScottWisper.Services
{
    public interface IAudioDeviceService
    {
        event EventHandler<AudioDeviceEventArgs> DeviceConnected;
        event EventHandler<AudioDeviceEventArgs> DeviceDisconnected;
        event EventHandler<AudioDeviceEventArgs> DefaultDeviceChanged;
        
        Task<List<AudioDevice>> GetInputDevicesAsync();
        Task<List<AudioDevice>> GetOutputDevicesAsync();
        Task<AudioDevice> GetDefaultInputDeviceAsync();
        Task<AudioDevice> GetDefaultOutputDeviceAsync();
        Task<bool> TestDeviceAsync(string deviceId);
        Task<AudioDeviceCapabilities> GetDeviceCapabilitiesAsync(string deviceId);
        Task<AudioDevice?> GetDeviceByIdAsync(string deviceId);
        bool IsDeviceCompatible(string deviceId);
    }

    public class AudioDeviceService : IAudioDeviceService, IDisposable
    {
        private readonly MMDeviceEnumerator _enumerator;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        public event EventHandler<AudioDeviceEventArgs>? DeviceConnected;
        public event EventHandler<AudioDeviceEventArgs>? DeviceDisconnected;
        public event EventHandler<AudioDeviceEventArgs>? DefaultDeviceChanged;

        public AudioDeviceService()
        {
            _enumerator = new MMDeviceEnumerator();
            _enumerator.DeviceAdded += OnDeviceAdded;
            _enumerator.DeviceRemoved += OnDeviceRemoved;
            _enumerator.DefaultDeviceChanged += OnDefaultDeviceChanged;
        }

        public async Task<List<AudioDevice>> GetInputDevicesAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return new List<AudioDevice>();

                    try
                    {
                        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                        return devices.Select(CreateAudioDevice).Where(d => d != null).ToList()!;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error enumerating input devices: {ex.Message}");
                        return new List<AudioDevice>();
                    }
                }
            });
        }

        public async Task<List<AudioDevice>> GetOutputDevicesAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return new List<AudioDevice>();

                    try
                    {
                        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                        return devices.Select(CreateAudioDevice).Where(d => d != null).ToList()!;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error enumerating output devices: {ex.Message}");
                        return new List<AudioDevice>();
                    }
                }
            });
        }

        public async Task<AudioDevice> GetDefaultInputDeviceAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(AudioDeviceService));

                    try
                    {
                        var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                        return CreateAudioDevice(device)!;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting default input device: {ex.Message}");
                        throw new InvalidOperationException("Unable to get default input device", ex);
                    }
                }
            });
        }

        public async Task<AudioDevice> GetDefaultOutputDeviceAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(AudioDeviceService));

                    try
                    {
                        var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                        return CreateAudioDevice(device)!;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting default output device: {ex.Message}");
                        throw new InvalidOperationException("Unable to get default output device", ex);
                    }
                }
            });
        }

        public async Task<bool> TestDeviceAsync(string deviceId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return false;

                    try
                    {
                        var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                            .FirstOrDefault(d => d.ID == deviceId);
                        
                        if (device == null) return false;

                        // Test basic device functionality
                        using (var waveIn = new WaveInEvent())
                        {
                            waveIn.DeviceNumber = int.Parse(deviceID: GetDeviceNumber(device.ID));
                            
                            // Test if we can configure the device for basic recording
                            waveIn.WaveFormat = new WaveFormat(16000, 1); // 16kHz mono for speech recognition
                            
                            // Try to start recording briefly
                            waveIn.StartRecording();
                            await Task.Delay(100); // Very brief test
                            waveIn.StopRecording();
                            
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error testing device {deviceId}: {ex.Message}");
                        return false;
                    }
                }
            });
        }

        public async Task<AudioDeviceCapabilities> GetDeviceCapabilitiesAsync(string deviceId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(AudioDeviceService));

                    try
                    {
                        var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                            .FirstOrDefault(d => d.ID == deviceId);
                        
                        if (device == null)
                            throw new ArgumentException($"Device with ID {deviceId} not found");

                        var capabilities = new AudioDeviceCapabilities();
                        
                        // Get supported formats
                        var formats = device.AudioClient?.MixFormat;
                        if (formats != null)
                        {
                            capabilities.SampleRate = formats.SampleRate;
                            capabilities.Channels = formats.Channels;
                            capabilities.BitsPerSample = formats.BitsPerSample;
                        }

                        // Get device properties
                        var properties = device.Properties;
                        if (properties != null)
                        {
                            capabilities.DeviceFriendlyName = properties[PropertyKeys.PKEY_Device_FriendlyName].Value as string ?? device.FriendlyName;
                            capabilities.DeviceDescription = properties[PropertyKeys.PKEY_Device_DeviceDesc].Value as string ?? "";
                        }

                        return capabilities;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting device capabilities for {deviceId}: {ex.Message}");
                        throw new InvalidOperationException("Unable to get device capabilities", ex);
                    }
                }
            });
        }

        public async Task<AudioDevice?> GetDeviceByIdAsync(string deviceId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_disposed) return null;

                    try
                    {
                        var device = _enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)
                            .FirstOrDefault(d => d.ID == deviceId);
                        
                        return device != null ? CreateAudioDevice(device) : null;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting device by ID {deviceId}: {ex.Message}");
                        return null;
                    }
                }
            });
        }

        public bool IsDeviceCompatible(string deviceId)
        {
            lock (_lockObject)
            {
                if (_disposed) return false;

                try
                {
                    var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                        .FirstOrDefault(d => d.ID == deviceId);
                    
                    if (device == null) return false;

                    // Check if device supports required format for speech recognition
                    var format = device.AudioClient?.MixFormat;
                    if (format == null) return false;

                    // Speech recognition typically needs: 16kHz or higher, mono, 16-bit or higher
                    return format.SampleRate >= 16000 && 
                           (format.Channels == 1 || format.Channels == 2) && 
                           format.BitsPerSample >= 16;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking device compatibility for {deviceId}: {ex.Message}");
                    return false;
                }
            }
        }

        private AudioDevice? CreateAudioDevice(MMDevice device)
        {
            try
            {
                return new AudioDevice
                {
                    Id = device.ID,
                    Name = device.FriendlyName,
                    Description = device.DeviceFriendlyName,
                    DataFlow = device.DataFlow == DataFlow.Capture ? DeviceType.Input : DeviceType.Output,
                    State = device.State == DeviceState.Active ? DeviceState.Active : DeviceState.Disabled,
                    IsDefault = false // Will be determined by context
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating audio device object: {ex.Message}");
                return null;
            }
        }

        private int GetDeviceNumber(string deviceId)
        {
            try
            {
                // Extract device number from device ID for WaveInEvent
                var parts = deviceId.Split('{', '}');
                if (parts.Length >= 2 && int.TryParse(parts[1], out var number))
                {
                    return number;
                }
                return 0; // Default device
            }
            catch
            {
                return 0;
            }
        }

        private void OnDeviceAdded(object? sender, DeviceEventArgs e)
        {
            var device = CreateAudioDevice(e.Device);
            if (device != null)
            {
                DeviceConnected?.Invoke(this, new AudioDeviceEventArgs(device));
            }
        }

        private void OnDeviceRemoved(object? sender, DeviceEventArgs e)
        {
            var device = CreateAudioDevice(e.Device);
            if (device != null)
            {
                DeviceDisconnected?.Invoke(this, new AudioDeviceEventArgs(device));
            }
        }

        private void OnDefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
        {
            var device = CreateAudioDevice(e.Device);
            if (device != null)
            {
                DefaultDeviceChanged?.Invoke(this, new AudioDeviceEventArgs(device));
            }
        }

        public void Dispose()
        {
            lock (_lockObject)
            {
                if (!_disposed)
                {
                    _enumerator.DeviceAdded -= OnDeviceAdded;
                    _enumerator.DeviceRemoved -= OnDeviceRemoved;
                    _enumerator.DefaultDeviceChanged -= OnDefaultDeviceChanged;
                    _enumerator?.Dispose();
                    _disposed = true;
                }
            }
        }
    }

    // Supporting classes
    public class AudioDevice
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DeviceType DataFlow { get; set; }
        public DeviceState State { get; set; }
        public bool IsDefault { get; set; }
    }

    public enum DeviceType
    {
        Input,
        Output
    }

    public enum DeviceState
    {
        Active,
        Disabled,
        Unplugged,
        NotPresent
    }

    public class AudioDeviceCapabilities
    {
        public int SampleRate { get; set; } = 44100;
        public int Channels { get; set; } = 2;
        public int BitsPerSample { get; set; } = 16;
        public string DeviceFriendlyName { get; set; } = string.Empty;
        public string DeviceDescription { get; set; } = string.Empty;
        public List<WaveFormat> SupportedFormats { get; set; } = new List<WaveFormat>();
    }

    public class AudioDeviceEventArgs : EventArgs
    {
        public AudioDevice Device { get; }

        public AudioDeviceEventArgs(AudioDevice device)
        {
            Device = device;
        }
    }
}