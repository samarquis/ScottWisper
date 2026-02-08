using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Represents a vocabulary pack for specialized terminology
    /// </summary>
    public class VocabularyPack
    {
        /// <summary>
        /// Unique identifier for the vocabulary pack
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Pack name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Pack description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Industry category (Medical, Legal, Technical, etc.)
        /// </summary>
        public VocabularyIndustry Industry { get; set; }
        
        /// <summary>
        /// Whether this pack is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// Version of the vocabulary pack
        /// </summary>
        public string Version { get; set; } = "1.0";
        
        /// <summary>
        /// Terms in this vocabulary pack
        /// </summary>
        public List<VocabularyTerm> Terms { get; set; } = new();
        
        /// <summary>
        /// Common phrases or patterns specific to this industry
        /// </summary>
        public List<string> CommonPhrases { get; set; } = new();
        
        /// <summary>
        /// Pronunciation hints for specialized terms
        /// </summary>
        public Dictionary<string, string> PronunciationHints { get; set; } = new();
        
        /// <summary>
        /// When the pack was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the pack was last updated
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Priority level for term matching (higher = more likely to match)
        /// </summary>
        public int Priority { get; set; } = 100;
        
        /// <summary>
        /// Language code (e.g., "en-US", "en-GB")
        /// </summary>
        public string Language { get; set; } = "en-US";
        
        /// <summary>
        /// Get all terms as a formatted string for context injection
        /// </summary>
        public string GetContextString()
        {
            return string.Join(", ", Terms.ConvertAll(t => t.Term));
        }
    }
    
    /// <summary>
    /// Industry categories for vocabulary packs
    /// </summary>
    public enum VocabularyIndustry
    {
        /// <summary>
        /// General vocabulary
        /// </summary>
        General,
        
        /// <summary>
        /// Medical/Healthcare terminology
        /// </summary>
        Medical,
        
        /// <summary>
        /// Legal terminology
        /// </summary>
        Legal,
        
        /// <summary>
        /// Technical/IT terminology
        /// </summary>
        Technical,
        
        /// <summary>
        /// Financial terminology
        /// </summary>
        Financial,
        
        /// <summary>
        /// Academic/Research terminology
        /// </summary>
        Academic,
        
        /// <summary>
        /// Engineering terminology
        /// </summary>
        Engineering,
        
        /// <summary>
        /// Scientific terminology
        /// </summary>
        Scientific
    }
    
    /// <summary>
    /// Individual vocabulary term
    /// </summary>
    public class VocabularyTerm
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// The term itself
        /// </summary>
        public string Term { get; set; } = string.Empty;
        
        /// <summary>
        /// Alternative spellings or variations
        /// </summary>
        public List<string> Variations { get; set; } = new();
        
        /// <summary>
        /// Definition or description
        /// </summary>
        public string Definition { get; set; } = string.Empty;
        
        /// <summary>
        /// Category within the industry (e.g., "Anatomy", "Cardiology" for Medical)
        /// </summary>
        public string Category { get; set; } = string.Empty;
        
        /// <summary>
        /// Frequency weight (how common the term is)
        /// </summary>
        public int Frequency { get; set; } = 1;
        
        /// <summary>
        /// Whether this is a high-priority term
        /// </summary>
        public bool IsHighPriority { get; set; } = false;
        
        /// <summary>
        /// Common misspellings to watch for
        /// </summary>
        public List<string> CommonMisspellings { get; set; } = new();
        
        /// <summary>
        /// Example usage in context
        /// </summary>
        public string ExampleUsage { get; set; } = string.Empty;
        
        /// <summary>
        /// When the term was added
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Custom user vocabulary
    /// </summary>
    public class CustomVocabulary
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// User's custom terms
        /// </summary>
        public List<VocabularyTerm> CustomTerms { get; set; } = new();
        
        /// <summary>
        /// User's custom phrases or shortcuts
        /// </summary>
        public Dictionary<string, string> CustomPhrases { get; set; } = new();
        
        /// <summary>
        /// Names and proper nouns specific to the user
        /// </summary>
        public List<string> ProperNouns { get; set; } = new();
        
        /// <summary>
        /// Technical terms specific to user's work
        /// </summary>
        public List<string> TechnicalTerms { get; set; } = new();
        
        /// <summary>
        /// Acronyms and abbreviations the user commonly uses
        /// </summary>
        public Dictionary<string, string> Acronyms { get; set; } = new();
        
        /// <summary>
        /// When created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When last modified
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Context for transcription with vocabulary enhancement
    /// </summary>
    public class VocabularyContext
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Name of this context
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Enabled vocabulary packs
        /// </summary>
        public List<string> EnabledPackIds { get; set; } = new();
        
        /// <summary>
        /// Custom vocabulary ID
        /// </summary>
        public string? CustomVocabularyId { get; set; }
        
        /// <summary>
        /// Additional context hints for transcription
        /// </summary>
        public string? AdditionalContext { get; set; }
        
        /// <summary>
        /// Priority boost factor for vocabulary terms
        /// </summary>
        public double PriorityBoost { get; set; } = 1.0;
        
        /// <summary>
        /// Whether to use case-sensitive matching
        /// </summary>
        public bool CaseSensitive { get; set; } = false;
        
        /// <summary>
        /// Maximum distance for fuzzy matching
        /// </summary>
        public int FuzzyMatchThreshold { get; set; } = 2;
        
        /// <summary>
        /// When created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When last used
        /// </summary>
        public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Result of vocabulary-enhanced transcription
    /// </summary>
    public class VocabularyEnhancedResult
    {
        /// <summary>
        /// Original transcription text
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;
        
        /// <summary>
        /// Enhanced text with vocabulary corrections
        /// </summary>
        public string EnhancedText { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether any corrections were made
        /// </summary>
        public bool HasCorrections => Corrections.Count > 0;
        
        /// <summary>
        /// List of corrections applied
        /// </summary>
        public List<VocabularyCorrection> Corrections { get; set; } = new();
        
        /// <summary>
        /// Vocabulary packs that contributed to corrections
        /// </summary>
        public List<string> ContributingPackIds { get; set; } = new();
        
        /// <summary>
        /// Confidence score after enhancement
        /// </summary>
        public double Confidence { get; set; } = 1.0;
    }
    
    /// <summary>
    /// Individual vocabulary correction
    /// </summary>
    public class VocabularyCorrection
    {
        /// <summary>
        /// Original (incorrect) text
        /// </summary>
        public string Original { get; set; } = string.Empty;
        
        /// <summary>
        /// Corrected text
        /// </summary>
        public string Corrected { get; set; } = string.Empty;
        
        /// <summary>
        /// Position in text where correction was made
        /// </summary>
        public int Position { get; set; }
        
        /// <summary>
        /// Confidence in the correction
        /// </summary>
        public double Confidence { get; set; } = 1.0;
        
        /// <summary>
        /// Source vocabulary pack or custom term
        /// </summary>
        public string Source { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of correction
        /// </summary>
        public CorrectionType Type { get; set; }
    }
    
    /// <summary>
    /// Types of vocabulary corrections
    /// </summary>
    public enum CorrectionType
    {
        /// <summary>
        /// Spelling correction
        /// </summary>
        Spelling,
        
        /// <summary>
        /// Term expansion (acronym -> full form)
        /// </summary>
        Expansion,
        
        /// <summary>
        /// Term abbreviation (full form -> acronym)
        /// </summary>
        Abbreviation,
        
        /// <summary>
        /// Proper noun capitalization
        /// </summary>
        Capitalization,
        
        /// <summary>
        /// Medical term correction
        /// </summary>
        MedicalTerm,
        
        /// <summary>
        /// Legal term correction
        /// </summary>
        LegalTerm,
        
        /// <summary>
        /// Technical term correction
        /// </summary>
        TechnicalTerm,
        
        /// <summary>
        /// Custom user term
        /// </summary>
        CustomTerm
    }
    
    /// <summary>
    /// Statistics for vocabulary usage
    /// </summary>
    public class VocabularyStatistics
    {
        /// <summary>
        /// Total number of enabled packs
        /// </summary>
        public int EnabledPacksCount { get; set; }
        
        /// <summary>
        /// Total number of custom terms
        /// </summary>
        public int CustomTermsCount { get; set; }
        
        /// <summary>
        /// Number of corrections made in last session
        /// </summary>
        public int CorrectionsInLastSession { get; set; }
        
        /// <summary>
        /// Most commonly corrected terms
        /// </summary>
        public List<string> MostCommonCorrections { get; set; } = new();
        
        /// <summary>
        /// Correction accuracy rate
        /// </summary>
        public double CorrectionAccuracy { get; set; } = 0.0;
        
        /// <summary>
        /// When statistics were generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
