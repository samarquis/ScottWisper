using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class LazyInitializationTests
    {
        private LazyInitializationService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new LazyInitializationService(NullLogger<LazyInitializationService>.Instance);
        }

        [TestMethod]
        public async Task Test_DeferredTaskExecution()
        {
            bool taskExecuted = false;
            _service.RegisterDeferredTask("TestTask", async () => 
            {
                await Task.Delay(10);
                taskExecuted = true;
            }, DeferredPriority.High);

            _service.StartDeferredInitialization();

            // Wait for background thread
            await Task.Delay(100);

            Assert.IsTrue(taskExecuted);
            Assert.IsTrue(_service.IsInitialized("TestTask"));
        }

        [TestMethod]
        public async Task Test_ResourcePreloading()
        {
            bool preloaded = false;
            await _service.PreloadResourceAsync("WhisperModel", async () =>
            {
                await Task.Delay(10);
                preloaded = true;
            });

            Assert.IsTrue(preloaded);
            Assert.IsTrue(_service.IsInitialized("WhisperModel"));
        }
    }
}
