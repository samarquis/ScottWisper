using System;
using System.IO;
using System.Collections.Generic;
using Whisper.net;

namespace WhisperKey.Services
{
    public class WhisperProcessorWrapper : IWhisperProcessor
    {
        private readonly WhisperProcessor _processor;

        public WhisperProcessorWrapper(WhisperProcessor processor)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        }

        public async IAsyncEnumerable<TranscriptionResult> ProcessAsync(Stream waveStream)
        {
            await foreach (var result in _processor.ProcessAsync(waveStream))
            {
                yield return new TranscriptionResult
                {
                    Text = result.Text,
                    Start = result.Start,
                    End = result.End
                };
            }
        }

        public void Dispose()
        {
            _processor?.Dispose();
        }
    }

    public class WhisperProcessorFactory : IWhisperProcessorFactory
    {
        public IWhisperProcessor CreateProcessor(string modelPath, string language = "auto")
        {
            var factory = WhisperFactory.FromPath(modelPath);
            var processor = factory.CreateBuilder()
                .WithLanguage(language)
                .Build();
            
            return new WhisperProcessorWrapper(processor);
        }
    }
}
