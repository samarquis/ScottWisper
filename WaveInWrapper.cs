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
        private readonly WaveInEvent _waveIn;

        public WaveFormat WaveFormat
        {
            get => _waveIn.WaveFormat;
            set => _waveIn.WaveFormat = value;
        }

        public int BufferMilliseconds
        {
            get => _waveIn.BufferMilliseconds;
            set => _waveIn.BufferMilliseconds = value;
        }

        public event EventHandler<WaveInEventArgs>? DataAvailable
        {
            add => _waveIn.DataAvailable += value;
            remove => _waveIn.DataAvailable -= value;
        }

        public event EventHandler<StoppedEventArgs>? RecordingStopped
        {
            add => _waveIn.RecordingStopped += value;
            remove => _waveIn.RecordingStopped -= value;
        }

        public WaveInWrapper()
        {
            _waveIn = new WaveInEvent();
        }

        public void StartRecording()
        {
            _waveIn.StartRecording();
        }

        public void StopRecording()
        {
            _waveIn.StopRecording();
        }

        public void Dispose()
        {
            _waveIn?.Dispose();
        }
    }
}
