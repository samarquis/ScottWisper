using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScottWisper.Models;
using ScottWisper.Services;

namespace ScottWisper.Tests
{
    [TestClass]
    public class VocabularyTests
    {
        private IVocabularyService _service = null!;
        private string _testDataDirectory = null!;

        [TestInitialize]
        public void Setup()
        {
            // Create a temporary test directory
            _testDataDirectory = Path.Combine(Path.GetTempPath(), $"ScottWisperVocabTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDataDirectory);
            
            _service = new VocabularyService(
                NullLogger<VocabularyService>.Instance,
                _testDataDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test directory
            try
            {
                if (Directory.Exists(_testDataDirectory))
                {
                    Directory.Delete(_testDataDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Vocabulary Pack Tests

        [TestMethod]
        public async Task Test_GetAllPacks()
        {
            var packs = await _service.GetAllPacksAsync();
            
            Assert.IsNotNull(packs);
            Assert.IsTrue(packs.Count >= 2); // Should have Medical and Legal at minimum
            
            // Check for Medical pack
            Assert.IsTrue(packs.Any(p => p.Industry == VocabularyIndustry.Medical));
            
            // Check for Legal pack
            Assert.IsTrue(packs.Any(p => p.Industry == VocabularyIndustry.Legal));
        }

        [TestMethod]
        public async Task Test_GetMedicalPack()
        {
            var pack = await _service.GetMedicalPackAsync();
            
            Assert.IsNotNull(pack);
            Assert.AreEqual(VocabularyIndustry.Medical, pack.Industry);
            Assert.IsTrue(pack.Terms.Count > 0);
            Assert.IsTrue(pack.Terms.Any(t => t.Term == "anterior"));
            Assert.IsTrue(pack.Terms.Any(t => t.Term == "hypertension"));
        }

        [TestMethod]
        public async Task Test_GetLegalPack()
        {
            var pack = await _service.GetLegalPackAsync();
            
            Assert.IsNotNull(pack);
            Assert.AreEqual(VocabularyIndustry.Legal, pack.Industry);
            Assert.IsTrue(pack.Terms.Count > 0);
            Assert.IsTrue(pack.Terms.Any(t => t.Term == "plaintiff"));
            Assert.IsTrue(pack.Terms.Any(t => t.Term == "contract"));
        }

        [TestMethod]
        public async Task Test_EnableDisablePack()
        {
            var medicalPack = await _service.GetMedicalPackAsync();
            Assert.IsNotNull(medicalPack);
            
            // Initially disabled
            Assert.IsFalse(medicalPack.IsEnabled);
            
            // Enable
            var enabled = await _service.EnablePackAsync(medicalPack.Id);
            Assert.IsTrue(enabled);
            
            var enabledPacks = await _service.GetEnabledPacksAsync();
            Assert.IsTrue(enabledPacks.Any(p => p.Id == medicalPack.Id));
            
            // Disable
            var disabled = await _service.DisablePackAsync(medicalPack.Id);
            Assert.IsTrue(disabled);
            
            enabledPacks = await _service.GetEnabledPacksAsync();
            Assert.IsFalse(enabledPacks.Any(p => p.Id == medicalPack.Id));
        }

        [TestMethod]
        public async Task Test_GetPack_ById()
        {
            var medicalPack = await _service.GetMedicalPackAsync();
            Assert.IsNotNull(medicalPack);
            
            var retrieved = await _service.GetPackAsync(medicalPack.Id);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(medicalPack.Id, retrieved.Id);
            Assert.AreEqual(medicalPack.Name, retrieved.Name);
        }

        [TestMethod]
        public async Task Test_PackTerms_MedicalCategories()
        {
            var pack = await _service.GetMedicalPackAsync();
            Assert.IsNotNull(pack);
            
            // Check for various medical categories
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Anatomy"));
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Cardiology"));
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Neurology"));
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Oncology"));
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Radiology"));
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Laboratory"));
        }

        [TestMethod]
        public async Task Test_PackTerms_LegalCategories()
        {
            var pack = await _service.GetLegalPackAsync();
            Assert.IsNotNull(pack);
            
            // Check for various legal categories
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Litigation"));
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Contract"));
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Court"));
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Criminal"));
            Assert.IsTrue(pack.Terms.Any(t => t.Category == "Corporate"));
        }

        [TestMethod]
        public async Task Test_MedicalTerms_HighPriority()
        {
            var pack = await _service.GetMedicalPackAsync();
            Assert.IsNotNull(pack);
            
            // Check for high priority medical terms
            var highPriorityTerms = pack.Terms.Where(t => t.IsHighPriority).ToList();
            Assert.IsTrue(highPriorityTerms.Count > 0);
            
            // Verify specific high priority terms
            Assert.IsTrue(highPriorityTerms.Any(t => t.Term == "atrial fibrillation"));
            Assert.IsTrue(highPriorityTerms.Any(t => t.Term == "myocardial infarction"));
            Assert.IsTrue(highPriorityTerms.Any(t => t.Term == "hypertension"));
        }

        #endregion

        #region Custom Vocabulary Tests

        [TestMethod]
        public async Task Test_AddCustomTerm()
        {
            var term = await _service.AddCustomTermAsync("customterm123", "TestCategory", "Test definition");
            
            Assert.IsNotNull(term);
            Assert.AreEqual("customterm123", term.Term);
            Assert.AreEqual("TestCategory", term.Category);
            Assert.AreEqual("Test definition", term.Definition);
            
            var customTerms = await _service.GetCustomTermsAsync();
            Assert.IsTrue(customTerms.Any(t => t.Term == "customterm123"));
        }

        [TestMethod]
        public async Task Test_AddMultipleCustomTerms()
        {
            var terms = new[] { "term1", "term2", "term3" };
            
            foreach (var t in terms)
            {
                await _service.AddCustomTermAsync(t);
            }
            
            var customTerms = await _service.GetCustomTermsAsync();
            Assert.AreEqual(3, customTerms.Count);
        }

        [TestMethod]
        public async Task Test_RemoveCustomTerm()
        {
            var term = await _service.AddCustomTermAsync("toremove");
            
            var removed = await _service.RemoveCustomTermAsync(term.Id);
            Assert.IsTrue(removed);
            
            var customTerms = await _service.GetCustomTermsAsync();
            Assert.IsFalse(customTerms.Any(t => t.Term == "toremove"));
        }

        [TestMethod]
        public async Task Test_RemoveNonExistentTerm()
        {
            var removed = await _service.RemoveCustomTermAsync("nonexistent-id");
            Assert.IsFalse(removed);
        }

        [TestMethod]
        public async Task Test_AddProperNoun()
        {
            await _service.AddProperNounAsync("John Smith");
            await _service.AddProperNounAsync("Acme Corporation");
            
            // Verify by checking if terms are in context
            var context = await _service.GetTranscriptionContextAsync();
            StringAssert.Contains(context, "John Smith");
            StringAssert.Contains(context, "Acme Corporation");
        }

        [TestMethod]
        public async Task Test_RemoveProperNoun()
        {
            await _service.AddProperNounAsync("TestName");
            
            var removed = await _service.RemoveProperNounAsync("TestName");
            Assert.IsTrue(removed);
        }

        [TestMethod]
        public async Task Test_AddAcronym()
        {
            await _service.AddAcronymAsync("API", "Application Programming Interface");
            await _service.AddAcronymAsync("SDK", "Software Development Kit");
            
            // Verify by checking if acronyms are in context
            var context = await _service.GetTranscriptionContextAsync();
            StringAssert.Contains(context, "API");
        }

        #endregion

        #region Transcription Context Tests

        [TestMethod]
        public async Task Test_GetTranscriptionContext_NoEnabledPacks()
        {
            var context = await _service.GetTranscriptionContextAsync();
            
            // Should return empty or only custom terms
            Assert.IsNotNull(context);
        }

        [TestMethod]
        public async Task Test_GetTranscriptionContext_WithEnabledPack()
        {
            // Enable medical pack
            var medicalPack = await _service.GetMedicalPackAsync();
            await _service.EnablePackAsync(medicalPack!.Id);
            
            var context = await _service.GetTranscriptionContextAsync();
            
            // Should contain high priority medical terms
            Assert.IsNotNull(context);
            Assert.IsTrue(context.Contains("atrial fibrillation") || context.Contains("hypertension"));
        }

        [TestMethod]
        public async Task Test_GetTranscriptionContext_WithCustomTerms()
        {
            await _service.AddCustomTermAsync("my_custom_term");
            await _service.AddProperNounAsync("MyCompany");
            await _service.AddAcronymAsync("ABC", "Alphabet");
            
            var context = await _service.GetTranscriptionContextAsync();
            
            StringAssert.Contains(context, "my_custom_term");
            StringAssert.Contains(context, "MyCompany");
            StringAssert.Contains(context, "ABC");
        }

        #endregion

        #region Transcription Enhancement Tests

        [TestMethod]
        public async Task Test_EnhanceTranscription_EmptyText()
        {
            var result = await _service.EnhanceTranscriptionAsync("");
            
            Assert.IsNotNull(result);
            Assert.AreEqual("", result.OriginalText);
            Assert.AreEqual("", result.EnhancedText);
            Assert.IsFalse(result.HasCorrections);
        }

        [TestMethod]
        public async Task Test_EnhanceTranscription_NoEnabledPacks()
        {
            var text = "patient has hypertension and needs a CBC";
            var result = await _service.EnhanceTranscriptionAsync(text);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(text, result.OriginalText);
            // Without packs enabled, text should be unchanged
        }

        [TestMethod]
        public async Task Test_EnhanceTranscription_WithMedicalPack()
        {
            // Enable medical pack
            var medicalPack = await _service.GetMedicalPackAsync();
            await _service.EnablePackAsync(medicalPack!.Id);
            
            var text = "The patient has hypertension and requires CBC and BMP tests";
            var result = await _service.EnhanceTranscriptionAsync(text);
            
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContributingPackIds.Contains(medicalPack.Id));
        }

        #endregion

        #region Statistics Tests

        [TestMethod]
        public async Task Test_GetStatistics_Initial()
        {
            var stats = await _service.GetStatisticsAsync();
            
            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.EnabledPacksCount);
            Assert.AreEqual(0, stats.CustomTermsCount);
        }

        [TestMethod]
        public async Task Test_GetStatistics_AfterEnablingPacks()
        {
            var medicalPack = await _service.GetMedicalPackAsync();
            await _service.EnablePackAsync(medicalPack!.Id);
            
            var stats = await _service.GetStatisticsAsync();
            
            Assert.AreEqual(1, stats.EnabledPacksCount);
        }

        [TestMethod]
        public async Task Test_GetStatistics_AfterAddingTerms()
        {
            await _service.AddCustomTermAsync("term1");
            await _service.AddCustomTermAsync("term2");
            
            var stats = await _service.GetStatisticsAsync();
            
            Assert.AreEqual(2, stats.CustomTermsCount);
        }

        #endregion

        #region Import/Export Tests

        [TestMethod]
        public async Task Test_ExportVocabulary()
        {
            // Add some custom terms
            await _service.AddCustomTermAsync("exporttest");
            await _service.AddProperNounAsync("ExportCompany");
            await _service.AddAcronymAsync("EXP", "Export Test");
            
            var exportPath = Path.Combine(_testDataDirectory, "export.json");
            var result = await _service.ExportVocabularyAsync(exportPath);
            
            Assert.IsTrue(File.Exists(result));
            
            var content = await File.ReadAllTextAsync(result);
            StringAssert.Contains(content, "exporttest");
            StringAssert.Contains(content, "ExportCompany");
        }

        [TestMethod]
        public async Task Test_ImportVocabulary()
        {
            // Create a test import file
            var importData = new CustomVocabulary
            {
                CustomTerms = new System.Collections.Generic.List<VocabularyTerm>
                {
                    new VocabularyTerm { Term = "imported1" },
                    new VocabularyTerm { Term = "imported2" }
                },
                ProperNouns = new System.Collections.Generic.List<string> { "ImportedCorp" },
                Acronyms = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "IMP", "Imported" }
                }
            };
            
            var importPath = Path.Combine(_testDataDirectory, "import.json");
            var json = System.Text.Json.JsonSerializer.Serialize(importData);
            await File.WriteAllTextAsync(importPath, json);
            
            // Import
            var imported = await _service.ImportVocabularyAsync(importPath);
            Assert.IsTrue(imported);
            
            // Verify
            var customTerms = await _service.GetCustomTermsAsync();
            Assert.IsTrue(customTerms.Any(t => t.Term == "imported1"));
            Assert.IsTrue(customTerms.Any(t => t.Term == "imported2"));
        }

        [TestMethod]
        public async Task Test_ImportInvalidFile()
        {
            var invalidPath = Path.Combine(_testDataDirectory, "nonexistent.json");
            var imported = await _service.ImportVocabularyAsync(invalidPath);
            
            Assert.IsFalse(imported);
        }

        #endregion

        #region Reset Tests

        [TestMethod]
        public async Task Test_ResetToDefaults()
        {
            // Enable packs and add custom terms
            var medicalPack = await _service.GetMedicalPackAsync();
            await _service.EnablePackAsync(medicalPack!.Id);
            await _service.AddCustomTermAsync("resettest");
            
            // Reset
            await _service.ResetToDefaultsAsync();
            
            // Verify
            var enabledPacks = await _service.GetEnabledPacksAsync();
            Assert.AreEqual(0, enabledPacks.Count);
            
            var customTerms = await _service.GetCustomTermsAsync();
            Assert.AreEqual(0, customTerms.Count);
        }

        #endregion

        #region Term Properties Tests

        [TestMethod]
        public async Task Test_MedicalTerm_Definitions()
        {
            var pack = await _service.GetMedicalPackAsync();
            Assert.IsNotNull(pack);
            
            // Check that terms have definitions
            var hypertension = pack.Terms.FirstOrDefault(t => t.Term == "hypertension");
            Assert.IsNotNull(hypertension);
            Assert.IsFalse(string.IsNullOrEmpty(hypertension.Definition));
            Assert.AreEqual("High blood pressure", hypertension.Definition);
        }

        [TestMethod]
        public async Task Test_LegalTerm_Definitions()
        {
            var pack = await _service.GetLegalPackAsync();
            Assert.IsNotNull(pack);
            
            // Check that terms have definitions
            var plaintiff = pack.Terms.FirstOrDefault(t => t.Term == "plaintiff");
            Assert.IsNotNull(plaintiff);
            Assert.IsFalse(string.IsNullOrEmpty(plaintiff.Definition));
        }

        [TestMethod]
        public async Task Test_MedicalPhrases_Exist()
        {
            var pack = await _service.GetMedicalPackAsync();
            Assert.IsNotNull(pack);
            
            Assert.IsTrue(pack.CommonPhrases.Count > 0);
            Assert.IsTrue(pack.CommonPhrases.Contains("chief complaint"));
            Assert.IsTrue(pack.CommonPhrases.Contains("physical examination"));
            Assert.IsTrue(pack.CommonPhrases.Contains("vital signs"));
        }

        [TestMethod]
        public async Task Test_LegalPhrases_Exist()
        {
            var pack = await _service.GetLegalPackAsync();
            Assert.IsNotNull(pack);
            
            Assert.IsTrue(pack.CommonPhrases.Count > 0);
            Assert.IsTrue(pack.CommonPhrases.Contains("whereas"));
            Assert.IsTrue(pack.CommonPhrases.Contains("witnesseth"));
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task Test_EnableNonExistentPack()
        {
            var enabled = await _service.EnablePackAsync("nonexistent-id");
            Assert.IsFalse(enabled);
        }

        [TestMethod]
        public async Task Test_DisableNonExistentPack()
        {
            var disabled = await _service.DisablePackAsync("nonexistent-id");
            Assert.IsFalse(disabled);
        }

        [TestMethod]
        public async Task Test_GetPack_NonExistent()
        {
            var pack = await _service.GetPackAsync("nonexistent-id");
            Assert.IsNull(pack);
        }

        #endregion
    }
}
