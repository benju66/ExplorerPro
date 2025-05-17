using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Helper class to provide level information for TreeViewItems
    /// </summary>
    public static class TreeViewItemExtensions
    {
        /// <summary>
        /// Gets the hierarchical level of a TreeViewItem (how deeply nested it is)
        /// </summary>
        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.RegisterAttached("Level", typeof(int), typeof(TreeViewItemExtensions),
                new PropertyMetadata(0, OnLevelPropertyChanged));

        /// <summary>
        /// Gets the level of the specified item
        /// </summary>
        public static int GetLevel(DependencyObject obj)
        {
            return (int)obj.GetValue(LevelProperty);
        }

        /// <summary>
        /// Sets the level of the specified item
        /// </summary>
        public static void SetLevel(DependencyObject obj, int value)
        {
            obj.SetValue(LevelProperty, value);
        }

        /// <summary>
        /// Called when the Level property is changed
        /// </summary>
        private static void OnLevelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeViewItem item)
            {
                // Update the level of all child items when parent level changes
                foreach (var child in GetChildTreeViewItems(item))
                {
                    SetLevel(child, (int)e.NewValue + 1);
                }
            }
        }

        /// <summary>
        /// Gets all child TreeViewItem elements of the specified item
        /// </summary>
        private static System.Collections.Generic.IEnumerable<TreeViewItem> GetChildTreeViewItems(ItemsControl parent)
        {
            for (int i = 0; i < parent.Items.Count; i++)
            {
                TreeViewItem childItem = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (childItem != null)
                {
                    yield return childItem;
                    
                    // Handle nested items
                    foreach (var grandChild in GetChildTreeViewItems(childItem))
                    {
                        yield return grandChild;
                    }
                }
            }
        }
        
        /// <summary>
        /// Attach this to the TreeView.Loaded event to initialize all item levels
        /// </summary>
        public static void InitializeTreeViewItemLevels(TreeView treeView)
        {
            foreach (var item in GetChildTreeViewItems(treeView))
            {
                // Get parent TreeViewItem
                var parent = GetParentTreeViewItem(item);
                if (parent != null)
                {
                    // Set level based on parent's level + 1
                    SetLevel(item, GetLevel(parent) + 1);
                }
                else
                {
                    // Root level item
                    SetLevel(item, 0);
                }
            }
        }
        
        /// <summary>
        /// Gets the parent TreeViewItem of the specified item
        /// </summary>
        private static TreeViewItem GetParentTreeViewItem(DependencyObject item)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(item);
            while (parent != null && !(parent is TreeViewItem))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as TreeViewItem;
        }
    }
}