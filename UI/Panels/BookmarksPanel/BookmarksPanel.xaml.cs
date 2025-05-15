using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ExplorerPro.Models;
using ExplorerPro.UI.MainWindow;

namespace ExplorerPro.UI.Panels.BookmarksPanel
{
    public partial class BookmarksPanel : UserControl
    {
        private readonly MetadataManager _metadataManager;

        public BookmarksPanel()
        {
            InitializeComponent();

            // Get the metadata manager from App instead of creating a new one
            _metadataManager = App.MetadataManager;

            // Initialize TreeView and refresh bookmarks
            RefreshBookmarks();
        }

        #region Core Functionality

        public void RefreshBookmarks()
        {
            bookmarksTree.Items.Clear();

            // Build a map of tag -> list of paths
            Dictionary<string, List<string>> tagToItems = new Dictionary<string, List<string>>();
            
            // Get all paths that have tags by checking all known tags
            var uniqueTags = new HashSet<string>();
            
            // Add some common tags to check
            uniqueTags.Add("Important");
            uniqueTags.Add("Work");
            uniqueTags.Add("Personal");
            uniqueTags.Add("Reference");
            uniqueTags.Add("Project");
            
            // For each tag, get all items with that tag
            foreach (string tag in uniqueTags)
            {
                var itemsWithTag = _metadataManager.GetItemsWithTag(tag);
                if (itemsWithTag.Count > 0)
                {
                    tagToItems[tag] = itemsWithTag;
                    
                    // For each item, get all its tags to find more tags
                    foreach (string path in itemsWithTag)
                    {
                        var pathTags = _metadataManager.GetTags(path);
                        foreach (string newTag in pathTags)
                        {
                            uniqueTags.Add(newTag);
                        }
                    }
                }
            }
            
            // Update the map with any newly discovered tags
            foreach (string tag in uniqueTags)
            {
                if (!tagToItems.ContainsKey(tag))
                {
                    var itemsWithTag = _metadataManager.GetItemsWithTag(tag);
                    if (itemsWithTag.Count > 0)
                    {
                        tagToItems[tag] = itemsWithTag;
                    }
                }
            }

            // Create tree items for each tag
            foreach (var pair in tagToItems)
            {
                string tagName = pair.Key;
                List<string> itemPaths = pair.Value;

                var tagItem = new TreeViewItem { Header = tagName };
                bookmarksTree.Items.Add(tagItem);

                // Add child items for each path with this tag
                foreach (string path in itemPaths)
                {
                    // Show the last part of the path or an ID, store the full path as Tag
                    string displayName = path.StartsWith("ProcoreLink:") 
                        ? path.Substring(12) // Remove "ProcoreLink:" prefix for display
                        : Path.GetFileName(path) ?? path;

                    var childItem = new TreeViewItem { Header = displayName, Tag = path };
                    tagItem.Items.Add(childItem);
                }
            }
        }

        private void BookmarksTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(e.OriginalSource is FrameworkElement element))
                return;

            var item = FindTreeViewItemParent(element);
            if (item == null)
                return;

