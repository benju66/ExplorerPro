using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls;
using ExplorerPro.UI.MainWindow;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Manages lifecycle of detached windows
    /// </summary>
    public class DetachedWindowManager : IDetachedWindowManager
    {
        private readonly ILogger<DetachedWindowManager> _logger;
        private readonly List<DetachedWindowInfo> _detachedWindows;
        private readonly List<Window> _allWindows;
        private readonly object _lockObject = new object();

        public DetachedWindowManager(ILogger<DetachedWindowManager> logger)
        {
            _logger = logger;
            _detachedWindows = new List<DetachedWindowInfo>();
            _allWindows = new List<Window>();
        }

        /// <summary>
        /// Detaches a tab to a new window
        /// </summary>
        public Window DetachTab(TabItemModel tab, Window sourceWindow)
        {
            try
            {
                lock (_lockObject)
                {
                    // Find source tab control
                    var sourceTabControl = FindTabControl(sourceWindow);
                    if (sourceTabControl == null || sourceTabControl.Items.Count <= 1)
                    {
                        _logger.LogWarning("Cannot detach: no tab control or last tab");
                        return null;
                    }

                    // Find the tab item
                    var tabItem = sourceTabControl.Items
                        .OfType<TabItem>()
                        .FirstOrDefault(ti => ti.Tag == tab);

                    if (tabItem == null)
                    {
                        _logger.LogWarning($"Tab '{tab.Title}' not found in source window");
                        return null;
                    }

                    // Remove from source
                    sourceTabControl.Items.Remove(tabItem);

                    // Create new window
                    var newWindow = CreateDetachedWindow(tab, sourceWindow);
                    
                    // Configure new window's tab control
                    var newTabControl = FindTabControl(newWindow);
                    if (newTabControl != null)
                    {
                        newTabControl.Items.Clear();
                        newTabControl.Items.Add(tabItem);
                        newTabControl.SelectedItem = tabItem;
                    }

                    // Track the detached window
                    var info = new DetachedWindowInfo
                    {
                        Window = newWindow,
                        TabControl = newTabControl,
                        OriginalTab = tab,
                        SourceWindow = sourceWindow,
                        DetachedAt = DateTime.Now,
                        InitialPosition = new Point(newWindow.Left, newWindow.Top)
                    };

                    _detachedWindows.Add(info);
                    RegisterWindow(newWindow);

                    // Wire up cleanup
                    newWindow.Closed += (s, e) => OnDetachedWindowClosed(info);

                    // Show the window
                    newWindow.Show();
                    newWindow.Activate();

                    _logger.LogInformation($"Successfully detached tab '{tab.Title}' to new window");
                    return newWindow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to detach tab '{tab?.Title}'");
                return null;
            }
        }

        /// <summary>
        /// Reattaches a tab to a target window
        /// </summary>
        public void ReattachTab(TabItemModel tab, Window targetWindow, int insertIndex = -1)
        {
            try
            {
                lock (_lockObject)
                {
                    // Find current window containing the tab
                    var sourceWindow = FindWindowContainingTab(tab);
                    if (sourceWindow == null)
                    {
                        _logger.LogWarning($"No window found containing tab '{tab.Title}'");
                        return;
                    }

                    // Get tab controls
                    var sourceTabControl = FindTabControl(sourceWindow);
                    var targetTabControl = FindTabControl(targetWindow);

                    if (sourceTabControl == null || targetTabControl == null)
                    {
                        _logger.LogError("Invalid tab controls for reattachment");
                        return;
                    }

                    // Don't move if it's the last tab in source
                    if (sourceTabControl.Items.Count <= 1 && sourceWindow != targetWindow)
                    {
                        _logger.LogWarning("Cannot reattach last tab from window");
                        return;
                    }

                    // Find the tab item
                    var tabItem = sourceTabControl.Items
                        .OfType<TabItem>()
                        .FirstOrDefault(ti => ti.Tag == tab);

                    if (tabItem == null)
                    {
                        _logger.LogError($"Tab item not found for '{tab.Title}'");
                        return;
                    }

                    // Remove from source
                    sourceTabControl.Items.Remove(tabItem);

                    // Add to target
                    if (insertIndex >= 0 && insertIndex < targetTabControl.Items.Count)
                    {
                        targetTabControl.Items.Insert(insertIndex, tabItem);
                    }
                    else
                    {
                        targetTabControl.Items.Add(tabItem);
                    }

                    targetTabControl.SelectedItem = tabItem;

                    // Update tab's window reference
                    tab.SourceWindow = targetWindow;

                    // Close source window if empty and it's a detached window
                    if (sourceTabControl.Items.Count == 0)
                    {
                        var detachedInfo = _detachedWindows.FirstOrDefault(d => d.Window == sourceWindow);
                        if (detachedInfo != null)
                        {
                            sourceWindow.Close();
                        }
                    }

                    _logger.LogInformation($"Reattached tab '{tab.Title}' to target window");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to reattach tab '{tab?.Title}'");
            }
        }

        /// <summary>
        /// Gets all detached windows
        /// </summary>
        public IReadOnlyList<DetachedWindowInfo> GetDetachedWindows()
        {
            lock (_lockObject)
            {
                return _detachedWindows.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Registers a window for management
        /// </summary>
        public void RegisterWindow(Window window)
        {
            lock (_lockObject)
            {
                if (!_allWindows.Contains(window))
                {
                    _allWindows.Add(window);
                    window.Closed += OnWindowClosed;
                    _logger.LogDebug($"Registered window: {window.Title}");
                }
            }
        }

        /// <summary>
        /// Unregisters a window
        /// </summary>
        public void UnregisterWindow(Window window)
        {
            lock (_lockObject)
            {
                _allWindows.Remove(window);
                _detachedWindows.RemoveAll(d => d.Window == window);
                window.Closed -= OnWindowClosed;
            }
            _logger.LogDebug($"Unregistered window: {window.Title}");
        }

        /// <summary>
        /// Finds window containing a tab
        /// </summary>
        public Window FindWindowContainingTab(TabItemModel tab)
        {
            lock (_lockObject)
            {
                // Check all registered windows
                foreach (var window in _allWindows)
                {
                    var tabControl = FindTabControl(window);
                    if (tabControl?.Items.OfType<TabItem>().Any(ti => ti.Tag == tab) == true)
                    {
                        return window;
                    }
                }
                
                return null;
            }
        }

        /// <summary>
        /// Gets all windows that can accept tab drops
        /// </summary>
        public IEnumerable<Window> GetDropTargetWindows()
        {
            lock (_lockObject)
            {
                return _allWindows.Where(w => w.IsVisible && w.WindowState != System.Windows.WindowState.Minimized).ToList();
            }
        }

        #region Private Methods

        private MainWindow CreateDetachedWindow(TabItemModel tab, Window sourceWindow)
        {
            var mainWindow = sourceWindow as MainWindow;
            var newWindow = new MainWindow
            {
                Title = $"ExplorerPro - {tab.Title}",
                Width = sourceWindow.Width * 0.8,
                Height = sourceWindow.Height * 0.8,
                Left = sourceWindow.Left + 50,
                Top = sourceWindow.Top + 50,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            return newWindow;
        }

        private ChromeStyleTabControl FindTabControl(Window window)
        {
            if (window is MainWindow mainWindow)
            {
                return mainWindow.MainTabs as ChromeStyleTabControl;
            }
            return null;
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            if (sender is Window window)
            {
                UnregisterWindow(window);
            }
        }

        private void OnDetachedWindowClosed(DetachedWindowInfo info)
        {
            lock (_lockObject)
            {
                _detachedWindows.Remove(info);
                _logger.LogInformation($"Detached window closed and unregistered");
            }
        }

        #endregion
    }
} 