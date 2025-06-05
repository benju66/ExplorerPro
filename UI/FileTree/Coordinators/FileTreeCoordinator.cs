// UI/FileTree/Coordinators/FileTreeCoordinator.cs
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
using System.Windows.Input;
using ExplorerPro.FileOperations;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Managers;
using ExplorerPro.UI.FileTree.Services;
using ExplorerPro.UI.FileTree.Utilities;
using ExplorerPro.UI.FileTree.Commands;
using ExplorerPro.Utilities;
using System.Windows.Threading;

namespace ExplorerPro.UI.FileTree.Coordinators
{
    /// <summary>
    /// Coordinates all file tree operations and manages the interaction between services and managers
    /// </summary>
    public class FileTreeCoordinator : INotifyPropertyChanged, IDisposable
    {
        #region Private Fields

        private readonly TreeView _treeView;
        private readonly ObservableCollection<FileTreeItem> _rootItems;
        
        // Managers - initialize in constructor to fix readonly assignment issue
        private readonly FileTreeEventManager _eventManager;
        private readonly FileTreePerformanceManager _performanceManager;
        private readonly FileTreeLoadChildrenManager _loadChildrenManager;
        
        // Services
        private readonly IFileTreeService _fileTreeService;
        private readonly IFileTreeCache _fileTreeCache;
        private readonly SelectionService _selectionService;
        private readonly FileTreeThemeService _themeService;
        private readonly FileOperationHandler _fileOperationHandler;
        private readonly FileTreeDragDropService _dragDropService;
        
        // State
        private string _currentFolderPath = string.Empty;
        private bool _showHiddenFiles = false;
        private bool _isProcessingSelection = false;
        private bool _isUpdatingVisualSelection = false;
        private bool _disposed = false;

        // Add new fields for selection state tracking
        private readonly Dictionary<string, bool> _previousSelectionState = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<TreeViewItem> _pendingSelectionUpdates = new Queue<TreeViewItem>();
        private readonly DispatcherTimer _selectionUpdateBatchTimer;
        private volatile bool _isBatchUpdateInProgress = false;
        private DateTime _lastSelectionUpdateStart;
        private TimeSpan _lastSelectionUpdateDuration;
        private int _lastItemsProcessed;
        private int _lastItemsChanged;

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<string> LocationChanged;
        public event EventHandler<FileTreeContextMenuEventArgs> ContextMenuRequested;
        public event EventHandler FileTreeClicked;

        #endregion

        #region Properties

        public ObservableCollection<FileTreeItem> RootItems => _rootItems;
        public string CurrentPath => _currentFolderPath;
        public bool HasSelectedItems => _selectionService?.HasSelection ?? false;
        public bool ShowHiddenFiles
        {
            get => _showHiddenFiles;
            set
            {
                if (_showHiddenFiles != value)
                {
                    _showHiddenFiles = value;
                    _loadChildrenManager?.UpdateSettings(value);
                    OnPropertyChanged(nameof(ShowHiddenFiles));
                    RefreshView();
                }
            }
        }
        public SelectionService SelectionService => _selectionService;
        public bool IsMultiSelectMode => _selectionService?.IsMultiSelectMode ?? false;
        public bool HasSelection => _selectionService?.HasSelection ?? false;
        public int SelectionCount => _selectionService?.SelectionCount ?? 0;

        #endregion

        #region Constructor

        public FileTreeCoordinator(
            TreeView treeView, 
            ObservableCollection<FileTreeItem> rootItems,
            IFileTreeService fileTreeService,
            IFileTreeCache fileTreeCache,
            SelectionService selectionService,
            FileTreeThemeService themeService,
            FileOperationHandler fileOperationHandler,
            FileTreeDragDropService dragDropService)
        {
            _treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
            _rootItems = rootItems ?? throw new ArgumentNullException(nameof(rootItems));
            _fileTreeService = fileTreeService ?? throw new ArgumentNullException(nameof(fileTreeService));
            _fileTreeCache = fileTreeCache ?? throw new ArgumentNullException(nameof(fileTreeCache));
            _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _fileOperationHandler = fileOperationHandler ?? throw new ArgumentNullException(nameof(fileOperationHandler));
            _dragDropService = dragDropService ?? throw new ArgumentNullException(nameof(dragDropService));

            // Initialize batch timer for selection updates
            _selectionUpdateBatchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };

