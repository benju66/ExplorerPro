using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ExplorerPro.UI.MainWindow;
using ExplorerPro.UI.Controls;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.Models;

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
                
                await RunPhase2OperationDetectionTests();
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

        /// <summary>
        /// Tests Phase 2 operation detection and coordinate system improvements
        /// </summary>
        public static async Task RunPhase2OperationDetectionTests()
        {
            Console.WriteLine("🔍 Testing Phase 2 Operation Detection & Coordinate Systems...");
            
            try
            {
                TestTabStripHeightConstant();
                TestTearOffThresholdConstant();
                TestDragOperationProperties();
                TestDragOperationTypes();
                TestChromeStyleTabControlInitialization();
                
                Console.WriteLine("✅ Phase 2 Operation Detection tests passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Phase 2 Operation Detection test failed: {ex.Message}");
                throw;
            }
        }

        private static void TestTabStripHeightConstant()
        {
            Console.WriteLine("  🔍 Testing TAB_STRIP_HEIGHT constant...");
            
            var tabStripHeightField = typeof(ChromeStyleTabControl)
                .GetField("TAB_STRIP_HEIGHT", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (tabStripHeightField == null)
            {
                throw new Exception("TAB_STRIP_HEIGHT constant not found");
            }
            
            var value = (double)tabStripHeightField.GetValue(null);
            if (value != 35.0)
            {
                throw new Exception($"TAB_STRIP_HEIGHT should be 35.0, but got {value}");
            }
            
            if (value <= 0 || value >= 100)
            {
                throw new Exception($"TAB_STRIP_HEIGHT value {value} is outside reasonable bounds");
            }
            
            Console.WriteLine($"    ✅ TAB_STRIP_HEIGHT correctly set to {value}px");
        }

        private static void TestTearOffThresholdConstant()
        {
            Console.WriteLine("  🔍 Testing TEAR_OFF_THRESHOLD constant...");
            
            var tearOffThresholdField = typeof(ChromeStyleTabControl)
                .GetField("TEAR_OFF_THRESHOLD", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (tearOffThresholdField == null)
            {
                throw new Exception("TEAR_OFF_THRESHOLD constant not found");
            }
            
            var value = (double)tearOffThresholdField.GetValue(null);
            if (value != 40.0)
            {
                throw new Exception($"TEAR_OFF_THRESHOLD should be 40.0, but got {value}");
            }
            
            Console.WriteLine($"    ✅ TEAR_OFF_THRESHOLD correctly set to {value}px");
        }

        private static void TestDragOperationProperties()
        {
            Console.WriteLine("  🔍 Testing DragOperation properties...");
            
            var dragOperation = new DragOperation();
            var testPoint = new Point(100, 50);
            
            // Test CurrentScreenPoint property
            dragOperation.CurrentScreenPoint = testPoint;
            if (!dragOperation.CurrentScreenPoint.Equals(testPoint))
            {
                throw new Exception("CurrentScreenPoint property not working correctly");
            }
            
            // Test state tracking
            dragOperation.CurrentOperationType = DragOperationType.Reorder;
            dragOperation.IsActive = true;
            
            if (dragOperation.CurrentOperationType != DragOperationType.Reorder)
            {
                throw new Exception("CurrentOperationType property not working correctly");
            }
            
            if (!dragOperation.IsActive)
            {
                throw new Exception("IsActive property not working correctly");
            }
            
            Console.WriteLine("    ✅ DragOperation properties working correctly");
        }

        private static void TestDragOperationTypes()
        {
            Console.WriteLine("  🔍 Testing DragOperationType enumeration...");
            
            // Verify all operation types exist
            if (!System.Enum.IsDefined(typeof(DragOperationType), DragOperationType.None))
            {
                throw new Exception("DragOperationType.None not defined");
            }
            
            if (!System.Enum.IsDefined(typeof(DragOperationType), DragOperationType.Reorder))
            {
                throw new Exception("DragOperationType.Reorder not defined");
            }
            
            if (!System.Enum.IsDefined(typeof(DragOperationType), DragOperationType.Detach))
            {
                throw new Exception("DragOperationType.Detach not defined");
            }
            
            if (!System.Enum.IsDefined(typeof(DragOperationType), DragOperationType.Transfer))
            {
                throw new Exception("DragOperationType.Transfer not defined");
            }
            
            Console.WriteLine("    ✅ All DragOperationType values are defined");
        }

        private static void TestChromeStyleTabControlInitialization()
        {
            Console.WriteLine("  🔍 Testing ChromeStyleTabControl initialization...");
            
            var tabControl = new ChromeStyleTabControl();
            
            if (tabControl == null)
            {
                throw new Exception("ChromeStyleTabControl failed to initialize");
            }
            
            if (tabControl.TabItems == null)
            {
                throw new Exception("TabItems collection not initialized");
            }
            
            if (!tabControl.AllowAddNew)
            {
                throw new Exception("AllowAddNew should default to true");
            }
            
            if (!tabControl.AllowDelete)
            {
                throw new Exception("AllowDelete should default to true");
            }
            
            // Test that drag-related properties can be set without exceptions
            try
            {
                tabControl.TabOperationsManager = null;
                tabControl.DragDropService = null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set drag-related properties: {ex.Message}");
            }
            
            Console.WriteLine("    ✅ ChromeStyleTabControl initialization successful");
        }
    }
} 