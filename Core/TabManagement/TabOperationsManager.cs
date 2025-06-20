using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls;
using ExplorerPro.UI.MainWindow;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Centralized manager for all tab operations
    /// </summary>
    public class TabOperationsManager
    {
        private readonly ILogger<TabOperationsManager> _logger;
        private readonly IDetachedWindowManager _windowManager;

        public TabOperationsManager(
            ILogger<TabOperationsManager> logger,
            IDetachedWindowManager windowManager)
        {
            _logger = logger;
            _windowManager = windowManager;
        }

        /// <summary>
        /// Reorders a tab within the same tab control
        /// </summary>
        public bool ReorderTab(ChromeStyleTabControl tabControl, TabItemModel tab, int newIndex)
        {
            try
            {
                var currentIndex = GetTabIndex(tabControl, tab);
                if (currentIndex == -1 || currentIndex == newIndex)
                    return false;

                // Find the TabItem
                var tabItem = FindTabItem(tabControl, tab);
                if (tabItem == null)
                    return false;

                // Animate the reorder
                AnimateTabReorder(tabControl, currentIndex, newIndex);

                // Remove and re-insert at new position
                tabControl.Items.RemoveAt(currentIndex);
                tabControl.Items.Insert(newIndex, tabItem);
                tabControl.SelectedIndex = newIndex;

                _logger.LogInformation($"Reordered tab '{tab.Title}' from index {currentIndex} to {newIndex}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reorder tab");
                return false;
            }
        }

        /// <summary>
        /// Transfers a tab between different tab controls
        /// </summary>
        public bool TransferTab(
            ChromeStyleTabControl source, 
            ChromeStyleTabControl target, 
            TabItemModel tab, 
            int targetIndex = -1)
        {
            try
            {
                // Validate
                if (source == target)
                    return ReorderTab(source, tab, targetIndex);

                // Don't transfer last tab
                if (source.Items.Count <= 1)
                {
                    _logger.LogWarning("Cannot transfer last remaining tab");
                    return false;
                }

                // Find and remove from source
                var tabItem = FindTabItem(source, tab);
                if (tabItem == null)
                    return false;

                source.Items.Remove(tabItem);

                // Add to target
                if (targetIndex == -1 || targetIndex >= target.Items.Count)
                {
                    target.Items.Add(tabItem);
                    target.SelectedItem = tabItem;
                }
                else
                {
                    target.Items.Insert(targetIndex, tabItem);
                    target.SelectedIndex = targetIndex;
                }

                // Update tab's source window reference
                tab.SourceWindow = Window.GetWindow(target);

                _logger.LogInformation($"Transferred tab '{tab.Title}' between windows");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transfer tab");
                return false;
            }
        }

        /// <summary>
        /// Creates a duplicate of a tab
        /// </summary>
        public TabItemModel DuplicateTab(ChromeStyleTabControl tabControl, TabItemModel sourceTab)
        {
            try
            {
                // Clone the model
                var newTab = sourceTab.Clone();
                newTab.Id = Guid.NewGuid().ToString();
                newTab.Title = $"{sourceTab.Title} - Copy";
                newTab.CreatedAt = DateTime.Now;
                newTab.LastAccessed = DateTime.Now;

                // Create new TabItem
                var newTabItem = new TabItem
                {
                    Header = newTab.Title,
                    Content = CreateTabContent(newTab),
                    Tag = newTab
                };

                // Add to control
                tabControl.Items.Add(newTabItem);
                tabControl.SelectedItem = newTabItem;

                _logger.LogInformation($"Duplicated tab '{sourceTab.Title}'");
                return newTab;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to duplicate tab");
                return null;
            }
        }

        /// <summary>
        /// Closes a tab with proper cleanup
        /// </summary>
        public bool CloseTab(ChromeStyleTabControl tabControl, TabItemModel tab)
        {
            try
            {
                // Don't close last tab
                if (tabControl.Items.Count <= 1)
                    return false;

                var tabItem = FindTabItem(tabControl, tab);
                if (tabItem == null)
                    return false;

                // Dispose content if needed
                if (tabItem.Content is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                // Remove tab
                var index = tabControl.Items.IndexOf(tabItem);
                tabControl.Items.Remove(tabItem);

                // Select appropriate tab
                if (tabControl.Items.Count > 0)
                {
                    var newIndex = Math.Min(index, tabControl.Items.Count - 1);
                    tabControl.SelectedIndex = newIndex;
                }

                _logger.LogInformation($"Closed tab '{tab.Title}'");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close tab");
                return false;
            }
        }

        /// <summary>
        /// Calculates the insertion index for a drag operation
        /// </summary>
        public int CalculateDropIndex(ChromeStyleTabControl tabControl, Point dropPoint)
        {
            var localPoint = tabControl.PointFromScreen(dropPoint);
            
            for (int i = 0; i < tabControl.Items.Count; i++)
            {
                if (tabControl.Items[i] is TabItem item)
                {
                    var itemBounds = item.TransformToAncestor(tabControl)
                        .TransformBounds(new Rect(0, 0, item.ActualWidth, item.ActualHeight));
                    
                    if (localPoint.X < itemBounds.Left + itemBounds.Width / 2)
                    {
                        return i;
                    }
                }
            }

            return tabControl.Items.Count;
        }

        #region Helper Methods

        private TabItem FindTabItem(ChromeStyleTabControl tabControl, TabItemModel model)
        {
            return tabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(ti => ti.Tag == model);
        }

        private int GetTabIndex(ChromeStyleTabControl tabControl, TabItemModel model)
        {
            var tabItem = FindTabItem(tabControl, model);
            return tabItem != null ? tabControl.Items.IndexOf(tabItem) : -1;
        }

        private object CreateTabContent(TabItemModel model)
        {
            // This should be injected or use a factory pattern
            // For now, return existing content or create a simple placeholder
            return model.Content ?? new System.Windows.Controls.Grid();
        }

        private void AnimateTabReorder(ChromeStyleTabControl tabControl, int fromIndex, int toIndex)
        {
            // Simple animation for visual feedback
            var storyboard = new Storyboard();
            
            // This is a placeholder - implement smooth animation based on your UI
            _logger.LogDebug($"Animating tab move from {fromIndex} to {toIndex}");
        }

        #endregion
    }
} 