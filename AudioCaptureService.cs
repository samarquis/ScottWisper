using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using WhisperKey.Services;

namespace WhisperKey
{
    public class AudioCaptureService : IAudioCaptureService
    {
        private IWaveIn? _waveIn;
        private ConcurrentQueue<byte[]>? _audioBuffer;
        private bool _isCapturing;
        private readonly ISettingsService? _settingsService;
        private readonly IAudioDeviceService? _audioDeviceService;
        private readonly IWaveIn? _waveInInstance;
        private readonly Func<IWaveIn>? _waveInFactory;
        
        // Audio format specifications (will be loaded from settings)
        private int _sampleRate = 16000; // 16kHz default
        private int _channels = 1; // Mono default
        private int _bitDepth = 16; // 16-bit default
        private int _bufferSize = 1024; // Low latency buffer default
        
        public event EventHandler<byte[]>? AudioDataCaptured;
        public event EventHandler<Exception>? CaptureError;
        
        // Permission events
        public event EventHandler? PermissionRequired;
        public event EventHandler? PermissionRetry;
        
        public bool IsCapturing => _isCapturing;

        // Windows API for opening settings
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr ShellExecute(
            IntPtr hwnd,
            string? lpOperation,
            string? lpFile,
            string? lpParameters,
            string? lpDirectory,
            int nShowCmd);

        public AudioCaptureService()
        {
            // Use default values and create WaveIn internally
        }
        
        public AudioCaptureService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadAudioSettingsFromSettings();
        }
        
        public AudioCaptureService(ISettingsService settingsService, IAudioDeviceService audioDeviceService)
        {
            _settingsService = settingsService;
            _audioDeviceService = audioDeviceService;
            LoadAudioSettingsFromSettings();
            
            // Subscribe to permission events from AudioDeviceService
            if (_audioDeviceService != null)
            {
                _audioDeviceService.PermissionDenied += OnPermissionDenied;
                _audioDeviceService.PermissionGranted += OnPermissionGranted;
                _audioDeviceService.PermissionRequestFailed += OnPermissionRequestFailed;
            }
        }

        /// <summary>
        /// Constructor that accepts an IWaveIn instance for dependency injection and testing.
        /// </summary>
        public AudioCaptureService(ISettingsService settingsService, IAudioDeviceService audioDeviceService, IWaveIn waveIn)
        {
            _settingsService = settingsService;
            _audioDeviceService = audioDeviceService;
            _waveInInstance = waveIn;
            LoadAudioSettingsFromSettings();
            
            // Subscribe to permission events from AudioDeviceService
            if (_audioDeviceService != null)
            {
                _audioDeviceService.PermissionDenied += OnPermissionDenied;
                _audioDeviceService.PermissionGranted += OnPermissionGranted;
                _audioDeviceService.PermissionRequestFailed += OnPermissionRequestFailed;
            }
        }

        /// <summary>
        /// Constructor that accepts a factory function for creating IWaveIn instances.
        /// Useful for scenarios where multiple instances might be needed.
        /// </summary>
        public AudioCaptureService(ISettingsService settingsService, IAudioDeviceService audioDeviceService, Func<IWaveIn> waveInFactory)
        {
            _settingsService = settingsService;
            _audioDeviceService = audioDeviceService;
            _waveInFactory = waveInFactory;
            LoadAudioSettingsFromSettings();
            
            // Subscribe to permission events from AudioDeviceService
            if (_audioDeviceService != null)
            {
                _audioDeviceService.PermissionDenied += OnPermissionDenied;
                _audioDeviceService.PermissionGranted += OnPermissionGranted;
                _audioDeviceService.PermissionRequestFailed += OnPermissionRequestFailed;
            }
        }

