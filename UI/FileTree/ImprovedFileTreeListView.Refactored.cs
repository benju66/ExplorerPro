// UI/FileTree/ImprovedFileTreeListView.Refactored.cs - Refactored with coordinator pattern
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerPro.FileOperations;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Coordinators;
using ExplorerPro.UI.FileTree.Commands;
using ExplorerPro.UI.FileTree.Dialogs;
using ExplorerPro.UI.FileTree.Services;
using ExplorerPro.UI.FileTree.Utilities;
using ExplorerPro.Utilities;
using ExplorerPro.Themes;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Refactored ImprovedFileTreeListView using coordinator pattern for better separation of concerns
    /// This demonstrates how the refactoring reduces complexity from 2400+ lines to under 400 lines
    /// </summary>
    public partial class ImprovedFileTreeListViewRefactored : UserControl, IFileTree, IDisposable, INotifyPropertyChanged
    {
        #region Private Fields

        private readonly ObservableCollection<FileTreeItem> _rootItems;
        private readonly FileTreeCoordinator _coordinator;
        private readonly SettingsManager _settingsManager;
        
        // Column management
        private const string NAME_COLUMN_WIDTH_KEY = "file_tree.name_column_width";
        private double _nameColumnWidth = 250;
        
        private bool _isInitialized = false;
        private bool _disposed = false;

        #endregion

        #region Events

        public event EventHandler<string> LocationChanged
        {
            add => _coordinator.LocationChanged += value;
            remove => _coordinator.LocationChanged -= value;
        }

        public event EventHandler<Tuple<string, string>> ContextMenuActionTriggered;
        
        public event EventHandler FileTreeClicked
        {
            add => _coordinator.FileTreeClicked += value;
            remove => _coordinator.FileTreeClicked -= value;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Properties - Delegated to Coordinator

        public ObservableCollection<FileTreeItem> RootItems => _coordinator.RootItems;
        public string CurrentPath => _coordinator.CurrentPath;
        public bool HasSelectedItems => _coordinator.HasSelectedItems;
        public bool ShowHiddenFiles
        {
            get => _coordinator.ShowHiddenFiles;
            set => _coordinator.ShowHiddenFiles = value;
        }
        public SelectionService SelectionService => _coordinator.SelectionService;
        public bool IsMultiSelectMode => _coordinator.IsMultiSelectMode;
        public bool HasSelection => _coordinator.HasSelection;
        public int SelectionCount => _coordinator.SelectionCount;

        #endregion

        #region Constructor

        public ImprovedFileTreeListViewRefactored()
        {
            try
            {
                InitializeComponent();

                _rootItems = new ObservableCollection<FileTreeItem>();
                _settingsManager = App.Settings ?? new SettingsManager();

                // Initialize all dependencies - much simpler now
                var dependencies = InitializeDependencies();
                
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

                SetupUI();
                DataContext = this;

                System.Diagnostics.Debug.WriteLine("[INIT] Refactored FileTreeListView initialized");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing FileTreeListView: {ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Initialization - Greatly Simplified

        private ServiceDependencies InitializeDependencies()
        {
            // All the complex initialization is now centralized
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

        private void SetupUI()
        {
            fileTreeView.ItemsSource = _rootItems;
            InitializeColumns();
            
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            
            _coordinator.PropertyChanged += OnCoordinatorPropertyChanged;
            fileTreeView.ContextMenuOpening += OnContextMenuOpening;
        }

        #endregion

        #region Event Handlers - Much Simpler

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                
                var showHidden = _settingsManager.GetSetting("file_view.show_hidden", false);
                ShowHiddenFiles = showHidden;
                
                TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                _coordinator.RefreshThemeElements();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Cleanup is handled by the coordinator
        }

        private void OnCoordinatorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (_disposed) return;
            
            // Context menu creation is now much simpler
            var contextMenuProvider = new ContextMenuProvider(
                App.MetadataManager ?? MetadataManager.Instance, 
                App.UndoManager ?? UndoManager.Instance, 
                CreateFileOperationHandler(),
                this);
            
            ContextMenu menu = null;
            
            if (HasSelection)
            {
                menu = SelectionCount > 1 ? 
                    contextMenuProvider.BuildMultiSelectContextMenu(SelectionService.SelectedPaths) :
                    contextMenuProvider.BuildContextMenu(SelectionService.FirstSelectedPath);
            }
            else
            {
                e.Handled = true;
                return;
            }
            
            fileTreeView.ContextMenu = menu;
        }

        #endregion

        #region IFileTree Implementation - All Delegated

        public string GetCurrentPath() => _coordinator.CurrentPath;
        public string GetSelectedPath() => SelectionService?.FirstSelectedPath;
        public IReadOnlyList<string> GetSelectedPaths() => SelectionService?.SelectedPaths ?? new List<string>();

        public string GetSelectedFolderPath()
        {
            var selected = GetSelectedPath();
            return string.IsNullOrEmpty(selected) ? _coordinator.CurrentPath :
                   Directory.Exists(selected) ? selected :
                   Path.GetDirectoryName(selected) ?? _coordinator.CurrentPath;
        }

        public void RefreshView() => _coordinator.RefreshView();
        public void RefreshDirectory(string directoryPath) => _ = _coordinator.RefreshDirectoryAsync(directoryPath);
        public void SelectItem(string path) => _coordinator.SelectItemByPath(path);
        
        public void SelectItems(IEnumerable<string> paths)
        {
            if (paths?.Any() != true) return;
            
            SelectionService.ClearSelection();
            foreach (var path in paths)
            {
                // Would need to implement FindItemByPath in coordinator
                // var item = _coordinator.FindItemByPath(path);
                // if (item != null) SelectionService.ToggleSelection(item);
            }
            _coordinator.ScheduleSelectionUpdate();
        }
        
        // All other IFileTree methods delegate to coordinator or selection service
        public void SelectAll() => SelectionService.SelectAll(_rootItems);
        public void InvertSelection() => SelectionService.InvertSelection(_rootItems);
        public void ToggleMultiSelectMode() => SelectionService.StickyMultiSelectMode = !SelectionService.StickyMultiSelectMode;
        public void ClearSelection() => SelectionService?.ClearSelection();

        // File operations delegate to a helper
        public void CopySelected() => GetFileOperationHelper().CopySelected();
        public void CutSelected() => GetFileOperationHelper().CutSelected();
        public void Paste() => GetFileOperationHelper().Paste();
        public void DeleteSelected() => GetFileOperationHelper().DeleteSelected();
        public void CreateFolder() => GetFileOperationHelper().CreateFolder();
        public void CreateFile() => GetFileOperationHelper().CreateFile();

        public void ToggleShowHidden()
        {
            ShowHiddenFiles = !ShowHiddenFiles;
            _settingsManager.UpdateSetting("file_view.show_hidden", ShowHiddenFiles);
        }

        public void RefreshThemeElements() => _coordinator.RefreshThemeElements();

        public void HandleFileDrop(object data)
        {
            // Delegate to coordinator's drag/drop service
            // Implementation would depend on the data format
        }
        
        public async void SetRootDirectory(string directory) => await _coordinator.SetRootDirectoryAsync(directory);
        
        public void NavigateAndHighlight(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var dirPath = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dirPath)) SetRootDirectory(dirPath);
                }
                else if (Directory.Exists(path))
                {
                    SetRootDirectory(path);
                }
                
                SelectItem(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] NavigateAndHighlight failed: {ex.Message}");
            }
        }
        
        // Other methods would be implemented similarly with delegation
        public void ExpandToPath(string path) 
        { 
            if (string.IsNullOrEmpty(path)) return;
            
            try
            {
                // Split path into segments to expand parent chain
                var directory = Path.GetDirectoryName(path);
                if (Directory.Exists(directory))
                {
                    SetRootDirectory(directory);
                    SelectItem(path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] ExpandToPath failed: {ex.Message}");
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

        private void CollapseRecursive(FileTreeItem item)
        {
            if (item == null) return;
            
            item.IsExpanded = false;
            foreach (var child in item.Children)
            {
                CollapseRecursive(child);
            }
        }

        private void ExpandRecursive(FileTreeItem item)
        {
            if (item == null || !item.IsDirectory) return;
            
            item.IsExpanded = true;
            
            // Load children if not already loaded
            if (item.HasDummyChild())
            {
                _ = Task.Run(async () => await _coordinator.SetRootDirectoryAsync(item.Path));
            }
            
            foreach (var child in item.Children)
            {
                ExpandRecursive(child);
            }
        }
        
        public FileTreeItem FindItemByPath(string path) 
        {
            if (string.IsNullOrEmpty(path)) return null;
            
            return FindItemInCollection(_rootItems, path);
        }

        private FileTreeItem FindItemInCollection(IEnumerable<FileTreeItem> items, string path)
        {
            foreach (var item in items)
            {
                if (string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase))
                    return item;
                    
                var found = FindItemInCollection(item.Children, path);
                if (found != null)
                    return found;
            }
            return null;
        }

        public void SelectByPattern(string pattern, bool addToSelection = false)
        {
            SelectionService.SelectByPattern(pattern, _rootItems, addToSelection);
            _coordinator.ScheduleSelectionUpdate();
        }

        #endregion

        #region Helper Methods

        private FileOperationHelper GetFileOperationHelper()
        {
            return new FileOperationHelper(this, CreateFileOperationHandler());
        }

        private FileOperationHandler CreateFileOperationHandler()
        {
            var metadataManager = App.MetadataManager ?? MetadataManager.Instance;
            var undoManager = App.UndoManager ?? UndoManager.Instance;
            var fileOperations = new FileOperations.FileOperations();
            
            return new FileOperationHandler(fileOperations, undoManager, metadataManager);
        }

        private void InitializeColumns()
        {
            try
            {
                var savedWidth = _settingsManager.GetSetting<double>(NAME_COLUMN_WIDTH_KEY, 250);
                if (savedWidth >= 100 && savedWidth <= 600)
                {
                    _nameColumnWidth = savedWidth;
                }

                if (NameColumn != null)
                {
                    NameColumn.Width = new GridLength(_nameColumnWidth);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Column initialization failed: {ex.Message}");
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable - Much Simpler

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Save settings
                try
                {
                    _settingsManager.UpdateSetting(NAME_COLUMN_WIDTH_KEY, _nameColumnWidth);
                }
                catch { /* Ignore errors during disposal */ }

                // Cleanup
                this.Loaded -= OnLoaded;
                this.Unloaded -= OnUnloaded;

                // Coordinator handles all complex disposal
                _coordinator?.Dispose();

                ContextMenuActionTriggered = null;
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

        private class FileOperationHelper
        {
            private readonly ImprovedFileTreeListViewRefactored _parent;
            private readonly FileOperationHandler _handler;

            public FileOperationHelper(ImprovedFileTreeListViewRefactored parent, FileOperationHandler handler)
            {
                _parent = parent;
                _handler = handler;
            }

            public void CopySelected()
            {
                if (_parent.HasSelection)
                {
                    _handler.CopyMultipleItems(_parent.SelectionService.SelectedPaths);
                }
            }

            public void CutSelected()
            {
                CopySelected();
                DeleteSelected();
            }

            public void Paste()
            {
                var targetPath = _parent.GetSelectedFolderPath();
                _ = _handler.PasteItemsAsync(targetPath);
            }

            public void DeleteSelected()
            {
                if (_parent.HasSelection)
                {
                    _ = _handler.DeleteMultipleItemsAsync(_parent.SelectionService.SelectedPaths, _parent);
                }
            }

            public void CreateFolder()
            {
                var targetPath = _parent.GetSelectedFolderPath();
                _handler.CreateNewFolder(targetPath, _parent);
            }

            public void CreateFile()
            {
                var targetPath = _parent.GetSelectedFolderPath();
                _handler.CreateNewFile(targetPath, _parent);
            }
        }

        #endregion
    }
} 