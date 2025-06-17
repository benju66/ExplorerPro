using System;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using ExplorerPro.UI.MainWindow;
using ExplorerPro.Core;

namespace ExplorerPro.Services
{
    /// <summary>
    /// Service responsible for managing tabs in the main window
    /// </summary>
    public class TabManagementService
    {
        private readonly ILogger<TabManagementService> _logger;
        private readonly WindowStateManager _stateManager;
        private readonly IExceptionHandler _exceptionHandler;
        private readonly ILoggerFactory _loggerFactory;

        public TabManagementService(
            ILogger<TabManagementService> logger,
            WindowStateManager stateManager,
            IExceptionHandler exceptionHandler,
            ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _stateManager = stateManager;
            _exceptionHandler = exceptionHandler;
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Adds a new tab to the main window
        /// </summary>
        public MainWindowContainer AddNewMainWindowTab(MainWindow parentWindow, TabControl mainTabs)
        {
            if (!_stateManager.IsOperational)
            {
                _logger.LogWarning($"Cannot add tab in state {_stateManager.CurrentState}");
                return null;
            }

            try
            {
                // Create new tab item
                var newTabItem = new TabItem
                {
                    Header = "New Tab",
                    Content = null
                };

                // Add to tab control
                mainTabs.Items.Add(newTabItem);
                mainTabs.SelectedItem = newTabItem;

                // Create container
                var container = new MainWindowContainer(parentWindow);
                newTabItem.Content = container;

                // Initialize with default path
                string defaultPath = ValidatePath(null);
                container.InitializeWithFileTree(defaultPath);

                // Update tab title
                string folderName = System.IO.Path.GetFileName(defaultPath);
                newTabItem.Header = !string.IsNullOrEmpty(folderName) ? folderName : "Home";

                _logger.LogInformation($"Added new tab with path: {defaultPath}");
                return container;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new tab");
                _exceptionHandler.HandleException(ex, "Failed to add new tab");
                return null;
            }
        }

        /// <summary>
        /// Safely adds a new tab with error handling
        /// </summary>
        public void AddNewMainWindowTabSafely(MainWindow parentWindow, TabControl mainTabs)
        {
            if (_stateManager.CurrentState == Core.WindowState.Ready)
            {
                AddNewMainWindowTab(parentWindow, mainTabs);
            }
            else
            {
                _logger.LogDebug($"Deferring tab creation until window is ready. Current state: {_stateManager.CurrentState}");
                if (mainTabs != null)
                {
                    _logger.LogDebug("MainTabs available, creating tab despite state");
                    AddNewMainWindowTab(parentWindow, mainTabs);
                }
            }
        }

        /// <summary>
        /// Detaches a tab into a new window
        /// </summary>
        public void DetachTab(int index, MainWindow parentWindow, TabControl mainTabs)
        {
            try
            {
                // Get the tab item
                TabItem tabItem = mainTabs.Items[index] as TabItem;
                if (tabItem == null) return;

                // Get the container
                MainWindowContainer container = tabItem.Content as MainWindowContainer;
                if (container == null) return;

                string tabTitle = tabItem.Header?.ToString() ?? "Detached";

                // Create new window
                MainWindow newWindow = new MainWindow();
                
                // Remove tab from current window
                mainTabs.Items.RemoveAt(index);
                
                // Clear tabs in new window
                newWindow.MainTabs.Items.Clear();
                
                // Create new tab item for detached window
                TabItem newTabItem = new TabItem
                {
                    Header = tabTitle,
                    Content = container
                };
                
                // Add to new window
                newWindow.MainTabs.Items.Add(newTabItem);
                
                // Configure and show new window
                newWindow.Title = $"Detached - {tabTitle}";
                newWindow.Width = 1000;
                newWindow.Height = 700;
                newWindow.Left = parentWindow.Left + 50;
                newWindow.Top = parentWindow.Top + 50;
                
                // Show window
                newWindow.Show();
                newWindow.Activate();
                
                // Connect panel signals
                if (container.PinnedPanel != null)
                {
                    newWindow.ConnectPinnedPanelSignals(container.PinnedPanel);
                }

                _logger.LogInformation($"Detached tab '{tabTitle}' into new window");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detaching tab");
                _exceptionHandler.HandleException(ex, "Failed to detach tab");
            }
        }

        private string ValidatePath(string path)
        {
            // Default to user's home directory if no path provided
            if (string.IsNullOrEmpty(path))
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            // Ensure path exists
            if (!System.IO.Directory.Exists(path))
            {
                _logger.LogWarning($"Path does not exist: {path}, using home directory");
                path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return path;
        }
    }
} 