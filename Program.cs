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

            // Regular application startup
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            
            var mainWindow = new MainWindow();
            app.MainWindow = mainWindow;
            
            mainWindow.Show();
            app.Run();
        }
    }
} 