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
    }
}
