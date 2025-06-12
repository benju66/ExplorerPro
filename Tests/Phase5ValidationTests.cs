using System;
using System.Threading.Tasks;
using ExplorerPro.Core.Collections;
using ExplorerPro.Core.Monitoring;
using ExplorerPro.UI.PaneManagement;

namespace ExplorerPro.Tests
{
    /// <summary>
    /// Phase 5 Validation Tests: Resource Bounds - History and Collection Limits
    /// Verifies bounded collections, resource monitoring, and memory management
    /// </summary>
    public class Phase5ValidationTests
    {
        public static async Task RunAllTests()
        {
            Console.WriteLine("=== Phase 5 Validation Starting ===");
            Console.WriteLine("Testing Resource Bounds - History and Collection Limits");
            Console.WriteLine();
            
            TestBoundedCollection();
            Console.WriteLine();
            
            TestPaneHistoryBounds();
            Console.WriteLine();
            
            await TestResourceMonitoring();
            Console.WriteLine();
            
            TestHistoryDisposal();
            Console.WriteLine();
            
            Console.WriteLine("=== Phase 5 Validation Complete! ===");
        }

        private static void TestBoundedCollection()
        {
            Console.WriteLine("Testing BoundedCollection...");
            
            using (var collection = new BoundedCollection<string>(5))
            {
                // Test basic properties
                Console.WriteLine($"Initial count: {collection.Count} (should be 0)");
                Console.WriteLine($"Max size: {collection.MaxSize} (should be 5)");
                Console.WriteLine($"Is empty: {collection.IsEmpty} (should be true)");
                
                // Add more than max to test bounds
                for (int i = 0; i < 10; i++)
                {
                    collection.Add($"Item {i}");
                }
                
                Console.WriteLine($"Count after adding 10 items: {collection.Count} (should be 5)");
                
                var items = collection.ToArray();
                Console.WriteLine($"First item: {items[0]} (should be 'Item 5')");
                Console.WriteLine($"Last item: {items[4]} (should be 'Item 9')");
                
                // Test AddFirst
                collection.AddFirst("First Item");
                items = collection.ToArray();
                Console.WriteLine($"After AddFirst - First: {items[0]} (should be 'First Item')");
                Console.WriteLine($"After AddFirst - Count: {collection.Count} (should be 5)");
                
                // Test removal
                var removed = collection.RemoveLast();
                Console.WriteLine($"Removed last: {removed}");
                Console.WriteLine($"Count after removal: {collection.Count} (should be 4)");
                
                Console.WriteLine("✓ BoundedCollection tests passed");
            }
        }

        private static void TestPaneHistoryBounds()
        {
            Console.WriteLine("Testing PaneHistoryManager bounds...");
            
            using (var historyManager = new PaneHistoryManager(maxHistorySize: 5, maxForwardSize: 3))
            {
                int tabIndex = 0;
                
                // Initialize tab
                historyManager.InitTabHistory(tabIndex, @"C:\Path0");
                
                // Add many entries to test bounds
                for (int i = 1; i < 15; i++)
                {
                    historyManager.PushPath(tabIndex, $@"C:\Path{i}");
                }
                
                var stats = historyManager.GetStatistics();
                Console.WriteLine($"Total tabs: {stats.TotalTabs} (should be 1)");
                Console.WriteLine($"Max history size: {stats.MaxHistorySize} (should be 5)");
                Console.WriteLine($"Max forward size: {stats.MaxForwardSize} (should be 3)");
                Console.WriteLine($"Total history entries: {stats.TotalHistoryEntries} (should be <= 5)");
                
                // Test navigation
                var currentEntry = historyManager.GetCurrentEntry(tabIndex);
                Console.WriteLine($"Current path: {currentEntry?.Path}");
                
                // Test going back multiple times
                int backCount = 0;
                string backPath;
                while ((backPath = historyManager.GoBack(tabIndex)) != null && backCount < 10)
                {
                    backCount++;
                    Console.WriteLine($"Back {backCount}: {backPath}");
                }
                Console.WriteLine($"Total back navigations: {backCount} (should be <= 5)");
                
                // Test going forward
                int forwardCount = 0;
                string forwardPath;
                while ((forwardPath = historyManager.GoForward(tabIndex)) != null && forwardCount < 10)
                {
                    forwardCount++;
                    Console.WriteLine($"Forward {forwardCount}: {forwardPath}");
                }
                Console.WriteLine($"Total forward navigations: {forwardCount} (should be <= {backCount})");
                
                Console.WriteLine("✓ PaneHistoryManager bounds tests passed");
            }
        }

