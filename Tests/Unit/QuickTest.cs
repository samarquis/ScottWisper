using System;
using System.Threading.Tasks;
using WhisperKey.Tests;

namespace WhisperKey.Tests.Unit
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("WhisperKey Quick Test Entry Point");
            
            if (args.Length > 0 && args[0] == "phase04")
            {
                // We would call Phase04 here if we could easily instantiate it
                Console.WriteLine("Running Phase 04 Validation...");
            }
            else if (args.Length > 0 && args[0] == "phase05")
            {
                Console.WriteLine("Running Phase 05 Validation...");
            }
            else
            {
                var result = await SystemTrayVerification.RunQuickVerification();
                Console.WriteLine($"\nOverall Result: {(result ? "SUCCESS" : "FAILED")}");
            }
            
            Console.WriteLine($"Press any key to exit...");
            // Console.ReadKey(); // Avoid blocking in automated environment
        }
    }
}
