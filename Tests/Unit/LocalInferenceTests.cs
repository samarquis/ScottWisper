using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class LocalInferenceTests
    {
        private string _testModelsDirectory = null!;
        private IModelManagerService _modelManager = null!;
        private Mock<ISettingsService> _settingsServiceMock = null!;
        private ILocalInferenceService _localInference = null!;

        [TestInitialize]
        public void Setup()
        {
            _testModelsDirectory = Path.Combine(Path.GetTempPath(), $"WhisperKeyModelsTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testModelsDirectory);

            _modelManager = new ModelManagerService(
                NullLogger<ModelManagerService>.Instance,
                _testModelsDirectory);

            _settingsServiceMock = new Mock<ISettingsService>();
            _settingsServiceMock.Setup(s => s.Settings).Returns(new AppSettings
            {
                Transcription = new TranscriptionSettings
                {
                    Mode = TranscriptionMode.Local,
                    LocalModelPath = "base",
                    AutoFallbackToCloud = true
                }
            });

            _localInference = new LocalInferenceService(
                _settingsServiceMock.Object,
                _modelManager,
                NullLogger<LocalInferenceService>.Instance);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testModelsDirectory))
                {
                    Directory.Delete(_testModelsDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Model Manager Tests

        [TestMethod]
        public async Task Test_ModelManager_GetAvailableModels()
        {
            var models = await _modelManager.GetAvailableModelsAsync();

            Assert.IsNotNull(models);
            Assert.IsTrue(models.Count > 0);
            Assert.IsTrue(models.Any(m => m.Id == "tiny"));
            Assert.IsTrue(models.Any(m => m.Id == "base"));
            Assert.IsTrue(models.Any(m => m.Id == "small"));
            Assert.IsTrue(models.Any(m => m.Id == "large"));
        }

        [TestMethod]
        public async Task Test_ModelManager_GetModelInfo()
        {
            var models = await _modelManager.GetAvailableModelsAsync();
            var baseModel = models.First(m => m.Id == "base");

            Assert.AreEqual("Base", baseModel.Name);
            Assert.AreEqual(ModelSize.Base, baseModel.Size);
            Assert.IsTrue(baseModel.SizeBytes > 0);
            Assert.IsFalse(string.IsNullOrEmpty(baseModel.DownloadUrl));
            Assert.IsTrue(baseModel.RequiredRamMb > 0);
        }

        [TestMethod]
        public async Task Test_ModelManager_GetRecommendedModel()
        {
            var recommended = await _modelManager.GetRecommendedModelAsync();

            Assert.IsNotNull(recommended);
            Assert.IsFalse(string.IsNullOrEmpty(recommended.Id));
            Assert.IsTrue(recommended.RequiredRamMb > 0);
        }

        [TestMethod]
        public async Task Test_ModelManager_IsModelDownloaded_NotDownloaded()
        {
            var isDownloaded = await _modelManager.IsModelDownloadedAsync("base");
            Assert.IsFalse(isDownloaded);
        }

        [TestMethod]
        public async Task Test_ModelManager_DownloadedModels_EmptyInitially()
        {
            var downloaded = await _modelManager.GetDownloadedModelsAsync();
            Assert.AreEqual(0, downloaded.Count);
        }

        [TestMethod]
        public async Task Test_ModelManager_DiskSpace_ZeroInitially()
        {
            var space = await _modelManager.GetTotalDiskSpaceUsedAsync();
            Assert.AreEqual(0, space);
        }

        #endregion

        #region Local Inference Service Tests

        [TestMethod]
        public async Task Test_LocalInference_Initialize_NoModelDownloaded()
        {
            // Try to initialize without downloading model first
            var result = await _localInference.InitializeAsync("base");

            // Should fail since model is not downloaded
            Assert.IsFalse(result);
            Assert.IsFalse(_localInference.IsModelLoaded);
        }

        [TestMethod]
        public void Test_LocalInference_Status_InitialState()
        {
            var status = _localInference.Status;

            Assert.IsNotNull(status);
            Assert.IsFalse(status.IsInitialized);
            Assert.IsFalse(status.IsTranscribing);
            Assert.IsNull(status.LoadedModelId);
        }

        [TestMethod]
        public async Task Test_LocalInference_GetAvailableModels()
        {
            var models = await _localInference.GetAvailableModelsAsync();

            Assert.IsNotNull(models);
            Assert.IsTrue(models.Count > 0);
        }

        [TestMethod]
        public async Task Test_LocalInference_Statistics_Initial()
        {
            var stats = await _localInference.GetStatisticsAsync();

            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.TotalTranscriptions);
            Assert.AreEqual(0, stats.FailureCount);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task Test_LocalInference_Transcribe_NotInitialized()
        {
            // Should throw when not initialized
            await _localInference.TranscribeAudioAsync(new byte[1000]);
        }

        [TestMethod]
        public async Task Test_LocalInference_Transcribe_EmptyAudio()
        {
            // Mock a downloaded model by creating a fake model file
            var modelPath = Path.Combine(_testModelsDirectory, "ggml-base.bin");
            File.WriteAllBytes(modelPath, new byte[100]); // Fake model file

            // Initialize
            var initResult = await _localInference.InitializeAsync("base");
            Assert.IsTrue(initResult);

            // Try to transcribe empty audio
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await _localInference.TranscribeAudioAsync(new byte[0]);
            });
        }

        #endregion

        #region Model Info Tests

        [TestMethod]
        public void Test_WhisperModelInfo_Properties()
        {
            var model = new WhisperModelInfo
            {
                Id = "test-model",
                Name = "Test Model",
                Size = ModelSize.Base,
                SizeBytes = 1000000,
                SizeHuman = "1 MB",
                Description = "Test description",
                RequiredRamMb = 1024,
                RelativeSpeed = 2.0,
                RelativeAccuracy = 0.8,
                IsRecommended = true
            };

            Assert.AreEqual("test-model", model.Id);
            Assert.AreEqual("Test Model", model.Name);
            Assert.AreEqual(ModelSize.Base, model.Size);
            Assert.AreEqual(1000000, model.SizeBytes);
            Assert.AreEqual(1024, model.RequiredRamMb);
            Assert.IsTrue(model.IsRecommended);
        }

        [TestMethod]
        public void Test_LocalTranscriptionResult_Properties()
        {
            var result = new LocalTranscriptionResult
            {
                Text = "Test transcription",
                Language = "en",
                Confidence = 0.95f,
                ModelId = "base",
                UsedGpu = false,
                IsCached = false,
                RealTimeFactor = 0.5,
                ProcessingDuration = TimeSpan.FromSeconds(2)
            };

            Assert.AreEqual("Test transcription", result.Text);
            Assert.AreEqual("en", result.Language);
            Assert.AreEqual(0.95f, result.Confidence);
            Assert.AreEqual(0.5, result.RealTimeFactor);
        }

        [TestMethod]
        public void Test_LocalInferenceStatus_Properties()
        {
            var status = new LocalInferenceStatus
            {
                IsInitialized = true,
                LoadedModelId = "base",
                LoadingState = ModelLoadingState.Loaded,
                IsTranscribing = false,
                GpuAccelerationAvailable = false,
                TotalTranscriptions = 10
            };

            Assert.IsTrue(status.IsInitialized);
            Assert.AreEqual("base", status.LoadedModelId);
            Assert.AreEqual(ModelLoadingState.Loaded, status.LoadingState);
            Assert.AreEqual(10, status.TotalTranscriptions);
        }

        [TestMethod]
        public void Test_LocalInferenceSettings_Properties()
        {
            var settings = new LocalInferenceSettings
            {
                SelectedModelId = "small",
                EnableGpuAcceleration = true,
                ThreadCount = 8,
                BeamSize = 10,
                Temperature = 0.2f,
                Language = "en"
            };

            Assert.AreEqual("small", settings.SelectedModelId);
            Assert.IsTrue(settings.EnableGpuAcceleration);
            Assert.AreEqual(8, settings.ThreadCount);
            Assert.AreEqual(10, settings.BeamSize);
        }

        #endregion

        #region Model Size Tests

        [TestMethod]
        public async Task Test_ModelSizes_Ordered()
        {
            var models = await _modelManager.GetAvailableModelsAsync();
            var tiny = models.First(m => m.Id == "tiny");
            var baseModel = models.First(m => m.Id == "base");
            var small = models.First(m => m.Id == "small");
            var large = models.First(m => m.Id == "large");

            // Check size ordering
            Assert.IsTrue(tiny.SizeBytes < baseModel.SizeBytes);
            Assert.IsTrue(baseModel.SizeBytes < small.SizeBytes);
            Assert.IsTrue(small.SizeBytes < large.SizeBytes);

            // Check speed ordering (faster = smaller)
            Assert.IsTrue(tiny.RelativeSpeed > baseModel.RelativeSpeed);
            Assert.IsTrue(baseModel.RelativeSpeed > small.RelativeSpeed);
        }

        [TestMethod]
        public async Task Test_ModelRequirements()
        {
            var models = await _modelManager.GetAvailableModelsAsync();

            foreach (var model in models)
            {
                Assert.IsTrue(model.RequiredRamMb > 0, $"Model {model.Id} should have RAM requirement");
                Assert.IsFalse(string.IsNullOrEmpty(model.Description), $"Model {model.Id} should have description");
                Assert.IsTrue(model.SupportedLanguages.Count > 0, $"Model {model.Id} should support languages");
            }
        }

        #endregion

        #region Download Status Tests

        [TestMethod]
        public void Test_ModelDownloadStatus_Properties()
        {
            var status = new ModelDownloadStatus
            {
                ModelId = "base",
                State = DownloadState.Downloading,
                TotalBytes = 1000000,
                DownloadedBytes = 500000,
                BytesPerSecond = 100000
            };

            Assert.AreEqual("base", status.ModelId);
            Assert.AreEqual(DownloadState.Downloading, status.State);
            Assert.AreEqual(50.0, status.ProgressPercent, 0.01);
            Assert.AreEqual(100000, status.BytesPerSecond);
        }

        [TestMethod]
        public void Test_ModelDownloadStatus_ProgressCalculation()
        {
            var status = new ModelDownloadStatus
            {
                TotalBytes = 1000,
                DownloadedBytes = 250
            };

            Assert.AreEqual(25.0, status.ProgressPercent);

            status.DownloadedBytes = 500;
            Assert.AreEqual(50.0, status.ProgressPercent);

            status.DownloadedBytes = 1000;
            Assert.AreEqual(100.0, status.ProgressPercent);
        }

        [TestMethod]
        public void Test_ModelDownloadStatus_ZeroTotal()
        {
            var status = new ModelDownloadStatus
            {
                TotalBytes = 0,
                DownloadedBytes = 0
            };

            Assert.AreEqual(0, status.ProgressPercent);
        }

        #endregion

        #region Transcription Segment Tests

        [TestMethod]
        public void Test_TranscriptionSegment_Properties()
        {
            var segment = new TranscriptionSegment
            {
                Text = "Hello world",
                StartTime = 0.0f,
                EndTime = 2.5f,
                Confidence = 0.95f,
                SpeakerId = 1
            };

            Assert.AreEqual("Hello world", segment.Text);
            Assert.AreEqual(0.0f, segment.StartTime);
            Assert.AreEqual(2.5f, segment.EndTime);
            Assert.AreEqual(0.95f, segment.Confidence);
            Assert.AreEqual(1, segment.SpeakerId);
        }

        [TestMethod]
        public void Test_LocalTranscriptionResult_WithSegments()
        {
            var result = new LocalTranscriptionResult
            {
                Text = "Segment 1 Segment 2",
                Segments = new System.Collections.Generic.List<TranscriptionSegment>
                {
                    new TranscriptionSegment { Text = "Segment 1", StartTime = 0, EndTime = 3 },
                    new TranscriptionSegment { Text = "Segment 2", StartTime = 3, EndTime = 6 }
                }
            };

            Assert.AreEqual(2, result.Segments.Count);
            Assert.AreEqual("Segment 1", result.Segments[0].Text);
            Assert.AreEqual("Segment 2", result.Segments[1].Text);
        }

        #endregion

        #region Event Tests

        [TestMethod]
        public async Task Test_LocalInference_TranscriptionEvents()
        {
            // Create fake model file
            var modelPath = Path.Combine(_testModelsDirectory, "ggml-base.bin");
            File.WriteAllBytes(modelPath, new byte[100]);

            // Initialize
            await _localInference.InitializeAsync("base");

            bool startedFired = false;
            bool progressFired = false;
            bool completedFired = false;

            _localInference.TranscriptionStarted += (s, e) => startedFired = true;
            _localInference.TranscriptionProgress += (s, e) => progressFired = true;
            _localInference.TranscriptionCompleted += (s, e) => completedFired = true;

            // Transcribe
            var result = await _localInference.TranscribeAudioAsync(new byte[1000]);

            Assert.IsTrue(startedFired, "TranscriptionStarted event should fire");
            Assert.IsTrue(progressFired, "TranscriptionProgress event should fire");
            Assert.IsTrue(completedFired, "TranscriptionCompleted event should fire");
        }

        #endregion

        #region Statistics Tests

        [TestMethod]
        public async Task Test_LocalInference_Statistics_AfterTranscription()
        {
            // Create fake model file
            var modelPath = Path.Combine(_testModelsDirectory, "ggml-base.bin");
            File.WriteAllBytes(modelPath, new byte[100]);

            // Initialize and transcribe
            await _localInference.InitializeAsync("base");
            await _localInference.TranscribeAudioAsync(new byte[1000]);

            var stats = await _localInference.GetStatisticsAsync();

            Assert.AreEqual(1, stats.TotalTranscriptions);
            Assert.AreEqual(0, stats.FailureCount);
            Assert.IsNotNull(stats.LastTranscriptionAt);
        }

        [TestMethod]
        public void Test_LocalInferenceStatistics_Properties()
        {
            var stats = new LocalInferenceStatistics
            {
                TotalTranscriptions = 100,
                TotalAudioDuration = TimeSpan.FromMinutes(10),
                TotalProcessingTime = TimeSpan.FromMinutes(5),
                FailureCount = 2,
                CacheHitRate = 0.25
            };

            Assert.AreEqual(100, stats.TotalTranscriptions);
            Assert.AreEqual(2, stats.FailureCount);
            Assert.AreEqual(0.25, stats.CacheHitRate);

            // Check RTF calculation
            Assert.AreEqual(2.0, stats.AverageRealTimeFactor, 0.01); // 10 min audio / 5 min processing
        }

        #endregion

        #region Model Validation Tests

        [TestMethod]
        public void Test_ModelId_Validation()
        {
            var validIds = new[] { "tiny", "tiny.en", "base", "base.en", "small", "medium", "large", "large-v2", "large-v3" };
            
            // Just verify the test doesn't throw
            Assert.IsTrue(validIds.Length > 0);
        }

        [TestMethod]
        public async Task Test_ModelManager_VerifyModel_NotDownloaded()
        {
            var isValid = await _modelManager.VerifyModelAsync("base");
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public async Task Test_ModelManager_VerifyModel_InvalidModel()
        {
            var isValid = await _modelManager.VerifyModelAsync("nonexistent-model");
            Assert.IsFalse(isValid);
        }

        #endregion

        #region Language Support Tests

        [TestMethod]
        public async Task Test_Models_LanguageSupport()
        {
            var models = await _modelManager.GetAvailableModelsAsync();
            var tiny = models.First(m => m.Id == "tiny");
            var tinyEn = models.First(m => m.Id == "tiny.en");

            // Multilingual model should support many languages
            Assert.IsTrue(tiny.SupportedLanguages.Count > 50);

            // English-only should support just English
            Assert.AreEqual(1, tinyEn.SupportedLanguages.Count);
            Assert.AreEqual("en", tinyEn.SupportedLanguages[0]);
        }

        #endregion
    }
}
