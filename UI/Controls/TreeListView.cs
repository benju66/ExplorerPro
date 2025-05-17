// UI/Controls/TreeListView.cs

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Custom TreeListView control that combines features of TreeView and ListView
    /// </summary>
    public class TreeListView : ListView
    {
        #region Dependency Properties
        
        /// <summary>
        /// Dependency property for handling expanded state
        /// </summary>
        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.RegisterAttached(
                "IsExpanded",
                typeof(bool),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsExpandedChanged));

        /// <summary>
        /// Gets the IsExpanded attached property
        /// </summary>
        public static bool GetIsExpanded(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsExpandedProperty);
        }

        /// <summary>
        /// Sets the IsExpanded attached property
        /// </summary>
        public static void SetIsExpanded(DependencyObject obj, bool value)
        {
            obj.SetValue(IsExpandedProperty, value);
        }

        /// <summary>
        /// Called when IsExpanded property changes
        /// </summary>
        private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListViewItem item && item.DataContext != null)
            {
                System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] IsExpanded property changed to {e.NewValue} for {item.DataContext}");
                
                // Find parent TreeListView
                TreeListView? treeListView = FindParent<TreeListView>(item);
                if (treeListView != null)
                {
                    // Raise event
                    treeListView.RaiseTreeItemExpanded(item.DataContext, (bool)e.NewValue);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[TREELISTVIEW] Failed to find parent TreeListView");
                }
            }
        }
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Event raised when a tree item is expanded or collapsed
        /// </summary>
        public event EventHandler<TreeItemExpandedEventArgs> TreeItemExpanded = delegate { };
        
        /// <summary>
        /// Event raised when a tree item is selected
        /// </summary>
        public event EventHandler<RoutedPropertyChangedEventArgs<object>> SelectedTreeItemChanged = delegate { };
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Initializes a new instance of the TreeListView class
        /// </summary>
        public TreeListView() : base()
        {
            // Set up selection handling
            SelectionChanged += TreeListView_SelectionChanged;
            
            // Add diagnostics to help track initialization
            System.Diagnostics.Debug.WriteLine("[TREELISTVIEW] TreeListView control initialized");
        }
        
        #endregion
        
        #region Event Handlers
        
        /// <summary>
        /// Handles selection changed events
        /// </summary>
        private void TreeListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Raise our custom event
            if (SelectedItem != null)
            {
                System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Selection changed to: {SelectedItem}");
                SelectedTreeItemChanged?.Invoke(this, 
                    new RoutedPropertyChangedEventArgs<object>(null, SelectedItem));
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Raises the TreeItemExpanded event
        /// </summary>
        private void RaiseTreeItemExpanded(object item, bool isExpanded)
        {
            System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Raising TreeItemExpanded event for {item}, IsExpanded={isExpanded}");
            
            // Create event args
            TreeItemExpandedEventArgs args = new TreeItemExpandedEventArgs(item, isExpanded);
            
            // Invoke event
            TreeItemExpanded?.Invoke(this, args);
        }
        
        /// <summary>
        /// Finds a parent of the specified type
        /// </summary>
        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(child);
            
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            return parent as T;
        }
        
        /// <summary>
        /// Override OnItemsSourceChanged to handle changes to the ItemsSource
        /// </summary>
        protected override void OnItemsSourceChanged(System.Collections.IEnumerable oldValue, System.Collections.IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);
            
            // Log the change
            System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] ItemsSource changed from {oldValue} to {newValue}");
        }
        
        /// <summary>
        /// Override OnItemContainerStyleChanged to handle changes to the ItemContainerStyle
        /// </summary>
        protected override void OnItemContainerStyleChanged(Style oldStyle, Style newStyle)
        {
            base.OnItemContainerStyleChanged(oldStyle, newStyle);
            
            // Log the change
            System.Diagnostics.Debug.WriteLine("[TREELISTVIEW] ItemContainerStyle changed");
        }
        
        /// <summary>
        /// Override PrepareContainerForItemOverride to handle item container preparation
        /// </summary>
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            
            // Ensure that the container gets the IsExpanded binding set up properly
            if (element is ListViewItem container && item != null)
            {
                // Log the preparation
                System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Preparing container for item: {item}");
                
                // Check if the item has an IsExpanded property
                var itemType = item.GetType();
                var property = itemType.GetProperty("IsExpanded");
                
                if (property != null)
                {
                    // Get the current value
                    bool isExpanded = (bool)property.GetValue(item);
                    
                    // Set the attached property
                    SetIsExpanded(container, isExpanded);
                    
                    System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Set IsExpanded={isExpanded} on container for item: {item}");
                }
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Event arguments for tree item expanded event
    /// </summary>
    public class TreeItemExpandedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the item that was expanded or collapsed
        /// </summary>
        public object Item { get; }
        
        /// <summary>
        /// Gets a value indicating whether the item is expanded
        /// </summary>
        public bool IsExpanded { get; }
        
        /// <summary>
        /// Initializes a new instance of the TreeItemExpandedEventArgs class
        /// </summary>
        public TreeItemExpandedEventArgs(object item, bool isExpanded)
        {
            Item = item;
            IsExpanded = isExpanded;
        }
    }
}