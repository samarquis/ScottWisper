using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using WhisperKey.Models;

namespace WhisperKey.Services.Validation
{
    public class SanitizationService : ISanitizationService
    {
        public string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Trim whitespace
            var sanitized = input.Trim();

            // Basic XSS protection: encode HTML special characters
            return HttpUtility.HtmlEncode(sanitized);
        }

        public string SanitizeHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;

            // Remove potentially dangerous tags using regex (whitelist approach is better, but this is a start)
            var sanitized = Regex.Replace(html, @"<script.*?>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            sanitized = Regex.Replace(sanitized, @"<iframe.*?>.*?</iframe>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            sanitized = Regex.Replace(sanitized, @"on\w+\s*=", "", RegexOptions.IgnoreCase); // Remove event handlers

            return sanitized;
        }

        public string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // Remove path traversal characters
            return path.Replace("..", "").Replace("//", "/").Replace(@"\\", @"\");
        }
    }

    public class InputValidationService : IInputValidationService
    {
        private readonly IAuditLoggingService _auditService;

        public InputValidationService(IAuditLoggingService auditService)
        {
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public ValidationResult Validate(string input, ValidationRuleSet rules)
        {
            var result = new ValidationResult();

            if (rules.Required && string.IsNullOrWhiteSpace(input))
            {
                result.AddError("Input is required.");
            }

            if (!string.IsNullOrEmpty(input))
            {
                if (rules.MinLength.HasValue && input.Length < rules.MinLength.Value)
                {
                    result.AddError($"Input must be at least {rules.MinLength.Value} characters long.");
                }

                if (rules.MaxLength.HasValue && input.Length > rules.MaxLength.Value)
                {
                    result.AddError($"Input cannot exceed {rules.MaxLength.Value} characters.");
                }

                if (!string.IsNullOrEmpty(rules.RegexPattern) && !Regex.IsMatch(input, rules.RegexPattern))
                {
                    result.AddError("Input format is invalid.");
                }

                if (!string.IsNullOrEmpty(rules.AllowedCharacters))
                {
                    foreach (var c in input)
                    {
                        if (!rules.AllowedCharacters.Contains(c))
                        {
                            result.AddError($"Character '{c}' is not allowed.");
                            break;
                        }
                    }
                }
            }

            if (!result.IsValid)
            {
                // Audit the validation failure
                _ = _auditService.LogEventAsync(
                    AuditEventType.SecurityEvent,
                    $"Input validation failed: {string.Join(", ", result.Errors)}",
                    null,
                    DataSensitivity.Low);
            }

            return result;
        }

        public ValidationResult ValidateObject<T>(T obj)
        {
            // Simple generic object validation using Reflection or DataAnnotations could be added here
            return new ValidationResult();
        }
    }
}