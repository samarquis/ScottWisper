using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Services
{
    /// <summary>
    /// Service for processing voice commands in transcription text
    /// </summary>
    public interface ICommandProcessingService
    {
        /// <summary>
        /// Process transcription text for voice commands and execute them
        /// </summary>
        /// <param name="transcription">The raw transcription text</param>
        /// <param name="autoPunctuation">Whether to apply auto-punctuation heuristics</param>
        /// <returns>Result containing processed text and executed commands</returns>
        CommandProcessingResult ProcessCommands(string transcription, bool autoPunctuation = false);
        
        /// <summary>
        /// Detect commands in text without executing them
        /// </summary>
        /// <param name="text">Text to analyze</param>
        /// <returns>List of detected commands</returns>
        List<VoiceCommand> DetectCommands(string text);
        
        /// <summary>
        /// Apply auto-punctuation to text
        /// </summary>
        /// <param name="text">Text to punctuate</param>
        /// <returns>Text with punctuation added</returns>
        string ApplyAutoPunctuation(string text);
        
        /// <summary>
        /// Register a custom voice command
        /// </summary>
        /// <param name="trigger">The trigger phrase</param>
        /// <param name="commandType">Type of command</param>
        /// <param name="replacement">Replacement text or action</param>
        void RegisterCustomCommand(string trigger, VoiceCommandType commandType, string? replacement = null);
    }
    
    /// <summary>
    /// Implementation of voice command processing service
    /// </summary>
    public class CommandProcessingService : ICommandProcessingService
    {
        private readonly ILogger<CommandProcessingService> _logger;
        private readonly Dictionary<string, (VoiceCommandType Type, string? Replacement)> _commandMap;
        private readonly List<string> _sentenceEnders;
        private readonly List<string> _pauseWords;
        
        public CommandProcessingService(ILogger<CommandProcessingService> logger)
        {
            _logger = logger;
            _commandMap = InitializeCommandMap();
            _sentenceEnders = new List<string> { ".", "!", "?" };
            _pauseWords = new List<string> { "um", "uh", "like", "you know", "so" };
        }
        
        /// <summary>
        /// Initialize the default command mappings
        /// </summary>
        private Dictionary<string, (VoiceCommandType Type, string? Replacement)> InitializeCommandMap()
        {
            var map = new Dictionary<string, (VoiceCommandType, string?)>(StringComparer.OrdinalIgnoreCase)
            {
                // Punctuation commands
                ["period"] = (VoiceCommandType.Punctuation, "."),
                ["full stop"] = (VoiceCommandType.Punctuation, "."),
                ["dot"] = (VoiceCommandType.Punctuation, "."),
                ["comma"] = (VoiceCommandType.Punctuation, ","),
                ["question mark"] = (VoiceCommandType.Punctuation, "?"),
                ["exclamation mark"] = (VoiceCommandType.Punctuation, "!"),
                ["exclamation point"] = (VoiceCommandType.Punctuation, "!"),
                ["semicolon"] = (VoiceCommandType.Punctuation, ";"),
                ["colon"] = (VoiceCommandType.Punctuation, ":"),
                ["dash"] = (VoiceCommandType.Punctuation, "-"),
                ["hyphen"] = (VoiceCommandType.Punctuation, "-"),
                ["ellipsis"] = (VoiceCommandType.Punctuation, "..."),
                ["dot dot dot"] = (VoiceCommandType.Punctuation, "..."),
                ["quote"] = (VoiceCommandType.Punctuation, "\""),
                ["open quote"] = (VoiceCommandType.Punctuation, "\""),
                ["close quote"] = (VoiceCommandType.Punctuation, "\""),
                ["apostrophe"] = (VoiceCommandType.Punctuation, "'"),
                ["left parenthesis"] = (VoiceCommandType.Punctuation, "("),
                ["right parenthesis"] = (VoiceCommandType.Punctuation, ")"),
                ["open bracket"] = (VoiceCommandType.Punctuation, "["),
                ["close bracket"] = (VoiceCommandType.Punctuation, "]"),
                
                // Delete commands
                ["delete"] = (VoiceCommandType.Delete, null),
                ["backspace"] = (VoiceCommandType.Delete, null),
                ["rub out"] = (VoiceCommandType.Delete, null),
                ["scratch that"] = (VoiceCommandType.Delete, null),
                ["undo that"] = (VoiceCommandType.Undo, null),
                ["undo"] = (VoiceCommandType.Undo, null),
                
                // Navigation commands
                ["new line"] = (VoiceCommandType.NewLine, "\n"),
                ["new paragraph"] = (VoiceCommandType.NewLine, "\n\n"),
                ["next line"] = (VoiceCommandType.NewLine, "\n"),
                ["tab"] = (VoiceCommandType.Tab, "\t"),
                
                // Capitalization
                ["capitalize"] = (VoiceCommandType.Capitalize, null),
                ["caps on"] = (VoiceCommandType.Capitalize, null),
                ["all caps"] = (VoiceCommandType.Capitalize, null)
            };
            
            return map;
        }
        
        /// <summary>
        /// Process transcription text for voice commands
        /// </summary>
        public CommandProcessingResult ProcessCommands(string transcription, bool autoPunctuation = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(transcription))
                {
                    return new CommandProcessingResult
                    {
                        ProcessedText = transcription,
                        Success = true
                    };
                }
                
                var commands = DetectCommands(transcription);
                var processedText = ExecuteCommands(transcription, commands);
                
                // Apply auto-punctuation if enabled
                if (autoPunctuation)
                {
                    processedText = ApplyAutoPunctuation(processedText);
                }
                
                _logger.LogInformation("Processed {CommandCount} commands in transcription", commands.Count);
                
                return new CommandProcessingResult
                {
                    ProcessedText = processedText,
                    Commands = commands,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing voice commands");
                return new CommandProcessingResult
                {
                    ProcessedText = transcription,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        
        /// <summary>
        /// Detect all commands in the given text
        /// </summary>
        public List<VoiceCommand> DetectCommands(string text)
        {
            var commands = new List<VoiceCommand>();
            
            if (string.IsNullOrWhiteSpace(text))
                return commands;
            
            // To track which characters are already part of a command
            var matchedIndices = new bool[text.Length];
            
            // Check for each known command, longest first to prioritize "open quote" over "quote"
            foreach (var kvp in _commandMap.OrderByDescending(k => k.Key.Length))
            {
                var trigger = kvp.Key;
                var (type, replacement) = kvp.Value;
                
                // Find all occurrences of the trigger
                var index = text.IndexOf(trigger, StringComparison.OrdinalIgnoreCase);
                while (index != -1)
                {
                    // Check if it's a whole word AND not already matched
                    var isWholeWord = IsWholeWord(text, index, trigger.Length);
                    var rangeAlreadyMatched = false;
                    for (int i = 0; i < trigger.Length; i++)
                    {
                        if (index + i < matchedIndices.Length && matchedIndices[index + i])
                        {
                            rangeAlreadyMatched = true;
                            break;
                        }
                    }
                    
                    if (isWholeWord && !rangeAlreadyMatched)
                    {
                        commands.Add(new VoiceCommand
                        {
                            OriginalText = trigger,
                            Type = type,
                            Position = index,
                            Length = trigger.Length,
                            Replacement = replacement,
                            DeleteCount = type == VoiceCommandType.Delete ? 1 : 0
                        });
                        
                        // Mark indices as matched
                        for (int i = 0; i < trigger.Length; i++)
                        {
                            if (index + i < matchedIndices.Length)
                                matchedIndices[index + i] = true;
                        }
                    }
                    
                    // Find next occurrence
                    index = text.IndexOf(trigger, index + 1, StringComparison.OrdinalIgnoreCase);
                }
            }
            
            // Sort by position
            return commands.OrderBy(c => c.Position).ToList();
        }
        
        /// <summary>
        /// Check if the text at the given position is a whole word
        /// </summary>
        private bool IsWholeWord(string text, int position, int length)
        {
            // Check character before
            if (position > 0)
            {
                var charBefore = text[position - 1];
                if (char.IsLetterOrDigit(charBefore))
                    return false;
            }
            
            // Check character after
            var endPosition = position + length;
            if (endPosition < text.Length)
            {
                var charAfter = text[endPosition];
                if (char.IsLetterOrDigit(charAfter))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Execute detected commands and return processed text
        /// </summary>
                private string ExecuteCommands(string text, List<VoiceCommand> commands)
                {
                    if (commands.Count == 0)
                        return text;
        
                    var result = text;
                    // Process commands from end to start to avoid index shifting issues
                    foreach (var command in commands.OrderByDescending(c => c.Position))
                    {
                        var pos = command.Position;
        
                        switch (command.Type)
                        {
                            case VoiceCommandType.Punctuation:
                                if (!string.IsNullOrEmpty(command.Replacement))
                                {
                                    var replacement = command.Replacement;
                                    bool shouldHandleSpacing = ShouldHandleSpacingForPunctuation(replacement);
        
                                    if (shouldHandleSpacing && pos > 0 && result[pos - 1] == ' ')
                                    {
                                        result = result.Remove(pos - 1, command.Length + 1)
                                                      .Insert(pos - 1, replacement);
                                    }
                                    else
                                    {
                                        result = result.Remove(pos, command.Length)
                                                      .Insert(pos, replacement);
                                    }
                                }
                                break;
        
                            case VoiceCommandType.Delete:
                                if (pos > 0)
                                {
                                    if (result[pos - 1] == ' ' && pos > 1)
                                    {
                                        result = result.Remove(pos - 2, command.Length + 2);
                                    }
                                    else
                                    {
                                        result = result.Remove(pos - 1, command.Length + 1);
                                    }
                                }
                                else
                                {
                                    result = result.Remove(pos, command.Length);
                                }
                                break;
        
                            case VoiceCommandType.NewLine:
                            case VoiceCommandType.Tab:
                                if (!string.IsNullOrEmpty(command.Replacement))
                                {
                                    result = result.Remove(pos, command.Length)
                                                  .Insert(pos, command.Replacement);
                                }
                                break;
        
                            case VoiceCommandType.Undo:
                                result = result.Remove(pos, command.Length);
                                break;
        
                            case VoiceCommandType.Capitalize:
                                result = result.Remove(pos, command.Length);
                                var nextWordStart = FindNextWordStart(result, pos);
                                if (nextWordStart >= 0 && nextWordStart < result.Length)
                                {
                                    var nextWordEnd = FindNextWordEnd(result, nextWordStart);
                                    if (nextWordEnd > nextWordStart)
                                    {
                                        var word = result.Substring(nextWordStart, nextWordEnd - nextWordStart);
                                        var capitalized = char.ToUpper(word[0]) + word.Substring(1);
                                        result = result.Remove(nextWordStart, word.Length)
                                                      .Insert(nextWordStart, capitalized);
                                    }
                                }
                                break;
        
                            default:
                                result = result.Remove(pos, command.Length);
                                break;
                        }
                    }
        
                    return result.Trim();
                }
                /// <summary>
        /// Apply auto-punctuation heuristics to text
        /// </summary>
        public string ApplyAutoPunctuation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;
            
            var sentences = new List<string>();
            var currentSentence = "";
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                currentSentence += (currentSentence.Length > 0 ? " " : "") + word;
                
                // Check if this word ends a sentence
                if (IsSentenceEnder(word) || ShouldEndSentence(words, i))
                {
                    // Ensure sentence ends with punctuation
                    if (!currentSentence.EndsWith(".") && !currentSentence.EndsWith("!") && 
                        !currentSentence.EndsWith("?"))
                    {
                        currentSentence += ".";
                    }
                    sentences.Add(currentSentence);
                    currentSentence = "";
                }
            }
            
            // Add any remaining text
            if (!string.IsNullOrWhiteSpace(currentSentence))
            {
                if (!currentSentence.EndsWith(".") && !currentSentence.EndsWith("!") && 
                    !currentSentence.EndsWith("?"))
                {
                    currentSentence += ".";
                }
                sentences.Add(currentSentence);
            }
            
            // Capitalize first letter of each sentence
            for (int i = 0; i < sentences.Count; i++)
            {
                var sentence = sentences[i].Trim();
                if (sentence.Length > 0 && char.IsLower(sentence[0]))
                {
                    sentences[i] = char.ToUpper(sentence[0]) + sentence.Substring(1);
                }
            }
            
            return string.Join(" ", sentences);
        }
        
        /// <summary>
        /// Check if a word is a sentence-ending word
        /// </summary>
        private bool IsSentenceEnder(string word)
        {
            var cleanWord = word.TrimEnd('.', '!', '?', '"', ')');
            return _sentenceEnders.Any(e => word.EndsWith(e));
        }
        
        /// <summary>
        /// Determine if sentence should end based on context
        /// </summary>
        private bool ShouldEndSentence(string[] words, int currentIndex)
        {
            // If we're near the end, don't force a break
            if (currentIndex >= words.Length - 1)
                return true;
            
            var currentWord = words[currentIndex].ToLower();
            var nextWord = words[currentIndex + 1].ToLower();
            
            // Check for pause words that might indicate sentence break
            if (_pauseWords.Contains(currentWord) && currentIndex > 3)
            {
                // If we've had at least 4 words and hit a pause word, consider ending
                return true;
            }
            
            // Check for coordinating conjunctions that might start new sentence
            var conjunctions = new[] { "and", "but", "or", "so", "yet" };
            if (conjunctions.Contains(nextWord) && currentIndex > 5)
            {
                return true;
            }
            
            // Long sentences (15+ words) should break
            if (currentIndex > 15)
            {
                // Look for natural break points
                if (currentWord.EndsWith("ly") || currentWord.EndsWith("ed"))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Find the start of the next word
        /// </summary>
        private int FindNextWordStart(string text, int startPosition)
        {
            for (int i = startPosition; i < text.Length; i++)
            {
                if (char.IsLetterOrDigit(text[i]))
                    return i;
            }
            return -1;
        }
        
        /// <summary>
        /// Find the end of the current word
        /// </summary>
        private int FindNextWordEnd(string text, int startPosition)
        {
            for (int i = startPosition; i < text.Length; i++)
            {
                if (!char.IsLetterOrDigit(text[i]))
                    return i;
            }
            return text.Length;
        }
        
        /// <summary>
        /// Determines if punctuation should have spacing handling applied
        /// </summary>
        private bool ShouldHandleSpacingForPunctuation(string replacement)
        {
            // These punctuation marks typically shouldn't have spaces before them in English
            return replacement == "." || 
                   replacement == "," || 
                   replacement == "?" || 
                   replacement == "!" || 
                   replacement == ";" || 
                   replacement == ":";
        }
        
        /// <summary>
        /// Register a custom voice command
        /// </summary>
        public void RegisterCustomCommand(string trigger, VoiceCommandType commandType, string? replacement = null)
        {
            if (string.IsNullOrWhiteSpace(trigger))
                throw new ArgumentException("Trigger cannot be empty", nameof(trigger));
            
            _commandMap[trigger.ToLower()] = (commandType, replacement);
            _logger.LogInformation("Registered custom command: {Trigger} -> {Type}", trigger, commandType);
        }
    }
}
