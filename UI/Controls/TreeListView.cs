// UI/Controls/TreeListView.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// A custom control that combines the TreeView hierarchy with ListView columns.
    /// </summary>
    public class TreeListView : ListView
    {
        #region Dependency Properties

        /// <summary>
        /// Dependency property for item indentation.
        /// </summary>
        public static readonly DependencyProperty IndentationProperty =
            DependencyProperty.Register("Indentation", typeof(double), typeof(TreeListView), 
                new PropertyMetadata(16.0));

        /// <summary>
        /// Gets or sets the indentation for tree items.
        /// </summary>
        public double Indentation
        {
            get { return (double)GetValue(IndentationProperty); }
            set { SetValue(IndentationProperty, value); }
        }

        /// <summary>
        /// Dependency property for the currently selected tree item
        /// </summary>
        public static readonly DependencyProperty SelectedTreeItemProperty =
            DependencyProperty.Register("SelectedTreeItem", typeof(object), typeof(TreeListView), 
                new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the selected tree item
        /// </summary>
        public object SelectedTreeItem
        {
            get { return GetValue(SelectedTreeItemProperty); }
            set { SetValue(SelectedTreeItemProperty, value); }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when an item is expanded or collapsed
        /// </summary>
        public event EventHandler<TreeItemExpandedEventArgs> TreeItemExpanded;

        /// <summary>
        /// Event fired when the selected item changes
        /// </summary>
        public event EventHandler<RoutedPropertyChangedEventArgs<object>> SelectedTreeItemChanged;

        #endregion

        #region Private Fields

        private bool _isInternalChange;
        private GridViewColumnHeader _lastHeaderClicked;
        private ListSortDirection _lastSortDirection = ListSortDirection.Ascending;
        private Dictionary<string, ListSortDirection> _columnSortDirections = new Dictionary<string, ListSortDirection>();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the TreeListView.
        /// </summary>
        public TreeListView()
        {
            // Set default styles and properties
            SelectionMode = SelectionMode.Extended;
            
            // Handle selection change with custom event
            SelectionChanged += OnSelectionChanged;
            
            // Force to always use a GridView for columns
            var gridView = new GridView();
            View = gridView;
            
            // Add column header click handler for sorting
            AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnColumnHeaderClick));
            
            // Override default style
            DefaultStyleKey = typeof(TreeListView);
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Called when the template is applied.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            // Add the ScrollViewer.ScrollChanged handler for horizontal scrolling
            if (Template.FindName("PART_ScrollViewer", this) is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollChanged += OnScrollChanged;
            }
        }
        
        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles GridViewColumnHeader click for sorting.
        /// </summary>
        private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader headerClicked)
            {
                // Skip the click if it's on the expander column image
                if (headerClicked.Column == null || headerClicked.Role == GridViewColumnHeaderRole.Padding)
                    return;

                string headerName = headerClicked.Column.Header?.ToString() ?? string.Empty;
                
                // If we don't have a binding, we can't sort
                if (!(headerClicked.Column is GridViewColumn column) || 
                    !(column.DisplayMemberBinding is Binding binding) ||
                    string.IsNullOrEmpty(binding.Path.Path))
                {
                    return;
                }

                string sortPropertyName = binding.Path.Path;
                ListSortDirection direction;

                // Determine sort direction
                if (_lastHeaderClicked == headerClicked)
                {
                    direction = _lastSortDirection == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;
                }
                else
                {
                    // Get the previous direction for this column, or default to ascending
                    if (_columnSortDirections.TryGetValue(headerName, out ListSortDirection previousDirection))
                    {
                        direction = previousDirection == ListSortDirection.Ascending
                            ? ListSortDirection.Descending
                            : ListSortDirection.Ascending;
                    }
                    else
                    {
                        direction = ListSortDirection.Ascending;
                    }
                }

                // Store direction for this column
                _columnSortDirections[headerName] = direction;

                // Sort
                SortByColumn(sortPropertyName, direction);

                // Remember the header and direction
                _lastHeaderClicked = headerClicked;
                _lastSortDirection = direction;
            }
        }

        /// <summary>
        /// Handles scroll changed to maintain horizontal scroll position on expansion.
        /// </summary>
        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Handle horizontal scrolling logic if needed
        }

        /// <summary>
        /// Handles selection changed event.
        /// </summary>
        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInternalChange)
                return;

            if (e.AddedItems.Count > 0)
            {
                var oldValue = SelectedTreeItem;
                var newValue = e.AddedItems[0];
                
                // Update the dependency property
                SelectedTreeItem = newValue;
                
                // Raise the custom event
                SelectedTreeItemChanged?.Invoke(this, 
                    new RoutedPropertyChangedEventArgs<object>(oldValue, newValue));
            }
            else if (SelectedTreeItem != null && e.RemovedItems.Count > 0 && e.AddedItems.Count == 0)
            {
                // Selection cleared
                var oldValue = SelectedTreeItem;
                SelectedTreeItem = null;
                
                // Raise the custom event
                SelectedTreeItemChanged?.Invoke(this, 
                    new RoutedPropertyChangedEventArgs<object>(oldValue, null));
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sorts the items by the specified property and direction.
        /// </summary>
        public void SortByColumn(string propertyName, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(propertyName))
                return;

            // Get the current view
            if (ItemsSource is ICollectionView view)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(propertyName, direction));
                view.Refresh();
            }
            else if (Items.Count > 0 && Items.CanSort)
            {
                // For direct Items collection
                Items.SortDescriptions.Clear();
                Items.SortDescriptions.Add(new SortDescription(propertyName, direction));
                Items.Refresh();
            }
        }

        /// <summary>
        /// Refreshes the full view
        /// </summary>
        public void RefreshView()
        {
            if (ItemsSource is ICollectionView view)
            {
                view.Refresh();
            }
            else if (Items.Count > 0)
            {
                Items.Refresh();
            }
        }

        /// <summary>
        /// Selects an item by object reference
        /// </summary>
        public void SelectItem(object item)
        {
            if (item == null)
                return;

            // Try to find the item in the collection
            foreach (var listItem in Items)
            {
                if (listItem == item)
                {
                    _isInternalChange = true;
                    SelectedItem = listItem;
                    _isInternalChange = false;
                    break;
                }
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Raise the item expanded event
        /// </summary>
        internal void RaiseItemExpandedEvent(object item, bool isExpanded)
        {
            TreeItemExpanded?.Invoke(this, new TreeItemExpandedEventArgs(item, isExpanded));
        }

        #endregion
    }

    /// <summary>
    /// EventArgs for tree item expansion state changes
    /// </summary>
    public class TreeItemExpandedEventArgs : EventArgs
    {
        /// <summary>
        /// The item that was expanded or collapsed
        /// </summary>
        public object Item { get; }
        
        /// <summary>
        /// Whether the item is expanded
        /// </summary>
        public bool IsExpanded { get; }

        /// <summary>
        /// Creates a new instance of TreeItemExpandedEventArgs
        /// </summary>
        public TreeItemExpandedEventArgs(object item, bool isExpanded)
        {
            Item = item;
            IsExpanded = isExpanded;
        }
    }
}