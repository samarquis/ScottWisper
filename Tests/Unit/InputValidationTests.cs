using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Validation;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class InputValidationTests
    {
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private InputValidationService _validationService = null!;
        private SanitizationService _sanitizationService = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockAuditService = new Mock<IAuditLoggingService>();
            _validationService = new InputValidationService(_mockAuditService.Object);
            _sanitizationService = new SanitizationService();
        }

        [TestMethod]
        public void Validate_Required_FailsWhenEmpty()
        {
            var rules = new ValidationRuleSet { Required = true };
            var result = _validationService.Validate("", rules);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Contains("Input is required."));
        }

        [TestMethod]
        public void Validate_Length_FailsWhenTooShort()
        {
            var rules = new ValidationRuleSet { MinLength = 5 };
            var result = _validationService.Validate("123", rules);
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains(result.Errors[0], "at least 5 characters");
        }

        [TestMethod]
        public void Sanitize_EncodesHtml()
        {
            var input = "<script>alert('xss')</script>";
            var result = _sanitizationService.Sanitize(input);
            Assert.AreEqual("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;", result);
        }

        [TestMethod]
        public void SanitizeHtml_RemovesScriptTags()
        {
            var input = "<div>Hello<script>alert('xss')</script></div>";
            var result = _sanitizationService.SanitizeHtml(input);
            Assert.AreEqual("<div>Hello</div>", result);
        }

        [TestMethod]
        public void NormalizePath_PreventsTraversal()
        {
            var path = @"C:\App\Data\..\..\Windows\System32\cmd.exe";
            var result = _sanitizationService.NormalizePath(path);
            Assert.IsFalse(result.Contains(".."));
        }

        #region Extended Validation Tests

        [TestMethod]
        public void Validate_Required_WithWhitespace_Fails()
        {
            var rules = new ValidationRuleSet { Required = true };
            var result = _validationService.Validate("   ", rules);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Contains("Input is required."));
        }

        [TestMethod]
        public void Validate_MaxLength_FailsWhenTooLong()
        {
            var rules = new ValidationRuleSet { MaxLength = 5 };
            var result = _validationService.Validate("123456", rules);
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains(result.Errors[0], "cannot exceed 5 characters");
        }

        [TestMethod]
        public void Validate_RegexPattern_FailsWhenInvalid()
        {
            var rules = new ValidationRuleSet { RegexPattern = @"^[a-zA-Z]+$" };
            var result = _validationService.Validate("test123", rules);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Contains("Input format is invalid."));
        }

        [TestMethod]
        public void Validate_RegexPattern_PassesWhenValid()
        {
            var rules = new ValidationRuleSet { RegexPattern = @"^[a-zA-Z]+$" };
            var result = _validationService.Validate("test", rules);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void Validate_AllowedCharacters_FailsWhenInvalid()
        {
            var rules = new ValidationRuleSet { AllowedCharacters = "abc123" };
            var result = _validationService.Validate("testd", rules);
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains(result.Errors[0], "Character 'd' is not allowed.");
        }

        [TestMethod]
        public void Validate_AllowedCharacters_PassesWhenValid()
        {
            var rules = new ValidationRuleSet { AllowedCharacters = "abc123" };
            var result = _validationService.Validate("abc123", rules);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void Validate_AllRules_Combined()
        {
            var rules = new ValidationRuleSet 
            { 
                Required = true,
                MinLength = 3,
                MaxLength = 10,
                RegexPattern = @"^[a-zA-Z0-9]+$",
                AllowedCharacters = "abc123XYZ"
            };

            // Test valid input
            var validResult = _validationService.Validate("abc123", rules);
            Assert.IsTrue(validResult.IsValid);

            // Test invalid input (contains invalid character)
            var invalidResult = _validationService.Validate("abc123!", rules);
            Assert.IsFalse(invalidResult.IsValid);
        }

        [TestMethod]
        public void Validate_EmptyInput_WithOnlyLengthRules_Passes()
        {
            var rules = new ValidationRuleSet { MinLength = 5, MaxLength = 10 };
            var result = _validationService.Validate("", rules);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void Validate_NullInput_WithRequiredRule_Fails()
        {
            var rules = new ValidationRuleSet { Required = true };
            var result = _validationService.Validate(null!, rules);
            Assert.IsFalse(result.IsValid);
        }

        [TestMethod]
        public void Validate_NullInput_WithoutRequiredRule_Passes()
        {
            var rules = new ValidationRuleSet { MinLength = 5 };
            var result = _validationService.Validate(null!, rules);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void ValidateObject_GenericObject_ReturnsValidResult()
        {
            var testObject = new { Name = "test", Value = 123 };
            var result = _validationService.ValidateObject(testObject);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void ValidateObject_NullObject_ReturnsValidResult()
        {
            var result = _validationService.ValidateObject<object>(null!);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void Validate_InvalidPattern_LogsAuditEvent()
        {
            var rules = new ValidationRuleSet { Required = true };
            var result = _validationService.Validate("", rules);
            
            Assert.IsFalse(result.IsValid);
            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.SecurityEvent,
                It.Is<string>(s => s.Contains("Input validation failed")),
                It.IsAny<string>(),
                DataSensitivity.Low), Times.Once);
        }

        [TestMethod]
        public void Validate_MultipleErrors_ReturnsAllErrors()
        {
            var rules = new ValidationRuleSet 
            { 
                Required = true,
                MinLength = 5,
                MaxLength = 10,
                RegexPattern = @"^[a-zA-Z]+$"
            };

            var result = _validationService.Validate("ab", rules);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Count >= 2); // Should have at least min length and regex errors
        }

        [TestMethod]
        public void Validate_EdgeCaseBoundaryValues_Passes()
        {
            var rules = new ValidationRuleSet { MinLength = 3, MaxLength = 5 };
            
            var minResult = _validationService.Validate("abc", rules);
            Assert.IsTrue(minResult.IsValid);

            var maxResult = _validationService.Validate("abcde", rules);
            Assert.IsTrue(maxResult.IsValid);
        }

        [TestMethod]
        public void SanitizeHtml_RemovesIframeTags()
        {
            var input = "<div><iframe src='evil.com'></iframe>content</div>";
            var result = _sanitizationService.SanitizeHtml(input);
            Assert.IsFalse(result.Contains("iframe"));
            Assert.IsTrue(result.Contains("content"));
        }

        [TestMethod]
        public void SanitizeHtml_RemovesEventHandlers()
        {
            var input = "<div onclick='evil()' onload='alsoevil()'>content</div>";
            var result = _sanitizationService.SanitizeHtml(input);
            Assert.IsFalse(result.Contains("onclick"));
            Assert.IsFalse(result.Contains("onload"));
        }

        [TestMethod]
        public void SanitizeHtml_CaseInsensitive()
        {
            var input = "<SCRIPT>alert('xss')</SCRIPT>";
            var result = _sanitizationService.SanitizeHtml(input);
            Assert.IsFalse(result.Contains("SCRIPT"));
        }

        [TestMethod]
        public void NormalizePath_HandlesMultipleSlashes()
        {
            var path = @"C:\\App\\Data\\file.txt";
            var result = _sanitizationService.NormalizePath(path);
            Assert.IsFalse(result.Contains("\\\\"));
            Assert.IsTrue(result.Contains("\\"));
        }

        [TestMethod]
        public void Sanitize_NullInput_ReturnsNull()
        {
            var result = _sanitizationService.Sanitize(null!);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Sanitize_EmptyString_ReturnsEmpty()
        {
            var result = _sanitizationService.Sanitize("");
            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void SanitizeHtml_NullInput_ReturnsNull()
        {
            var result = _sanitizationService.SanitizeHtml(null!);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void NormalizePath_NullInput_ReturnsNull()
        {
            var result = _sanitizationService.NormalizePath(null!);
            Assert.IsNull(result);
        }

        #endregion
    }
}
