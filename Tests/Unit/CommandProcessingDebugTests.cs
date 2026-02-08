using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class CommandProcessingDebugTests
    {
        private ICommandProcessingService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new CommandProcessingService(NullLogger<CommandProcessingService>.Instance);
        }

        [TestMethod]
        public void Debug_PeriodCommand_Simple()
        {
            string input = "hello world period";
            var result = _service.ProcessCommands(input);
            
            System.Diagnostics.Debug.WriteLine($"--- DEBUG COMMAND PROCESSING ---");
            System.Diagnostics.Debug.WriteLine($"Input: '{input}'");
            System.Diagnostics.Debug.WriteLine($"Output: '{result.ProcessedText}'");
            System.Diagnostics.Debug.WriteLine($"Commands Count: {result.Commands.Count}");
            if (result.Commands.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"First Command: {result.Commands[0].Type} -> '{result.Commands[0].Replacement}' @ pos {result.Commands[0].Position}");
            }
            
            // Just check it's not adding space before punctuation
            Assert.IsFalse(result.ProcessedText.Contains(" ."), "Should not have space before period");
        }
    }
}
