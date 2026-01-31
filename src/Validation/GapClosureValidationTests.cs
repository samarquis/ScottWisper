using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ScottWisper.Integration;
using ScottWisper.Services;
using ScottWisper.Validation;
using ScottWisper; // For CrossApplicationValidationResult

namespace ScottWisper.Tests
{
    [TestClass]
    public class GapClosureValidationTests
    {
        private Mock<ITextInjection> _textInjectionMock = null!;
        private Mock<ICrossApplicationValidator> _crossAppMock = null!;
        private Mock<IAudioDeviceService> _audioDeviceMock = null!;
        private Mock<ISettingsService> _settingsMock = null!;
        private Mock<IPermissionService> _permissionMock = null!;
        private Phase04Validator _validator = null!;
        private GapClosureTestRunner _runner = null!;

        [TestInitialize]
        public void Setup()
        {
            _textInjectionMock = new Mock<ITextInjection>();
            _crossAppMock = new Mock<ICrossApplicationValidator>();
            _audioDeviceMock = new Mock<IAudioDeviceService>();
            _settingsMock = new Mock<ISettingsService>();
            _permissionMock = new Mock<IPermissionService>();

            _validator = new Phase04Validator(
                _crossAppMock.Object,
                _audioDeviceMock.Object,
                _settingsMock.Object,
                _permissionMock.Object,
                NullLogger<Phase04Validator>.Instance
            );

            _runner = new GapClosureTestRunner(_validator, NullLogger<GapClosureTestRunner>.Instance);
        }

        [TestMethod]
        public async Task RunPhase04ComprehensiveValidation_ShouldPass()
        {
            // Setup successful mocks
            _textInjectionMock.Setup(x => x.InjectTextAsync(It.IsAny<string>(), It.IsAny<InjectionOptions>()))
                .Returns(Task.FromResult(true));
            
            _crossAppMock.Setup(x => x.ValidateCrossApplicationInjectionAsync())
                .Returns(Task.FromResult(new ScottWisper.CrossApplicationValidationResult { OverallSuccessRate = 100, TotalApplicationsTested = 1, SuccessfulApplications = 1 }));
            
            _audioDeviceMock.Setup(x => x.GetInputDevicesAsync())
                .Returns(Task.FromResult(new System.Collections.Generic.List<AudioDevice> { new AudioDevice { Id = "test-mic", Name = "Test Microphone" } }));
            
            _audioDeviceMock.Setup(x => x.GetDefaultInputDeviceAsync())
                .Returns(Task.FromResult(new AudioDevice { Id = "test-mic", Name = "Test Microphone" }));
            
            _audioDeviceMock.Setup(x => x.SwitchDeviceAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(true));

            _permissionMock.Setup(x => x.CheckMicrophonePermissionAsync())
                .Returns(Task.FromResult(MicrophonePermissionStatus.Granted));

            _settingsMock.Setup(x => x.Settings)
                .Returns(new Configuration.AppSettings { UI = new Configuration.UISettings() });
            
            _settingsMock.Setup(x => x.GetValueAsync<string>(It.IsAny<string>()))
                .Returns(Task.FromResult("Validation_Test_Value"));

            // Run validation
            bool success = await _runner.RunPhase04ValidationAsync();

            // Assert
            Assert.IsTrue(success, "Phase 04 Validation should succeed with mock services");
        }
    }
}