        private void LoadAudioSettingsFromSettings()
        {
            if (_settingsService?.Settings?.Audio != null)
            {
                var audioSettings = _settingsService.Settings.Audio;
                _sampleRate = audioSettings.SampleRate > 0 ? audioSettings.SampleRate : 16000;
                _channels = audioSettings.Channels >= 1 && audioSettings.Channels <= 2 ? audioSettings.Channels : 1;
                
                // Use device-specific settings if available, otherwise use defaults
                if (!string.IsNullOrEmpty(audioSettings.InputDeviceId) && 
                    audioSettings.DeviceSettings.TryGetValue(audioSettings.InputDeviceId, out var deviceSettings))
                {
                    _bitDepth = 16; // Default bit depth
                    _bufferSize = deviceSettings.BufferSize > 0 ? deviceSettings.BufferSize : 1024;
                }
                else
                {
                    _bitDepth = 16; // Default bit depth
                    _bufferSize = 1024; // Default buffer size
                }
            }
        }

        private void OnPermissionDenied(object? sender, EventArgs e)
        {
            PermissionRequired?.Invoke(this, EventArgs.Empty);
        }

        private void OnPermissionGranted(object? sender, EventArgs e)
        {
            PermissionRetry?.Invoke(this, EventArgs.Empty);
        }

        private void OnPermissionRequestFailed(object? sender, EventArgs e)
        {
            PermissionRequired?.Invoke(this, EventArgs.Empty);
        }

        public async Task<bool> StartCaptureAsync()
        {
            try
            {
                if (_isCapturing)
                {
                    return false; // Already capturing
                }
                
                // Check microphone permission first if AudioDeviceService is available
                if (_audioDeviceService != null)
                {
                    var permissionStatus = await _audioDeviceService.CheckMicrophonePermissionAsync().ConfigureAwait(false);
                    if (!permissionStatus.Equals(MicrophonePermissionStatus.Granted))
                    {
                        // Permission not granted, try to request it
                        var permissionGranted = await _audioDeviceService.RequestMicrophonePermissionAsync().ConfigureAwait(false);
                        if (!permissionGranted)
                        {
                            ShowPermissionErrorMessage();
                            return false;
                        }
                    }
                }
                
                // Check for available audio devices
                if (WaveIn.DeviceCount == 0)
                {
                    throw new InvalidOperationException("No audio input devices found");
                }
                
                // Initialize audio capture with injected or created IWaveIn
                _waveIn = _waveInInstance ?? _waveInFactory?.Invoke() ?? new WaveInWrapper();
                
                _waveIn.WaveFormat = new WaveFormat(_sampleRate, _bitDepth, _channels);
                _waveIn.BufferMilliseconds = (int)((_bufferSize * 1000.0) / _sampleRate);
                
                // Set up event handlers
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                
                // Initialize lock-free audio buffer
                _audioBuffer = new ConcurrentQueue<byte[]>();
                
                // Start recording
                _waveIn.StartRecording();
                _isCapturing = true;
                
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                HandleUnauthorizedAccessException(ex);
                return false;
            }
            catch (SecurityException ex)
            {
                HandleSecurityException(ex);
                return false;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException && ex is not AccessViolationException)
            {
                CaptureError?.Invoke(this, ex);
                return false;
            }
        }

        public async Task StopCaptureAsync()
        {
            await Task.Run(() => {
                _waveIn?.StopRecording();
                _isCapturing = false;
            });
        }

        private void ShowPermissionErrorMessage()
        {
            PermissionRequired?.Invoke(this, EventArgs.Empty);
        }

        private void HandleUnauthorizedAccessException(Exception ex)
        {
            PermissionRequired?.Invoke(this, EventArgs.Empty);
        }

        private void HandleSecurityException(Exception ex)
        {
            PermissionRequired?.Invoke(this, EventArgs.Empty);
        }

