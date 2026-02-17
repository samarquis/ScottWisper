using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WhisperKey.Tests.Integration
{
    [TestClass]
    public class InstallerValidationTests
    {
        private static string? _msiPath;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            var solutionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (solutionDir != null && !File.Exists(Path.Combine(solutionDir, "WhisperKey.sln")))
            {
                solutionDir = Path.GetDirectoryName(solutionDir);
            }

            if (solutionDir != null)
            {
                var msiDir = Path.Combine(solutionDir, ".wix", "bin", "Release");
                if (Directory.Exists(msiDir))
                {
                    var msiFiles = Directory.GetFiles(msiDir, "*.msi");
                    _msiPath = msiFiles.FirstOrDefault();
                }
            }

            context.WriteLine($"MSI Path: {_msiPath ?? "Not found"}");
        }

        [TestMethod]
        public void Test_MSI_Exists()
        {
            Assert.IsNotNull(_msiPath, "MSI file should exist at .wix/bin/Release/");
            Assert.IsTrue(File.Exists(_msiPath), $"MSI file not found at: {_msiPath}");
        }

        [TestMethod]
        public void Test_MSI_ValidSize()
        {
            if (_msiPath == null) Assert.Inconclusive("MSI not found");

            var fileInfo = new FileInfo(_msiPath);
            var sizeInMB = fileInfo.Length / (1024.0 * 1024.0);

            Assert.IsTrue(sizeInMB > 0.1, "MSI should be at least 100KB");
            Assert.IsTrue(sizeInMB < 50, "MSI should be less than 50MB");

            Console.WriteLine($"MSI Size: {sizeInMB:F2} MB");
        }

        [TestMethod]
        public void Test_MSI_ValidVersion()
        {
            if (_msiPath == null) Assert.Inconclusive("MSI not found");

            var fileName = Path.GetFileNameWithoutExtension(_msiPath);
            Assert.IsTrue(fileName.StartsWith("WhisperKey-"), "MSI should have WhisperKey prefix");

            var version = fileName.Replace("WhisperKey-", "");
            var parts = version.Split('.');
            Assert.AreEqual(3, parts.Length, "Version should have 3 parts (X.Y.Z)");

            Assert.IsTrue(int.TryParse(parts[0], out var major), "Major version should be numeric");
            Assert.IsTrue(major >= 1, "Major version should be >= 1");

            Console.WriteLine($"MSI Version: {version}");
        }

        [TestMethod]
        public void Test_WiX_Files_Exist()
        {
            var solutionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (solutionDir != null && !File.Exists(Path.Combine(solutionDir, "WhisperKey.sln")))
            {
                solutionDir = Path.GetDirectoryName(solutionDir);
            }

            Assert.IsNotNull(solutionDir, "Solution directory should exist");

            var wixDir = Path.Combine(solutionDir, ".wix");
            Assert.IsTrue(Directory.Exists(wixDir), ".wix directory should exist");

            var requiredFiles = new[]
            {
                "WhisperKey.Setup.wxs",
                "WhisperKey.Setup.wixproj",
                "Variables.wxi",
                "Config.wxi",
                "Files.wxs",
                "Components.wxs",
                "Registry.wxs",
                "Shortcuts.wxs",
                "Logging.wxi"
            };

            foreach (var file in requiredFiles)
            {
                var filePath = Path.Combine(wixDir, file);
                Assert.IsTrue(File.Exists(filePath), $"Required WiX file missing: {file}");
            }
        }

        [TestMethod]
        public void Test_WiX_ContainsRequiredComponents()
        {
            var solutionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (solutionDir != null && !File.Exists(Path.Combine(solutionDir, "WhisperKey.sln")))
            {
                solutionDir = Path.GetDirectoryName(solutionDir);
            }

            Assert.IsNotNull(solutionDir);

            var filesWxs = Path.Combine(solutionDir, ".wix", "Files.wxs");
            var content = File.ReadAllText(filesWxs);

            Assert.IsTrue(content.Contains("WhisperKey.exe"), "Should include main executable");
            Assert.IsTrue(content.Contains("NAudio"), "Should include NAudio libraries");
            Assert.IsTrue(content.Contains("Serilog"), "Should include Serilog");
        }

        [TestMethod]
        public void Test_Registry_Entries_Defined()
        {
            var solutionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (solutionDir != null && !File.Exists(Path.Combine(solutionDir, "WhisperKey.sln")))
            {
                solutionDir = Path.GetDirectoryName(solutionDir);
            }

            Assert.IsNotNull(solutionDir);

            var registryWxs = Path.Combine(solutionDir, ".wix", "Registry.wxs");
            var content = File.ReadAllText(registryWxs);

            Assert.IsTrue(content.Contains("Software\\WhisperKey"), "Should have WhisperKey registry entries");
            Assert.IsTrue(content.Contains("Uninstall"), "Should have uninstall registry entries");
        }

        [TestMethod]
        public void Test_Shortcuts_Defined()
        {
            var solutionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (solutionDir != null && !File.Exists(Path.Combine(solutionDir, "WhisperKey.sln")))
            {
                solutionDir = Path.GetDirectoryName(solutionDir);
            }

            Assert.IsNotNull(solutionDir);

            var shortcutsWxs = Path.Combine(solutionDir, ".wix", "Shortcuts.wxs");
            var content = File.ReadAllText(shortcutsWxs);

            Assert.IsTrue(content.Contains("StartMenu"), "Should have Start Menu shortcuts");
            Assert.IsTrue(content.Contains("Desktop"), "Should have Desktop shortcuts");
        }

        [TestMethod]
        public void Test_LaunchConditions_Defined()
        {
            var solutionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (solutionDir != null && !File.Exists(Path.Combine(solutionDir, "WhisperKey.sln")))
            {
                solutionDir = Path.GetDirectoryName(solutionDir);
            }

            Assert.IsNotNull(solutionDir);

            var setupWxs = Path.Combine(solutionDir, ".wix", "WhisperKey.Setup.wxs");
            var content = File.ReadAllText(setupWxs);

            Assert.IsTrue(content.Contains("VersionNT"), "Should have Windows version check");
            Assert.IsTrue(content.Contains("Privileged"), "Should have admin privilege check");
        }

        [TestMethod]
        public void Test_Logging_Configured()
        {
            var solutionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (solutionDir != null && !File.Exists(Path.Combine(solutionDir, "WhisperKey.sln")))
            {
                solutionDir = Path.GetDirectoryName(solutionDir);
            }

            Assert.IsNotNull(solutionDir);

            var loggingWxi = Path.Combine(solutionDir, ".wix", "Logging.wxi");
            Assert.IsTrue(File.Exists(loggingWxi), "Logging.wxi should exist");

            var content = File.ReadAllText(loggingWxi);
            Assert.IsTrue(content.Contains("MsiLogging"), "Should have MsiLogging property");
        }

        [TestMethod]
        public void Test_MajorUpgrade_Configured()
        {
            var solutionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (solutionDir != null && !File.Exists(Path.Combine(solutionDir, "WhisperKey.sln")))
            {
                solutionDir = Path.GetDirectoryName(solutionDir);
            }

            Assert.IsNotNull(solutionDir);

            var setupWxs = Path.Combine(solutionDir, ".wix", "WhisperKey.Setup.wxs");
            var content = File.ReadAllText(setupWxs);

            Assert.IsTrue(content.Contains("MajorUpgrade"), "Should have MajorUpgrade configured");
        }
    }
}
