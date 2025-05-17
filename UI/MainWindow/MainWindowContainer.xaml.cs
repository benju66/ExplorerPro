using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree;
using ExplorerPro.UI.TabManagement;
using ExplorerPro.UI.Panels.PinnedPanel;
using ExplorerPro.UI.Panels.BookmarksPanel;
using ExplorerPro.UI.Panels.ToDoPanel;
using ExplorerPro.UI.Panels.ProcoreLinksPanel;

namespace ExplorerPro.UI.MainWindow
{
    /// <summary>
    /// Interaction logic for MainWindowContainer.xaml
    /// Container that hosts dockable panels and a tab manager.
    /// Multiple instances can be created as tabs within MainWindow.
    /// </summary>
    public partial class MainWindowContainer : UserControl
    {
        #region Fields

        // Track all instances
        private static List<MainWindowContainer> _allContainers = new List<MainWindowContainer>();

        // Parent window reference
        private readonly MainWindow _parentWindow;

        // Core managers
        private readonly SettingsManager _settingsManager;

        // Tab management
        private TabManager? _tabManager;
        private TabManager? _rightTabManager; // For split view
        private TabManager? _activeTabManager;
        
        // Panel references
        private PinnedPanel? _pinnedPanel;
        private BookmarksPanel? _bookmarksPanel;
        private ToDoPanel? _toDoPanel;
        private ProcoreLinksPanel? _procorePanel;

        // Panel state tracking
        private bool _splitViewActive;
        private Dictionary<string, double> _panelWidths = new Dictionary<string, double>();
        private HashSet<string> _panelsInConsole = new HashSet<string>();
        private DispatcherTimer? _consoleAnimationTimer;
        private bool _consoleAnimationActive;
        
        // Constants
        private const double DEFAULT_PANEL_WIDTH = 200;
        private const double COLLAPSED_PANEL_WIDTH = 3;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the active tab manager
        /// </summary>
        public TabManager? ActiveTabManager => _activeTabManager;

        /// <summary>
        /// Gets the pinned panel
        /// </summary>
        public PinnedPanel? PinnedPanel => _pinnedPanel;

        #endregion

        #region Events

