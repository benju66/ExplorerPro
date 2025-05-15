// UI/FileTree/FileTreeView.xaml.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using Newtonsoft.Json;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
// Add namespace aliases to resolve ambiguities
using WPF = System.Windows;
using WinForms = System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Interaction logic for FileTreeView.xaml
    /// </summary>
    public partial class FileTreeView : UserControl, IFileTree, IDisposable
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
        public bool HasSelectedItems => GetSelectedTreeViewItem() != null;
        #endregion

        #region Fields
        private MetadataManager metadataManager = null!;
        private CustomFileSystemModel fileSystemModel = null!;
        private UndoManager undoManager = null!;
        private SettingsManager _settingsManager = null!;
        private string currentFolderPath = string.Empty;
        private bool autoResizeEnabled = true;
        private Dictionary<string, object> pathCache = new Dictionary<string, object>();
        private Dictionary<string, TreeViewItem> itemCache = new Dictionary<string, TreeViewItem>();
        private const int CacheLimit = 1000;
        private Point? dragStartPosition;
        private DispatcherTimer columnResizeTimer = null!;
        private const double DragThreshold = 10.0;
        private readonly IFileOperations fileOperations;
        #endregion

        public FileTreeView()
        {
            InitializeComponent();
            
            // Initialize file operations
            fileOperations = new FileOperations.FileOperations();
            
            // Initialize column resize timer
            columnResizeTimer = new DispatcherTimer();
            columnResizeTimer.Interval = TimeSpan.FromMilliseconds(200);
            columnResizeTimer.Tick += (s, e) => {
                columnResizeTimer.Stop();
                AutoResizeColumns();
            };

            // Initialize the file system model and metadata manager
            InitializeManagersAndModel();

            // Set up event handlers
            this.Loaded += FileTreeView_Loaded;
            this.SizeChanged += FileTreeView_SizeChanged;
            
            // REMOVED: Don't set root directory in constructor
            // This was causing the null reference exception
        }

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
                
                Debug.WriteLine("[DEBUG] File tree components initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to initialize file tree components: {ex.Message}");
                
                // Create minimal working instances as fallback
                try {
                    if (metadataManager == null) metadataManager = new MetadataManager();
                    if (undoManager == null) undoManager = new UndoManager();
                    if (_settingsManager == null) _settingsManager = new SettingsManager();
                    if (fileSystemModel == null) fileSystemModel = new CustomFileSystemModel(metadataManager, undoManager, fileOperations);
                }
                catch (Exception fallbackEx) {
                    Debug.WriteLine($"[CRITICAL] Failed to create fallback components: {fallbackEx.Message}");
                    // Last resort - create absolute minimal dependencies
                    metadataManager = new MetadataManager();
                    undoManager = new UndoManager();
                    _settingsManager = new SettingsManager();
                    fileSystemModel = new CustomFileSystemModel(metadataManager, undoManager, fileOperations);
                }
            }
        }

        private void FileTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            // Additional initialization logic once the control is loaded
            AutoResizeColumns();
        }

        private void FileTreeView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScheduleColumnAdjustment();
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
            var selectedItem = GetSelectedTreeViewItem();
            return selectedItem?.Tag as string;
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
            NavigateAndHighlight(path);
        }

        /// <summary>
        /// Copies selected items to clipboard.
        /// </summary>
        public void CopySelected()
        {
            var selectedItem = GetSelectedTreeViewItem();
            if (selectedItem != null && selectedItem.Tag is string path)
            {
                CopyItem(path);
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
            var selectedItem = GetSelectedTreeViewItem();
            if (selectedItem != null && selectedItem.Tag is string path)
            {
                DeleteItemWithUndo(path);
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
            // Toggle internal flag and refresh the view
            bool showHidden = !_settingsManager.GetSetting("file_view.show_hidden", false);
            _settingsManager.UpdateSetting("file_view.show_hidden", showHidden);
            RefreshTreeView();
        }

        /// <summary>
        /// Clears the current selection.
        /// </summary>
        public void ClearSelection()
        {
            if (treeView.SelectedItem is TreeViewItem item)
            {
                item.IsSelected = false;
            }
        }

        /// <summary>
        /// Handles files dropped on the tree view.
        /// </summary>
        public void HandleFileDrop(object data)
        {
            if (data is WPF.IDataObject dataObject && dataObject.GetDataPresent(WPF.DataFormats.FileDrop))
            {
                string[] files = (string[])dataObject.GetData(WPF.DataFormats.FileDrop);
                HandleDrop(files);
            }
        }
        
        #endregion

        #region TreeView Management
        public void SetRootDirectory(string directory)
        {
            try {
                if (string.IsNullOrEmpty(directory))
                {
                    Debug.WriteLine($"[ERROR] Invalid directory path: null or empty");
                    return;
                }
                
                if (!Directory.Exists(directory))
                {
                    Debug.WriteLine($"[ERROR] Invalid or inaccessible directory: {directory}");
                    return;
                }

                treeView.Items.Clear();
                itemCache.Clear();
                pathCache.Clear();

                // Create root item
                var rootItem = CreateTreeViewItemForDirectory(directory);
                if (rootItem != null)
                {
                    treeView.Items.Add(rootItem);
                    
                    // Expand the root to show its contents
                    rootItem.IsExpanded = true;
                    
                    // Update current folder path
                    currentFolderPath = directory;
                    
                    // Notify listeners of location change
                    LocationChanged?.Invoke(this, directory);
                    
                    Debug.WriteLine($"[DEBUG] Set root directory to: {directory}");
                }
                else 
                {
                    Debug.WriteLine($"[ERROR] Failed to create root item for: {directory}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to set root directory: {ex.Message}");
                WPF.MessageBox.Show($"Error setting root directory: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private TreeViewItem CreateTreeViewItemForDirectory(string path)
        {
            try {
                if (itemCache.ContainsKey(path))
                    return itemCache[path];
                
                var directoryInfo = new DirectoryInfo(path);
                var item = new TreeViewItem
                {
                    Header = directoryInfo.Name,
                    Tag = path,
                    IsExpanded = false
                };

                // Add dummy item to enable expander
                item.Items.Add(CreateDummyItem());
                
                // Add to cache
                if (itemCache.Count > CacheLimit)
                    itemCache.Clear();
                
                itemCache[path] = item;
                
                // Apply custom styling from metadata if applicable
                ApplyMetadataStyling(item, path);
                
                return item;
            }
            catch (Exception ex) {
                Debug.WriteLine($"[ERROR] Failed to create tree view item for directory: {ex.Message}");
                return new TreeViewItem { Header = Path.GetFileName(path), Tag = path };
            }
        }

        private TreeViewItem CreateTreeViewItemForFile(string path)
        {
            try {
                if (itemCache.ContainsKey(path))
                    return itemCache[path];
                
                var fileInfo = new FileInfo(path);
                var item = new TreeViewItem
                {
                    Header = fileInfo.Name,
                    Tag = path
                };

                // Add to cache
                if (itemCache.Count > CacheLimit)
                    itemCache.Clear();
                
                itemCache[path] = item;
                
                // Apply custom styling from metadata if applicable
                ApplyMetadataStyling(item, path);
                
                return item;
            }
            catch (Exception ex) {
                Debug.WriteLine($"[ERROR] Failed to create tree view item for file: {ex.Message}");
                return new TreeViewItem { Header = Path.GetFileName(path), Tag = path };
            }
        }

        private void ApplyMetadataStyling(TreeViewItem item, string path)
        {
            try {
                // Apply text color if set in metadata
                string? colorHex = metadataManager.GetItemColor(path);
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
                bool isBold = metadataManager.GetItemBold(path);
                if (isBold)
                {
                    if (item.Header is string headerText)
                    {
                        var textBlock = new TextBlock
                        {
                            Text = headerText,
                            FontWeight = FontWeights.Bold
                        };
                        
                        if (!string.IsNullOrEmpty(colorHex))
                        {
                            try {
                                textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
                            }
                            catch {
                                // Ignore color conversion errors
                            }
                        }
                        
                        item.Header = textBlock;
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"[ERROR] Failed to apply metadata styling: {ex.Message}");
                // Continue without styling
            }
        }

        private TreeViewItem CreateDummyItem()
        {
            return new TreeViewItem { Header = "Loading..." };
        }

        private bool HasRealChildren(TreeViewItem item)
        {
            if (item == null || item.Items.Count == 0) 
                return false;
                
            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem firstChild)
                return firstChild.Header?.ToString() != "Loading...";
                
            return true;
        }

        private void LoadDirectoryContents(TreeViewItem parentItem)
        {
            if (parentItem == null || !(parentItem.Tag is string path) || HasRealChildren(parentItem))
                return;

            try
            {
                // Remove dummy item
                parentItem.Items.Clear();

                // Mark as loaded in path cache to avoid reloading
                if (pathCache.Count > CacheLimit)
                    pathCache.Clear();
                
                pathCache[path] = true;

                // Get and sort directories first
                var directories = Directory.GetDirectories(path)
                    .OrderBy(d => new DirectoryInfo(d).Name);

                // Add directories
                foreach (var dir in directories)
                {
                    try
                    {
                        var dirItem = CreateTreeViewItemForDirectory(dir);
                        parentItem.Items.Add(dirItem);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible directories
                        continue;
                    }
                }

                // Get and sort files
                var files = Directory.GetFiles(path)
                    .OrderBy(f => new FileInfo(f).Name);

                // Add files
                foreach (var file in files)
                {
                    try
                    {
                        var fileItem = CreateTreeViewItemForFile(file);
                        parentItem.Items.Add(fileItem);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible files
                        continue;
                    }
                }

                // Process UI events
                DoEvents();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to load directory contents: {ex.Message}");
                
                // Re-add dummy item in case of failure
                parentItem.Items.Clear();
                parentItem.Items.Add(CreateDummyItem());
            }
        }

        private void DoEvents()
        {
            // Allows UI to update during lengthy operations
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        private object? ExitFrame(object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
        }
        #endregion

        #region Event Handlers
        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item && item.Tag is string path)
            {
                LoadDirectoryContents(item);
                AutoResizeNameColumn();
            }
            e.Handled = true;
        }

        private void TreeViewItem_Collapsed(object sender, RoutedEventArgs e)
        {
            if (autoResizeEnabled)
            {
                AutoResizeNameColumn();
            }
            e.Handled = true;
        }

        private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = GetSelectedTreeViewItem();
            if (item != null && item.Tag is string path)
            {
                HandleDoubleClick(path);
            }
        }

        private void TreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var item = GetSelectedTreeViewItem();
            if (item != null && item.Tag is string path)
            {
                BuildContextMenu(path);
            }
            else
            {
                e.Handled = true; // Prevent menu from showing if no item selected
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = e.NewValue as TreeViewItem;
            if (item != null && item.Tag is string path)
            {
                OnTreeItemClicked(path);
            }
        }

        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check if user clicked on empty space
            if (GetTreeViewItemFromPoint(e.GetPosition(treeView)) == null)
            {
                // Clear selection without directly setting SelectedItem
                if (treeView.SelectedItem is TreeViewItem selectedItem)
                {
                    selectedItem.IsSelected = false;
                }
                e.Handled = true;
                return;
            }

            // Store start position for potential drag operation
            dragStartPosition = e.GetPosition(treeView);
        }

        private void TreeView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && dragStartPosition.HasValue)
            {
                Point currentPosition = e.GetPosition(treeView);
                Vector dragVector = currentPosition - dragStartPosition.Value;
                double dragDistance = Math.Sqrt(Math.Pow(dragVector.X, 2) + Math.Pow(dragVector.Y, 2));

                if (dragDistance > DragThreshold)
                {
                    StartDrag();
                }
            }
        }

        private void TreeView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            dragStartPosition = null;
        }

        private void TreeView_DragEnter(object sender, WPF.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(WPF.DataFormats.FileDrop) || 
                e.Data.GetDataPresent("FileGroupDescriptor") ||
                e.Data.GetDataPresent("FileGroupDescriptorW"))
            {
                e.Effects = WPF.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = WPF.DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void TreeView_DragOver(object sender, WPF.DragEventArgs e)
        {
            // Find the TreeViewItem under the cursor
            var item = GetTreeViewItemFromPoint(e.GetPosition(treeView));
            
            if (item != null && item.Tag is string path && Directory.Exists(path))
            {
                e.Effects = WPF.DragDropEffects.Copy;
                // Instead of directly setting SelectedItem
                if (item != null)
                {
                    item.IsSelected = true;
                }
                Mouse.SetCursor(Cursors.Arrow);
            }
            else
            {
                e.Effects = WPF.DragDropEffects.None;
                Mouse.SetCursor(Cursors.No);
            }
            
            e.Handled = true;
        }

        private void TreeView_Drop(object sender, WPF.DragEventArgs e)
        {
            var item = GetTreeViewItemFromPoint(e.GetPosition(treeView));
            if (item == null || !(item.Tag is string targetPath) || !Directory.Exists(targetPath))
            {
                e.Handled = true;
                return;
            }

            try
            {
                // Handle file drop from Windows Explorer
                if (e.Data.GetDataPresent(WPF.DataFormats.FileDrop))
                {
                    HandleStandardFileDrop(e, targetPath);
                }
                // Handle Outlook attachments
                else if (e.Data.GetDataPresent("FileGroupDescriptor") || 
                         e.Data.GetDataPresent("FileGroupDescriptorW"))
                {
                    HandleOutlookAttachments(e, targetPath);
                }
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error processing dropped files: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.SetCursor(Cursors.Arrow);
            }
            
            e.Handled = true;
        }

        private void TreeView_DragLeave(object sender, WPF.DragEventArgs e)
        {
            Mouse.SetCursor(Cursors.Arrow);
            e.Handled = true;
        }
        #endregion

        #region Drag and Drop
        private void StartDrag()
        {
            var selectedItem = GetSelectedTreeViewItem();
            if (selectedItem == null || !(selectedItem.Tag is string path))
                return;

            List<string> paths = new List<string>();
            
            // If there are multiple selected items, include them all
            foreach (var item in GetSelectedTreeViewItems())
            {
                if (item.Tag is string itemPath)
                    paths.Add(itemPath);
            }

            // Prepare the data object
            WPF.DataObject dataObject = new WPF.DataObject(WPF.DataFormats.FileDrop, paths.ToArray());
            
            // Execute the drag-drop operation
            WPF.DragDrop.DoDragDrop(treeView, dataObject, WPF.DragDropEffects.Copy | WPF.DragDropEffects.Move);
            
            // Reset drag start position
            dragStartPosition = null;
        }

        private void HandleStandardFileDrop(WPF.DragEventArgs e, string targetPath)
        {
            if (e.Data.GetDataPresent(WPF.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(WPF.DataFormats.FileDrop);
                List<string> errors = new List<string>();

                foreach (string sourcePath in files)
                {
                    try
                    {
                        string destPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));
                        
                        if (File.Exists(sourcePath))
                        {
                            // Move the file
                            if (File.Exists(destPath))
                            {
                                var result = WPF.MessageBox.Show(
                                    $"File {Path.GetFileName(sourcePath)} already exists. Overwrite?",
                                    "File Exists", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                
                                if (result == MessageBoxResult.No)
                                    continue;
                            }
                            
                            File.Move(sourcePath, destPath, true);
                            Debug.WriteLine($"Moved {sourcePath} -> {destPath}");
                        }
                        else if (Directory.Exists(sourcePath))
                        {
                            // Move the directory
                            if (Directory.Exists(destPath))
                            {
                                var result = WPF.MessageBox.Show(
                                    $"Folder {Path.GetFileName(sourcePath)} already exists. Overwrite?",
                                    "Folder Exists", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                
                                if (result == MessageBoxResult.No)
                                    continue;
                                
                                Directory.Delete(destPath, true);
                            }
                            
                            Directory.Move(sourcePath, destPath);
                            Debug.WriteLine($"Moved {sourcePath} -> {destPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{sourcePath} => {ex.Message}");
                    }
                }

                if (errors.Count > 0)
                {
                    WPF.MessageBox.Show($"Some files failed to move:\n{string.Join("\n", errors)}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // Refresh the target directory
                if (treeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag?.ToString() == targetPath)
                {
                    // Collapse and re-expand to refresh
                    selectedItem.IsExpanded = false;
                    selectedItem.IsExpanded = true;
                }
            }
        }

        private void HandleOutlookAttachments(WPF.DragEventArgs e, string targetPath)
        {
            // Implementation for Outlook attachments would go here
            // This is a complex topic and would require COM interop or additional libraries
            
            WPF.MessageBox.Show("Outlook attachment handling is not implemented in this version.", 
                "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region Context Menu
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
                Debug.WriteLine($"[ERROR] Failed to build context menu: {ex.Message}");
                // Create minimal context menu
                treeContextMenu.Items.Clear();
                var menuItem = new MenuItem { Header = "Refresh" };
                menuItem.Click += (s, e) => RefreshView();
                treeContextMenu.Items.Add(menuItem);
            }
        }

        private void AddMenuItem(ContextMenu menu, string header, string iconPath, RoutedEventHandler clickHandler)
        {
            var menuItem = new MenuItem { Header = header };
            
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                try
                {
                    var image = new Image
                    {
                        Source = new BitmapImage(new Uri(iconPath, UriKind.Relative)),
                        Width = 16,
                        Height = 16
                    };
                    
                    menuItem.Icon = image;
                }
                catch
                {
                    // Ignore icon loading errors
                }
            }
            
            if (clickHandler != null)
                menuItem.Click += clickHandler;
            
            menu.Items.Add(menuItem);
        }
        #endregion

        #region Context Menu Action Handlers
        private void OpenFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                else if (Directory.Exists(path))
                {
                    // Set this directory as root
                    SetRootDirectory(path);
                }
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Failed to open file: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowInFileExplorer(string path)
        {
            try
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening file explorer for {path}: {ex.Message}");
            }
        }

        private void RenameItem(string path)
        {
            var item = GetTreeViewItemForPath(path);
            if (item != null)
            {
                // Implementation would depend on how you handle inline editing
                // This is a placeholder
                string newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter new name:", "Rename", Path.GetFileName(path));
                
                if (!string.IsNullOrWhiteSpace(newName) && newName != Path.GetFileName(path))
                {
                    // Fixed: Properly create a RenameCommand with all required parameters
                    var command = new RenameCommand(fileOperations, path, newName);
                    undoManager.ExecuteCommand(command);
                    
                    // Refresh the tree view to reflect the change
                    RefreshParentDirectory(path);
                }
            }
        }

        private void DeleteItemWithUndo(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                WPF.MessageBox.Show($"'{path}' does not exist.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = WPF.MessageBox.Show($"Are you sure you want to delete:\n{path}?", 
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                var command = new DeleteItemCommand(fileOperations, this, path);
                undoManager.ExecuteCommand(command);
                
                // Refresh the parent directory
                RefreshParentDirectory(path);
            }
        }

        private void CopyItem(string path)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                string[] filePaths = { path };
                Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { path });
                Debug.WriteLine($"Copied: {path}");
            }
            else
            {
                WPF.MessageBox.Show("The selected file or folder does not exist.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PasteItem(string targetPath)
        {
            if (!Directory.Exists(targetPath))
            {
                WPF.MessageBox.Show("You can only paste into a directory.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var files = Clipboard.GetFileDropList();
            if (files.Count == 0)
            {
                WPF.MessageBox.Show("No valid file path(s) in clipboard.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (string sourcePath in files)
            {
                if (File.Exists(sourcePath) || Directory.Exists(sourcePath))
                {
                    string? newPath = fileOperations.CopyItem(sourcePath, targetPath);
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        Debug.WriteLine($"Pasted: {newPath}");
                    }
                    else
                    {
                        WPF.MessageBox.Show($"Failed to paste item: {sourcePath}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    WPF.MessageBox.Show($"Clipboard file/folder does not exist: {sourcePath}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // Refresh the target directory
            RefreshDirectory(targetPath);
        }

        private void CreateNewFile(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                WPF.MessageBox.Show("Cannot create a file outside a folder.", "Invalid Target", 
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

        private void CreateNewFolder(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                WPF.MessageBox.Show("Cannot create a folder outside a directory.", "Invalid Target", 
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

        private void CollapseAll()
        {
            foreach (var item in treeView.Items.OfType<TreeViewItem>())
            {
                CollapseTreeViewItem(item);
            }
        }

        private void ExpandAll()
        {
            foreach (var item in treeView.Items.OfType<TreeViewItem>())
            {
                ExpandTreeViewItem(item);
            }
        }

        private void CollapseTreeViewItem(TreeViewItem item)
        {
            item.IsExpanded = false;
            foreach (var child in item.Items.OfType<TreeViewItem>())
            {
                CollapseTreeViewItem(child);
            }
        }

        private void ExpandTreeViewItem(TreeViewItem item)
        {
            item.IsExpanded = true;
            foreach (var child in item.Items.OfType<TreeViewItem>())
            {
                ExpandTreeViewItem(child);
            }
        }

        private void OpenFolderInNewTab(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Debug.WriteLine($"Error: {folderPath} is not a valid directory.");
                return;
            }

            // This would need to interact with your TabManager
            var mainWindow = Window.GetWindow(this);
            if (mainWindow != null)
            {
                // Example of how this might work - adjust based on your actual MainWindow structure
                // mainWindow.OpenFolderInNewTab(folderPath);
                Debug.WriteLine($"Opening folder in new tab: {folderPath}");
            }
        }

        private void OpenFolderInNewWindow(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Debug.WriteLine($"Error: {folderPath} is not a valid directory.");
                return;
            }

            // This would need to interact with your MainWindow
            var mainWindow = Window.GetWindow(this);
            if (mainWindow != null)
            {
                // Example of how this might work - adjust based on your actual MainWindow structure
                // mainWindow.OpenFolderInNewWindow(folderPath);
                Debug.WriteLine($"Opening folder in new window: {folderPath}");
            }
        }

        private void ToggleSplitView(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Debug.WriteLine($"Error: {folderPath} is not a valid directory.");
                return;
            }

            // This would need to interact with your MainWindow
            var mainWindow = Window.GetWindow(this);
            if (mainWindow != null)
            {
                // Example of how this might work - adjust based on your actual MainWindow structure
                // mainWindow.ToggleSplitView(folderPath);
                Debug.WriteLine($"Toggling split view for: {folderPath}");
            }
        }

        private void PreviewPdf(string filePath)
        {
            // Placeholder for PDF preview functionality
            WPF.MessageBox.Show("PDF preview functionality not implemented in this version.", 
                "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PreviewImage(string filePath)
        {
            // Placeholder for image preview functionality
            WPF.MessageBox.Show("Image preview functionality not implemented in this version.", 
                "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ChangeItemTextColor(string path)
        {
            // Placeholder for text color customization
            var dialog = new WinForms.ColorDialog();
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                Color color = Color.FromArgb(
                    dialog.Color.A, 
                    dialog.Color.R, 
                    dialog.Color.G, 
                    dialog.Color.B);
                
                string colorHex = color.ToString();
                metadataManager.SetItemColor(path, colorHex);
                
                // Also update bold status if needed
                bool isBold = false; // You would get this from a checkbox
                metadataManager.SetItemBold(path, isBold);
                
                // Refresh the tree view to apply the changes
                RefreshTreeView();
            }
        }
        #endregion

        #region Utility Methods
        private void HandleDoubleClick(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    WPF.MessageBox.Show($"Failed to open file:\n{path}\n\n{ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (Directory.Exists(path))
            {
                var item = GetTreeViewItemForPath(path);
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
                Debug.WriteLine("[WARNING] Clicked on an invalid path, ignoring.");
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

        private TreeViewItem? GetSelectedTreeViewItem()
        {
            return treeView.SelectedItem as TreeViewItem;
        }

        private IEnumerable<TreeViewItem> GetSelectedTreeViewItems()
        {
            // This is a simplification since WPF TreeView doesn't natively support multi-select
            // For multi-select, you would need a custom implementation
            var selectedItem = GetSelectedTreeViewItem();
            if (selectedItem != null)
                yield return selectedItem;
        }

        private TreeViewItem? GetTreeViewItemFromPoint(Point point)
        {
            var element = treeView.InputHitTest(point) as DependencyObject;
            while (element != null && !(element is TreeViewItem))
            {
                element = VisualTreeHelper.GetParent(element);
            }
            return element as TreeViewItem;
        }

        private TreeViewItem? GetTreeViewItemForPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
                
            if (itemCache.ContainsKey(path))
                return itemCache[path];
            
            // Fallback to searching through the tree
            return FindTreeViewItemByPath(treeView.Items, path);
        }

        private TreeViewItem? FindTreeViewItemByPath(ItemCollection items, string path)
        {
            foreach (var item in items.OfType<TreeViewItem>())
            {
                if (item.Tag?.ToString() == path)
                    return item;
                
                var childItem = FindTreeViewItemByPath(item.Items, path);
                if (childItem != null)
                    return childItem;
            }
            return null;
        }

        private void RefreshParentDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            string? parentDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentDir))
            {
                RefreshDirectory(parentDir);
            }
        }

        private void RefreshDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;
                
            var item = GetTreeViewItemForPath(directoryPath);
            if (item != null)
            {
                bool wasExpanded = item.IsExpanded;
                item.Items.Clear();
                item.Items.Add(CreateDummyItem());
                
                if (wasExpanded)
                {
                    item.IsExpanded = false;
                    item.IsExpanded = true;
                }
            }
        }

        private void RefreshTreeView()
        {
            // This is a more extensive refresh that might be needed after metadata changes
            string? currentRoot = null;
            if (treeView.Items.Count > 0 && treeView.Items[0] is TreeViewItem rootItem)
            {
                currentRoot = rootItem.Tag?.ToString();
            }
            
            if (!string.IsNullOrEmpty(currentRoot))
            {
                SetRootDirectory(currentRoot);
            }
        }

        public bool NavigateAndHighlight(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return false;
                    
                string absPath = Path.GetFullPath(path);
                
                if (!File.Exists(absPath) && !Directory.Exists(absPath))
                {
                    Debug.WriteLine($"[ERROR] Cannot navigate to non-existent path: {absPath}");
                    return false;
                }

                // Expand parent directories
                string? dirPath = File.Exists(absPath) ? Path.GetDirectoryName(absPath) : absPath;
                if (dirPath != null)
                {
                    ExpandToPath(dirPath);
                }
                
                // Select and scroll to the item
                var item = GetTreeViewItemForPath(absPath);
                if (item != null)
                {
                    // Instead of: treeView.SelectedItem = item;
                    item.IsSelected = true;
                    item.BringIntoView();
                    Debug.WriteLine($"✅ Successfully navigated and highlighted: {absPath}");
                    return true;
                }
                
                Debug.WriteLine($"[ERROR] Could not find TreeViewItem for path: {absPath}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception while navigating: {ex.Message}");
                return false;
            }
        }

        public bool ExpandToPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return false;
                    
                string absPath = Path.GetFullPath(path);
                
                if (!Directory.Exists(absPath))
                {
                    if (File.Exists(absPath))
                    {
                        // If it's a file, expand to its parent directory
                        absPath = Path.GetDirectoryName(absPath) ?? string.Empty;
                        if (string.IsNullOrEmpty(absPath))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[ERROR] Cannot expand non-existent path: {absPath}");
                        return false;
                    }
                }

                // Get path components
                var components = absPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
                
                if (components.Count == 0)
                    return false;
                
                // Start from the root
                string currentPath = components[0] + Path.DirectorySeparatorChar;
                var currentItem = GetTreeViewItemForPath(currentPath);
                
                if (currentItem == null)
                {
                    // If we can't find the root, try to use the first item in the tree
                    if (treeView.Items.Count > 0 && treeView.Items[0] is TreeViewItem rootItem)
                    {
                        currentItem = rootItem;
                        currentPath = rootItem.Tag?.ToString() ?? string.Empty;
                        if (string.IsNullOrEmpty(currentPath))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[ERROR] Cannot find root item for path: {absPath}");
                        return false;
                    }
                }

                // Expand each component
                for (int i = 1; i < components.Count; i++)
                {
                    currentPath = Path.Combine(currentPath, components[i]);
                    currentItem.IsExpanded = true;
                    
                    var nextItem = GetTreeViewItemForPath(currentPath);
                    if (nextItem == null)
                    {
                        Debug.WriteLine($"[ERROR] Cannot find item for path component: {currentPath}");
                        return false;
                    }
                    
                    currentItem = nextItem;
                }

                // Expand the final directory
                currentItem.IsExpanded = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception while expanding path: {ex.Message}");
                return false;
            }
        }

        private void AutoResizeNameColumn()
        {
            if (!autoResizeEnabled)
                return;
            
            // Placeholder method since TreeView doesn't have columns
            // In a GridView implementation, this would resize the columns
        }

        private void AutoResizeColumns()
        {
            if (!autoResizeEnabled)
                return;
            
            // Placeholder method since TreeView doesn't have columns
            // In a GridView implementation, this would resize the columns
        }

        private void ScheduleColumnAdjustment()
        {
            if (!autoResizeEnabled)
                return;
            
            columnResizeTimer.Stop();
            columnResizeTimer.Start();
        }

        /// <summary>
        /// Handles dropped files.
        /// </summary>
        /// <param name="files">Array of file paths that were dropped</param>
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
                            
                            // Copy all files and subdirectories
                            foreach (string file in Directory.GetFiles(filePath))
                            {
                                string fileName = Path.GetFileName(file);
                                File.Copy(file, Path.Combine(destPath, fileName), false);
                            }
                            
                            foreach (string dir in Directory.GetDirectories(filePath))
                            {
                                string dirName = Path.GetFileName(dir);
                                DirectoryCopy(dir, Path.Combine(destPath, dirName), true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WPF.MessageBox.Show($"Error handling dropped file {filePath}: {ex.Message}", 
                            "Drop Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            
            // Refresh the view to show new files
            RefreshTreeView();
        }

        /// <summary>
        /// Helper method to recursively copy directories
        /// </summary>
        private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
            }
            
            // If the destination directory doesn't exist, create it
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }
            
            // Get the files in the directory and copy them to the new location
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }
            
            // If copying subdirectories, copy them and their contents to new location
            if (copySubDirs)
            {
                DirectoryInfo[] dirs = dir.GetDirectories();
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Releases resources used by this control
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from events to prevent memory leaks
            this.Loaded -= FileTreeView_Loaded;
            this.SizeChanged -= FileTreeView_SizeChanged;
            
            // Dispose timers
            columnResizeTimer.Stop();
            
            // Clear collections to release memory
            itemCache.Clear();
            pathCache.Clear();
            
            // Dispose any disposable dependencies
            if (fileSystemModel is IDisposable disposableModel)
            {
                disposableModel.Dispose();
            }
            
            // Clean up references
            metadataManager = null!;
            fileSystemModel = null!;
            undoManager = null!;
            _settingsManager = null!;
        }
        #endregion
    }
}