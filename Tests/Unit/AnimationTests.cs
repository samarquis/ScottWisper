using System;
using System.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class AnimationTests
    {
        private AnimationService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new AnimationService(NullLogger<AnimationService>.Instance);
        }

        [TestMethod]
        public void Test_AnimationService_Initialization()
        {
            Assert.IsNotNull(_service);
        }

        [TestMethod]
        public void Test_FadeIn_NullCheck()
        {
            // Should not throw
            _service.FadeIn(null!, TimeSpan.FromSeconds(1));
        }
    }
}
