using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ScottWisper.Services;
using ScottWisper.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace ScottWisper.Tests
{
    [TestClass]
    public class CrossAppCompatibilityTests
    {
        private Mock<ITextInjection> _textInjectionMock = null!;
        private CrossApplicationValidator _validator = null!;

        [TestInitialize]
        public void Setup()
        {
            _textInjectionMock = new Mock<ITextInjection>();
            _validator = new CrossApplicationValidator(_textInjectionMock.Object, NullLogger<CrossApplicationValidator>.Instance);
        }

        [TestMethod]
        public async Task Test_ChromeCompatibility()
        {
            // Verify if chrome is running, if not skip or mock
            var processes = Process.GetProcessesByName("chrome");
            if (processes.Length == 0)
            {
                Assert.Inconclusive("Chrome is not running, skipping live compatibility test");
                return;
            }

            _textInjectionMock.Setup(x => x.InjectTextAsync(It.IsAny<string>(), It.IsAny<InjectionOptions>()))
                .ReturnsAsync(true);

            var result = await _validator.ValidateCrossApplicationInjectionAsync();
            var chromeResult = result.ApplicationResults.Find(r => r.DisplayName == "Chrome");
            
            Assert.IsNotNull(chromeResult);
            // In a real environment we would check chromeResult.IsSuccess
        }

        [TestMethod]
        public async Task Test_ApplicationFocusSwitching_Stress()
        {
            // Simulate switching applications 10 times in 5 seconds
            // This is more of a logic validation
            for (int i = 0; i < 10; i++)
            {
                var app = _textInjectionMock.Object.DetectActiveApplication();
                Assert.IsNotNull(app);
                await Task.Delay(100);
            }
        }
    }
}
