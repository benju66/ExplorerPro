using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using ExplorerPro.UI.FileTree;
using ExplorerPro.UI.MainWindow;

namespace ExplorerPro.UI.PaneManagement
{
    /// <summary>
    /// Interaction logic for PaneManager.xaml
    /// Manages panes containing ImprovedFileTreeListView instances
    /// </summary>
    public partial class PaneManager : UserControl, IDisposable
    {
        #region Fields

        // Event raised when the active tab changes to notify parent container
        public event EventHandler<string>? CurrentPathChanged;
        
        // Event raised when a pin request is made from a tab
        public event EventHandler<string>? PinItemRequested;
        
        // Event raised when this pane manager becomes active
        public event EventHandler<string>? ActiveManagerChanged;
        
        // Track original manager when a tab is detached
        private PaneManager? _originalPaneManager;
        
        // Navigation history 
        private PaneHistoryManager _historyManager = null!;
        
        // Track detached windows
        private List<Window> _detachedWindows = new List<Window>();
        
        // Track splitter
        private bool _splitViewActive = false;

        // Drag and drop fields
        private TabItem? _draggedItem;
        private Point _dragStartPoint;
        private bool _isDragging;

        // Memory monitoring
        private Dictionary<TabItem, long> _paneMemoryUsage = new Dictionary<TabItem, long>();
        private System.Windows.Threading.DispatcherTimer _memoryMonitorTimer;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the original pane manager this was detached from
        /// </summary>
        public PaneManager? OriginalPaneManager
        {
            get { return _originalPaneManager; }
            set { _originalPaneManager = value; UpdateReattachVisibility(); }
        }

        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// Initializes a new instance of the PaneManager
        /// </summary>
        public PaneManager()
        {
            try
            {
                InitializeComponent();
                
                // Initialize pane history manager
                _historyManager = new PaneHistoryManager();
                
                // Setup event handling - safe navigation for XAML elements
                if (TabControl != null)
                {
                    TabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
                    TabControl.SelectionChanged += TabControl_SelectionChanged;
                    
                    // Setup drag-drop
                    TabControl.AllowDrop = true;
                    TabControl.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
                    TabControl.PreviewMouseMove += TabControl_PreviewMouseMove;
                    TabControl.DragEnter += TabControl_DragEnter;
                    TabControl.Drop += TabControl_Drop;
                }
                
                // Initialize memory monitoring
                InitializeMemoryMonitoring();
                
                Console.WriteLine("PaneManager initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing PaneManager: {ex.Message}");
                
                // Create history manager anyway to avoid null reference
                if (_historyManager == null)
                {
                    _historyManager = new PaneHistoryManager();
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
            _memoryMonitorTimer.Tick += (s, e) => MonitorPaneMemory();
            _memoryMonitorTimer.Start();
        }

        #endregion

        #region Essential Methods Required by MainWindow

        /// <summary>
        /// Adds a new pane with a ImprovedFileTreeListView
        /// </summary>
        /// <param name="title">Title for the pane</param>
        /// <param name="rootPath">Root path for the file tree</param>
        /// <returns>The created pane content</returns>
        public TabItem? AddNewFileTreePane(string title, string rootPath)
        {
            try
            {
                // Validate input
                if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
                {
                    return null;
                }

                // Create content panel
                Grid tabContent = new Grid();
                
                // Create file tree view
                ImprovedFileTreeListView fileTree = new ImprovedFileTreeListView();
                tabContent.Children.Add(fileTree);
                
                // Create and add the tab
                TabItem newTab = new TabItem
                {
                    Header = title,
                    Content = tabContent
                };
                
                TabControl.Items.Add(newTab);
                TabControl.SelectedItem = newTab;
                
                // Set root directory AFTER the tree is in the visual tree
                fileTree.SetRootDirectory(rootPath);
                
                return newTab;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating new tab: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds the active file tree in the current tab
        /// </summary>
        /// <returns>The active file tree or null</returns>
        public ImprovedFileTreeListView? FindActiveFileTree()
        {
            try
            {
                if (TabControl.SelectedItem is TabItem selectedTab &&
                    selectedTab.Content is Grid grid &&
                    grid.Children.Count > 0 &&
                    grid.Children[0] is ImprovedFileTreeListView fileTree)
                {
                    return fileTree;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding active file tree: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Navigate up one directory level in the current tab
        /// </summary>
        public void GoUp()
        {
            try
            {
                                 var fileTree = FindActiveFileTree();
                 if (fileTree != null)
                 {
                     var currentPath = fileTree.CurrentPath;
                     if (!string.IsNullOrEmpty(currentPath))
                     {
                         var parent = Directory.GetParent(currentPath);
                         if (parent != null)
                         {
                             fileTree.SetRootDirectory(parent.FullName);
                         }
                     }
                 }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error navigating up: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigate back in history
        /// </summary>
        public void GoBack()
        {
            try
            {
                int selectedIndex = TabControl.SelectedIndex;
                if (selectedIndex >= 0)
                {
                    string previousPath = _historyManager.GoBack(selectedIndex);
                    if (!string.IsNullOrEmpty(previousPath))
                    {
                        var fileTree = FindActiveFileTree();
                        fileTree?.SetRootDirectory(previousPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error navigating back: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigate forward in history
        /// </summary>
        public void GoForward()
        {
            try
            {
                int selectedIndex = TabControl.SelectedIndex;
                if (selectedIndex >= 0)
                {
                    string nextPath = _historyManager.GoForward(selectedIndex);
                    if (!string.IsNullOrEmpty(nextPath))
                    {
                        var fileTree = FindActiveFileTree();
                        fileTree?.SetRootDirectory(nextPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error navigating forward: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh the current pane
        /// </summary>
        public void RefreshCurrentPane()
        {
            try
            {
                var fileTree = FindActiveFileTree();
                fileTree?.RefreshView();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing current tab: {ex.Message}");
            }
        }

        /// <summary>
        /// Alias for AddNewFileTreePane - used by MainWindowContainer for compatibility
        /// </summary>
        /// <param name="title">Title for the tab</param>
        /// <param name="rootPath">Root path for the file tree</param>
        /// <returns>The created tab item</returns>
        public TabItem? AddNewFileTreeTab(string title, string rootPath)
        {
            return AddNewFileTreePane(title, rootPath);
        }

        /// <summary>
        /// Alias for RefreshCurrentPane - used by MainWindowContainer for compatibility
        /// </summary>
        public void RefreshCurrentTab()
        {
            RefreshCurrentPane();
        }

        #endregion

        #region Event Handlers

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                UpdateCurrentPath();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in selection changed: {ex.Message}");
            }
        }

        private void UpdateCurrentPath()
        {
            try
            {
                                 var fileTree = FindActiveFileTree();
                 if (fileTree != null)
                 {
                     string currentPath = fileTree.CurrentPath ?? "";
                     CurrentPathChanged?.Invoke(this, currentPath);
                 }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating current path: {ex.Message}");
            }
        }

        private void TabControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Handle double-click events
        }

        private void TabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Handle mouse down for drag operations
        }

        private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            // Handle mouse move for drag operations
        }

        private void TabControl_DragEnter(object sender, DragEventArgs e)
        {
            // Handle drag enter
        }

        private void TabControl_Drop(object sender, DragEventArgs e)
        {
            // Handle drop operations
        }

        private void MonitorPaneMemory()
        {
            // Monitor memory usage
        }

        private void UpdateReattachVisibility()
        {
            // Update UI visibility for reattach button
        }

        #endregion

        #region Context Menu Event Handlers

        /// <summary>
        /// Handle new pane menu item click
        /// </summary>
        private void NewPaneMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddNewFileTreePane("New Pane", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating new pane: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle duplicate pane menu item click
        /// </summary>
        private void DuplicatePaneMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedTab = TabControl.SelectedItem as TabItem;
                if (selectedTab?.Content is ImprovedFileTreeListView fileTree)
                {
                    string currentPath = fileTree.CurrentPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string tabName = selectedTab.Header?.ToString() ?? "Duplicate";
                    AddNewFileTreePane($"{tabName} - Copy", currentPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error duplicating pane: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle pin/unpin pane button click
        /// </summary>
        private void PinPaneButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedTab = TabControl.SelectedItem as TabItem;
                if (selectedTab?.Content is ImprovedFileTreeListView fileTree)
                {
                    string currentPath = fileTree.CurrentPath ?? "";
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        PinItemRequested?.Invoke(this, currentPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pinning pane: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle close pane menu item click
        /// </summary>
        private void ClosePaneMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedTab = TabControl.SelectedItem as TabItem;
                if (selectedTab != null && TabControl.Items.Count > 1)
                {
                    TabControl.Items.Remove(selectedTab);
                    
                    // Dispose the file tree if it's disposable
                    if (selectedTab.Content is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing pane: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle refresh pane menu item click
        /// </summary>
        private void RefreshPane_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshCurrentPane();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing pane: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle refresh all panes menu item click
        /// </summary>
        private void RefreshAllPanes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (TabItem tab in TabControl.Items)
                {
                    if (tab.Content is ImprovedFileTreeListView fileTree)
                    {
                        string currentPath = fileTree.CurrentPath ?? "";
                        if (!string.IsNullOrEmpty(currentPath))
                        {
                            fileTree.RefreshDirectory(currentPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing all panes: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle detach pane menu item click
        /// </summary>
        private void DetachPaneMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedTab = TabControl.SelectedItem as TabItem;
                if (selectedTab != null)
                {
                    // TODO: Implement pane detachment functionality
                    Console.WriteLine("Detach pane functionality not yet implemented");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detaching pane: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _memoryMonitorTimer?.Stop();
            _memoryMonitorTimer = null;
        }

        #endregion
    }
} 