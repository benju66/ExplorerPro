// UI/Panels/PinnedPanel/PinnedPanel.xaml.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree;
// Add namespace alias to resolve the conflict
using MainWindowType = ExplorerPro.UI.MainWindow.MainWindow;

namespace ExplorerPro.UI.Panels.PinnedPanel
{
    /// <summary>
    /// Interaction logic for PinnedPanel.xaml
    /// </summary>
    public partial class PinnedPanel : DockPanel
    {
        #region Events
        
        /// <summary>
        /// Event raised when a pinned item is added globally
        /// </summary>
        public event EventHandler<StringEventArgs> PinnedItemAddedGlobal = delegate { };
        
        /// <summary>
        /// Event raised when a pinned item is modified
        /// </summary>
        public event EventHandler<ItemModifiedEventArgs> PinnedItemModified = delegate { };
        
        /// <summary>
        /// Event raised when a pinned item is removed
        /// </summary>
        public event EventHandler<StringEventArgs> PinnedItemRemoved = delegate { };

        /// <summary>
        /// Event raised when a pinned item is added
        /// </summary>
        public event EventHandler<StringEventArgs> PinnedItemAdded = delegate { };
        
        #endregion
        
        #region Fields
        
        private readonly PinnedManager _pinnedManager;
        private readonly MetadataManager _metadataManager;
        private readonly ILogger<PinnedPanel>? _logger;
        
        private Point? _dragStartPosition;
        private bool _isDragging;
        private TreeViewItem? _draggedItem = null!;
        
        private const string HEADING_ROLE = "IsHeading";
        
        private Dictionary<string, bool> _expandedStates = new Dictionary<string, bool>();
        private TreeViewItem _favoritesRoot = null!;
        private TreeViewItem _pinnedRoot = null!;
        
        // Boolean to track if signals are connected
        private bool _isSignalsConnected;
        
        #endregion
        
        #region Constructors
        
        /// <summary>
        /// Initializes a new instance of the PinnedPanel class
        /// </summary>
        public PinnedPanel()
        {
            InitializeComponent();
            
            // Initialize managers
            _pinnedManager = PinnedManager.Instance;
            _metadataManager = MetadataManager.Instance;
            
            // Initialize non-nullable fields with default values
            _draggedItem = null!;
            _expandedStates = new Dictionary<string, bool>();
            
            // Create the root items
            _favoritesRoot = new TreeViewItem
            {
                Header = "Favorites",
                IsExpanded = true
            };
            _favoritesRoot.SetValue(TreeViewItem.TagProperty, HEADING_ROLE);
            
            _pinnedRoot = new TreeViewItem
            {
                Header = "Pinned Items",
                IsExpanded = true
            };
            _pinnedRoot.SetValue(TreeViewItem.TagProperty, HEADING_ROLE);
            
            // Connect pinned manager updates
            _pinnedManager.PinnedItemsUpdated += OnPinnedItemsUpdated;
            
            // Setup UI
            SetupUI();
            RefreshPinnedItems();
            LoadExpandedStatesFromFile();
        }
        
        /// <summary>
        /// Initializes a new instance of the PinnedPanel class with logging
        /// </summary>
        public PinnedPanel(ILogger<PinnedPanel> logger) : this()
        {
            _logger = logger;
        }
        
        #endregion
        
        #region UI Setup
        
        /// <summary>
        /// Sets up the UI elements for the pinned panel
        /// </summary>
        private void SetupUI()
        {
            // Create the top-level nodes: Favorites & Pinned Items
            _favoritesRoot = new TreeViewItem
            {
                Header = "Favorites",
                IsExpanded = true
            };
            _favoritesRoot.SetValue(TreeViewItem.TagProperty, HEADING_ROLE);
            
            _pinnedRoot = new TreeViewItem
            {
                Header = "Pinned Items",
                IsExpanded = true
            };
            _pinnedRoot.SetValue(TreeViewItem.TagProperty, HEADING_ROLE);
            
            pinnedTree.Items.Add(_favoritesRoot);
            pinnedTree.Items.Add(_pinnedRoot);
        }
        
        #endregion
        
        #region Mouse and Drag Events
        
        private void PinnedTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Store starting point for possible drag operation
            _dragStartPosition = e.GetPosition(pinnedTree);
            
            // Find the TreeViewItem at this position
            _draggedItem = GetTreeViewItemFromPoint(_dragStartPosition.Value);
            
            e.Handled = false;
        }
        
