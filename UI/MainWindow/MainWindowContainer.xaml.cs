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
using ExplorerPro.Themes;

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
        
        // Constants for optimal user experience
        private const double DEFAULT_SIDEBAR_WIDTH = 250;    // Comfortable default width
        private const double MIN_SIDEBAR_WIDTH = 200;        // Minimum usable width  
        private const double COLLAPSED_PANEL_WIDTH = 3;      // Visual separator when collapsed
        
        // Panel proportional sizing constants
        private const double DEFAULT_PANEL_RATIO = 0.6;      // Default panel ratio within sidebar
        private const double MIN_PANEL_HEIGHT = 120;         // Minimum panel height for usability

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
                
                // Initialize panel drag handlers for future docking functionality
                InitializePanelDragHandlers();

                // Apply saved panel visibility settings
                ApplySavedPanelVisibility(null);
                
                // Initialize panel layouts after visibility is set
                // Use a dispatcher to ensure UI is fully loaded before layout updates
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    UpdatePanelLayout(SidebarLocation.Left);
                    UpdatePanelLayout(SidebarLocation.Right);
                }));
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
        /// Note: Panel edge resize handlers removed to avoid conflicts with hierarchical panel system.
        /// Individual panels no longer have independent collapse/expand behavior.
        /// Use sidebar toggles and panel visibility toggles instead.
        /// </summary>
        private void InstallPanelResizeHandlers()
        {
            // Removed: Individual panel edge-click collapse functionality
            // This was creating conflicts with the hierarchical sidebar system
            // where panels could be collapsed at both sidebar and individual level
        }

        /// <summary>
        /// Removed: Individual panel edge-click collapse functionality.
        /// This method is kept for compatibility but no longer attaches conflicting handlers.
        /// Use the hierarchical sidebar system instead: sidebar toggles control sections,
        /// individual panel toggles control visibility within sections.
        /// </summary>
        /// <param name="border">Border (no longer used)</param>
        private void AttachPanelResizeHandlers(Border border)
        {
            // Intentionally empty - removing conflicting individual panel collapse behavior
            // The hierarchical system (sidebar toggles + panel visibility toggles) provides
            // a cleaner, more predictable user experience
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
                        case "left_sidebar_visible":
                            if (LeftColumnContainer != null)
                            {
                                LeftColumnContainer.Visibility = setting.Value ? Visibility.Visible : Visibility.Collapsed;
                                var leftSplitter = FindName("LeftSplitter") as GridSplitter;
                                var leftColumn = FindName("LeftColumn") as ColumnDefinition;
                                if (leftSplitter != null) leftSplitter.Visibility = LeftColumnContainer.Visibility;
                                if (leftColumn != null)
                                {
                                    if (setting.Value)
                                    {
                                        var savedWidth = _settingsManager.GetSetting<double>("dockable_panels.left_sidebar_width");
                                        leftColumn.Width = new GridLength(savedWidth > 0 ? savedWidth : 200);
                                        leftColumn.MinWidth = 150;
                                    }
                                    else
                                    {
                                        leftColumn.Width = new GridLength(0);
                                        leftColumn.MinWidth = 0;
                                    }
                                }
                            }
                            break;
                        case "right_sidebar_visible":
                            if (RightColumnContainer != null)
                            {
                                RightColumnContainer.Visibility = setting.Value ? Visibility.Visible : Visibility.Collapsed;
                                var rightSplitter = FindName("RightSplitter") as GridSplitter;
                                var rightColumn = FindName("RightColumn") as ColumnDefinition;
                                if (rightSplitter != null) rightSplitter.Visibility = RightColumnContainer.Visibility;
                                if (rightColumn != null)
                                {
                                    if (setting.Value)
                                    {
                                        var savedWidth = _settingsManager.GetSetting<double>("dockable_panels.right_sidebar_width");
                                        rightColumn.Width = new GridLength(savedWidth > 0 ? savedWidth : 200);
                                        rightColumn.MinWidth = 150;
                                    }
                                    else
                                    {
                                        rightColumn.Width = new GridLength(0);
                                        rightColumn.MinWidth = 0;
                                    }
                                }
                            }
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
        /// Toggle pinned panel visibility (respects sidebar state)
        /// </summary>
        public void TogglePinnedPanel()
        {
            TogglePanelVisibility(PinnedPanelContainer, "pinned_panel", SidebarLocation.Left);
        }

        /// <summary>
        /// Toggle bookmarks panel visibility (respects sidebar state)
        /// </summary>
        public void ToggleBookmarksPanel()
        {
            TogglePanelVisibility(BookmarksPanelContainer, "bookmarks_panel", SidebarLocation.Right);
        }

        /// <summary>
        /// Toggle to-do panel visibility (respects sidebar state)
        /// </summary>
        public void ToggleTodoPanel()
        {
            TogglePanelVisibility(ToDoPanelContainer, "to_do_panel", SidebarLocation.Right);
        }

        /// <summary>
        /// Toggle Procore links panel visibility (respects sidebar state)
        /// </summary>
        public void ToggleProcorePanel()
        {
            TogglePanelVisibility(ProcorePanelContainer, "procore_panel", SidebarLocation.Right);
        }

        /// <summary>
        /// Helper enum to identify sidebar locations
        /// </summary>
        public enum SidebarLocation
        {
            Left,
            Right
        }

        /// <summary>
        /// Check if a sidebar is currently visible and functional
        /// </summary>
        /// <param name="location">Which sidebar to check</param>
        /// <returns>True if sidebar is visible and functional</returns>
        private bool IsSidebarVisible(SidebarLocation location)
        {
            return location switch
            {
                SidebarLocation.Left => LeftColumnContainer?.Visibility == Visibility.Visible,
                SidebarLocation.Right => RightColumnContainer?.Visibility == Visibility.Visible,
                _ => false
            };
        }

        /// <summary>
        /// Get all panels within a specific sidebar
        /// </summary>
        /// <param name="location">Sidebar location</param>
        /// <returns>List of panel containers in that sidebar</returns>
        private List<Border> GetPanelsInSidebar(SidebarLocation location)
        {
            return location switch
            {
                SidebarLocation.Left => new List<Border> { PinnedPanelContainer }.Where(p => p != null).ToList(),
                SidebarLocation.Right => new List<Border> { ToDoPanelContainer, ProcorePanelContainer, BookmarksPanelContainer }.Where(p => p != null).ToList(),
                _ => new List<Border>()
            };
        }

        /// <summary>
        /// Toggle the entire left sidebar visibility (like VS Code)
        /// </summary>
        public void ToggleLeftSidebar()
        {
            try
            {
                if (LeftColumnContainer == null) return;
                
                // Find the left splitter and column definition
                var leftSplitter = FindName("LeftSplitter") as GridSplitter;
                var leftColumn = FindName("LeftColumn") as ColumnDefinition;
                
                if (LeftColumnContainer.Visibility == Visibility.Visible)
                {
                    // Store current width before collapsing
                    if (leftColumn != null && leftColumn.Width.Value > 0)
                    {
                        _settingsManager.UpdateSetting("dockable_panels.left_sidebar_width", leftColumn.Width.Value);
                    }
                    
                    // Store panel states before collapsing sidebar
                    StorePanelStatesForSidebar(SidebarLocation.Left);
                    
                    // Animate collapse
                    AnimateSidebarCollapse(LeftColumnContainer, () =>
                    {
                        LeftColumnContainer.Visibility = Visibility.Collapsed;
                        if (leftSplitter != null) leftSplitter.Visibility = Visibility.Collapsed;
                        if (leftColumn != null)
                        {
                            leftColumn.Width = new GridLength(0);
                            leftColumn.MinWidth = 0;
                        }
                        _settingsManager.UpdateSetting("dockable_panels.left_sidebar_visible", false);
                    });
                }
                else
                {
                    // Restore column width to comfortable default or saved preference
                    if (leftColumn != null)
                    {
                        var savedWidth = _settingsManager.GetSetting<double>("dockable_panels.left_sidebar_width");
                        var targetWidth = savedWidth > MIN_SIDEBAR_WIDTH ? savedWidth : DEFAULT_SIDEBAR_WIDTH;
                        leftColumn.Width = new GridLength(targetWidth);
                        leftColumn.MinWidth = MIN_SIDEBAR_WIDTH;
                    }
                    
                    // Show and animate expand
                    LeftColumnContainer.Visibility = Visibility.Visible;
                    if (leftSplitter != null) leftSplitter.Visibility = Visibility.Visible;
                    AnimateSidebarExpand(LeftColumnContainer);
                    _settingsManager.UpdateSetting("dockable_panels.left_sidebar_visible", true);
                    
                    // Restore panel states after expanding sidebar
                    RestorePanelStatesForSidebar(SidebarLocation.Left);
                    
                    // Update panel layout for dynamic height allocation
                    UpdatePanelLayout(SidebarLocation.Left);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling left sidebar: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle the entire right sidebar visibility (like VS Code)
        /// </summary>
        public void ToggleRightSidebar()
        {
            try
            {
                if (RightColumnContainer == null) return;
                
                // Find the right splitter and column definition
                var rightSplitter = FindName("RightSplitter") as GridSplitter;
                var rightColumn = FindName("RightColumn") as ColumnDefinition;
                
                if (RightColumnContainer.Visibility == Visibility.Visible)
                {
                    // Store current width before collapsing
                    if (rightColumn != null && rightColumn.Width.Value > 0)
                    {
                        _settingsManager.UpdateSetting("dockable_panels.right_sidebar_width", rightColumn.Width.Value);
                    }
                    
                    // Store panel states before collapsing sidebar
                    StorePanelStatesForSidebar(SidebarLocation.Right);
                    
                    // Animate collapse
                    AnimateSidebarCollapse(RightColumnContainer, () =>
                    {
                        RightColumnContainer.Visibility = Visibility.Collapsed;
                        if (rightSplitter != null) rightSplitter.Visibility = Visibility.Collapsed;
                        if (rightColumn != null)
                        {
                            rightColumn.Width = new GridLength(0);
                            rightColumn.MinWidth = 0;
                        }
                        _settingsManager.UpdateSetting("dockable_panels.right_sidebar_visible", false);
                    });
                }
                else
                {
                    // Restore column width to comfortable default or saved preference
                    if (rightColumn != null)
                    {
                        var savedWidth = _settingsManager.GetSetting<double>("dockable_panels.right_sidebar_width");
                        var targetWidth = savedWidth > MIN_SIDEBAR_WIDTH ? savedWidth : DEFAULT_SIDEBAR_WIDTH;
                        rightColumn.Width = new GridLength(targetWidth);
                        rightColumn.MinWidth = MIN_SIDEBAR_WIDTH;
                    }
                    
                    // Show and animate expand
                    RightColumnContainer.Visibility = Visibility.Visible;
                    if (rightSplitter != null) rightSplitter.Visibility = Visibility.Visible;
                    AnimateSidebarExpand(RightColumnContainer);
                    _settingsManager.UpdateSetting("dockable_panels.right_sidebar_visible", true);
                    
                    // Restore panel states after expanding sidebar
                    RestorePanelStatesForSidebar(SidebarLocation.Right);
                    
                    // Update panel layout for dynamic height allocation
                    UpdatePanelLayout(SidebarLocation.Right);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling right sidebar: {ex.Message}");
            }
        }

        /// <summary>
        /// Store panel states before collapsing a sidebar
        /// </summary>
        /// <param name="location">Sidebar location</param>
        private void StorePanelStatesForSidebar(SidebarLocation location)
        {
            try
            {
                var panels = GetPanelsInSidebar(location);
                var prefix = location == SidebarLocation.Left ? "left_" : "right_";
                
                foreach (var panel in panels)
                {
                    if (panel != null)
                    {
                        var panelKey = GetPanelSettingName(panel);
                        if (!string.IsNullOrEmpty(panelKey))
                        {
                            var stateKey = $"{prefix}panel_state_{panelKey}";
                            _settingsManager.UpdateSetting(stateKey, panel.Visibility == Visibility.Visible);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing panel states: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore panel states after expanding a sidebar
        /// </summary>
        /// <param name="location">Sidebar location</param>
        private void RestorePanelStatesForSidebar(SidebarLocation location)
        {
            try
            {
                var panels = GetPanelsInSidebar(location);
                var prefix = location == SidebarLocation.Left ? "left_" : "right_";
                
                foreach (var panel in panels)
                {
                    if (panel != null)
                    {
                        var panelKey = GetPanelSettingName(panel);
                        if (!string.IsNullOrEmpty(panelKey))
                        {
                            var stateKey = $"{prefix}panel_state_{panelKey}";
                            var wasVisible = _settingsManager.GetSetting<bool>(stateKey, true); // Default to visible
                            
                            panel.Visibility = wasVisible ? Visibility.Visible : Visibility.Collapsed;
                            if (wasVisible)
                            {
                                // Panel width is now handled by Grid column definitions
                                // No individual panel width restoration needed
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring panel states: {ex.Message}");
            }
        }

        /// <summary>
        /// Get setting name for a panel container
        /// </summary>
        /// <param name="panel">Panel container</param>
        /// <returns>Setting name or null if not found</returns>
        private string? GetPanelSettingName(Border panel)
        {
            if (panel == PinnedPanelContainer) return "pinned_panel";
            if (panel == BookmarksPanelContainer) return "bookmarks_panel";
            if (panel == ToDoPanelContainer) return "to_do_panel";
            if (panel == ProcorePanelContainer) return "procore_panel";
            return null;
        }

        /// <summary>
        /// Toggle a panel's visibility (respects parent sidebar state)
        /// </summary>
        /// <param name="container">Panel container</param>
        /// <param name="settingName">Setting name for persistence</param>
        /// <param name="location">Sidebar location</param>
        private void TogglePanelVisibility(Border container, string settingName, SidebarLocation location)
        {
            try
            {
                if (container == null) return;
                
                // Check if parent sidebar is visible
                if (!IsSidebarVisible(location))
                {
                    // If sidebar is collapsed, show it first, then show the panel
                    ShowSidebarAndPanel(location, container, settingName);
                    return;
                }
                
                // Sidebar is visible, toggle individual panel
                if (container.Visibility == Visibility.Visible)
                {
                    // Width is handled by Grid column definitions, no storage needed
                    
                    // Hide panel
                    container.Visibility = Visibility.Collapsed;
                    
                    // Update settings
                    _settingsManager.UpdateSetting($"dockable_panels.{settingName}", false);
                    
                    // Check if this was the last visible panel in sidebar
                    CheckAndCollapseSidebarIfEmpty(location);
                }
                else
                {
                    // Show panel
                    container.Visibility = Visibility.Visible;
                    
                    // Width is handled by Grid column definitions, no restoration needed
                    
                    // Update settings
                    _settingsManager.UpdateSetting($"dockable_panels.{settingName}", true);
                }
                
                // Update panel layout for dynamic height allocation
                UpdatePanelLayout(location);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling panel visibility: {ex.Message}");
            }
        }

        /// <summary>
        /// Show sidebar and activate specific panel
        /// </summary>
        /// <param name="location">Sidebar location</param>
        /// <param name="targetPanel">Panel to show after sidebar is expanded</param>
        /// <param name="settingName">Panel setting name</param>
        private void ShowSidebarAndPanel(SidebarLocation location, Border targetPanel, string settingName)
        {
            try
            {
                // First show the sidebar
                if (location == SidebarLocation.Left)
                {
                    ToggleLeftSidebar();
                }
                else
                {
                    ToggleRightSidebar();
                }
                
                // Then ensure the target panel is visible
                if (targetPanel != null)
                {
                    targetPanel.Visibility = Visibility.Visible;
                    // Width is handled by Grid column definitions, no restoration needed
                    _settingsManager.UpdateSetting($"dockable_panels.{settingName}", true);
                    
                    // Update panel layout for dynamic height allocation
                    UpdatePanelLayout(location);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing sidebar and panel: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if sidebar should be collapsed when all panels are hidden
        /// </summary>
        /// <param name="location">Sidebar location to check</param>
        private void CheckAndCollapseSidebarIfEmpty(SidebarLocation location)
        {
            try
            {
                var panels = GetPanelsInSidebar(location);
                bool anyPanelVisible = panels.Any(p => p?.Visibility == Visibility.Visible);
                
                if (!anyPanelVisible)
                {
                    // All panels are hidden, optionally collapse sidebar
                    // This is a UX decision - you might want to keep sidebar open for easy re-access
                    // For now, let's keep sidebar open but notify user
                    Console.WriteLine($"{location} sidebar has no visible panels but will remain open for easy access");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking sidebar state: {ex.Message}");
            }
        }

        /// <summary>
        /// Removed: Individual panel collapse functionality.
        /// This created conflicts with the hierarchical sidebar system where panels
        /// could be collapsed at both sidebar and individual level, causing confusion.
        /// Use sidebar toggles and panel visibility toggles instead.
        /// </summary>
        /// <param name="container">Panel container (no longer used)</param>
        private void TogglePanelCollapsedState(Border container)
        {
            // Removed: Individual panel collapse behavior
            // The hierarchical system provides better UX:
            // - Sidebar toggles control entire sidebar visibility  
            // - Panel toggles control individual panel visibility within sidebars
            // - No conflicting edge-click collapse behavior
        }

        #region Panel Architecture - Proportional & Dockable System
        
        /// <summary>
        /// Enhanced panel system with proportional sizing, dynamic height allocation, and future docking support.
        /// Panels maintain hierarchical relationship with sidebars while supporting individual sizing.
        /// Features:
        /// - Dynamic height: Single visible panel expands to full sidebar height
        /// - Individual width preferences within sidebars (future: resizable panel widths)
        /// - Architecture supports future drag-out docking functionality
        /// </summary>
        
        /// <summary>
        /// Panel information for managing proportional sizing, width preferences, and docking
        /// </summary>
        public class PanelInfo
        {
            public string Name { get; set; }
            public Border Container { get; set; }
            public Border Header { get; set; }
            public SidebarLocation Location { get; set; }
            public double PreferredRatio { get; set; } = DEFAULT_PANEL_RATIO;
            public double PreferredWidthRatio { get; set; } = 1.0; // For future width resizing within sidebars
            public bool IsDockable { get; set; } = true;
            public bool IsFloating { get; set; } = false;
        }
        
        /// <summary>
        /// Get panel proportional height within its sidebar
        /// </summary>
        /// <param name="panelName">Panel name</param>
        /// <returns>Proportional height ratio</returns>
        private double GetPanelRatio(string panelName)
        {
            try
            {
                var savedRatio = _settingsManager.GetSetting<double>($"panel_ratios.{panelName}");
                return savedRatio > 0 ? savedRatio : DEFAULT_PANEL_RATIO;
            }
            catch
            {
                return DEFAULT_PANEL_RATIO;
            }
        }
        
        /// <summary>
        /// Store panel proportional height for future restoration
        /// </summary>
        /// <param name="panelName">Panel name</param>
        /// <param name="ratio">Proportional height ratio</param>
        private void StorePanelRatio(string panelName, double ratio)
        {
            try
            {
                _settingsManager.UpdateSetting($"panel_ratios.{panelName}", ratio);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing panel ratio for {panelName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get all panels within a sidebar with their information
        /// </summary>
        /// <param name="location">Sidebar location</param>
        /// <returns>List of panel information</returns>
        private List<PanelInfo> GetPanelInfoForSidebar(SidebarLocation location)
        {
            var panels = new List<PanelInfo>();
            
            try
            {
                if (location == SidebarLocation.Left)
                {
                    panels.Add(new PanelInfo 
                    { 
                        Name = "pinned", 
                        Container = PinnedPanelContainer, 
                        Header = PinnedPanelHeader,
                        Location = SidebarLocation.Left 
                    });
                    panels.Add(new PanelInfo 
                    { 
                        Name = "bookmarks", 
                        Container = BookmarksPanelContainer, 
                        Header = BookmarksPanelHeader,
                        Location = SidebarLocation.Left 
                    });
                }
                else if (location == SidebarLocation.Right)
                {
                    panels.Add(new PanelInfo 
                    { 
                        Name = "todo", 
                        Container = ToDoPanelContainer, 
                        Header = ToDoPanelHeader,
                        Location = SidebarLocation.Right 
                    });
                    panels.Add(new PanelInfo 
                    { 
                        Name = "procore", 
                        Container = ProcorePanelContainer, 
                        Header = ProcorePanelHeader,
                        Location = SidebarLocation.Right 
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting panel info for {location}: {ex.Message}");
            }
            
            return panels;
        }
        
        /// <summary>
        /// Initialize panel drag handlers for future docking functionality
        /// </summary>
        private void InitializePanelDragHandlers()
        {
            try
            {
                // Get all panels and set up drag preparation
                var allPanels = GetPanelInfoForSidebar(SidebarLocation.Left)
                    .Concat(GetPanelInfoForSidebar(SidebarLocation.Right));
                
                foreach (var panel in allPanels)
                {
                    if (panel.Header != null && panel.IsDockable)
                    {
                        // Add visual feedback for drag capability
                        panel.Header.MouseEnter += (s, e) =>
                        {
                            panel.Header.Background = new SolidColorBrush(Color.FromArgb(40, 9, 105, 218)); // Light blue overlay
                        };
                        
                        panel.Header.MouseLeave += (s, e) =>
                        {
                            // Restore original background based on panel type
                            var originalBrush = panel.Name switch
                            {
                                "pinned" => new SolidColorBrush(Color.FromRgb(227, 242, 253)), // #E3F2FD
                                "bookmarks" => new SolidColorBrush(Color.FromRgb(240, 249, 255)), // #F0F9FF
                                "todo" => new SolidColorBrush(Color.FromRgb(255, 243, 205)), // #FFF3CD
                                "procore" => new SolidColorBrush(Color.FromRgb(232, 245, 232)), // #E8F5E8
                                _ => new SolidColorBrush(Color.FromRgb(246, 248, 250)) // Default #F6F8FA
                            };
                            panel.Header.Background = originalBrush;
                        };
                        
                        // Future: Add drag-and-drop handlers here
                        // panel.Header.MouseLeftButtonDown += StartPanelDrag;
                        // panel.Header.MouseLeftButtonUp += EndPanelDrag;  
                        // panel.Header.MouseMove += HandlePanelDrag;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing panel drag handlers: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update panel layout dynamically based on visible panels in a sidebar
        /// Single visible panel expands to full height, multiple panels use proportional sizing
        /// </summary>
        /// <param name="location">Sidebar location to update</param>
        private void UpdatePanelLayout(SidebarLocation location)
        {
            try
            {
                Grid sidebarGrid = null;
                List<PanelInfo> panels = null;
                GridSplitter splitter = null;
                
                if (location == SidebarLocation.Left)
                {
                    sidebarGrid = LeftColumnContainer.Child as Grid;
                    panels = GetPanelInfoForSidebar(SidebarLocation.Left);
                    splitter = LeftPanelSplitter;
                }
                else if (location == SidebarLocation.Right)
                {
                    sidebarGrid = RightColumnContainer.Child as Grid;
                    panels = GetPanelInfoForSidebar(SidebarLocation.Right);
                    // Right sidebar splitter - find it in the grid
                    splitter = sidebarGrid?.Children.OfType<GridSplitter>()
                        .FirstOrDefault(gs => Grid.GetRow(gs) == 1);
                }
                
                if (sidebarGrid == null || panels == null) 
                {
                    Console.WriteLine($"UpdatePanelLayout: Failed to get sidebar grid or panels for {location}");
                    return;
                }
                
                // Count visible panels
                var visiblePanels = panels.Where(p => p.Container.Visibility == Visibility.Visible).ToList();
                
                Console.WriteLine($"UpdatePanelLayout: {location} sidebar has {visiblePanels.Count} visible panels");
                foreach (var panel in visiblePanels)
                {
                    Console.WriteLine($"  - Visible panel: {panel.Name}");
                }
                
                if (visiblePanels.Count == 1)
                {
                    Console.WriteLine($"Setting single panel layout for {location}");
                    // Single panel - expand to full height
                    SetSinglePanelLayout(sidebarGrid, visiblePanels[0], splitter);
                }
                else if (visiblePanels.Count > 1)
                {
                    Console.WriteLine($"Setting multi panel layout for {location}");
                    // Multiple panels - use proportional sizing
                    SetMultiPanelLayout(sidebarGrid, visiblePanels, splitter);
                }
                else
                {
                    Console.WriteLine($"No visible panels in {location} sidebar");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating panel layout for {location}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Configure layout for single visible panel (full height)
        /// </summary>
        private void SetSinglePanelLayout(Grid sidebarGrid, PanelInfo visiblePanel, GridSplitter splitter)
        {
            try
            {
                Console.WriteLine($"SetSinglePanelLayout: Configuring layout for panel {visiblePanel.Name}");
                Console.WriteLine($"  - Grid has {sidebarGrid.RowDefinitions.Count} row definitions");
                
                // For sidebar with single panel, make it take full height
                if (sidebarGrid.RowDefinitions.Count >= 1)
                {
                    var visibleRowIndex = Grid.GetRow(visiblePanel.Container);
                    Console.WriteLine($"  - Visible panel is in row {visibleRowIndex}");
                    
                    // Clear all row definitions and recreate with single expanding row
                    sidebarGrid.RowDefinitions.Clear();
                    
                    // Add single row that expands to fill all space
                    sidebarGrid.RowDefinitions.Add(new RowDefinition 
                    { 
                        Height = new GridLength(1, GridUnitType.Star),
                        MinHeight = MIN_PANEL_HEIGHT 
                    });
                    
                    // Move the visible panel to row 0
                    Grid.SetRow(visiblePanel.Container, 0);
                    
                    // Ensure the panel fills full width by removing any margins/padding
                    visiblePanel.Container.Margin = new Thickness(0);
                    visiblePanel.Container.Padding = new Thickness(0);
                    
                    Console.WriteLine($"  - Grid recreated with single expanding row");
                    Console.WriteLine($"  - Visible panel moved to row 0");
                    
                    // Hide splitter
                    if (splitter != null)
                    {
                        splitter.Visibility = Visibility.Collapsed;
                        Console.WriteLine("  - Splitter hidden");
                    }
                    else
                    {
                        Console.WriteLine("  - No splitter found");
                    }
                    
                    // Force layout update
                    sidebarGrid.UpdateLayout();
                    Console.WriteLine("  - Layout update forced");
                }
                else
                {
                    Console.WriteLine($"  - Warning: Grid has no row definitions");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting single panel layout: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Configure layout for multiple visible panels (proportional sizing)
        /// </summary>
        private void SetMultiPanelLayout(Grid sidebarGrid, List<PanelInfo> visiblePanels, GridSplitter splitter)
        {
            try
            {
                Console.WriteLine($"SetMultiPanelLayout: Configuring layout for {visiblePanels.Count} panels");
                
                // Recreate the grid structure for multiple panels
                sidebarGrid.RowDefinitions.Clear();
                
                var firstPanel = visiblePanels.FirstOrDefault();
                if (firstPanel?.Location == SidebarLocation.Left)
                {
                    // Left sidebar: recreate with pinned and bookmarks structure
                    var pinnedRatio = GetPanelRatio("pinned");
                    var bookmarksRatio = GetPanelRatio("bookmarks");
                    
                    // Row 0: Pinned panel
                    sidebarGrid.RowDefinitions.Add(new RowDefinition 
                    { 
                        Height = new GridLength(pinnedRatio, GridUnitType.Star),
                        MinHeight = MIN_PANEL_HEIGHT 
                    });
                    
                    // Row 1: Splitter
                    sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    
                    // Row 2: Bookmarks panel
                    sidebarGrid.RowDefinitions.Add(new RowDefinition 
                    { 
                        Height = new GridLength(bookmarksRatio, GridUnitType.Star),
                        MinHeight = MIN_PANEL_HEIGHT 
                    });
                    
                    // Set panel positions
                    Grid.SetRow(PinnedPanelContainer, 0);
                    Grid.SetRow(BookmarksPanelContainer, 2);
                    if (splitter != null) Grid.SetRow(splitter, 1);
                    
                    Console.WriteLine($"  - Left sidebar: pinned({pinnedRatio}*), splitter(auto), bookmarks({bookmarksRatio}*)");
                }
                else if (firstPanel?.Location == SidebarLocation.Right)
                {
                    // Right sidebar: recreate with todo and procore structure
                    var todoRatio = GetPanelRatio("todo");
                    var procoreRatio = GetPanelRatio("procore");
                    
                    // Row 0: Todo panel
                    sidebarGrid.RowDefinitions.Add(new RowDefinition 
                    { 
                        Height = new GridLength(todoRatio, GridUnitType.Star),
                        MinHeight = MIN_PANEL_HEIGHT 
                    });
                    
                    // Row 1: Splitter
                    sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    
                    // Row 2: Procore panel
                    sidebarGrid.RowDefinitions.Add(new RowDefinition 
                    { 
                        Height = new GridLength(procoreRatio, GridUnitType.Star),
                        MinHeight = MIN_PANEL_HEIGHT 
                    });
                    
                    // Set panel positions
                    Grid.SetRow(ToDoPanelContainer, 0);
                    Grid.SetRow(ProcorePanelContainer, 2);
                    if (splitter != null) Grid.SetRow(splitter, 1);
                    
                    Console.WriteLine($"  - Right sidebar: todo({todoRatio}*), splitter(auto), procore({procoreRatio}*)");
                }
                
                // Show splitter
                if (splitter != null)
                {
                    splitter.Visibility = Visibility.Visible;
                    Console.WriteLine("  - Splitter shown");
                }
                
                // Force layout update
                sidebarGrid.UpdateLayout();
                Console.WriteLine("  - Layout update forced");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting multi panel layout: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        #endregion

        /// <summary>
        /// Animate sidebar collapse with smooth transition
        /// </summary>
        /// <param name="sidebar">Sidebar element to animate</param>
        /// <param name="onComplete">Action to execute when animation completes</param>
        private void AnimateSidebarCollapse(FrameworkElement sidebar, Action onComplete)
        {
            try
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = sidebar.ActualWidth,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
                };

                animation.Completed += (s, e) => onComplete?.Invoke();
                sidebar.BeginAnimation(FrameworkElement.WidthProperty, animation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error animating sidebar collapse: {ex.Message}");
                onComplete?.Invoke(); // Fallback to immediate action
            }
        }

        /// <summary>
        /// Animate sidebar expand with smooth transition to comfortable width
        /// </summary>
        /// <param name="sidebar">Sidebar element to animate</param>
        private void AnimateSidebarExpand(FrameworkElement sidebar)
        {
            try
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0,
                    To = DEFAULT_SIDEBAR_WIDTH, // Use comfortable default width
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
                };

                sidebar.BeginAnimation(FrameworkElement.WidthProperty, animation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error animating sidebar expand: {ex.Message}");
                sidebar.Width = DEFAULT_SIDEBAR_WIDTH; // Fallback to comfortable width
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

        #region Theme Management

        /// <summary>
        /// Refreshes UI elements based on the current theme
        /// </summary>
        public void RefreshThemeElements()
        {
            try
            {
                // Get current theme information
                bool isDarkMode = ThemeManager.Instance.IsDarkMode;
                
                // Update container-level UI elements
                MainGrid.Background = GetResource<SolidColorBrush>("WindowBackground");
                
                // Update dock area
                if (DockArea != null)
                {
                    DockArea.Background = GetResource<SolidColorBrush>("BackgroundColor");
                }
                
                // Update panel containers
                RefreshPanelContainer(LeftColumnContainer);
                RefreshPanelContainer(RightColumnContainer);
                RefreshPanelContainer(PinnedPanelContainer);
                RefreshPanelContainer(ToDoPanelContainer);
                RefreshPanelContainer(ProcorePanelContainer);
                RefreshPanelContainer(BookmarksPanelContainer);
                
                // Update console area
                if (ConsoleArea != null)
                {
                    ConsoleArea.Background = GetResource<SolidColorBrush>("BackgroundColor");
                    ConsoleArea.BorderBrush = GetResource<SolidColorBrush>("BorderColor");
                }
                
                // Drop indicator
                if (DropIndicator != null)
                {
                    DropIndicator.Background = GetResource<SolidColorBrush>(isDarkMode ? 
                        "DropIndicatorBackgroundDark" : "DropIndicatorBackgroundLight");
                    DropIndicator.BorderBrush = GetResource<SolidColorBrush>("BorderColor");
                }
                
                // Update all content controls
                UpdateContentControls();
                
                // Update tab managers
                RefreshTabManagers();
                
                // Update file tree
                var fileTree = FindFileTree();
                if (fileTree != null)
                {
                    fileTree.RefreshThemeElements();
                }
                
                Console.WriteLine("MainWindowContainer theme elements refreshed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing container theme elements: {ex.Message}");
                // Non-critical error, continue
            }
        }
        
        /// <summary>
        /// Updates all content controls with theme-appropriate colors
        /// </summary>
        private void UpdateContentControls()
        {
            try
            {
                // Update all Content controls in this container
                if (MainContent != null)
                {
                    MainContent.Background = GetResource<SolidColorBrush>("WindowBackground");
                }
                
                if (PinnedPanelContent != null)
                {
                    PinnedPanelContent.Background = GetResource<SolidColorBrush>("BackgroundColor");
                }
                
                if (BookmarksPanelContent != null)
                {
                    BookmarksPanelContent.Background = GetResource<SolidColorBrush>("BackgroundColor");
                }
                
                if (ToDoPanelContent != null)
                {
                    ToDoPanelContent.Background = GetResource<SolidColorBrush>("BackgroundColor");
                }
                
                if (ProcorePanelContent != null)
                {
                    ProcorePanelContent.Background = GetResource<SolidColorBrush>("BackgroundColor");
                }
                
                if (ConsoleContent != null)
                {
                    ConsoleContent.Background = GetResource<SolidColorBrush>("BackgroundColor");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating content controls: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Refreshes a panel container with theme-appropriate styling
        /// </summary>
        private void RefreshPanelContainer(Border container)
        {
            if (container == null) return;
            
            try
            {
                container.Background = GetResource<SolidColorBrush>("BackgroundColor");
                container.BorderBrush = GetResource<SolidColorBrush>("BorderColor");
                
                // Update panel headers inside containers
                foreach (var textBlock in FindVisualChildren<TextBlock>(container))
                {
                    if (textBlock.Parent is DockPanel && textBlock.Text.Contains("Panel"))
                    {
                        // This looks like a panel header
                        textBlock.Background = GetResource<SolidColorBrush>("GroupBoxHeaderBackground");
                        textBlock.Foreground = GetResource<SolidColorBrush>("TextColor");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing panel container: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Refreshes tab managers in the split view
        /// </summary>
        private void RefreshTabManagers()
        {
            try
            {
                // Refresh main tab manager
                if (_tabManager != null)
                {
                    // The tab manager might need its own RefreshThemeElements method
                    // For now, we'll update the TabControl background directly
                    var tabControl = FindTabControl(_tabManager);
                    if (tabControl != null)
                    {
                        tabControl.Background = GetResource<SolidColorBrush>("TabControlBackground");
                    }
                }
                
                // Refresh right tab manager if in split view
                if (_rightTabManager != null && _splitViewActive)
                {
                    var tabControl = FindTabControl(_rightTabManager);
                    if (tabControl != null)
                    {
                        tabControl.Background = GetResource<SolidColorBrush>("TabControlBackground");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing tab managers: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Helper to find TabControl within a TabManager
        /// </summary>
        private TabControl FindTabControl(object tabManager)
        {
            try
            {
                if (tabManager is DependencyObject dependencyObject)
                {
                    return FindVisualChildren<TabControl>(dependencyObject).FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding TabControl: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Helper method to get a resource from the current theme
        /// </summary>
        private T GetResource<T>(string resourceKey) where T : class
        {
            return ThemeManager.Instance.GetResource<T>(resourceKey);
        }
        
        /// <summary>
        /// Finds all visual children of the specified type
        /// </summary>
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t)
                {
                    yield return t;
                }
                
                foreach (var grandChild in FindVisualChildren<T>(child))
                {
                    yield return grandChild;
                }
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

        /// <summary>
        /// Check if a specific panel can be toggled (its sidebar is visible)
        /// </summary>
        /// <param name="panelName">Panel name ("pinned", "bookmarks", "todo", "procore")</param>
        /// <returns>True if panel can be toggled</returns>
        public bool IsPanelToggleable(string panelName)
        {
            return panelName.ToLower() switch
            {
                "pinned" => IsSidebarVisible(SidebarLocation.Left),
                "bookmarks" or "todo" or "procore" => IsSidebarVisible(SidebarLocation.Right),
                _ => false
            };
        }

        /// <summary>
        /// Get the current panel visibility status with context
        /// </summary>
        /// <param name="panelName">Panel name</param>
        /// <returns>Panel status information</returns>
        public PanelStatus GetPanelStatus(string panelName)
        {
            var location = panelName.ToLower() switch
            {
                "pinned" => SidebarLocation.Left,
                "bookmarks" or "todo" or "procore" => SidebarLocation.Right,
                _ => (SidebarLocation?)null
            };

            if (location == null)
                return new PanelStatus { IsAvailable = false, Message = "Unknown panel" };

            var sidebarVisible = IsSidebarVisible(location.Value);
            
            if (!sidebarVisible)
            {
                return new PanelStatus 
                { 
                    IsAvailable = false, 
                    Message = $"{location.Value} sidebar is collapsed. Click to expand and show panel.",
                    CanExpand = true
                };
            }

            var panel = GetPanelByName(panelName);
            if (panel == null)
                return new PanelStatus { IsAvailable = false, Message = "Panel not found" };

            return new PanelStatus
            {
                IsAvailable = true,
                IsVisible = panel.Visibility == Visibility.Visible,
                Message = panel.Visibility == Visibility.Visible ? "Panel is visible" : "Panel is hidden",
                CanExpand = false
            };
        }

        /// <summary>
        /// Get panel container by name
        /// </summary>
        /// <param name="panelName">Panel name</param>
        /// <returns>Panel container or null</returns>
        private Border? GetPanelByName(string panelName)
        {
            return panelName.ToLower() switch
            {
                "pinned" => PinnedPanelContainer,
                "bookmarks" => BookmarksPanelContainer,
                "todo" => ToDoPanelContainer,
                "procore" => ProcorePanelContainer,
                _ => null
            };
        }

        /// <summary>
        /// Panel status information
        /// </summary>
        public class PanelStatus
        {
            public bool IsAvailable { get; set; }
            public bool IsVisible { get; set; }
            public string Message { get; set; } = string.Empty;
            public bool CanExpand { get; set; }
        }
    }
}