// UI/FileTree/FileTreeView.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading; // Added missing namespace
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using ExplorerPro.UI.Controls;
using ExplorerPro.Utilities;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Interaction logic for FileTreeView.xaml
    /// </summary>
    public partial class FileTreeListView : UserControl, IFileTree, IDisposable
    {
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
        public bool HasSelectedItems => treeListView.SelectedItem != null;
        
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
        private bool autoResizeEnabled = true;
        private ObservableCollection<FileTreeItem> _rootItems = new ObservableCollection<FileTreeItem>();
        private Dictionary<string, FileTreeItem> _itemCache = new Dictionary<string, FileTreeItem>();
        private const int CacheLimit = 1000;
        private Point? dragStartPosition;
        private const double DragThreshold = 10.0;
        private readonly IFileOperations fileOperations;
        private FileIconProvider _iconProvider;
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new instance of the FileTreeListView class
        /// </summary>
        public FileTreeListView()
        {
            InitializeComponent();
            
            // Initialize file operations
            fileOperations = new FileOperations.FileOperations();
            
            // Initialize file icon provider
            _iconProvider = new FileIconProvider(true);
            
            // Initialize managers and model
            InitializeManagersAndModel();

            // Set the TreeListView ItemsSource
            treeListView.ItemsSource = _rootItems;
            
            // Set up event handlers for the TreeListView
            treeListView.SelectedTreeItemChanged += TreeListView_SelectedItemChanged;
            treeListView.TreeItemExpanded += TreeListView_ItemExpanded;
            treeListView.MouseDoubleClick += TreeListView_MouseDoubleClick;
            treeListView.ContextMenu = treeContextMenu;
            
            // Mouse event handlers for drag and drop
            treeListView.PreviewMouseLeftButtonDown += TreeListView_PreviewMouseLeftButtonDown;
            treeListView.PreviewMouseLeftButtonUp += TreeListView_PreviewMouseLeftButtonUp;
            treeListView.MouseMove += TreeListView_MouseMove;
            
            // Add drag/drop handlers
            treeListView.AllowDrop = true;
            treeListView.DragEnter += TreeListView_DragEnter;
            treeListView.DragOver += TreeListView_DragOver;
            treeListView.Drop += TreeListView_Drop;
            treeListView.DragLeave += TreeListView_DragLeave;
            
            // Setup column click events
            this.Loaded += FileTreeListView_Loaded;
            
            // Add debug logging for initialization
            System.Diagnostics.Debug.WriteLine("[INIT] FileTreeListView initialized");
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
                try {
                    if (metadataManager == null) metadataManager = new MetadataManager();
                    if (undoManager == null) undoManager = new UndoManager();
                    if (_settingsManager == null) _settingsManager = new SettingsManager();
                    if (fileSystemModel == null) fileSystemModel = new CustomFileSystemModel(metadataManager, undoManager, fileOperations);
                }
                catch (Exception fallbackEx) {
                    System.Diagnostics.Debug.WriteLine($"[CRITICAL] Failed to create fallback components: {fallbackEx.Message}");
                    // Last resort - create absolute minimal dependencies
                    metadataManager = new MetadataManager();
                    undoManager = new UndoManager();
                    _settingsManager = new SettingsManager();
                    fileSystemModel = new CustomFileSystemModel(metadataManager, undoManager, fileOperations);
                }
            }
        }

        private void FileTreeListView_Loaded(object sender, RoutedEventArgs e)
        {
            // Load settings
            _showHiddenFiles = _settingsManager.GetSetting("file_view.show_hidden", false);
            
            // Configure columns
            AutoResizeColumns();
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
            if (treeListView.SelectedItem is FileTreeItem selectedItem)
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
            if (treeListView.SelectedItem is FileTreeItem selectedItem)
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
            if (treeListView.SelectedItem is FileTreeItem selectedItem)
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
            treeListView.SelectedItem = null;
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
            try {
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
                    
                    // Important: Ensure the LoadChildren event is wired up
                    rootItem.LoadChildren -= Item_LoadChildren; // Remove any existing handler
                    rootItem.LoadChildren += Item_LoadChildren; // Add the handler
                    
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
        
        #endregion

        #region File Tree Item Creation

        /// <summary>
        /// Creates a FileTreeItem for a directory
        /// </summary>
        private FileTreeItem CreateFileTreeItem(string path)
        {
            try {
                if (_itemCache.TryGetValue(path, out FileTreeItem cachedItem))
                {
                    // Always ensure the event handler is attached for cached items
                    cachedItem.LoadChildren -= Item_LoadChildren; // Remove any existing handler
                    cachedItem.LoadChildren += Item_LoadChildren; // Re-add handler
                    return cachedItem;
                }
                
                var item = FileTreeItem.FromPath(path);
                
                // Apply styling from metadata
                ApplyMetadataStyling(item);
                
                // Set icon
                item.Icon = _iconProvider.GetIcon(path);
                
                // Register event handler for loading children
                item.LoadChildren += Item_LoadChildren;
                
                // Cache the item
                if (_itemCache.Count > CacheLimit)
                {
                    _itemCache.Clear();
                }
                _itemCache[path] = item;
                
                return item;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to create file tree item: {ex.Message}");
                
                // Return a basic item if creation fails
                var fallbackItem = new FileTreeItem
                {
                    Name = Path.GetFileName(path),
                    Path = path,
                    IsDirectory = Directory.Exists(path),
                    Type = Directory.Exists(path) ? "Folder" : "File"
                };
                
                // Ensure the event handler is attached even for fallback items
                fallbackItem.LoadChildren += Item_LoadChildren;
                
                return fallbackItem;
            }
        }

        /// <summary>
        /// Applies metadata styling to a FileTreeItem
        /// </summary>
        private void ApplyMetadataStyling(FileTreeItem item)
        {
            try {
                // Apply text color if set in metadata
                string colorHex = metadataManager.GetItemColor(item.Path);
                if (!string.IsNullOrEmpty(colorHex))
                {
                    try {
                        item.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
                    }
                    catch {
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
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to apply metadata styling: {ex.Message}");
                // Continue without styling
            }
        }

        /// <summary>
        /// Loads children when a directory item is expanded
        /// </summary>
        private void Item_LoadChildren(object sender, EventArgs e)
        {
            if (sender is FileTreeItem item && item.IsDirectory)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] LoadChildren event fired for: {item.Path}, Current Children: {item.Children.Count}");
                
                // Only load if needed (has dummy child or no children)
                if (item.HasDummyChild() || item.Children.Count == 0)
                {
                    LoadDirectoryContents(item);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] After loading via direct event, Children count: {item.Children.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Skipping direct load for {item.Path} - already has {item.Children.Count} real children");
                }
            }
        }

        /// <summary>
        /// Loads the contents of a directory
        /// </summary>
        private void LoadDirectoryContents(FileTreeItem parentItem)
        {
            if (parentItem == null || !parentItem.IsDirectory)
                return;
                
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
                
                // Remove dummy item
                parentItem.ClearChildren();

                // Mark as loaded in cache
                if (_itemCache.ContainsKey(path))
                {
                    _itemCache[path] = parentItem;
                }

                // Get and sort directories first
                var directories = Directory.GetDirectories(path)
                    .OrderBy(d => Path.GetFileName(d));  // Use Path.GetFileName for consistent sorting

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
                        parentItem.Children.Add(dirItem);
                        
                        // Important: Ensure the dirItem has LoadChildren event wired up
                        dirItem.LoadChildren -= Item_LoadChildren; // Remove any existing handler to avoid duplicates
                        dirItem.LoadChildren += Item_LoadChildren; // Add the handler
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible directories
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Skipping inaccessible directory: {dir}");
                        continue;
                    }
                }

                // Get and sort files
                var files = Directory.GetFiles(path)
                    .OrderBy(f => Path.GetFileName(f));  // Use Path.GetFileName for consistent sorting

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
                        parentItem.Children.Add(fileItem);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible files
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Skipping inaccessible file: {file}");
                        continue;
                    }
                }

                // Ensure UI updates
                DoEvents();
                
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
        /// Allows UI to update during lengthy operations
        /// </summary>
        private void DoEvents()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        private object ExitFrame(object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
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
        /// Handles TreeListView selection changes
        /// </summary>
        private void TreeListView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FileTreeItem item)
            {
                OnTreeItemClicked(item.Path);
            }
        }

        /// <summary>
        /// Handles TreeListView item expansion
        /// </summary>
        private void TreeListView_ItemExpanded(object sender, TreeItemExpandedEventArgs e)
        {
            // It's crucial that this handler correctly identifies the FileTreeItem
            if (e.Item is FileTreeItem item && item.IsDirectory)
            {
                // Check if this is an expansion (not collapse)
                if (e.IsExpanded)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] TreeListView_ItemExpanded: Expanding directory: {item.Path}, Current Children: {item.Children.Count}, HasDummyChild: {item.HasDummyChild()}");
                    
                    // Only load if needed (has dummy child or no children)
                    if (item.HasDummyChild() || item.Children.Count == 0)
                    {
                        LoadDirectoryContents(item);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] After loading, Children count: {item.Children.Count}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Skipping load for {item.Path} - already has {item.Children.Count} real children");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] TreeListView_ItemExpanded: Collapsing directory: {item.Path}");
                }
            }
            
            if (autoResizeEnabled)
            {
                AutoResizeColumns();
            }
        }

        /// <summary>
        /// Handles TreeListView double-click
        /// </summary>
        private void TreeListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (treeListView.SelectedItem is FileTreeItem item)
            {
                HandleDoubleClick(item.Path);
            }
        }

        /// <summary>
        /// Handles TreeListView context menu opening
        /// </summary>
        private void TreeListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (treeListView.SelectedItem is FileTreeItem item)
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
        private void TreeListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStartPosition = e.GetPosition(treeListView);
        }

        /// <summary>
        /// Ends drag operation when mouse is released
        /// </summary>
        private void TreeListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            dragStartPosition = null;
        }

        /// <summary>
        /// Initiates drag operation when mouse moves with button pressed
        /// </summary>
        private void TreeListView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && dragStartPosition.HasValue)
            {
                Point currentPosition = e.GetPosition(treeListView);
                Vector dragVector = currentPosition - dragStartPosition.Value;
                double dragDistance = Math.Sqrt(Math.Pow(dragVector.X, 2) + Math.Pow(dragVector.Y, 2));

                if (dragDistance > DragThreshold)
                {
                    StartDrag();
                }
            }
        }

        /// <summary>
        /// Handles TreeListView drag enter events
        /// </summary>
        private void TreeListView_DragEnter(object sender, DragEventArgs e)
        {
            HandleDragEnter(e);
        }

        /// <summary>
        /// Handles TreeListView drag over events
        /// </summary>
        private void TreeListView_DragOver(object sender, DragEventArgs e)
        {
            HandleDragOver(e);
        }

        /// <summary>
        /// Handles TreeListView drop events
        /// </summary>
        private void TreeListView_Drop(object sender, DragEventArgs e)
        {
            HandleDropEvent(e);
        }

        /// <summary>
        /// Handles TreeListView drag leave events
        /// </summary>
        private void TreeListView_DragLeave(object sender, DragEventArgs e)
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
            if (treeListView.SelectedItem is FileTreeItem selectedItem)
            {
                List<string> paths = new List<string> { selectedItem.Path };
                
                // Currently only supports single selection
                // For multi-select support, collect all selected items' paths
                
                // Create data object for drag and drop
                DataObject dataObject = new DataObject(DataFormats.FileDrop, paths.ToArray());
                
                // Start drag-drop operation
                DragDrop.DoDragDrop(treeListView, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
                
                // Reset drag start position
                dragStartPosition = null;
            }
        }
        
        #endregion

        #region Pinned Items Management
        
        /// <summary>
        /// Event handler for when pinned items are updated
        /// </summary>
        private void OnPinnedItemsUpdated(object? sender, EventArgs e)
        {
            RefreshPinnedItems();
        }
        
        /// <summary>
        /// Refreshes the pinned items tree
        /// </summary>
        public void RefreshPinnedItems()
        {
            // Implementation would go here
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
            try {
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
            catch (Exception ex) {
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
            var item = GetItemFromPoint(e.GetPosition(treeListView));
            
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
            var item = GetItemFromPoint(e.GetPosition(treeListView));
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
                    
                    // Refresh target directory
                    if (item.IsExpanded)
                    {
                        // Force reload by toggling expanded state
                        item.IsExpanded = false;
                        item.IsExpanded = true;
                    }
                    else
                    {
                        // Expand to show contents
                        item.IsExpanded = true;
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
            var result = VisualTreeHelper.HitTest(treeListView, point);
            if (result == null)
                return null;
                
            DependencyObject obj = result.VisualHit;
            while (obj != null && !(obj is ListViewItem))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }
            
            if (obj is ListViewItem item)
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
                
            var item = FindItemByPath(directoryPath);
            if (item != null)
            {
                bool wasExpanded = item.IsExpanded;
                item.ClearChildren();
                item.AddDummyChild();
                
                if (wasExpanded)
                {
                    // Force a full reload
                    LoadDirectoryContents(item);
                    item.IsExpanded = true;
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
                ListViewItem listViewItem = GetListViewItemForObject(item);
                listViewItem?.BringIntoView();
                
                System.Diagnostics.Debug.WriteLine($"✅ Successfully navigated and highlighted: {absPath}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Could not find item for path: {absPath}");
            }
        }

        /// <summary>
        /// Navigates to a path and highlights it in the tree view.
        /// This method is called from other files that expect it to exist.
        /// </summary>
        /// <param name="path">The path to navigate to and highlight</param>
        public void NavigateAndHighlight(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            try
            {
                string absPath = Path.GetFullPath(path);
                
                // If it's a directory, set as root
                if (Directory.Exists(absPath))
                {
                    SetRootDirectory(absPath);
                    return;
                }
                
                // If it's a file, set root to parent directory and select file
                if (File.Exists(absPath))
                {
                    string parentDir = Path.GetDirectoryName(absPath);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        SetRootDirectory(parentDir);
                        
                        // Allow UI to update before selecting
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            SelectItemByPath(absPath);
                        }));
                    }
                    return;
                }
                
                // If neither file nor directory exists
                System.Diagnostics.Debug.WriteLine($"[ERROR] Path does not exist: {absPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error in NavigateAndHighlight: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a ListViewItem for a specific object
        /// </summary>
        private ListViewItem GetListViewItemForObject(object obj)
        {
            if (treeListView.ItemContainerGenerator.ContainerFromItem(obj) is ListViewItem item)
            {
                return item;
            }
            
            // Wait for container generation if needed
            if (treeListView.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                treeListView.UpdateLayout();
                return treeListView.ItemContainerGenerator.ContainerFromItem(obj) as ListViewItem;
            }
            
            return null;
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
        
        /// <summary>
        /// Auto-resizes all columns
        /// </summary>
        private void AutoResizeColumns()
        {
            if (!autoResizeEnabled || !(treeListView.View is GridView gridView))
                return;
                
            foreach (GridViewColumn column in gridView.Columns)
            {
                // Skip first column (name with indentation)
                if (gridView.Columns.IndexOf(column) == 0)
                    continue;
                    
                // Auto-size based on header and content
                column.Width = double.NaN; // Auto-size
                
                // Use Width property for minimum width instead of MinWidth
                if (column.Width < 50)
                {
                    column.Width = 50;
                }
            }
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
                    // Dispose managed resources
                    foreach (var item in _rootItems)
                    {
                        if (item is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
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