        public void OpenWindowsMicrophoneSettings()
        {
            try
            {
                // Open Windows Privacy & Security -> Microphone settings
                const string settingsPath = "ms-settings:privacy-microphone";
                ShellExecute(IntPtr.Zero, "open", settingsPath, null, null, 1);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException && ex is not AccessViolationException)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening Windows microphone settings: {ex.Message}");
            }
        }

        public async Task<bool> RetryWithPermissionAsync()
        {
            // Wait a moment and retry capture
            await Task.Delay(1000);
            return await StartCaptureAsync();
        }
        
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                if (_audioBuffer != null)
                {
                    // Copy buffer to avoid NAudio reusing the buffer
                    var audioChunk = new byte[e.BytesRecorded];
                    Buffer.BlockCopy(e.Buffer, 0, audioChunk, 0, e.BytesRecorded);
                    _audioBuffer.Enqueue(audioChunk);
                    
                    // Notify subscribers of new audio data
                    AudioDataCaptured?.Invoke(this, audioChunk);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException && ex is not AccessViolationException)
            {
                CaptureError?.Invoke(this, ex);
            }
        }
        
        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _isCapturing = false;
            
            if (e.Exception != null)
            {
                if (e.Exception is UnauthorizedAccessException)
                {
                    HandleUnauthorizedAccessException(e.Exception);
                }
                else if (e.Exception is SecurityException)
                {
                    HandleSecurityException(e.Exception);
                }
                else
                {
                    CaptureError?.Invoke(this, e.Exception);
                }
            }
        }
        
        public byte[]? GetCapturedAudio()
        {
            if (_audioBuffer == null || _audioBuffer.IsEmpty)
            {
                return null;
            }
            
            // Drain queue into a list and concatenate
            var chunks = new System.Collections.Generic.List<byte[]>();
            while (_audioBuffer.TryDequeue(out var chunk))
            {
                chunks.Add(chunk);
            }
            
            if (chunks.Count == 0)
            {
                return null;
            }
            
            // Calculate total size and concatenate
            var totalSize = chunks.Sum(c => c.Length);
            var audioData = new byte[totalSize];
            var position = 0;
            foreach (var chunk in chunks)
            {
                Buffer.BlockCopy(chunk, 0, audioData, position, chunk.Length);
                position += chunk.Length;
            }
            
            // Convert to WAV format
            using var rawStream = new MemoryStream(audioData);
            using var waveStream = new RawSourceWaveStream(rawStream, _waveIn?.WaveFormat ?? new WaveFormat(_sampleRate, _bitDepth, _channels));
            using var wavStream = new MemoryStream();
            WaveFileWriter.WriteWavFileToStream(wavStream, waveStream);
            return wavStream.ToArray();
        }
        
        public void ClearCapturedAudio()
        {
            // Drain the queue to clear it (lock-free)
            if (_audioBuffer != null)
            {
                while (_audioBuffer.TryDequeue(out _))
                {
                    // Just drain the queue
                }
            }
        }
        
        public static CaptureAudioDevice[] GetAvailableDevices()
        {
            var devices = new CaptureAudioDevice[WaveIn.DeviceCount];
            
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                devices[i] = new CaptureAudioDevice
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
                // Unsubscribe from AudioDeviceService permission events to prevent memory leaks
                if (_audioDeviceService != null)
                {
                    _audioDeviceService.PermissionDenied -= OnPermissionDenied;
                    _audioDeviceService.PermissionGranted -= OnPermissionGranted;
                    _audioDeviceService.PermissionRequestFailed -= OnPermissionRequestFailed;
                }

                // Unsubscribe from waveIn events
                if (_waveIn != null)
                {
                    _waveIn.DataAvailable -= OnDataAvailable;
                    _waveIn.RecordingStopped -= OnRecordingStopped;
                }

                if (_isCapturing && _waveIn != null)
                {
                    _waveIn.StopRecording();
                }
                
                _waveIn?.Dispose();
                
                // Clear the queue on dispose
                if (_audioBuffer != null)
                {
                    while (_audioBuffer.TryDequeue(out _))
                    {
                        // Drain any remaining items
                    }
                    _audioBuffer = null;
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException && ex is not AccessViolationException)
            {
                CaptureError?.Invoke(this, ex);
            }
            finally
            {
                _waveIn = null;
                _audioBuffer = null;
                _isCapturing = false;
            }
        }
    }
    
    public class CaptureAudioDevice
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
