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
                
                // Initialize tab history manager
                _historyManager = new TabHistoryManager();
                
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

        #region Essential Methods Required by MainWindow

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
        /// Refresh the current tab
        /// </summary>
        public void RefreshCurrentTab()
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

        private void MonitorTabMemory()
        {
            // Monitor memory usage
        }

        private void UpdateReattachVisibility()
        {
            // Update UI visibility for reattach button
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