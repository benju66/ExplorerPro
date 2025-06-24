using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
                if (tabControl == null || tab == null)
                {
                    _logger.LogWarning("TabControl or TabItemModel is null in ReorderTab");
                    return false;
                }

                var currentIndex = GetTabIndex(tabControl, tab);
                if (currentIndex == -1)
                {
                    _logger.LogWarning($"Tab '{tab.Title}' not found in tab control");
                    return false;
                }

                // Add bounds checking for indices
                if (newIndex < 0 || newIndex >= tabControl.Items.Count)
                {
                    // Clamp index to valid range
                    newIndex = Math.Max(0, Math.Min(newIndex, tabControl.Items.Count - 1));
                    _logger.LogDebug($"Clamped new index to {newIndex}");
                }

                // No need to move if already at correct position
                if (currentIndex == newIndex)
                {
                    _logger.LogDebug($"Tab '{tab.Title}' already at index {newIndex}");
                    return true;
                }

                // Find the TabItem
                var tabItem = FindTabItem(tabControl, tab);
                if (tabItem == null)
                {
                    _logger.LogWarning($"TabItem not found for model '{tab.Title}'");
                    return false;
                }

                // Ensure UI operations are performed on UI thread
                bool operationResult = false;
                tabControl.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Animate the reorder for visual feedback
                        AnimateTabReorder(tabControl, currentIndex, newIndex);

                        // Store the currently selected item to restore selection
                        var wasSelected = tabControl.SelectedItem == tabItem;
                        
                        // Use ObservableCollection.Move for proper binding support
                        if (tabControl.TabItems is ObservableCollection<TabItemModel> observableTabItems)
                        {
                            // Move in the underlying collection
                            observableTabItems.Move(currentIndex, newIndex);
                        }
                        else
                        {
                            // Fallback: Use Items collection directly
                            tabControl.Items.RemoveAt(currentIndex);
                            tabControl.Items.Insert(newIndex, tabItem);
                        }

                        // Update tab selection after reorder
                        if (wasSelected)
                        {
                            tabControl.SelectedIndex = newIndex;
                        }
                        else
                        {
                            // Adjust selection index if it was affected by the move
                            var selectedIndex = tabControl.SelectedIndex;
                            if (selectedIndex != -1)
                            {
                                if (currentIndex < newIndex)
                                {
                                    // Moving right - adjust selected index if it's in between
                                    if (selectedIndex > currentIndex && selectedIndex <= newIndex)
                                    {
                                        tabControl.SelectedIndex = selectedIndex - 1;
                                    }
                                }
                                else
                                {
                                    // Moving left - adjust selected index if it's in between
                                    if (selectedIndex >= newIndex && selectedIndex < currentIndex)
                                    {
                                        tabControl.SelectedIndex = selectedIndex + 1;
                                    }
                                }
                            }
                        }

                        // Update tab model's last accessed time
                        tab.UpdateLastAccessed();
                        
                        operationResult = true;
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Error during UI operations in ReorderTab");
                        operationResult = false;
                    }
                });

                if (operationResult)
                {
                    _logger.LogInformation($"Successfully reordered tab '{tab.Title}' from index {currentIndex} to {newIndex}");
                }

                return operationResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to reorder tab '{tab?.Title ?? "unknown"}'");
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
        /// Calculates the insertion index for a drag operation based on cursor position
        /// </summary>
        public int CalculateDropIndex(ChromeStyleTabControl tabControl, Point dropPoint)
        {
            try
            {
                if (tabControl == null)
                {
                    _logger.LogWarning("TabControl is null in CalculateDropIndex");
                    return 0;
                }

                if (tabControl.Items.Count == 0)
                {
                    return 0;
                }

                // Convert screen point to local coordinates relative to the tab control
                Point localPoint;
                try
                {
                    localPoint = tabControl.PointFromScreen(dropPoint);
                }
                catch (InvalidOperationException)
                {
                    // If coordinate conversion fails, fallback to end position
                    _logger.LogDebug("Failed to convert screen coordinates, defaulting to end position");
                    return tabControl.Items.Count;
                }

                // Get tab positions relative to control
                double accumulatedWidth = 0;
                const double TAB_SPACING = 2.0; // Account for spacing between tabs

                for (int i = 0; i < tabControl.Items.Count; i++)
                {
                    if (tabControl.Items[i] is TabItem tabItem)
                    {
                        // Get the actual width of the tab item
                        double tabWidth = tabItem.ActualWidth;
                        
                        // If width is not yet available (during initialization), use estimated width
                        if (tabWidth <= 0)
                        {
                            tabWidth = EstimateTabWidth(tabItem);
                        }

                        // Find insertion point based on cursor X position
                        // Check if cursor is in the left half of this tab
                        double tabCenterX = accumulatedWidth + (tabWidth / 2);
                        
                        if (localPoint.X <= tabCenterX)
                        {
                            // Insert before this tab
                            return i;
                        }

                        // Account for tab widths and spacing
                        accumulatedWidth += tabWidth + TAB_SPACING;
                    }
                }

                // If we get here, the cursor is past all tabs - insert at the end
                return tabControl.Items.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating drop index");
                // Return clamped index as fallback
                return Math.Max(0, Math.Min(tabControl?.Items.Count ?? 0, 0));
            }
        }

        /// <summary>
        /// Enhanced drop index calculation that accounts for visual feedback and edge cases
        /// </summary>
        public int CalculateDropIndexWithVisualFeedback(ChromeStyleTabControl tabControl, Point dropPoint, TabItemModel draggedTab = null)
        {
            try
            {
                if (tabControl == null)
                    return 0;

                var baseIndex = CalculateDropIndex(tabControl, dropPoint);
                
                // If we're dragging a tab within the same control, adjust for its removal
                if (draggedTab != null)
                {
                    var draggedIndex = GetTabIndex(tabControl, draggedTab);
                    if (draggedIndex != -1 && draggedIndex < baseIndex)
                    {
                        // The dragged tab will be removed, so adjust the insertion point
                        baseIndex = Math.Max(0, baseIndex - 1);
                    }
                }

                // Return clamped index to ensure it's always valid
                return Math.Max(0, Math.Min(baseIndex, tabControl.Items.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enhanced drop index calculation");
                return 0;
            }
        }

        /// <summary>
        /// Estimates tab width when ActualWidth is not available
        /// </summary>
        private double EstimateTabWidth(TabItem tabItem)
        {
            const double DEFAULT_TAB_WIDTH = 150.0;
            const double MIN_TAB_WIDTH = 100.0;
            const double MAX_TAB_WIDTH = 250.0;

            try
            {
                // Try to estimate based on header content
                if (tabItem.Header is string headerText && !string.IsNullOrEmpty(headerText))
                {
                    // Rough estimation: 8 pixels per character + padding
                    double estimatedWidth = (headerText.Length * 8) + 40;
                    return Math.Max(MIN_TAB_WIDTH, Math.Min(estimatedWidth, MAX_TAB_WIDTH));
                }

                return DEFAULT_TAB_WIDTH;
            }
            catch
            {
                return DEFAULT_TAB_WIDTH;
            }
        }

        #region Helper Methods

        /// <summary>
        /// Validates that a reorder operation is valid
        /// </summary>
        private bool ValidateReorderOperation(ChromeStyleTabControl tabControl, TabItemModel tab, int newIndex)
        {
            if (tabControl == null || tab == null)
            {
                _logger.LogWarning("Invalid parameters for reorder operation");
                return false;
            }

            if (tabControl.Items.Count == 0)
            {
                _logger.LogWarning("Cannot reorder in empty tab control");
                return false;
            }

            if (newIndex < 0 || newIndex >= tabControl.Items.Count)
            {
                _logger.LogDebug($"Index {newIndex} is out of bounds, will be clamped");
                // This is not necessarily invalid as we clamp the index
            }

            var currentIndex = GetTabIndex(tabControl, tab);
            if (currentIndex == -1)
            {
                _logger.LogWarning($"Tab '{tab.Title}' not found in tab control");
                return false;
            }

            return true;
        }

        private TabItem FindTabItem(ChromeStyleTabControl tabControl, TabItemModel model)
        {
            try
            {
                return tabControl?.Items
                    .OfType<TabItem>()
                    .FirstOrDefault(ti => ti.Tag == model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding tab item for model '{model?.Title ?? "unknown"}'");
                return null;
            }
        }

        private int GetTabIndex(ChromeStyleTabControl tabControl, TabItemModel model)
        {
            try
            {
                var tabItem = FindTabItem(tabControl, model);
                return tabItem != null ? tabControl.Items.IndexOf(tabItem) : -1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting tab index for model '{model?.Title ?? "unknown"}'");
                return -1;
            }
        }

        private object CreateTabContent(TabItemModel model)
        {
            try
            {
                // This should be injected or use a factory pattern
                // For now, return existing content or create a simple placeholder
                return model?.Content ?? new System.Windows.Controls.Grid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating tab content for model '{model?.Title ?? "unknown"}'");
                return new System.Windows.Controls.Grid();
            }
        }

        private void AnimateTabReorder(ChromeStyleTabControl tabControl, int fromIndex, int toIndex)
        {
            try
            {
                // Enhanced animation for visual feedback
                if (Math.Abs(fromIndex - toIndex) <= 1)
                {
                    // Small moves don't need animation
                    return;
                }

                var storyboard = new Storyboard();
                
                // Create a subtle animation to indicate the reorder
                // This could be enhanced with actual visual transforms
                var duration = TimeSpan.FromMilliseconds(200);
                
                // For now, just log the animation - could be enhanced with actual visual feedback
                _logger.LogDebug($"Animating tab move from {fromIndex} to {toIndex} over {duration.TotalMilliseconds}ms");
                
                // Future enhancement: Add actual transform animations here
                // Example: slide animation, fade effects, etc.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during tab reorder animation");
                // Animation failure shouldn't break the reorder operation
            }
        }

        /// <summary>
        /// Safely updates the UI on the correct thread
        /// </summary>
        private void SafeUIUpdate(ChromeStyleTabControl tabControl, Action uiAction)
        {
            try
            {
                if (tabControl.Dispatcher.CheckAccess())
                {
                    uiAction();
                }
                else
                {
                    tabControl.Dispatcher.Invoke(uiAction);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during safe UI update");
                throw; // Re-throw to let caller handle
            }
        }

        #endregion
    }
} 