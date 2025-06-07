using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using ExplorerPro.UI.FileTree;
using ExplorerPro.UI.MainWindow;

namespace ExplorerPro.UI.TabManagement
{
    /// <summary>
    /// Interaction logic for TabManager.xaml
    /// Manages tabs containing ImprovedFileTreeListView instances
    /// </summary>
    public partial class TabManager : UserControl, IDisposable
    {
        #region Fields

        // Event raised when the active tab changes to notify parent container
        public event EventHandler<string>? CurrentPathChanged;
        
        // Event raised when a pin request is made from a tab
        public event EventHandler<string>? PinItemRequested;
        
        // Event raised when this tab manager becomes active
        public event EventHandler<string>? ActiveManagerChanged;
        
        // Track original manager when a tab is detached
        private TabManager? _originalTabManager;
        
        // Navigation history 
        private TabHistoryManager _historyManager = null!;
        
        // Track detached windows
        private List<Window> _detachedWindows = new List<Window>();
        
        // Track splitter
        private bool _splitViewActive = false;

        // Drag and drop fields
        private TabItem? _draggedItem;
        private Point _dragStartPoint;
        private bool _isDragging;

        // Memory monitoring
        private Dictionary<TabItem, long> _tabMemoryUsage = new Dictionary<TabItem, long>();
        private System.Windows.Threading.DispatcherTimer _memoryMonitorTimer;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the original tab manager this was detached from
        /// </summary>
        public TabManager? OriginalTabManager
        {
            get { return _originalTabManager; }
            set { _originalTabManager = value; UpdateReattachVisibility(); }
        }

        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// Initializes a new instance of the TabManager
        /// </summary>
        public TabManager()
        {
            try
            {
                InitializeComponent();
                
                // Initialize tab history manager
                _historyManager = new TabHistoryManager();
                
                // Setup event handling
                TabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
                
                // Setup drag-drop
                TabControl.AllowDrop = true;
                TabControl.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
                TabControl.PreviewMouseMove += TabControl_PreviewMouseMove;
                TabControl.DragEnter += TabControl_DragEnter;
                TabControl.Drop += TabControl_Drop;
                
                // Initialize memory monitoring
                InitializeMemoryMonitoring();
                
                Console.WriteLine("TabManager initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing TabManager: {ex.Message}");
                
                // Create history manager anyway to avoid null reference
                if (_historyManager == null)
                {
                    _historyManager = new TabHistoryManager();
                }
            }
        }

        /// <summary>
        /// Initialize memory monitoring timer
        /// </summary>
        private void InitializeMemoryMonitoring()
        {
            _memoryMonitorTimer = new System.Windows.Threading.DispatcherTimer();
            _memoryMonitorTimer.Interval = TimeSpan.FromSeconds(30); // Monitor every 30 seconds
            _memoryMonitorTimer.Tick += (s, e) => MonitorTabMemory();
            _memoryMonitorTimer.Start();
        }

        #endregion

        #region Tab Management

