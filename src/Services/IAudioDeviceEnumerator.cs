using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WhisperKey.Services
{
    /// <summary>
    /// Abstraction over MMDevice to enable testing without hardware dependencies.
    /// </summary>
    public interface IMMDeviceWrapper
    {
        string ID { get; }
        string FriendlyName { get; }
        string DeviceFriendlyName { get; }
        DataFlow DataFlow { get; }
        NAudio.CoreAudioApi.DeviceState State { get; }
        IMMDevicePropertiesWrapper? Properties { get; }
        IAudioClientWrapper? AudioClient { get; }
    }

    /// <summary>
    /// Abstraction over MMDevice.Properties to enable testing.
    /// </summary>
    public interface IMMDevicePropertiesWrapper
    {
        PropertyStoreProperty this[PropertyKey key] { get; }
    }

    /// <summary>
    /// Abstraction over AudioClient to enable testing.
    /// </summary>
    public interface IAudioClientWrapper
    {
        AudioWaveFormat? MixFormat { get; }
    }

    /// <summary>
    /// Abstraction over WaveFormat from NAudio.
    /// </summary>
    public interface IWaveFormatWrapper
    {
        int SampleRate { get; }
        int Channels { get; }
        int BitsPerSample { get; }
    }

    /// <summary>
    /// Wrapper for NAudio WaveFormat.
    /// </summary>
    public class WaveFormatWrapper : IWaveFormatWrapper
    {
        private readonly NAudio.Wave.WaveFormat _format;

        public WaveFormatWrapper(NAudio.Wave.WaveFormat format)
        {
            _format = format ?? throw new ArgumentNullException(nameof(format));
        }

        public int SampleRate => _format.SampleRate;
        public int Channels => _format.Channels;
        public int BitsPerSample => _format.BitsPerSample;
    }

    /// <summary>
    /// Wrapper for MMDevice to provide testable interface.
    /// </summary>
    public class MMDeviceWrapper : IMMDeviceWrapper
    {
        private readonly MMDevice _device;

        public MMDeviceWrapper(MMDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public string ID => _device.ID;
        public string FriendlyName => _device.FriendlyName;
        public string DeviceFriendlyName => _device.DeviceFriendlyName;
        public DataFlow DataFlow => _device.DataFlow;
        public NAudio.CoreAudioApi.DeviceState State => _device.State;
        public IMMDevicePropertiesWrapper? Properties => _device.Properties != null ? new MMDevicePropertiesWrapper(_device.Properties) : null;
        public IAudioClientWrapper? AudioClient => _device.AudioClient != null ? new AudioClientWrapper(_device.AudioClient) : null;
    }

    /// <summary>
    /// Wrapper for MMDevice.Properties.
    /// </summary>
    public class MMDevicePropertiesWrapper : IMMDevicePropertiesWrapper
    {
        private readonly PropertyStore _properties;

        public MMDevicePropertiesWrapper(PropertyStore properties)
        {
            _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public PropertyStoreProperty this[PropertyKey key] => _properties[key];
    }

    /// <summary>
    /// Wrapper for AudioClient.
    /// </summary>
    public class AudioClientWrapper : IAudioClientWrapper
    {
        private readonly AudioClient _audioClient;

        public AudioClientWrapper(AudioClient audioClient)
        {
            _audioClient = audioClient ?? throw new ArgumentNullException(nameof(audioClient));
        }

        public AudioWaveFormat? MixFormat
        {
            get
            {
                try
                {
                    var format = _audioClient.MixFormat;
                    return format != null ? new AudioWaveFormat(new WaveFormatWrapper(format)) : null;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// Wrapper for WaveFormat that implements IWaveFormatWrapper.
    /// </summary>
    public class AudioWaveFormat : IWaveFormatWrapper
    {
        private readonly IWaveFormatWrapper _wrapper;

        public AudioWaveFormat(IWaveFormatWrapper wrapper)
        {
            _wrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
        }

        public int SampleRate => _wrapper.SampleRate;
        public int Channels => _wrapper.Channels;
        public int BitsPerSample => _wrapper.BitsPerSample;

        public static implicit operator AudioWaveFormat(NAudio.Wave.WaveFormat format) =>
            new AudioWaveFormat(new WaveFormatWrapper(format));
    }

    /// <summary>
    /// Abstraction over MMDeviceEnumerator to enable testing without hardware dependencies.
    /// This is the core interface that allows mocking device enumeration for unit tests.
    /// </summary>
    public interface IAudioDeviceEnumerator : IDisposable
    {
        /// <summary>
        /// Enumerates audio endpoints for the specified data flow and device state.
        /// </summary>
        IEnumerable<IMMDeviceWrapper> EnumerateAudioEndPoints(DataFlow dataFlow, NAudio.CoreAudioApi.DeviceState deviceState);

        /// <summary>
        /// Gets the default audio endpoint for the specified data flow and role.
        /// </summary>
        IMMDeviceWrapper GetDefaultAudioEndpoint(DataFlow dataFlow, Role role);

        /// <summary>
        /// Registers for device notification callbacks.
        /// </summary>
        void RegisterEndpointNotificationCallback(IMMNotificationClient notificationCallback);

        /// <summary>
        /// Unregisters from device notification callbacks.
        /// </summary>
        void UnregisterEndpointNotificationCallback(IMMNotificationClient notificationCallback);
    }

    /// <summary>
    /// Real implementation of IAudioDeviceEnumerator that wraps MMDeviceEnumerator.
    /// This is used in production code to interact with actual audio hardware.
    /// </summary>
    public class AudioDeviceEnumerator : IAudioDeviceEnumerator
    {
        private readonly MMDeviceEnumerator _enumerator;
        private bool _disposed;

        public AudioDeviceEnumerator()
        {
            _enumerator = new MMDeviceEnumerator();
        }

        public IEnumerable<IMMDeviceWrapper> EnumerateAudioEndPoints(DataFlow dataFlow, NAudio.CoreAudioApi.DeviceState deviceState)
        {
            ThrowIfDisposed();
            return _enumerator.EnumerateAudioEndPoints(dataFlow, deviceState)
                .Select(d => new MMDeviceWrapper(d));
        }

        public IMMDeviceWrapper GetDefaultAudioEndpoint(DataFlow dataFlow, Role role)
        {
            ThrowIfDisposed();
            return new MMDeviceWrapper(_enumerator.GetDefaultAudioEndpoint(dataFlow, role));
        }

        public void RegisterEndpointNotificationCallback(IMMNotificationClient notificationCallback)
        {
            ThrowIfDisposed();
            _enumerator.RegisterEndpointNotificationCallback(notificationCallback);
        }

        public void UnregisterEndpointNotificationCallback(IMMNotificationClient notificationCallback)
        {
            ThrowIfDisposed();
            _enumerator.UnregisterEndpointNotificationCallback(notificationCallback);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _enumerator?.Dispose();
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AudioDeviceEnumerator));
            }
        }
    }
}
