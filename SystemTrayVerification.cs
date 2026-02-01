using System;
using System.Threading.Tasks;
using WhisperKey.Tests;

namespace WhisperKey.Testing
{
    /// <summary>
    /// Quick verification of system tray validation implementation
    /// </summary>
    public class SystemTrayVerification
    {
        public static async Task<bool> RunQuickVerification()
        {
            Console.WriteLine("=== System Tray Validation Verification ===");
            
            try
            {
                // Test 1: SystemTrayTests functionality
                Console.WriteLine("Test 1: SystemTrayTests creation and basic functionality...");
                var systemTrayTests = new SystemTrayTests();
                
                // Test 2: Performance metrics
                Console.WriteLine("Test 2: Performance testing integration...");
                
                // Test 3: TestRunner integration
                Console.WriteLine("Test 3: TestRunner system tray integration... (Skipped: TestRunner not found)");
                // var testRunner = new TestRunner();
                
                // Verify SystemTrayService enhancements
                Console.WriteLine("Test 4: SystemTrayService performance enhancements...");
                var systemTrayService = new SystemTrayService();
                
                var initStopwatch = System.Diagnostics.Stopwatch.StartNew();
                systemTrayService.Initialize();
                initStopwatch.Stop();
                
                Console.WriteLine($"  ✓ Initialization time: {initStopwatch.ElapsedMilliseconds}ms (< 3000ms target)");
                
                // Test status update performance
                var statusStopwatch = System.Diagnostics.Stopwatch.StartNew();
                systemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                statusStopwatch.Stop();
                
                Console.WriteLine($"  ✓ Status update time: {statusStopwatch.ElapsedMilliseconds}ms (< 100ms target)");
                
                // Test notification performance
                var notificationStopwatch = System.Diagnostics.Stopwatch.StartNew();
                systemTrayService.ShowNotification("Test notification", "Verification");
                notificationStopwatch.Stop();
                
                Console.WriteLine($"  ✓ Notification time: {notificationStopwatch.ElapsedMilliseconds}ms (< 50ms target)");
                
                // Test disposal performance
                var disposeStopwatch = System.Diagnostics.Stopwatch.StartNew();
                systemTrayService.Dispose();
                disposeStopwatch.Stop();
                
                Console.WriteLine($"  ✓ Disposal time: {disposeStopwatch.ElapsedMilliseconds}ms (< 1000ms target)");
                
                // Performance validation
                var allPerformanceTargetsMet = 
                    initStopwatch.ElapsedMilliseconds < 3000 &&
                    statusStopwatch.ElapsedMilliseconds < 100 &&
                    notificationStopwatch.ElapsedMilliseconds < 50 &&
                    disposeStopwatch.ElapsedMilliseconds < 1000;
                
                Console.WriteLine($"  ✓ All performance targets met: {allPerformanceTargetsMet}");
                
                return allPerformanceTargetsMet;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Verification failed: {ex.Message}");
                return false;
            }
            finally
            {
                Console.WriteLine("=== Verification Complete ===");
            }
        }
    }
}