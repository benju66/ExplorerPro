// UI/FileTree/Example_OptimizedTreeViewIntegration.cs
// This example shows how to integrate the OptimizedTreeViewIndexer into existing code

using System;
using System.Windows.Controls;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Managers;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Example integration of the optimized TreeView performance solution
    /// </summary>
    public class OptimizedFileTreeExample
    {
        private TreeView _treeView;
        private OptimizedFileTreePerformanceManager _performanceManager;

        public void InitializeOptimizedTreeView()
        {
            // 1. Create your TreeView as usual
            _treeView = new TreeView();
            
            // 2. Replace the old performance manager with the optimized version
            // OLD: _performanceManager = new FileTreePerformanceManager(_treeView);
            _performanceManager = new OptimizedFileTreePerformanceManager(_treeView);
            
            // 3. All existing code works unchanged, but now with O(1) performance!
            DemonstrateOptimizedOperations();
            
            // 4. Optional: Use advanced indexer features
            DemonstrateAdvancedFeatures();
        }

        private void DemonstrateOptimizedOperations()
        {
            // These operations are now O(1) instead of O(n)
            
            // Get a specific container (was O(n), now O(1))
            var dataItem = new FileTreeItem { Name = "example.txt", Path = @"C:\example.txt" };
            var container = _performanceManager.GetTreeViewItemCached(dataItem);
            
            // Get all visible items (was O(n), now O(1))
            var visibleItems = _performanceManager.GetAllVisibleTreeViewItems();
            Console.WriteLine($"Found {visibleItems.Count()} visible items instantly");
            
            // Get all realized items (was O(n), now O(1))
            var allItems = _performanceManager.GetAllTreeViewItemsFast();
            Console.WriteLine($"Found {allItems.Count()} realized items instantly");
            
            // Get expanded items (was O(n), now O(1))
            var expandedItems = _performanceManager.GetExpandedTreeViewItems();
            Console.WriteLine($"Found {expandedItems.Count()} expanded items instantly");
        }

        private void DemonstrateAdvancedFeatures()
        {
            var indexer = _performanceManager.GetIndexer();
            
            // Subscribe to real-time events
            indexer.ContainerCreated += (sender, e) =>
            {
                Console.WriteLine($"Container created for: {e.DataItem.Name}");
                // React to new items being realized
            };
            
            indexer.ContainerDestroyed += (sender, e) =>
            {
                Console.WriteLine($"Container destroyed for: {e.DataItem.Name}");
                // React to items being virtualized away
            };
            
            indexer.VisibilityChanged += (sender, e) =>
            {
                Console.WriteLine($"{e.BecameVisible.Count} items became visible");
                Console.WriteLine($"{e.BecameHidden.Count} items became hidden");
                // Perfect for lazy loading visible content
            };
            
            // Check performance statistics
            var stats = indexer.GetStats();
            Console.WriteLine($"Cache hit ratio: {stats.CacheHitRatio:P}");
            Console.WriteLine($"Realized containers: {stats.RealizedCount}");
            Console.WriteLine($"Visible containers: {stats.VisibleCount}");
        }

        public void DemonstrateBulkOperations()
        {
            // Optimize bulk operations by temporarily disabling indexing
            _performanceManager.DisableIndexing();
            
            try
            {
                // Add many items efficiently
                for (int i = 0; i < 10000; i++)
                {
                    var item = new FileTreeItem 
                    { 
                        Name = $"File{i}.txt", 
                        Path = $@"C:\Temp\File{i}.txt" 
                    };
                    _treeView.Items.Add(item);
                }
                
                Console.WriteLine("Added 10,000 items efficiently");
            }
            finally
            {
                // Re-enable indexing and rebuild
                _performanceManager.EnableIndexing();
                Console.WriteLine("Index rebuilt automatically");
            }
        }

        public void DemonstrateRealTimeQueries()
        {
            var indexer = _performanceManager.GetIndexer();
            
            // All of these are O(1) operations:
            
            // Find a specific container
            var dataItem = new FileTreeItem { Name = "test.txt" };
            var container = indexer.GetContainer(dataItem);
            
            // Check container state instantly
            if (container != null)
            {
                bool isVisible = indexer.IsVisible(container);
                bool isRealized = indexer.IsRealized(container);
                bool isExpanded = indexer.IsExpanded(container);
                
                Console.WriteLine($"Container - Visible: {isVisible}, Realized: {isRealized}, Expanded: {isExpanded}");
            }
            
            // Get collections instantly
            var visibleContainers = indexer.GetVisibleContainers();
            var realizedContainers = indexer.GetRealizedContainers();
            var expandedContainers = indexer.GetExpandedContainers();
            
            Console.WriteLine($"Counts - Visible: {visibleContainers.Count()}, " +
                            $"Realized: {realizedContainers.Count()}, " +
                            $"Expanded: {expandedContainers.Count()}");
        }

        public void DemonstrateBackwardCompatibility()
        {
            // All existing FileTreePerformanceManager code works unchanged:
            
            // These method calls are identical but now O(1)
            var stats = _performanceManager.GetPerformanceStats();
            _performanceManager.UpdateVisibleItemsCache(); // Now a no-op, handled automatically
            _performanceManager.ClearAllCaches();
            _performanceManager.ScheduleSelectionUpdate();
            
            // Hit testing still works the same way
            var point = new System.Windows.Point(100, 100);
            var itemAtPoint = _performanceManager.GetItemFromPoint(point);
            
            // Directory invalidation still works
            _performanceManager.InvalidateDirectory(@"C:\SomeDirectory");
            
            Console.WriteLine("All existing code works without changes!");
        }

        public void CleanupExample()
        {
            // Proper disposal prevents memory leaks
            _performanceManager?.Dispose();
        }

        #region Integration with Existing FileTreeListView

        /// <summary>
        /// Example of how to modify the existing ImprovedFileTreeListView to use optimization
        /// </summary>
        public void IntegrateWithExistingCode()
        {
            // In your ImprovedFileTreeListView.xaml.cs constructor, replace:
            
            // OLD:
            // _performanceManager = new FileTreePerformanceManager(TreeView, scrollViewer);
            
            // NEW:
            // _performanceManager = new OptimizedFileTreePerformanceManager(TreeView, scrollViewer);
            
            // That's it! All existing method calls will now be O(1) instead of O(n)
            
            // Optional: Add advanced features
            var indexer = _performanceManager.GetIndexer();
            
            // Use events for better responsiveness
            indexer.VisibilityChanged += (sender, e) =>
            {
                // Update UI only for newly visible items
                foreach (var item in e.BecameVisible.Take(10)) // Limit to prevent UI lag
                {
                    var dataItem = indexer.GetDataItem(item);
                    if (dataItem != null)
                    {
                        // Update item appearance, load content, etc.
                        UpdateItemAppearance(item, dataItem);
                    }
                }
            };
        }

        private void UpdateItemAppearance(TreeViewItem container, FileTreeItem dataItem)
        {
            // Example: Update visual state for newly visible items
            // This is much more efficient than updating all items
        }

        #endregion

        #region Performance Monitoring

        public void SetupPerformanceMonitoring()
        {
            var indexer = _performanceManager.GetIndexer();
            
            // Set up a timer to log performance metrics
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            
            timer.Tick += (sender, e) =>
            {
                var stats = indexer.GetStats();
                
                if (stats.TotalLookups > 0)
                {
                    Console.WriteLine($"[PERF] Lookups: {stats.TotalLookups}, " +
                                    $"Hit Ratio: {stats.CacheHitRatio:P}, " +
                                    $"Visible: {stats.VisibleCount}, " +
                                    $"Realized: {stats.RealizedCount}");
                }
            };
            
            timer.Start();
        }

        #endregion
    }
}

// Summary of Benefits:
// 1. O(1) lookups instead of O(n) tree traversal
// 2. Real-time tracking of container lifecycle
// 3. Automatic handling of WPF virtualization
// 4. Thread-safe concurrent access
// 5. Automatic cleanup of destroyed items
// 6. Backward compatibility with existing code
// 7. Advanced event-driven capabilities
// 8. Dramatic performance improvement for large trees (50,000+ items) 