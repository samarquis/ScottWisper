using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NAudio.Wave;
using NAudio.Dsp;

namespace WhisperKey
{
    /// <summary>
    /// Real-time audio visualization component with waveform display and level monitoring
    /// </summary>
    public partial class AudioVisualizer : UserControl, IDisposable
    {
        private readonly DispatcherTimer _animationTimer;
        private readonly Queue<float> _audioBuffer = new Queue<float>();
        private readonly object _bufferLock = new object();
        private const int BufferSize = 1024;
        private const int Fps = 60;
        private const int AnimationInterval = 1000 / Fps; // 60 FPS
        
        private VisualizationMode _currentMode = VisualizationMode.Waveform;
        private float _currentLevel = 0f;
        private float _peakLevel = 0f;
        private bool _hasVoiceActivity = false;
        
        public enum VisualizationMode
        {
            Waveform,
            Bars,
            Minimal
        }
        
        public AudioVisualizer()
        {
            InitializeComponent();
            
            // Initialize animation timer
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AnimationInterval)
            };
            _animationTimer.Tick += AnimationTimer_Tick;
            
            // Initialize UI
            ResetVisualization();
        }
        
        #region Public Methods
        
        /// <summary>
        /// Starts the visualization animation
        /// </summary>
        public void StartVisualization()
        {
            if (!_animationTimer.IsEnabled)
            {
                _animationTimer.Start();
            }
        }
        
        /// <summary>
        /// Stops the visualization animation
        /// </summary>
        public void StopVisualization()
        {
            _animationTimer.Stop();
            ResetVisualization();
        }
        
        /// <summary>
        /// Updates audio data for visualization
        /// </summary>
        /// <param name="audioData">Raw audio byte data</param>
        public void UpdateAudioData(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return;
                
            // Convert bytes to float samples for processing
            var samples = ConvertBytesToFloatSamples(audioData);
            
            lock (_bufferLock)
            {
                // Add samples to buffer
                foreach (var sample in samples)
                {
                    _audioBuffer.Enqueue(sample);
                    
                    // Maintain buffer size
                    if (_audioBuffer.Count > BufferSize)
                    {
                        _audioBuffer.Dequeue();
                    }
                }
                
                // Calculate current audio level
                UpdateAudioLevel(samples);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            lock (_bufferLock)
            {
                switch (_currentMode)
                {
                    case VisualizationMode.Waveform:
                        UpdateWaveformVisualization();
                        break;
                    case VisualizationMode.Bars:
                        UpdateBarsVisualization();
                        break;
                    case VisualizationMode.Minimal:
                        UpdateMinimalVisualization();
                        break;
                }
            }
            
            UpdateLevelIndicators();
        }
        
        private void UpdateWaveformVisualization()
        {
            if (_audioBuffer.Count == 0)
            {
                ResetWaveform();
                return;
            }
            
            var samples = _audioBuffer.ToArray();
            var canvasWidth = WaveformCanvas.ActualWidth;
            var canvasHeight = WaveformCanvas.ActualHeight;
            
            if (canvasWidth <= 0 || canvasHeight <= 0)
                return;
            
            // Create waveform points
            var points = new PointCollection();
            var samplesPerPixel = Math.Max(1, samples.Length / (int)canvasWidth);
            
            for (int x = 0; x < canvasWidth; x++)
            {
                var startIndex = x * samplesPerPixel;
                var endIndex = Math.Min(startIndex + samplesPerPixel, samples.Length);
                
                if (startIndex >= samples.Length)
                    break;
                
                // Calculate average amplitude for this pixel column
                float sum = 0;
                int count = 0;
                
                for (int i = startIndex; i < endIndex; i++)
                {
                    sum += Math.Abs(samples[i]);
                    count++;
                }
                
                var averageAmplitude = count > 0 ? sum / count : 0;
                var y = canvasHeight / 2 + (averageAmplitude * canvasHeight * 0.4); // Scale to 80% of height
                
                // Clamp to canvas bounds
                y = Math.Max(0, Math.Min(canvasHeight, y));
                
                points.Add(new Point(x, y));
            }
            
            // Update waveform polyline
            WaveformPolyline.Points = points;
        }
        
        private void UpdateBarsVisualization()
        {
            if (_audioBuffer.Count == 0)
            {
                ResetBars();
                return;
            }
            
            var samples = _audioBuffer.ToArray();
            var canvasWidth = WaveformCanvas.ActualWidth;
            var canvasHeight = WaveformCanvas.ActualHeight;
            
            if (canvasWidth <= 0 || canvasHeight <= 0)
                return;
            
            // Create frequency bars visualization
            var barCount = 20;
            var barWidth = canvasWidth / barCount;
            var samplesPerBar = samples.Length / barCount;
            
            // Clear existing bars
            WaveformCanvas.Children.Clear();
            WaveformCanvas.Children.Add(WaveformPolyline); // Keep base polyline
            
            for (int i = 0; i < barCount; i++)
            {
                var startIndex = i * samplesPerBar;
                var endIndex = Math.Min(startIndex + samplesPerBar, samples.Length);
                
                if (startIndex >= samples.Length)
                    break;
                
                // Calculate RMS for this bar
                double sumSquares = 0;
                int count = 0;
                
                for (int j = startIndex; j < endIndex; j++)
                {
                    sumSquares += samples[j] * samples[j];
                    count++;
                }
                
                var rms = count > 0 ? Math.Sqrt(sumSquares / count) : 0;
                var barHeight = rms * canvasHeight * 0.8; // Scale to 80% of height
                
                // Create bar rectangle
                var bar = new Rectangle
                {
                    Fill = GetLevelBrush((float)rms),
                    Width = barWidth - 2, // Small gap between bars
                    Height = Math.Max(1, barHeight)
                };
                Canvas.SetLeft(bar, i * barWidth + 1);
                Canvas.SetTop(bar, canvasHeight - barHeight);
                
                WaveformCanvas.Children.Add(bar);
            }
        }
        
        private void UpdateMinimalVisualization()
        {
            // Minimal mode only shows voice activity indicator
            VoiceActivityIndicator.Opacity = _hasVoiceActivity ? 1.0 : 0.3;
            
            // Fade waveform
            WaveformPolyline.Opacity = _hasVoiceActivity ? 0.3 : 0.1;
            
            // Remove any bar elements
            var barsToRemove = WaveformCanvas.Children.OfType<Rectangle>().Where(r => r != PeakLevelIndicator).ToList();
            foreach (var bar in barsToRemove)
            {
                WaveformCanvas.Children.Remove(bar);
            }
        }
        
        private void UpdateAudioLevel(float[] samples)
        {
            // Calculate RMS (Root Mean Square) for audio level
            double sumSquares = 0;
            foreach (var sample in samples)
            {
                sumSquares += sample * sample;
            }
            
            _currentLevel = (float)Math.Sqrt(sumSquares / samples.Length);
            
            // Update peak level with decay
            if (_currentLevel > _peakLevel)
            {
                _peakLevel = _currentLevel;
            }
            else
            {
                _peakLevel *= 0.99f; // Slow decay
            }
            
            // Detect voice activity (threshold-based)
            _hasVoiceActivity = _currentLevel > 0.01f; // Adjust threshold as needed
        }
        
        private void UpdateLevelIndicators()
        {
            // Update audio level bar
            var levelPercentage = Math.Min(1.0, _currentLevel * 10); // Scale for visibility
            var levelBarWidth = 300 * levelPercentage;
            AudioLevelBar.Width = levelBarWidth;
            
            // Update level bar color
            AudioLevelBar.Fill = GetLevelBrush(_currentLevel);
            
            // Update peak level text
            var peakDb = 20 * Math.Log10(Math.Max(0.001, _peakLevel)); // Convert to dB
            PeakLevelText.Text = $"{peakDb:F1} dB";
            
            // Update peak level indicator position
            var peakX = 196 + (_peakLevel * 180); // Centered with spread
            Canvas.SetLeft(PeakLevelIndicator, peakX - 2); // Center the 4px indicator
            
            // Update peak level indicator color
            PeakLevelIndicator.Fill = GetLevelBrush(_peakLevel);
            
            // Update voice activity indicator
            VoiceActivityIndicator.Fill = _hasVoiceActivity ? 
                new SolidColorBrush(Colors.LimeGreen) : 
                new SolidColorBrush(Color.FromRgb(76, 175, 80));
        }
        
        private SolidColorBrush GetLevelBrush(float level)
        {
            // Color coding: green -> yellow -> red
            if (level < 0.3f)
                return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            else if (level < 0.7f)
                return new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow/Amber
            else
                return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
        }
        
        private float[] ConvertBytesToFloatSamples(byte[] audioData)
        {
            // Convert 16-bit PCM bytes to float samples
            var samples = new float[audioData.Length / 2]; // 16-bit = 2 bytes per sample
            var buffer = new byte[2];
            
            for (int i = 0; i < samples.Length; i++)
            {
                Buffer.BlockCopy(audioData, i * 2, buffer, 0, 2);
                var shortValue = BitConverter.ToInt16(buffer, 0);
                samples[i] = shortValue / 32768f; // Normalize to -1.0 to 1.0
            }
            
            return samples;
        }
        
        private void ResetVisualization()
        {
            ResetWaveform();
            ResetBars();
            ResetLevelIndicators();
        }
        
        private void ResetWaveform()
        {
            // Reset to center line
            var centerY = WaveformCanvas.ActualHeight / 2;
            WaveformPolyline.Points = new PointCollection 
            { 
                new Point(0, centerY), 
                new Point(WaveformCanvas.ActualWidth, centerY) 
            };
        }
        
        private void ResetBars()
        {
            // Remove all bar elements
            var barsToRemove = WaveformCanvas.Children.OfType<Rectangle>().Where(r => r != PeakLevelIndicator).ToList();
            foreach (var bar in barsToRemove)
            {
                WaveformCanvas.Children.Remove(bar);
            }
        }
        
        private void ResetLevelIndicators()
        {
            _currentLevel = 0f;
            _peakLevel = 0f;
            _hasVoiceActivity = false;
            
            AudioLevelBar.Width = 0;
            PeakLevelText.Text = "-∞ dB";
            PeakLevelIndicator.SetValue(Canvas.LeftProperty, 196.0);
            VoiceActivityIndicator.Opacity = 0.3;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void VisualizationModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VisualizationModeCombo?.SelectedItem is ComboBoxItem selectedItem)
            {
                var modeText = selectedItem.Content.ToString();
                
                switch (modeText)
                {
                    case "Waveform":
                        _currentMode = VisualizationMode.Waveform;
                        break;
                    case "Bars":
                        _currentMode = VisualizationMode.Bars;
                        break;
                    case "Minimal":
                        _currentMode = VisualizationMode.Minimal;
                        break;
                }
            }
        }
        
        #endregion
        
        #region IDisposable
        
        private bool _disposed = false;
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_animationTimer != null)
                    {
                        _animationTimer.Stop();
                        _animationTimer.Tick -= AnimationTimer_Tick;
                    }
                    
                    lock (_bufferLock)
                    {
                        _audioBuffer.Clear();
                    }
                }
                
                _disposed = true;
            }
        }
        
        #endregion
    }
}
