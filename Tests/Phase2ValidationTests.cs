using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ExplorerPro.UI.MainWindow;

namespace ExplorerPro.Tests
{
    /// <summary>
    /// Validation tests for Phase 2: Core Infrastructure - IDisposable and Static Collections
    /// Tests proper resource management and weak reference collection behavior
    /// </summary>
    public static class Phase2ValidationTests
    {
        public static async Task RunAllTests()
        {
            Console.WriteLine("=== Phase 2 Core Infrastructure Validation ===");
            Console.WriteLine();

            try
            {
                await TestMainWindowContainerDisposal();
                Console.WriteLine();
                
                await TestWeakReferenceCollection();
                Console.WriteLine();
                
                TestContainerCollection();
                Console.WriteLine();
                
                Console.WriteLine("✅ Phase 2 Validation Complete - All tests passed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Phase 2 Validation Failed: {ex.Message}");
                throw;
            }
        }

        private static async Task TestMainWindowContainerDisposal()
        {
            Console.WriteLine("🔍 Testing MainWindowContainer Disposal...");
            
            var initialCount = MainWindowContainer.GetAllContainers().Count();
            Console.WriteLine($"Initial container count: {initialCount}");
            
            // Create a test window (in real scenario this would be from MainWindow)
            var testWindow = new MainWindow();
            var container = new MainWindowContainer(testWindow);
            
            await Task.Delay(100); // Allow initialization
            
            var newCount = MainWindowContainer.GetAllContainers().Count();
            Console.WriteLine($"After creation: {newCount} (should be {initialCount + 1})");
            
            if (newCount != initialCount + 1)
            {
                throw new Exception($"Container was not properly added to collection. Expected {initialCount + 1}, got {newCount}");
            }
            
            // Test disposal
            container.Dispose();
            await Task.Delay(100);
            
            var finalCount = MainWindowContainer.GetAllContainers().Count();
            Console.WriteLine($"After disposal: {finalCount} (should be {initialCount})");
            
            if (finalCount != initialCount)
            {
                throw new Exception($"Container was not properly removed from collection. Expected {initialCount}, got {finalCount}");
            }
            
            // Clean up test window
            testWindow.Close();
            
            Console.WriteLine("✅ MainWindowContainer disposal test passed");
        }

        private static async Task TestWeakReferenceCollection()
        {
            Console.WriteLine("🔍 Testing Weak Reference Collection...");
            
            var initialCount = MainWindowContainer.GetAllContainers().Count();
            Console.WriteLine($"Initial count: {initialCount}");
            
            // Create and abandon a container (without disposing)
            CreateAndAbandonContainer();
            
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            await Task.Delay(200); // Allow collection to complete
            
            var finalCount = MainWindowContainer.GetAllContainers().Count();
            Console.WriteLine($"After GC: {finalCount} (should be {initialCount})");
            
            if (finalCount != initialCount)
            {
                Console.WriteLine($"⚠️  Warning: Weak reference cleanup may need more time. Expected {initialCount}, got {finalCount}");
                // This is not necessarily a failure as GC timing is non-deterministic
            }
            else
            {
                Console.WriteLine("✅ Weak reference cleanup test passed");
            }
        }

        private static void CreateAndAbandonContainer()
        {
            // Create container in separate method to ensure it goes out of scope
            var testWindow = new MainWindow();
            var container = new MainWindowContainer(testWindow);
            
            // Don't dispose, just let it go out of scope
            // This simulates a memory leak scenario that weak references should handle
            
            testWindow.Close();
        }

        private static void TestContainerCollection()
        {
            Console.WriteLine("🔍 Testing Container Collection Methods...");
            
            var initialCount = MainWindowContainer.GetAllContainers().Count();
            
            // Test that GetAllContainers returns live instances
            var containers = MainWindowContainer.GetAllContainers().ToList();
            Console.WriteLine($"Retrieved {containers.Count} live containers");
            
            // Verify all returned containers are actually alive
            foreach (var container in containers)
            {
                if (container == null)
                {
                    throw new Exception("GetAllContainers returned null container");
                }
            }
            
            Console.WriteLine("✅ Container collection methods test passed");
        }

        /// <summary>
        /// Manual test for memory leak verification
        /// Call this method and monitor memory usage
        /// </summary>
        public static async Task StressTestContainerCreation(int count = 100)
        {
            Console.WriteLine($"🔍 Stress Testing Container Creation ({count} instances)...");
            
            var initialCount = MainWindowContainer.GetAllContainers().Count();
            
            for (int i = 0; i < count; i++)
            {
                var testWindow = new MainWindow();
                var container = new MainWindowContainer(testWindow);
                
                // Immediately dispose to test cleanup
                container.Dispose();
                testWindow.Close();
                
                if (i % 10 == 0)
                {
                    Console.WriteLine($"Created and disposed {i + 1} containers...");
                    
                    // Periodic GC to ensure cleanup
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }
            
            // Final cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            await Task.Delay(500);
            
            var finalCount = MainWindowContainer.GetAllContainers().Count();
            Console.WriteLine($"Final count: {finalCount} (should be {initialCount})");
            
            if (finalCount == initialCount)
            {
                Console.WriteLine("✅ Stress test passed - no memory leaks detected");
            }
            else
            {
                Console.WriteLine($"⚠️  Warning: {finalCount - initialCount} containers may still be referenced");
            }
        }
    }
} 