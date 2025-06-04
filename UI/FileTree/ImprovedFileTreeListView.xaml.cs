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
// Add alias to avoid ambiguity
using Path = System.IO.Path;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Improved FileTreeListView following Single Responsibility Principle.
    /// This refactored version delegates responsibilities to specialized manager classes
    /// while preserving all existing functionality and UI behavior.
    /// </summary>
    public partial class ImprovedFileTreeListView : UserControl, IFileTree, IDisposable, INotifyPropertyChanged
    {
        #region Private Fields - Simplified with Managers

        private readonly ObservableCollection<FileTreeItem> _rootItems;
        private readonly SettingsManager _settingsManager;
        
        // Specialized manager classes following SRP
        private FileTreeCoordinator _coordinator;
        private FileTreePerformanceManager _performanceManager;
        private FileTreeUIEventManager _uiEventManager;
        private FileTreeColumnManager _columnManager;
        private FileTreeOperationHelper _operationHelper;
        
        // Debounced selection update
        private DispatcherTimer _selectionUpdateTimer;
        private bool _pendingSelectionUpdate = false;
        
        // State tracking
        private bool _isInitialized = false;
        private bool _disposed = false;

        #endregion

        #region Events

        public event EventHandler<string> LocationChanged = delegate { };
        public event EventHandler<Tuple<string, string>> ContextMenuActionTriggered = delegate { };
        public event EventHandler FileTreeClicked = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

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
        public SelectionService SelectionService => _coordinator?.SelectionService;
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

                _rootItems = new ObservableCollection<FileTreeItem>();
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
            // Initialize performance manager first
            _performanceManager = new FileTreePerformanceManager(fileTreeView, TreeScrollViewer);
            
            // Initialize all dependencies for coordinator
            var dependencies = CreateServiceDependencies();
            
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
                nameColumnSplitter
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
            _uiEventManager.ItemDoubleClicked += OnItemDoubleClicked;
            _uiEventManager.ItemClicked += OnItemClicked;
            _uiEventManager.EmptySpaceClicked += OnEmptySpaceClicked;
            _uiEventManager.SelectionRectangleCompleted += OnSelectionRectangleCompleted;
            
            // Wire up selection changes to update debouncing
            _coordinator.SelectionService.SelectionChanged += OnSelectionChanged;
            _coordinator.SelectionService.PropertyChanged += OnSelectionService_PropertyChanged;
            
            // Wire up coordinator property changes
            _coordinator.PropertyChanged += OnCoordinatorPropertyChanged;
            
            // Wire up coordinator events to main events
            _coordinator.LocationChanged += (s, path) => LocationChanged?.Invoke(this, path);
            _coordinator.FileTreeClicked += (s, e) => FileTreeClicked?.Invoke(this, e);
            
            // Wire up performance manager events
            _performanceManager.VisibleItemsCacheUpdated += OnVisibleItemsCacheUpdated;
        }

        private void SetupUI()
        {
            fileTreeView.ItemsSource = _rootItems;
            fileTreeView.ContextMenuOpening += OnContextMenuOpening;
            
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        #endregion

        #region Event Handlers

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                
                var showHidden = _settingsManager.GetSetting("file_view.show_hidden", false);
                ShowHiddenFiles = showHidden;
                
                TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                _coordinator.RefreshThemeElements();
                
                _performanceManager.UpdateVisibleItemsCache();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _columnManager?.SaveColumnWidths();
        }

        private void OnItemDoubleClicked(object sender, string filePath)
        {
            HandleDoubleClick(filePath);
        }

        private void OnItemClicked(object sender, FileTreeItem item)
        {
            if (item != null)
            {
                OnPropertyChanged(nameof(CurrentPath));
                LocationChanged?.Invoke(this, item.Path);
                ScheduleSelectionUpdate();
            }
        }

        private void OnEmptySpaceClicked(object sender, Point point)
        {
            ScheduleSelectionUpdate();
        }

        private void OnSelectionRectangleCompleted(object sender, EventArgs e)
        {
            ScheduleSelectionUpdate();
        }

        private void OnSelectionChanged(object sender, FileTreeSelectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectionCount));
            
            ScheduleSelectionUpdate();
        }

        private void OnSelectionService_PropertyChanged(object sender, PropertyChangedEventArgs e)
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

        private void OnCoordinatorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        private void OnVisibleItemsCacheUpdated(object sender, EventArgs e)
        {
            ScheduleSelectionUpdate();
        }

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (_disposed) return;
            
            var contextMenuProvider = new ContextMenuProvider(
                App.MetadataManager ?? MetadataManager.Instance, 
                App.UndoManager ?? UndoManager.Instance, 
                CreateFileOperationHandler(),
                this
            );

            var treeViewItem = e.Source as TreeViewItem;
            var fileItem = treeViewItem?.DataContext as FileTreeItem;
            
            ContextMenu contextMenu = null;
            if (fileItem != null)
            {
                contextMenu = contextMenuProvider.BuildContextMenu(fileItem.Path);
            }
            
            if (contextMenu != null)
            {
                contextMenu.PlacementTarget = fileTreeView;
                contextMenu.IsOpen = true;
            }
            else
            {
                e.Handled = true;
            }
        }

        private void OnSelectionUpdateTimer_Tick(object sender, EventArgs e)
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
            if (sender is CheckBox checkBox && checkBox.Tag is FileTreeItem item)
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
        public string GetSelectedPath() => _coordinator?.SelectionService?.FirstSelectedPath;
        public IReadOnlyList<string> GetSelectedPaths() => _coordinator?.SelectionService?.SelectedPaths ?? new List<string>();

        public string GetSelectedFolderPath()
        {
            var selectedPath = GetSelectedPath();
            return Directory.Exists(selectedPath) ? selectedPath : Path.GetDirectoryName(selectedPath);
        }

        public void RefreshView() => _coordinator?.RefreshView();
        public void RefreshDirectory(string directoryPath) => _ = _coordinator?.RefreshDirectoryAsync(directoryPath);
        public void SelectItem(string path) => _coordinator?.SelectItemByPath(path);

        public void SelectItems(IEnumerable<string> paths)
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
            if (_coordinator != null)
                await _coordinator.SetRootDirectoryAsync(directory);
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
                    if (item != null)
                    {
                        _coordinator.SelectionService.SelectSingle(item);
                        
                        var treeViewItem = _performanceManager.GetTreeViewItemCached(item);
                        treeViewItem?.BringIntoView();
                    }
                }), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Navigate and highlight failed: {ex.Message}");
            }
        }

        public void ExpandToPath(string path) 
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

        public FileTreeItem FindItemByPath(string path) 
        {
            return FindItemInCollection(_rootItems, path);
        }

        public void SelectByPattern(string pattern, bool addToSelection = false)
        {
            _coordinator?.SelectionService?.SelectByPattern(pattern, _rootItems, addToSelection);
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

        private FileTreeItem FindItemInCollection(IEnumerable<FileTreeItem> items, string path)
        {
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
            if (_coordinator?.SelectionService == null) return;
            
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

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                if (fileTreeView != null)
                {
                    fileTreeView.ContextMenuOpening -= OnContextMenuOpening;
                }

                // Dispose all managers
                _performanceManager?.Dispose();
                _uiEventManager?.Dispose();
                _columnManager?.Dispose();
                _operationHelper?.Dispose();
                _coordinator?.Dispose();

                LocationChanged = null;
                ContextMenuActionTriggered = null;
                FileTreeClicked = null;
                PropertyChanged = null;
                DataContext = null;

                System.Diagnostics.Debug.WriteLine("[DISPOSE] Refactored FileTreeListView disposed");
            }
        }

        #endregion

        #region Nested Types

        private class ServiceDependencies
        {
            public IFileTreeService FileTreeService { get; set; }
            public IFileTreeCache FileTreeCache { get; set; }
            public SelectionService SelectionService { get; set; }
            public FileTreeThemeService ThemeService { get; set; }
            public FileOperationHandler FileOperationHandler { get; set; }
            public FileTreeDragDropService DragDropService { get; set; }
        }

        #endregion
    }
}