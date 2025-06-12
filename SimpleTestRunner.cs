using System;
using System.Threading.Tasks;
using ExplorerPro.Tests;

public class SimpleTestRunner
{
    [System.STAThread]
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== ExplorerPro Phase 5 Tests ===");
        Console.WriteLine();
        
        try
        {
            await Phase5ValidationTests.RunAllTests();
            Console.WriteLine();
            Console.WriteLine("All tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Details: {ex.StackTrace}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
} 