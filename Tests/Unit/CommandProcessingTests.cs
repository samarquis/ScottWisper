using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class CommandProcessingTests
    {
        private ICommandProcessingService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new CommandProcessingService(NullLogger<CommandProcessingService>.Instance);
        }

        #region Basic Punctuation Tests

        [TestMethod]
        public void Test_PeriodCommand()
        {
            var result = _service.ProcessCommands("hello world period");
            //Debug.WriteLine($"Actual Output: '{result.ProcessedText}'");
            //Debug.WriteLine($"Expected Output: 'hello world.'");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("hello world.", result.ProcessedText);
            Assert.AreEqual(1, result.Commands.Count);
            Assert.AreEqual(VoiceCommandType.Punctuation, result.Commands[0].Type);
        }

        [TestMethod]
        public void Test_CommaCommand()
        {
            var result = _service.ProcessCommands("first item comma second item");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("first item, second item", result.ProcessedText);
        }

        [TestMethod]
        public void Test_QuestionMarkCommand()
        {
            var result = _service.ProcessCommands("how are you question mark");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("how are you?", result.ProcessedText);
        }

        [TestMethod]
        public void Test_ExclamationCommand()
        {
            var result = _service.ProcessCommands("wow exclamation mark");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("wow!", result.ProcessedText);
        }

        [TestMethod]
        public void Test_MultiplePunctuationCommands()
        {
            var result = _service.ProcessCommands("item one period item two period item three");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("item one. item two. item three", result.ProcessedText);
            Assert.AreEqual(2, result.Commands.Count);
        }

        #endregion

        #region Delete Command Tests

        [TestMethod]
        public void Test_DeleteCommand_RemovesPreviousChar()
        {
            var result = _service.ProcessCommands("helllo delete");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("helll", result.ProcessedText);
        }

        [TestMethod]
        public void Test_BackspaceCommand()
        {
            var result = _service.ProcessCommands("testt backspace");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("test", result.ProcessedText);
        }

        [TestMethod]
        public void Test_ScratchThatCommand()
        {
            var result = _service.ProcessCommands("mistake scratch that");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("mistak", result.ProcessedText);
        }

        [TestMethod]
        public void Test_DeleteAtStartOfText()
        {
            var result = _service.ProcessCommands("delete hello");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("hello", result.ProcessedText);
        }

        #endregion

        #region Navigation Command Tests

        [TestMethod]
        public void Test_NewLineCommand()
        {
            var result = _service.ProcessCommands("line one new line line two");
            Assert.IsTrue(result.Success);
            StringAssert.Contains(result.ProcessedText, "\n");
            Assert.IsTrue(result.ProcessedText.Contains("line one\nline two") ||
                         result.ProcessedText.Contains("line one \n line two"));
        }

        [TestMethod]
        public void Test_NewParagraphCommand()
        {
            var result = _service.ProcessCommands("paragraph one new paragraph paragraph two");
            Assert.IsTrue(result.Success);
            StringAssert.Contains(result.ProcessedText, "\n\n");
        }

        [TestMethod]
        public void Test_TabCommand()
        {
            var result = _service.ProcessCommands("column one tab column two");
            Assert.IsTrue(result.Success);
            StringAssert.Contains(result.ProcessedText, "\t");
        }

        #endregion

        #region Auto-Punctuation Tests

        [TestMethod]
        public void Test_AutoPunctuation_SimpleSentence()
        {
            var text = "this is a test";
            var result = _service.ApplyAutoPunctuation(text);
            Assert.AreEqual("This is a test.", result);
        }

        [TestMethod]
        public void Test_AutoPunctuation_Capitalization()
        {
            var text = "first sentence second sentence";
            var result = _service.ApplyAutoPunctuation(text);
            Assert.IsTrue(result.StartsWith("First sentence"));
        }

        [TestMethod]
        public void Test_AutoPunctuation_WithCommands()
        {
            var result = _service.ProcessCommands("hello world period how are you", autoPunctuation: true);
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProcessedText.Contains("Hello world."));
        }

        [TestMethod]
        public void Test_AutoPunctuation_LongSentenceBreak()
        {
            var text = "this is a very long sentence that goes on and on and should eventually break";
            var result = _service.ApplyAutoPunctuation(text);
            // Should have at least one period
            Assert.IsTrue(result.Contains("."));
            // Should be capitalized
            Assert.IsTrue(char.IsUpper(result[0]));
        }

        #endregion

        #region Command Detection Tests

        [TestMethod]
        public void Test_DetectCommands_Multiple()
        {
            var commands = _service.DetectCommands("period comma question mark");
            Assert.AreEqual(3, commands.Count);
            Assert.IsTrue(commands.Any(c => c.OriginalText == "period"));
            Assert.IsTrue(commands.Any(c => c.OriginalText == "comma"));
            Assert.IsTrue(commands.Any(c => c.OriginalText == "question mark"));
        }

        [TestMethod]
        public void Test_DetectCommands_CaseInsensitive()
        {
            var commands = _service.DetectCommands("PERIOD COMMA");
            Assert.AreEqual(2, commands.Count);
        }

        [TestMethod]
        public void Test_DetectCommands_NoCommands()
        {
            var commands = _service.DetectCommands("just regular text with no commands");
            Assert.AreEqual(0, commands.Count);
        }

        [TestMethod]
        public void Test_DetectCommands_PartialWordsIgnored()
        {
            var commands = _service.DetectCommands("this is a periodic table");
            // "periodic" should not trigger "period" command
            Assert.IsFalse(commands.Any(c => c.OriginalText == "period"));
        }

        #endregion

        #region Complex Scenario Tests

        [TestMethod]
        public void Test_ComplexDictationWithCommands()
        {
            var result = _service.ProcessCommands("dear sir comma new paragraph I am writing to you period");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Commands.Count >= 3);
            Assert.IsTrue(result.ProcessedText.Contains(","));
            Assert.IsTrue(result.ProcessedText.Contains("."));
        }

        [TestMethod]
        public void Test_ProfessionalEmailScenario()
        {
            var result = _service.ProcessCommands(
                "Hello John comma new paragraph " +
                "Thank you for your email period " +
                "I will review and get back to you soon period " +
                "new paragraph Best regards comma new line Sarah");
            
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProcessedText.Contains(","));
            Assert.IsTrue(result.ProcessedText.Contains("."));
            Assert.IsTrue(result.ProcessedText.Contains("\n"));
        }

        [TestMethod]
        public void Test_CodeSnippetScenario()
        {
            var result = _service.ProcessCommands("function hello world left parenthesis right parenthesis");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProcessedText.Contains("("));
            Assert.IsTrue(result.ProcessedText.Contains(")"));
        }

        #endregion

        #region Custom Command Tests

        [TestMethod]
        public void Test_RegisterCustomCommand()
        {
            _service.RegisterCustomCommand("smiley", VoiceCommandType.Punctuation, ":)");
            var result = _service.ProcessCommands("hello smiley");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("hello :)", result.ProcessedText);
        }

        [TestMethod]
        public void Test_CustomCommand_OverrideDefault()
        {
            // Register custom interpretation of "dot"
            _service.RegisterCustomCommand("dot", VoiceCommandType.Punctuation, "•");
            var result = _service.ProcessCommands("item dot");
            Assert.AreEqual("item •", result.ProcessedText);
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public void Test_EmptyText()
        {
            var result = _service.ProcessCommands("");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("", result.ProcessedText);
        }

        [TestMethod]
        public void Test_WhitespaceOnly()
        {
            var result = _service.ProcessCommands("   ");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(string.IsNullOrWhiteSpace(result.ProcessedText));
        }

        [TestMethod]
        public void Test_NullText()
        {
            var result = _service.ProcessCommands(null!);
            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public void Test_CommandAtEnd()
        {
            var result = _service.ProcessCommands("hello world period");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProcessedText.EndsWith("."));
        }

        [TestMethod]
        public void Test_CommandAtStart()
        {
            var result = _service.ProcessCommands("period hello");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProcessedText.Contains("."));
        }

        #endregion

        #region Special Punctuation Tests

        [TestMethod]
        public void Test_SemicolonCommand()
        {
            var result = _service.ProcessCommands("item one semicolon item two");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProcessedText.Contains(";"));
        }

        [TestMethod]
        public void Test_ColonCommand()
        {
            var result = _service.ProcessCommands("note colon important");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProcessedText.Contains(":"));
        }

        [TestMethod]
        public void Test_DashCommand()
        {
            var result = _service.ProcessCommands("word dash another word");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProcessedText.Contains("-"));
        }

        [TestMethod]
        public void Test_QuoteCommands()
        {
            var result = _service.ProcessCommands("open quote hello close quote");
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.ProcessedText.Count(c => c == '"'));
        }

        [TestMethod]
        public void Test_EllipsisCommand()
        {
            var result = _service.ProcessCommands("and so on ellipsis");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProcessedText.Contains("..."));
        }

        #endregion

        #region Capitalization Tests

        [TestMethod]
        public void Test_CapitalizeCommand()
        {
            var result = _service.ProcessCommands("capitalize john smith");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProcessedText.Contains("John smith") || 
                         result.ProcessedText.Contains("John Smith"));
        }

        #endregion

        #region Undo Command Tests

        [TestMethod]
        public void Test_UndoCommand()
        {
            var result = _service.ProcessCommands("text undo");
            Assert.IsTrue(result.Success);
            // Undo command should be removed from text
            Assert.IsFalse(result.ProcessedText.Contains("undo"));
        }

        [TestMethod]
        public void Test_UndoThatCommand()
        {
            var result = _service.ProcessCommands("mistake undo that");
            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.ProcessedText.Contains("undo"));
            Assert.AreEqual(VoiceCommandType.Undo, result.Commands[0].Type);
        }

        #endregion

        #region Result Metadata Tests

        [TestMethod]
        public void Test_Result_HasCommandsFlag()
        {
            var resultWithCommands = _service.ProcessCommands("hello period");
            Assert.IsTrue(resultWithCommands.HasCommands);

            var resultWithoutCommands = _service.ProcessCommands("hello world");
            Assert.IsFalse(resultWithoutCommands.HasCommands);
        }

        [TestMethod]
        public void Test_CommandPositions()
        {
            var commands = _service.DetectCommands("start period middle comma end");
            Assert.AreEqual(2, commands.Count);
            
            // Verify positions are in order
            Assert.IsTrue(commands[0].Position < commands[1].Position);
        }

        [TestMethod]
        public void Test_CommandTimestamps()
        {
            var before = DateTime.Now;
            var commands = _service.DetectCommands("hello period");
            var after = DateTime.Now;
            
            Assert.IsTrue(commands[0].DetectedAt >= before);
            Assert.IsTrue(commands[0].DetectedAt <= after);
        }

        #endregion
    }
}
