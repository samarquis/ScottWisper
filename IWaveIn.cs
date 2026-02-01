using System;
using NAudio.Wave;

namespace ScottWisper
{
    /// <summary>
    /// Interface that abstracts NAudio's WaveInEvent for testability.
    /// Provides all the necessary functionality for audio capture.
    /// </summary>
    public interface IWaveIn : IDisposable
    {
        /// <summary>
        /// The wave format being used for recording.
        /// </summary>
        WaveFormat WaveFormat { get; set; }

        /// <summary>
        /// Buffer duration in milliseconds.
        /// </summary>
        int BufferMilliseconds { get; set; }

        /// <summary>
        /// Event raised when audio data is available.
        /// </summary>
        event EventHandler<WaveInEventArgs>? DataAvailable;

        /// <summary>
        /// Event raised when recording stops.
        /// </summary>
        event EventHandler<StoppedEventArgs>? RecordingStopped;

        /// <summary>
        /// Starts recording audio.
        /// </summary>
        void StartRecording();

        /// <summary>
        /// Stops recording audio.
        /// </summary>
        void StopRecording();
    }
}