        /// <summary>
        /// Adds a new tab with a ImprovedFileTreeListView
        /// </summary>
        /// <param name="title">Title for the tab</param>
        /// <param name="rootPath">Root path for the file tree</param>
        /// <returns>The created tab content</returns>
        public TabItem? AddNewFileTreeTab(string title, string rootPath)
        {
            try
            {
                // Log who's calling this method
                var stackTrace = new System.Diagnostics.StackTrace();
                var callingMethod = stackTrace.GetFrame(1)?.GetMethod()?.Name ?? "Unknown";
                System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] AddNewFileTreeTab called by: {callingMethod}");
                System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Creating tab '{title}' for path: {rootPath}");
                
                // Validate input
                if (string.IsNullOrEmpty(rootPath))
                {
                    MessageBox.Show($"Error: Root path cannot be empty. Tab not created.",
                        "Directory Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                if (!Directory.Exists(rootPath))
                {
                    MessageBox.Show($"Error: Cannot access directory '{rootPath}'. Tab not created.",
                        "Directory Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // Ensure history manager is initialized
                if (_historyManager == null)
                {
                    _historyManager = new TabHistoryManager();
                }

                // Create content panel
                Grid tabContent = new Grid();
                
                // Create file tree view
                ImprovedFileTreeListView fileTree = new ImprovedFileTreeListView();
                
                // Add file tree to the grid BEFORE connecting events or setting root
                tabContent.Children.Add(fileTree);
                
                // Create and add the tab with icon support
                TabItem newTab = new TabItem
                {
                    Header = title,
                    Content = tabContent,
                    Tag = new TabMetadata 
                    { 
                        Path = rootPath, 
                        Icon = GetFolderIcon(rootPath),
                        LastAccessed = DateTime.Now,
                        ScrollPosition = 0,
                        SelectedItems = new List<string>()
                    }
                };
                
                TabControl.Items.Add(newTab);
                TabControl.SelectedItem = newTab;
                
                // Now hook up file tree events
                try {
                    fileTree.FileTreeClicked += FileTree_FileTreeClicked;
                    fileTree.ContextMenuActionTriggered += FileTree_ContextMenuActionTriggered;
                }
                catch (Exception ex) {
                    Console.WriteLine($"Warning: Could not connect events: {ex.Message}");
                    // Continue anyway - the tab will still be usable
                }
                
                // Now set root directory AFTER the tree is in the visual tree
                try {
                    fileTree.SetRootDirectory(rootPath);
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error setting root directory: {ex.Message}");
                    // Continue anyway - we'll at least have a tab
                }
                
                // Initialize history for this tab with scroll position and selection state
                _historyManager.InitTabHistory(TabControl.Items.Count - 1, rootPath);
                
                // Initialize memory tracking
                _tabMemoryUsage[newTab] = EstimateTabMemoryUsage(newTab);
                
                // Notify parent that this manager is now active with this path
                ActiveManagerChanged?.Invoke(this, rootPath);
                
                return newTab;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating new tab: {ex.Message}",
                    "Tab Creation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Adds a new tab with custom content
        /// </summary>
        /// <param name="title">Title for the tab</param>
        /// <param name="content">Content for the tab</param>
        /// <returns>The created tab</returns>
        public TabItem AddTab(string title, UIElement content)
        {
            TabItem newTab = new TabItem
            {
                Header = title,
                Content = content
            };
            
            TabControl.Items.Add(newTab);
            TabControl.SelectedItem = newTab;
            
            return newTab;
        }

        /// <summary>
        /// Adds a new nested tab
        /// </summary>
        public void AddNewNestedTab()
        {
            try
            {
                string defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "OneDrive - Fendler Patterson Construction, Inc");
                    
                // Use Documents folder as fallback
                if (!Directory.Exists(defaultPath))
                {
                    defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                
                // Use user profile as a last resort
                if (!Directory.Exists(defaultPath))
                {
                    defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
                
                string folderLabel = Path.GetFileName(defaultPath);
                if (string.IsNullOrEmpty(folderLabel))
                {
                    folderLabel = defaultPath; // For root paths like "C:\"
                }
                
                AddNewFileTreeTab(folderLabel, defaultPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding nested tab: {ex.Message}");
                MessageBox.Show($"Error creating new tab: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handler for tab control selection changed
        /// </summary>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCurrentPath();
        }

        /// <summary>
        /// Updates the current path based on the active tab
        /// </summary>
        private void UpdateCurrentPath()
        {
            try
            {
                if (TabControl.SelectedItem is TabItem tabItem)
                {
                    if (tabItem.Content is FrameworkElement tabContent)
                    {
                        var fileTree = FindFileTree(tabContent);
                        if (fileTree != null)
                        {
                            string activePath = DeterminePathForAddressBar(fileTree);
                            CurrentPathChanged?.Invoke(this, activePath);
                            ActiveManagerChanged?.Invoke(this, activePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating current path: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines the path to display in the address bar
        /// </summary>
        /// <param name="fileTree">The file tree to get the path from</param>
        /// <returns>The path to display</returns>
        private string DeterminePathForAddressBar(ImprovedFileTreeListView fileTree)
        {
            if (fileTree == null)
                return string.Empty;

            // Check if there's a selection
            if (fileTree.HasSelectedItems)
            {
                string? selectedPath = fileTree.GetSelectedPath();
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    return selectedPath;
                }
            }
            
            // Otherwise use root path
            return fileTree.GetCurrentPath();
        }

        /// <summary>
        /// Handler for add button click
        /// </summary>
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewNestedTab();
        }

        /// <summary>
        /// Handler for double-click on tab header to add a new tab - FIXED VERSION
        /// </summary>
        private void TabControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Get the original source of the click
            var originalSource = e.OriginalSource as DependencyObject;
            
            // Check if the click originated from within a file tree (most specific check first)
            var fileTree = FindAncestorOfType<ImprovedFileTreeListView>(originalSource);
            if (fileTree != null)
            {
                // Double-click came from file tree - ignore it
                return;
            }
            
            // Check if we're clicking within a Grid (tab content container)
            var grid = FindAncestorOfType<Grid>(originalSource);
            if (grid != null)
            {
                // Check if this grid is a tab's content (has a file tree as child)
                foreach (UIElement child in grid.Children)
                {
                    if (child is ImprovedFileTreeListView)
                    {
                        // This is a tab content grid - ignore the double-click
                        return;
                    }
                }
            }
            
            // Check if the click originated from within any tab item
            var tabItem = FindAncestorOfType<TabItem>(originalSource);
            if (tabItem != null)
            {
                // Click was on a tab header or inside a tab - ignore it
                return;
            }
            
            // If we get here, the click was on empty space in the tab control header area
            AddNewNestedTab();
            e.Handled = true;
        }

        /// <summary>
        /// Handler for close tab button click
        /// </summary>
        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            // Find the tab to close
            Button? closeButton = sender as Button;
            if (closeButton != null)
            {
                var tabItem = FindAncestorOfType<TabItem>(closeButton);
                if (tabItem != null)
                {
                    int index = TabControl.Items.IndexOf(tabItem);
                    if (index >= 0)
                    {
                        CloseTab(index);
                    }
                }
            }
        }

        /// <summary>
        /// Closes a tab
        /// </summary>
        /// <param name="index">Index of the tab to close</param>
        public void CloseTab(int index)
        {
            if (index < 0 || index >= TabControl.Items.Count)
                return;

            // Handle tab history removal
            _historyManager.RemoveTabHistory(index);

            // Handle normal tab closing
            if (TabControl.Items.Count > 1)
            {
                TabItem? tabToClose = TabControl.Items[index] as TabItem;
                TabControl.Items.RemoveAt(index);
                
                // Clean up resources if needed
                if (tabToClose?.Content is Grid grid)
                {
                    foreach (var child in grid.Children)
                    {
                        if (child is ImprovedFileTreeListView fileTree)
                        {
                            fileTree.Dispose();
                        }
                    }
                }
            }
            else
            {
                // Don't close the last tab
                MessageBox.Show("Cannot close the last tab.", 
                    "Close Tab", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Tab Enhancement Methods

        /// <summary>
        /// Metadata for tab management
        /// </summary>
        public class TabMetadata
        {
            public string Path { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public DateTime LastAccessed { get; set; }
            public double ScrollPosition { get; set; }
            public List<string> SelectedItems { get; set; } = new List<string>();
        }

        /// <summary>
        /// Gets folder icon path for a directory
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <returns>Icon path or empty string</returns>
        private string GetFolderIcon(string path)
        {
            // For now, return empty string - icons are handled by XAML DrawingImage
            // In future versions, this could return dynamic icons based on folder type
            return string.Empty;
        }

        /// <summary>
        /// Estimates memory usage for a tab
        /// </summary>
        /// <param name="tab">Tab to estimate</param>
        /// <returns>Estimated memory usage in bytes</returns>
        private long EstimateTabMemoryUsage(TabItem tab)
        {
            if (tab?.Content is Grid grid)
            {
                var fileTree = FindFileTree(grid);
                if (fileTree != null)
                {
                    // Rough estimation based on number of items in file tree
                    // This is a simplified calculation
                    try
                    {
                        long baseMemory = 1024 * 1024; // 1MB base
                        // Add more sophisticated calculation if needed
                        return baseMemory;
                    }
                    catch
                    {
                        return 1024 * 1024; // Default 1MB
                    }
                }
            }
            return 1024 * 512; // Default 512KB for empty tabs
        }

        /// <summary>
        /// Monitors memory usage of all tabs
        /// </summary>
        private void MonitorTabMemory()
        {
            try
            {
                Dictionary<TabItem, long> newUsage = new Dictionary<TabItem, long>();
                
                foreach (TabItem tab in TabControl.Items)
                {
                    if (tab?.Content is Grid grid)
                    {
                        var fileTree = FindFileTree(grid);
                        if (fileTree != null)
                        {
                            // Calculate approximate memory usage
                            long memUsage = GC.GetTotalMemory(false) / Math.Max(TabControl.Items.Count, 1);
                            newUsage[tab] = memUsage;
                            
                            // Update metadata
                            if (tab.Tag is TabMetadata metadata)
                            {
                                metadata.LastAccessed = TabControl.SelectedItem == tab ? DateTime.Now : metadata.LastAccessed;
                            }
                        }
                    }
                }
                
                _tabMemoryUsage = newUsage;
                
                // Optional: Log high memory usage
                foreach (var kvp in _tabMemoryUsage)
                {
                    if (kvp.Value > 50 * 1024 * 1024) // 50MB threshold
                    {
                        System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] High memory tab: {kvp.Key.Header} - {kvp.Value / (1024 * 1024)}MB");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Error monitoring memory: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle mouse down for drag initiation
        /// </summary>
        private void TabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(TabControl);
            _isDragging = false;
        }

        /// <summary>
        /// Detach a tab to a new TabManager window
        /// </summary>
        /// <param name="tabItem">Tab to detach</param>
        public void DetachTabToNewTabManager(TabItem tabItem)
        {
            if (tabItem == null || TabControl.Items.Count <= 1)
                return;

            try
            {
                string tabTitle = tabItem.Header?.ToString() ?? "Detached";
                object? tabContent = tabItem.Content;
                if (tabContent == null) return;

                // Remove from current tab control
                int index = TabControl.Items.IndexOf(tabItem);
                TabControl.Items.RemoveAt(index);

                // Create new window with TabManager
                Window newWindow = new Window
                {
                    Title = $"ExplorerPro - {tabTitle}",
                    Width = 900,
                    Height = 650,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                TabManager newTabManager = new TabManager
                {
                    OriginalTabManager = this
                };

                newWindow.Content = newTabManager;

                // Create new tab in the new manager
                TabItem newTab = new TabItem
                {
                    Header = tabTitle,
                    Content = tabContent,
                    Tag = tabItem.Tag
                };

                newTabManager.TabControl.Items.Add(newTab);
                newTabManager.TabControl.SelectedItem = newTab;

                // Track the detached window
                _detachedWindows.Add(newWindow);
                
                // Show the window
                newWindow.Show();
                newWindow.Activate();

                // Update history indices
                _historyManager.RemoveTabHistory(index);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error detaching tab: {ex.Message}",
                    "Detach Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Context Menu Handlers

        /// <summary>
        /// Handler for detach tab menu item click
        /// </summary>
        private void DetachTab_Click(object sender, RoutedEventArgs e)
        {
            // Get the selected tab
            int selectedIndex = TabControl.SelectedIndex;
            if (selectedIndex >= 0)
            {
                DetachTab(selectedIndex);
            }
        }

        /// <summary>
        /// Handler for reattach tab menu item click
        /// </summary>
        private void ReattachTab_Click(object sender, RoutedEventArgs e)
        {
            // Get the selected tab
            int selectedIndex = TabControl.SelectedIndex;
            if (selectedIndex >= 0 && OriginalTabManager != null)
            {
                ReattachTab(selectedIndex, OriginalTabManager);
            }
        }

        /// <summary>
        /// Handler for split view menu item click
        /// </summary>
        private void SplitView_Click(object sender, RoutedEventArgs e)
        {
            ToggleSplitView();
        }

        /// <summary>
        /// Detaches a tab into a new window
        /// </summary>
        /// <param name="index">The index of the tab to detach</param>
        public void DetachTab(int index)
        {
            if (index < 0 || index >= TabControl.Items.Count || TabControl.Items.Count <= 1)
                return;

            try
            {
                // Get the tab to detach
                TabItem? tabItem = TabControl.Items[index] as TabItem;
                if (tabItem == null) return;
                
                string tabTitle = tabItem.Header?.ToString() ?? "Detached";
                object? tabContent = tabItem.Content;
                if (tabContent == null) return;

                // Remove from this tab control
                TabControl.Items.RemoveAt(index);

                // Create a new window
                Window newWindow = new Window
                {
                    Title = $"Detached - {tabTitle}",
                    Width = 800,
                    Height = 600
                };

                // Create a new tab manager for the detached window
                TabManager detachedTabManager = new TabManager
                {
                    OriginalTabManager = this
                };

                // Set as window content
                newWindow.Content = detachedTabManager;

                // Add the tab content to the new tab manager
                TabItem newTab = new TabItem
                {
                    Header = tabTitle,
                    Content = tabContent
                };
                detachedTabManager.TabControl.Items.Add(newTab);
                detachedTabManager.TabControl.SelectedItem = newTab;

                // Track and show the window
                _detachedWindows.Add(newWindow);
                newWindow.Show();
                newWindow.Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error detaching tab: {ex.Message}",
                    "Detach Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Reattaches a tab to its original tab manager
        /// </summary>
        /// <param name="index">Index of tab to reattach</param>
        /// <param name="originalManager">Original tab manager</param>
        public void ReattachTab(int index, TabManager originalManager)
        {
            if (index < 0 || index >= TabControl.Items.Count || originalManager == null)
                return;

            try
            {
                // Get the tab to reattach
                TabItem? tabItem = TabControl.Items[index] as TabItem;
                if (tabItem == null) return;
                
                string tabTitle = tabItem.Header?.ToString() ?? "Reattached";
                object? tabContent = tabItem.Content;
                if (tabContent == null) return;

                // Remove from this tab control
                TabControl.Items.RemoveAt(index);

                // Add to original tab manager
                TabItem newTab = new TabItem
                {
                    Header = tabTitle,
                    Content = tabContent
                };
                originalManager.TabControl.Items.Add(newTab);
                originalManager.TabControl.SelectedItem = newTab;

                // If this window has no tabs left, close it
                if (TabControl.Items.Count == 0)
                {
                    Window? parentWindow = Window.GetWindow(this);
                    parentWindow?.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reattaching tab: {ex.Message}",
                    "Reattach Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Updates the visibility of the reattach menu item
        /// </summary>
        private void UpdateReattachVisibility()
        {
            // ReattachTabMenuItem functionality can be implemented when needed
            // ReattachTabMenuItem.Visibility = 
            //     OriginalTabManager != null ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Handler for refresh tab context menu item
        /// </summary>
        private void RefreshTab_Click(object sender, RoutedEventArgs e)
        {
            RefreshCurrentTab();
        }

        /// <summary>
        /// Handler for refresh all tabs context menu item
        /// </summary>
        private void RefreshAllTabs_Click(object sender, RoutedEventArgs e)
        {
            RefreshAllTabs();
        }

        /// <summary>
        /// Handler for new tab context menu item
        /// </summary>
        private void NewTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddNewFileTreeTab("New File Tree", @"C:\");
        }

        /// <summary>
        /// Handler for duplicate tab context menu item
        /// </summary>
        private void DuplicateTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (TabControl.SelectedItem is TabItem selectedTab)
            {
                var fileTree = FindFileTree(selectedTab.Content as FrameworkElement);
                if (fileTree != null)
                {
                    string currentPath = fileTree.CurrentPath ?? @"C:\";
                    AddNewFileTreeTab($"Copy of {selectedTab.Header}", currentPath);
                }
            }
        }

        /// <summary>
        /// Handler for close tab context menu item
        /// </summary>
        private void CloseTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (TabControl.SelectedItem is TabItem selectedTab)
            {
                CloseTab(TabControl.Items.IndexOf(selectedTab));
            }
        }

        /// <summary>
        /// Handler for detach tab context menu item
        /// </summary>
        private void DetachTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (TabControl.SelectedItem is TabItem selectedTab)
            {
                DetachTabToNewTabManager(selectedTab);
            }
        }

        #endregion

        #region Navigation Methods

        /// <summary>
        /// Opens a directory in the current tab
        /// </summary>
        /// <param name="path">Path to open</param>
        public void OpenDirectoryInCurrentTab(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show($"Error: {path} is not a valid directory.",
                    "Invalid Directory", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int tabIndex = TabControl.SelectedIndex;
            if (tabIndex < 0)
                return;

            // Get the current tab's file tree
            if (TabControl.SelectedItem is TabItem tabItem && tabItem.Content is FrameworkElement tabContent)
            {
                var fileTree = FindFileTree(tabContent);
                if (fileTree != null)
                {
                    fileTree.SetRootDirectory(path);
                    _historyManager.PushPath(tabIndex, path);
                    CurrentPathChanged?.Invoke(this, path);
                }
            }
        }

        /// <summary>
        /// Go up one directory level
        /// </summary>
        public void GoUp()
        {
            int tabIndex = TabControl.SelectedIndex;
            string? newPath = _historyManager.GoUp(tabIndex);
            if (!string.IsNullOrEmpty(newPath))
            {
                SetTabPath(tabIndex, newPath);
            }
        }

        /// <summary>
        /// Go back in history
        /// </summary>
        public void GoBack()
        {
            int tabIndex = TabControl.SelectedIndex;
            
            // Store current scroll position and selection before navigating
            StoreCurrentTabState(tabIndex);
            
            string? newPath = _historyManager.GoBack(tabIndex);
            if (!string.IsNullOrEmpty(newPath))
            {
                SetTabPath(tabIndex, newPath);
                
                // Restore scroll position and selection for the target path
                RestoreTabState(tabIndex, newPath);
            }
        }

        /// <summary>
        /// Go forward in history
        /// </summary>
        public void GoForward()
        {
            int tabIndex = TabControl.SelectedIndex;
            
            // Store current scroll position and selection before navigating
            StoreCurrentTabState(tabIndex);
            
            string? newPath = _historyManager.GoForward(tabIndex);
            if (!string.IsNullOrEmpty(newPath))
            {
                SetTabPath(tabIndex, newPath);
                
                // Restore scroll position and selection for the target path
                RestoreTabState(tabIndex, newPath);
            }
        }

        /// <summary>
        /// Store current tab state (scroll position and selection)
        /// </summary>
        /// <param name="tabIndex">Tab index</param>
        private void StoreCurrentTabState(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= TabControl.Items.Count)
                return;

            if (TabControl.Items[tabIndex] is TabItem tabItem && tabItem.Tag is TabMetadata metadata)
            {
                var fileTree = FindActiveFileTree();
                if (fileTree != null)
                {
                    // Store scroll position (simplified - in real implementation would get actual scroll position)
                    metadata.ScrollPosition = 0; // Placeholder
                    
                    // Store selected items
                    try
                    {
                        metadata.SelectedItems.Clear();
                        if (fileTree.HasSelectedItems)
                        {
                            string? selectedPath = fileTree.GetSelectedPath();
                            if (!string.IsNullOrEmpty(selectedPath))
                            {
                                metadata.SelectedItems.Add(selectedPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Error storing tab state: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Restore tab state (scroll position and selection)
        /// </summary>
        /// <param name="tabIndex">Tab index</param>
        /// <param name="path">Target path</param>
        private void RestoreTabState(int tabIndex, string path)
        {
            if (tabIndex < 0 || tabIndex >= TabControl.Items.Count)
                return;

            if (TabControl.Items[tabIndex] is TabItem tabItem && tabItem.Tag is TabMetadata metadata)
            {
                var fileTree = FindActiveFileTree();
                if (fileTree != null && metadata.Path == path)
                {
                    try
                    {
                        // Restore scroll position (simplified)
                        // In real implementation would restore actual scroll position
                        
                        // Restore selection
                        if (metadata.SelectedItems.Count > 0)
                        {
                            // In real implementation would restore selection in file tree
                            System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Would restore selection: {string.Join(", ", metadata.SelectedItems)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Error restoring tab state: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Sets the path for a specific tab
        /// </summary>
        /// <param name="tabIndex">Tab index</param>
        /// <param name="path">Path to set</param>
        private void SetTabPath(int tabIndex, string path)
        {
            if (tabIndex < 0 || tabIndex >= TabControl.Items.Count || string.IsNullOrEmpty(path))
                return;

            if (TabControl.Items[tabIndex] is TabItem tabItem && tabItem.Content is FrameworkElement tabContent)
            {
                var fileTree = FindFileTree(tabContent);
                if (fileTree != null)
                {
                    fileTree.SetRootDirectory(path);
                    CurrentPathChanged?.Invoke(this, path);
                }
            }
        }

        /// <summary>
        /// Toggles split view
        /// </summary>
        public void ToggleSplitView()
        {
            // Get parent window
            var mainWindow = Window.GetWindow(this) as MainWindow.MainWindow;
            if (mainWindow != null && mainWindow.GetCurrentContainer() is MainWindowContainer container)
            {
                container.ToggleSplitView();
            }
            else
            {
                MessageBox.Show("Could not find a valid parent container to toggle split view.",
                    "Split View Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Refreshes the current tab
        /// </summary>
        public void RefreshCurrentTab()
        {
            if (TabControl.SelectedItem is TabItem tabItem && tabItem.Content is FrameworkElement tabContent)
            {
                var fileTree = FindFileTree(tabContent);
                if (fileTree != null)
                {
                    string currentDirectory = ((IFileTree)fileTree).GetCurrentPath();
                    fileTree.SetRootDirectory(currentDirectory);
                }
            }
        }

        /// <summary>
        /// Refreshes all tabs
        /// </summary>
        public void RefreshAllTabs()
        {
            for (int i = 0; i < TabControl.Items.Count; i++)
            {
                if (TabControl.Items[i] is TabItem tabItem && tabItem.Content is FrameworkElement tabContent)
                {
                    var fileTree = FindFileTree(tabContent);
                    if (fileTree != null)
                    {
                        string currentDirectory = ((IFileTree)fileTree).GetCurrentPath();
                        fileTree.SetRootDirectory(currentDirectory);
                    }
                }
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles file tree click events
        /// </summary>
        private void FileTree_FileTreeClicked(object? sender, EventArgs e)
        {
            // Find which tab contains this FileTree
            var clickedTree = sender as ImprovedFileTreeListView;
            if (clickedTree == null)
                return;

            System.Diagnostics.Debug.WriteLine("[TAB-MANAGER] FileTree clicked - activating tab");

            for (int i = 0; i < TabControl.Items.Count; i++)
            {
                if (TabControl.Items[i] is TabItem tabItem && tabItem.Content is FrameworkElement tabContent)
                {
                    var fileTree = FindFileTree(tabContent);
                    if (fileTree == clickedTree)
                    {
                        TabControl.SelectedIndex = i;
                        string activePath = DeterminePathForAddressBar(fileTree);
                        ActiveManagerChanged?.Invoke(this, activePath);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Handles context menu actions from file tree
        /// </summary>
        private void FileTree_ContextMenuActionTriggered(object? sender, Tuple<string, string> e)
        {
            string action = e.Item1;
            string filePath = e.Item2;

            System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Context menu action: '{action}' for '{filePath}'");

            switch (action)
            {
                case "pin":
                    PinItemRequested?.Invoke(this, filePath);
                    break;
                    
                case "open":
                    // CRITICAL: "open" should navigate in current view, NOT create new tab
                    if (sender is ImprovedFileTreeListView fileTree)
                    {
                        if (Directory.Exists(filePath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Navigating to directory: {filePath}");
                            fileTree.SetRootDirectory(filePath);
                            CurrentPathChanged?.Invoke(this, filePath);
                        }
                        else if (File.Exists(filePath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Opening file: {filePath}");
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = filePath,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Failed to open file: {ex.Message}", 
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                    break;
                    
                case "open_in_new_tab":
                    // ONLY this action should create a new tab
                    if (Directory.Exists(filePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Opening in new tab: {filePath}");
                        string tabName = Path.GetFileName(filePath);
                        if (string.IsNullOrEmpty(tabName))
                        {
                            tabName = filePath; // For root paths
                        }
                        AddNewFileTreeTab(tabName, filePath);
                    }
                    break;
                    
                case "open_in_new_window":
                    System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Open in new window not implemented");
                    // TODO: Implement if needed
                    break;
                    
                case "show_in_explorer":
                    if (File.Exists(filePath) || Directory.Exists(filePath))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                    }
                    break;
                    
                case "toggle_split_view":
                    ToggleSplitView();
                    break;
                    
                case "rename":
                case "delete":
                case "copy":
                case "paste":
                case "duplicate":
                case "new_file":
                case "new_folder":
                case "show_metadata":
                case "add_tag":
                case "remove_tag":
                case "collapse_all":
                case "expand_all":
                case "change_text_color":
                case "preview_pdf":
                case "preview_image":
                    // These actions should be handled by the file tree itself
                    // or passed to a higher-level handler
                    System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Action '{action}' not handled at tab level");
                    break;
                    
                default:
                    System.Diagnostics.Debug.WriteLine($"[TAB-MANAGER] Unknown action: {action}");
                    break;
            }
        }

        /// <summary>
        /// Handle tab dragging
        /// </summary>
        private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point position = e.GetPosition(TabControl);
                
                // Check if the mouse has moved far enough to start a drag
                if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    StartDragDrop(position);
                }
            }
        }

        private void TabControl_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("TabItem"))
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void TabControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabItem"))
            {
                TabItem? draggedItem = e.Data.GetData("TabItem") as TabItem;
                if (draggedItem != null)
                {
                    Point dropPosition = e.GetPosition(TabControl);
                    TabItem? targetItem = GetTabItemFromPoint(dropPosition);
                    
                    if (targetItem != null && draggedItem != targetItem)
                    {
                        int sourceIndex = TabControl.Items.IndexOf(draggedItem);
                        int targetIndex = TabControl.Items.IndexOf(targetItem);
                        
                        if (sourceIndex >= 0 && targetIndex >= 0)
                        {
                            MoveTab(sourceIndex, targetIndex);
                        }
                    }
                    else
                    {
                        // Check if dropping outside tab area to detach
                        var tabControl = FindAncestorOfType<TabControl>(targetItem);
                        if (tabControl == null)
                        {
                            DetachTabToNewTabManager(draggedItem);
                        }
                    }
                }
            }
            else if (e.Data.GetDataPresent("TabManagerDrop"))
            {
                // Handle dropping tabs between different TabManager instances
                var droppedData = e.Data.GetData("TabManagerDrop") as Dictionary<string, object>;
                if (droppedData != null)
                {
                    // Implementation for cross-TabManager drops
                    System.Diagnostics.Debug.WriteLine("[TAB-MANAGER] Cross-TabManager drop detected");
                }
            }
        }

        private void StartDragDrop(Point position)
        {
            // Find the tab item under the cursor
            _draggedItem = GetTabItemFromPoint(position);
            if (_draggedItem != null)
            {
                _isDragging = true;
                
                // Create the data object
                DataObject data = new DataObject("TabItem", _draggedItem);
                
                // Start drag-drop operation
                DragDrop.DoDragDrop(_draggedItem, data, DragDropEffects.Move);
                
                _isDragging = false;
                _draggedItem = null;
            }
        }

        /// <summary>
        /// Gets the tab item at a specific point
        /// </summary>
        private TabItem? GetTabItemFromPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(TabControl, point);
            if (result == null)
                return null;

            DependencyObject? obj = result.VisualHit;
            while (obj != null && !(obj is TabItem))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }
            
            return obj as TabItem;
        }

        /// <summary>
        /// Moves a tab from one position to another
        /// </summary>
        private void MoveTab(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= TabControl.Items.Count ||
                targetIndex < 0 || targetIndex >= TabControl.Items.Count)
            {
                return;
            }
            
            // Get the item to move
            object item = TabControl.Items[sourceIndex];
            
            // Remove from source position
            TabControl.Items.RemoveAt(sourceIndex);
            
            // Insert at target position
            TabControl.Items.Insert(targetIndex, item);
            
            // Select the moved tab
            TabControl.SelectedIndex = targetIndex;
            
            // Update history manager indices
            _historyManager.MoveTabHistory(sourceIndex, targetIndex);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Finds the ImprovedFileTreeListView within a container
        /// </summary>
        /// <param name="container">Container to search in</param>
        /// <returns>The ImprovedFileTreeListView or null</returns>
        private ImprovedFileTreeListView? FindFileTree(DependencyObject? container)
        {
            if (container == null)
                return null;

            // Check if the container is directly a ImprovedFileTreeListView
            if (container is ImprovedFileTreeListView fileTree)
                return fileTree;

            // If it's a panel, look through children
            if (container is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is ImprovedFileTreeListView tree)
                        return tree;

                    // Recursive search
                    if (child is DependencyObject dependencyObject)
                    {
                        var foundTree = FindFileTree(dependencyObject);
                        if (foundTree != null)
                            return foundTree;
                    }
                }
            }

            // If it's ContentControl, check its content
            if (container is ContentControl contentControl)
            {
                if (contentControl.Content is ImprovedFileTreeListView contentTree)
                    return contentTree;

                if (contentControl.Content is DependencyObject contentObject)
                {
                    return FindFileTree(contentObject);
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the ImprovedFileTreeListView in the active tab
        /// </summary>
        /// <returns>The active ImprovedFileTreeListView or null</returns>
        public ImprovedFileTreeListView? FindActiveFileTree()
        {
            if (TabControl.SelectedItem is TabItem tabItem && tabItem.Content is FrameworkElement tabContent)
            {
                return FindFileTree(tabContent);
            }
            return null;
        }

        /// <summary>
        /// Find an ancestor of a specific type
        /// </summary>
        /// <typeparam name="T">Type to find</typeparam>
        /// <param name="element">Starting element</param>
        /// <returns>The ancestor or null</returns>
        private T? FindAncestorOfType<T>(DependencyObject? element) where T : DependencyObject
        {
            if (element == null)
                return null;
                
            while (element != null && !(element is T))
            {
                element = VisualTreeHelper.GetParent(element);
            }
            return element as T;
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases resources
        /// </summary>
        public void Dispose()
        {
            // Stop memory monitoring timer
            _memoryMonitorTimer?.Stop();
            _memoryMonitorTimer = null;

            // Close any detached windows
            foreach (var window in _detachedWindows)
            {
                window.Close();
            }
            _detachedWindows.Clear();

            // Clean up any file trees
            for (int i = 0; i < TabControl.Items.Count; i++)
            {
                if (TabControl.Items[i] is TabItem tabItem && tabItem.Content is FrameworkElement tabContent)
                {
                    var fileTree = FindFileTree(tabContent);
                    if (fileTree != null)
                    {
                        fileTree.Dispose();
                    }
                }
            }

            // Clear memory usage tracking
            _tabMemoryUsage.Clear();
        }

        #endregion
    }
}