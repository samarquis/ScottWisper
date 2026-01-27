using System;
using System.Threading.Tasks;
using ScottWisper.Tests;

namespace ScottWisper.Testing
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var result = await SystemTrayVerification.RunQuickVerification();
            Console.WriteLine($"\nOverall Result: {(result ? "SUCCESS" : "FAILED")}");
            Console.WriteLine($"Press any key to exit...");
            Console.ReadKey();
        }
    }
}