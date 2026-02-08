using System;
using System.Collections.Generic;

namespace WhisperKey.Services.Validation
{
    /// <summary>
    /// Interface for input validation services.
    /// </summary>
    public interface IInputValidationService
    {
        /// <summary>
        /// Validates an input string against a set of rules.
        /// </summary>
        ValidationResult Validate(string input, ValidationRuleSet rules);

        /// <summary>
        /// Validates a complex object.
        /// </summary>
        ValidationResult ValidateObject<T>(T obj);
    }

    /// <summary>
    /// Interface for input sanitization services.
    /// </summary>
    public interface ISanitizationService
    {
        /// <summary>
        /// Sanitizes an input string to prevent XSS and other injections.
        /// </summary>
        string Sanitize(string input);

        /// <summary>
        /// Sanitizes HTML content.
        /// </summary>
        string SanitizeHtml(string html);

        /// <summary>
        /// Normalizes file paths to prevent traversal.
        /// </summary>
        string NormalizePath(string path);
    }

    /// <summary>
    /// Result of a validation operation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }
    }

    /// <summary>
    /// Set of rules for validation.
    /// </summary>
    public class ValidationRuleSet
    {
        public bool Required { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string? RegexPattern { get; set; }
        public string? AllowedCharacters { get; set; }
        public bool NoHtml { get; set; } = true;
    }
}