        /// <summary>
        /// Event fired when path changes in active tab
        /// </summary>
        public event EventHandler<string>? PathChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of MainWindowContainer
        /// </summary>
        /// <param name="parentWindow">Parent window</param>
        public MainWindowContainer(MainWindow parentWindow)
        {
            try
            {
                InitializeComponent();

                _parentWindow = parentWindow;
                _settingsManager = App.Settings ?? new SettingsManager(); // Use App.Settings instead of SettingsManager.Instance
                _activeTabManager = null;
                _splitViewActive = false;

                // Track this instance
                _allContainers.Add(this);

                // Configure drop handling
                DockArea.AllowDrop = true;
                DockArea.DragEnter += DockArea_DragEnter;
                DockArea.DragOver += DockArea_DragOver;
                DockArea.DragLeave += DockArea_DragLeave;
                DockArea.Drop += DockArea_Drop;

                // Initialize container
                InitializeContainer();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MainWindowContainer constructor: {ex.Message}");
                MessageBox.Show($"Error initializing container: {ex.Message}",
                    "Container Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw; // Re-throw to let caller handle the error
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize container components
        /// </summary>
        private void InitializeContainer()
        {
            try
            {
                // Create tab manager
                _tabManager = new TabManager();
                _tabManager.CurrentPathChanged += TabManager_CurrentPathChanged;
                _tabManager.PinItemRequested += TabManager_PinItemRequested;
                _tabManager.ActiveManagerChanged += TabManager_ActiveManagerChanged;
                MainContent.Content = _tabManager;

                // Set initial active tab manager
                _activeTabManager = _tabManager;

                // Create panels
                CreatePanels();

                // Set up console animation timer
                _consoleAnimationTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };
                _consoleAnimationTimer.Tick += ConsoleAnimationTimer_Tick;

                // Install event handlers for panel resizing
                InstallPanelResizeHandlers();

                // Apply saved panel visibility settings
                ApplySavedPanelVisibility(null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing container: {ex.Message}");
                MessageBox.Show($"Error initializing container: {ex.Message}",
                    "Container Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Try to at least initialize the tab manager if it failed
                if (_tabManager == null)
                {
                    try
                    {
                        _tabManager = new TabManager();
                        MainContent.Content = _tabManager;
                        _activeTabManager = _tabManager;
                    }
                    catch
                    {
                        // If even this fails, we'll let the calling code handle it
                    }
                }
            }
        }

        /// <summary>
        /// Initialize with a file tree at the specified path
        /// </summary>
        /// <param name="rootPath">Path to open</param>
        public void InitializeWithFileTree(string rootPath)
        {
            try
            {
                // Validate path more thoroughly
                string validPath = ValidatePath(rootPath);
                    
                string name = string.IsNullOrEmpty(validPath) ? "Documents" : 
                    (Path.GetFileName(validPath) ?? validPath);

                // Ensure name is not empty if path is a root directory
                if (string.IsNullOrEmpty(name) || name.EndsWith(":\\"))
                {
                    name = validPath; // For root paths like "C:\"
                }

                // Add a file tree tab with validated path
                _tabManager?.AddNewFileTreeTab(name, validPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing file tree: {ex.Message}");
                // Fall back to a safe location
                string fallbackPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                try
                {
                    _tabManager?.AddNewFileTreeTab("Documents", fallbackPath);
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"Failed to initialize with fallback path: {innerEx.Message}");
                    // Try one more time with user profile as a last resort
                    string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _tabManager?.AddNewFileTreeTab("Home", userPath);
                }
            }
        }

        /// <summary>
        /// Helper method to validate paths
        /// </summary>
        private string ValidatePath(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            try
            {
                // Try to normalize the path
                string normalizedPath = Path.GetFullPath(rootPath);
                
                // Check if it exists
                if (Directory.Exists(normalizedPath))
                {
                    return normalizedPath;
                }
                
                // If original path doesn't exist, try common fallbacks
                // First try Documents folder
                string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (Directory.Exists(docsPath))
                {
                    return docsPath;
                }
                
                // If all else fails, use the user profile folder
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            catch
            {
                // If any error occurs in path processing, fallback to My Documents
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        /// <summary>
        /// Create all dockable panels with error handling
        /// </summary>
        private void CreatePanels()
        {
            try
            {
                // Create panel instances with proper error handling
                try
                {
                    _pinnedPanel = new PinnedPanel();
                    PinnedPanelContent.Content = _pinnedPanel;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating Pinned Panel: {ex.Message}");
                    PinnedPanelContainer.Visibility = Visibility.Collapsed;
                }
                
                try
                {
                    _bookmarksPanel = new BookmarksPanel();
                    BookmarksPanelContent.Content = _bookmarksPanel;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating Bookmarks Panel: {ex.Message}");
                    BookmarksPanelContainer.Visibility = Visibility.Collapsed;
                }
                
                try
                {
                    _toDoPanel = new ToDoPanel();
                    ToDoPanelContent.Content = _toDoPanel;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating ToDo Panel: {ex.Message}");
                    ToDoPanelContainer.Visibility = Visibility.Collapsed;
                }
                
                try
                {
                    _procorePanel = new ProcoreLinksPanel();
                    ProcorePanelContent.Content = _procorePanel;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating Procore Panel: {ex.Message}");
                    ProcorePanelContainer.Visibility = Visibility.Collapsed;
                }

                // Hide Procore panel by default
                ProcorePanelContainer.Visibility = Visibility.Collapsed;

                // Connect pinned panel events only if successfully created
                if (_pinnedPanel != null)
                {
                    try
                    {
                        _pinnedPanel.PinnedItemAdded += (s, e) => 
                            _parentWindow?.RefreshAllPinnedPanels(e.Value);
                            
                        _pinnedPanel.PinnedItemModified += (s, e) => 
                            _parentWindow?.RefreshAllPinnedPanels(e.OldPath, e.NewPath);
                            
                        _pinnedPanel.PinnedItemRemoved += (s, e) => 
                            _parentWindow?.RefreshAllPinnedPanels(e.Value);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error connecting pinned panel events: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in panel creation: {ex.Message}");
                // Ensure all panels are hidden if initialization fails
                PinnedPanelContainer.Visibility = Visibility.Collapsed;
                BookmarksPanelContainer.Visibility = Visibility.Collapsed;
                ToDoPanelContainer.Visibility = Visibility.Collapsed;
                ProcorePanelContainer.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Install handlers for panel edge resize
        /// </summary>
        private void InstallPanelResizeHandlers()
        {
            try
            {
                // Attach handlers to all panel borders
                AttachPanelResizeHandlers(LeftColumnContainer);
                AttachPanelResizeHandlers(RightColumnContainer);
                AttachPanelResizeHandlers(PinnedPanelContainer);
                AttachPanelResizeHandlers(ToDoPanelContainer);
                AttachPanelResizeHandlers(ProcorePanelContainer);
                AttachPanelResizeHandlers(BookmarksPanelContainer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing panel resize handlers: {ex.Message}");
                // Non-critical error, continue
            }
        }

        /// <summary>
        /// Attach resize handlers to a panel border
        /// </summary>
        /// <param name="border">Border to attach handlers to</param>
        private void AttachPanelResizeHandlers(Border border)
        {
            if (border == null) return;
            
            try
            {
                // Handle mouse clicks on edges to collapse/expand
                border.MouseLeftButtonDown += (s, e) =>
                {
                    try
                    {
                        var pos = e.GetPosition(border);
                        double edgeThreshold = 5;
                        
                        // Detect clicks on left or right edge
                        if (pos.X <= edgeThreshold || border.ActualWidth - pos.X <= edgeThreshold)
                        {
                            TogglePanelCollapsedState(border);
                            e.Handled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in MouseLeftButtonDown handler: {ex.Message}");
                    }
                };
                
                // Change cursor when mouse is over edges
                border.MouseMove += (s, e) =>
                {
                    try
                    {
                        var pos = e.GetPosition(border);
                        double edgeThreshold = 5;
                        
                        if (pos.X <= edgeThreshold || border.ActualWidth - pos.X <= edgeThreshold)
                        {
                            border.Cursor = Cursors.SizeWE;
                        }
                        else
                        {
                            border.Cursor = Cursors.Arrow;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in MouseMove handler: {ex.Message}");
                        // Reset cursor to default on error
                        border.Cursor = Cursors.Arrow;
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error attaching panel resize handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply saved panel visibility settings
        /// </summary>
        /// <param name="panelSettings">Panel visibility settings dictionary</param>
        public void ApplySavedPanelVisibility(Dictionary<string, bool>? panelSettings)
        {
            try
            {
                if (panelSettings == null)
                {
                    // Get from settings manager
                    panelSettings = _settingsManager.GetSetting<Dictionary<string, bool>>(
                        "dockable_panels", new Dictionary<string, bool>());
                }

                // Apply to panel containers
                foreach (var setting in panelSettings)
                {
                    switch (setting.Key)
                    {
                        case "pinned_panel":
                            if (PinnedPanelContainer != null)
                                PinnedPanelContainer.Visibility = setting.Value ? Visibility.Visible : Visibility.Collapsed;
                            break;
                        case "bookmarks_panel":
                            if (BookmarksPanelContainer != null)
                                BookmarksPanelContainer.Visibility = setting.Value ? Visibility.Visible : Visibility.Collapsed;
                            break;
                        case "to_do_panel":
                            if (ToDoPanelContainer != null)
                                ToDoPanelContainer.Visibility = setting.Value ? Visibility.Visible : Visibility.Collapsed;
                            break;
                        case "procore_panel":
                            if (ProcorePanelContainer != null)
                                ProcorePanelContainer.Visibility = setting.Value ? Visibility.Visible : Visibility.Collapsed;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying panel visibility settings: {ex.Message}");
                // Continue with default visibility settings
            }
        }

        #endregion

        #region Panel Management

        /// <summary>
        /// Toggle pinned panel visibility
        /// </summary>
        public void TogglePinnedPanel()
        {
            TogglePanelVisibility(PinnedPanelContainer, "pinned_panel");
        }

        /// <summary>
        /// Toggle bookmarks panel visibility
        /// </summary>
        public void ToggleBookmarksPanel()
        {
            TogglePanelVisibility(BookmarksPanelContainer, "bookmarks_panel");
        }

        /// <summary>
        /// Toggle to-do panel visibility
        /// </summary>
        public void ToggleTodoPanel()
        {
            TogglePanelVisibility(ToDoPanelContainer, "to_do_panel");
        }

        /// <summary>
        /// Toggle Procore links panel visibility
        /// </summary>
        public void ToggleProcorePanel()
        {
            TogglePanelVisibility(ProcorePanelContainer, "procore_panel");
        }

        /// <summary>
        /// Toggle a panel's visibility
        /// </summary>
        /// <param name="container">Panel container</param>
        /// <param name="settingName">Setting name for persistence</param>
        private void TogglePanelVisibility(Border container, string settingName)
        {
            try
            {
                if (container == null) return;
                
                if (container.Visibility == Visibility.Visible)
                {
                    // Store width before hiding
                    StoreCurrentPanelWidth(container);
                    
                    // Hide panel
                    container.Visibility = Visibility.Collapsed;
                    
                    // Update settings
                    _settingsManager.UpdateSetting($"dockable_panels.{settingName}", false);
                }
                else
                {
                    // Show panel
                    container.Visibility = Visibility.Visible;
                    
                    // Restore width
                    RestorePanelWidth(container);
                    
                    // Update settings
                    _settingsManager.UpdateSetting($"dockable_panels.{settingName}", true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling panel visibility: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle a panel between normal and collapsed states
        /// </summary>
        /// <param name="container">Panel container to toggle</param>
        private void TogglePanelCollapsedState(Border container)
        {
            try
            {
                if (container == null) return;
                
                var parent = container.Parent as FrameworkElement;
                if (parent == null) return;

                // Check if panel is in left or right column
                if (parent == LeftColumnContainer || parent == RightColumnContainer)
                {
                    // Handle column-level collapse/expand
                    if (parent.Width <= COLLAPSED_PANEL_WIDTH + 2)
                    {
                        ExpandColumn(parent);
                    }
                    else
                    {
                        CollapseColumn(parent);
                    }
                }
                else
                {
                    // Handle individual panel collapse/expand
                    if (container.ActualWidth <= COLLAPSED_PANEL_WIDTH + 2)
                    {
                        ExpandPanel(container);
                    }
                    else
                    {
                        CollapsePanel(container);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling panel collapsed state: {ex.Message}");
            }
        }

        /// <summary>
        /// Collapse a panel to separator width
        /// </summary>
        /// <param name="container">Panel to collapse</param>
        private void CollapsePanel(Border container)
        {
            try
            {
                if (container == null) return;
                
                // Store current width for later restoration
                StoreCurrentPanelWidth(container);
                
                // Set minimum width
                container.Width = COLLAPSED_PANEL_WIDTH;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error collapsing panel: {ex.Message}");
            }
        }

        /// <summary>
        /// Expand a panel from separator state
        /// </summary>
        /// <param name="container">Panel to expand</param>
        private void ExpandPanel(Border container)
        {
            try
            {
                if (container == null) return;
                
                // Restore previous width
                RestorePanelWidth(container);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error expanding panel: {ex.Message}");
                // Try to set a default width if restoration fails
                if (container != null)
                {
                    container.Width = DEFAULT_PANEL_WIDTH;
                }
            }
        }

        /// <summary>
        /// Collapse a column to separator width
        /// </summary>
        /// <param name="column">Column to collapse</param>
        private void CollapseColumn(FrameworkElement column)
        {
            try
            {
                if (column == null) return;
                
                // Store current width
                string columnKey = column.Name;
                _panelWidths[columnKey] = column.ActualWidth;
                
                // Set to collapsed width
                column.Width = COLLAPSED_PANEL_WIDTH;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error collapsing column: {ex.Message}");
            }
        }

        /// <summary>
        /// Expand a column from separator state
        /// </summary>
        /// <param name="column">Column to expand</param>
        private void ExpandColumn(FrameworkElement column)
        {
            try
            {
                if (column == null) return;
                
                // Get stored width
                string columnKey = column.Name;
                double storedWidth = _panelWidths.ContainsKey(columnKey) ? 
                    _panelWidths[columnKey] : DEFAULT_PANEL_WIDTH;
                    
                if (storedWidth <= COLLAPSED_PANEL_WIDTH)
                {
                    storedWidth = DEFAULT_PANEL_WIDTH;
                }
                
                // Apply width
                column.Width = storedWidth;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error expanding column: {ex.Message}");
                // Try to set a default width if expansion fails
                if (column != null)
                {
                    column.Width = DEFAULT_PANEL_WIDTH;
                }
            }
        }

        /// <summary>
        /// Store a panel's current width
        /// </summary>
        /// <param name="panel">Panel to store width for</param>
        private void StoreCurrentPanelWidth(FrameworkElement panel)
        {
            try
            {
                if (panel == null) return;
                
                string panelKey = panel.Name;
                double currentWidth = panel.ActualWidth;
                
                // Only store if not collapsed already
                if (currentWidth > COLLAPSED_PANEL_WIDTH)
                {
                    _panelWidths[panelKey] = currentWidth;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing panel width: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore a panel's width from stored value
        /// </summary>
        /// <param name="panel">Panel to restore width for</param>
        private void RestorePanelWidth(FrameworkElement panel)
        {
            try
            {
                if (panel == null) return;
                
                string panelKey = panel.Name;
                
                if (_panelWidths.ContainsKey(panelKey))
                {
                    double storedWidth = _panelWidths[panelKey];
                    
                    // Use default if stored width is too small
                    if (storedWidth <= COLLAPSED_PANEL_WIDTH)
                    {
                        storedWidth = DEFAULT_PANEL_WIDTH;
                    }
                    
                    panel.Width = storedWidth;
                }
                else
                {
                    // Use default width if no stored value
                    panel.Width = DEFAULT_PANEL_WIDTH;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring panel width: {ex.Message}");
                // Set default width on error
                if (panel != null)
                {
                    panel.Width = DEFAULT_PANEL_WIDTH;
                }
            }
        }

        #endregion

        #region Tab Management

        /// <summary>
        /// Handle tab manager path changed event
        /// </summary>
        private void TabManager_CurrentPathChanged(object? sender, string path)
        {
            try
            {
                // Update parent window address bar
                _parentWindow?.UpdateAddressBar(path);
                
                // Notify subscribers
                PathChanged?.Invoke(this, path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CurrentPathChanged handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle pin item request from tab manager
        /// </summary>
        private void TabManager_PinItemRequested(object? sender, string path)
        {
            try
            {
                if (_pinnedPanel != null && !string.IsNullOrEmpty(path))
                {
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        _pinnedPanel.PinItem(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PinItemRequested handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle tab manager active changed event
        /// </summary>
        private void TabManager_ActiveManagerChanged(object? sender, string path)
        {
            try
            {
                _activeTabManager = sender as TabManager;
                
                // Update UI to reflect active tab manager
                UpdateActiveTab();
                
                // Update address bar with current path
                if (!string.IsNullOrEmpty(path))
                {
                    _parentWindow?.UpdateAddressBar(path);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ActiveManagerChanged handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Update the UI to reflect the active tab
        /// </summary>
        private void UpdateActiveTab()
        {
            try
            {
                // This implementation could highlight the active tab manager
                // or update other UI elements as needed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating active tab: {ex.Message}");
            }
        }

        /// <summary>
        /// Open a directory in a new tab
        /// </summary>
        /// <param name="path">Path to open</param>
        public void OpenDirectoryInNewTab(string path)
        {
            try
            {
                // Validate the path
                string validPath = ValidatePath(path);
                
                if (string.IsNullOrEmpty(validPath))
                {
                    MessageBox.Show("Could not find a valid directory to open.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string fileName = Path.GetFileName(validPath);
                // For root paths like C:\, use full path
                string title = string.IsNullOrEmpty(fileName) ? validPath : fileName;
                
                // Add new tab
                _tabManager?.AddNewFileTreeTab(title, validPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening directory in new tab: {ex.Message}");
                MessageBox.Show($"Error opening directory: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Try with fallback path
                try
                {
                    string fallback = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    _tabManager?.AddNewFileTreeTab("Documents", fallback);
                }
                catch
                {
                    // Last resort, just continue without opening a new tab
                }
            }
        }

        /// <summary>
        /// Finds the ImprovedFileTreeListView in this container
        /// </summary>
        /// <returns>The active ImprovedFileTreeListView or null</returns>
        public ImprovedFileTreeListView? FindFileTree()
        {
            try
            {
                if (_activeTabManager != null)
                {
                    return _activeTabManager.FindActiveFileTree();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding file tree: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Go up one directory level
        /// </summary>
        public void GoUp()
        {
            try
            {
                _activeTabManager?.GoUp();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error navigating up: {ex.Message}");
            }
        }

        /// <summary>
        /// Go back in history
        /// </summary>
        public void GoBack()
        {
            try
            {
                _activeTabManager?.GoBack();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error navigating back: {ex.Message}");
            }
        }

        /// <summary>
        /// Go forward in history
        /// </summary>
        public void GoForward()
        {
            try
            {
                _activeTabManager?.GoForward();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error navigating forward: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh current tab
        /// </summary>
        public void RefreshCurrentTab()
        {
            try
            {
                _activeTabManager?.RefreshCurrentTab();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing tab: {ex.Message}");
            }
        }

        #endregion

        #region Split View

        /// <summary>
        /// Toggle split view
        /// </summary>
        /// <param name="targetPath">Optional path for right pane</param>
        public void ToggleSplitView(string? targetPath = null)
        {
            try
            {
                if (_splitViewActive)
                {
                    // Disable split view
                    DisableSplitView();
                }
                else
                {
                    // Enable split view
                    EnableSplitView(targetPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling split view: {ex.Message}");
                MessageBox.Show($"Error toggling split view: {ex.Message}",
                    "Split View Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Enable split view
        /// </summary>
        /// <param name="targetPath">Path to open in right pane</param>
        private void EnableSplitView(string? targetPath)
        {
            try
            {
                // Determine path for right pane
                string path = targetPath ?? DetermineSplitViewPath();

                // Create right tab manager
                _rightTabManager = new TabManager();
                _rightTabManager.CurrentPathChanged += TabManager_CurrentPathChanged;
                _rightTabManager.PinItemRequested += TabManager_PinItemRequested;
                _rightTabManager.ActiveManagerChanged += TabManager_ActiveManagerChanged;
                
                // Create splitter
                Grid grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                // Create splitter
                GridSplitter splitter = new GridSplitter
                {
                    Width = 3, 
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetColumn(splitter, 1);
                
                // Remove tab manager from current parent
                MainContent.Content = null;
                
                // Add to grid
                if (_tabManager != null)
                {
                    Grid.SetColumn(_tabManager, 0);
                    grid.Children.Add(_tabManager);
                }
                
                grid.Children.Add(splitter);
                
                if (_rightTabManager != null)
                {
                    Grid.SetColumn(_rightTabManager, 2);
                    grid.Children.Add(_rightTabManager);
                }
                
                // Add grid to main content
                MainContent.Content = grid;
                
                // Validate path for right tab manager
                string validPath = ValidatePath(path);
                
                // Open path in right tab manager
                string fileName = Path.GetFileName(validPath);
                string title = string.IsNullOrEmpty(fileName) ? validPath : fileName;
                _rightTabManager?.AddNewFileTreeTab(title, validPath);
                
                // Update state
                _splitViewActive = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enabling split view: {ex.Message}");
                throw; // Let the calling method handle this
            }
        }

        /// <summary>
        /// Disable split view
        /// </summary>
        private void DisableSplitView()
        {
            try
            {
                // Remove tab managers from grid
                if (_rightTabManager is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _rightTabManager = null;
                
                // Restore single tab manager
                MainContent.Content = _tabManager;
                
                // Update state
                _splitViewActive = false;
                _activeTabManager = _tabManager;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disabling split view: {ex.Message}");
                throw; // Let the calling method handle this
            }
        }

        /// <summary>
        /// Determine the path to use for right pane in split view
        /// </summary>
        /// <returns>The path to use</returns>
        private string DetermineSplitViewPath()
        {
            string defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            
            try
            {
                var fileTree = FindFileTree();
                
                if (fileTree != null)
                {
                    // Try current selected folder
                    string? selectedPath = fileTree.GetSelectedFolderPath();
                    if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
                    {
                        return selectedPath;
                    }
                    
                    // Fall back to root path
                    string? rootPath = fileTree.GetCurrentPath(); // Changed from CurrentPath property to GetCurrentPath method
                    if (!string.IsNullOrEmpty(rootPath) && Directory.Exists(rootPath))
                    {
                        return rootPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error determining split view path: {ex.Message}");
            }
            
            return defaultPath;
        }

        #endregion

        #region Console Management

        /// <summary>
        /// Handle drag enter on dock area
        /// </summary>
        private void DockArea_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                HandleDockAreaDrag(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling drag enter: {ex.Message}");
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle drag over on dock area
        /// </summary>
        private void DockArea_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                HandleDockAreaDrag(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling drag over: {ex.Message}");
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle drag leave on dock area
        /// </summary>
        private void DockArea_DragLeave(object sender, DragEventArgs e)
        {
            try
            {
                DropIndicator.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling drag leave: {ex.Message}");
            }
            e.Handled = true;
        }

        /// <summary>
        /// Handle drop on dock area
        /// </summary>
        private void DockArea_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (DropIndicator.Visibility == Visibility.Visible)
                {
                    DropIndicator.Visibility = Visibility.Collapsed;
                    
                    // Animate console showing
                    AnimateConsoleShowing();
                    
                    // Accept drop action
                    e.Handled = true;
                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // Forward file drops to file tree
                    var fileTree = FindFileTree();
                    if (fileTree != null)
                    {
                        fileTree.HandleFileDrop(e.Data);
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling drop: {ex.Message}");
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle drag operations in dock area
        /// </summary>
        /// <param name="e">Drag event args</param>
        private void HandleDockAreaDrag(DragEventArgs e)
        {
            try
            {
                // Check if this is a dock panel or file drop
                bool isDockPanel = e.Data.GetDataPresent("application/x-dockpanel");
                bool isFileDrop = e.Data.GetDataPresent(DataFormats.FileDrop);
                
                if (isDockPanel || isFileDrop)
                {
                    // Accept the drag
                    e.Effects = DragDropEffects.Move;
                    
                    // Check if we're in the bottom zone for console
                    Point pos = e.GetPosition(DockArea);
                    double bottomZoneHeight = DockArea.ActualHeight * 0.70;
                    Rect bottomZone = new Rect(
                        0,
                        DockArea.ActualHeight - bottomZoneHeight,
                        DockArea.ActualWidth,
                        bottomZoneHeight
                    );
                    
                    if (isDockPanel && bottomZone.Contains(pos))
                    {
                        // Show drop indicator if console not visible
                        if (ConsoleArea != null && ConsoleArea.Visibility != Visibility.Visible)
                        {
                            DropIndicator.Visibility = Visibility.Visible;
                            Canvas.SetLeft(DropIndicator, 20);
                            Canvas.SetTop(DropIndicator, bottomZone.Y + 20);
                            DropIndicator.Width = bottomZone.Width - 40;
                            DropIndicator.Height = bottomZone.Height - 40;
                        }
                    }
                    else
                    {
                        DropIndicator.Visibility = Visibility.Collapsed;
                    }
                    
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling dock area drag: {ex.Message}");
                e.Handled = true;
            }
        }

        /// <summary>
        /// Animate console area showing
        /// </summary>
        private void AnimateConsoleShowing()
        {
            try
            {
                if (ConsoleArea == null) return;
                
                // Show console
                ConsoleArea.Visibility = Visibility.Visible;
                ConsoleArea.Height = 0;
                
                // Start animation timer
                _consoleAnimationActive = true;
                _consoleAnimationTimer?.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error animating console showing: {ex.Message}");
                // Ensure console is visible even if animation fails
                if (ConsoleArea != null)
                {
                    ConsoleArea.Visibility = Visibility.Visible;
                    ConsoleArea.Height = 200; // Default height
                }
            }
        }

        /// <summary>
        /// Animate console area hiding
        /// </summary>
        private void AnimateConsoleHiding()
        {
            try
            {
                // Start animation timer for hiding
                _consoleAnimationActive = false;
                _consoleAnimationTimer?.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error animating console hiding: {ex.Message}");
                // Hide console immediately if animation fails
                if (ConsoleArea != null)
                {
                    ConsoleArea.Visibility = Visibility.Collapsed;
                    ConsoleArea.Height = 0;
                }
            }
        }

        /// <summary>
        /// Handle console animation timer tick
        /// </summary>
        private void ConsoleAnimationTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (ConsoleArea == null) return;
                
                const double TARGET_HEIGHT = 200;
                const double ANIMATION_STEP = 20;
                
                if (_consoleAnimationActive)
                {
                    // Showing animation
                    if (ConsoleArea.Height < TARGET_HEIGHT)
                    {
                        ConsoleArea.Height += ANIMATION_STEP;
                        if (ConsoleArea.Height >= TARGET_HEIGHT)
                        {
                            ConsoleArea.Height = TARGET_HEIGHT;
                            _consoleAnimationTimer?.Stop();
                        }
                    }
                    else
                    {
                        _consoleAnimationTimer?.Stop();
                    }
                }
                else
                {
                    // Hiding animation
                    if (ConsoleArea.Height > 0)
                    {
                        ConsoleArea.Height -= ANIMATION_STEP;
                        if (ConsoleArea.Height <= 0)
                        {
                            ConsoleArea.Height = 0;
                            ConsoleArea.Visibility = Visibility.Collapsed;
                            _consoleAnimationTimer?.Stop();
                        }
                    }
                    else
                    {
                        ConsoleArea.Visibility = Visibility.Collapsed;
                        _consoleAnimationTimer?.Stop();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in console animation timer: {ex.Message}");
                _consoleAnimationTimer?.Stop();
                
                // Ensure console is in a valid state
                if (ConsoleArea != null)
                {
                    if (_consoleAnimationActive)
                    {
                        ConsoleArea.Visibility = Visibility.Visible;
                        ConsoleArea.Height = 200;
                    }
                    else
                    {
                        ConsoleArea.Visibility = Visibility.Collapsed;
                        ConsoleArea.Height = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Update tracking of which panels are in the console area
        /// </summary>
        private void UpdateConsolePanelTracking()
        {
            try
            {
                if (ConsoleArea == null || ConsoleArea.Visibility != Visibility.Visible)
                {
                    return;
                }

                // TODO: Implement tracking of panels in console area
                // This would be more complex in WPF than in Qt
                // as we'd need a way to detect panels added to the console
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating console panel tracking: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if console is empty and hide if so
        /// </summary>
        private void CheckConsoleEmpty()
        {
            try
            {
                UpdateConsolePanelTracking();
                
                if (_panelsInConsole.Count == 0 && ConsoleArea != null && ConsoleArea.Visibility == Visibility.Visible)
                {
                    AnimateConsoleHiding();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking console empty: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Stop timers
                if (_consoleAnimationTimer != null)
                {
                    _consoleAnimationTimer.Stop();
                    _consoleAnimationTimer = null;
                }
                
                // Remove from tracking
                _allContainers.Remove(this);
                
                // Dispose tab managers
                if (_tabManager is IDisposable tabManagerDisposable)
                {
                    tabManagerDisposable.Dispose();
                }
                
                if (_rightTabManager is IDisposable rightTabManagerDisposable)
                {
                    rightTabManagerDisposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Dispose: {ex.Message}");
                // Continue with disposal even if some parts fail
            }
        }

        #endregion
    }
}