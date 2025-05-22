// UI/FileTree/ImprovedFileTreeListView.xaml.cs (FIXED - Column Management and Initialization)
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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Shapes;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using ExplorerPro.Utilities;
using ExplorerPro.UI.FileTree.Services;
using ExplorerPro.UI.FileTree.Models;
using ExplorerPro.Themes;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Interaction logic for ImprovedFileTreeListView.xaml with fixed column management
    /// </summary>
    public partial class ImprovedFileTreeListView : UserControl, IFileTree, IDisposable, INotifyPropertyChanged
    {
        #region Services and Dependencies

        private IFileTreeService _fileTreeService;
        private IFileTreeCache _fileTreeCache;
        private IFileTreeDragDropService _dragDropService;
        private IFileTreeColumnService _columnService;
        private MetadataManager _metadataManager;
        private CustomFileSystemModel _fileSystemModel;
        private UndoManager _undoManager;
        private SettingsManager _settingsManager;
        private readonly IFileOperations _fileOperations;

        #endregion

        #region Property Changed Implementation
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
        
        #region Column Width Properties
        
        private double _nameColumnWidth = 250;
        private double _sizeColumnWidth = 100;
        private double _typeColumnWidth = 120;
        private double _dateColumnWidth = 150;
        
        public double NameColumnWidth
        {
            get => _nameColumnWidth;
            set
            {
                if (_nameColumnWidth != value)
                {
                    _nameColumnWidth = Math.Max(100, Math.Min(600, value)); // Enforce min/max
                    OnPropertyChanged(nameof(NameColumnWidth));
                }
            }
        }
        
        public double SizeColumnWidth
        {
            get => _sizeColumnWidth;
            set
            {
                if (_sizeColumnWidth != value)
                {
                    _sizeColumnWidth = Math.Max(60, Math.Min(150, value)); // Enforce min/max
                    OnPropertyChanged(nameof(SizeColumnWidth));
                }
            }
        }
        
        public double TypeColumnWidth
        {
            get => _typeColumnWidth;
            set
            {
                if (_typeColumnWidth != value)
                {
                    _typeColumnWidth = Math.Max(80, Math.Min(200, value)); // Enforce min/max
                    OnPropertyChanged(nameof(TypeColumnWidth));
                }
            }
        }
        
        public double DateColumnWidth
        {
            get => _dateColumnWidth;
            set
            {
                if (_dateColumnWidth != value)
                {
                    _dateColumnWidth = Math.Max(100, Math.Min(250, value)); // Enforce min/max
                    OnPropertyChanged(nameof(DateColumnWidth));
                }
            }
        }
        
        #endregion

        #region Events
        
        public event EventHandler<string> LocationChanged = delegate { };
        public event EventHandler<Tuple<string, string>> ContextMenuActionTriggered = delegate { };
        public event EventHandler FileTreeClicked = delegate { };
        
        #endregion

        #region Properties
        
        /// <summary>
        /// Gets the current path being displayed
        /// </summary>
        public string CurrentPath 
        { 
            get { return _currentFolderPath; }
            private set { _currentFolderPath = value; }
        }

        /// <summary>
        /// Gets whether any items are selected
        /// </summary>
        public bool HasSelectedItems => fileTreeView.SelectedItem != null;
        
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
        
        #endregion

        #region Fields
        
        private string _currentFolderPath = string.Empty;
        private bool _showHiddenFiles = false;
        private ObservableCollection<FileTreeItem> _rootItems = new ObservableCollection<FileTreeItem>();
        private Point? _dragStartPosition;
        private ContextMenu _treeContextMenu;
        private bool _isInitialized = false;
        
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

                // Initialize column service and setup
                InitializeColumnService();

                // Set DataContext to self for bindings
                DataContext = this;

                // Hide the progress overlay - we won't use it for Outlook extraction
                if (ProgressOverlay != null)
                {
                    ProgressOverlay.Visibility = Visibility.Collapsed;
                }

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
                _dragDropService = new FileTreeDragDropService();

                _fileTreeService.ErrorOccurred += (s, error) => 
                {
                    Dispatcher.Invoke(() => MessageBox.Show(error, "File Tree Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                };

                _dragDropService.FilesDropped += async (s, e) => 
                {
                    await RefreshDirectoryAsync(e.TargetPath);
                    
                    if (e.IsInternalMove)
                    {
                        var sourceDirectories = e.SourceFiles
                            .Select(f => System.IO.Path.GetDirectoryName(f))
                            .Where(d => !string.IsNullOrEmpty(d))
                            .Distinct()
                            .ToArray();
                            
                        foreach (var sourceDir in sourceDirectories)
                        {
                            await RefreshDirectoryAsync(sourceDir);
                        }
                    }
                };

                _dragDropService.FilesMoved += async (s, e) => 
                {
                    await RefreshDirectoryAsync(e.TargetPath);
                    
                    foreach (var sourceDir in e.SourceDirectories)
                    {
                        await RefreshDirectoryAsync(sourceDir);
                    }
                };

                _dragDropService.ErrorOccurred += (s, error) => 
                {
                    Dispatcher.Invoke(() => MessageBox.Show(error, "Drag/Drop Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                };

                // Handle Outlook extraction completion silently
                _dragDropService.OutlookExtractionCompleted += (s, e) => 
                {
                    Dispatcher.Invoke(() => 
                    {
                        // Only show error message if extraction failed
                        if (!e.Result.Success && !string.IsNullOrEmpty(e.Result.ErrorMessage))
                        {
                            MessageBox.Show(
                                $"There was a problem extracting Outlook attachments: {e.Result.ErrorMessage}",
                                "Extraction Problem",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning
                            );
                        }
                        
                        // Refresh the target directory regardless
                        _ = RefreshDirectoryAsync(e.TargetPath);
                    });
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

        private void InitializeColumnService()
        {
            try
            {
                // Create column service with settings manager
                _columnService = new FileTreeColumnService(_settingsManager);
                
                // Subscribe to column width changes
                _columnService.ColumnWidthChanged += ColumnService_ColumnWidthChanged;
                
                // Load saved column settings
                _columnService.LoadColumnSettings();
                
                // Apply loaded settings to our properties
                var nameCol = _columnService.GetColumn("Name");
                if (nameCol != null) _nameColumnWidth = nameCol.Width;
                
                var sizeCol = _columnService.GetColumn("Size");
                if (sizeCol != null) _sizeColumnWidth = sizeCol.Width;
                
                var typeCol = _columnService.GetColumn("Type");
                if (typeCol != null) _typeColumnWidth = typeCol.Width;
                
                var dateCol = _columnService.GetColumn("DateModified");
                if (dateCol != null) _dateColumnWidth = dateCol.Width;
                
                // Notify property changes so UI bindings update
                OnPropertyChanged(nameof(NameColumnWidth));
                OnPropertyChanged(nameof(SizeColumnWidth));
                OnPropertyChanged(nameof(TypeColumnWidth));
                OnPropertyChanged(nameof(DateColumnWidth));
                
                System.Diagnostics.Debug.WriteLine("[DEBUG] Column service initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to initialize column service: {ex.Message}");
                // Continue without column service - use defaults
            }
        }

        private void ColumnService_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            // Column width changes are now handled by the property setters
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Column '{e.ColumnName}' width changed from {e.OldWidth} to {e.NewWidth}");
        }

        private void SetupEventHandlers()
        {
            fileTreeView.SelectedItemChanged += FileTreeView_SelectedItemChanged;
            fileTreeView.MouseDoubleClick += FileTreeView_MouseDoubleClick;
            fileTreeView.ContextMenuOpening += FileTreeView_ContextMenuOpening;
            fileTreeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(TreeViewItem_Expanded));
            fileTreeView.PreviewMouseLeftButtonDown += FileTreeView_PreviewMouseLeftButtonDown;
            fileTreeView.PreviewMouseLeftButtonUp += FileTreeView_PreviewMouseLeftButtonUp;
            fileTreeView.MouseMove += FileTreeView_MouseMove;

            fileTreeView.AllowDrop = true;
            fileTreeView.DragEnter += FileTreeView_DragEnter;
            fileTreeView.DragOver += FileTreeView_DragOver;
            fileTreeView.Drop += FileTreeView_Drop;
            fileTreeView.DragLeave += FileTreeView_DragLeave;
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
                
                // Update column header widths after layout is complete
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    UpdateHeaderColumnWidths();
                }));
                
                System.Diagnostics.Debug.WriteLine("[DEBUG] FileTreeListView loaded and initialized");
            }
        }
        
        private void UpdateHeaderColumnWidths()
        {
            try
            {
                if (HeaderGrid != null)
                {
                    NameHeaderColumn.Width = new GridLength(NameColumnWidth, GridUnitType.Star);
                    SizeHeaderColumn.Width = new GridLength(SizeColumnWidth);
                    TypeHeaderColumn.Width = new GridLength(TypeColumnWidth);
                    DateHeaderColumn.Width = new GridLength(DateColumnWidth);
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Updated header column widths - Name: {NameColumnWidth}, Size: {SizeColumnWidth}, Type: {TypeColumnWidth}, Date: {DateColumnWidth}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to update header column widths: {ex.Message}");
            }
        }
        
        // Column splitter event handlers
        private void NameColumnSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            NameColumnWidth = NameHeaderColumn.ActualWidth;
            _columnService?.UpdateColumnWidth("Name", NameColumnWidth);
            _columnService?.SaveColumnSettings();
        }
        
        private void SizeColumnSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            SizeColumnWidth = SizeHeaderColumn.ActualWidth;
            _columnService?.UpdateColumnWidth("Size", SizeColumnWidth);
            _columnService?.SaveColumnSettings();
        }
        
        private void TypeColumnSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            TypeColumnWidth = TypeHeaderColumn.ActualWidth;
            _columnService?.UpdateColumnWidth("Type", TypeColumnWidth);
            _columnService?.SaveColumnSettings();
        }
        
        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (fileTreeView.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
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
                
                // Update header background
                if (HeaderGrid != null && HeaderGrid.Parent is Border headerBorder)
                {
                    headerBorder.Background = GetResource<SolidColorBrush>("BackgroundColor");
                    headerBorder.BorderBrush = GetResource<SolidColorBrush>("BorderColor");
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
                
                // Update progress overlay if visible
                if (ProgressOverlay != null && ProgressOverlay.Visibility == Visibility.Visible)
                {
                    ProgressOverlay.Background = GetResource<SolidColorBrush>("BackgroundColor");
                    
                    // Make background semi-transparent
                    if (ProgressOverlay.Background is SolidColorBrush brush)
                    {
                        Color color = brush.Color;
                        color.A = 200; // Make semi-transparent
                        ProgressOverlay.Background = new SolidColorBrush(color);
                    }
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
                foreach (var item in FindVisualChildren<TreeViewItem>(fileTreeView))
                {
                    // Apply theme to the TreeViewItem
                    item.Foreground = textColor;
                    
                    // Find and update all TextBlocks within the item
                    foreach (var textBlock in FindVisualChildren<TextBlock>(item))
                    {
                        // Don't override custom colors from metadata
                        if (textBlock.Foreground == SystemColors.WindowTextBrush)
                        {
                            textBlock.Foreground = textColor;
                        }
                    }
                    
                    // Update tree lines in the item
                    foreach (var line in FindVisualChildren<Line>(item))
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
                    foreach (var toggle in FindVisualChildren<ToggleButton>(item))
                    {
                        RefreshTreeViewToggleButton(toggle);
                    }
                    
                    // Update Images (file/folder icons)
                    foreach (var image in FindVisualChildren<Image>(item))
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
                var pathElement = FindVisualChild<System.Windows.Shapes.Path>(toggle);
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
                foreach (var line in FindVisualChildren<Line>(sender as DependencyObject))
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
                foreach (var line in FindVisualChildren<Line>(sender as DependencyObject))
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
                    var pathElement = FindVisualChild<System.Windows.Shapes.Path>(toggle);
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
                    var pathElement = FindVisualChild<System.Windows.Shapes.Path>(toggle);
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
                var itemsPresenter = FindVisualChild<ItemsPresenter>(fileTreeView);
                if (itemsPresenter != null)
                {
                    itemsPresenter.UpdateLayout();
                }
                
                // Explicitly update all visible TextBlocks in the tree
                var textBlocks = FindVisualChildren<TextBlock>(fileTreeView);
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
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                return selectedItem.Path;
            }
            return null;
        }

        public string GetSelectedFolderPath()
        {
            var selected = GetSelectedPath();
            if (string.IsNullOrEmpty(selected))
                return _currentFolderPath;
                
            if (Directory.Exists(selected))
                return selected;
                
            return System.IO.Path.GetDirectoryName(selected) ?? _currentFolderPath;
        }

        public void RefreshView()
        {
            RefreshTreeView();
        }

        public void SelectItem(string path)
        {
            SelectItemByPath(path);
        }

        public void CopySelected()
        {
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
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
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
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

                var rootItem = _fileTreeService.CreateFileTreeItem(directory, 0);
                if (rootItem != null)
                {
                    _rootItems.Add(rootItem);
                    _fileTreeCache.SetItem(directory, rootItem);
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Root item created: {rootItem.Name}, HasChildren: {rootItem.HasChildren}");
                    
                    await LoadDirectoryContentsAsync(rootItem);
                    rootItem.IsExpanded = true;
                    _currentFolderPath = directory;
                    LocationChanged?.Invoke(this, directory);
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Root directory set to: {directory}, Children count: {rootItem.Children.Count}");
                    
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                        TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                        fileTreeView.UpdateLayout();
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
                    string dirPath = System.IO.Path.GetDirectoryName(path);
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
            if (e.NewValue is FileTreeItem item)
            {
                OnTreeItemClicked(item.Path);
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
            if (fileTreeView.SelectedItem is FileTreeItem item)
            {
                HandleDoubleClick(item.Path);
            }
        }

        private void FileTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (fileTreeView.SelectedItem is FileTreeItem item)
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
            _dragStartPosition = e.GetPosition(fileTreeView);
        }

        private void FileTreeView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragStartPosition = null;
        }

        private void FileTreeView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _dragStartPosition.HasValue)
            {
                Point currentPosition = e.GetPosition(fileTreeView);
                Vector dragVector = currentPosition - _dragStartPosition.Value;
                double dragDistance = Math.Sqrt(Math.Pow(dragVector.X, 2) + Math.Pow(dragVector.Y, 2));

                if (dragDistance > 10.0)
                {
                    StartDrag();
                }
            }
        }

        private void FileTreeView_DragEnter(object sender, DragEventArgs e)
        {
            // No need to show progress overlay for Outlook data anymore
            // We're processing it silently like Windows Explorer
            _dragDropService.HandleDragEnter(e);
        }

        private void FileTreeView_DragOver(object sender, DragEventArgs e)
        {
            _dragDropService.HandleDragOver(e, GetItemFromPoint);
        }

        private void FileTreeView_Drop(object sender, DragEventArgs e)
        {
            string currentTreePath = _rootItems.Count > 0 ? _rootItems[0].Path : string.Empty;
            _dragDropService.HandleDrop(e, GetItemFromPoint, currentTreePath);
        }

        private void FileTreeView_DragLeave(object sender, DragEventArgs e)
        {
            _dragDropService.HandleDragLeave(e);
        }
        
        #endregion

        #region Navigation and Context Menu

        private void HandleDoubleClick(string path)
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
            else if (Directory.Exists(path))
            {
                var item = _fileTreeService.FindItemByPath(_rootItems, path);
                if (item != null)
                {
                    item.IsExpanded = !item.IsExpanded;
                }
            }
        }

        private void OnTreeItemClicked(string path)
        {
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
                _currentFolderPath = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
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

        private void StartDrag()
        {
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                var selectedPaths = new List<string> { selectedItem.Path };
                _dragDropService.StartDrag(fileTreeView, selectedPaths);
                _dragStartPosition = null;
            }
        }
        
        private FileTreeItem GetItemFromPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(fileTreeView, point);
            if (result == null)
                return null;
                
            DependencyObject obj = result.VisualHit;
            
            while (obj != null && !(obj is TreeViewItem))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }
            
            if (obj is TreeViewItem item)
            {
                return item.DataContext as FileTreeItem;
            }
            
            return null;
        }
        
        #endregion

        #region Utility Methods

        private void RefreshParentDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            string parentDir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentDir))
            {
                RefreshDirectory(parentDir);
            }
        }

        private async void RefreshDirectory(string directoryPath)
        {
            await RefreshDirectoryAsync(directoryPath);
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
                item.IsSelected = true;
                
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                    var treeViewItem = FindTreeViewItemForData(fileTreeView, item);
                    treeViewItem?.BringIntoView();
                }));
            }
        }

        private TreeViewItem GetSelectedTreeViewItem()
        {
            var items = FindVisualChildren<TreeViewItem>(fileTreeView);
            return items.FirstOrDefault(item => item.IsSelected);
        }
        
        #endregion

        #region Helper Methods

        internal static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
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
        
        internal static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t)
                {
                    return t;
                }
                
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            
            return null;
        }
        
        internal static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            
            return current as T;
        }
        
        private TreeViewItem FindTreeViewItemForData(ItemsControl container, object item)
        {
            if (container == null || item == null)
                return null;
                
            if (container.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
                return tvi;
                
            for (int i = 0; i < container.Items.Count; i++)
            {
                var childContainer = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (childContainer != null)
                {
                    var result = FindTreeViewItemForData(childContainer, item);
                    if (result != null)
                        return result;
                }
            }
            
            return null;
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
                    // Save column settings before disposal
                    _columnService?.SaveColumnSettings();
                    
                    // Unsubscribe from column service events
                    if (_columnService != null)
                    {
                        _columnService.ColumnWidthChanged -= ColumnService_ColumnWidthChanged;
                    }
                    
                    // Cancel any ongoing operations
                    _dragDropService?.CancelOutlookExtraction();
                    
                    if (fileTreeView != null)
                    {
                        fileTreeView.ItemContainerGenerator.StatusChanged -= ItemContainerGenerator_StatusChanged;
                    }
                    
                    _rootItems.Clear();
                    _fileTreeCache?.Clear();
                    (_fileTreeCache as IDisposable)?.Dispose();
                    (_columnService as IDisposable)?.Dispose();
                }
                
                _disposed = true;
            }
        }
        
        #endregion
    }
}