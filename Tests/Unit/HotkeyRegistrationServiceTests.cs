using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class HotkeyRegistrationServiceTests
    {
        private Mock<IHotkeyRegistrar> _win32RegistrarMock = null!;
        private Mock<ILogger<HotkeyRegistrationService>> _loggerMock = null!;
        private HotkeyRegistrationService _registrationService = null!;
        private IntPtr _testWindowHandle = new IntPtr(12345);

                [TestInitialize]
                public void Setup()
                {
                    _win32RegistrarMock = new Mock<IHotkeyRegistrar>();
                    _win32RegistrarMock.Setup(r => r.UnregisterHotKey(It.IsAny<IntPtr>(), It.IsAny<int>())).Returns(true);
                    _loggerMock = new Mock<ILogger<HotkeyRegistrationService>>();
        
                    _registrationService = new HotkeyRegistrationService(
                        _win32RegistrarMock.Object,
                        _loggerMock.Object,
                        _testWindowHandle
                    );
                }
                [TestCleanup]
        public void Cleanup()
        {
            _registrationService?.Dispose();
        }

        [TestMethod]
        public void RegisterHotkey_ValidCombination_ReturnsTrue()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            _win32RegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            var result = _registrationService.RegisterHotkey(hotkey);

            Assert.IsTrue(result);
            Assert.IsTrue(_registrationService.IsHotkeyRegistered(hotkey.Id));
        }

        [TestMethod]
        public void RegisterHotkey_NullHotkey_ReturnsFalse()
        {
            var result = _registrationService.RegisterHotkey(null!);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RegisterHotkey_EmptyCombination_ReturnsFalse()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "",
                IsEnabled = true
            };

            var result = _registrationService.RegisterHotkey(hotkey);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RegisterHotkey_DuplicateRegistration_UnregistersFirst()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            _win32RegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            // First registration
            var firstResult = _registrationService.RegisterHotkey(hotkey);
            Assert.IsTrue(firstResult);

            // Second registration should unregister first
            var secondResult = _registrationService.RegisterHotkey(hotkey);
            Assert.IsTrue(secondResult);

            _win32RegistrarMock.Verify(r => r.UnregisterHotKey(_testWindowHandle, It.IsAny<int>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public void UnregisterHotkey_ExistingHotkey_ReturnsTrue()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            _win32RegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            _registrationService.RegisterHotkey(hotkey);
            var result = _registrationService.UnregisterHotkey(hotkey.Id);

            Assert.IsTrue(result);
            Assert.IsFalse(_registrationService.IsHotkeyRegistered(hotkey.Id));
        }

        [TestMethod]
        public void UnregisterHotkey_NonExistentHotkey_ReturnsFalse()
        {
            var result = _registrationService.UnregisterHotkey("non_existent_id");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void UnregisterAllHotkeys_ClearsRegistrations()
        {
            var hotkey1 = new HotkeyDefinition
            {
                Id = "test_hotkey_1",
                Name = "Test Hotkey 1",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            var hotkey2 = new HotkeyDefinition
            {
                Id = "test_hotkey_2",
                Name = "Test Hotkey 2",
                Combination = "Ctrl+Alt+R",
                IsEnabled = true
            };

            _win32RegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            _registrationService.RegisterHotkey(hotkey1);
            _registrationService.RegisterHotkey(hotkey2);

            _registrationService.UnregisterAllHotkeys();

            Assert.IsFalse(_registrationService.IsHotkeyRegistered(hotkey1.Id));
            Assert.IsFalse(_registrationService.IsHotkeyRegistered(hotkey2.Id));
        }

        [TestMethod]
        public void Dispose_UnregistersAllHotkeys()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            _win32RegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            _registrationService.RegisterHotkey(hotkey);
            _registrationService.Dispose();

            _win32RegistrarMock.Verify(r => r.UnregisterHotKey(_testWindowHandle, It.IsAny<int>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public void GetRegisteredHotkeys_ReturnsCorrectCount()
        {
            var hotkey1 = new HotkeyDefinition
            {
                Id = "test_hotkey_1",
                Name = "Test Hotkey 1",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            var hotkey2 = new HotkeyDefinition
            {
                Id = "test_hotkey_2",
                Name = "Test Hotkey 2",
                Combination = "Ctrl+Alt+R",
                IsEnabled = true
            };

            _win32RegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            _registrationService.RegisterHotkey(hotkey1);
            _registrationService.RegisterHotkey(hotkey2);

            var registeredHotkeys = _registrationService.GetRegisteredHotkeys();
            Assert.AreEqual(2, registeredHotkeys.Count);
            Assert.IsTrue(registeredHotkeys.ContainsKey(hotkey1.Id));
            Assert.IsTrue(registeredHotkeys.ContainsKey(hotkey2.Id));
        }
    }
}
