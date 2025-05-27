// UI/FileTree/Services/SelectionService.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Manages multi-selection state for the file tree
    /// </summary>
    public class SelectionService : IDisposable
    {
        #region Fields
        
        private readonly ObservableCollection<FileTreeItem> _selectedItems;
        private readonly HashSet<string> _selectedPaths;
        private FileTreeItem _lastSelectedItem;
        private FileTreeItem _anchorItem;
        private bool _isSelecting;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Raised when the selection changes
        /// </summary>
        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Gets the currently selected items
        /// </summary>
        public IReadOnlyList<FileTreeItem> SelectedItems => _selectedItems;
        
        /// <summary>
        /// Gets the selected file paths
        /// </summary>
        public IReadOnlyList<string> SelectedPaths => _selectedPaths.ToList();
        
        /// <summary>
        /// Gets the count of selected items
        /// </summary>
        public int SelectionCount => _selectedItems.Count;
        
        /// <summary>
        /// Gets whether there is a selection
        /// </summary>
        public bool HasSelection => _selectedItems.Count > 0;
        
        /// <summary>
        /// Gets whether multiple items are selected
        /// </summary>
        public bool HasMultipleSelection => _selectedItems.Count > 1;
        
        /// <summary>
        /// Gets or sets whether selection is being processed
        /// </summary>
        public bool IsSelecting
        {
            get => _isSelecting;
            set => _isSelecting = value;
        }
        
        #endregion
        
        #region Constructor
        
        public SelectionService()
        {
            _selectedItems = new ObservableCollection<FileTreeItem>();
            _selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        
        #endregion
        
        #region Selection Methods
        
        /// <summary>
        /// Handles item selection with keyboard modifiers
        /// </summary>
        public void HandleSelection(FileTreeItem item, ModifierKeys modifiers, IEnumerable<FileTreeItem> allItems)
        {
            if (item == null) return;
            
            _isSelecting = true;
            
            try
            {
                var previousSelection = _selectedItems.ToList();
                
                if (modifiers.HasFlag(ModifierKeys.Control))
                {
                    // Ctrl+Click: Toggle selection
                    ToggleSelection(item);
                }
                else if (modifiers.HasFlag(ModifierKeys.Shift) && _anchorItem != null)
                {
                    // Shift+Click: Range selection
                    SelectRange(_anchorItem, item, allItems);
                }
                else
                {
                    // Normal click: Single selection
                    SelectSingle(item);
                    _anchorItem = item;
                }
                
                _lastSelectedItem = item;
                
                // Raise event if selection changed
                if (!AreSelectionsEqual(previousSelection, _selectedItems))
                {
                    OnSelectionChanged();
                }
            }
            finally
            {
                _isSelecting = false;
            }
        }
        
        /// <summary>
        /// Selects a single item, clearing previous selection
        /// </summary>
        public void SelectSingle(FileTreeItem item)
        {
            if (item == null) return;
            
            ClearSelection();
            AddToSelection(item);
            _anchorItem = item;
        }
        
        /// <summary>
        /// Toggles selection of an item
        /// </summary>
        public void ToggleSelection(FileTreeItem item)
        {
            if (item == null) return;
            
            if (_selectedPaths.Contains(item.Path))
            {
                RemoveFromSelection(item);
            }
            else
            {
                AddToSelection(item);
            }
        }
        
        /// <summary>
        /// Selects a range of items
        /// </summary>
        public void SelectRange(FileTreeItem from, FileTreeItem to, IEnumerable<FileTreeItem> allItems)
        {
            if (from == null || to == null || allItems == null) return;
            
            var flatList = FlattenTree(allItems).ToList();
            var fromIndex = flatList.IndexOf(from);
            var toIndex = flatList.IndexOf(to);
            
            if (fromIndex == -1 || toIndex == -1) return;
            
            var startIndex = Math.Min(fromIndex, toIndex);
            var endIndex = Math.Max(fromIndex, toIndex);
            
            ClearSelection();
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                AddToSelection(flatList[i]);
            }
        }
        
        /// <summary>
        /// Selects all items
        /// </summary>
        public void SelectAll(IEnumerable<FileTreeItem> allItems)
        {
            if (allItems == null) return;
            
            ClearSelection();
            
            foreach (var item in FlattenTree(allItems))
            {
                AddToSelection(item);
            }
            
            OnSelectionChanged();
        }
        
        /// <summary>
        /// Clears all selections
        /// </summary>
        public void ClearSelection()
        {
            foreach (var item in _selectedItems)
            {
                item.IsSelected = false;
            }
            
            _selectedItems.Clear();
            _selectedPaths.Clear();
        }
        
        /// <summary>
        /// Adds an item to the selection
        /// </summary>
        private void AddToSelection(FileTreeItem item)
        {
            if (item == null || _selectedPaths.Contains(item.Path)) return;
            
            item.IsSelected = true;
            _selectedItems.Add(item);
            _selectedPaths.Add(item.Path);
        }
        
        /// <summary>
        /// Removes an item from the selection
        /// </summary>
        private void RemoveFromSelection(FileTreeItem item)
        {
            if (item == null || !_selectedPaths.Contains(item.Path)) return;
            
            item.IsSelected = false;
            _selectedItems.Remove(item);
            _selectedPaths.Remove(item.Path);
        }
        
        /// <summary>
        /// Updates selection based on paths (useful after refresh)
        /// </summary>
        public void RestoreSelection(IEnumerable<FileTreeItem> allItems)
        {
            if (!_selectedPaths.Any()) return;
            
            var pathsToRestore = _selectedPaths.ToList();
            ClearSelection();
            
            foreach (var item in FlattenTree(allItems))
            {
                if (pathsToRestore.Contains(item.Path))
                {
                    AddToSelection(item);
                }
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Flattens the tree structure into a linear list
        /// </summary>
        private IEnumerable<FileTreeItem> FlattenTree(IEnumerable<FileTreeItem> items)
        {
            if (items == null) yield break;
            
            foreach (var item in items)
            {
                yield return item;
                
                if (item.IsExpanded && item.Children != null)
                {
                    foreach (var child in FlattenTree(item.Children))
                    {
                        yield return child;
                    }
                }
            }
        }
        
        /// <summary>
        /// Checks if two selections are equal
        /// </summary>
        private bool AreSelectionsEqual(IList<FileTreeItem> selection1, IList<FileTreeItem> selection2)
        {
            if (selection1.Count != selection2.Count) return false;
            
            var paths1 = selection1.Select(i => i.Path).OrderBy(p => p);
            var paths2 = selection2.Select(i => i.Path).OrderBy(p => p);
            
            return paths1.SequenceEqual(paths2);
        }
        
        /// <summary>
        /// Raises the SelectionChanged event
        /// </summary>
        private void OnSelectionChanged()
        {
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(
                _selectedItems.ToList(),
                _selectedPaths.ToList()
            ));
        }
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            ClearSelection();
            _selectedItems.Clear();
            _selectedPaths.Clear();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Event arguments for selection changes
    /// </summary>
    public class SelectionChangedEventArgs : EventArgs
    {
        public IReadOnlyList<FileTreeItem> SelectedItems { get; }
        public IReadOnlyList<string> SelectedPaths { get; }
        
        public SelectionChangedEventArgs(IReadOnlyList<FileTreeItem> selectedItems, IReadOnlyList<string> selectedPaths)
        {
            SelectedItems = selectedItems;
            SelectedPaths = selectedPaths;
        }
    }
}