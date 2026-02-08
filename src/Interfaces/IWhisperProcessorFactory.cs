using System;
using System.IO;
using System.Collections.Generic;

namespace WhisperKey.Services
{
    /// <summary>
    /// Abstraction for Whisper.net processor to allow unit testing without native binaries.
    /// </summary>
    public interface IWhisperProcessor : IDisposable
    {
        IAsyncEnumerable<TranscriptionResult> ProcessAsync(Stream waveStream);
    }

    /// <summary>
    /// Wrapper for Whisper.net result.
    /// </summary>
    public class TranscriptionResult
    {
        public string Text { get; set; } = string.Empty;
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
    }

    /// <summary>
    /// Factory for creating Whisper processors.
    /// </summary>
    public interface IWhisperProcessorFactory
    {
        IWhisperProcessor CreateProcessor(string modelPath, string language = "auto");
    }
}