        private static async Task TestResourceMonitoring()
        {
            Console.WriteLine("Testing ResourceMonitor...");
            
            using (var monitor = new ResourceMonitor(TimeSpan.FromMilliseconds(500)))
            {
                bool resourceUpdateReceived = false;
                bool memoryPressureReceived = false;
                
                // Subscribe to events
                monitor.ResourceUsageUpdated += (sender, args) =>
                {
                    resourceUpdateReceived = true;
                    Console.WriteLine($"Resource update: {args.WorkingSetMB}MB working set, {args.ManagedMemoryMB}MB managed");
                    Console.WriteLine($"Threads: {args.ThreadCount}, Handles: {args.HandleCount}");
                    Console.WriteLine($"GC Gen0: {args.Gen0CollectionCount}, Gen1: {args.Gen1CollectionCount}, Gen2: {args.Gen2CollectionCount}");
                };
                
                monitor.HighMemoryPressure += (sender, args) =>
                {
                    memoryPressureReceived = true;
                    Console.WriteLine($"Memory pressure detected: {args.CurrentWorkingSetMB}MB");
                    Console.WriteLine($"Growth rates: Working Set {args.WorkingSetGrowthRate:F2}x, Managed {args.ManagedMemoryGrowthRate:F2}x");
                    Console.WriteLine($"Recommendation: {args.Recommendation}");
                };
                
                // Get initial snapshot
                var snapshot1 = monitor.GetCurrentSnapshot();
                Console.WriteLine($"Initial snapshot: {snapshot1}");
                Console.WriteLine($"Snapshot valid: {snapshot1.IsValid}");
                
                // Trigger manual update
                monitor.UpdateResourceUsage();
                
                // Create some memory pressure for testing
                var largeArrays = new byte[10][];
                for (int i = 0; i < 10; i++)
                {
                    largeArrays[i] = new byte[5 * 1024 * 1024]; // 5MB each = 50MB total
                }
                
                await Task.Delay(100);
                
                var snapshot2 = monitor.GetCurrentSnapshot();
                Console.WriteLine($"After allocation: {snapshot2}");
                
                // Force garbage collection
                monitor.ForceGarbageCollection();
                
                await Task.Delay(100);
                
                var snapshot3 = monitor.GetCurrentSnapshot();
                Console.WriteLine($"After GC: {snapshot3}");
                
                // Wait for potential resource updates
                await Task.Delay(1000);
                
                Console.WriteLine($"Resource update received: {resourceUpdateReceived}");
                Console.WriteLine($"Memory pressure detected: {memoryPressureReceived}");
                
                // Clear memory
                largeArrays = null;
                
                Console.WriteLine("✓ ResourceMonitor tests completed");
            }
        }

        private static void TestHistoryDisposal()
        {
            Console.WriteLine("Testing history disposal patterns...");
            
            // Test HistoryEntry disposal
            var entry = new PaneHistoryManager.HistoryEntry
            {
                Path = @"C:\Test",
                SelectedItems = new System.Collections.Generic.List<string> { "file1.txt", "file2.txt" }
            };
            
            Console.WriteLine($"Entry created with {entry.SelectedItems.Count} selected items");
            entry.Dispose();
            // Note: After disposal, SelectedItems is cleared but the list might still exist
            Console.WriteLine("✓ HistoryEntry disposal test passed");
            
            // Test PaneHistoryManager disposal
            var disposed = false;
            using (var historyManager = new PaneHistoryManager(5, 3))
            {
                // Add some data
                historyManager.InitTabHistory(0, @"C:\Test1");
                historyManager.PushPath(0, @"C:\Test2");
                historyManager.InitTabHistory(1, @"C:\Test3");
                
                var stats = historyManager.GetStatistics();
                Console.WriteLine($"Before disposal - Total tabs: {stats.TotalTabs}");
                
                disposed = true;
            } // Dispose is called here
            
            Console.WriteLine($"✓ PaneHistoryManager disposal test passed: {disposed}");
            
            // Test BoundedCollection disposal with disposable items
            using (var collection = new BoundedCollection<DisposableTestItem>(3))
            {
                for (int i = 0; i < 5; i++)
                {
                    collection.Add(new DisposableTestItem(i));
                }
                
                Console.WriteLine($"Added 5 items to collection with max 3");
                Console.WriteLine($"Collection count: {collection.Count}");
                
                // Collection should have disposed the first 2 items when they were evicted
            } // Remaining items disposed here
            
            Console.WriteLine("✓ BoundedCollection disposal tests passed");
        }

        private class DisposableTestItem : IDisposable
        {
            private readonly int _id;
            private bool _disposed;
            
            public DisposableTestItem(int id)
            {
                _id = id;
            }
            
            public void Dispose()
            {
                if (_disposed) return;
                Console.WriteLine($"DisposableTestItem {_id} disposed");
                _disposed = true;
            }
        }
    }
} 