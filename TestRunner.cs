using System;
using System.Threading.Tasks;
using ExplorerPro.Tests;

namespace ExplorerPro
{
    public class TestRunner
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("ExplorerPro Phase 5 Test Runner");
            Console.WriteLine("===============================");
            Console.WriteLine();
            
            try
            {
                await Phase5ValidationTests.RunAllTests();
                Console.WriteLine();
                Console.WriteLine("All tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test execution failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
} 