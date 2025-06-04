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

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<string> LocationChanged;
        public event EventHandler<Tuple<string, string>> ContextMenuActionTriggered;
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

            // Initialize managers in constructor to fix readonly field assignment
            _eventManager = new FileTreeEventManager(_treeView, _fileTreeService, _selectionService);
            _performanceManager = new FileTreePerformanceManager(_treeView);
            _loadChildrenManager = new FileTreeLoadChildrenManager(_fileTreeService, _fileTreeCache);

            SetupEventHandlers();
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
                Application.Current.Dispatcher.Invoke(() => ClearCurrentData());
                
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

            if (Directory.Exists(path))
            {
                _currentFolderPath = path;
            }
            else
            {
                _currentFolderPath = Path.GetDirectoryName(path) ?? string.Empty;
            }

            LocationChanged?.Invoke(this, path);
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

        private void OnContextMenuRequested(object sender, ContextMenuEventArgs e)
        {
            // Context menu handling can be implemented here
            // For now, let the original event bubble up
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
            Application.Current.Dispatcher.Invoke(() => _ = RefreshDirectoryAsync(e.DirectoryPath));
        }

        private void OnMultipleDirectoriesRefreshRequested(object sender, MultipleDirectoriesRefreshEventArgs e)
        {
            if (_disposed) return;
            foreach (var directory in e.DirectoryPaths)
            {
                Application.Current.Dispatcher.Invoke(() => _ = RefreshDirectoryAsync(directory));
            }
        }

        private void OnFileOperationError(object sender, FileOperationErrorEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] File operation error: {e.Operation} - {e.Exception.Message}");
        }

        private void OnPasteCompleted(object sender, PasteCompletedEventArgs e)
        {
            if (_disposed) return;
            Application.Current.Dispatcher.Invoke(() => _ = RefreshDirectoryAsync(e.TargetPath));
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
            Application.Current.Dispatcher.Invoke(() => 
                MessageBox.Show(error, "Drag/Drop Error", MessageBoxButton.OK, MessageBoxImage.Warning));
        }

        private void OnOutlookExtractionCompleted(object sender, OutlookExtractionCompletedEventArgs e)
        {
            if (_disposed) return;
            
            Application.Current.Dispatcher.Invoke(() => 
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
            });
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
            
            _isUpdatingVisualSelection = true;
            _isProcessingSelection = true;
            
            try
            {
                var selectedPaths = new HashSet<string>(_selectionService.SelectedPaths, StringComparer.OrdinalIgnoreCase);
                
                foreach (var treeViewItem in _performanceManager.GetAllTreeViewItemsFast())
                {
                    if (_disposed) break;
                    
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
            finally
            {
                _isUpdatingVisualSelection = false;
                _isProcessingSelection = false;
            }
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

                // Dispose managers
                try
                {
                    _eventManager?.Dispose();
                    _performanceManager?.Dispose();
                    _loadChildrenManager?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Error disposing managers: {ex.Message}");
                }

                // Clear event handlers - FIXED: Set to null to break references
                LocationChanged = null;
                ContextMenuActionTriggered = null;
                FileTreeClicked = null;
                PropertyChanged = null;
            }
        }

        #endregion
    }
} 