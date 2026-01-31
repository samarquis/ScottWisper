using System;

namespace ScottWisper.Models
{
    /// <summary>
    /// Represents a voice command detected in transcription text
    /// </summary>
    public class VoiceCommand
    {
        /// <summary>
        /// The original text that triggered the command
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;
        
        /// <summary>
        /// The type of command detected
        /// </summary>
        public VoiceCommandType Type { get; set; }
        
        /// <summary>
        /// The position in the text where the command was found
        /// </summary>
        public int Position { get; set; }
        
        /// <summary>
        /// The length of the command text in the original transcription
        /// </summary>
        public int Length { get; set; }
        
        /// <summary>
        /// The character or action to insert (for punctuation commands)
        /// </summary>
        public string? Replacement { get; set; }
        
        /// <summary>
        /// Number of characters to delete (for delete commands)
        /// </summary>
        public int DeleteCount { get; set; }
        
        /// <summary>
        /// Timestamp when the command was detected
        /// </summary>
        public DateTime DetectedAt { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Types of voice commands supported
    /// </summary>
    public enum VoiceCommandType
    {
        /// <summary>
        /// Punctuation command (period, comma, etc.)
        /// </summary>
        Punctuation,
        
        /// <summary>
        /// Delete command (remove previous character/word)
        /// </summary>
        Delete,
        
        /// <summary>
        /// New line command
        /// </summary>
        NewLine,
        
        /// <summary>
        /// Undo last action
        /// </summary>
        Undo,
        
        /// <summary>
        /// Select text command
        /// </summary>
        Select,
        
        /// <summary>
        /// Capitalization command
        /// </summary>
        Capitalize,
        
        /// <summary>
        /// Tab insertion command
        /// </summary>
        Tab,
        
        /// <summary>
        /// Unknown or unrecognized command
        /// </summary>
        Unknown
    }
    
    /// <summary>
    /// Result of processing transcription text for voice commands
    /// </summary>
    public class CommandProcessingResult
    {
        /// <summary>
        /// The processed text with commands executed
        /// </summary>
        public string ProcessedText { get; set; } = string.Empty;
        
        /// <summary>
        /// List of commands that were detected and executed
        /// </summary>
        public System.Collections.Generic.List<VoiceCommand> Commands { get; set; } = new();
        
        /// <summary>
        /// Whether any commands were detected
        /// </summary>
        public bool HasCommands => Commands.Count > 0;
        
        /// <summary>
        /// Whether the processing was successful
        /// </summary>
        public bool Success { get; set; } = true;
        
        /// <summary>
        /// Error message if processing failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
