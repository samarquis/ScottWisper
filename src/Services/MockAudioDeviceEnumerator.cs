using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WhisperKey.Services
{
    /// <summary>
    /// Mock implementation of IMMDeviceWrapper for testing purposes.
    /// Allows creating test devices without actual hardware.
    /// </summary>
    public class MockMMDeviceWrapper : IMMDeviceWrapper
    {
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public string FriendlyName { get; set; } = "Mock Device";
        public string DeviceFriendlyName { get; set; } = "Mock Audio Device";
        public DataFlow DataFlow { get; set; } = DataFlow.Capture;
        public NAudio.CoreAudioApi.DeviceState State { get; set; } = NAudio.CoreAudioApi.DeviceState.Active;
        public IMMDevicePropertiesWrapper? Properties { get; set; }
        public IAudioClientWrapper? AudioClient { get; set; }

        public MockMMDeviceWrapper()
        {
        }

        public MockMMDeviceWrapper(string id, string friendlyName, DataFlow dataFlow = DataFlow.Capture)
        {
            ID = id;
            FriendlyName = friendlyName;
            DataFlow = dataFlow;
            DeviceFriendlyName = friendlyName;
        }

        /// <summary>
        /// Creates a mock input device with specified characteristics.
        /// </summary>
        public static MockMMDeviceWrapper CreateInputDevice(string id, string name, int sampleRate = 16000, int channels = 1, int bitsPerSample = 16)
        {
            return new MockMMDeviceWrapper
            {
                ID = id,
                FriendlyName = name,
                DeviceFriendlyName = name,
                DataFlow = DataFlow.Capture,
                State = NAudio.CoreAudioApi.DeviceState.Active,
                AudioClient = new MockAudioClientWrapper(sampleRate, channels, bitsPerSample)
            };
        }

        /// <summary>
        /// Creates a mock output device with specified characteristics.
        /// </summary>
        public static MockMMDeviceWrapper CreateOutputDevice(string id, string name, int sampleRate = 48000, int channels = 2, int bitsPerSample = 16)
        {
            return new MockMMDeviceWrapper
            {
                ID = id,
                FriendlyName = name,
                DeviceFriendlyName = name,
                DataFlow = DataFlow.Render,
                State = NAudio.CoreAudioApi.DeviceState.Active,
                AudioClient = new MockAudioClientWrapper(sampleRate, channels, bitsPerSample)
            };
        }
    }

    /// <summary>
    /// Mock implementation of IAudioClientWrapper for testing.
    /// </summary>
    public class MockAudioClientWrapper : IAudioClientWrapper
    {
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _bitsPerSample;

        public MockAudioClientWrapper(int sampleRate = 16000, int channels = 1, int bitsPerSample = 16)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _bitsPerSample = bitsPerSample;
        }

        public AudioWaveFormat? MixFormat
        {
            get
            {
                var nAudioFormat = new NAudio.Wave.WaveFormat(_sampleRate, _bitsPerSample, _channels);
                return new AudioWaveFormat(new WaveFormatWrapper(nAudioFormat));
            }
        }
    }

    /// <summary>
    /// Mock implementation of IAudioDeviceEnumerator for testing.
    /// Allows complete simulation of device enumeration without hardware dependencies.
    /// </summary>
    public class MockAudioDeviceEnumerator : IAudioDeviceEnumerator
    {
        private readonly List<IMMDeviceWrapper> _devices = new();
        private readonly List<IMMNotificationClient> _callbacks = new();
        private bool _disposed;

        /// <summary>
        /// Gets the list of mock devices. Modify this to simulate different device configurations.
        /// </summary>
        public List<IMMDeviceWrapper> Devices => _devices;

        /// <summary>
        /// Sets the default input device for testing.
        /// </summary>
        public IMMDeviceWrapper? DefaultInputDevice { get; set; }

        /// <summary>
        /// Sets the default output device for testing.
        /// </summary>
        public IMMDeviceWrapper? DefaultOutputDevice { get; set; }

        /// <summary>
        /// When set to true, operations will throw COMException to simulate hardware errors.
        /// </summary>
        public bool SimulateHardwareError { get; set; }

        /// <summary>
        /// When set to true, operations will throw UnauthorizedAccessException to simulate permission errors.
        /// </summary>
        public bool SimulatePermissionDenied { get; set; }

        /// <summary>
        /// When set to true, operations will throw InvalidOperationException to simulate system errors.
        /// </summary>
        public bool SimulateSystemError { get; set; }

        /// <summary>
        /// Event raised when devices are enumerated (for test verification).
        /// </summary>
        public event EventHandler<DataFlow>? DevicesEnumerated;

        /// <summary>
        /// Event raised when default endpoint is requested (for test verification).
        /// </summary>
        public event EventHandler<(DataFlow, Role)>? DefaultEndpointRequested;

        public MockAudioDeviceEnumerator()
        {
        }

        public IEnumerable<IMMDeviceWrapper> EnumerateAudioEndPoints(DataFlow dataFlow, NAudio.CoreAudioApi.DeviceState deviceState)
        {
            ThrowIfDisposed();
            SimulateErrorsIfNeeded();

            DevicesEnumerated?.Invoke(this, dataFlow);

            return _devices.Where(d =>
                (dataFlow == DataFlow.All || d.DataFlow == dataFlow) &&
                (deviceState == NAudio.CoreAudioApi.DeviceState.All || d.State == deviceState));
        }

        public IMMDeviceWrapper GetDefaultAudioEndpoint(DataFlow dataFlow, Role role)
        {
            ThrowIfDisposed();
            SimulateErrorsIfNeeded();

            DefaultEndpointRequested?.Invoke(this, (dataFlow, role));

            var device = dataFlow switch
            {
                DataFlow.Capture => DefaultInputDevice,
                DataFlow.Render => DefaultOutputDevice,
                _ => DefaultInputDevice ?? DefaultOutputDevice
            };

            if (device == null)
            {
                throw new InvalidOperationException($"No default {dataFlow} device available");
            }

            return device;
        }

        public void RegisterEndpointNotificationCallback(IMMNotificationClient notificationCallback)
        {
            ThrowIfDisposed();
            if (!_callbacks.Contains(notificationCallback))
            {
                _callbacks.Add(notificationCallback);
            }
        }

        public void UnregisterEndpointNotificationCallback(IMMNotificationClient notificationCallback)
        {
            ThrowIfDisposed();
            _callbacks.Remove(notificationCallback);
        }

        /// <summary>
        /// Simulates a device being added (triggers notifications).
        /// </summary>
        public void SimulateDeviceAdded(IMMDeviceWrapper device)
        {
            _devices.Add(device);
            foreach (var callback in _callbacks.ToList())
            {
                try
                {
                    callback.OnDeviceAdded(device.ID);
                }
                catch
                {
                    // Ignore callback errors in tests
                }
            }
        }

        /// <summary>
        /// Simulates a device being removed (triggers notifications).
        /// </summary>
        public void SimulateDeviceRemoved(IMMDeviceWrapper device)
        {
            _devices.Remove(device);
            foreach (var callback in _callbacks.ToList())
            {
                try
                {
                    callback.OnDeviceRemoved(device.ID);
                }
                catch
                {
                    // Ignore callback errors in tests
                }
            }
        }

        /// <summary>
        /// Simulates default device change (triggers notifications).
        /// </summary>
        public void SimulateDefaultDeviceChanged(DataFlow dataFlow, Role role, string deviceId)
        {
            foreach (var callback in _callbacks.ToList())
            {
                try
                {
                    callback.OnDefaultDeviceChanged(dataFlow, role, deviceId);
                }
                catch
                {
                    // Ignore callback errors in tests
                }
            }
        }

        /// <summary>
        /// Clears all devices and resets state.
        /// </summary>
        public void Reset()
        {
            _devices.Clear();
            _callbacks.Clear();
            DefaultInputDevice = null;
            DefaultOutputDevice = null;
            SimulateHardwareError = false;
            SimulatePermissionDenied = false;
            SimulateSystemError = false;
        }

        /// <summary>
        /// Adds a device to the mock enumerator.
        /// </summary>
        public void AddDevice(IMMDeviceWrapper device)
        {
            _devices.Add(device);
        }

        /// <summary>
        /// Adds multiple devices to the mock enumerator.
        /// </summary>
        public void AddDevices(params IMMDeviceWrapper[] devices)
        {
            _devices.AddRange(devices);
        }

        /// <summary>
        /// Convenience method to add a default input device.
        /// </summary>
        public void SetDefaultInputDevice(string id, string name, int sampleRate = 16000, int channels = 1)
        {
            var device = MockMMDeviceWrapper.CreateInputDevice(id, name, sampleRate, channels);
            DefaultInputDevice = device;
            if (!_devices.Contains(device))
            {
                _devices.Add(device);
            }
        }

        /// <summary>
        /// Convenience method to add a default output device.
        /// </summary>
        public void SetDefaultOutputDevice(string id, string name, int sampleRate = 48000, int channels = 2)
        {
            var device = MockMMDeviceWrapper.CreateOutputDevice(id, name, sampleRate, channels);
            DefaultOutputDevice = device;
            if (!_devices.Contains(device))
            {
                _devices.Add(device);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _devices.Clear();
                _callbacks.Clear();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MockAudioDeviceEnumerator));
            }
        }

        private void SimulateErrorsIfNeeded()
        {
            if (SimulateHardwareError)
            {
                throw new System.Runtime.InteropServices.COMException("Simulated hardware error", -2147467259);
            }

            if (SimulatePermissionDenied)
            {
                throw new UnauthorizedAccessException("Simulated permission denied");
            }

            if (SimulateSystemError)
            {
                throw new InvalidOperationException("Simulated system error");
            }
        }
    }
}