            // Initialize managers in constructor to fix readonly field assignment
            _eventManager = new FileTreeEventManager(_treeView, _fileTreeService, _selectionService);
            _performanceManager = new FileTreePerformanceManager(_treeView); // Will find ScrollViewer internally
            _loadChildrenManager = new FileTreeLoadChildrenManager(_fileTreeService, _fileTreeCache);

            SetupEventHandlers();
            SetupDragDrop();
        }

        #endregion

        #region Initialization

        private void SetupEventHandlers()
        {
            // Event manager events
            _eventManager.ItemDoubleClicked += OnItemDoubleClicked;
            _eventManager.ItemClicked += OnItemClicked;
            _eventManager.ItemExpanded += OnItemExpanded;
            _eventManager.ContextMenuRequested += OnContextMenuRequested;
            _eventManager.MouseEvent += OnMouseEvent;
            _eventManager.KeyboardEvent += OnKeyboardEvent;

            // Performance manager events
            _performanceManager.SelectionUpdateRequested += OnSelectionUpdateRequested;

            // Load children manager events
            _loadChildrenManager.DirectoryLoaded += OnDirectoryLoaded;
            _loadChildrenManager.DirectoryLoadError += OnDirectoryLoadError;

            // Selection service events
            _selectionService.SelectionChanged += OnSelectionChanged;
            _selectionService.PropertyChanged += OnSelectionServicePropertyChanged;

            // File operation handler events
            _fileOperationHandler.DirectoryRefreshRequested += OnDirectoryRefreshRequested;
            _fileOperationHandler.MultipleDirectoriesRefreshRequested += OnMultipleDirectoriesRefreshRequested;
            _fileOperationHandler.OperationError += OnFileOperationError;
            _fileOperationHandler.PasteCompleted += OnPasteCompleted;

            // Drag drop service events
            _dragDropService.FilesDropped += OnFilesDropped;
            _dragDropService.FilesMoved += OnFilesMoved;
            _dragDropService.ErrorOccurred += OnDragDropError;
            _dragDropService.OutlookExtractionCompleted += OnOutlookExtractionCompleted;
        }

        private void SetupDragDrop()
        {
            // Attach drag drop service to the tree view
            _dragDropService.AttachToControl(_treeView, GetItemFromPoint);
        }

        #endregion

        #region Public Methods

        public async Task SetRootDirectoryAsync(string directory)
        {
            try 
            {
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Invalid directory: {directory}");
                    return;
                }
                
                // Cancel any ongoing operations
                _loadChildrenManager.CancelActiveOperations();
                
                // Clear existing data on UI thread
                await Application.Current.Dispatcher.InvokeAsync(() => ClearCurrentData());
                
                // Normalize the path
                directory = Path.GetFullPath(directory);
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Setting root directory to: {directory}");
                
                // OPTIMIZED: Use ConfigureAwait(false) for async operations
                var rootItem = await _fileTreeService.CreateFileTreeItemAsync(directory, 0, _showHiddenFiles, CancellationToken.None).ConfigureAwait(false);
                
                if (rootItem != null)
                {
                    // Setup root item and add to collection on UI thread
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _loadChildrenManager.SubscribeToLoadChildren(rootItem);
                        _rootItems.Add(rootItem);
                        _fileTreeCache.SetItem(directory, rootItem);
                    });
                    
                    // Load children
                    await _loadChildrenManager.LoadDirectoryContentsAsync(rootItem).ConfigureAwait(false);
                    
