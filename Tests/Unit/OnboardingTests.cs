using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Database;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class OnboardingTests
    {
        private OnboardingService _service = null!;
        private Mock<JsonDatabaseService> _mockDb = null!;
        private List<OnboardingState> _states = new();

        [TestInitialize]
        public void Setup()
        {
            _mockDb = new Mock<JsonDatabaseService>(new Mock<IFileSystemService>().Object, NullLogger<JsonDatabaseService>.Instance);
            _states = new List<OnboardingState>();

            _mockDb.Setup(db => db.QueryListAsync<OnboardingState>(It.IsAny<string>(), It.IsAny<Func<OnboardingState, bool>>()))
                .ReturnsAsync((string coll, Func<OnboardingState, bool> predicate) => _states.Where(predicate).ToList());

            _mockDb.Setup(db => db.UpsertAsync(It.IsAny<string>(), It.IsAny<OnboardingState>(), It.IsAny<Func<OnboardingState, bool>>()))
                .Callback<string, OnboardingState, Func<OnboardingState, bool>>((coll, item, identity) => 
                {
                    var existing = _states.FirstOrDefault(identity);
                    if (existing != null) _states.Remove(existing);
                    _states.Add(item);
                })
                .Returns(Task.CompletedTask);

            _service = new OnboardingService(
                NullLogger<OnboardingService>.Instance,
                _mockDb.Object);
        }

        [TestMethod]
        public async Task Test_CompleteModule()
        {
            await _service.CompleteModuleAsync("Hotkeys");
            
            var state = await _service.GetStateAsync();
            Assert.IsTrue(state.HotkeyTutorialCompleted);
            Assert.IsTrue(state.CompletedModules.ContainsKey("Hotkeys"));
        }

        [TestMethod]
        public async Task Test_WelcomeWalkthrough()
        {
            await _service.StartWelcomeAsync();
            
            var state = await _service.GetStateAsync();
            Assert.IsTrue(state.WelcomeCompleted);
        }
    }
}
