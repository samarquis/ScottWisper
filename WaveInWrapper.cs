using System;
using NAudio.Wave;

namespace WhisperKey
{
    /// <summary>
    /// Wrapper around NAudio's WaveInEvent that implements IWaveIn interface.
    /// This allows the real NAudio implementation to be used in production.
    /// </summary>
    public class WaveInWrapper : IWaveIn
    {
        private WaveInEvent? _waveIn;
        private int _deviceNumber;

        public WaveFormat WaveFormat
        {
            get => _waveIn?.WaveFormat ?? new WaveFormat(16000, 1);
            set
            {
                if (_waveIn != null)
                    _waveIn.WaveFormat = value;
            }
        }

        public int BufferMilliseconds
        {
            get => _waveIn?.BufferMilliseconds ?? 100;
            set
            {
                if (_waveIn != null)
                    _waveIn.BufferMilliseconds = value;
            }
        }

        public int DeviceNumber
        {
            get => _deviceNumber;
            set
            {
                _deviceNumber = value;
                if (_waveIn != null)
                {
                    _waveIn.Dispose();
                    _waveIn = new WaveInEvent { DeviceNumber = value };
                }
            }
        }

        public event EventHandler<WaveInEventArgs>? DataAvailable
        {
            add
            {
                if (_waveIn != null) _waveIn.DataAvailable += value;
            }
            remove
            {
                if (_waveIn != null) _waveIn.DataAvailable -= value;
            }
        }

        public event EventHandler<StoppedEventArgs>? RecordingStopped
        {
            add
            {
                if (_waveIn != null) _waveIn.RecordingStopped += value;
            }
            remove
            {
                if (_waveIn != null) _waveIn.RecordingStopped -= value;
            }
        }

        public WaveInWrapper(int deviceNumber = 0)
        {
            _deviceNumber = deviceNumber;
            _waveIn = new WaveInEvent { DeviceNumber = deviceNumber };
        }

        public void StartRecording()
        {
            _waveIn?.StartRecording();
        }

        public void StopRecording()
        {
            _waveIn?.StopRecording();
        }

        public void Dispose()
        {
            _waveIn?.Dispose();
            _waveIn = null;
        }
    }
}
