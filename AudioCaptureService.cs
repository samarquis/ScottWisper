using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using ScottWisper.Services;

namespace ScottWisper
{
    public class AudioCaptureService : IDisposable
    {
        private WaveInEvent? _waveIn;
        private MemoryStream? _audioStream;
        private bool _isCapturing;
        private readonly object _lockObject = new object();
        private readonly ISettingsService? _settingsService;
        
        // Audio format specifications (will be loaded from settings)
        private int _sampleRate = 16000; // 16kHz default
        private int _channels = 1; // Mono default
        private int _bitDepth = 16; // 16-bit default
        private int _bufferSize = 1024; // Low latency buffer default
        
        public event EventHandler<byte[]>? AudioDataCaptured;
        public event EventHandler<Exception>? CaptureError;
        
        public bool IsCapturing => _isCapturing;
        
        public AudioCaptureService()
        {
            // Use default values
        }
        
        public AudioCaptureService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadAudioSettingsFromSettings();
            
            // Subscribe to settings changes
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged += OnSettingsChanged;
            }
        }

        private void LoadAudioSettingsFromSettings()
        {
            if (_settingsService?.Settings?.Audio != null)
            {
                var audioSettings = _settingsService.Settings.Audio;
                _sampleRate = audioSettings.SampleRate > 0 ? audioSettings.SampleRate : 16000;
                _channels = audioSettings.Channels >= 1 && audioSettings.Channels <= 2 ? audioSettings.Channels : 1;
                _bitDepth = audioSettings.BitDepth > 0 ? audioSettings.BitDepth : 16;
                _bufferSize = audioSettings.BufferSize > 0 ? audioSettings.BufferSize : 1024;
            }
        }

        private async void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            // Handle audio settings changes
            if (e.Category == "Audio")
            {
                LoadAudioSettingsFromSettings();
                // If currently capturing, restart with new settings
                if (_isCapturing)
                {
                    await StopCaptureAsync();
                    await Task.Delay(100); // Brief pause
                    await StartCaptureAsync();
                }
            }
        }

        public Task<bool> StartCaptureAsync()
        {
            try
            {
                if (_isCapturing)
                {
                    return Task.FromResult(false); // Already capturing
                }
                
                // Check for available audio devices
                if (WaveIn.DeviceCount == 0)
                {
                    throw new InvalidOperationException("No audio input devices found");
                }
                
                // Initialize audio capture with settings-based values
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(_sampleRate, _bitDepth, _channels),
                    BufferMilliseconds = (int)((_bufferSize * 1000.0) / _sampleRate)
                };
                
                // Set up event handlers
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                
                // Initialize audio stream
                _audioStream = new MemoryStream();
                
                // Start recording
                _waveIn.StartRecording();
                _isCapturing = true;
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                CaptureError?.Invoke(this, ex);
                return Task.FromResult(false);
            }
        }
        
        public Task<bool> StopCaptureAsync()
        {
            try
            {
                if (!_isCapturing || _waveIn == null)
                {
                    return Task.FromResult(false); // Not capturing
                }
                
                // Stop recording
                _waveIn.StopRecording();
                _isCapturing = false;
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                CaptureError?.Invoke(this, ex);
                return Task.FromResult(false);
            }
        }
        
        public byte[]? GetCapturedAudio()
        {
            lock (_lockObject)
            {
                if (_audioStream == null || _audioStream.Length == 0)
                {
                    return null;
                }
                
                // Convert to WAV format
                _audioStream.Position = 0;
                using var waveStream = new RawSourceWaveStream(_audioStream, _waveIn?.WaveFormat ?? new WaveFormat(SampleRate, BitDepth, Channels));
                using var wavStream = new MemoryStream();
                WaveFileWriter.WriteWavFileToStream(wavStream, waveStream);
                return wavStream.ToArray();
            }
        }
        
        public void ClearCapturedAudio()
        {
            lock (_lockObject)
            {
                if (_audioStream != null)
                {
                    _audioStream.SetLength(0);
                    _audioStream.Position = 0;
                }
            }
        }
        
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                if (_audioStream != null)
                {
                    lock (_lockObject)
                    {
                        _audioStream.Write(e.Buffer, 0, e.BytesRecorded);
                    }
                    
                    // Notify subscribers of new audio data
                    AudioDataCaptured?.Invoke(this, e.Buffer);
                }
            }
            catch (Exception ex)
            {
                CaptureError?.Invoke(this, ex);
            }
        }
        
        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _isCapturing = false;
            
            if (e.Exception != null)
            {
                CaptureError?.Invoke(this, e.Exception);
            }
        }
        
        public static AudioDevice[] GetAvailableDevices()
        {
            var devices = new AudioDevice[WaveIn.DeviceCount];
            
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                devices[i] = new AudioDevice
                {
                    DeviceNumber = i,
                    Name = capabilities.ProductName,
                    Channels = capabilities.Channels
                };
            }
            
            return devices;
        }
        
        public void Dispose()
        {
            try
            {
                if (_isCapturing && _waveIn != null)
                {
                    _waveIn.StopRecording();
                }
                
                _waveIn?.Dispose();
                _audioStream?.Dispose();
            }
            catch (Exception ex)
            {
                CaptureError?.Invoke(this, ex);
            }
            finally
            {
                _waveIn = null;
                _audioStream = null;
                _isCapturing = false;
            }
        }
    }
    
    public class AudioDevice
    {
        public int DeviceNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Channels { get; set; }
        
        public override string ToString()
        {
            return $"{Name} (Device {DeviceNumber}, {Channels} channels)";
        }
    }
}