        private void PinnedTree_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _dragStartPosition.HasValue && _draggedItem != null)
            {
                Point position = e.GetPosition(pinnedTree);
                Vector difference = position - _dragStartPosition.Value;
                
                if (difference.Length > SystemParameters.MinimumHorizontalDragDistance)
                {
                    StartDrag();
                    e.Handled = true;
                }
            }
        }
        
        private void PinnedTree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Reset drag tracking
            _dragStartPosition = null;
            _isDragging = false;
            _draggedItem = null;
            
            e.Handled = false;
        }
        
        private void PinnedTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = GetTreeViewItemFromPoint(e.GetPosition(pinnedTree));
            if (item != null)
            {
                HandleDoubleClick(item);
            }
            
            e.Handled = true;
        }
        
        private void PinnedTree_DragEnter(object sender, DragEventArgs e)
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
        
        private void PinnedTree_DragOver(object sender, DragEventArgs e)
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
        
        private void PinnedTree_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (File.Exists(file) || Directory.Exists(file))
                    {
                        PinItem(file);
                    }
                }
            }
            
            e.Handled = true;
        }
        
        private void PinnedTree_DragLeave(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }
        
        private void PinnedTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var item = GetTreeViewItemFromPoint(Mouse.GetPosition(pinnedTree));
            if (item != null)
            {
                ShowContextMenu(item);
            }
            else
            {
                e.Handled = true;
            }
        }
        
        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item != null && item.Tag is string path && !string.IsNullOrEmpty(path))
            {
                _expandedStates[path] = true;
                SaveExpandedStatesToFile();
            }
            
            e.Handled = true;
        }
        
        private void TreeViewItem_Collapsed(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item != null && item.Tag is string path && !string.IsNullOrEmpty(path))
            {
                _expandedStates[path] = false;
                SaveExpandedStatesToFile();
            }
            
            e.Handled = true;
        }
        
        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item != null)
            {
                HandleTreeClick(item);
            }
            
            e.Handled = true;
        }
        
        #endregion
        
        #region Drag Implementation
        
        /// <summary>
        /// Starts a drag operation with the currently selected items
        /// </summary>
        private void StartDrag()
        {
            if (_draggedItem == null || _isDragging)
                return;
                
            if (_draggedItem.Tag is string tag && tag == HEADING_ROLE)
                return; // Don't allow dragging heading items
                
            _isDragging = true;
            
            // Get the path from the dragged item
            string? path = GetItemPath(_draggedItem);
            if (string.IsNullOrEmpty(path) || !File.Exists(path) && !Directory.Exists(path))
            {
                _isDragging = false;
                return;
            }
            
            // Create data object for drag operation
            DataObject dataObject = new DataObject(DataFormats.FileDrop, new string[] { path });
            
            // Start the drag operation
            DragDropEffects effects = DragDrop.DoDragDrop(_draggedItem, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
            
            // Reset drag state
            _isDragging = false;
            _draggedItem = null;
            _dragStartPosition = null;
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
            // Store expanded states
            StoreExpandedStates();
            
            // Clear and recreate favorites and pinned roots
            _favoritesRoot.Items.Clear();
            _pinnedRoot.Items.Clear();
            
            // Get pinned and favorite items
            List<string> pinnedItems = _pinnedManager.GetPinnedItems();
            List<string> favoriteItems = _pinnedManager.GetFavoriteItems();
            
            // Add favorites (flat list)
            foreach (string favPath in favoriteItems)
            {
                CreateFavoriteItem(favPath, _favoritesRoot);
            }
            
            // Add pinned items (hierarchical)
            foreach (string pinnedPath in pinnedItems)
            {
                AddPinnedItemToTree(pinnedPath);
            }
            
            // Restore expansion states
            RestoreExpandedStates();
            
            // Save states to file
            SaveExpandedStatesToFile();
        }
        
        /// <summary>
        /// Adds a pinned item to the tree, maintaining hierarchy
        /// </summary>
        /// <param name="itemPath">The path to pin</param>
        private void AddPinnedItemToTree(string itemPath)
        {
            if (!File.Exists(itemPath) && !Directory.Exists(itemPath))
            {
                _logger?.LogError($"Path '{itemPath}' does not exist.");
                return;
            }
            
            List<string> pinnedItems = _pinnedManager.GetPinnedItems();
            string? parentPath = Path.GetDirectoryName(itemPath);
            
            // Find the nearest pinned ancestor
            string? nearestPinnedAncestor = null;
            string? tempPath = parentPath;
            while (!string.IsNullOrEmpty(tempPath))
            {
                if (pinnedItems.Contains(tempPath))
                {
                    nearestPinnedAncestor = tempPath;
                    break;
                }
                
                string? newTemp = Path.GetDirectoryName(tempPath);
                if (newTemp == tempPath)
                    break;
                    
                tempPath = newTemp;
            }
            
            // Ensure ancestor is in the pinned tree
            if (nearestPinnedAncestor != null && FindItemByPath(_pinnedRoot, nearestPinnedAncestor) == null)
            {
                AddPinnedItemToTree(nearestPinnedAncestor);
            }
            
            // Determine the correct parent
            TreeViewItem parentItem = nearestPinnedAncestor != null ? 
                FindItemByPath(_pinnedRoot, nearestPinnedAncestor) ?? _pinnedRoot : _pinnedRoot;
            
            // Find or create the item under the correct parent
            TreeViewItem? existingItem = FindItemByPath(pinnedTree, itemPath);
            if (existingItem != null)
            {
                TreeViewItem? existingParent = GetItemParent(existingItem);
                
                // Detect if existing parent is the "Favorites" heading
                bool isFavorites = false;
                if (existingParent != null)
                {
                    if (existingParent.Tag is string tag && tag == HEADING_ROLE && 
                        existingParent.Header.ToString() == "Favorites")
                    {
                        isFavorites = true;
                    }
                }
                
                // If it's already under some other parent, handle appropriately
                if (existingParent != null && existingParent != parentItem)
                {
                    if (isFavorites)
                    {
                        CreateTreeItem(itemPath, parentItem);
                    }
                    else
                    {
                        // Move it from current parent to new parent
                        MoveItemToParent(existingItem, parentItem);
                    }
                }
                // Else it's already in pinned under the correct parent - do nothing
            }
            else
            {
                // No existing item, create it
                CreateTreeItem(itemPath, parentItem);
            }
            
            // Reorganize children if this is a newly pinned folder
            foreach (string childPath in pinnedItems)
            {
                if (Path.GetDirectoryName(childPath) == itemPath)
                {
                    TreeViewItem? existingChild = FindItemByPath(pinnedTree, childPath);
                    if (existingChild != null)
                    {
                        TreeViewItem? newParent = FindItemByPath(pinnedTree, itemPath);
                        if (newParent != null)
                        {
                            MoveItemToParent(existingChild, newParent);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Pins an item and broadcasts globally
        /// </summary>
        /// <param name="itemPath">The path to pin</param>
        public void PinItem(string itemPath)
        {
            _pinnedManager.AddPinnedItem(itemPath);
            PinnedItemAddedGlobal?.Invoke(this, new StringEventArgs(itemPath));
            PinnedItemAdded?.Invoke(this, new StringEventArgs(itemPath));
            _logger?.LogInformation($"Pinned item: {itemPath}");
        }
        
        /// <summary>
        /// Unpins an item
        /// </summary>
        /// <param name="itemPath">The path to unpin</param>
        public void UnpinItem(string itemPath)
        {
            _pinnedManager.RemovePinnedItem(itemPath);
            PinnedItemRemoved?.Invoke(this, new StringEventArgs(itemPath));
            _logger?.LogInformation($"Unpinned item: {itemPath}");
        }
        
        #endregion
        
        #region Favorites Management
        
        /// <summary>
        /// Adds an item to favorites
        /// </summary>
        /// <param name="itemPath">The path to favorite</param>
        private void FavoriteItem(string itemPath)
        {
            _pinnedManager.FavoriteItem(itemPath);
            _logger?.LogInformation($"Favorited item: {itemPath}");
            RefreshPinnedItems();
        }
        
        /// <summary>
        /// Removes an item from favorites
        /// </summary>
        /// <param name="itemPath">The path to unfavorite</param>
        private void UnfavoriteItem(string itemPath)
        {
            _pinnedManager.UnfavoriteItem(itemPath);
            _logger?.LogInformation($"Unfavorited item: {itemPath}");
            RefreshPinnedItems();
        }
        
        #endregion
        
        #region Expanded States Management
        
        /// <summary>
        /// Stores expanded states for the current items
        /// </summary>
        private void StoreExpandedStates()
        {
            _expandedStates.Clear();
            StoreItemExpandedStates(_favoritesRoot);
            StoreItemExpandedStates(_pinnedRoot);
        }
        
        /// <summary>
        /// Recursively stores expanded state for an item and its children
        /// </summary>
        /// <param name="item">The item to process</param>
        private void StoreItemExpandedStates(TreeViewItem item)
        {
            if (item == null)
                return;
                
            if (item.Tag is string path && !string.IsNullOrEmpty(path) && path != HEADING_ROLE)
            {
                _expandedStates[path] = item.IsExpanded;
            }
            
            foreach (var child in item.Items.OfType<TreeViewItem>())
            {
                StoreItemExpandedStates(child);
            }
        }
        
        /// <summary>
        /// Restores expanded states to the current items
        /// </summary>
        private void RestoreExpandedStates()
        {
            RestoreItemExpandedStates(_favoritesRoot);
            RestoreItemExpandedStates(_pinnedRoot);
        }
        
        /// <summary>
        /// Recursively restores expanded state for an item and its children
        /// </summary>
        /// <param name="item">The item to process</param>
        private void RestoreItemExpandedStates(TreeViewItem item)
        {
            if (item == null)
                return;
                
            if (item.Tag is string path && !string.IsNullOrEmpty(path) && path != HEADING_ROLE && 
                _expandedStates.TryGetValue(path, out bool isExpanded))
            {
                item.IsExpanded = isExpanded;
            }
            
            foreach (var child in item.Items.OfType<TreeViewItem>())
            {
                RestoreItemExpandedStates(child);
            }
        }
        
        /// <summary>
        /// Saves expanded states to a file
        /// </summary>
        private void SaveExpandedStatesToFile()
        {
            // Implementation to save expanded states to a JSON file
            // Using System.Text.Json or Newtonsoft.Json
        }
        
        /// <summary>
        /// Loads expanded states from a file
        /// </summary>
        private void LoadExpandedStatesFromFile()
        {
            // Implementation to load expanded states from a JSON file
            // Using System.Text.Json or Newtonsoft.Json
        }
        
        #endregion
        
        #region Tree Item Helpers
        
        /// <summary>
        /// Creates a tree item for a pinned path
        /// </summary>
        /// <param name="path">The path to create an item for</param>
        /// <param name="parent">The parent item</param>
        /// <returns>The created TreeViewItem</returns>
        private TreeViewItem? CreateTreeItem(string path, TreeViewItem parent)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                _logger?.LogError($"Cannot create tree item for non-existent path '{path}'.");
                return null;
            }
            
            string name = Path.GetFileName(path);
            var newItem = new TreeViewItem
            {
                Header = name,
                Tag = path,
                ToolTip = GetItemToolTip(path)
            };
            
            // Set icon based on file/folder type
            newItem.Header = CreateItemHeader(path, name);
            
            parent.Items.Add(newItem);
            return newItem;
        }
        
        /// <summary>
        /// Creates a favorite item under the favorites root
        /// </summary>
        /// <param name="path">The path to create a favorite for</param>
        /// <param name="parent">The parent item (usually favorites root)</param>
        /// <returns>The created TreeViewItem</returns>
        private TreeViewItem? CreateFavoriteItem(string path, TreeViewItem parent)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                _logger?.LogError($"Favorite path does not exist: {path}");
                return null;
            }
            
            string name = Path.GetFileName(path);
            var favItem = new TreeViewItem
            {
                Header = name,
                Tag = path,
                ToolTip = GetItemToolTip(path)
            };
            
            // Set icon based on file/folder type
            favItem.Header = CreateItemHeader(path, name);
            
            parent.Items.Add(favItem);
            return favItem;
        }
        
        /// <summary>
        /// Creates a header with icon and text for a tree item
        /// </summary>
        /// <param name="path">The file/folder path</param>
        /// <param name="name">The display name</param>
        /// <returns>A StackPanel containing the icon and text</returns>
        private StackPanel CreateItemHeader(string path, string name)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Create icon
            Image icon = new Image { Width = 16, Height = 16, Margin = new Thickness(0, 0, 5, 0) };
            
            // Set icon source based on file/folder type
            if (Directory.Exists(path))
            {
                icon.Source = new BitmapImage(new Uri("/Assets/Icons/folder.png", UriKind.Relative));
            }
            else if (File.Exists(path))
            {
                // Different icon based on extension
                string ext = Path.GetExtension(path).ToLower();
                switch (ext)
                {
                    case ".pdf":
                        icon.Source = new BitmapImage(new Uri("/Assets/Icons/file-pdf.png", UriKind.Relative));
                        break;
                    case ".doc":
                    case ".docx":
                        icon.Source = new BitmapImage(new Uri("/Assets/Icons/file-doc.png", UriKind.Relative));
                        break;
                    case ".xls":
                    case ".xlsx":
                        icon.Source = new BitmapImage(new Uri("/Assets/Icons/file-xls.png", UriKind.Relative));
                        break;
                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                    case ".gif":
                    case ".bmp":
                        icon.Source = new BitmapImage(new Uri("/Assets/Icons/file-image.png", UriKind.Relative));
                        break;
                    default:
                        icon.Source = new BitmapImage(new Uri("/Assets/Icons/file.png", UriKind.Relative));
                        break;
                }
            }
            
            panel.Children.Add(icon);
            
            // Add text
            TextBlock text = new TextBlock { Text = name };
            panel.Children.Add(text);
            
            return panel;
        }
        
        /// <summary>
        /// Gets tooltip text for an item
        /// </summary>
        /// <param name="path">The item path</param>
        /// <returns>The tooltip text</returns>
        private string GetItemToolTip(string path)
        {
            // Get tags from metadata manager
            List<string> tags = _metadataManager.GetTags(path);
            
            if (tags != null && tags.Count > 0)
            {
                return $"Tags: {string.Join(", ", tags)}";
            }
            
            return "No tags";
        }
        
        /// <summary>
        /// Moves a tree item from its current parent to a new parent
        /// </summary>
        /// <param name="item">The item to move</param>
        /// <param name="newParent">The new parent</param>
        private void MoveItemToParent(TreeViewItem item, TreeViewItem newParent)
        {
            TreeViewItem? currentParent = GetItemParent(item);
            
            if (currentParent != null)
            {
                currentParent.Items.Remove(item);
            }
            else if (item.Parent == pinnedTree)
            {
                pinnedTree.Items.Remove(item);
            }
            
            newParent.Items.Add(item);
        }
        
        /// <summary>
        /// Gets the parent TreeViewItem of the given item
        /// </summary>
        /// <param name="item">The item to find the parent for</param>
        /// <returns>The parent TreeViewItem, or null if not found</returns>
        private TreeViewItem? GetItemParent(TreeViewItem item)
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(item);
            while (parent != null && !(parent is TreeViewItem))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            return parent as TreeViewItem;
        }
        
        /// <summary>
        /// Finds a TreeViewItem by its path
        /// </summary>
        /// <param name="root">The root to search from</param>
        /// <param name="path">The path to find</param>
        /// <returns>The found TreeViewItem, or null if not found</returns>
        private TreeViewItem? FindItemByPath(ItemsControl root, string path)
        {
            foreach (var item in root.Items.OfType<TreeViewItem>())
            {
                if (item.Tag is string itemPath && itemPath == path)
                {
                    return item;
                }
                
                TreeViewItem? foundItem = FindItemByPath(item, path);
                if (foundItem != null)
                {
                    return foundItem;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets the path associated with a TreeViewItem
        /// </summary>
        /// <param name="item">The item to get the path for</param>
        /// <returns>The path, or null if not found</returns>
        private string? GetItemPath(TreeViewItem item)
        {
            return item?.Tag as string;
        }
        
        /// <summary>
        /// Gets the TreeViewItem at the specified point
        /// </summary>
        /// <param name="point">The point to check</param>
        /// <returns>The TreeViewItem at the point, or null if not found</returns>
        private TreeViewItem? GetTreeViewItemFromPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(pinnedTree, point);
            if (result == null)
                return null;
                
            DependencyObject? elem = result.VisualHit;
            while (elem != null && !(elem is TreeViewItem))
            {
                elem = VisualTreeHelper.GetParent(elem);
            }
            
            return elem as TreeViewItem;
        }
        
        #endregion
        
        #region Event Handlers
        
        /// <summary>
        /// Handles clicks on tree items to navigate in the main file tree
        /// </summary>
        /// <param name="item">The clicked item</param>
        private void HandleTreeClick(TreeViewItem item)
        {
            // Check if it's a heading node
            if (item.Tag is string tag && tag == HEADING_ROLE)
                return; // Don't navigate for heading items
                
            string? itemPath = GetItemPath(item);
            if (string.IsNullOrEmpty(itemPath))
                return;
                
            if (!File.Exists(itemPath) && !Directory.Exists(itemPath))
            {
                _logger?.LogError($"Path '{itemPath}' does not exist.");
                return;
            }
            
            // Locate the main file tree and navigate
            ImprovedFileTreeListView? fileTree = FindActiveFileTree();
            if (fileTree == null)
            {
                _logger?.LogError("No active file tree found for navigation.");
                return;
            }
            
            // Navigate to the path
            Window? mainWindow = Window.GetWindow(this);
            if (mainWindow is MainWindowType window)
            {
                window.UpdateAddressBar(itemPath);
            }
            
            fileTree.NavigateAndHighlight(itemPath);
        }
        
        /// <summary>
        /// Handles double-clicks on tree items
        /// </summary>
        /// <param name="item">The double-clicked item</param>
        private void HandleDoubleClick(TreeViewItem item)
        {
            string? itemPath = GetItemPath(item);
            if (string.IsNullOrEmpty(itemPath))
                return;
                
            // If it's a folder, open in a new tab
            if (Directory.Exists(itemPath))
            {
                OpenInNewTab(itemPath);
            }
            // Otherwise, open with default app
            else if (File.Exists(itemPath))
            {
                OpenWithDefaultApplication(itemPath);
            }
        }
        
        /// <summary>
        /// Shows the context menu for a tree item
        /// </summary>
        /// <param name="item">The item to show the menu for</param>
        private void ShowContextMenu(TreeViewItem item)
        {
            string? itemPath = GetItemPath(item);
            if (string.IsNullOrEmpty(itemPath) || (item.Tag is string tag && tag == HEADING_ROLE))
                return;
                
            ContextMenu menu = new ContextMenu();
            
            // Quick Access / Open / Navigation
            MenuItem openTabMenuItem = CreateMenuItem("Open in New Tab", "Assets/Icons/open-tab.png", 
                () => OpenInNewTab(itemPath));
            menu.Items.Add(openTabMenuItem);
            
            MenuItem openWindowMenuItem = CreateMenuItem("Open in New Window", "Assets/Icons/open-window.png", 
                () => OpenInNewWindow(itemPath));
            menu.Items.Add(openWindowMenuItem);
            
            MenuItem splitViewMenuItem = CreateMenuItem("Open in Right Pane (Split View)", "Assets/Icons/split.png", 
                () => OpenInSplitView(itemPath));
            menu.Items.Add(splitViewMenuItem);
            
            menu.Items.Add(new Separator());
            
            // Pin/Unpin + Favorites
            MenuItem unpinMenuItem = CreateMenuItem("Unpin", "Assets/Icons/pin-off.png", 
                () => UnpinItem(itemPath));
            menu.Items.Add(unpinMenuItem);
            
            if (!_pinnedManager.IsFavorite(itemPath))
            {
                MenuItem addFavoriteMenuItem = CreateMenuItem("Add to Favorites", "Assets/Icons/star.png", 
                    () => FavoriteItem(itemPath));
                menu.Items.Add(addFavoriteMenuItem);
            }
            else
            {
                MenuItem removeFavoriteMenuItem = CreateMenuItem("Remove from Favorites", "Assets/Icons/star-off.png", 
                    () => UnfavoriteItem(itemPath));
                menu.Items.Add(removeFavoriteMenuItem);
            }
            
            menu.Items.Add(new Separator());
            
            // Preview options for PDFs
            if (itemPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                MenuItem previewPdfMenuItem = CreateMenuItem("Preview PDF", "Assets/Icons/preview-pdf.png", 
                    () => PreviewPdf(itemPath));
                menu.Items.Add(previewPdfMenuItem);
                menu.Items.Add(new Separator());
            }
            
            // Explorer / File Operations
            MenuItem showInExplorerMenuItem = CreateMenuItem("Show in File Explorer", "Assets/Icons/folder.png", 
                () => ShowInFileExplorer(itemPath));
            showInExplorerMenuItem.IsEnabled = File.Exists(itemPath) || Directory.Exists(itemPath);
            menu.Items.Add(showInExplorerMenuItem);
            
            MenuItem openWithDefaultMenuItem = CreateMenuItem("Open with Default App", "Assets/Icons/external-link.png", 
                () => OpenWithDefaultApplication(itemPath));
            openWithDefaultMenuItem.IsEnabled = File.Exists(itemPath) || Directory.Exists(itemPath);
            menu.Items.Add(openWithDefaultMenuItem);
            
            MenuItem copyPathMenuItem = CreateMenuItem("Copy File Path", "Assets/Icons/copy.png", 
                () => CopyFilePath(itemPath));
            menu.Items.Add(copyPathMenuItem);
            
            menu.Items.Add(new Separator());
            
            // Rename / Properties
            MenuItem renameMenuItem = CreateMenuItem("Rename", "Assets/Icons/folder-pen.png", 
                () => RenamePinnedItem(item));
            renameMenuItem.IsEnabled = File.Exists(itemPath) || Directory.Exists(itemPath);
            menu.Items.Add(renameMenuItem);
            
            MenuItem propertiesMenuItem = CreateMenuItem("Properties", "Assets/Icons/info.png", 
                () => ShowItemProperties(itemPath));
            propertiesMenuItem.IsEnabled = File.Exists(itemPath) || Directory.Exists(itemPath);
            menu.Items.Add(propertiesMenuItem);
            
            menu.Items.Add(new Separator());
            
            // Tags
            MenuItem addTagMenuItem = CreateMenuItem("Add Tag", "Assets/Icons/add-tag.png", 
                () => AddTagToItem(item));
            menu.Items.Add(addTagMenuItem);
            
            MenuItem removeTagMenuItem = CreateMenuItem("Remove Tag", "Assets/Icons/remove-tag.png", 
                () => RemoveTagFromItem(item));
            menu.Items.Add(removeTagMenuItem);
            
            menu.Items.Add(new Separator());
            
            // Refresh
            MenuItem refreshMenuItem = CreateMenuItem("Refresh Pinned Items", "Assets/Icons/refresh.png", 
                () => RefreshPinnedItems());
            menu.Items.Add(refreshMenuItem);
            
            // Show the menu
            item.ContextMenu = menu;
            menu.IsOpen = true;
        }
        
        /// <summary>
        /// Creates a menu item with the specified properties
        /// </summary>
        /// <param name="header">The menu item header</param>
        /// <param name="iconPath">The path to the icon</param>
        /// <param name="clickHandler">The click handler</param>
        /// <returns>The created MenuItem</returns>
        private MenuItem CreateMenuItem(string header, string iconPath, Action clickHandler)
        {
            MenuItem menuItem = new MenuItem { Header = header };
            
            try
            {
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    Image icon = new Image
                    {
                        Source = new BitmapImage(new Uri(iconPath, UriKind.Relative)),
                        Width = 16,
                        Height = 16
                    };
                    menuItem.Icon = icon;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error creating menu item icon: {iconPath}");
            }
            
            menuItem.Click += (s, e) => clickHandler();
            return menuItem;
        }
        
        #endregion
        
        #region Navigation and Operations
        
        /// <summary>
        /// Finds the active file tree in the main window
        /// </summary>
        /// <returns>The active ImprovedFileTreeListView, or null if not found</returns>
        private ImprovedFileTreeListView? FindActiveFileTree()
        {
            Window? mainWindow = Window.GetWindow(this);
            if (mainWindow == null)
                return null;
                
            // This implementation depends on your MainWindow structure
            // You need to adapt it to match your actual implementation
            if (mainWindow is MainWindowType window)
            {
                return window.GetActiveFileTree();
            }
            
            return null;
        }
        
        /// <summary>
        /// Opens a path in a new tab
        /// </summary>
        /// <param name="itemPath">The path to open</param>
        private void OpenInNewTab(string itemPath)
        {
            if (!File.Exists(itemPath) && !Directory.Exists(itemPath))
            {
                _logger?.LogError($"Path does not exist - {itemPath}");
                return;
            }
            
            Window? mainWindow = Window.GetWindow(this);
            if (mainWindow is MainWindowType window)
            {
                window.OpenItemInNewTab(itemPath);
            }
        }
        
        /// <summary>
        /// Opens a path in a new window
        /// </summary>
        /// <param name="itemPath">The path to open</param>
        private void OpenInNewWindow(string itemPath)
        {
            if (!File.Exists(itemPath) && !Directory.Exists(itemPath))
            {
                _logger?.LogError($"Path does not exist - {itemPath}");
                return;
            }
            
            Window? mainWindow = Window.GetWindow(this);
            if (mainWindow is MainWindowType window)
            {
                window.OpenItemInNewWindow(itemPath);
            }
        }
        
        /// <summary>
        /// Opens a path in a split view
        /// </summary>
        /// <param name="itemPath">The path to open</param>
        private void OpenInSplitView(string itemPath)
        {
            if (!File.Exists(itemPath) && !Directory.Exists(itemPath))
            {
                _logger?.LogError($"Path does not exist - {itemPath}");
                return;
            }
            
            Window? mainWindow = Window.GetWindow(this);
            if (mainWindow is MainWindowType window)
            {
                window.ToggleSplitView(itemPath);
            }
        }
        
        /// <summary>
        /// Shows a file/folder in File Explorer
        /// </summary>
        /// <param name="itemPath">The path to show</param>
        private void ShowInFileExplorer(string itemPath)
        {
            if (File.Exists(itemPath) || Directory.Exists(itemPath))
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{itemPath}\"");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error opening Explorer for {itemPath}");
                }
            }
            else
            {
                _logger?.LogError($"Path does not exist - {itemPath}");
            }
        }
        
        /// <summary>
        /// Opens a file with the default application
        /// </summary>
        /// <param name="filePath">The file to open</param>
        private void OpenWithDefaultApplication(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error opening file: {filePath}");
                }
            }
            else
            {
                _logger?.LogError($"File does not exist - {filePath}");
            }
        }
        
        /// <summary>
        /// Copies a file path to the clipboard
        /// </summary>
        /// <param name="filePath">The path to copy</param>
        private void CopyFilePath(string filePath)
        {
            try
            {
                Clipboard.SetText(filePath);
                _logger?.LogInformation($"Copied to clipboard: {filePath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error copying to clipboard");
            }
        }
        
        /// <summary>
        /// Renames a pinned item
        /// </summary>
        /// <param name="item">The item to rename</param>
        private void RenamePinnedItem(TreeViewItem item)
        {
            string? itemPath = GetItemPath(item);
            if (string.IsNullOrEmpty(itemPath) || !File.Exists(itemPath) && !Directory.Exists(itemPath))
            {
                _logger?.LogError("Cannot rename, path does not exist.");
                return;
            }
            
            // Show a dialog to get the new name
            string currentName = Path.GetFileName(itemPath);
            InputDialog dialog = new InputDialog("Rename Item", "Enter new name:", currentName);
            
            if (dialog.ShowDialog() == true)
            {
                string newName = dialog.ResponseText;
                if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
                {
                    string? directoryPath = Path.GetDirectoryName(itemPath);
                    string newPath = Path.Combine(directoryPath ?? string.Empty, newName);
                    try
                    {
                        // Check if target already exists
                        if (File.Exists(newPath) || Directory.Exists(newPath))
                        {
                            MessageBox.Show($"Cannot rename: {newName} already exists.", 
                                "Rename Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        
                        // Rename the file/folder
                        if (File.Exists(itemPath))
                        {
                            File.Move(itemPath, newPath);
                        }
                        else if (Directory.Exists(itemPath))
                        {
                            Directory.Move(itemPath, newPath);
                        }
                        
                        // Update metadata
                        _metadataManager.UpdatePathReferences(itemPath, newPath);
                        
                        // Update pinned/favorite items
                        _pinnedManager.UpdatePathReferences(itemPath, newPath);
                        
                        // Raise event
                        PinnedItemModified?.Invoke(this, new ItemModifiedEventArgs(itemPath, newPath));
                        
                        // Refresh the tree
                        RefreshPinnedItems();
                        
                        _logger?.LogInformation($"Renamed '{itemPath}' to '{newPath}'");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error renaming file: {ex.Message}");
                        MessageBox.Show($"Error renaming file: {ex.Message}", 
                            "Rename Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        
        /// <summary>
        /// Shows properties for an item
        /// </summary>
        /// <param name="itemPath">The path to show properties for</param>
        private void ShowItemProperties(string itemPath)
        {
            // Implementation for showing item properties
            _logger?.LogInformation($"Showing properties for: {itemPath}");
        }
        
        /// <summary>
        /// Previews a PDF file
        /// </summary>
        /// <param name="filePath">The PDF file to preview</param>
        private void PreviewPdf(string filePath)
        {
            // Implementation for PDF preview
            _logger?.LogInformation($"Preview PDF not implemented. Path: {filePath}");
        }
        
        #endregion
        
        #region Tag Management
        
        /// <summary>
        /// Adds a tag to an item
        /// </summary>
        /// <param name="item">The item to tag</param>
        private void AddTagToItem(TreeViewItem item)
        {
            string? itemPath = GetItemPath(item);
            if (string.IsNullOrEmpty(itemPath))
            {
                _logger?.LogError("Cannot add tag, item path is invalid.");
                return;
            }
            
            // Show a dialog to get the tag
            InputDialog dialog = new InputDialog("Add Tag", 
                $"Enter a tag for pinned item: {Path.GetFileName(itemPath)}", "");
            
            if (dialog.ShowDialog() == true)
            {
                string tag = dialog.ResponseText;
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    // Use MetadataManager
                    List<string> currentTags = _metadataManager.GetTags(itemPath);
                    if (currentTags == null || !currentTags.Contains(tag))
                    {
                        _metadataManager.AddTag(itemPath, tag);
                        MessageBox.Show($"Tag '{tag}' added to '{Path.GetFileName(itemPath)}'.", 
                            "Tag Added", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"'{Path.GetFileName(itemPath)}' already has tag '{tag}'.", 
                            "Tag Exists", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    
                    RefreshPinnedItems();  // Re-build the tree, updating tooltips
                }
            }
        }
        
        /// <summary>
        /// Removes a tag from an item
        /// </summary>
        /// <param name="item">The item to remove a tag from</param>
        private void RemoveTagFromItem(TreeViewItem item)
        {
            string? itemPath = GetItemPath(item);
            if (string.IsNullOrEmpty(itemPath))
            {
                _logger?.LogError("Cannot remove tag, item path is invalid.");
                return;
            }
            
            List<string> currentTags = _metadataManager.GetTags(itemPath);
            if (currentTags == null || currentTags.Count == 0)
            {
                MessageBox.Show($"No tags available for '{Path.GetFileName(itemPath)}'.", 
                    "No Tags", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // Show a dialog to select which tag to remove
            SelectionDialog dialog = new SelectionDialog("Remove Tag", 
                $"Select a tag to remove from pinned item '{Path.GetFileName(itemPath)}':", 
                currentTags.ToArray());
            
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedItem))
            {
                string selectedTag = dialog.SelectedItem;
                _metadataManager.RemoveTag(itemPath, selectedTag);
                MessageBox.Show($"Removed '{selectedTag}' from '{Path.GetFileName(itemPath)}'.", 
                    "Tag Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                
                RefreshPinnedItems();
            }
        }
        
        #endregion
        
        #region MainWindow Interface Methods
        
        /// <summary>
        /// Gets whether signals are connected.
        /// </summary>
        /// <returns>True if signals are connected</returns>
        public bool GetIsSignalsConnected()
        {
            return _isSignalsConnected;
        }
        
        /// <summary>
        /// Sets the signals connected state.
        /// </summary>
        /// <param name="connected">Whether signals are connected</param>
        public void SetIsSignalsConnected(bool connected)
        {
            _isSignalsConnected = connected;
        }
        
        /// <summary>
        /// Handles a file or folder being renamed.
        /// </summary>
        /// <param name="oldPath">The original path</param>
        /// <param name="newPath">The new path</param>
        public void HandleItemRename(string oldPath, string newPath)
        {
            if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
                return;
            
            // Check if the renamed item is in pinned items
            if (_pinnedManager.HasPinnedItem(oldPath))
            {
                // Remove the old path and add the new one
                _pinnedManager.RemovePinnedItem(oldPath);
                _pinnedManager.AddPinnedItem(newPath);
                
                // Notify listeners of the change
                PinnedItemModified?.Invoke(this, new ItemModifiedEventArgs(oldPath, newPath));
            }
            
            // Refresh the pinned items tree
            RefreshPinnedItems();
        }
        
        /// <summary>
        /// Refreshes the items in the pinned panel.
        /// </summary>
        public void RefreshItems()
        {
            RefreshPinnedItems();
        }
        
        /// <summary>
        /// Adds a new pinned item.
        /// </summary>
        /// <param name="path">The path to pin</param>
        public void AddPinnedItem(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) && !Directory.Exists(path))
                return;
            
            PinItem(path);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Simple dialog for getting text input
    /// </summary>
    public class InputDialog : Window
    {
        private TextBox responseTextBox;
        
        public string ResponseText
        {
            get { return responseTextBox.Text; }
            set { responseTextBox.Text = value; }
        }
        
        public InputDialog(string title, string promptText, string defaultResponse = "")
        {
            Title = title;
            Width = 350;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            TextBlock promptTextBlock = new TextBlock
            {
                Text = promptText,
                Margin = new Thickness(10, 10, 10, 0)
            };
            grid.Children.Add(promptTextBlock);
            Grid.SetRow(promptTextBlock, 0);
            
            responseTextBox = new TextBox
            {
                Margin = new Thickness(10, 5, 10, 10),
                Text = defaultResponse
            };
            grid.Children.Add(responseTextBox);
            Grid.SetRow(responseTextBox, 1);
            
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 10, 10)
            };
            
            Button okButton = new Button
            {
                Content = "OK",
                IsDefault = true,
                Width = 75,
                Margin = new Thickness(0, 0, 5, 0)
            };
            okButton.Click += (s, e) => { DialogResult = true; };
            
            Button cancelButton = new Button
            {
                Content = "Cancel",
                IsCancel = true,
                Width = 75
            };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 2);
            
            Content = grid;
        }
    }
    
    /// <summary>
    /// Simple dialog for selecting an item from a list
    /// </summary>
    public class SelectionDialog : Window
    {
        private ComboBox selectionComboBox;
        
        public string? SelectedItem
        {
            get { return selectionComboBox.SelectedItem as string; }
        }
        
        public SelectionDialog(string title, string promptText, string[] items)
        {
            Title = title;
            Width = 350;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            TextBlock promptTextBlock = new TextBlock
            {
                Text = promptText,
                Margin = new Thickness(10, 10, 10, 0)
            };
            grid.Children.Add(promptTextBlock);
            Grid.SetRow(promptTextBlock, 0);
            
            selectionComboBox = new ComboBox
            {
                Margin = new Thickness(10, 5, 10, 10),
                ItemsSource = items
            };
            
            if (items.Length > 0)
                selectionComboBox.SelectedIndex = 0;
                
            grid.Children.Add(selectionComboBox);
            Grid.SetRow(selectionComboBox, 1);
            
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 10, 10)
            };
            
            Button okButton = new Button
            {
                Content = "OK",
                IsDefault = true,
                Width = 75,
                Margin = new Thickness(0, 0, 5, 0)
            };
            okButton.Click += (s, e) => { DialogResult = true; };
            
            Button cancelButton = new Button
            {
                Content = "Cancel",
                IsCancel = true,
                Width = 75
            };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 2);
            
            Content = grid;
        }
    }
}