                    // Update current path and expand root item on UI thread
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _currentFolderPath = directory;
                        rootItem.IsExpanded = true;
                    });
                    
                    LocationChanged?.Invoke(this, directory);
                    
                    // Update UI
                    await UpdateUIAfterDirectoryLoad().ConfigureAwait(false);
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Root directory set successfully: {directory}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to set root directory: {ex.Message}");
                throw;
            }
        }

        public void RefreshView()
        {
            if (!string.IsNullOrEmpty(_currentFolderPath))
            {
                _ = SetRootDirectoryAsync(_currentFolderPath);
            }
        }

        /// <summary>
        /// OPTIMIZED: Refreshes directory with selective cache invalidation
        /// </summary>
        public async Task RefreshDirectoryAsync(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || _disposed)
                return;

            try
            {
                // OPTIMIZED: Use ConfigureAwait(false) for async operations
                _fileTreeCache.RemoveWhere(kvp => kvp.Key.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase));
                
                var item = _fileTreeService.FindItemByPath(_rootItems, directoryPath);
                if (item != null)
                {
                    await _loadChildrenManager.RefreshDirectoryAsync(item).ConfigureAwait(false);
                    
                    // OPTIMIZED: Use selective invalidation instead of clearing all caches
                    _performanceManager.InvalidateDirectory(directoryPath);
                }
                else if (_rootItems.Count > 0 && string.Equals(_rootItems[0].Path, directoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    await SetRootDirectoryAsync(directoryPath).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to refresh directory {directoryPath}: {ex.Message}");
            }
        }

        public void SelectItemByPath(string path)
        {
            if (string.IsNullOrEmpty(path) || _disposed)
                return;
                
            var item = _fileTreeService.FindItemByPath(_rootItems, path);
            if (item != null)
            {
                _selectionService.SelectSingle(item);
                _performanceManager.ScheduleSelectionUpdate();
                
                // Bring item into view
                Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, 
                    new Action(() => {
                        if (!_disposed)
                        {
                            var treeViewItem = _performanceManager.GetTreeViewItemCached(item);
                            treeViewItem?.BringIntoView();
                        }
                    }));
            }
        }

        public FileTreeItem GetItemFromPoint(Point point)
        {
            return _performanceManager.GetItemFromPoint(point);
        }

        public void ScheduleSelectionUpdate()
        {
            _performanceManager.ScheduleSelectionUpdate();
        }

        public void RefreshThemeElements()
        {
            _themeService?.RefreshThemeElements();
        }

        #endregion

        #region Event Handlers

        private void OnItemDoubleClicked(object sender, string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open file:\n{path}\n\n{ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnItemClicked(object sender, string path)
        {
            if (_disposed || string.IsNullOrEmpty(path))
                return;

            // Only change the current folder path for directory clicks, not file selection
            // This prevents navigation changes when files are selected via context menu
            if (Directory.Exists(path))
            {
                _currentFolderPath = path;
                LocationChanged?.Invoke(this, path);
            }

            FileTreeClicked?.Invoke(this, EventArgs.Empty);
        }

        private async void OnItemExpanded(object sender, FileTreeItem item)
        {
            try
            {
                if (item.HasDummyChild() || item.Children.Count == 0)
                {
                    // OPTIMIZED: Use ConfigureAwait(false) for async operations
                    await _loadChildrenManager.LoadDirectoryContentsAsync(item).ConfigureAwait(false);
                }
                
                await UpdateUIAfterDirectoryLoad().ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Access denied expanding {item.Path}: {ex.Message}");
                // Could show user-friendly message here
            }
            catch (DirectoryNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Directory not found {item.Path}: {ex.Message}");
                // Could refresh parent directory here
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to expand directory {item.Path}: {ex.Message}");
                // Could show error in UI
            }
        }

        private void OnContextMenuRequested(object sender, FileTreeContextMenuEventArgs e)
        {
            // Forward the event to the view
            ContextMenuRequested?.Invoke(this, e);
        }

        private void OnMouseEvent(object sender, MouseEventArgs e)
        {
            // Handle mouse events for selection rectangle, etc.
            // Delegate to selection service if needed
            HandleMouseEventForSelection(e);
        }

        private void OnKeyboardEvent(object sender, KeyEventArgs e)
        {
            // Delegate keyboard shortcuts to selection service
            if (_selectionService.HandleKeyboardShortcut(e.Key, Keyboard.Modifiers, _rootItems))
            {
                _performanceManager.ScheduleSelectionUpdate();
                e.Handled = true;
            }
        }

        private void OnSelectionUpdateRequested(object sender, EventArgs e)
        {
            UpdateTreeViewSelectionOptimized();
        }

        private void OnDirectoryLoaded(object sender, DirectoryLoadedEventArgs e)
        {
            // OPTIMIZED: Removed aggressive cache clearing - let cache manage itself
        }

        private void OnDirectoryLoadError(object sender, DirectoryLoadErrorEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Directory load failed: {e.Exception.Message}");
        }

        private void OnSelectionChanged(object sender, FileTreeSelectionChangedEventArgs e)
        {
            if (_disposed) return;
            
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectionCount));
            
            if (!_isProcessingSelection && !_isUpdatingVisualSelection)
            {
                _performanceManager.ScheduleSelectionUpdate();
            }
        }

        private void OnSelectionServicePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_disposed) return;
            
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

        // Note: These event handlers reference types that need to be defined elsewhere
        // For the refactoring demo, we'll include placeholder implementations

        private void OnDirectoryRefreshRequested(object sender, DirectoryRefreshEventArgs e)
        {
            if (_disposed) return;
            Application.Current.Dispatcher.InvokeAsync(() => _ = RefreshDirectoryAsync(e.DirectoryPath)).Wait(TimeSpan.FromSeconds(5));
        }

        private void OnMultipleDirectoriesRefreshRequested(object sender, MultipleDirectoriesRefreshEventArgs e)
        {
            if (_disposed) return;
            foreach (var directory in e.DirectoryPaths)
            {
                Application.Current.Dispatcher.InvokeAsync(() => _ = RefreshDirectoryAsync(directory)).Wait(TimeSpan.FromSeconds(5));
            }
        }

        private void OnFileOperationError(object sender, FileOperationErrorEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] File operation error: {e.Operation} - {e.Exception.Message}");
        }

        private void OnPasteCompleted(object sender, PasteCompletedEventArgs e)
        {
            if (_disposed) return;
            Application.Current.Dispatcher.InvokeAsync(() => _ = RefreshDirectoryAsync(e.TargetPath)).Wait(TimeSpan.FromSeconds(5));
        }

        private async void OnFilesDropped(object sender, FilesDroppedEventArgs e)
        {
            if (_disposed) return;
            
            try
            {
                await RefreshDirectoryAsync(e.TargetPath).ConfigureAwait(false);
                
                if (e.IsInternalMove)
                {
                    var sourceDirectories = e.SourceFiles
                        .Select(f => Path.GetDirectoryName(f))
                        .Where(d => !string.IsNullOrEmpty(d))
                        .Distinct();
                        
                    foreach (var sourceDir in sourceDirectories)
                    {
                        await RefreshDirectoryAsync(sourceDir).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Files dropped operation failed: {ex.Message}");
            }
        }

        private async void OnFilesMoved(object sender, FilesMoved e)
        {
            if (_disposed) return;
            
            try
            {
                await RefreshDirectoryAsync(e.TargetPath).ConfigureAwait(false);
                
                foreach (var sourceDir in e.SourceDirectories)
                {
                    await RefreshDirectoryAsync(sourceDir).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Files moved operation failed: {ex.Message}");
            }
        }

        private void OnDragDropError(object sender, string error)
        {
            if (_disposed) return;
            Application.Current.Dispatcher.InvokeAsync(() => 
                MessageBox.Show(error, "Drag/Drop Error", MessageBoxButton.OK, MessageBoxImage.Warning)).Wait(TimeSpan.FromSeconds(5));
        }

        private void OnOutlookExtractionCompleted(object sender, OutlookExtractionCompletedEventArgs e)
        {
            if (_disposed) return;
            
            Application.Current.Dispatcher.InvokeAsync(() => 
            {
                if (!e.Result.Success && !string.IsNullOrEmpty(e.Result.ErrorMessage))
                {
                    MessageBox.Show(
                        $"There was a problem extracting Outlook attachments: {e.Result.ErrorMessage}",
                        "Extraction Problem",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
                
                _ = RefreshDirectoryAsync(e.TargetPath);
            }).Wait(TimeSpan.FromSeconds(5));
        }

        #endregion

        #region Private Methods

        private void ClearCurrentData()
        {
            // Ensure this method is called from UI thread since it modifies collections
            if (Application.Current?.Dispatcher.CheckAccess() != true)
            {
                throw new InvalidOperationException("ClearCurrentData must be called from the UI thread");
            }
            
            _rootItems.Clear();
            _fileTreeCache.Clear();
            _selectionService?.ClearSelection();
            _performanceManager.ClearTreeViewItemCache();
            _loadChildrenManager.UnsubscribeAllLoadChildren();
        }

        private async Task UpdateUIAfterDirectoryLoad()
        {
            if (_disposed) return;

            await Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, 
                new Action(() => {
                    if (_disposed) return;
                    
                    TreeViewItemExtensions.InitializeTreeViewItemLevels(_treeView);
                    _treeView.UpdateLayout();
                    
                    if (_rootItems.Count > 0)
                    {
                        var container = _treeView.ItemContainerGenerator.ContainerFromItem(_rootItems[0]) as TreeViewItem;
                        container?.BringIntoView();
                    }
                }));
        }

        private void HandleMouseEventForSelection(MouseEventArgs e)
        {
            // Handle selection-related mouse events
            if (e is MouseButtonEventArgs buttonEvent)
            {
                if (buttonEvent.RoutedEvent == UIElement.PreviewMouseLeftButtonDownEvent)
                {
                    var position = buttonEvent.GetPosition(_treeView);
                    var item = GetItemFromPoint(position);
                    
                    if (item != null)
                    {
                        _selectionService.HandleSelection(item, Keyboard.Modifiers, _rootItems);
                        _performanceManager.ScheduleSelectionUpdate();
                    }
                }
            }
        }

        private void UpdateTreeViewSelectionOptimized()
        {
            if (_isUpdatingVisualSelection || _disposed) return;
            
            var updateStart = DateTime.UtcNow;
            _lastSelectionUpdateStart = updateStart;
            
            _isUpdatingVisualSelection = true;
            _isProcessingSelection = true;
            
            try
            {
                // Use high-priority for immediate visible updates, background for others
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                {
                    ProcessSelectionUpdatesWithChangeTracking();
                }));
            }
            finally
            {
                _isUpdatingVisualSelection = false;
                _isProcessingSelection = false;
            }
        }

        /// <summary>
        /// Processes selection updates with intelligent change tracking and batching
        /// </summary>
        private void ProcessSelectionUpdatesWithChangeTracking()
        {
            if (_disposed || _isBatchUpdateInProgress) return;
            
            _isBatchUpdateInProgress = true;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int itemsProcessed = 0;
            int itemsChanged = 0;
            
            try
            {
                var currentSelectedPaths = new HashSet<string>(_selectionService.SelectedPaths, StringComparer.OrdinalIgnoreCase);
                var changedItems = new List<(TreeViewItem item, bool shouldBeSelected)>();
                
                // Step 1: Identify changes by comparing with previous state
                var pathsToCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                pathsToCheck.UnionWith(currentSelectedPaths);
                pathsToCheck.UnionWith(_previousSelectionState.Keys);
                
                // Step 2: Get visible items first for immediate updates
                var visibleItems = GetVisibleTreeViewItemsWithPaths();
                var visiblePaths = new HashSet<string>(visibleItems.Keys, StringComparer.OrdinalIgnoreCase);
                
                // Step 3: Process visible items first (immediate UI response)
                foreach (var kvp in visibleItems)
                {
                    var path = kvp.Key;
                    var treeViewItem = kvp.Value;
                    
                    if (pathsToCheck.Contains(path))
                    {
                        var shouldBeSelected = currentSelectedPaths.Contains(path);
                        var wasSelected = _previousSelectionState.GetValueOrDefault(path, false);
                        
                        if (shouldBeSelected != wasSelected || treeViewItem.IsSelected != shouldBeSelected)
                        {
                            changedItems.Add((treeViewItem, shouldBeSelected));
                            itemsChanged++;
                        }
                        
                        itemsProcessed++;
                    }
                }
                
                // Step 4: Apply visible changes immediately with batching
                if (changedItems.Count > 0)
                {
                    ApplySelectionChangesBatched(changedItems, isVisible: true);
                }
                
                // Step 5: Schedule non-visible items for background processing
                var nonVisibleChanges = new List<string>();
                foreach (var path in pathsToCheck)
                {
                    if (!visiblePaths.Contains(path))
                    {
                        var shouldBeSelected = currentSelectedPaths.Contains(path);
                        var wasSelected = _previousSelectionState.GetValueOrDefault(path, false);
                        
                        if (shouldBeSelected != wasSelected)
                        {
                            nonVisibleChanges.Add(path);
                        }
                    }
                }
                
                if (nonVisibleChanges.Count > 0)
                {
                    ScheduleNonVisibleSelectionUpdates(nonVisibleChanges, currentSelectedPaths);
                }
                
                // Step 6: Update previous state tracking
                _previousSelectionState.Clear();
                foreach (var path in currentSelectedPaths)
                {
                    _previousSelectionState[path] = true;
                }
                
                stopwatch.Stop();
                _lastSelectionUpdateDuration = stopwatch.Elapsed;
                _lastItemsProcessed = itemsProcessed;
                _lastItemsChanged = itemsChanged;
                
                // Log performance metrics for large updates
                if (itemsChanged > 10 || stopwatch.ElapsedMilliseconds > 5)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SELECTION-PERF] Updated {itemsChanged}/{itemsProcessed} items in {stopwatch.ElapsedMilliseconds}ms " +
                        $"(Visible: {changedItems.Count}, Non-visible queued: {nonVisibleChanges.Count})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Selection update failed: {ex.Message}");
            }
            finally
            {
                _isBatchUpdateInProgress = false;
            }
        }

        /// <summary>
        /// Gets visible TreeViewItems with their data paths for efficient processing
        /// </summary>
        private Dictionary<string, TreeViewItem> GetVisibleTreeViewItemsWithPaths()
        {
            var result = new Dictionary<string, TreeViewItem>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                // Use performance manager's optimized visible items retrieval
                var visibleItems = _performanceManager.GetAllVisibleTreeViewItems()
                    .Take(100) // Limit to prevent excessive processing
                    .ToList();
                
                foreach (var item in visibleItems)
                {
                    if (item.DataContext is FileTreeItem dataItem && !string.IsNullOrEmpty(dataItem.Path))
                    {
                        result[dataItem.Path] = item;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to get visible items: {ex.Message}");
                
                // Fallback to basic approach
                foreach (var item in _performanceManager.GetAllTreeViewItemsFast().Take(50))
                {
                    if (item.DataContext is FileTreeItem dataItem && !string.IsNullOrEmpty(dataItem.Path))
                    {
                        result[dataItem.Path] = item;
                    }
                }
            }
            
            return result;
        }

        /// <summary>
        /// Applies selection changes in batches for better performance
        /// </summary>
        private void ApplySelectionChangesBatched(List<(TreeViewItem item, bool shouldBeSelected)> changes, bool isVisible)
        {
            // Use different batch sizes based on visibility
            int batchSize = isVisible ? 20 : 10; // Smaller batches for non-visible items
            var batches = changes.ChunksOf(batchSize);
            
            var priority = isVisible ? DispatcherPriority.Render : DispatcherPriority.Background;
            
            foreach (var batch in batches)
            {
                Application.Current.Dispatcher.BeginInvoke(priority, new Action(() =>
                {
                    if (_disposed) return;
                    
                    foreach (var (item, shouldBeSelected) in batch)
                    {
                        if (item != null && item.IsSelected != shouldBeSelected)
                        {
                            item.IsSelected = shouldBeSelected;
                        }
                    }
                }));
            }
        }

        /// <summary>
        /// Schedules non-visible items for background selection updates
        /// </summary>
        private void ScheduleNonVisibleSelectionUpdates(List<string> pathsToUpdate, HashSet<string> currentSelectedPaths)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (_disposed) return;
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                int updated = 0;
                
                try
                {
                    // Process in small batches to avoid blocking UI
                    var batchSize = 15;
                    var processed = 0;
                    
                    foreach (var path in pathsToUpdate)
                    {
                        if (_disposed) break;
                        
                        // Find the TreeViewItem (may not be visible/created yet)
                        var dataItem = _fileTreeService.FindItemByPath(_rootItems, path);
                        if (dataItem != null)
                        {
                            var treeViewItem = _performanceManager.GetTreeViewItemCached(dataItem);
                            if (treeViewItem != null)
                            {
                                var shouldBeSelected = currentSelectedPaths.Contains(path);
                                if (treeViewItem.IsSelected != shouldBeSelected)
                                {
                                    treeViewItem.IsSelected = shouldBeSelected;
                                    updated++;
                                }
                            }
                        }
                        
                        processed++;
                        
                        // Yield control periodically to keep UI responsive
                        if (processed % batchSize == 0)
                        {
                            Application.Current.Dispatcher.InvokeAsync(() => { }).Wait(TimeSpan.FromSeconds(5));
                        }
                    }
                    
                    stopwatch.Stop();
                    if (updated > 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SELECTION-PERF] Background update: {updated} non-visible items in {stopwatch.ElapsedMilliseconds}ms");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Background selection update failed: {ex.Message}");
                }
            }));
        }

        /// <summary>
        /// Gets performance metrics for the selection update system
        /// </summary>
        public SelectionUpdatePerformanceMetrics GetSelectionPerformanceMetrics()
        {
            return new SelectionUpdatePerformanceMetrics
            {
                LastUpdateStart = _lastSelectionUpdateStart,
                LastUpdateDuration = _lastSelectionUpdateDuration,
                LastItemsProcessed = _lastItemsProcessed,
                LastItemsChanged = _lastItemsChanged,
                PreviousStateTrackingCount = _previousSelectionState.Count,
                IsBatchUpdateInProgress = _isBatchUpdateInProgress
            };
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // FIXED: Properly unsubscribe ALL event handlers to prevent memory leaks
                try
                {
                    // Event manager events
                    _eventManager.ItemDoubleClicked -= OnItemDoubleClicked;
                    _eventManager.ItemClicked -= OnItemClicked;
                    _eventManager.ItemExpanded -= OnItemExpanded;
                    _eventManager.ContextMenuRequested -= OnContextMenuRequested;
                    _eventManager.MouseEvent -= OnMouseEvent;
                    _eventManager.KeyboardEvent -= OnKeyboardEvent;

                    // Performance manager events
                    _performanceManager.SelectionUpdateRequested -= OnSelectionUpdateRequested;

                    // Load children manager events
                    _loadChildrenManager.DirectoryLoaded -= OnDirectoryLoaded;
                    _loadChildrenManager.DirectoryLoadError -= OnDirectoryLoadError;

                    // Selection service events
                    _selectionService.SelectionChanged -= OnSelectionChanged;
                    _selectionService.PropertyChanged -= OnSelectionServicePropertyChanged;

                    // File operation handler events
                    _fileOperationHandler.DirectoryRefreshRequested -= OnDirectoryRefreshRequested;
                    _fileOperationHandler.MultipleDirectoriesRefreshRequested -= OnMultipleDirectoriesRefreshRequested;
                    _fileOperationHandler.OperationError -= OnFileOperationError;
                    _fileOperationHandler.PasteCompleted -= OnPasteCompleted;

                    // Drag drop service events
                    _dragDropService.FilesDropped -= OnFilesDropped;
                    _dragDropService.FilesMoved -= OnFilesMoved;
                    _dragDropService.ErrorOccurred -= OnDragDropError;
                    _dragDropService.OutlookExtractionCompleted -= OnOutlookExtractionCompleted;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Error unsubscribing events during disposal: {ex.Message}");
                }

                // Detach drag drop service from control
                try
                {
                    _dragDropService?.DetachFromControl();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Error detaching drag drop service: {ex.Message}");
                }

                // Dispose managers
                try
                {
                    _eventManager?.Dispose();
                    _performanceManager?.Dispose();
                    _loadChildrenManager?.Dispose();
                    
                    // Dispose the selection update timer
                    _selectionUpdateBatchTimer?.Stop();
                    _pendingSelectionUpdates?.Clear();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Error disposing managers: {ex.Message}");
                }

                // Clear event handlers - FIXED: Set to null to break references
                LocationChanged = null;
                ContextMenuRequested = null;
                FileTreeClicked = null;
                PropertyChanged = null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Performance metrics for selection updates
    /// </summary>
    public class SelectionUpdatePerformanceMetrics
    {
        public DateTime LastUpdateStart { get; set; }
        public TimeSpan LastUpdateDuration { get; set; }
        public int LastItemsProcessed { get; set; }
        public int LastItemsChanged { get; set; }
        public int PreviousStateTrackingCount { get; set; }
        public bool IsBatchUpdateInProgress { get; set; }
    }

    /// <summary>
    /// Extension methods for collection chunking
    /// </summary>
    public static class CollectionExtensions
    {
        public static IEnumerable<IEnumerable<T>> ChunksOf<T>(this IEnumerable<T> enumerable, int chunkSize)
        {
            var chunk = new List<T>(chunkSize);
            foreach (var item in enumerable)
            {
                chunk.Add(item);
                if (chunk.Count == chunkSize)
                {
                    yield return chunk;
                    chunk = new List<T>(chunkSize);
                }
            }
            if (chunk.Count > 0)
                yield return chunk;
        }
    }
}