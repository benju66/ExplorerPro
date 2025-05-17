using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using ExplorerPro.Utilities;
using System.Windows.Automation.Peers;
using System.Windows.Automation;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Interaction logic for ImprovedFileTreeListView.xaml
    /// </summary>
    public partial class ImprovedFileTreeListView : UserControl, IFileTree, IDisposable, INotifyPropertyChanged
    {
        #region Property Changed Implementation
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
        
        #region Column Width Properties
        
        private double _nameColumnWidth = 250;
        public double NameColumnWidth
        {
            get { return _nameColumnWidth; }
            set
            {
                if (_nameColumnWidth != value)
                {
                    _nameColumnWidth = value;
                    OnPropertyChanged(nameof(NameColumnWidth));
                }
            }
        }
        
        private double _sizeColumnWidth = 100;
        public double SizeColumnWidth
        {
            get { return _sizeColumnWidth; }
            set
            {
                if (_sizeColumnWidth != value)
                {
                    _sizeColumnWidth = value;
                    OnPropertyChanged(nameof(SizeColumnWidth));
                }
            }
        }
        
        private double _typeColumnWidth = 120;
        public double TypeColumnWidth
        {
            get { return _typeColumnWidth; }
            set
            {
                if (_typeColumnWidth != value)
                {
                    _typeColumnWidth = value;
                    OnPropertyChanged(nameof(TypeColumnWidth));
                }
            }
        }
        
        private double _dateColumnWidth = 150;
        public double DateColumnWidth
        {
            get { return _dateColumnWidth; }
            set
            {
                if (_dateColumnWidth != value)
                {
                    _dateColumnWidth = value;
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
            get { return currentFolderPath; }
            private set { currentFolderPath = value; }
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
        
        private MetadataManager metadataManager = null!;
        private CustomFileSystemModel fileSystemModel = null!;
        private UndoManager undoManager = null!;
        private SettingsManager _settingsManager = null!;
        private string currentFolderPath = string.Empty;
        private bool _showHiddenFiles = false;
        private ObservableCollection<FileTreeItem> _rootItems = new ObservableCollection<FileTreeItem>();
        private Dictionary<string, FileTreeItem> _itemCache = new Dictionary<string, FileTreeItem>();
        private const int CacheLimit = 1000;
        private Point? dragStartPosition;
        private const double DragThreshold = 10.0;
        private readonly IFileOperations fileOperations;
        private FileIconProvider _iconProvider;
        private List<GridViewColumnHeader> _columnHeaders = new List<GridViewColumnHeader>();
        private ContextMenu treeContextMenu;
        private GridView _columnHeaderGridView;
        
        private GridViewColumn NameColumn { get; set; }
        private GridViewColumn SizeColumn { get; set; }
        private GridViewColumn TypeColumn { get; set; }
        private GridViewColumn DateColumn { get; set; }
        
        private GridViewColumnHeader _draggedColumn;
        private double _originalWidth;
        
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

                // Initialize file operations
                fileOperations = new FileOperations.FileOperations();

                // Initialize file icon provider
                _iconProvider = new FileIconProvider(true);

                // Initialize managers and model
                InitializeManagersAndModel();

                // Get the GridView from resources
                _columnHeaderGridView = (GridView)FindResource("columnHeaderGridView");

                // Create context menu
                treeContextMenu = new ContextMenu();
                fileTreeView.ContextMenu = treeContextMenu;

                // Set the TreeView ItemsSource
                fileTreeView.ItemsSource = _rootItems;

                // Set up event handlers
                fileTreeView.SelectedItemChanged += FileTreeView_SelectedItemChanged;
                fileTreeView.MouseDoubleClick += FileTreeView_MouseDoubleClick;
                fileTreeView.ContextMenuOpening += FileTreeView_ContextMenuOpening;

                // Add handlers for TreeViewItem expanded event
                fileTreeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(TreeViewItem_Expanded));

                // Mouse event handlers for drag and drop
                fileTreeView.PreviewMouseLeftButtonDown += FileTreeView_PreviewMouseLeftButtonDown;
                fileTreeView.PreviewMouseLeftButtonUp += FileTreeView_PreviewMouseLeftButtonUp;
                fileTreeView.MouseMove += FileTreeView_MouseMove;

                // Add drag/drop handlers
                fileTreeView.AllowDrop = true;
                fileTreeView.DragEnter += FileTreeView_DragEnter;
                fileTreeView.DragOver += FileTreeView_DragOver;
                fileTreeView.Drop += FileTreeView_Drop;
                fileTreeView.DragLeave += FileTreeView_DragLeave;

                // Setup column headers
                SetupColumnHeaders();

                // Setup column click events
                this.Loaded += ImprovedFileTreeListView_Loaded;

                // Set DataContext to self for bindings
                DataContext = this;

                // Add debug logging for initialization
                System.Diagnostics.Debug.WriteLine("[INIT] ImprovedFileTreeListView initialized");
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
                // Try to use the global instances first, fall back to new instances if needed
                metadataManager = App.MetadataManager ?? MetadataManager.Instance;
                undoManager = App.UndoManager ?? UndoManager.Instance;
                _settingsManager = App.Settings ?? new SettingsManager();

                // Initialize the file system model with required dependencies
                fileSystemModel = new CustomFileSystemModel(metadataManager, undoManager, fileOperations);
                
                System.Diagnostics.Debug.WriteLine("[DEBUG] File tree components initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to initialize file tree components: {ex.Message}");
                
                // Create minimal working instances as fallback
                try 
                {
                    if (metadataManager == null) metadataManager = new MetadataManager();
                    if (undoManager == null) undoManager = new UndoManager();
                    if (_settingsManager == null) _settingsManager = new SettingsManager();
                    if (fileSystemModel == null) fileSystemModel = new CustomFileSystemModel(metadataManager, undoManager, fileOperations);
                }
                catch (Exception fallbackEx) 
                {
                    System.Diagnostics.Debug.WriteLine($"[CRITICAL] Failed to create fallback components: {fallbackEx.Message}");
                    // Last resort - create absolute minimal dependencies
                    metadataManager = new MetadataManager();
                    undoManager = new UndoManager();
                    _settingsManager = new SettingsManager();
                    fileSystemModel = new CustomFileSystemModel(metadataManager, undoManager, fileOperations);
                }
            }
        }

        private void ImprovedFileTreeListView_Loaded(object sender, RoutedEventArgs e)
        {
            // Load settings
            _showHiddenFiles = _settingsManager.GetSetting("file_view.show_hidden", false);
            
            // Make column headers resizable
            MakeColumnsResizable();
            
            // Initialize TreeViewItem levels
            TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
            
            // Add handler for TreeView item creation to dynamically update levels
            fileTreeView.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
        }
        
        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (fileTreeView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                // Update TreeView item levels whenever containers are generated
                TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
            }
        }
        
        private void SetupColumnHeaders()
        {
            try
            {
                // Define the columns for the header
                NameColumn = new GridViewColumn { 
                    Header = "Name", 
                    Width = NameColumnWidth 
                };
                
                SizeColumn = new GridViewColumn { 
                    Header = "Size", 
                    Width = SizeColumnWidth 
                };
                
                TypeColumn = new GridViewColumn { 
                    Header = "Type", 
                    Width = TypeColumnWidth 
                };
                
                DateColumn = new GridViewColumn { 
                    Header = "Date Modified", 
                    Width = DateColumnWidth 
                };
                
                // Bind the column widths to the properties
                BindingOperations.SetBinding(NameColumn, GridViewColumn.WidthProperty, 
                    new Binding(nameof(NameColumnWidth)) { Source = this, Mode = BindingMode.TwoWay });
                
                BindingOperations.SetBinding(SizeColumn, GridViewColumn.WidthProperty, 
                    new Binding(nameof(SizeColumnWidth)) { Source = this, Mode = BindingMode.TwoWay });
                
                BindingOperations.SetBinding(TypeColumn, GridViewColumn.WidthProperty, 
                    new Binding(nameof(TypeColumnWidth)) { Source = this, Mode = BindingMode.TwoWay });
                
                BindingOperations.SetBinding(DateColumn, GridViewColumn.WidthProperty, 
                    new Binding(nameof(DateColumnWidth)) { Source = this, Mode = BindingMode.TwoWay });
                
                // Add columns to the GridView - ensure _columnHeaderGridView is not null
                if (_columnHeaderGridView != null)
                {
                    // Clear any existing columns first
                    _columnHeaderGridView.Columns.Clear();
                    
                    // Add the columns
                    _columnHeaderGridView.Columns.Add(NameColumn);
                    _columnHeaderGridView.Columns.Add(SizeColumn);
                    _columnHeaderGridView.Columns.Add(TypeColumn);
                    _columnHeaderGridView.Columns.Add(DateColumn);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ERROR] _columnHeaderGridView is null in SetupColumnHeaders");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to setup column headers: {ex.Message}");
            }
        }
        
        private void MakeColumnsResizable()
        {
            try
            {
                // Find all column headers
                _columnHeaders.Clear();
                
                var headerPresenter = FindVisualChild<GridViewHeaderRowPresenter>(this);
                if (headerPresenter == null)
                {
                    System.Diagnostics.Debug.WriteLine("[WARNING] GridViewHeaderRowPresenter not found");
                    return;
                }
                
                foreach (var header in FindVisualChildren<GridViewColumnHeader>(headerPresenter))
                {
                    _columnHeaders.Add(header);
                    
                    // Find the grip and add event handlers for resizing
                    if (FindVisualChild<Thumb>(header) is Thumb thumb)
                    {
                        thumb.DragStarted += ColumnHeader_DragStarted;
                        thumb.DragDelta += ColumnHeader_DragDelta;
                        thumb.DragCompleted += ColumnHeader_DragCompleted;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to make columns resizable: {ex.Message}");
            }
        }
        
        private void ColumnHeader_DragStarted(object sender, DragStartedEventArgs e)
        {
            // Find the column being resized
            if (sender is Thumb thumb)
            {
                _draggedColumn = FindAncestor<GridViewColumnHeader>(thumb);
                if (_draggedColumn != null)
                {
                    // Store its original width
                    _originalWidth = _draggedColumn.ActualWidth;
                }
            }
        }
        
        private void ColumnHeader_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_draggedColumn != null)
            {
                // Calculate new width
                double newWidth = Math.Max(20, _originalWidth + e.HorizontalChange);
                
                // Find the column index
                int index = _columnHeaders.IndexOf(_draggedColumn);
                
                // Update the appropriate property based on index
                switch (index)
                {
                    case 0:
                        NameColumnWidth = newWidth;
                        break;
                    case 1:
                        SizeColumnWidth = newWidth;
                        break;
                    case 2:
                        TypeColumnWidth = newWidth;
                        break;
                    case 3:
                        DateColumnWidth = newWidth;
                        break;
                }
            }
        }
        
        private void ColumnHeader_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _draggedColumn = null;
        }
        
        private GridViewColumn FindColumnByHeader(GridViewColumnHeader header)
        {
            // Find the column associated with this header
            int index = _columnHeaders.IndexOf(header);
            if (index >= 0 && _columnHeaderGridView != null && index < _columnHeaderGridView.Columns.Count)
            {
                return _columnHeaderGridView.Columns[index];
            }
            return null;
        }
        
        #endregion

        #region IFileTree Implementation
        
        /// <summary>
        /// Gets the current path being displayed
        /// </summary>
        public string GetCurrentPath()
        {
            return currentFolderPath;
        }
        
        /// <summary>
        /// Gets the path of the selected item.
        /// </summary>
        public string? GetSelectedPath()
        {
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                return selectedItem.Path;
            }
            return null;
        }

        /// <summary>
        /// Gets the path of the selected folder, or the parent folder if a file is selected.
        /// </summary>
        public string GetSelectedFolderPath()
        {
            var selected = GetSelectedPath();
            if (string.IsNullOrEmpty(selected))
                return currentFolderPath;
                
            if (Directory.Exists(selected))
                return selected;
                
            return Path.GetDirectoryName(selected) ?? currentFolderPath;
        }

        /// <summary>
        /// Refreshes the file tree view.
        /// </summary>
        public void RefreshView()
        {
            RefreshTreeView();
        }

        /// <summary>
        /// Selects an item in the tree by path.
        /// </summary>
        public void SelectItem(string path)
        {
            SelectItemByPath(path);
        }

        /// <summary>
        /// Copies selected items to clipboard.
        /// </summary>
        public void CopySelected()
        {
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                CopyItem(selectedItem.Path);
            }
        }

        /// <summary>
        /// Cuts selected items to clipboard.
        /// </summary>
        public void CutSelected()
        {
            // First copy the selected items to clipboard
            CopySelected();
            
            // Then delete the selected items
            DeleteSelected();
        }

        /// <summary>
        /// Pastes items from clipboard to current location.
        /// </summary>
        public void Paste()
        {
            string targetPath = currentFolderPath;
            PasteItem(targetPath);
        }

        /// <summary>
        /// Deletes the selected items.
        /// </summary>
        public void DeleteSelected()
        {
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                DeleteItemWithUndo(selectedItem.Path);
            }
        }

        /// <summary>
        /// Creates a new folder in the current directory.
        /// </summary>
        public void CreateFolder()
        {
            CreateNewFolder(currentFolderPath);
        }

        /// <summary>
        /// Creates a new file in the current directory.
        /// </summary>
        public void CreateFile()
        {
            CreateNewFile(currentFolderPath);
        }

        /// <summary>
        /// Toggles the display of hidden files.
        /// </summary>
        public void ToggleShowHidden()
        {
            ShowHiddenFiles = !ShowHiddenFiles;
            _settingsManager.UpdateSetting("file_view.show_hidden", ShowHiddenFiles);
        }

        /// <summary>
        /// Clears the current selection.
        /// </summary>
        public void ClearSelection()
        {
            // Find the currently selected item and unselect it
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                selectedItem.IsSelected = false;
            }
            
            // Alternative approach if the above doesn't work
            var selectedTreeViewItem = GetSelectedTreeViewItem();
            if (selectedTreeViewItem != null)
            {
                selectedTreeViewItem.IsSelected = false;
            }
        }

        /// <summary>
        /// Gets the currently selected TreeViewItem
        /// </summary>
        private TreeViewItem GetSelectedTreeViewItem()
        {
            var items = FindVisualChildren<TreeViewItem>(fileTreeView);
            return items.FirstOrDefault(item => item.IsSelected);
        }
        
        /// <summary>
        /// Handles files dropped on the tree view.
        /// </summary>
        public void HandleFileDrop(object data)
        {
            if (data is DataObject dataObject && dataObject.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])dataObject.GetData(DataFormats.FileDrop);
                HandleDrop(files);
            }
        }
        
        /// <summary>
        /// Sets the root directory for the tree view
        /// </summary>
        public void SetRootDirectory(string directory)
        {
            try 
            {
                if (string.IsNullOrEmpty(directory))
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Invalid directory path: null or empty");
                    return;
                }
                
                if (!Directory.Exists(directory))
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Invalid or inaccessible directory: {directory}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[DEBUG] Setting root directory to: {directory}");
                
                // Clear root items
                _rootItems.Clear();
                _itemCache.Clear();

                // Create root item
                var rootItem = CreateFileTreeItem(directory);
                if (rootItem != null)
                {
                    // Add root item to the collection
                    _rootItems.Add(rootItem);
                    
                    // Load the initial set of children
                    LoadDirectoryContents(rootItem);
                    
                    // Expand the root item - this should be done AFTER loading children
                    rootItem.IsExpanded = true;
                    
                    // Update current folder path
                    currentFolderPath = directory;
                    
                    // Notify listeners of location change
                    LocationChanged?.Invoke(this, directory);
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Root directory set to: {directory}, Children count: {rootItem.Children.Count}");
                    
                    // Log the tree state to help with debugging
                    LogTreeState();
                    
                    // Ensure tree item levels are initialized
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                        TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
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
                MessageBox.Show($"Error setting root directory: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Navigates to a path and highlights it in the tree view
        /// </summary>
        public void NavigateAndHighlight(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            try
            {
                // If it's a file, navigate to its parent directory
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
                
                // Now select the item
                SelectItemByPath(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to navigate and highlight: {ex.Message}");
            }
        }
        
        #endregion

        #region File Tree Item Creation

        /// <summary>
        /// Creates a FileTreeItem for a directory
        /// </summary>
        private FileTreeItem CreateFileTreeItem(string path)
        {
            try 
            {
                if (_itemCache.TryGetValue(path, out FileTreeItem cachedItem))
                {
                    return cachedItem;
                }
                
                var item = FileTreeItem.FromPath(path);
                
                // Apply styling from metadata
                ApplyMetadataStyling(item);
                
                // Set icon
                item.Icon = _iconProvider.GetIcon(path);
                
                // Cache the item
                if (_itemCache.Count > CacheLimit)
                {
                    _itemCache.Clear();
                }
                _itemCache[path] = item;
                
                return item;
            }
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to create file tree item: {ex.Message}");
                
                // Return a basic item if creation fails
                var fallbackItem = new FileTreeItem
                {
                    Name = Path.GetFileName(path),
                    Path = path,
                    IsDirectory = Directory.Exists(path),
                    Type = Directory.Exists(path) ? "Folder" : "File"
                };
                
                return fallbackItem;
            }
        }

        /// <summary>
        /// Applies metadata styling to a FileTreeItem
        /// </summary>
        private void ApplyMetadataStyling(FileTreeItem item)
        {
            try 
            {
                // Apply text color if set in metadata
                string colorHex = metadataManager.GetItemColor(item.Path);
                if (!string.IsNullOrEmpty(colorHex))
                {
                    try 
                    {
                        item.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
                    }
                    catch 
                    {
                        // Ignore color conversion errors
                    }
                }

                // Apply bold if set in metadata
                bool isBold = metadataManager.GetItemBold(item.Path);
                if (isBold)
                {
                    item.FontWeight = FontWeights.Bold;
                }
            }
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to apply metadata styling: {ex.Message}");
                // Continue without styling
            }
        }

        /// <summary>
        /// Loads the contents of a directory
        /// </summary>
        private void LoadDirectoryContents(FileTreeItem parentItem)
        {
            if (parentItem == null || !parentItem.IsDirectory)
            {
                System.Diagnostics.Debug.WriteLine("[ERROR] Invalid parent item for loading directory contents");
                return;
            }
                
            string path = parentItem.Path;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Loading directory contents for: {path}");
                
                // Skip if already loaded with real items (not just dummy)
                if (parentItem.Children.Count > 0 && !parentItem.HasDummyChild())
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Directory {path} already has {parentItem.Children.Count} items loaded, skipping");
                    return;
                }
                
                // Remove dummy item and clear all children
                parentItem.ClearChildren();

                // Mark as loaded in cache
                if (_itemCache.ContainsKey(path))
                {
                    _itemCache[path] = parentItem;
                }

                List<FileTreeItem> newChildren = new List<FileTreeItem>();

                // Get and sort directories first
                IEnumerable<string> directories;
                try 
                {
                    directories = Directory.GetDirectories(path).OrderBy(d => Path.GetFileName(d));
                }
                catch (UnauthorizedAccessException) 
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Access denied to directory: {path}");
                    // Add a special "Access Denied" item
                    parentItem.Children.Add(new FileTreeItem 
                    { 
                        Name = "Access Denied", 
                        Path = path + "\\Access Denied",
                        Type = "Error" 
                    });
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Error listing directories: {ex.Message}");
                    return;
                }

                // Add directories
                foreach (var dir in directories)
                {
                    try
                    {
                        // Skip hidden folders if not showing hidden files
                        if (!ShowHiddenFiles && IsHidden(dir))
                            continue;
                            
                        var dirItem = CreateFileTreeItem(dir);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Adding directory: {dir}");
                        
                        // Add to our temporary collection first
                        newChildren.Add(dirItem);
                        
                        // Make sure directory has a dummy child to show expander
                        if (dirItem.Children.Count == 0)
                        {
                            dirItem.AddDummyChild();
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible directories
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Skipping inaccessible directory: {dir}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ERROR] Error processing directory {dir}: {ex.Message}");
                        continue;
                    }
                }

                // Get and sort files
                IEnumerable<string> files;
                try 
                {
                    files = Directory.GetFiles(path).OrderBy(f => Path.GetFileName(f));
                }
                catch (UnauthorizedAccessException) 
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Access denied to files in: {path}");
                    // We can still add the directories we found
                    foreach (var item in newChildren)
                    {
                        parentItem.Children.Add(item);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Error listing files: {ex.Message}");
                    // We can still add the directories we found
                    foreach (var item in newChildren)
                    {
                        parentItem.Children.Add(item);
                    }
                    return;
                }

                // Add files
                foreach (var file in files)
                {
                    try
                    {
                        // Skip hidden files if not showing hidden files
                        if (!ShowHiddenFiles && IsHidden(file))
                            continue;
                            
                        var fileItem = CreateFileTreeItem(file);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Adding file: {file}");
                        
                        // Add to our temporary collection
                        newChildren.Add(fileItem);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible files
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Skipping inaccessible file: {file}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ERROR] Error processing file {file}: {ex.Message}");
                        continue;
                    }
                }

                // Add all children to parent at once for better performance
                foreach (var child in newChildren)
                {
                    parentItem.Children.Add(child);
                }
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Loaded {parentItem.Children.Count} items for directory: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load directory contents: {ex.Message} for path {path}");
                
                // Add dummy items back in case of failure
                parentItem.ClearChildren();
                parentItem.AddDummyChild();
            }
        }

        /// <summary>
        /// Checks if a file or directory is hidden
        /// </summary>
        private bool IsHidden(string path)
        {
            try
            {
                var attributes = File.GetAttributes(path);
                return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Logs the current state of the tree for debugging
        /// </summary>
        private void LogTreeState()
        {
            System.Diagnostics.Debug.WriteLine("Current Tree State:");
            int rootCount = _rootItems?.Count ?? 0;
            System.Diagnostics.Debug.WriteLine($"Root items count: {rootCount}");
            
            if (_rootItems != null && _rootItems.Count > 0)
            {
                var rootItem = _rootItems[0];
                System.Diagnostics.Debug.WriteLine($"Root path: {rootItem.Path}, Has children: {rootItem.Children.Count}");
                
                foreach (var child in rootItem.Children)
                {
                    System.Diagnostics.Debug.WriteLine($"  - Child: {child.Name}, Is Directory: {child.IsDirectory}, Expanded: {child.IsExpanded}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Cache size: {_itemCache?.Count ?? 0}");
        }
        
        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles TreeView selection changes
        /// </summary>
        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FileTreeItem item)
            {
                OnTreeItemClicked(item.Path);
            }
        }

        /// <summary>
        /// Handles TreeViewItem expanded events
        /// </summary>
        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem treeViewItem && 
                treeViewItem.DataContext is FileTreeItem item && 
                item.IsDirectory)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] TreeViewItem_Expanded: {item.Path}");
                
                // Check if we need to load children
                if (item.HasDummyChild() || item.Children.Count == 0)
                {
                    LoadDirectoryContents(item);
                }
                
                // Ensure child item levels are updated
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                    TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                }));
            }
        }

        /// <summary>
        /// Handles TreeView double-click
        /// </summary>
        private void FileTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (fileTreeView.SelectedItem is FileTreeItem item)
            {
                HandleDoubleClick(item.Path);
            }
        }

        /// <summary>
        /// Handles TreeView context menu opening
        /// </summary>
        private void FileTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (fileTreeView.SelectedItem is FileTreeItem item)
            {
                BuildContextMenu(item.Path);
            }
            else
            {
                e.Handled = true; // Prevent menu from showing if no item selected
            }
        }

        /// <summary>
        /// Starts drag operation when mouse is pressed
        /// </summary>
        private void FileTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStartPosition = e.GetPosition(fileTreeView);
        }

        /// <summary>
        /// Ends drag operation when mouse is released
        /// </summary>
        private void FileTreeView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            dragStartPosition = null;
        }

        /// <summary>
        /// Initiates drag operation when mouse moves with button pressed
        /// </summary>
        private void FileTreeView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && dragStartPosition.HasValue)
            {
                Point currentPosition = e.GetPosition(fileTreeView);
                Vector dragVector = currentPosition - dragStartPosition.Value;
                double dragDistance = Math.Sqrt(Math.Pow(dragVector.X, 2) + Math.Pow(dragVector.Y, 2));

                if (dragDistance > DragThreshold)
                {
                    StartDrag();
                }
            }
        }

        /// <summary>
        /// Handles TreeView drag enter events
        /// </summary>
        private void FileTreeView_DragEnter(object sender, DragEventArgs e)
        {
            HandleDragEnter(e);
        }

        /// <summary>
        /// Handles TreeView drag over events
        /// </summary>
        private void FileTreeView_DragOver(object sender, DragEventArgs e)
        {
            HandleDragOver(e);
        }

        /// <summary>
        /// Handles TreeView drop events
        /// </summary>
        private void FileTreeView_Drop(object sender, DragEventArgs e)
        {
            HandleDropEvent(e);
        }

        /// <summary>
        /// Handles TreeView drag leave events
        /// </summary>
        private void FileTreeView_DragLeave(object sender, DragEventArgs e)
        {
            Mouse.OverrideCursor = null;
            e.Handled = true;
        }
        
        #endregion

        #region Drag and Drop

        /// <summary>
        /// Starts a drag operation for selected items
        /// </summary>
        private void StartDrag()
        {
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                List<string> paths = new List<string> { selectedItem.Path };
                
                // Currently only supports single selection
                // For multi-select support, collect all selected items' paths
                
                // Create data object for drag and drop
                DataObject dataObject = new DataObject(DataFormats.FileDrop, paths.ToArray());
                
                // Start drag-drop operation
                DragDrop.DoDragDrop(fileTreeView, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
                
                // Reset drag start position
                dragStartPosition = null;
            }
        }
        
        #endregion

        #region Navigation and Context Menu

        /// <summary>
        /// Handles a double-click on a tree item
        /// </summary>
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
                // Find the item and toggle expansion
                var item = FindItemByPath(path);
                if (item != null)
                {
                    item.IsExpanded = !item.IsExpanded;
                }
            }
        }

        /// <summary>
        /// Called when a tree item is clicked
        /// </summary>
        private void OnTreeItemClicked(string path)
        {
            if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                System.Diagnostics.Debug.WriteLine("[WARNING] Clicked on an invalid path, ignoring.");
                return;
            }

            // Update current folder path
            if (Directory.Exists(path))
            {
                currentFolderPath = path;
            }
            else
            {
                currentFolderPath = Path.GetDirectoryName(path) ?? string.Empty;
            }

            // Notify listeners of location change
            LocationChanged?.Invoke(this, path);
            
            // Notify that this FileTree was clicked
            FileTreeClicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Deletes an item with undo support
        /// </summary>
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
                var command = new DeleteItemCommand(fileOperations, this, path);
                undoManager.ExecuteCommand(command);
                
                // Refresh the parent directory
                RefreshParentDirectory(path);
            }
        }

        /// <summary>
        /// Copies an item to the clipboard
        /// </summary>
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

        /// <summary>
        /// Pastes items from the clipboard
        /// </summary>
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
                    string newPath = fileOperations.CopyItem(sourcePath, targetPath);
                    if (string.IsNullOrEmpty(newPath))
                    {
                        MessageBox.Show($"Failed to paste item: {sourcePath}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show($"Clipboard file/folder does not exist: {sourcePath}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // Refresh the target directory
            RefreshDirectory(targetPath);
        }

        /// <summary>
        /// Creates a new file
        /// </summary>
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
                var command = new CreateFileCommand(fileOperations, this, directoryPath, newFileName);
                undoManager.ExecuteCommand(command);
                
                // Refresh the directory
                RefreshDirectory(directoryPath);
            }
        }

        /// <summary>
        /// Creates a new folder
        /// </summary>
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
                var command = new CreateFolderCommand(fileOperations, this, directoryPath, newFolderName);
                undoManager.ExecuteCommand(command);
                
                // Refresh the directory
                RefreshDirectory(directoryPath);
            }
        }
        
        /// <summary>
        /// Builds the context menu for a file or folder
        /// </summary>
        private void BuildContextMenu(string selectedPath)
        {
            try 
            {
                // Clear existing items
                treeContextMenu.Items.Clear();
    
                // Create a context menu provider with required dependencies
                var contextMenuProvider = new ContextMenuProvider(metadataManager, undoManager);
                
                // Build the context menu with the action handler
                ContextMenu menu = contextMenuProvider.BuildContextMenu(selectedPath, 
                    (action, path) => ContextMenuActionTriggered?.Invoke(this, new Tuple<string, string>(action, path)));
                
                // Copy all items from the built menu to our existing menu
                foreach (var item in menu.Items)
                {
                    treeContextMenu.Items.Add(item);
                }
            }
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to build context menu: {ex.Message}");
                // Create minimal context menu
                treeContextMenu.Items.Clear();
                var menuItem = new MenuItem { Header = "Refresh" };
                menuItem.Click += (s, e) => RefreshView();
                treeContextMenu.Items.Add(menuItem);
            }
        }
        
        #endregion

        #region Drag and Drop Handlers

        /// <summary>
        /// Handles drag enter events
        /// </summary>
        private void HandleDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
        
        /// <summary>
        /// Handles drag over events
        /// </summary>
        private void HandleDragOver(DragEventArgs e)
        {
            // Get the item under the cursor
            var item = GetItemFromPoint(e.GetPosition(fileTreeView));
            
            if (item != null && item.IsDirectory)
            {
                e.Effects = DragDropEffects.Copy;
                item.IsSelected = true;
                Mouse.OverrideCursor = Cursors.Arrow;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                Mouse.OverrideCursor = Cursors.No;
            }
            
            e.Handled = true;
        }

        /// <summary>
        /// Handles drop events
        /// </summary>
        private void HandleDropEvent(DragEventArgs e)
        {
            var item = GetItemFromPoint(e.GetPosition(fileTreeView));
            if (item == null || !item.IsDirectory)
            {
                e.Handled = true;
                return;
            }

            string targetPath = item.Path;

            try
            {
                // Handle file drop from Windows Explorer
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    
                    foreach (string sourcePath in files)
                    {
                        try
                        {
                            string destPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));
                            
                            if (File.Exists(sourcePath))
                            {
                                // Check for overwrite
                                if (File.Exists(destPath))
                                {
                                    if (MessageBox.Show(
                                        $"File {Path.GetFileName(sourcePath)} already exists. Overwrite?",
                                        "File Exists", MessageBoxButton.YesNo, MessageBoxImage.Question) 
                                        != MessageBoxResult.Yes)
                                    {
                                        continue;
                                    }
                                }
                                
                                File.Copy(sourcePath, destPath, true);
                            }
                            else if (Directory.Exists(sourcePath))
                            {
                                // Check for overwrite
                                if (Directory.Exists(destPath))
                                {
                                    if (MessageBox.Show(
                                        $"Folder {Path.GetFileName(sourcePath)} already exists. Merge?",
                                        "Folder Exists", MessageBoxButton.YesNo, MessageBoxImage.Question) 
                                        != MessageBoxResult.Yes)
                                    {
                                        continue;
                                    }
                                }
                                else
                                {
                                    Directory.CreateDirectory(destPath);
                                }
                                
                                // Copy directory contents
                                CopyDirectory(sourcePath, destPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error copying {sourcePath}: {ex.Message}", 
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    
                    // Refresh target directory including its children
                    RefreshDirectory(targetPath);
                    
                    // Ensure target directory is expanded and children are visible
                    if (item != null)
                    {
                        item.IsExpanded = true;
                        LoadDirectoryContents(item);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing dropped files: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            Mouse.OverrideCursor = null;
            e.Handled = true;
        }

        /// <summary>
        /// Gets the FileTreeItem at a specific point
        /// </summary>
        private FileTreeItem GetItemFromPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(fileTreeView, point);
            if (result == null)
                return null;
                
            DependencyObject obj = result.VisualHit;
            
            // Find TreeViewItem by walking up the visual tree
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
        
        /// <summary>
        /// Recursively copies a directory and its contents
        /// </summary>
        private void CopyDirectory(string sourceDirName, string destDirName)
        {
            // Create the destination directory
            Directory.CreateDirectory(destDirName);

            // Copy files
            foreach (string file in Directory.GetFiles(sourceDirName))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDirName, fileName);
                File.Copy(file, destFile, true);
            }

            // Copy subdirectories
            foreach (string dir in Directory.GetDirectories(sourceDirName))
            {
                string dirName = Path.GetFileName(dir);
                string destDir = Path.Combine(destDirName, dirName);
                CopyDirectory(dir, destDir);
            }
        }
        
        /// <summary>
        /// Handles a collection of dropped files
        /// </summary>
        private void HandleDrop(string[] files)
        {
            if (files == null || files.Length == 0)
                return;
                
            foreach (string filePath in files)
            {
                if (File.Exists(filePath) || Directory.Exists(filePath))
                {
                    string destPath = Path.Combine(currentFolderPath, Path.GetFileName(filePath));
                    
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Copy(filePath, destPath, false);
                        }
                        else if (Directory.Exists(filePath))
                        {
                            // Create destination directory
                            Directory.CreateDirectory(destPath);
                            
                            // Copy directory contents
                            CopyDirectory(filePath, destPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error handling dropped file {filePath}: {ex.Message}", 
                            "Drop Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            
            // Refresh view
            RefreshView();
        }
        
        #endregion

        #region Utility Methods

        /// <summary>
        /// Refreshes the parent directory of a path
        /// </summary>
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

        /// <summary>
        /// Refreshes a specific directory
        /// </summary>
        private void RefreshDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;
                
            // First try to find the item in the visible tree
            var item = FindItemByPath(directoryPath);
            if (item != null)
            {
                bool wasExpanded = item.IsExpanded;
                
                // Clear all children including any dummy child
                item.ClearChildren();
                
                // Add a dummy child to show expander
                item.AddDummyChild();
                
                if (wasExpanded)
                {
                    // Force a full reload by calling LoadDirectoryContents directly
                    LoadDirectoryContents(item);
                    
                    // Ensure it stays expanded
                    item.IsExpanded = true;
                }
            }
            else
            {
                // If the item is not in the visible tree, and it's the root path,
                // we need to reload the entire root
                if (_rootItems.Count > 0 && _rootItems[0].Path == directoryPath)
                {
                    SetRootDirectory(directoryPath);
                }
                else
                {
                    // Try to find and refresh its parent
                    string parentDir = Path.GetDirectoryName(directoryPath);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        RefreshDirectory(parentDir);
                    }
                }
            }
        }

        /// <summary>
        /// Refreshes the entire tree view
        /// </summary>
        private void RefreshTreeView()
        {
            // This is a more extensive refresh that might be needed after metadata changes
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

        /// <summary>
        /// Finds a FileTreeItem by path
        /// </summary>
        private FileTreeItem FindItemByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
                
            if (_itemCache.TryGetValue(path, out FileTreeItem cachedItem))
                return cachedItem;
            
            // Recursively search through the items
            foreach (var rootItem in _rootItems)
            {
                var foundItem = FindItemByPathRecursive(rootItem, path);
                if (foundItem != null)
                {
                    return foundItem;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Recursively searches for an item by path
        /// </summary>
        private FileTreeItem FindItemByPathRecursive(FileTreeItem parent, string path)
        {
            if (parent.Path == path)
                return parent;
                
            foreach (var child in parent.Children)
            {
                if (child.Path == path)
                    return child;
                    
                if (child.IsDirectory && child.Children.Count > 0)
                {
                    var foundItem = FindItemByPathRecursive(child, path);
                    if (foundItem != null)
                        return foundItem;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Selects an item by path and scrolls it into view
        /// </summary>
        public void SelectItemByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            string absPath = Path.GetFullPath(path);
            
            if (!File.Exists(absPath) && !Directory.Exists(absPath))
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Cannot navigate to non-existent path: {absPath}");
                return;
            }

            // First, expand parent directories
            string dirPath = File.Exists(absPath) ? Path.GetDirectoryName(absPath) : absPath;
            if (dirPath != null)
            {
                ExpandPathToRoot(dirPath);
            }
            
            // Now find and select the item
            var item = FindItemByPath(absPath);
            if (item != null)
            {
                item.IsSelected = true;
                
                // Ensure item is visible
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                    // Find the TreeViewItem for this item
                    var treeViewItem = FindTreeViewItemForData(fileTreeView, item);
                    treeViewItem?.BringIntoView();
                }));
                
                System.Diagnostics.Debug.WriteLine($"✅ Successfully navigated and highlighted: {absPath}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Could not find item for path: {absPath}");
            }
        }

        /// <summary>
        /// Expands all directories in a path from the root
        /// </summary>
        private void ExpandPathToRoot(string path)
        {
            string rootPath = null;
            if (_rootItems.Count > 0)
            {
                rootPath = _rootItems[0].Path;
            }
            
            if (string.IsNullOrEmpty(rootPath) || !path.StartsWith(rootPath))
            {
                return;
            }
            
            // Get relative path from root
            string relativePath = path.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] pathComponents = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            // Start at root
            string currentPath = rootPath;
            FileTreeItem currentItem = _rootItems[0];
            
            // Ensure root item is expanded
            currentItem.IsExpanded = true;
            
            // Make sure root children are loaded
            LoadDirectoryContents(currentItem);
            
            // Expand each component
            foreach (string component in pathComponents)
            {
                if (string.IsNullOrEmpty(component))
                    continue;
                    
                currentPath = Path.Combine(currentPath, component);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Expanding component: {component}, Full path: {currentPath}");
                
                // Find item in current item's children
                FileTreeItem nextItem = null;
                foreach (var child in currentItem.Children)
                {
                    if (child.Path == currentPath || string.Equals(child.Name, component, StringComparison.OrdinalIgnoreCase))
                    {
                        nextItem = child;
                        break;
                    }
                }
                
                if (nextItem == null)
                {
                    // Try to create and add the item
                    if (Directory.Exists(currentPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Creating missing directory item: {currentPath}");
                        nextItem = CreateFileTreeItem(currentPath);
                        currentItem.Children.Add(nextItem);
                    }
                    else
                    {
                        // Cannot continue
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Path component not found and not a directory: {currentPath}");
                        break;
                    }
                }
                
                // Expand if directory
                if (nextItem.IsDirectory)
                {
                    // Load children before expanding
                    LoadDirectoryContents(nextItem);
                    nextItem.IsExpanded = true;
                    currentItem = nextItem;
                }
                else
                {
                    // End of path
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Reached end of path at non-directory: {currentPath}");
                    break;
                }
            }
        }
        
        #endregion

        #region Helper Methods

        /// <summary>
        /// Find all visual children of a specific type
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
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
        
        /// <summary>
        /// Find a visual child of a specific type
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
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
        
        /// <summary>
        /// Find an ancestor of a specific type
        /// </summary>
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            
            return current as T;
        }
        
        /// <summary>
        /// Find the TreeViewItem that contains the specified data item
        /// </summary>
        private TreeViewItem FindTreeViewItemForData(ItemsControl container, object item)
        {
            if (container == null || item == null)
                return null;
                
            if (container.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
                return tvi;
                
            // Search in all generated containers
            for (int i = 0; i < container.Items.Count; i++)
            {
                var childContainer = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (childContainer != null)
                {
                    // Check if this is a TreeViewItem
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

        /// <summary>
        /// Releases resources used by this control
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources and optionally releases the managed resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Unregister event handlers
                    if (fileTreeView != null)
                    {
                        fileTreeView.ItemContainerGenerator.StatusChanged -= ItemContainerGenerator_StatusChanged;
                    }
                    
                    // Clear collections
                    _rootItems.Clear();
                    _itemCache.Clear();
                }
                
                // Set disposed flag
                _disposed = true;
            }
        }
        
        #endregion
    }
}
