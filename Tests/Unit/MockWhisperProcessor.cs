using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    public class MockWhisperProcessor : IWhisperProcessor
    {
        public async IAsyncEnumerable<TranscriptionResult> ProcessAsync(Stream waveStream)
        {
            yield return new TranscriptionResult
            {
                Text = "Mock transcription result",
                Start = TimeSpan.Zero,
                End = TimeSpan.FromSeconds(1)
            };
            await Task.CompletedTask;
        }

        public void Dispose() { }
    }

    public class MockWhisperProcessorFactory : IWhisperProcessorFactory
    {
        public IWhisperProcessor CreateProcessor(string modelPath, string language = "auto")
        {
            return new MockWhisperProcessor();
        }
    }
}
