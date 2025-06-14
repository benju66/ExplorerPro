using System;
using System.Threading.Tasks;
using System.Windows;
using ExplorerPro.UI.MainWindow;
using ExplorerPro.Tests;

namespace ExplorerPro
{
    class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            // Check for test mode
            if (args.Length > 0 && args[0] == "--test-phase5")
            {
                Console.WriteLine("Running Phase 5 validation tests...");
                await ExplorerPro.Tests.Phase5ValidationTests.RunAllTests();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            
            if (args.Length > 0 && args[0] == "--test-phase6")
            {
                Console.WriteLine("Running Phase 6 validation tests...");
                await ExplorerPro.Tests.Phase6ValidationTests.RunAllTests();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // Use the proper App.xaml infrastructure for clean shutdown
            // This ensures App.OnStartup and App.OnExit are called properly
            var app = new App();
            app.Run();
        }
    }
} 