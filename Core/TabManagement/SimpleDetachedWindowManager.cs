using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ExplorerPro.Models;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Simple implementation of IDetachedWindowManager
    /// </summary>
    public class SimpleDetachedWindowManager : IDetachedWindowManager
    {
        private readonly ILogger<SimpleDetachedWindowManager> _logger;
        private readonly List<DetachedWindowInfo> _detachedWindows = new List<DetachedWindowInfo>();
        private readonly List<Window> _registeredWindows = new List<Window>();

        public SimpleDetachedWindowManager(ILogger<SimpleDetachedWindowManager> logger = null)
        {
            _logger = logger;
        }

        public Window DetachTab(TabItemModel tab, Window sourceWindow)
        {
            try
            {
                if (tab == null)
                {
                    _logger?.LogWarning("Cannot detach null tab");
                    return null;
                }

                if (sourceWindow == null)
                {
                    _logger?.LogWarning("Cannot detach from null source window");
                    return null;
                }

                // Find source tab control
                var sourceTabControl = FindTabControl(sourceWindow);
                if (sourceTabControl == null)
                {
                    _logger?.LogWarning("No tab control found in source window");
                    return null;
                }

                // Don't detach the last tab
                if (sourceTabControl.Items.Count <= 1)
                {
                    _logger?.LogWarning("Cannot detach the last remaining tab");
                    return null;
                }

                // Find the actual TabItem to transfer
                var tabItem = FindTabItem(sourceTabControl, tab);
                if (tabItem == null)
                {
                    _logger?.LogWarning($"Tab item not found for '{tab.Title}' in source window");
                    return null;
                }

                // 1. Create new MainWindow instance
                var newWindow = CreateNewMainWindow(tab, sourceWindow);
                if (newWindow == null)
                {
                    _logger?.LogError("Failed to create new window for detached tab");
                    return null;
                }

                // 2. Transfer tab content to new window
                var success = TransferTabContent(tabItem, tab, sourceTabControl, newWindow);
                if (!success)
                {
                    _logger?.LogError("Failed to transfer tab content to new window");
                    newWindow.Close();
                    return null;
                }

                // 3. Remove tab from source window
                RemoveTabFromSource(sourceTabControl, tabItem);

                // 4. Position new window at cursor location
                PositionNewWindow(newWindow);

                // 5. Show and activate new window
                ShowAndActivateWindow(newWindow);

                // Track the detached window
                TrackDetachedWindow(newWindow, sourceWindow, tab);

                _logger?.LogInformation($"Successfully detached tab '{tab.Title}' to new window");

                // 6. Return the new window reference
                return newWindow;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to detach tab '{tab?.Title ?? "unknown"}'");
                return null;
            }
        }

        public void ReattachTab(TabItemModel tab, Window targetWindow, int insertIndex = -1)
        {
            try
            {
                // Validate target window and tab
                if (tab == null)
                {
                    _logger?.LogWarning("Cannot reattach null tab");
                    return;
                }

                if (targetWindow == null)
                {
                    _logger?.LogWarning("Cannot reattach to null target window");
                    return;
                }

                var targetTabControl = FindTabControl(targetWindow);
                if (targetTabControl == null)
                {
                    _logger?.LogWarning("No tab control found in target window");
                    return;
                }

                // Find source window containing the tab
                var sourceWindow = FindWindowContainingTab(tab);
                if (sourceWindow == null)
                {
                    _logger?.LogWarning($"Source window not found for tab '{tab.Title}'");
                    return;
                }

                var sourceTabControl = FindTabControl(sourceWindow);
                if (sourceTabControl == null)
                {
                    _logger?.LogWarning("No tab control found in source window");
                    return;
                }

                // Find the actual TabItem
                var tabItem = FindTabItem(sourceTabControl, tab);
                if (tabItem == null)
                {
                    _logger?.LogWarning($"Tab item not found for '{tab.Title}' in source window");
                    return;
                }

                // Create new TabItem with transferred content
                var newTabItem = CreateTabItemWithTransferredContent(tabItem, tab);
                if (newTabItem == null)
                {
                    _logger?.LogError("Failed to create tab item with transferred content");
                    return;
                }

                // Insert at specified index or append
                if (insertIndex >= 0 && insertIndex < targetTabControl.Items.Count)
                {
                    targetTabControl.Items.Insert(insertIndex, newTabItem);
                }
                else
                {
                    targetTabControl.Items.Add(newTabItem);
                }

                // Select the reattached tab
                targetTabControl.SelectedItem = newTabItem;

                // Remove from source window if different
                if (sourceWindow != targetWindow)
                {
                    RemoveTabFromSource(sourceTabControl, tabItem);

                    // Close source window if it's empty and detached
                    if (sourceTabControl.Items.Count == 0)
                    {
                        var detachedInfo = _detachedWindows.FirstOrDefault(d => d.Window == sourceWindow);
                        if (detachedInfo != null)
                        {
                            _detachedWindows.Remove(detachedInfo);
                            sourceWindow.Close();
                        }
                    }
                }

                // Update tab's source window reference
                tab.SourceWindow = targetWindow;

                _logger?.LogInformation($"Successfully reattached tab '{tab.Title}' to target window");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to reattach tab '{tab?.Title ?? "unknown"}'");
            }
        }

        public IReadOnlyList<DetachedWindowInfo> GetDetachedWindows()
        {
            return _detachedWindows.AsReadOnly();
        }

        public void RegisterWindow(Window window)
        {
            if (window != null && !_registeredWindows.Contains(window))
            {
                _registeredWindows.Add(window);
                _logger?.LogDebug($"Window registered: {window.Title}");
            }
        }

        public void UnregisterWindow(Window window)
        {
            if (window != null && _registeredWindows.Contains(window))
            {
                _registeredWindows.Remove(window);
                _logger?.LogDebug($"Window unregistered: {window.Title}");
            }
        }

        public Window FindWindowContainingTab(TabItemModel tab)
        {
            if (tab == null) return null;

            // Search through all registered windows
            foreach (var window in _registeredWindows)
            {
                var tabControl = FindTabControl(window);
                if (tabControl != null)
                {
                    var tabItem = FindTabItem(tabControl, tab);
                    if (tabItem != null)
                    {
                        return window;
                    }
                }
            }

            return null;
        }

        public IEnumerable<Window> GetDropTargetWindows()
        {
            return _registeredWindows.AsEnumerable();
        }

        #region Private Helper Methods

        /// <summary>
        /// Finds the TabControl in the specified window
        /// </summary>
        private UI.Controls.ChromeStyleTabControl FindTabControl(Window window)
        {
            if (window is UI.MainWindow.MainWindow mainWindow)
            {
                return mainWindow.MainTabs as UI.Controls.ChromeStyleTabControl;
            }
            return null;
        }

        /// <summary>
        /// Finds the TabItem corresponding to the TabItemModel in the specified tab control
        /// </summary>
        private System.Windows.Controls.TabItem FindTabItem(UI.Controls.ChromeStyleTabControl tabControl, TabItemModel tabModel)
        {
            if (tabControl == null || tabModel == null) return null;

            return tabControl.Items
                .OfType<System.Windows.Controls.TabItem>()
                .FirstOrDefault(ti => ti.Tag == tabModel);
        }

        /// <summary>
        /// Creates a new MainWindow instance for the detached tab
        /// </summary>
        private UI.MainWindow.MainWindow CreateNewMainWindow(TabItemModel tab, Window sourceWindow)
        {
            try
            {
                var newWindow = new UI.MainWindow.MainWindow
                {
                    Title = $"ExplorerPro - {tab.Title}",
                    Width = sourceWindow.Width * 0.8,
                    Height = sourceWindow.Height * 0.8,
                    WindowStartupLocation = WindowStartupLocation.Manual
                };

                // Position offset from source window
                newWindow.Left = sourceWindow.Left + 50;
                newWindow.Top = sourceWindow.Top + 50;

                return newWindow;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create new MainWindow instance");
                return null;
            }
        }

        /// <summary>
        /// Transfers tab content from source to new window
        /// </summary>
        private bool TransferTabContent(System.Windows.Controls.TabItem sourceTabItem, TabItemModel tabModel, 
            UI.Controls.ChromeStyleTabControl sourceTabControl, UI.MainWindow.MainWindow newWindow)
        {
            try
            {
                // Get the new window's tab control
                var newTabControl = FindTabControl(newWindow);
                if (newTabControl == null)
                {
                    _logger?.LogError("New window does not have a valid tab control");
                    return false;
                }

                // Clear any default tabs in the new window
                newTabControl.Items.Clear();

                // Create a new TabItem for the new window with the same content
                var newTabItem = new System.Windows.Controls.TabItem
                {
                    Header = sourceTabItem.Header,
                    Content = sourceTabItem.Content,
                    Tag = tabModel,
                    ToolTip = sourceTabItem.ToolTip
                };

                // Add to the new window's tab control
                newTabControl.Items.Add(newTabItem);
                newTabControl.SelectedItem = newTabItem;

                // Update the tab model's source window reference
                tabModel.SourceWindow = newWindow;

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to transfer tab content");
                return false;
            }
        }

        /// <summary>
        /// Removes a tab from the source tab control
        /// </summary>
        private void RemoveTabFromSource(UI.Controls.ChromeStyleTabControl sourceTabControl, System.Windows.Controls.TabItem tabItem)
        {
            try
            {
                if (sourceTabControl != null && tabItem != null)
                {
                    sourceTabControl.Items.Remove(tabItem);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to remove tab from source");
            }
        }

        /// <summary>
        /// Positions the new window at the cursor location
        /// </summary>
        private void PositionNewWindow(UI.MainWindow.MainWindow newWindow)
        {
            try
            {
                // Get current cursor position
                var cursorPos = System.Windows.Forms.Cursor.Position;
                
                // Position window at cursor with some offset
                newWindow.Left = Math.Max(0, cursorPos.X - 100);
                newWindow.Top = Math.Max(0, cursorPos.Y - 50);

                // Ensure window is on screen
                var workingArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                if (newWindow.Left + newWindow.Width > workingArea.Right)
                {
                    newWindow.Left = workingArea.Right - newWindow.Width;
                }
                if (newWindow.Top + newWindow.Height > workingArea.Bottom)
                {
                    newWindow.Top = workingArea.Bottom - newWindow.Height;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to position new window");
                // Fallback to center screen
                newWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        /// <summary>
        /// Shows and activates the new window
        /// </summary>
        private void ShowAndActivateWindow(UI.MainWindow.MainWindow newWindow)
        {
            try
            {
                newWindow.Show();
                newWindow.Activate();
                newWindow.Focus();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show and activate new window");
            }
        }

        /// <summary>
        /// Tracks the detached window in the management system
        /// </summary>
        private void TrackDetachedWindow(UI.MainWindow.MainWindow newWindow, Window sourceWindow, TabItemModel tab)
        {
            try
            {
                var detachedInfo = new DetachedWindowInfo
                {
                    Window = newWindow,
                    TabControl = FindTabControl(newWindow),
                    OriginalTab = tab,
                    SourceWindow = sourceWindow,
                    DetachedAt = DateTime.Now,
                    InitialPosition = new Point(newWindow.Left, newWindow.Top)
                };

                _detachedWindows.Add(detachedInfo);
                RegisterWindow(newWindow);

                // Set up cleanup when window is closed
                newWindow.Closed += (s, e) =>
                {
                    _detachedWindows.Remove(detachedInfo);
                    UnregisterWindow(newWindow);
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to track detached window");
            }
        }

        /// <summary>
        /// Creates a new TabItem with transferred content for reattachment
        /// </summary>
        private System.Windows.Controls.TabItem CreateTabItemWithTransferredContent(System.Windows.Controls.TabItem sourceTabItem, TabItemModel tabModel)
        {
            try
            {
                return new System.Windows.Controls.TabItem
                {
                    Header = sourceTabItem.Header,
                    Content = sourceTabItem.Content,
                    Tag = tabModel,
                    ToolTip = sourceTabItem.ToolTip
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create tab item with transferred content");
                return null;
            }
        }

        #endregion
    }
} 