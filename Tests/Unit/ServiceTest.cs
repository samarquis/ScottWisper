using System;
using System.Threading.Tasks;

namespace WhisperKey.Tests.Unit
{
    // Simple test class to verify service functionality
    public class ServiceTest
    {
        public static Task<bool> RunBasicTests()
        {
            Console.WriteLine("Running basic service tests...");
            
            // Test 1: AudioCaptureService instantiation and device enumeration
            try
            {
                using var audioService = new AudioCaptureService();
                var devices = AudioCaptureService.GetAvailableDevices();
                Console.WriteLine($"✓ AudioCaptureService instantiated successfully");
                Console.WriteLine($"✓ Found {devices.Length} audio devices");
                
                if (devices.Length == 0)
                {
                    Console.WriteLine("⚠ Warning: No audio input devices found");
                }
                else
                {
                    foreach (var device in devices)
                    {
                        Console.WriteLine($"  - {device}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ AudioCaptureService test failed: {ex.Message}");
                return Task.FromResult(false);
            }
            
            // Test 2: WhisperService instantiation (will fail if no API key)
            try
            {
                using var whisperService = new WhisperService();
                Console.WriteLine("✓ WhisperService instantiated successfully");
                
                var usage = whisperService.GetUsageStats();
                Console.WriteLine($"✓ Usage tracking initialized: {usage}");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("OPENAI_API_KEY"))
            {
                Console.WriteLine("⚠ WhisperService API key not set (expected for development)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ WhisperService test failed: {ex.Message}");
                return Task.FromResult(false);
            }
            
            Console.WriteLine("✓ All basic tests passed!");
            return Task.FromResult(true);
        }
    }
}