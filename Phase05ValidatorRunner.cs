using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Moq;
using ScottWisper.Services;
using ScottWisper.Tests;

namespace ScottWisper.Testing
{
    class Phase05ValidatorRunner
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("ScottWisper Phase 05 End-to-End Validation Tool");
            Console.WriteLine("=================================================\n");

            // Setup mock services
            var settingsMock = new Mock<ISettingsService>();
            var audioDeviceMock = new Mock<IAudioDeviceService>();
            var textInjectionMock = new Mock<ITextInjection>();
            var feedbackMock = new Mock<IFeedbackService>();
            var hotkeyMock = new Mock<IHotkeyService>();
            var audioCaptureMock = new Mock<IAudioCaptureService>();
            var whisperMock = new Mock<IWhisperService>();

            settingsMock.Setup(x => x.Settings).Returns(new Configuration.AppSettings());
            textInjectionMock.Setup(x => x.InjectTextAsync(It.IsAny<string>(), It.IsAny<InjectionOptions>()))
                .Returns(Task.FromResult(true));

            var validator = new DictationFlowValidator(
                hotkeyMock.Object,
                audioCaptureMock.Object,
                whisperMock.Object,
                textInjectionMock.Object,
                feedbackMock.Object
            );

            Console.WriteLine("Executing End-to-End Dictation Flow Validation...");
            var results = await validator.RunAllScenariosAsync();

            bool allPassed = true;
            Console.WriteLine("\nTest Results:");
            Console.WriteLine("-------------------------------------------------");
            foreach (var res in results)
            {
                Console.WriteLine($"Scenario: {res.ScenarioName,-20} | Status: {(res.Success ? "PASS" : "FAIL")} | Latency: {res.Latency.TotalMilliseconds:F0}ms");
                if (!res.Success)
                {
                    allPassed = false;
                    Console.WriteLine($"  Error: {res.ErrorMessage}");
                }
                foreach (var step in res.StepLog)
                {
                    Console.WriteLine($"  - {step}");
                }
            }
            Console.WriteLine("-------------------------------------------------");

            Console.WriteLine("\n=================================================");
            Console.WriteLine($"OVERALL RESULT: {(allPassed ? "PASSED" : "FAILED")}");
            Console.WriteLine("=================================================\n");
            
            if (allPassed)
            {
                Console.WriteLine("\nPhase 05 Plan 01: Dictation Activation Flow is verified.");
            }
        }
    }
}
