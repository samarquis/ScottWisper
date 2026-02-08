using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class SimpleFunctionalityTests
    {
        [TestMethod]
        public async Task BasicServices_ShouldInstantiate_Successfully()
        {
            // Arrange & Act - Test core service instantiation
            
            // Test AudioCaptureService
            try
            {
                using var audioService = new AudioCaptureService();
                var devices = AudioCaptureService.GetAvailableDevices();
                Assert.IsNotNull(devices, "Audio devices list should not be null");
                Console.WriteLine($"✓ AudioCaptureService instantiated with {devices.Length} devices");
            }
            catch (Exception ex)
            {
                Assert.Fail($"AudioCaptureService instantiation failed: {ex.Message}");
            }
            
            // Test WhisperService (may fail without API key, that's expected)
            try
            {
                using var whisperService = new WhisperService();
                var usage = whisperService.GetUsageStats();
                Assert.IsNotNull(usage, "Usage stats should not be null");
                Console.WriteLine("✓ WhisperService instantiated successfully");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("OPENAI_API_KEY"))
            {
                Console.WriteLine("⚠ WhisperService API key not set (expected for development)");
                // This is expected behavior in development environment
            }
            catch (Exception ex)
            {
                Assert.Fail($"WhisperService instantiation failed unexpectedly: {ex.Message}");
            }
        }

        [TestMethod]
        public void Application_ShouldHaveValidStructure()
        {
            // Find project root by looking for WhisperKey.csproj starting from assembly location
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var projectRoot = "";
            var dir = new System.IO.DirectoryInfo(currentDir);
            
            while (dir != null)
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "WhisperKey.csproj")))
                {
                    projectRoot = dir.FullName;
                    break;
                }
                dir = dir.Parent;
            }

            Assert.IsFalse(string.IsNullOrEmpty(projectRoot), "Could not locate project root directory");
            
            // Test that the application structure is valid
            Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(projectRoot, "WhisperKey.csproj")), "Project file should exist");
            Assert.IsTrue(System.IO.Directory.Exists(System.IO.Path.Combine(projectRoot, "src")), "Source directory should exist");
            Assert.IsTrue(System.IO.Directory.Exists(System.IO.Path.Combine(projectRoot, "Tests")), "Tests directory should exist");
            Console.WriteLine("✓ Application structure is valid");
        }

        [TestMethod]
        public async Task ServiceTest_RunBasicTests_ReturnsTrue()
        {
            // Test the existing ServiceTest functionality
            var result = await ServiceTest.RunBasicTests();
            Assert.IsTrue(result, "ServiceTest.RunBasicTests should return true");
            Console.WriteLine("✓ ServiceTest.RunBasicTests passed");
        }
    }
}
