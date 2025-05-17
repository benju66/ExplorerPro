// UI/Controls/TreeListView.cs - Fixed Version

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

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
        /// Called when IsExpanded property changes - FIXED to ensure LoadChildren is directly called
        /// </summary>
        private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListViewItem item && item.DataContext != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] IsExpanded property changed to {e.NewValue} for {item.DataContext}");
                    
                    var dataItem = item.DataContext;
                    
                    // CRITICAL FIX: Directly invoke the LoadChildren event on the data item when expanded
                    if ((bool)e.NewValue == true)
                    {
                        // Check for HasDummyChild method
                        var hasDummyChildMethod = dataItem.GetType().GetMethod("HasDummyChild");
                        bool hasDummy = false;
                        
                        if (hasDummyChildMethod != null)
                        {
                            hasDummy = (bool)hasDummyChildMethod.Invoke(dataItem, null);
                        }
                        
                        // Check for IsDirectory property
                        var isDirectoryProperty = dataItem.GetType().GetProperty("IsDirectory");
                        bool isDirectory = false;
                        
                        if (isDirectoryProperty != null)
                        {
                            isDirectory = (bool)isDirectoryProperty.GetValue(dataItem);
                        }
                        
                        // If it's a directory and has a dummy child, directly invoke LoadChildren
                        if (isDirectory && (hasDummy || hasDummyChildMethod == null))
                        {
                            System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Directly invoking LoadChildren event via reflection");
                            
                            // Get the LoadChildren event and invoke it directly
                            var loadChildrenEvent = dataItem.GetType().GetEvent("LoadChildren");
                            if (loadChildrenEvent != null)
                            {
                                var delegates = DelegateUtilities.GetInvocationList(dataItem, loadChildrenEvent);
                                if (delegates != null && delegates.Count > 0)
                                {
                                    foreach (var del in delegates)
                                    {
                                        del.DynamicInvoke(dataItem, EventArgs.Empty);
                                    }
                                    System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Successfully invoked {delegates.Count} LoadChildren delegates");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("[TREELISTVIEW] No LoadChildren delegates found to invoke");
                                }
                            }
                        }
                    }
                    
                    // Find parent TreeListView
                    TreeListView treeListView = FindParent<TreeListView>(item);
                    if (treeListView != null)
                    {
                        // Use Dispatcher to ensure event handling happens after UI updates
                        treeListView.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            // Raise expansion event
                            treeListView.RaiseTreeItemExpanded(dataItem, (bool)e.NewValue);
                            
                            // Force layout update to ensure children are displayed
                            treeListView.UpdateLayout();
                        }));
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[TREELISTVIEW] Failed to find parent TreeListView");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Error in IsExpanded change handler: {ex.Message}");
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
            
            // Additional setup for container generation
            ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
            
            // Attach to ItemsSource change events
            this.Loaded += TreeListView_Loaded;
        }
        
        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                System.Diagnostics.Debug.WriteLine("[TREELISTVIEW] Containers generated, ensuring expansion states");
                
                // Check for any expanded items that need children loaded
                EnsureExpandedItemsHaveChildren();
            }
        }
        
        private void TreeListView_Loaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[TREELISTVIEW] TreeListView loaded");
            EnsureExpandedItemsHaveChildren();
        }
        
        /// <summary>
        /// Ensures all expanded items have their children loaded
        /// </summary>
        private void EnsureExpandedItemsHaveChildren()
        {
            // This method helps fix issues where items appear expanded but children aren't loaded
            System.Diagnostics.Debug.WriteLine("[TREELISTVIEW] Ensuring expanded items have children");
            
            try
            {
                foreach (var item in Items)
                {
                    var container = ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                    if (container != null)
                    {
                        bool isExpanded = GetIsExpanded(container);
                        if (isExpanded)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Found expanded item: {item}");
                            
                            // IMPROVED: More robust checking for directory items that need children loaded
                            bool isDirectory = false;
                            var isDirectoryProperty = item.GetType().GetProperty("IsDirectory");
                            if (isDirectoryProperty != null)
                            {
                                isDirectory = (bool)isDirectoryProperty.GetValue(item);
                            }
                            
                            if (isDirectory)
                            {
                                // Check if this is a FileTreeItem with HasDummyChild method
                                var hasDummyChildMethod = item.GetType().GetMethod("HasDummyChild");
                                bool hasDummy = false;
                                
                                if (hasDummyChildMethod != null)
                                {
                                    hasDummy = (bool)hasDummyChildMethod.Invoke(item, null);
                                }
                                
                                var childrenProperty = item.GetType().GetProperty("Children");
                                bool hasNoChildren = false;
                                
                                if (childrenProperty != null)
                                {
                                    var children = childrenProperty.GetValue(item);
                                    if (children != null)
                                    {
                                        var countProperty = children.GetType().GetProperty("Count");
                                        if (countProperty != null)
                                        {
                                            int count = (int)countProperty.GetValue(children);
                                            hasNoChildren = count == 0;
                                        }
                                    }
                                }
                                
                                if (hasDummy || hasNoChildren)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Found expanded item with dummy child or no children, raising event");
                                    
                                    // FIX: Invoke LoadChildren directly for more reliability
                                    var loadChildrenEvent = item.GetType().GetEvent("LoadChildren");
                                    if (loadChildrenEvent != null)
                                    {
                                        var delegates = DelegateUtilities.GetInvocationList(item, loadChildrenEvent);
                                        if (delegates != null && delegates.Count > 0)
                                        {
                                            foreach (var del in delegates)
                                            {
                                                del.DynamicInvoke(item, EventArgs.Empty);
                                            }
                                            System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Successfully invoked {delegates.Count} LoadChildren delegates");
                                        }
                                    }
                                    
                                    // Also raise the normal event
                                    RaiseTreeItemExpanded(item, true);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Error ensuring expanded items have children: {ex.Message}");
            }
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
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            return parent as T;
        }
        
        #endregion
        
        #region Overrides
        
        /// <summary>
        /// Override OnItemsSourceChanged to handle changes to the ItemsSource
        /// </summary>
        protected override void OnItemsSourceChanged(System.Collections.IEnumerable oldValue, System.Collections.IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);
            
            // Log the change
            System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] ItemsSource changed from {oldValue} to {newValue}");
            
            // Schedule a check for expanded items after binding completes
            if (newValue != null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => {
                    EnsureExpandedItemsHaveChildren();
                }));
            }
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
                try
                {
                    // Log the preparation
                    System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Preparing container for item: {item}");
                    
                    // Check if the item has an IsExpanded property
                    var itemType = item.GetType();
                    var expandedProperty = itemType.GetProperty("IsExpanded");
                    
                    if (expandedProperty != null)
                    {
                        // Get the current value
                        bool isExpanded = (bool)expandedProperty.GetValue(item);
                        
                        // Set the attached property
                        SetIsExpanded(container, isExpanded);
                        
                        System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Set IsExpanded={isExpanded} on container for item: {item}");
                        
                        // If it's expanded, make sure children are loaded
                        if (isExpanded)
                        {
                            // NEW APPROACH: Direct invocation for directory items                            
                            bool isDirectory = false;
                            var isDirectoryProperty = itemType.GetProperty("IsDirectory");
                            if (isDirectoryProperty != null)
                            {
                                isDirectory = (bool)isDirectoryProperty.GetValue(item);
                            }
                            
                            if (isDirectory)
                            {
                                // Check if this item has dummy children
                                bool hasDummy = false;
                                var hasDummyChildMethod = itemType.GetMethod("HasDummyChild");
                                if (hasDummyChildMethod != null)
                                {
                                    hasDummy = (bool)hasDummyChildMethod.Invoke(item, null);
                                }
                                
                                if (hasDummy)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Found expanded directory with dummy child, scheduling LoadChildren");
                                    
                                    // Schedule event on a later priority to allow container preparation to complete
                                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
                                        // Directly invoke the LoadChildren event
                                        var loadChildrenEvent = itemType.GetEvent("LoadChildren");
                                        if (loadChildrenEvent != null)
                                        {
                                            var delegates = DelegateUtilities.GetInvocationList(item, loadChildrenEvent);
                                            if (delegates != null && delegates.Count > 0)
                                            {
                                                foreach (var del in delegates)
                                                {
                                                    del.DynamicInvoke(item, EventArgs.Empty);
                                                }
                                                System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Successfully invoked {delegates.Count} LoadChildren delegates");
                                            }
                                        }
                                        
                                        // Also raise normal event
                                        RaiseTreeItemExpanded(item, true);
                                    }));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TREELISTVIEW] Error in PrepareContainerForItemOverride: {ex.Message}");
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
    
    /// <summary>
    /// Utility class for working with delegates
    /// </summary>
    public static class DelegateUtilities
    {
        /// <summary>
        /// Get the invocation list for an event
        /// </summary>
        public static List<Delegate> GetInvocationList(object target, System.Reflection.EventInfo eventInfo)
        {
            try
            {
                // Get the field that stores the delegate
                var eventField = target.GetType().GetField(eventInfo.Name,
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                
                if (eventField != null)
                {
                    var eventDelegate = eventField.GetValue(target) as Delegate;
                    if (eventDelegate != null)
                    {
                        return new List<Delegate>(eventDelegate.GetInvocationList());
                    }
                }
                
                return new List<Delegate>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting invocation list: {ex.Message}");
                return new List<Delegate>();
            }
        }
    }
}
