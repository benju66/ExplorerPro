// UI/FileTree/ImprovedFileTreeListView.xaml.cs - Refactored with SRP using specialized managers
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using ExplorerPro.Utilities;
using ExplorerPro.UI.FileTree.Services;
using ExplorerPro.UI.FileTree.Dialogs;
using ExplorerPro.UI.FileTree.Utilities;
using ExplorerPro.UI.FileTree.Commands;
using ExplorerPro.UI.FileTree.Coordinators;
using ExplorerPro.UI.FileTree.Managers;
using ExplorerPro.UI.FileTree.Helpers;
using ExplorerPro.Themes;
using ExplorerPro.Core;
using ExplorerPro.Core.Threading;
// Add alias to avoid ambiguity
using Path = System.IO.Path;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Improved FileTreeListView following Single Responsibility Principle.
    /// This refactored version delegates responsibilities to specialized manager classes
    /// while preserving all existing functionality and UI behavior.
    /// PHASE 6: Thread Safety Standardization - All UI operations are thread-safe
    /// </summary>
    public partial class ImprovedFileTreeListView : UserControl, IFileTree, IDisposable, INotifyPropertyChanged
    {
        // Explicit interface implementations to handle nullability
        SelectionService IFileTree.SelectionService => _coordinator?.SelectionService ?? throw new InvalidOperationException("SelectionService not initialized");
        string IFileTree.GetSelectedFolderPath() => GetSelectedFolderPath() ?? string.Empty;
        FileTreeItem IFileTree.FindItemByPath(string path) => FindItemByPath(path) ?? throw new InvalidOperationException($"Item not found: {path}");

        #region Private Fields - Simplified with Managers

        private readonly ObservableCollection<FileTreeItem> _rootItems = new();
        private readonly SettingsManager _settingsManager = null!;
        
        // Specialized manager classes following SRP
        private FileTreeCoordinator? _coordinator;
        private FileTreePerformanceManager? _performanceManager;
        private FileTreeUIEventManager? _uiEventManager;
        private FileTreeColumnManager? _columnManager;
        private FileTreeOperationHelper? _operationHelper;
        
        // Debounced selection update
        private DispatcherTimer? _selectionUpdateTimer;
        private bool _pendingSelectionUpdate = false;
        
        // State tracking
        private bool _isInitialized = false;
        private bool _disposed = false;
        
        // Search highlighting
        private string _searchQuery = "";
        private readonly Dictionary<string, bool> _highlightedItems = new Dictionary<string, bool>();
        
        // Performance optimization settings
        private bool _enableAnimations = false; // Disabled by default for performance
        private const int MAX_ITEMS_FOR_ANIMATIONS = 500; // Threshold for enabling animations

        #endregion

        #region Events

        public event EventHandler<string> LocationChanged = delegate { };
        public event EventHandler<Tuple<string, string>> ContextMenuActionTriggered = delegate { };
        public event EventHandler FileTreeClicked = delegate { };
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Properties - Delegated to Coordinator

        public ObservableCollection<FileTreeItem> RootItems => _rootItems;
        public string CurrentPath => _coordinator?.CurrentPath ?? string.Empty;
        public bool HasSelectedItems => _coordinator?.HasSelectedItems ?? false;
        public bool ShowHiddenFiles
        {
            get => _coordinator?.ShowHiddenFiles ?? false;
            set { if (_coordinator != null) _coordinator.ShowHiddenFiles = value; }
        }
        public SelectionService? SelectionService => _coordinator?.SelectionService;
        public bool IsMultiSelectMode => _coordinator?.IsMultiSelectMode ?? false;
        public bool HasSelection => _coordinator?.HasSelection ?? false;
        public int SelectionCount => _coordinator?.SelectionCount ?? 0;

        #endregion

        #region Constructor

        public ImprovedFileTreeListView()
        {
            try
            {
                InitializeComponent();

                _settingsManager = App.Settings ?? new SettingsManager();

                // Initialize all specialized managers in constructor to fix readonly assignment
                InitializeManagers();
                
                // Wire up events between managers
                WireUpManagerEvents();
                
                SetupUI();
                DataContext = this;

                System.Diagnostics.Debug.WriteLine("[INIT] FileTreeListView initialized with SRP design");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing FileTreeListView: {ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Initialization

        private void InitializeManagers()
        {
            // Initialize performance manager first - no explicit ScrollViewer needed, it will find it
            _performanceManager = new FileTreePerformanceManager(fileTreeView);
            
            // Initialize all dependencies for coordinator
            var dependencies = CreateServiceDependencies();
            
            if (dependencies.FileTreeService == null || 
                dependencies.FileTreeCache == null || 
                dependencies.SelectionService == null || 
                dependencies.ThemeService == null || 
                dependencies.FileOperationHandler == null || 
                dependencies.DragDropService == null)
            {
                throw new InvalidOperationException("Failed to initialize required dependencies");
            }

            // Create coordinator with all dependencies
            _coordinator = new FileTreeCoordinator(
                fileTreeView,
                _rootItems,
                dependencies.FileTreeService,
                dependencies.FileTreeCache,
                dependencies.SelectionService,
                dependencies.ThemeService,
                dependencies.FileOperationHandler,
                dependencies.DragDropService
            );

            // Initialize UI event manager
            _uiEventManager = new FileTreeUIEventManager(
                fileTreeView, 
                this, 
                _coordinator.SelectionService, 
                _performanceManager
            );

            // Initialize column manager
            var nameColumnSplitter = this.FindName("NameColumnSplitter") as GridSplitter;
            _columnManager = new FileTreeColumnManager(
                _settingsManager, 
                NameColumn, 
                nameColumnSplitter ?? throw new InvalidOperationException("NameColumnSplitter not found in XAML")
            );

            // Initialize operation helper
            _operationHelper = new FileTreeOperationHelper(
                this,
                _coordinator.SelectionService,
                dependencies.FileOperationHandler
            );

            // Initialize selection update timer
            _selectionUpdateTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _selectionUpdateTimer.Tick += OnSelectionUpdateTimer_Tick;
        }

        private ServiceDependencies CreateServiceDependencies()
        {
            var metadataManager = App.MetadataManager ?? MetadataManager.Instance;
            var undoManager = App.UndoManager ?? UndoManager.Instance;
            var fileOperations = new FileOperations.FileOperations();

            var iconProvider = new FileIconProvider(true);
            var fileTreeService = new FileTreeService(metadataManager, iconProvider);
            var fileTreeCache = new FileTreeCacheService(1000);
            
            var themeService = new FileTreeThemeService(fileTreeView, MainGrid);
            var selectionService = new SelectionService();
            
            var fileOperationHandler = new FileOperationHandler(fileOperations, undoManager, metadataManager);
            var dragDropService = new FileTreeDragDropService(undoManager, fileOperations, selectionService);

            return new ServiceDependencies
            {
                FileTreeService = fileTreeService,
                FileTreeCache = fileTreeCache,
                SelectionService = selectionService,
                ThemeService = themeService,
                FileOperationHandler = fileOperationHandler,
                DragDropService = dragDropService
            };
        }

        private void WireUpManagerEvents()
        {
            // Wire up UI events to business logic
            if (_uiEventManager != null)
            {
                _uiEventManager.ItemDoubleClicked += OnItemDoubleClicked;
                _uiEventManager.ItemClicked += OnItemClicked;
                _uiEventManager.EmptySpaceClicked += OnEmptySpaceClicked;
                _uiEventManager.SelectionRectangleCompleted += OnSelectionRectangleCompleted;
            }
            
            // Wire up selection changes to update debouncing
            if (_coordinator?.SelectionService != null)
            {
                _coordinator.SelectionService.SelectionChanged += OnSelectionChanged;
                _coordinator.SelectionService.PropertyChanged += OnSelectionService_PropertyChanged;
                _coordinator.SelectionService.MultiSelectModeChanged += OnMultiSelectModeChanged;
            }
            
            // Wire up coordinator property changes
            if (_coordinator != null)
            {
                _coordinator.PropertyChanged += OnCoordinatorPropertyChanged;
                
                // Wire up coordinator events to main events
                _coordinator.LocationChanged += (s, path) => LocationChanged?.Invoke(this, path);
                _coordinator.FileTreeClicked += (s, e) => FileTreeClicked?.Invoke(this, e);
                
                // Wire up context menu event from coordinator
                _coordinator.ContextMenuRequested += OnContextMenuRequested;
            }
            
            // Wire up performance manager events
            if (_performanceManager != null)
            {
                _performanceManager.VisibleItemsCacheUpdated += OnVisibleItemsCacheUpdated;
            }
        }

        private void SetupUI()
        {
            fileTreeView.ItemsSource = _rootItems;
            // NOTE: We don't attach ContextMenuOpening here anymore - it goes through the event manager chain
            
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        #endregion

        #region Event Handlers

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                
                var showHidden = _settingsManager.GetSetting("file_view.show_hidden", false);
                ShowHiddenFiles = showHidden;
                
                TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                if (_coordinator != null)
                {
                    _coordinator.RefreshThemeElements();
                }
                
                if (_performanceManager != null)
                {
                    _performanceManager.UpdateVisibleItemsCache();
                }
            }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            _columnManager?.SaveColumnWidths();
        }

        private void OnItemDoubleClicked(object? sender, string filePath)
        {
            HandleDoubleClick(filePath);
        }

        private void OnItemClicked(object? sender, FileTreeItem item)
        {
            if (item != null && _coordinator != null)
            {
                OnPropertyChanged(nameof(CurrentPath));
                LocationChanged?.Invoke(this, item.Path);
                ScheduleSelectionUpdate();
            }
        }

        private void OnEmptySpaceClicked(object? sender, Point point)
        {
            ScheduleSelectionUpdate();
        }

        private void OnSelectionRectangleCompleted(object? sender, EventArgs e)
        {
            ScheduleSelectionUpdate();
        }

        private void OnSelectionChanged(object? sender, FileTreeSelectionChangedEventArgs e)
        {
            if (_coordinator != null)
            {
                OnPropertyChanged(nameof(HasSelectedItems));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectionCount));
                
                ScheduleSelectionUpdate();
            }
        }

        private void OnSelectionService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectionService.IsMultiSelectMode))
            {
                OnPropertyChanged(nameof(SelectionService));
                OnPropertyChanged(nameof(IsMultiSelectMode));
            }
            else if (e.PropertyName == nameof(SelectionService.HasSelection))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
            else if (e.PropertyName == nameof(SelectionService.SelectionCount))
            {
                OnPropertyChanged(nameof(SelectionCount));
            }
        }

        private void OnMultiSelectModeChanged(object? sender, EventArgs e)
        {
            if (_disposed) return;
            
            // Update the TreeView tag to trigger checkbox visibility binding
            if (_coordinator?.SelectionService != null)
            {
                fileTreeView.Tag = _coordinator.SelectionService.IsMultiSelectMode ? "MultiSelect" : "SingleSelect";
            }
            
            // Force visual update for all visible items
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (!_disposed)
                {
                    // This forces WPF to re-evaluate the bindings
                    fileTreeView.Items.Refresh();
                }
            }));
        }

        private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        private void OnVisibleItemsCacheUpdated(object? sender, EventArgs e)
        {
            if (_coordinator != null)
            {
                ScheduleSelectionUpdate();
            }
        }

        // FIXED: Handle context menu request from coordinator
        private void OnContextMenuRequested(object? sender, FileTreeContextMenuEventArgs e)
        {
            if (_disposed || _coordinator == null) return;
            
            var contextMenuProvider = new ContextMenuProvider(
                App.MetadataManager ?? MetadataManager.Instance, 
                App.UndoManager ?? UndoManager.Instance, 
                CreateFileOperationHandler(),
                this
            );

            ContextMenu? contextMenu = null;
            
            // If we have a clicked item, build a context menu for it
            if (e.ClickedItem != null)
            {
                // Check if this item is part of multi-selection
                if (_coordinator.SelectionService.HasMultipleSelection && 
                    _coordinator.SelectionService.SelectedPaths.Contains(e.ClickedItem.Path))
                {
                    // Build multi-select context menu
                    contextMenu = contextMenuProvider.BuildMultiSelectContextMenu(e.SelectedPaths);
                }
                else
                {
                    // Build single item context menu
                    contextMenu = contextMenuProvider.BuildContextMenu(e.ClickedItem.Path);
                }
            }
            else if (_coordinator.SelectionService.HasSelection)
            {
                // No specific item clicked, but we have selection - use multi-select menu
                contextMenu = contextMenuProvider.BuildMultiSelectContextMenu(e.SelectedPaths);
            }
            
            if (contextMenu != null)
            {
                contextMenu.PlacementTarget = fileTreeView;
                contextMenu.IsOpen = true;
                e.Handled = true;
            }
            else
            {
                // No context menu to show
                e.Handled = true;
            }
        }

        private void OnSelectionUpdateTimer_Tick(object? sender, EventArgs e)
        {
            _selectionUpdateTimer?.Stop();
            
            if (_pendingSelectionUpdate && !_disposed)
            {
                _pendingSelectionUpdate = false;
                UpdateTreeViewSelection();
            }
        }

        // Keep for XAML compatibility
        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                if (checkBox.IsChecked == true)
                {
                    SelectAll();
                }
                else
                {
                    ClearSelection();
                }
            }
        }

        // Keep for XAML compatibility
        private void SelectionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is FileTreeItem item && _coordinator?.SelectionService != null)
            {
                _coordinator.SelectionService.ToggleSelection(item);
                ScheduleSelectionUpdate();
            }
        }

        // XAML event handler for column resizing
        private void NameColumnSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            // The column manager handles this automatically through its own event handler
            // This is just a pass-through for XAML compatibility
        }

        #endregion

        #region IFileTree Implementation - Delegated to Coordinator and Managers

        public string GetCurrentPath() => _coordinator?.CurrentPath ?? string.Empty;
        public string? GetSelectedPath() => _coordinator?.SelectionService?.FirstSelectedPath;
        public IReadOnlyList<string> GetSelectedPaths() => _coordinator?.SelectionService?.SelectedPaths ?? new List<string>();

        public string? GetSelectedFolderPath()
        {
            var selectedPath = GetSelectedPath();
            if (selectedPath == null) return null;
            return Directory.Exists(selectedPath) ? selectedPath : Path.GetDirectoryName(selectedPath);
        }

        public void RefreshView() 
        { 
            if (_coordinator != null)
            {
                _coordinator.RefreshView();
            }
            
            // Optimize performance after view refresh
            OptimizePerformanceForTreeSize();
        }
        public void RefreshDirectory(string directoryPath) => _ = _coordinator?.RefreshDirectoryAsync(directoryPath);
        public void SelectItem(string path) => _coordinator?.SelectItemByPath(path);

        public void SelectItems(IEnumerable<string>? paths)
        {
            if (paths == null) return;
            foreach (var path in paths)
            {
                SelectItem(path);
            }
        }

        public void SelectAll() => _coordinator?.SelectionService?.SelectAll(_rootItems);
        public void InvertSelection() => _coordinator?.SelectionService?.InvertSelection(_rootItems);
        public void ToggleMultiSelectMode() 
        {
            if (_coordinator?.SelectionService != null)
                _coordinator.SelectionService.StickyMultiSelectMode = !_coordinator.SelectionService.StickyMultiSelectMode;
        }
        public void ClearSelection() => _coordinator?.SelectionService?.ClearSelection();

        // File operations delegated to operation helper
        public void CopySelected() => _operationHelper?.CopySelected();
        public void CutSelected() => _operationHelper?.CutSelected();
        public void Paste() => _operationHelper?.Paste();
        public void DeleteSelected() => _operationHelper?.DeleteSelected();
        public void CreateFolder() => _operationHelper?.CreateFolder();
        public void CreateFile() => _operationHelper?.CreateFile();

        public void ToggleShowHidden()
        {
            ShowHiddenFiles = !ShowHiddenFiles;
            _settingsManager?.UpdateSetting("file_view.show_hidden", ShowHiddenFiles);
        }

        public void RefreshThemeElements() => _coordinator?.RefreshThemeElements();

        public void HandleFileDrop(object data)
        {
            // Delegate to coordinator's drag drop service
        }

        public async void SetRootDirectory(string directory) 
        {
            ThreadSafetyValidator.LogThreadContext("SetRootDirectory");
            
            if (_coordinator != null)
            {
                // Background work can be done on any thread
                await Task.Run(async () =>
                {
                    ThreadSafetyValidator.AssertBackgroundThread();
                    await _coordinator.SetRootDirectoryAsync(directory).ConfigureAwait(false);
                });
            }
        }

        /// <summary>
        /// Sets root directory with performance optimization for large directories
        /// </summary>
        public async Task SetRootDirectoryOptimizedAsync(string directory, int maxItems = 10000)
        {
            var dependencies = CreateServiceDependencies();
            if (dependencies?.FileTreeService == null) return;

            try
            {
                // Use the new large directory loading method for better performance
                var items = await dependencies.FileTreeService.LoadLargeDirectoryAsync(
                    directory, 
                    ShowHiddenFiles, 
                    pageSize: 500, 
                    maxItems: maxItems);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _rootItems.Clear();
                    foreach (var item in items)
                    {
                        _rootItems.Add(item);
                    }
                    
                    // Optimize performance based on item count
                    OptimizePerformanceForTreeSize();
                });

                LocationChanged?.Invoke(this, directory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to set root directory optimized: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads multiple directories in batch for improved performance
        /// </summary>
        public async Task LoadDirectoriesBatchAsync(IEnumerable<string> directories)
        {
            var dependencies = CreateServiceDependencies();
            if (dependencies?.FileTreeService == null) return;

            try
            {
                var items = await dependencies.FileTreeService.LoadDirectoryBatchAsync(
                    directories, 
                    ShowHiddenFiles);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _rootItems.Clear();
                    foreach (var item in items)
                    {
                        _rootItems.Add(item);
                    }
                    
                    OptimizePerformanceForTreeSize();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load directories batch: {ex.Message}");
            }
        }

        public void NavigateAndHighlight(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                ExpandToPath(Path.GetDirectoryName(path));
                
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var item = FindItemByPath(path);
                    if (item != null && _coordinator?.SelectionService != null)
                    {
                        _coordinator.SelectionService.SelectSingle(item);
                        
                        if (_performanceManager != null)
                        {
                            var treeViewItem = _performanceManager.GetTreeViewItemCached(item);
                            treeViewItem?.BringIntoView();
                        }
                    }
                }), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Navigate and highlight failed: {ex.Message}");
            }
        }

        public void ExpandToPath(string? path) 
        { 
            if (string.IsNullOrEmpty(path)) return;
            
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                           .Where(p => !string.IsNullOrEmpty(p))
                           .ToList();
            
            var currentItems = _rootItems;
            string currentPath = "";
            
            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : Path.Combine(currentPath, part);
                var item = currentItems.FirstOrDefault(i => 
                    string.Equals(i.Path, currentPath, StringComparison.OrdinalIgnoreCase));
                
                if (item != null)
                {
                    item.IsExpanded = true;
                    currentItems = item.Children;
                }
                else
                {
                    break;
                }
            }
        }

        public void CollapseAll() 
        { 
            foreach (var item in _rootItems)
            {
                CollapseRecursive(item);
            }
        }

        public void ExpandAll() 
        { 
            foreach (var item in _rootItems)
            {
                ExpandRecursive(item);
            }
        }

        public FileTreeItem? FindItemByPath(string? path) 
        {
            if (string.IsNullOrEmpty(path)) return null;
            return FindItemInCollection(_rootItems, path);
        }

        public void SelectByPattern(string pattern, bool addToSelection = false)
        {
            _coordinator?.SelectionService?.SelectByPattern(pattern, _rootItems, addToSelection);
        }

        /// <summary>
        /// Highlights search results in the tree view
        /// </summary>
        public void HighlightSearchResults(string query)
        {
            _searchQuery = query?.Trim() ?? "";
            _highlightedItems.Clear();
            
            if (string.IsNullOrEmpty(_searchQuery))
            {
                RefreshView();
                return;
            }
            
            // Find matching items
            var matchingItems = FindMatchingItems(_rootItems, _searchQuery);
            foreach (var item in matchingItems)
            {
                _highlightedItems[item.Path] = true;
                
                // Expand parent items to make matches visible
                ExpandToPath(item.Path);
            }
            
            RefreshView();
        }

        /// <summary>
        /// Clears search highlighting
        /// </summary>
        public void ClearSearchHighlighting()
        {
            _searchQuery = "";
            _highlightedItems.Clear();
            RefreshView();
        }

        /// <summary>
        /// Gets whether an item is highlighted from search
        /// </summary>
        public bool IsItemHighlighted(string path)
        {
            return _highlightedItems.ContainsKey(path);
        }

        /// <summary>
        /// Recursively finds items matching the search query
        /// </summary>
        private List<FileTreeItem> FindMatchingItems(IEnumerable<FileTreeItem> items, string query)
        {
            var matches = new List<FileTreeItem>();
            
            foreach (var item in items)
            {
                // Check if item name contains the query (case-insensitive)
                if (item.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches.Add(item);
                }
                
                // Recursively search children
                if (item.Children?.Count > 0)
                {
                    matches.AddRange(FindMatchingItems(item.Children, query));
                }
            }
            
            return matches;
        }

        #endregion

        #region Performance Optimization Methods
        
        /// <summary>
        /// Gets or sets whether animations are enabled based on performance considerations
        /// </summary>
        public bool EnableAnimations
        {
            get => _enableAnimations;
            set
            {
                if (_enableAnimations != value)
                {
                    _enableAnimations = value;
                    ApplyPerformanceSettings();
                }
            }
        }
        
        /// <summary>
        /// Automatically adjusts performance settings based on tree size
        /// </summary>
        private void OptimizePerformanceForTreeSize()
        {
            try
            {
                var totalItemCount = CountVisibleTreeItems();
                
                // Automatically disable animations for large trees
                var shouldEnableAnimations = totalItemCount <= MAX_ITEMS_FOR_ANIMATIONS;
                
                if (_enableAnimations != shouldEnableAnimations)
                {
                    _enableAnimations = shouldEnableAnimations;
                    ApplyPerformanceSettings();
                    
                    System.Diagnostics.Debug.WriteLine(
                        $"FileTree Performance: {totalItemCount} items, animations {(_enableAnimations ? "enabled" : "disabled")}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error optimizing performance: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Counts visible items in the tree for performance optimization
        /// </summary>
        private int CountVisibleTreeItems()
        {
            return CountItemsRecursive(_rootItems);
        }
        
        /// <summary>
        /// Recursively counts items in the tree
        /// </summary>
        private int CountItemsRecursive(IEnumerable<FileTreeItem> items)
        {
            int count = 0;
            if (items == null) return count;
            
            foreach (var item in items)
            {
                count++;
                if (item.IsExpanded && item.Children?.Count > 0)
                {
                    count += CountItemsRecursive(item.Children);
                }
            }
            return count;
        }
        
        /// <summary>
        /// Applies performance settings to the TreeView
        /// </summary>
        private void ApplyPerformanceSettings()
        {
            try
            {
                // For now, we're using lightweight CSS-style hover effects instead of animations
                // This method can be extended in the future if we want to re-enable optional animations
                
                // Update virtualization settings based on performance mode
                if (fileTreeView != null)
                {
                    VirtualizingPanel.SetIsVirtualizing(fileTreeView, true);
                    VirtualizingPanel.SetVirtualizationMode(fileTreeView, VirtualizationMode.Recycling);
                    VirtualizingPanel.SetIsContainerVirtualizable(fileTreeView, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying performance settings: {ex.Message}");
            }
        }
        
        #endregion

        #region Inline Editing Event Handlers

        /// <summary>
        /// Handles when the edit TextBox is loaded - focus and select all text
        /// </summary>
        private void EditNameTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        /// <summary>
        /// Handles key presses in the edit TextBox
        /// </summary>
        private void EditNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is FileTreeItem item)
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        // Commit the rename
                        CommitRename(item, textBox.Text);
                        e.Handled = true;
                        break;
                        
                    case Key.Escape:
                        // Cancel the rename
                        CancelRename(item);
                        e.Handled = true;
                        break;
                }
            }
        }

        /// <summary>
        /// Handles when the edit TextBox loses focus - commit the rename
        /// </summary>
        private void EditNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is FileTreeItem item)
            {
                CommitRename(item, textBox.Text);
            }
        }

        /// <summary>
        /// Commits the rename operation
        /// </summary>
        private void CommitRename(FileTreeItem item, string newName)
        {
            if (item == null || string.IsNullOrWhiteSpace(newName))
            {
                CancelRename(item);
                return;
            }

            // Trim whitespace
            newName = newName.Trim();

            // Check if name actually changed
            if (newName == item.Name)
            {
                CancelRename(item);
                return;
            }

            // Validate the new name
            if (newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("The name contains invalid characters.", "Invalid Name", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CancelRename(item);
                return;
            }

            try
            {
                // Perform the rename using the file operation handler
                var fileOperationHandler = CreateFileOperationHandler();
                bool success = fileOperationHandler.RenameItem(item.Path, newName, this);

                if (success)
                {
                    // Update the item name (this will be updated by the file system watcher)
                    // but we can update it immediately for better UX
                    item.Name = newName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rename: {ex.Message}", "Rename Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Always exit edit mode
                item.IsInEditMode = false;
            }
        }

        /// <summary>
        /// Cancels the rename operation
        /// </summary>
        private void CancelRename(FileTreeItem? item)
        {
            if (item != null)
            {
                item.IsInEditMode = false;
            }
        }

        #endregion

        #region Helper Methods

        private void HandleDoubleClick(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to open file '{path}': {ex.Message}");
            }
        }

        private FileOperationHandler CreateFileOperationHandler()
        {
            var metadataManager = App.MetadataManager ?? MetadataManager.Instance;
            var undoManager = App.UndoManager ?? UndoManager.Instance;
            var fileOperations = new FileOperations.FileOperations();
            return new FileOperationHandler(fileOperations, undoManager, metadataManager);
        }

        private FileTreeItem? FindItemInCollection(IEnumerable<FileTreeItem>? items, string? path)
        {
            if (items == null || path == null) return null;
            foreach (var item in items)
            {
                if (string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase))
                    return item;
                var found = FindItemInCollection(item.Children, path);
                if (found != null) return found;
            }
            return null;
        }

        private void CollapseRecursive(FileTreeItem item)
        {
            item.IsExpanded = false;
            foreach (var child in item.Children)
            {
                CollapseRecursive(child);
            }
        }

        private void ExpandRecursive(FileTreeItem item)
        {
            item.IsExpanded = true;
            foreach (var child in item.Children)
            {
                ExpandRecursive(child);
            }
        }

        private void ScheduleSelectionUpdate()
        {
            _pendingSelectionUpdate = true;
            
            if (_selectionUpdateTimer != null && !_selectionUpdateTimer.IsEnabled)
            {
                _selectionUpdateTimer.Start();
            }
        }

        private void UpdateTreeViewSelection()
        {
            if (_coordinator?.SelectionService == null || _performanceManager == null) return;
            var selectedPaths = new HashSet<string>(_coordinator.SelectionService.SelectedPaths, StringComparer.OrdinalIgnoreCase);
            foreach (var treeViewItem in _performanceManager.GetAllVisibleTreeViewItems())
            {
                if (treeViewItem.DataContext is FileTreeItem dataItem)
                {
                    var shouldBeSelected = selectedPaths.Contains(dataItem.Path);
                    if (treeViewItem.IsSelected != shouldBeSelected)
                    {
                        treeViewItem.IsSelected = shouldBeSelected;
                    }
                }
            }
        }

        protected void OnPropertyChanged(string? propertyName)
        {
            if (propertyName != null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _selectionUpdateTimer?.Stop();
                _selectionUpdateTimer = null;

                this.Loaded -= OnLoaded;
                this.Unloaded -= OnUnloaded;
                
                if (_coordinator != null)
                {
                    _coordinator.ContextMenuRequested -= OnContextMenuRequested;
                    if (_coordinator.SelectionService != null)
                    {
                        _coordinator.SelectionService.MultiSelectModeChanged -= OnMultiSelectModeChanged;
                    }
                }

                _performanceManager?.Dispose();
                _uiEventManager?.Dispose();
                _columnManager?.Dispose();
                _operationHelper?.Dispose();
                _coordinator?.Dispose();

                // Clear all event handlers
                LocationChanged = delegate { };
                ContextMenuActionTriggered = delegate { };
                FileTreeClicked = delegate { };
                PropertyChanged = delegate { };
                DataContext = null;

                _coordinator = null;
                _performanceManager = null;
                _uiEventManager = null;
                _columnManager = null;
                _operationHelper = null;

                System.Diagnostics.Debug.WriteLine("[DISPOSE] Refactored FileTreeListView disposed");
            }
        }

        #endregion

        #region Nested Types

        private class ServiceDependencies
        {
            public IFileTreeService? FileTreeService { get; set; }
            public IFileTreeCache? FileTreeCache { get; set; }
            public SelectionService? SelectionService { get; set; }
            public FileTreeThemeService? ThemeService { get; set; }
            public FileOperationHandler? FileOperationHandler { get; set; }
            public FileTreeDragDropService? DragDropService { get; set; }
        }

        #endregion
    }
}