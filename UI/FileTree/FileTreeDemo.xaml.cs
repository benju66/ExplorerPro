// UI/FileTree/FileTreeDemo.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Interaction logic for FileTreeDemo.xaml
    /// </summary>
    public partial class FileTreeDemo : Window
    {
        public FileTreeDemo()
        {
            InitializeComponent();
            
            // Set up event handlers
            fileTreeListView.LocationChanged += FileTreeListView_LocationChanged;
            fileTreeListView.ContextMenuActionTriggered += FileTreeListView_ContextMenuActionTriggered;
            
            // Start at a default location
            string defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            fileTreeListView.SetRootDirectory(defaultPath);
            
            // Update path text
            pathTextBox.Text = defaultPath;
        }
        
        private void FileTreeListView_LocationChanged(object sender, string path)
        {
            // Update path textbox
            pathTextBox.Text = path;
        }
        
        private void FileTreeListView_ContextMenuActionTriggered(object sender, Tuple<string, string> e)
        {
            string action = e.Item1;
            string path = e.Item2;
            
            // Handle action if needed
            // This demo doesn't need to handle actions, as they're already handled by the tree view
        }
        
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate up
            string currentPath = fileTreeListView.GetCurrentPath();
            string parentPath = System.IO.Path.GetDirectoryName(currentPath);
            
            if (!string.IsNullOrEmpty(parentPath))
            {
                fileTreeListView.SetRootDirectory(parentPath);
            }
        }
        
        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            // Not implemented in this demo
            MessageBox.Show("Forward navigation not implemented in this demo.", "Not Implemented");
        }
        
        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to the path in the textbox
            string path = pathTextBox.Text;
            
            if (System.IO.Directory.Exists(path))
            {
                fileTreeListView.SetRootDirectory(path);
            }
            else
            {
                MessageBox.Show($"Invalid directory: {path}", "Error");
            }
        }
        
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // Refresh the current view
            fileTreeListView.RefreshView();
        }
        
        private void ToggleHiddenButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle showing hidden files
            fileTreeListView.ToggleShowHidden();
        }
    }
}