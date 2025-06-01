// UI/FileTree/ImprovedFileTreeListView.xaml.cs - Fixed with proper selection synchronization
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
using ExplorerPro.Themes;
// Add alias to avoid ambiguity
using Path = System.IO.Path;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Interaction logic for ImprovedFileTreeListView.xaml with fixed selection synchronization
    /// </summary>
    public partial class ImprovedFileTreeListView : UserControl, IFileTree, IDisposable, INotifyPropertyChanged
    {
        #region Services and Dependencies

        private IFileTreeService _fileTreeService;
        private IFileTreeCache _fileTreeCache;
        private IFileTreeDragDropService _dragDropService;
        private MetadataManager _metadataManager;
        private CustomFileSystemModel _fileSystemModel;
        private UndoManager _undoManager;
        private SettingsManager _settingsManager;
        private readonly IFileOperations _fileOperations;
        
        // Selection service handles all selection logic - single source of truth
        private SelectionService _selectionService;
        
        // Enhanced drag & drop service
        private FileTreeDragDropService _enhancedDragDropService;

        #endregion

        #region Property Changed Implementation
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
        
        #region Column Management - Simplified for Name Column Only
        
        private const string NAME_COLUMN_WIDTH_KEY = "file_tree.name_column_width";
        private double _nameColumnWidth = 250; // Default width
        
        #endregion

        #region Events
        
        public event EventHandler<string> LocationChanged = delegate { };
        public event EventHandler<Tuple<string, string>> ContextMenuActionTriggered = delegate { };
        public event EventHandler FileTreeClicked = delegate { };
        
        #endregion

        #region Properties
        
        /// <summary>
        /// Gets the root items collection for data binding
        /// </summary>
        public ObservableCollection<FileTreeItem> RootItems => _rootItems;
        
        /// <summary>
        /// Gets the current path being displayed
        /// </summary>
        public string CurrentPath 
        { 
            get { return _currentFolderPath; }
            private set { _currentFolderPath = value; }
        }

        /// <summary>
        /// Gets whether any items are selected (delegates to SelectionService)
        /// </summary>
        public bool HasSelectedItems => _selectionService?.HasSelection ?? false;
        
        /// <summary>
        /// Gets or sets whether to show hidden files
        /// </summary>
        public bool ShowHiddenFiles
        {
            get { return _showHiddenFiles; }
            set
            {
                if (_showHiddenFiles != value)
                {
                    _showHiddenFiles = value;
                    RefreshView();
                }
            }
        }
        
        /// <summary>
        /// Gets the selection service for data binding
        /// </summary>
        public SelectionService SelectionService => _selectionService;
        
        /// <summary>
        /// Gets whether multi-select mode is active
        /// </summary>
        public bool IsMultiSelectMode => _selectionService?.IsMultiSelectMode ?? false;
        
        /// <summary>
        /// Gets whether any items are selected
        /// </summary>
        public bool HasSelection => _selectionService?.HasSelection ?? false;
        
        /// <summary>
        /// Gets the count of selected items
        /// </summary>
        public int SelectionCount => _selectionService?.SelectionCount ?? 0;
        
        #endregion

        #region Fields
        
        private string _currentFolderPath = string.Empty;
        private bool _showHiddenFiles = false;
        private ObservableCollection<FileTreeItem> _rootItems = new ObservableCollection<FileTreeItem>();
        private ContextMenu _treeContextMenu;
        private bool _isInitialized = false;
        private bool _isHandlingDoubleClick = false;
        
        // Selection rectangle fields
        private bool _isSelectionRectangleMode = false;
        private Point _selectionStartPoint;
        private SelectionRectangleAdorner _selectionAdorner;
        private AdornerLayer _adornerLayer;
        
        // Track whether we're processing selection changes
        private bool _isProcessingSelection = false;
        
        // Track if we're in the middle of a visual update
        private bool _isUpdatingVisualSelection = false;
        
        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ImprovedFileTreeListView class
        /// </summary>
        public ImprovedFileTreeListView()
        {
            try
            {
                InitializeComponent();

                // Initialize dependencies first
                _fileOperations = new FileOperations.FileOperations();
                InitializeManagersAndModel();
                InitializeServices();

                // Create context menu
                _treeContextMenu = new ContextMenu();
                fileTreeView.ContextMenu = _treeContextMenu;

                // Set the TreeView ItemsSource
                fileTreeView.ItemsSource = _rootItems;

                // Set up event handlers
                SetupEventHandlers();

                // Initialize column management
                InitializeColumns();

                // Set DataContext to self for bindings
                DataContext = this;

                // Setup loaded event handler for final initialization
                this.Loaded += ImprovedFileTreeListView_Loaded;

                System.Diagnostics.Debug.WriteLine("[INIT] ImprovedFileTreeListView initialized with services");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing ImprovedFileTreeListView: {ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        #endregion

        #region Initialization
        
        private void InitializeManagersAndModel()
        {
            try
            {
                _metadataManager = App.MetadataManager ?? MetadataManager.Instance;
                _undoManager = App.UndoManager ?? UndoManager.Instance;
                _settingsManager = App.Settings ?? new SettingsManager();
                _fileSystemModel = new CustomFileSystemModel(_metadataManager, _undoManager, _fileOperations);
                
                System.Diagnostics.Debug.WriteLine("[DEBUG] File tree components initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to initialize file tree components: {ex.Message}");
                
                try 
                {
                    if (_metadataManager == null) _metadataManager = new MetadataManager();
                    if (_undoManager == null) _undoManager = new UndoManager();
                    if (_settingsManager == null) _settingsManager = new SettingsManager();
                    if (_fileSystemModel == null) _fileSystemModel = new CustomFileSystemModel(_metadataManager, _undoManager, _fileOperations);
                }
                catch (Exception fallbackEx) 
                {
                    System.Diagnostics.Debug.WriteLine($"[CRITICAL] Failed to create fallback components: {fallbackEx.Message}");
                    _metadataManager = new MetadataManager();
                    _undoManager = new UndoManager();
                    _settingsManager = new SettingsManager();
                    _fileSystemModel = new CustomFileSystemModel(_metadataManager, _undoManager, _fileOperations);
                }
            }
        }

        private void InitializeServices()
        {
            try
            {
                var iconProvider = new FileIconProvider(true);
                _fileTreeService = new FileTreeService(_metadataManager, iconProvider);
                _fileTreeCache = new FileTreeCacheService(1000);
                
                // Initialize selection service and bind to UI updates
                _selectionService = new SelectionService();
                _selectionService.SelectionChanged += OnSelectionChanged;
                _selectionService.PropertyChanged += SelectionService_PropertyChanged;
                
                // Initialize enhanced drag/drop service
                _enhancedDragDropService = new FileTreeDragDropService(_undoManager, _fileOperations, _selectionService);
                _enhancedDragDropService.FilesDropped += OnFilesDropped;
                _enhancedDragDropService.FilesMoved += OnFilesMoved;
                _enhancedDragDropService.ErrorOccurred += OnDragDropError;
                _enhancedDragDropService.OutlookExtractionCompleted += OnOutlookExtractionCompleted;
                
                // Attach to the tree view with GetItemFromPoint function
                _enhancedDragDropService.AttachToControl(fileTreeView, GetItemFromPoint);
                
                // Keep old service interface for compatibility
                _dragDropService = new FileTreeDragDropServiceAdapter(_enhancedDragDropService);

                _fileTreeService.ErrorOccurred += (s, error) => 
                {
                    Dispatcher.Invoke(() => MessageBox.Show(error, "File Tree Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                };

                _fileTreeCache.ItemEvicted += (s, e) => 
                {
                    System.Diagnostics.Debug.WriteLine($"[CACHE] Item evicted: {e.Key} (Reason: {e.Reason})");
                };

                System.Diagnostics.Debug.WriteLine("[DEBUG] File tree services initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to initialize services: {ex.Message}");
                throw;
            }
        }

        private void InitializeColumns()
        {
            try
            {
                // Load saved name column width from settings
                LoadNameColumnWidth();
                
                // Apply loaded width to the Name column
                if (NameColumn != null)
                {
                    NameColumn.Width = new GridLength(_nameColumnWidth);
                }
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Name column initialized with width: {_nameColumnWidth}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to initialize columns: {ex.Message}");
                // Continue with default width
            }
        }

        private void LoadNameColumnWidth()
        {
            try
            {
                var savedWidth = _settingsManager.GetSetting<double>(NAME_COLUMN_WIDTH_KEY, 250);
                if (savedWidth >= 100 && savedWidth <= 600) // Sanity check
                {
                    _nameColumnWidth = savedWidth;
                }
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Name column width loaded: {_nameColumnWidth}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load name column width: {ex.Message}");
            }
        }

        private void SaveNameColumnWidth()
        {
            try
            {
                _settingsManager.UpdateSetting(NAME_COLUMN_WIDTH_KEY, _nameColumnWidth);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Name column width saved: {_nameColumnWidth}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to save name column width: {ex.Message}");
            }
        }

        private void SetupEventHandlers()
        {
            fileTreeView.SelectedItemChanged += FileTreeView_SelectedItemChanged;
            fileTreeView.MouseDoubleClick += FileTreeView_MouseDoubleClick;
            fileTreeView.ContextMenuOpening += FileTreeView_ContextMenuOpening;
            fileTreeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(TreeViewItem_Expanded));
            
            // Mouse events for selection - these happen BEFORE drag/drop service sees them
            fileTreeView.PreviewMouseLeftButtonDown += FileTreeView_PreviewMouseLeftButtonDown;
            fileTreeView.PreviewMouseLeftButtonUp += FileTreeView_PreviewMouseLeftButtonUp;
            fileTreeView.PreviewMouseMove += FileTreeView_PreviewMouseMove;

            fileTreeView.AllowDrop = true;
            
            // Keyboard events
            fileTreeView.PreviewKeyDown += FileTreeView_PreviewKeyDown;
        }

        private void ImprovedFileTreeListView_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                
                _showHiddenFiles = _settingsManager.GetSetting("file_view.show_hidden", false);
                
                TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                fileTreeView.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
                
                // Apply theme settings on load
                RefreshThemeElements();
                
                System.Diagnostics.Debug.WriteLine("[DEBUG] FileTreeListView loaded and initialized");
            }
        }
        
        #endregion

        #region Column Resize Handler
        
        private void NameColumnSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            try
            {
                // Save the new width of the Name column
                if (NameColumn != null)
                {
                    _nameColumnWidth = NameColumn.ActualWidth;
                    SaveNameColumnWidth();
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Name column resized to: {_nameColumnWidth}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error handling column resize: {ex.Message}");
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
                bool isDarkMode = ThemeManager.Instance.IsDarkMode;
                
                // Update main grid background
                if (MainGrid != null)
                {
                    MainGrid.Background = GetResource<SolidColorBrush>("BackgroundColor");
                }
                
                // Update TreeView itself
                if (fileTreeView != null)
                {
                    fileTreeView.Background = GetResource<SolidColorBrush>("TreeViewBackground");
                    fileTreeView.BorderBrush = GetResource<SolidColorBrush>("TreeViewBorder");
                    fileTreeView.Foreground = GetResource<SolidColorBrush>("TextColor");
                    
                    // Update TreeViewItems
                    RefreshTreeViewItems();
                }
                
                // Refresh dynamic resources in DataTemplates
                RefreshDataTemplateResources();
                
                Console.WriteLine("FileTree theme elements refreshed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error refreshing file tree theme: {ex.Message}");
                // Non-critical error, continue
            }
        }

        /// <summary>
        /// Refreshes TreeViewItems with theme-appropriate styling
        /// </summary>
        private void RefreshTreeViewItems()
        {
            try
            {
                // Get theme colors for tree lines and text
                var treeLine = GetResource<SolidColorBrush>("TreeLineColor");
                var treeLineHighlight = GetResource<SolidColorBrush>("TreeLineHighlightColor");
                var textColor = GetResource<SolidColorBrush>("TextColor");
                
                // Update all TreeViewItems
                foreach (var item in VisualTreeHelperEx.FindVisualChildren<TreeViewItem>(fileTreeView))
                {
                    // Apply theme to the TreeViewItem
                    item.Foreground = textColor;
                    
                    // Find and update all TextBlocks within the item
                    foreach (var textBlock in VisualTreeHelperEx.FindVisualChildren<TextBlock>(item))
                    {
                        // Don't override custom colors from metadata
                        if (textBlock.Foreground == SystemColors.WindowTextBrush)
                        {
                            textBlock.Foreground = textColor;
                        }
                    }
                    
                    // Update tree lines in the item
                    foreach (var line in VisualTreeHelperEx.FindVisualChildren<System.Windows.Shapes.Line>(item))
                    {
                        line.Stroke = treeLine;
                        
                        // Set up mouse over handling for lines
                        if (line.Parent is UIElement parent)
                        {
                            parent.MouseEnter -= TreeLine_MouseEnter;
                            parent.MouseLeave -= TreeLine_MouseLeave;
                            
                            parent.MouseEnter += TreeLine_MouseEnter;
                            parent.MouseLeave += TreeLine_MouseLeave;
                        }
                    }
                    
                    // Update toggle buttons
                    foreach (var toggle in VisualTreeHelperEx.FindVisualChildren<ToggleButton>(item))
                    {
                        RefreshTreeViewToggleButton(toggle);
                    }
                    
                    // Update Images (file/folder icons)
                    foreach (var image in VisualTreeHelperEx.FindVisualChildren<Image>(item))
                    {
                        // Keep the image as is - just ensure it's visible
                        image.Opacity = 1.0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error refreshing tree view items: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes a TreeView toggle button (expander) with theme-appropriate styling
        /// </summary>
        private void RefreshTreeViewToggleButton(ToggleButton toggle)
        {
            try
            {
                // Set background to transparent to let hover effect work
                toggle.Background = Brushes.Transparent;
                    
                // Find the Path element for the expander arrow
                var pathElement = VisualTreeHelperEx.FindVisualChild<System.Windows.Shapes.Path>(toggle);
                if (pathElement != null)
                {
                    pathElement.Stroke = GetResource<SolidColorBrush>("TextColor");
                    pathElement.Fill = GetResource<SolidColorBrush>("TextColor");
                    
                    // Set up mouse over handling
                    toggle.MouseEnter -= ToggleButton_MouseEnter;
                    toggle.MouseLeave -= ToggleButton_MouseLeave;
                    
                    toggle.MouseEnter += ToggleButton_MouseEnter;
                    toggle.MouseLeave += ToggleButton_MouseLeave;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error refreshing toggle button: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for mouse enter on tree lines
        /// </summary>
        private void TreeLine_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                foreach (var line in VisualTreeHelperEx.FindVisualChildren<System.Windows.Shapes.Line>(sender as DependencyObject))
                {
                    line.Stroke = GetResource<SolidColorBrush>("TreeLineHighlightColor");
                }
            }
            catch { /* Ignore errors in UI effects */ }
        }

        /// <summary>
        /// Event handler for mouse leave on tree lines
        /// </summary>
        private void TreeLine_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                foreach (var line in VisualTreeHelperEx.FindVisualChildren<System.Windows.Shapes.Line>(sender as DependencyObject))
                {
                    line.Stroke = GetResource<SolidColorBrush>("TreeLineColor");
                }
            }
            catch { /* Ignore errors in UI effects */ }
        }

        /// <summary>
        /// Event handler for mouse enter on toggle buttons
        /// </summary>
        private void ToggleButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (sender is ToggleButton toggle)
                {
                    var pathElement = VisualTreeHelperEx.FindVisualChild<System.Windows.Shapes.Path>(toggle);
                    if (pathElement != null)
                    {
                        pathElement.Stroke = GetResource<SolidColorBrush>("TreeLineHighlightColor");
                        pathElement.Fill = GetResource<SolidColorBrush>("TreeLineHighlightColor");
                    }
                }
            }
            catch { /* Ignore errors in UI effects */ }
        }

        /// <summary>
        /// Event handler for mouse leave on toggle buttons
        /// </summary>
        private void ToggleButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (sender is ToggleButton toggle)
                {
                    var pathElement = VisualTreeHelperEx.FindVisualChild<System.Windows.Shapes.Path>(toggle);
                    if (pathElement != null)
                    {
                        pathElement.Stroke = GetResource<SolidColorBrush>("TextColor");
                        pathElement.Fill = GetResource<SolidColorBrush>("TextColor");
                    }
                }
            }
            catch { /* Ignore errors in UI effects */ }
        }

        /// <summary>
        /// Refreshes resources in data templates
        /// </summary>
        private void RefreshDataTemplateResources()
        {
            try
            {
                // Force a refresh of all item containers
                fileTreeView.UpdateLayout();
                
                // Refresh the items panel (if available)
                var itemsPresenter = VisualTreeHelperEx.FindVisualChild<ItemsPresenter>(fileTreeView);
                if (itemsPresenter != null)
                {
                    itemsPresenter.UpdateLayout();
                }
                
                // Explicitly update all visible TextBlocks in the tree
                var textBlocks = VisualTreeHelperEx.FindVisualChildren<TextBlock>(fileTreeView);
                foreach (var textBlock in textBlocks)
                {
                    // Don't override custom foreground colors (from metadata)
                    if (textBlock.Foreground == SystemColors.WindowTextBrush)
                    {
                        textBlock.Foreground = GetResource<SolidColorBrush>("TextColor");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error refreshing data templates: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to get a resource from the current theme
        /// </summary>
        private T GetResource<T>(string resourceKey) where T : class
        {
            try
            {
                if (Application.Current.Resources[resourceKey] is T resource)
                {
                    return resource;
                }
                
                // Try ThemeManager as a fallback for resources
                return ThemeManager.Instance.GetResource<T>(resourceKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error getting resource '{resourceKey}': {ex.Message}");
            }
            
            // Default values for common types
            bool isDarkMode = ThemeManager.Instance.IsDarkMode;
            
            if (typeof(T) == typeof(SolidColorBrush))
            {
                if (resourceKey.Contains("Background"))
                    return new SolidColorBrush(isDarkMode ? Colors.Black : Colors.White) as T;
                if (resourceKey.Contains("Foreground") || resourceKey.Contains("Text"))
                    return new SolidColorBrush(isDarkMode ? Colors.LightGray : Colors.Black) as T;
                if (resourceKey.Contains("Border"))
                    return new SolidColorBrush(isDarkMode ? Colors.DarkGray : Colors.LightGray) as T;
                if (resourceKey.Contains("Line"))
                    return new SolidColorBrush(isDarkMode ? Colors.DarkGray : Colors.LightGray) as T;
            }
            
            return default;
        }

        #endregion

        #region IFileTree Implementation
        
        public string GetCurrentPath()
        {
            return _currentFolderPath;
        }
        
        public string? GetSelectedPath()
        {
            return _selectionService?.FirstSelectedPath ?? 
                   (fileTreeView.SelectedItem as FileTreeItem)?.Path;
        }
        
        public IReadOnlyList<string> GetSelectedPaths()
        {
            return _selectionService?.SelectedPaths ?? new List<string>();
        }

        public string GetSelectedFolderPath()
        {
            var selected = GetSelectedPath();
            if (string.IsNullOrEmpty(selected))
                return _currentFolderPath;
                
            if (Directory.Exists(selected))
                return selected;
                
            return Path.GetDirectoryName(selected) ?? _currentFolderPath;
        }

        public void RefreshView()
        {
            RefreshTreeView();
        }
        
        public void RefreshDirectory(string directoryPath)
        {
            RefreshDirectoryAsync(directoryPath).Wait();
        }

        public void SelectItem(string path)
        {
            SelectItemByPath(path);
        }
        
        public void SelectItems(IEnumerable<string> paths)
        {
            if (paths == null || !paths.Any())
                return;
                
            _selectionService.ClearSelection();
            
            foreach (var path in paths)
            {
                var item = _fileTreeService.FindItemByPath(_rootItems, path);
                if (item != null)
                {
                    _selectionService.ToggleSelection(item);
                }
            }
            
            UpdateTreeViewSelection();
        }
        
        public void SelectAll()
        {
            _selectionService.SelectAll(_rootItems);
            UpdateTreeViewSelection();
        }
        
        public void InvertSelection()
        {
            _selectionService.InvertSelection(_rootItems);
            UpdateTreeViewSelection();
        }
        
        public void SelectByPattern(string pattern, bool addToSelection = false)
        {
            _selectionService.SelectByPattern(pattern, _rootItems, addToSelection);
            UpdateTreeViewSelection();
        }
        
        public void ToggleMultiSelectMode()
        {
            _selectionService.StickyMultiSelectMode = !_selectionService.StickyMultiSelectMode;
        }

        public void CopySelected()
        {
            if (_selectionService?.HasSelection == true)
            {
                CopyMultipleItems(_selectionService.SelectedPaths);
            }
            else if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                CopyItem(selectedItem.Path);
            }
        }

        public void CutSelected()
        {
            CopySelected();
            DeleteSelected();
        }

        public void Paste()
        {
            string targetPath = _currentFolderPath;
            PasteItem(targetPath);
        }

        public void DeleteSelected()
        {
            if (_selectionService?.HasSelection == true)
            {
                DeleteMultipleItems(_selectionService.SelectedPaths);
            }
            else if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                DeleteItemWithUndo(selectedItem.Path);
            }
        }

        public void CreateFolder()
        {
            CreateNewFolder(_currentFolderPath);
        }

        public void CreateFile()
        {
            CreateNewFile(_currentFolderPath);
        }

        public void ToggleShowHidden()
        {
            ShowHiddenFiles = !ShowHiddenFiles;
            _settingsManager.UpdateSetting("file_view.show_hidden", ShowHiddenFiles);
        }

        public void ClearSelection()
        {
            _selectionService?.ClearSelection();
            var selectedTreeViewItem = GetSelectedTreeViewItem();
            selectedTreeViewItem?.SetValue(TreeViewItem.IsSelectedProperty, false);
        }

        public void HandleFileDrop(object data)
        {
            if (data is DataObject dataObject && dataObject.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])dataObject.GetData(DataFormats.FileDrop);
                _dragDropService.HandleExternalFileDrop(files, _currentFolderPath);
            }
        }
        
        public async void SetRootDirectory(string directory)
        {
            try 
            {
                if (string.IsNullOrEmpty(directory))
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] SetRootDirectory called with empty directory");
                    return;
                }
                
                // Normalize the path
                directory = Path.GetFullPath(directory);
                
                if (!Directory.Exists(directory))
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Directory does not exist: {directory}");
                    MessageBox.Show($"Directory does not exist: {directory}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[DEBUG] Setting root directory to: {directory}");
                
                _rootItems.Clear();
                _fileTreeCache.Clear();
                _selectionService?.ClearSelection();

                var rootItem = _fileTreeService.CreateFileTreeItem(directory, 0);
                if (rootItem != null)
                {
                    // Attach the LoadChildren event handler BEFORE adding to collection
                    rootItem.LoadChildren += async (s, e) => await LoadDirectoryContentsAsync(rootItem);
                    
                    _rootItems.Add(rootItem);
                    _fileTreeCache.SetItem(directory, rootItem);
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Root item created: {rootItem.Name}, HasChildren: {rootItem.HasChildren}");
                    
                    // Load children first
                    await LoadDirectoryContentsAsync(rootItem);
                    
                    // Update current folder path
                    _currentFolderPath = directory;
                    LocationChanged?.Invoke(this, directory);
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Root directory set to: {directory}, Children count: {rootItem.Children.Count}");
                    
                    // Defer expansion until the visual tree is generated
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
                        // Now expand the root item after the visual tree is ready
                        rootItem.IsExpanded = true;
                        
                        // Initialize tree view item levels
                        TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                        fileTreeView.UpdateLayout();
                        
                        // Ensure the root item is visible
                        if (_rootItems.Count > 0)
                        {
                            var container = fileTreeView.ItemContainerGenerator.ContainerFromItem(rootItem) as TreeViewItem;
                            container?.BringIntoView();
                        }
                    }));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to create root item for: {directory}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to set root directory: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error setting root directory: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        public void NavigateAndHighlight(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            try
            {
                if (File.Exists(path))
                {
                    string dirPath = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dirPath))
                    {
                        SetRootDirectory(dirPath);
                    }
                }
                else if (Directory.Exists(path))
                {
                    SetRootDirectory(path);
                }
                
                SelectItemByPath(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to navigate and highlight: {ex.Message}");
            }
        }
        
        public void ExpandToPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            var item = _fileTreeService.FindItemByPath(_rootItems, path);
            if (item != null)
            {
                // Expand all parent nodes
                var parent = item.Parent;
                while (parent != null)
                {
                    parent.IsExpanded = true;
                    parent = parent.Parent;
                }
                
                // Bring the item into view
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    var treeViewItem = VisualTreeHelperEx.FindTreeViewItemForData(fileTreeView, item);
                    treeViewItem?.BringIntoView();
                }));
            }
        }
        
        public void CollapseAll()
        {
            foreach (var item in _rootItems)
            {
                CollapseItemRecursive(item);
            }
        }
        
        public void ExpandAll()
        {
            foreach (var item in _rootItems)
            {
                ExpandItemRecursive(item);
            }
        }
        
        public FileTreeItem FindItemByPath(string path)
        {
            return _fileTreeService.FindItemByPath(_rootItems, path);
        }
        
        #endregion

        #region File Loading

        private async Task LoadDirectoryContentsAsync(FileTreeItem parentItem)
        {
            if (parentItem == null || !parentItem.IsDirectory)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] LoadDirectoryContentsAsync: Invalid parent item");
                return;
            }
                
            string path = parentItem.Path;
            int childLevel = parentItem.Level + 1;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Loading directory contents for: {path}");
                
                if (parentItem.Children.Count > 0 && !parentItem.HasDummyChild())
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Directory already loaded: {path}");
                    return;
                }
                
                parentItem.ClearChildren();
                parentItem.Children.Add(new FileTreeItem { Name = "Loading...", Level = childLevel });

                var children = await _fileTreeService.LoadDirectoryAsync(path, _showHiddenFiles, childLevel);

                parentItem.ClearChildren();

                foreach (var child in children)
                {
                    // Set parent reference for efficient parent/child operations
                    child.Parent = parentItem;
                    
                    parentItem.Children.Add(child);
                    _fileTreeCache.SetItem(child.Path, child);
                    
                    if (child.IsDirectory)
                    {
                        child.LoadChildren += async (s, e) => await LoadDirectoryContentsAsync(child);
                    }
                }
                
                parentItem.HasChildren = parentItem.Children.Count > 0;
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Loaded {parentItem.Children.Count} items for directory: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load directory contents: {ex.Message}");
                
                parentItem.ClearChildren();
                parentItem.Children.Add(new FileTreeItem { 
                    Name = $"Error: {ex.Message}", 
                    Level = childLevel,
                    Type = "Error"
                });
                parentItem.HasChildren = true;
            }
        }

        #endregion

        #region Event Handlers

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Don't process if we're already handling selection changes
            if (_isProcessingSelection || _isUpdatingVisualSelection)
                return;
                
            // Don't process selection changes during double-click
            if (_isHandlingDoubleClick)
                return;
                
            _isProcessingSelection = true;
            try
            {
                // Get the newly selected item
                if (e.NewValue is FileTreeItem item)
                {
                    // Only update selection if it's not already in sync
                    if (!_selectionService.StickyMultiSelectMode && 
                        _selectionService.SelectionCount <= 1 &&
                        _selectionService.FirstSelectedItem != item)
                    {
                        // Single selection mode - update selection service
                        _selectionService.SelectSingle(item);
                    }
                    
                    // Add a small delay to distinguish between single and double clicks
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        if (!_isHandlingDoubleClick)
                        {
                            OnTreeItemClicked(item.Path);
                        }
                    }));
                }
            }
            finally
            {
                _isProcessingSelection = false;
            }
        }

        private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem treeViewItem && 
                treeViewItem.DataContext is FileTreeItem item && 
                item.IsDirectory)
            {
                if (item.HasDummyChild() || item.Children.Count == 0)
                {
                    await LoadDirectoryContentsAsync(item);
                }
                
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                    TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                }));
            }
        }

        private void FileTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Get the item that was actually clicked on
            var originalSource = e.OriginalSource as DependencyObject;
            var treeViewItem = VisualTreeHelperEx.FindAncestor<TreeViewItem>(originalSource);
            
            if (treeViewItem?.DataContext is FileTreeItem item)
            {
                // Only handle double-click for files, let TreeView handle folders naturally
                if (!item.IsDirectory)
                {
                    // Set flag to prevent selection changed from creating new tabs
                    _isHandlingDoubleClick = true;
                    
                    // Open the file
                    HandleDoubleClick(item.Path);
                    
                    // Mark as handled to prevent bubbling
                    e.Handled = true;
                    
                    // Reset flag after a delay
                    Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        _isHandlingDoubleClick = false;
                    }));
                }
                // For directories, don't handle the event - let TreeView do its default behavior
            }
        }

        private void FileTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (_selectionService?.HasSelection == true)
            {
                BuildContextMenuForSelection(_selectionService.SelectedPaths);
            }
            else if (fileTreeView.SelectedItem is FileTreeItem item)
            {
                BuildContextMenu(item.Path);
            }
            else
            {
                e.Handled = true;
            }
        }

        private void FileTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _selectionStartPoint = e.GetPosition(fileTreeView);
            
            // Get the clicked item
            var item = GetItemFromPoint(_selectionStartPoint);
            
            // Check if we clicked on empty space (start selection rectangle)
            if (item == null)
            {
                // Clear selection if not holding Ctrl
                if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    _selectionService.ClearSelection();
                    UpdateTreeViewSelection();
                }
                
                // Prepare for selection rectangle
                _isSelectionRectangleMode = true;
                e.Handled = true;
            }
            else
            {
                // Check if we clicked on a checkbox
                var originalSource = e.OriginalSource as DependencyObject;
                var checkbox = VisualTreeHelperEx.FindAncestor<CheckBox>(originalSource);
                
                if (checkbox == null)
                {
                    // Handle normal item selection
                    // This will update the SelectionService, which the drag/drop service monitors
                    _selectionService.HandleSelection(item, Keyboard.Modifiers, _rootItems);
                    UpdateTreeViewSelection();
                    
                    // Don't mark as handled - let drag/drop service also see this event
                    // The drag/drop service will check if the clicked item is selected before starting drag
                }
                // If checkbox clicked, let the checkbox handler deal with it
            }
        }

        private void FileTreeView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelectionRectangleMode && _selectionAdorner != null)
            {
                // Complete selection rectangle
                CompleteSelectionRectangle();
            }
            
            _isSelectionRectangleMode = false;
            
            // Don't mark as handled - let drag/drop service also see this event
        }

        private void FileTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelectionRectangleMode && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(fileTreeView);
                var diff = currentPoint - _selectionStartPoint;
                
                // Start drawing selection rectangle if moved enough
                if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
                {
                    if (_selectionAdorner == null)
                    {
                        // Create selection rectangle adorner
                        _adornerLayer = AdornerLayer.GetAdornerLayer(fileTreeView);
                        if (_adornerLayer != null)
                        {
                            _selectionAdorner = new SelectionRectangleAdorner(fileTreeView, _selectionStartPoint);
                            _adornerLayer.Add(_selectionAdorner);
                        }
                    }
                    else
                    {
                        // Update selection rectangle
                        _selectionAdorner.UpdateEndPoint(currentPoint);
                        UpdateSelectionRectangleItems();
                    }
                    
                    // Mark as handled to prevent drag/drop when doing selection rectangle
                    e.Handled = true;
                }
            }
            
            // If not doing selection rectangle, let drag/drop service handle mouse move
        }

        private void FileTreeView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Delegate keyboard shortcuts to selection service
            if (_selectionService.HandleKeyboardShortcut(e.Key, Keyboard.Modifiers, _rootItems))
            {
                UpdateTreeViewSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                // Ctrl+Shift+A - Open select by pattern dialog
                ShowSelectByPatternDialog();
                e.Handled = true;
            }
        }
        
        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (fileTreeView.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                
                // Also ensure visual selection is updated
                if (!_isUpdatingVisualSelection)
                {
                    UpdateTreeViewSelection();
                }
            }
        }
        
        #endregion

        #region Selection Synchronization - FIXED

        /// <summary>
        /// Updates TreeViewItem selection to match SelectionService state
        /// FIXED: Now properly highlights ALL selected items, not just the first
        /// </summary>
        private void UpdateTreeViewSelection()
        {
            if (_isUpdatingVisualSelection) return;
            
            _isUpdatingVisualSelection = true;
            _isProcessingSelection = true;
            
            try
            {
                // Use dispatcher to ensure UI thread updates
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    try
                    {
                        // First pass: Clear all TreeViewItem selections
                        foreach (var tvi in GetAllTreeViewItems())
                        {
                            if (tvi.IsSelected)
                            {
                                tvi.IsSelected = false;
                            }
                        }
                        
                        // Second pass: Set IsSelected for all items in SelectionService
                        // In multi-select mode, we rely on the DataTrigger in the style to show selection
                        // based on FileTreeItem.IsSelected property
                        
                        // For single selection mode or when we need TreeViewItem.IsSelected
                        if (!_selectionService.IsMultiSelectMode && _selectionService.FirstSelectedItem != null)
                        {
                            var treeViewItem = VisualTreeHelperEx.FindTreeViewItemForData(fileTreeView, _selectionService.FirstSelectedItem);
                            if (treeViewItem != null)
                            {
                                treeViewItem.IsSelected = true;
                                treeViewItem.BringIntoView();
                            }
                        }
                        else if (_selectionService.HasSelection)
                        {
                            // In multi-select mode, still set the first item as selected for keyboard navigation
                            var firstItem = _selectionService.FirstSelectedItem;
                            if (firstItem != null)
                            {
                                var treeViewItem = VisualTreeHelperEx.FindTreeViewItemForData(fileTreeView, firstItem);
                                if (treeViewItem != null)
                                {
                                    treeViewItem.IsSelected = true;
                                    treeViewItem.BringIntoView();
                                }
                            }
                        }
                        
                        // Force visual update
                        fileTreeView.UpdateLayout();
                    }
                    finally
                    {
                        _isUpdatingVisualSelection = false;
                        _isProcessingSelection = false;
                    }
                }));
            }
            catch
            {
                _isUpdatingVisualSelection = false;
                _isProcessingSelection = false;
            }
        }

        /// <summary>
        /// Gets all TreeViewItems in the tree - improved version
        /// </summary>
        private IEnumerable<TreeViewItem> GetAllTreeViewItems()
        {
            return GetExpandedTreeViewItems(fileTreeView);
        }
        
        /// <summary>
        /// Gets all expanded TreeViewItems recursively
        /// </summary>
        private IEnumerable<TreeViewItem> GetExpandedTreeViewItems(ItemsControl parent)
        {
            if (parent == null) yield break;
            
            // Ensure containers are generated
            parent.UpdateLayout();
            
            // Force container generation if needed
            if (parent.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                parent.UpdateLayout();
                parent.ApplyTemplate();
            }
            
            for (int i = 0; i < parent.Items.Count; i++)
            {
                var container = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (container != null)
                {
                    yield return container;
                    
                    // Only recurse if expanded
                    if (container.IsExpanded)
                    {
                        foreach (var child in GetExpandedTreeViewItems(container))
                        {
                            yield return child;
                        }
                    }
                }
            }
        }

        #endregion

        #region Selection Rectangle

        private void UpdateSelectionRectangleItems()
        {
            if (_selectionAdorner == null) return;
            
            var selectionBounds = _selectionAdorner.GetSelectionBounds();
            var addToSelection = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            
            if (!addToSelection)
            {
                _selectionService.ClearSelection();
            }
            
            // Get all visible TreeViewItems and check if they intersect with selection rectangle
            var items = GetAllVisibleTreeViewItems();
            foreach (var treeViewItem in items)
            {
                if (treeViewItem.DataContext is FileTreeItem fileItem)
                {
                    var itemBounds = GetItemBounds(treeViewItem);
                    if (itemBounds.IntersectsWith(selectionBounds))
                    {
                        if (!_selectionService.SelectedPaths.Contains(fileItem.Path))
                        {
                            _selectionService.ToggleSelection(fileItem);
                        }
                    }
                }
            }
            
            UpdateTreeViewSelection();
        }

        private void CompleteSelectionRectangle()
        {
            // Remove adorner
            if (_adornerLayer != null && _selectionAdorner != null)
            {
                _adornerLayer.Remove(_selectionAdorner);
                _selectionAdorner = null;
            }
            
            // Enable multi-select mode if multiple items selected
            if (_selectionService.HasMultipleSelection && !_selectionService.IsMultiSelectMode)
            {
                _selectionService.IsMultiSelectMode = true;
            }
        }

        private IEnumerable<TreeViewItem> GetAllVisibleTreeViewItems()
        {
            return VisualTreeHelperEx.FindVisualChildren<TreeViewItem>(fileTreeView)
                .Where(item => item.IsVisible);
        }

        private Rect GetItemBounds(TreeViewItem item)
        {
            var topLeft = item.TranslatePoint(new Point(0, 0), fileTreeView);
            return new Rect(topLeft, new Size(item.ActualWidth, item.ActualHeight));
        }

        #endregion

        #region Select by Pattern

        private void ShowSelectByPatternDialog()
        {
            var dialog = new SelectByPatternDialog(Window.GetWindow(this));
            if (dialog.ShowDialog() == true)
            {
                _selectionService.SelectByPattern(
                    dialog.Pattern, 
                    dialog.IncludeSubfolders ? _rootItems : GetVisibleItems(),
                    dialog.AddToSelection);
                    
                UpdateTreeViewSelection();
            }
        }

        private IEnumerable<FileTreeItem> GetVisibleItems()
        {
            // Get only expanded items
            var result = new List<FileTreeItem>();
            GetVisibleItemsRecursive(_rootItems, result);
            return result;
        }

        private void GetVisibleItemsRecursive(IEnumerable<FileTreeItem> items, List<FileTreeItem> result)
        {
            foreach (var item in items)
            {
                result.Add(item);
                if (item.IsExpanded && item.Children != null)
                {
                    GetVisibleItemsRecursive(item.Children, result);
                }
            }
        }

        #endregion

        #region Enhanced Drag & Drop Event Handlers

        private async void OnFilesDropped(object sender, FilesDroppedEventArgs e)
        {
            await RefreshDirectoryAsync(e.TargetPath);
            
            if (e.IsInternalMove)
            {
                var sourceDirectories = e.SourceFiles
                    .Select(f => Path.GetDirectoryName(f))
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct()
                    .ToArray();
                    
                foreach (var sourceDir in sourceDirectories)
                {
                    await RefreshDirectoryAsync(sourceDir);
                }
            }
        }

        private async void OnFilesMoved(object sender, FilesMoved e)
        {
            await RefreshDirectoryAsync(e.TargetPath);
            
            foreach (var sourceDir in e.SourceDirectories)
            {
                await RefreshDirectoryAsync(sourceDir);
            }
        }

        private void OnDragDropError(object sender, string error)
        {
            Dispatcher.Invoke(() => 
                MessageBox.Show(error, "Drag/Drop Error", MessageBoxButton.OK, MessageBoxImage.Warning));
        }

        private void OnOutlookExtractionCompleted(object sender, OutlookExtractionCompletedEventArgs e)
        {
            Dispatcher.Invoke(() => 
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

        private void OnSelectionChanged(object sender, FileTreeSelectionChangedEventArgs e)
        {
            // Update any UI that depends on selection
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectionCount));
            
            // Update context menu based on selection
            if (e.SelectedItems.Count > 0)
            {
                BuildContextMenuForSelection(e.SelectedPaths);
            }
            
            // Update TreeView selection if needed
            if (!_isProcessingSelection && !_isUpdatingVisualSelection)
            {
                UpdateTreeViewSelection();
            }
        }

        private void SelectionService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Forward relevant property changes to our own PropertyChanged event
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

        #endregion

        #region UI Button Handlers

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Handle select all checkbox in header
            if (sender is CheckBox checkBox)
            {
                if (checkBox.IsChecked == true)
                {
                    _selectionService.SelectAll(_rootItems);
                }
                else
                {
                    _selectionService.ClearSelection();
                }
                
                UpdateTreeViewSelection();
                e.Handled = true;
            }
        }

        private void SelectionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Handle checkbox click for multi-selection
            if (sender is CheckBox checkBox && checkBox.Tag is FileTreeItem item)
            {
                // Use HandleCheckboxSelection which toggles based on SelectionService state
                _selectionService.HandleCheckboxSelection(item);
                e.Handled = true;
            }
        }

        #endregion

        #region Navigation and Context Menu

        private void HandleDoubleClick(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    // Open file in default application
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

        private void OnTreeItemClicked(string path)
        {
            // Don't process if we're handling a double-click
            if (_isHandlingDoubleClick)
                return;
                
            if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                return;
            }

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

        private void BuildContextMenu(string selectedPath)
        {
            try 
            {
                _treeContextMenu.Items.Clear();
    
                var contextMenuProvider = new ContextMenuProvider(_metadataManager, _undoManager);
                ContextMenu menu = contextMenuProvider.BuildContextMenu(selectedPath, 
                    (action, path) => ContextMenuActionTriggered?.Invoke(this, new Tuple<string, string>(action, path)));
                
                foreach (var item in menu.Items)
                {
                    _treeContextMenu.Items.Add(item);
                }
            }
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to build context menu: {ex.Message}");
                _treeContextMenu.Items.Clear();
                var menuItem = new MenuItem { Header = "Refresh" };
                menuItem.Click += (s, e) => RefreshView();
                _treeContextMenu.Items.Add(menuItem);
            }
        }

        private void BuildContextMenuForSelection(IReadOnlyList<string> selectedPaths)
        {
            _treeContextMenu.Items.Clear();
            
            if (selectedPaths.Count == 1)
            {
                // Single selection - use existing context menu
                BuildContextMenu(selectedPaths[0]);
            }
            else if (selectedPaths.Count > 1)
            {
                // Multi-selection context menu
                var menuItem = new MenuItem { Header = $"Delete {selectedPaths.Count} items" };
                menuItem.Click += (s, e) => DeleteMultipleItems(selectedPaths);
                _treeContextMenu.Items.Add(menuItem);
                
                menuItem = new MenuItem { Header = $"Copy {selectedPaths.Count} items" };
                menuItem.Click += (s, e) => CopyMultipleItems(selectedPaths);
                _treeContextMenu.Items.Add(menuItem);
                
                _treeContextMenu.Items.Add(new Separator());
                
                menuItem = new MenuItem { Header = "Select by Pattern..." };
                menuItem.Click += (s, e) => ShowSelectByPatternDialog();
                _treeContextMenu.Items.Add(menuItem);
                
                menuItem = new MenuItem { Header = "Invert Selection" };
                menuItem.Click += (s, e) => {
                    _selectionService.InvertSelection(_rootItems);
                    UpdateTreeViewSelection();
                };
                _treeContextMenu.Items.Add(menuItem);
                
                _treeContextMenu.Items.Add(new Separator());
                
                menuItem = new MenuItem { Header = "Clear Selection" };
                menuItem.Click += (s, e) => {
                    _selectionService.ClearSelection();
                    UpdateTreeViewSelection();
                };
                _treeContextMenu.Items.Add(menuItem);
            }
        }
        
        #endregion

        #region File Operations

        private void DeleteItemWithUndo(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                MessageBox.Show($"'{path}' does not exist.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to delete:\n{path}?", 
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var command = new DeleteItemCommand(_fileOperations, this, path);
                _undoManager.ExecuteCommand(command);
                
                RefreshParentDirectory(path);
            }
        }

        private void DeleteMultipleItems(IReadOnlyList<string> paths)
        {
            if (MessageBox.Show($"Are you sure you want to delete {paths.Count} items?", 
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var path in paths)
                {
                    var command = new DeleteItemCommand(_fileOperations, this, path);
                    _undoManager.ExecuteCommand(command);
                }
                
                // Refresh affected directories
                var directories = paths.Select(p => Path.GetDirectoryName(p))
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct();
                
                foreach (var dir in directories)
                {
                    RefreshDirectory(dir);
                }
            }
        }

        private void CopyItem(string path)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                var filePaths = new System.Collections.Specialized.StringCollection();
                filePaths.Add(path);
                Clipboard.SetFileDropList(filePaths);
            }
            else
            {
                MessageBox.Show("The selected file or folder does not exist.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyMultipleItems(IReadOnlyList<string> paths)
        {
            var filePaths = new System.Collections.Specialized.StringCollection();
            filePaths.AddRange(paths.ToArray());
            Clipboard.SetFileDropList(filePaths);
        }

        private void PasteItem(string targetPath)
        {
            if (!Directory.Exists(targetPath))
            {
                MessageBox.Show("You can only paste into a directory.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var files = Clipboard.GetFileDropList();
            if (files.Count == 0)
            {
                MessageBox.Show("No valid file path(s) in clipboard.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (string sourcePath in files)
            {
                if (File.Exists(sourcePath) || Directory.Exists(sourcePath))
                {
                    string newPath = _fileOperations.CopyItem(sourcePath, targetPath);
                    if (string.IsNullOrEmpty(newPath))
                    {
                        MessageBox.Show($"Failed to paste item: {sourcePath}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            RefreshDirectory(targetPath);
        }

        private void CreateNewFile(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show("Cannot create a file outside a folder.", "Invalid Target", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string newFileName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter file name (e.g., new_file.txt):", "Add New File", "");
            
            if (!string.IsNullOrWhiteSpace(newFileName))
            {
                var command = new CreateFileCommand(_fileOperations, this, directoryPath, newFileName);
                _undoManager.ExecuteCommand(command);
                
                RefreshDirectory(directoryPath);
            }
        }

        private void CreateNewFolder(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show("Cannot create a folder outside a directory.", "Invalid Target", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string newFolderName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter folder name:", "Add New Folder", "");
            
            if (!string.IsNullOrWhiteSpace(newFolderName))
            {
                var command = new CreateFolderCommand(_fileOperations, this, directoryPath, newFolderName);
                _undoManager.ExecuteCommand(command);
                
                RefreshDirectory(directoryPath);
            }
        }

        #endregion

        #region Drag and Drop

        // Updated GetItemFromPoint method to ensure it works correctly
        private FileTreeItem GetItemFromPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(fileTreeView, point);
            if (result == null)
                return null;
                
            DependencyObject obj = result.VisualHit;
            
            // Walk up the visual tree to find TreeViewItem
            while (obj != null && !(obj is TreeViewItem))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }
            
            if (obj is TreeViewItem treeViewItem)
            {
                return treeViewItem.DataContext as FileTreeItem;
            }
            
            return null;
        }
        
        #endregion

        #region Utility Methods

        private void RefreshParentDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            string parentDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentDir))
            {
                RefreshDirectory(parentDir);
            }
        }

        private async Task RefreshDirectoryAsync(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Refreshing directory: {directoryPath}");
                
                _fileTreeCache.RemoveWhere(kvp => kvp.Key.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase));
                
                var item = _fileTreeService.FindItemByPath(_rootItems, directoryPath);
                if (item != null)
                {
                    bool wasExpanded = item.IsExpanded;
                    
                    item.ClearChildren();
                    item.HasChildren = _fileTreeService.DirectoryHasAccessibleChildren(directoryPath, _showHiddenFiles);
                    
                    if (wasExpanded && item.HasChildren)
                    {
                        await LoadDirectoryContentsAsync(item);
                        item.IsExpanded = true;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Directory refreshed: {directoryPath}, Children: {item.Children.Count}");
                }
                else if (_rootItems.Count > 0 && string.Equals(_rootItems[0].Path, directoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    SetRootDirectory(directoryPath);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WARNING] Could not find item to refresh: {directoryPath}");
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    OnPropertyChanged(nameof(_rootItems));
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to refresh directory {directoryPath}: {ex.Message}");
            }
        }

        private void RefreshTreeView()
        {
            string currentRoot = null;
            if (_rootItems.Count > 0)
            {
                currentRoot = _rootItems[0].Path;
            }
            
            if (!string.IsNullOrEmpty(currentRoot))
            {
                SetRootDirectory(currentRoot);
            }
        }
        
        public void SelectItemByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            var item = _fileTreeService.FindItemByPath(_rootItems, path);
            if (item != null)
            {
                _selectionService.SelectSingle(item);
                UpdateTreeViewSelection();
                
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                    var treeViewItem = VisualTreeHelperEx.FindTreeViewItemForData(fileTreeView, item);
                    treeViewItem?.BringIntoView();
                }));
            }
        }
        
        private void CollapseItemRecursive(FileTreeItem item)
        {
            item.IsExpanded = false;
            foreach (var child in item.Children)
            {
                CollapseItemRecursive(child);
            }
        }
        
        private void ExpandItemRecursive(FileTreeItem item)
        {
            item.IsExpanded = true;
            foreach (var child in item.Children)
            {
                if (child.IsDirectory)
                {
                    ExpandItemRecursive(child);
                }
            }
        }

        private TreeViewItem GetSelectedTreeViewItem()
        {
            var items = VisualTreeHelperEx.FindVisualChildren<TreeViewItem>(fileTreeView);
            return items.FirstOrDefault(item => item.IsSelected);
        }

        // Add this method to properly handle visual updates during drag operations
        private void RefreshDropTargetVisuals()
        {
            // Force a visual update on the tree view
            fileTreeView.UpdateLayout();
            
            // Ensure all TreeViewItems refresh their visual states
            foreach (var item in VisualTreeHelperEx.FindVisualChildren<TreeViewItem>(fileTreeView))
            {
                item.InvalidateVisual();
            }
        }
        
        #endregion

        #region IDisposable Implementation
        
        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Save name column width before disposal
                    SaveNameColumnWidth();
                    
                    // Cancel any ongoing operations
                    _dragDropService?.CancelOutlookExtraction();
                    
                    // Dispose enhanced services
                    _enhancedDragDropService?.DetachFromControl();
                    _enhancedDragDropService?.Dispose();
                    
                    // Clean up selection adorner if active
                    if (_selectionAdorner != null && _adornerLayer != null)
                    {
                        _adornerLayer.Remove(_selectionAdorner);
                        _selectionAdorner = null;
                    }
                    
                    // Unsubscribe from selection service events
                    if (_selectionService != null)
                    {
                        _selectionService.SelectionChanged -= OnSelectionChanged;
                        _selectionService.PropertyChanged -= SelectionService_PropertyChanged;
                        _selectionService.Dispose();
                    }
                    
                    if (fileTreeView != null)
                    {
                        fileTreeView.ItemContainerGenerator.StatusChanged -= ItemContainerGenerator_StatusChanged;
                    }
                    
                    _rootItems.Clear();
                    _fileTreeCache?.Clear();
                    (_fileTreeCache as IDisposable)?.Dispose();
                }
                
                _disposed = true;
            }
        }
        
        #endregion
    }
}