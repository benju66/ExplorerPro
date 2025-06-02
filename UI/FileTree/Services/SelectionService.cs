// UI/FileTree/Services/SelectionService.cs - Performance Optimized Version
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Threading;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Manages multi-selection state for the file tree with UI mode support.
    /// This is the single source of truth for all selection state.
    /// Performance optimized version with event debouncing and efficient lookups.
    /// </summary>
    public class SelectionService : IDisposable, INotifyPropertyChanged
    {
        #region Fields
        
        private readonly ObservableCollection<FileTreeItem> _selectedItems;
        private readonly HashSet<string> _selectedPaths;
        private readonly Dictionary<string, FileTreeItem> _pathToItemMap; // Fast lookup cache
        private FileTreeItem _lastSelectedItem;
        private FileTreeItem _anchorItem;
        private bool _isSelecting;
        private bool _isMultiSelectMode;
        private bool _stickyMultiSelectMode;
        
        // Performance optimization: cached flat list
        private List<FileTreeItem> _flatTreeCache;
        private bool _flatTreeCacheValid;
        
        // Pattern selection
        private string _lastPattern;
        
        // Event debouncing
        private readonly DispatcherTimer _eventDebounceTimer;
        private bool _pendingSelectionEvent;
        private List<FileTreeItem> _pendingAddedItems;
        private List<FileTreeItem> _pendingRemovedItems;
        private const int DEBOUNCE_DELAY_MS = 50;
        
        // Performance tracking
        private DateTime _lastEventTime = DateTime.MinValue;
        private int _eventCount = 0;
        
        // Disposal flag
        private bool _disposed;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Raised when the selection changes (debounced)
        /// </summary>
        public event EventHandler<FileTreeSelectionChangedEventArgs> SelectionChanged;
        
        /// <summary>
        /// Property changed event for data binding
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        
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
        
        /// <summary>
        /// Gets or sets whether multi-selection mode is active (shows checkboxes)
        /// </summary>
        public bool IsMultiSelectMode
        {
            get => _isMultiSelectMode;
            set
            {
                if (_isMultiSelectMode != value)
                {
                    _isMultiSelectMode = value;
                    OnPropertyChanged();
                    
                    // Clear selection when exiting multi-select mode (unless sticky)
                    if (!value && !_stickyMultiSelectMode && HasMultipleSelection)
                    {
                        ClearSelection();
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets or sets whether multi-select mode is sticky (manual toggle)
        /// </summary>
        public bool StickyMultiSelectMode
        {
            get => _stickyMultiSelectMode;
            set
            {
                if (_stickyMultiSelectMode != value)
                {
                    _stickyMultiSelectMode = value;
                    if (value)
                    {
                        IsMultiSelectMode = true;
                    }
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// Gets whether all items are selected (for select all checkbox)
        /// </summary>
        public bool AreAllItemsSelected { get; private set; }
        
        /// <summary>
        /// Gets the first selected item (for single selection scenarios)
        /// </summary>
        public FileTreeItem FirstSelectedItem => _selectedItems.FirstOrDefault();
        
        /// <summary>
        /// Gets the first selected path (for single selection scenarios)
        /// </summary>
        public string FirstSelectedPath => _selectedPaths.FirstOrDefault();
        
        #endregion
        
        #region Constructor
        
        public SelectionService()
        {
            _selectedItems = new ObservableCollection<FileTreeItem>();
            _selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _pathToItemMap = new Dictionary<string, FileTreeItem>(StringComparer.OrdinalIgnoreCase);
            _flatTreeCache = new List<FileTreeItem>();
            _pendingAddedItems = new List<FileTreeItem>();
            _pendingRemovedItems = new List<FileTreeItem>();
            
            // Initialize debounce timer
            _eventDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DEBOUNCE_DELAY_MS)
            };
            _eventDebounceTimer.Tick += OnDebounceTimerTick;
        }
        
        #endregion
        
        #region Selection Methods
        
        /// <summary>
        /// Handles item selection with keyboard modifiers
        /// </summary>
        public void HandleSelection(FileTreeItem item, ModifierKeys modifiers, IEnumerable<FileTreeItem> allItems)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SelectionService));
                
            if (item == null) return;
            
            _isSelecting = true;
            InvalidateFlatTreeCache();
            
            try
            {
                var previousSelection = _selectedItems.ToList();
                
                if (modifiers.HasFlag(ModifierKeys.Control))
                {
                    // Ctrl+Click: Toggle selection
                    ToggleSelection(item);
                    
                    // Auto-enable multi-select mode when multiple items selected (unless sticky)
                    if (!_stickyMultiSelectMode && HasMultipleSelection && !IsMultiSelectMode)
                    {
                        IsMultiSelectMode = true;
                    }
                }
                else if (modifiers.HasFlag(ModifierKeys.Shift) && _anchorItem != null)
                {
                    // Shift+Click: Range selection
                    SelectRange(_anchorItem, item, allItems);
                    
                    // Auto-enable multi-select mode for range selection (unless sticky)
                    if (!_stickyMultiSelectMode && HasMultipleSelection && !IsMultiSelectMode)
                    {
                        IsMultiSelectMode = true;
                    }
                }
                else
                {
                    // Normal click: Single selection
                    SelectSingle(item);
                    _anchorItem = item;
                    
                    // Exit multi-select mode on single selection (unless sticky)
                    if (!_stickyMultiSelectMode && IsMultiSelectMode && SelectionCount <= 1)
                    {
                        IsMultiSelectMode = false;
                    }
                }
                
                _lastSelectedItem = item;
                
                // Schedule debounced event
                ScheduleSelectionChangedEvent();
            }
            finally
            {
                _isSelecting = false;
            }
        }
        
        /// <summary>
        /// Handles checkbox-based selection toggle.
        /// This method toggles selection based on SelectionService state, not item state.
        /// </summary>
        public void HandleCheckboxSelection(FileTreeItem item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SelectionService));
                
            if (item == null) return;
            
            _isSelecting = true;
            
            try
            {
                // Toggle based on current state in SelectionService, not item property
                if (_selectedPaths.Contains(item.Path))
                {
                    RemoveFromSelection(item);
                }
                else
                {
                    AddToSelection(item);
                }
                
                // Update multi-select mode based on selection count (unless sticky)
                if (!_stickyMultiSelectMode)
                {
                    if (HasMultipleSelection && !IsMultiSelectMode)
                    {
                        IsMultiSelectMode = true;
                    }
                    else if (!HasSelection || SelectionCount == 1)
                    {
                        IsMultiSelectMode = false;
                    }
                }
                
                ScheduleSelectionChangedEvent();
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
            if (_disposed)
                throw new ObjectDisposedException(nameof(SelectionService));
                
            if (item == null) return;
            
            // Batch clear for performance
            BatchClearSelection();
            AddToSelection(item);
            _anchorItem = item;
            
            ScheduleSelectionChangedEvent();
        }
        
        /// <summary>
        /// Toggles selection of an item
        /// </summary>
        public void ToggleSelection(FileTreeItem item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SelectionService));
                
            if (item == null) return;
            
            if (_selectedPaths.Contains(item.Path))
            {
                RemoveFromSelection(item);
            }
            else
            {
                AddToSelection(item);
            }
            
            ScheduleSelectionChangedEvent();
        }
        
        /// <summary>
        /// Selects a range of items
        /// </summary>
        public void SelectRange(FileTreeItem from, FileTreeItem to, IEnumerable<FileTreeItem> allItems)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SelectionService));
                
            if (from == null || to == null || allItems == null) return;
            
            var flatList = GetFlattenedTree(allItems);
            var fromIndex = flatList.IndexOf(from);
            var toIndex = flatList.IndexOf(to);
            
            if (fromIndex == -1 || toIndex == -1) return;
            
            var startIndex = Math.Min(fromIndex, toIndex);
            var endIndex = Math.Max(fromIndex, toIndex);
            
            // Batch operations for performance
            BatchClearSelection();
            
            var itemsToAdd = new List<FileTreeItem>();
            for (int i = startIndex; i <= endIndex; i++)
            {
                itemsToAdd.Add(flatList[i]);
            }
            
            BatchAddToSelection(itemsToAdd);
            ScheduleSelectionChangedEvent();
        }
        
        /// <summary>
        /// Selects all items
        /// </summary>
        public void SelectAll(IEnumerable<FileTreeItem> allItems)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SelectionService));
                
            if (allItems == null) return;
            
            // Clear and batch add for performance
            BatchClearSelection();
            
            var flatList = GetFlattenedTree(allItems);
            BatchAddToSelection(flatList);
            
            // Enable multi-select mode when selecting all (unless sticky)
            if (!_stickyMultiSelectMode && HasMultipleSelection)
            {
                IsMultiSelectMode = true;
            }
            
            // Update select all state
            UpdateSelectAllState(allItems);
            ScheduleSelectionChangedEvent();
        }
        
        /// <summary>
        /// Clears all selections
        /// </summary>
        public void ClearSelection()
        {
            if (_disposed) return;
            
            BatchClearSelection();
            AreAllItemsSelected = false;
            OnPropertyChanged(nameof(AreAllItemsSelected));
            ScheduleSelectionChangedEvent();
        }
        
        /// <summary>
        /// Checks if an item is selected - O(1) lookup
        /// </summary>
        public bool IsItemSelected(FileTreeItem item)
        {
            if (_disposed) return false;
            return item != null && _selectedPaths.Contains(item.Path);
        }
        
        /// <summary>
        /// Gets selected item by path - O(1) lookup
        /// </summary>
        public FileTreeItem GetSelectedItemByPath(string path)
        {
            if (_disposed || string.IsNullOrEmpty(path)) return null;
            
            if (_pathToItemMap.TryGetValue(path, out FileTreeItem item))
            {
                return item;
            }
            
            return null;
        }
        
        /// <summary>
        /// Handles keyboard shortcuts for selection
        /// </summary>
        public bool HandleKeyboardShortcut(Key key, ModifierKeys modifiers, IEnumerable<FileTreeItem> allItems)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SelectionService));
                
            if (key == Key.A && modifiers == ModifierKeys.Control)
            {
                // Ctrl+A - Select all
                SelectAll(allItems);
                return true;
            }
            else if (key == Key.Escape && HasSelection)
            {
                // ESC - Clear selection and exit multi-select mode (unless sticky)
                ClearSelection();
                if (!_stickyMultiSelectMode)
                {
                    IsMultiSelectMode = false;
                }
                return true;
            }
            else if (key == Key.Home && modifiers.HasFlag(ModifierKeys.Shift) && _lastSelectedItem != null)
            {
                // Shift+Home - Select from current to first
                var flatList = GetFlattenedTree(allItems);
                if (flatList.Count > 0)
                {
                    SelectRange(_lastSelectedItem, flatList[0], allItems);
                }
                return true;
            }
            else if (key == Key.End && modifiers.HasFlag(ModifierKeys.Shift) && _lastSelectedItem != null)
            {
                // Shift+End - Select from current to last
                var flatList = GetFlattenedTree(allItems);
                if (flatList.Count > 0)
                {
                    SelectRange(_lastSelectedItem, flatList[flatList.Count - 1], allItems);
                }
                return true;
            }
            else if (key == Key.Space && modifiers == ModifierKeys.Control && _lastSelectedItem != null)
            {
                // Ctrl+Space - Toggle current item
                ToggleSelection(_lastSelectedItem);
                return true;
            }
            else if ((key == Key.Up || key == Key.Down) && modifiers.HasFlag(ModifierKeys.Shift))
            {
                // Shift+Arrow - Extend selection
                return HandleArrowKeySelection(key, modifiers, allItems);
            }
            
            return false;
        }
        
        /// <summary>
        /// Handles arrow key selection extension
        /// </summary>
        private bool HandleArrowKeySelection(Key key, ModifierKeys modifiers, IEnumerable<FileTreeItem> allItems)
        {
            if (_lastSelectedItem == null) return false;
            
            var flatList = GetFlattenedTree(allItems);
            var currentIndex = flatList.IndexOf(_lastSelectedItem);
            if (currentIndex == -1) return false;
            
            int newIndex = currentIndex;
            if (key == Key.Up && currentIndex > 0)
                newIndex = currentIndex - 1;
            else if (key == Key.Down && currentIndex < flatList.Count - 1)
                newIndex = currentIndex + 1;
                
            if (newIndex != currentIndex)
            {
                var newItem = flatList[newIndex];
                
                if (modifiers.HasFlag(ModifierKeys.Shift))
                {
                    // Extend selection
                    if (_anchorItem != null)
                    {
                        SelectRange(_anchorItem, newItem, allItems);
                    }
                    else
                    {
                        AddToSelection(newItem);
                        ScheduleSelectionChangedEvent();
                    }
                }
                
                _lastSelectedItem = newItem;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Selects items matching a pattern
        /// </summary>
        public void SelectByPattern(string pattern, IEnumerable<FileTreeItem> allItems, bool addToSelection = false)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SelectionService));
                
            if (string.IsNullOrEmpty(pattern) || allItems == null) return;
            
            _lastPattern = pattern;
            
            // Convert wildcard pattern to regex
            string regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
            
            if (!addToSelection)
            {
                BatchClearSelection();
            }
            
            var flatList = GetFlattenedTree(allItems);
            var itemsToAdd = new List<FileTreeItem>();
            
            foreach (var item in flatList)
            {
                if (regex.IsMatch(item.Name))
                {
                    itemsToAdd.Add(item);
                }
            }
            
            if (itemsToAdd.Count > 0)
            {
                BatchAddToSelection(itemsToAdd);
            }
            
            // Enable multi-select mode if needed
            if (!_stickyMultiSelectMode && HasMultipleSelection && !IsMultiSelectMode)
            {
                IsMultiSelectMode = true;
            }
            
            ScheduleSelectionChangedEvent();
        }
        
        /// <summary>
        /// Inverts the current selection
        /// </summary>
        public void InvertSelection(IEnumerable<FileTreeItem> allItems)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SelectionService));
                
            if (allItems == null) return;
            
            var currentlySelected = _selectedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            BatchClearSelection();
            
            var flatList = GetFlattenedTree(allItems);
            var itemsToAdd = new List<FileTreeItem>();
            
            foreach (var item in flatList)
            {
                if (!currentlySelected.Contains(item.Path))
                {
                    itemsToAdd.Add(item);
                }
            }
            
            if (itemsToAdd.Count > 0)
            {
                BatchAddToSelection(itemsToAdd);
            }
            
            ScheduleSelectionChangedEvent();
        }
        
        /// <summary>
        /// Selects all child items of selected folders
        /// </summary>
        public void SelectChildrenOfSelectedFolders(IEnumerable<FileTreeItem> allItems)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SelectionService));
                
            var foldersToProcess = _selectedItems.Where(i => i.IsDirectory).ToList();
            var itemsToAdd = new List<FileTreeItem>();
            
            foreach (var folder in foldersToProcess)
            {
                CollectAllDescendants(folder, itemsToAdd);
            }
            
            if (itemsToAdd.Count > 0)
            {
                BatchAddToSelection(itemsToAdd);
                ScheduleSelectionChangedEvent();
            }
        }
        
        /// <summary>
        /// Updates selection based on paths (useful after refresh)
        /// </summary>
        public void RestoreSelection(IEnumerable<FileTreeItem> allItems)
        {
            if (_disposed || !_selectedPaths.Any()) return;
            
            var pathsToRestore = _selectedPaths.ToList();
            BatchClearSelection();
            
            var flatList = GetFlattenedTree(allItems);
            var itemsToAdd = new List<FileTreeItem>();
            
            foreach (var item in flatList)
            {
                if (pathsToRestore.Contains(item.Path))
                {
                    itemsToAdd.Add(item);
                }
            }
            
            if (itemsToAdd.Count > 0)
            {
                BatchAddToSelection(itemsToAdd);
            }
            
            UpdateSelectAllState(allItems);
            ScheduleSelectionChangedEvent();
        }
        
        #endregion
        
        #region Private Methods - Performance Optimized
        
        /// <summary>
        /// Batch adds items to selection for better performance
        /// </summary>
        private void BatchAddToSelection(List<FileTreeItem> items)
        {
            if (items == null || items.Count == 0) return;
            
            foreach (var item in items)
            {
                if (item != null && !_selectedPaths.Contains(item.Path))
                {
                    item.SetSelectionState(true);
                    _selectedItems.Add(item);
                    _selectedPaths.Add(item.Path);
                    _pathToItemMap[item.Path] = item;
                    
                    // Track for event
                    _pendingAddedItems.Add(item);
                }
            }
        }
        
        /// <summary>
        /// Batch clears selection for better performance
        /// </summary>
        private void BatchClearSelection()
        {
            // Track removed items for event
            _pendingRemovedItems.AddRange(_selectedItems);
            
            // Clear selection state
            foreach (var item in _selectedItems)
            {
                item.SetSelectionState(false);
            }
            
            _selectedItems.Clear();
            _selectedPaths.Clear();
            _pathToItemMap.Clear();
        }
        
        /// <summary>
        /// Adds an item to the selection.
        /// This is the only method that should set selection state.
        /// Uses the internal SetSelectionState method to maintain encapsulation.
        /// </summary>
        private void AddToSelection(FileTreeItem item)
        {
            if (item == null || _selectedPaths.Contains(item.Path)) return;
            
            item.SetSelectionState(true);
            _selectedItems.Add(item);
            _selectedPaths.Add(item.Path);
            _pathToItemMap[item.Path] = item;
            
            // Track for event
            _pendingAddedItems.Add(item);
        }
        
        /// <summary>
        /// Removes an item from the selection.
        /// This is the only method that should clear selection state.
        /// Uses the internal SetSelectionState method to maintain encapsulation.
        /// </summary>
        private void RemoveFromSelection(FileTreeItem item)
        {
            if (item == null || !_selectedPaths.Contains(item.Path)) return;
            
            item.SetSelectionState(false);
            _selectedItems.Remove(item);
            _selectedPaths.Remove(item.Path);
            _pathToItemMap.Remove(item.Path);
            
            // Track for event
            _pendingRemovedItems.Add(item);
        }
        
        /// <summary>
        /// Collects all descendants of an item
        /// </summary>
        private void CollectAllDescendants(FileTreeItem item, List<FileTreeItem> result)
        {
            if (item == null) return;
            
            foreach (var child in item.Children)
            {
                result.Add(child);
                if (child.IsDirectory)
                {
                    CollectAllDescendants(child, result);
                }
            }
        }
        
        /// <summary>
        /// Updates the select all checkbox state
        /// </summary>
        private void UpdateSelectAllState(IEnumerable<FileTreeItem> allItems)
        {
            if (allItems == null) return;
            
            var totalCount = GetFlattenedTree(allItems).Count;
            AreAllItemsSelected = totalCount > 0 && _selectedItems.Count == totalCount;
            OnPropertyChanged(nameof(AreAllItemsSelected));
        }
        
        /// <summary>
        /// Gets a flattened tree structure (with caching for performance)
        /// </summary>
        private List<FileTreeItem> GetFlattenedTree(IEnumerable<FileTreeItem> items)
        {
            if (!_flatTreeCacheValid)
            {
                _flatTreeCache.Clear();
                FlattenTreeRecursive(items, _flatTreeCache);
                _flatTreeCacheValid = true;
            }
            return _flatTreeCache;
        }
        
        /// <summary>
        /// Recursively flattens tree into list
        /// </summary>
        private void FlattenTreeRecursive(IEnumerable<FileTreeItem> items, List<FileTreeItem> result)
        {
            if (items == null) return;
            
            foreach (var item in items)
            {
                result.Add(item);
                
                if (item.IsExpanded && item.Children != null && item.Children.Count > 0)
                {
                    FlattenTreeRecursive(item.Children, result);
                }
            }
        }
        
        /// <summary>
        /// Invalidates the flat tree cache
        /// </summary>
        private void InvalidateFlatTreeCache()
        {
            _flatTreeCacheValid = false;
        }
        
        /// <summary>
        /// Schedules a debounced selection changed event
        /// </summary>
        private void ScheduleSelectionChangedEvent()
        {
            _pendingSelectionEvent = true;
            
            if (!_eventDebounceTimer.IsEnabled)
            {
                _eventDebounceTimer.Start();
            }
        }
        
        /// <summary>
        /// Handles the debounce timer tick
        /// </summary>
        private void OnDebounceTimerTick(object sender, EventArgs e)
        {
            _eventDebounceTimer.Stop();
            
            if (_pendingSelectionEvent && !_disposed)
            {
                _pendingSelectionEvent = false;
                FireSelectionChangedEvent();
            }
        }
        
        /// <summary>
        /// Fires the selection changed event with accumulated changes
        /// </summary>
        private void FireSelectionChangedEvent()
        {
            if (_disposed) return;
            
            // Track performance
            _eventCount++;
            var now = DateTime.Now;
            if ((now - _lastEventTime).TotalSeconds > 1)
            {
                if (_eventCount > 10)
                {
                    System.Diagnostics.Debug.WriteLine($"[PERF] SelectionService: {_eventCount} events in the last second");
                }
                _eventCount = 0;
                _lastEventTime = now;
            }
            
            // Create event args with accumulated changes
            var args = new FileTreeSelectionChangedEventArgs(
                _selectedItems.ToList(),
                _selectedPaths.ToList(),
                _pendingAddedItems.ToList(),
                _pendingRemovedItems.ToList()
            );
            
            // Clear pending lists
            _pendingAddedItems.Clear();
            _pendingRemovedItems.Clear();
            
            // Raise event
            SelectionChanged?.Invoke(this, args);
            
            // Notify property changes for data binding
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasMultipleSelection));
            OnPropertyChanged(nameof(SelectionCount));
            OnPropertyChanged(nameof(FirstSelectedItem));
            OnPropertyChanged(nameof(FirstSelectedPath));
        }
        
        /// <summary>
        /// Raises property changed event
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (!_disposed)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Stop and dispose timer
                    if (_eventDebounceTimer != null)
                    {
                        _eventDebounceTimer.Stop();
                        _eventDebounceTimer.Tick -= OnDebounceTimerTick;
                    }
                    
                    // Clear selection first
                    ClearSelection();
                    
                    // Clear all collections
                    _selectedItems?.Clear();
                    _selectedPaths?.Clear();
                    _pathToItemMap?.Clear();
                    _flatTreeCache?.Clear();
                    _pendingAddedItems?.Clear();
                    _pendingRemovedItems?.Clear();
                    
                    // Null out references
                    _lastSelectedItem = null;
                    _anchorItem = null;
                    _lastPattern = null;
                    
                    // Clear event handlers to prevent memory leaks
                    SelectionChanged = null;
                    PropertyChanged = null;
                }
                
                _disposed = true;
            }
        }
        
        ~SelectionService()
        {
            Dispose(false);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Event arguments for file tree selection changes - Enhanced version
    /// </summary>
    public class FileTreeSelectionChangedEventArgs : EventArgs
    {
        public IReadOnlyList<FileTreeItem> SelectedItems { get; }
        public IReadOnlyList<string> SelectedPaths { get; }
        public IReadOnlyList<FileTreeItem> AddedItems { get; }
        public IReadOnlyList<FileTreeItem> RemovedItems { get; }
        
        public FileTreeSelectionChangedEventArgs(
            IReadOnlyList<FileTreeItem> selectedItems, 
            IReadOnlyList<string> selectedPaths,
            IReadOnlyList<FileTreeItem> addedItems = null,
            IReadOnlyList<FileTreeItem> removedItems = null)
        {
            SelectedItems = selectedItems;
            SelectedPaths = selectedPaths;
            AddedItems = addedItems ?? new List<FileTreeItem>();
            RemovedItems = removedItems ?? new List<FileTreeItem>();
        }
    }
}