// Examples/TreeViewCacheUsageExample.cs
// Example demonstrating how to use the improved FileTreePerformanceManager with automatic cleanup

using System;
using System.Windows;
using System.Windows.Controls;
using ExplorerPro.UI.FileTree.Managers;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree;

namespace ExplorerPro.Examples
{
    /// <summary>
    /// Example showing how to use the improved FileTreePerformanceManager with automatic cleanup
    /// </summary>
    public class TreeViewCacheUsageExample
    {
        private FileTreePerformanceManager _performanceManager;
        private TreeView _treeView;

        public void InitializeWithAutoCleanup()
        {
            // Create your TreeView
            _treeView = new TreeView();
            
            // Initialize the performance manager - it will automatically start cleanup timer
            _performanceManager = new FileTreePerformanceManager(_treeView);
            
            // Subscribe to cleanup events for monitoring
            _performanceManager.CleanupCompleted += OnCleanupCompleted;
            
            // Optional: Subscribe to other events
            _performanceManager.VisibleItemsCacheUpdated += OnVisibleItemsCacheUpdated;
        }

        public void UseCache()
        {
            // Use the cache normally - cleanup happens automatically in background
            var fileItem = new FileTreeItem { Path = @"C:\SomeFile.txt", Name = "SomeFile.txt" };
            
            // This call will use cache and add to it if not present
            var treeViewItem = _performanceManager.GetTreeViewItemCached(fileItem);
            
            if (treeViewItem != null)
            {
                // Use the TreeViewItem
                treeViewItem.IsSelected = true;
                treeViewItem.BringIntoView();
            }
        }

        public void MonitorPerformance()
        {
            // Get comprehensive performance statistics including cleanup info
            var stats = _performanceManager.GetPerformanceStats();
            
            Console.WriteLine($"Cache Hit Ratio: {stats.CacheHitRatio:P2}");
            Console.WriteLine($"Cached Items: {stats.CachedItemsCount}");
            Console.WriteLine($"Visible Items: {stats.VisibleItemsCount}");
            
            // Cleanup statistics
            var cleanupStats = stats.CleanupStats;
            Console.WriteLine($"Total Cleanups: {cleanupStats.TotalCleanupsPerformed}");
            Console.WriteLine($"Dead Entries Removed: {cleanupStats.TotalDeadEntriesRemoved}");
            Console.WriteLine($"Average Cleanup Time: {cleanupStats.AverageCleanupDuration.TotalMilliseconds:F1}ms");
            Console.WriteLine($"Last Cleanup: {cleanupStats.LastCleanupTime}");
            
            if (cleanupStats.CleanupErrors > 0)
            {
                Console.WriteLine($"Cleanup Errors: {cleanupStats.CleanupErrors}");
            }
        }

        public void ForceCleanupIfNeeded()
        {
            // You can manually trigger cleanup if needed (e.g., after major changes)
            _performanceManager.ForceCleanup();
        }

        public void HandleDirectoryChanges(string changedDirectory)
        {
            // Invalidate cache for specific directory when changes occur
            _performanceManager.InvalidateDirectory(changedDirectory);
        }

        private void OnCleanupCompleted(object sender, FileTreePerformanceManager.CleanupCompletedEventArgs e)
        {
            // Log cleanup results
            Console.WriteLine($"[CLEANUP] Removed {e.DeadEntriesRemoved} dead entries from {e.InitialCacheSize} " +
                            $"total entries in {e.CleanupDuration.TotalMilliseconds:F1}ms " +
                            $"(Total cleanups: {e.TotalCleanupsPerformed})");
            
            // You could show a notification or update UI if needed
            if (e.DeadEntriesRemoved > 10)
            {
                Console.WriteLine($"[INFO] Significant cleanup performed - memory usage should be reduced");
            }
        }

        private void OnVisibleItemsCacheUpdated(object sender, EventArgs e)
        {
            // React to visible items cache updates if needed
            var stats = _performanceManager.GetPerformanceStats();
            Console.WriteLine($"[CACHE] Visible items updated: {stats.VisibleItemsCount} items");
        }

        public void Dispose()
        {
            // Always dispose properly to stop timers and release resources
            if (_performanceManager != null)
            {
                // Unsubscribe from events
                _performanceManager.CleanupCompleted -= OnCleanupCompleted;
                _performanceManager.VisibleItemsCacheUpdated -= OnVisibleItemsCacheUpdated;
                
                // Dispose will automatically stop cleanup timer and release locks
                _performanceManager.Dispose();
                _performanceManager = null;
            }
        }

        // Example of advanced usage with custom monitoring
        public void SetupAdvancedMonitoring()
        {
            // Create a timer to periodically log performance stats
            var monitoringTimer = new System.Windows.Threading.DispatcherTimer();
            monitoringTimer.Interval = TimeSpan.FromMinutes(5); // Every 5 minutes
            monitoringTimer.Tick += (sender, e) =>
            {
                var stats = _performanceManager.GetPerformanceStats();
                var cleanupStats = stats.CleanupStats;
                
                // Log to your preferred logging system
                LogPerformanceMetrics(stats);
                
                // Check for potential issues
                if (stats.CacheHitRatio < 0.7)
                {
                    Console.WriteLine("[WARNING] Cache hit ratio is low, consider investigating");
                }
                
                if (cleanupStats.CleanupErrors > 0)
                {
                    Console.WriteLine($"[WARNING] {cleanupStats.CleanupErrors} cleanup errors occurred");
                }
                
                if (cleanupStats.AverageCleanupDuration.TotalMilliseconds > 100)
                {
                    Console.WriteLine("[INFO] Cleanup operations are taking longer than expected");
                }
            };
            
            monitoringTimer.Start();
        }

        private void LogPerformanceMetrics(FileTreePerformanceManager.PerformanceStats stats)
        {
            // Example of comprehensive logging - adapt to your logging system
            var logData = new
            {
                CacheHitRatio = stats.CacheHitRatio,
                CachedItemsCount = stats.CachedItemsCount,
                VisibleItemsCount = stats.VisibleItemsCount,
                TotalCleanups = stats.CleanupStats.TotalCleanupsPerformed,
                TotalDeadEntriesRemoved = stats.CleanupStats.TotalDeadEntriesRemoved,
                AverageCleanupTimeMs = stats.CleanupStats.AverageCleanupDuration.TotalMilliseconds,
                CleanupErrors = stats.CleanupStats.CleanupErrors
            };
            
            // Log to file, database, or monitoring service
            Console.WriteLine($"[METRICS] {System.Text.Json.JsonSerializer.Serialize(logData)}");
        }
    }
} 