using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class FileSystemAclTests
    {
        private FileSystemService _fileSystem = null!;
        private string _testFile = null!;
        private string _testDir = null!;

        [TestInitialize]
        public void Setup()
        {
            _fileSystem = new FileSystemService();
            _testFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
            _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            
            File.WriteAllText(_testFile, "test content");
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_testFile)) File.Delete(_testFile);
            if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
        }

        [TestMethod]
        public void SetStrictPermissions_File_AppliesCorrectAcls()
        {
            // Act
            _fileSystem.SetStrictPermissions(_testFile);

            // Assert
            var fileInfo = new FileInfo(_testFile);
            var security = fileInfo.GetAccessControl();
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));

            // Should have at least 2 rules (User + SYSTEM)
            Assert.IsTrue(rules.Count >= 2);

            bool foundUser = false;
            bool foundSystem = false;
            var currentUser = WindowsIdentity.GetCurrent().User;
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.IdentityReference == currentUser) foundUser = true;
                if (rule.IdentityReference == systemSid) foundSystem = true;
            }

            Assert.IsTrue(foundUser, "Current user not found in ACLs");
            Assert.IsTrue(foundSystem, "SYSTEM account not found in ACLs");
            Assert.IsTrue(security.AreAccessRulesProtected, "Inheritance should be disabled");
        }

        [TestMethod]
        public void SetStrictPermissions_Directory_AppliesCorrectAcls()
        {
            // Act
            _fileSystem.SetStrictPermissions(_testDir);

            // Assert
            var dirInfo = new DirectoryInfo(_testDir);
            var security = dirInfo.GetAccessControl();
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));

            Assert.IsTrue(rules.Count >= 2);

            bool foundUser = false;
            bool foundSystem = false;
            var currentUser = WindowsIdentity.GetCurrent().User;
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.IdentityReference == currentUser) foundUser = true;
                if (rule.IdentityReference == systemSid) foundSystem = true;
            }

            Assert.IsTrue(foundUser);
            Assert.IsTrue(foundSystem);
            Assert.IsTrue(security.AreAccessRulesProtected);
        }
    }
}