            // Check if it's a child item (not a tag)
            if (item.Parent is TreeViewItem)
            {
                string? itemPath = item.Tag as string;
                if (string.IsNullOrEmpty(itemPath))
                    return;

                // Handle file/folder paths or Procore links
                if (File.Exists(itemPath) || Directory.Exists(itemPath))
                {
                    OpenPinnedFolderInTab(itemPath);
                }
                else if (itemPath.StartsWith("ProcoreLink:"))
                {
                    ExpandProcoreItem(itemPath);
                }
                else
                {
                    MessageBox.Show($"Unknown item path: {itemPath}", "Bookmark Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private TreeViewItem? FindTreeViewItemParent(FrameworkElement? element)
        {
            // Handle null elements
            if (element == null)
                return null;
                
            while (element != null && !(element is TreeViewItem))
            {
                element = element.Parent as FrameworkElement;
            }
            return element as TreeViewItem;
        }

        private void OpenPinnedFolderInTab(string path)
        {
            var mainWindow = FindMainWindow() as MainWindow.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.OpenDirectoryInTab(path);
            }
            else
            {
                MessageBox.Show("Cannot access main window to open tab.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExpandProcoreItem(string linkId)
        {
            // Implementation depends on how the ProcoreLinks panel is structured
            MessageBox.Show($"Procore links integration not implemented: {linkId}", "Info", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private Window? FindMainWindow()
        {
            DependencyObject parent = this;
            while (parent != null && !(parent is Window))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as Window;
        }

        #endregion

        #region Adding Bookmarks/Tags

        private void BtnAddBookmark_Click(object sender, RoutedEventArgs e)
        {
            AddBookmarkDialog();
        }

        private void AddBookmarkDialog()
        {
            // Ask for item path
            var pathDialog = new InputDialog
            {
                Title = "Add Bookmark Path",
                Question = "Enter local path or procore ID (e.g. 'ProcoreLink:Project->Link'):",
                Owner = Window.GetWindow(this)
            };

            if (pathDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(pathDialog.Answer))
                return;

            string path = pathDialog.Answer;

            // Ask for tag name
            var tagDialog = new InputDialog
            {
                Title = "Tag",
                Question = $"Enter a tag for:\n{path}",
                Owner = Window.GetWindow(this)
            };

            if (tagDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(tagDialog.Answer))
                return;

            string tag = tagDialog.Answer;

            // Add tag and refresh
            _metadataManager.AddTag(path, tag);
            RefreshBookmarks();
        }

        #endregion

        #region Context Menu

        private void BookmarksTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Clear existing context menu items
            treeContextMenu.Items.Clear();

            // Get the clicked item
            var mousePosition = Mouse.GetPosition(bookmarksTree);
            var result = VisualTreeHelper.HitTest(bookmarksTree, mousePosition);
            
            // Check for null result
            if (result?.VisualHit == null) 
            {
                e.Handled = true;
                return;
            }

            // Find TreeViewItem
            var hitElement = result.VisualHit as FrameworkElement;
            var item = FindTreeViewItemParent(hitElement);
            
            // Check if item was found
            if (item == null)
            {
                e.Handled = true;
                return;
            }

            // Create appropriate menu items based on the item type
            if (item.Parent is TreeViewItem)
            {
                // Child item (under a tag)
                string? path = item.Tag as string;
                if (path == null) return;
                
                string? tagName = (item.Parent as TreeViewItem)?.Header?.ToString();
                if (tagName == null) return;

                var removeFromTagMenuItem = new MenuItem
                {
                    Header = "Remove Tag from This Item",
                    Tag = new Tuple<string, string>(tagName, path)
                };
                removeFromTagMenuItem.Click += RemoveTagFromItem_Click;
                treeContextMenu.Items.Add(removeFromTagMenuItem);
            }
            else
            {
                // Top-level item (a tag)
                string? tagName = item.Header?.ToString();
                if (tagName == null) return;

                var removeTagMenuItem = new MenuItem
                {
                    Header = "Remove Entire Tag",
                    Tag = tagName
                };
                removeTagMenuItem.Click += RemoveEntireTag_Click;
                treeContextMenu.Items.Add(removeTagMenuItem);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshBookmarks();
        }

        private void RemoveTagFromItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Tuple<string, string> tagPathPair)
            {
                string tagName = tagPathPair.Item1;
                string path = tagPathPair.Item2;

                _metadataManager.RemoveTag(path, tagName);
                RefreshBookmarks();
            }
        }

        private void RemoveEntireTag_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem menuItem)) return;
            if (!(menuItem.Tag is string tagName)) return;

            // Get all items with this tag
            var itemsWithTag = _metadataManager.GetItemsWithTag(tagName);
            
            // Remove the tag from each item
            foreach (var path in itemsWithTag)
            {
                _metadataManager.RemoveTag(path, tagName);
            }
            
            RefreshBookmarks();
        }

        #endregion

        public void Reload()
        {
            RefreshBookmarks();
        }
    }

    public class InputDialog : Window
    {
        private TextBox textBox;
        public string Question { get; set; } = string.Empty;
        public string Answer => textBox.Text;

        public InputDialog()
        {
            Title = "Input";
            Width = 400;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = grid;

            var panel = new StackPanel { Margin = new Thickness(10) };
            var label = new TextBox
            {
                Text = Question,
                TextWrapping = TextWrapping.Wrap,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = SystemColors.ControlBrush
            };
            panel.Children.Add(label);

            textBox = new TextBox { Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(textBox);
            grid.Children.Add(panel);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var okButton = new Button
            {
                Content = "OK",
                IsDefault = true,
                MinWidth = 60,
                Margin = new Thickness(0, 0, 10, 0)
            };
            okButton.Click += (s, e) => { DialogResult = true; };

            var cancelButton = new Button
            {
                Content = "Cancel",
                IsCancel = true,
                MinWidth = 60
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            Loaded += (s, e) =>
            {
                label.Text = Question;
                textBox.Focus();
            };
        }
    }
}