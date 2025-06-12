using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExplorerPro.Core.Collections;
using ExplorerPro.Core;

namespace ExplorerPro.UI.PaneManagement
{
    /// <summary>
    /// Manages navigation history for panes in the PaneManager.
    /// Phase 5: Resource Bounds - Uses bounded collections to limit memory growth
    /// </summary>
    public class PaneHistoryManager : IDisposable
    {
        #region Constants

        private const int DefaultMaxHistorySize = 50;
        private const int DefaultMaxForwardSize = 30;

        #endregion

        #region Fields

        // Dictionary mapping tab index to its history
        private Dictionary<int, TabHistory> _tabHistories = new Dictionary<int, TabHistory>();
        private readonly int _maxHistorySize;
        private readonly int _maxForwardSize;
        private readonly object _lock = new object();
        private bool _disposed;

        #endregion

        #region Constructor and Disposal

        public PaneHistoryManager(int maxHistorySize = DefaultMaxHistorySize, int maxForwardSize = DefaultMaxForwardSize)
        {
            _maxHistorySize = maxHistorySize;
            _maxForwardSize = maxForwardSize;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                lock (_lock)
                {
                    foreach (var history in _tabHistories.Values)
                    {
                        history.Dispose();
                    }
                    _tabHistories.Clear();
                }
            }

            _disposed = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize history for a new tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <param name="initialPath">The initial path for the tab</param>
        public void InitTabHistory(int tabIndex, string initialPath)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PaneHistoryManager));

            lock (_lock)
            {
                if (!_tabHistories.ContainsKey(tabIndex))
                {
                    _tabHistories[tabIndex] = new TabHistory(_maxHistorySize, _maxForwardSize);
                }
                
                _tabHistories[tabIndex].AddPath(initialPath);
            }
        }

        /// <summary>
        /// Push a new path onto the history stack for a tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <param name="path">The path to add</param>
        public void PushPath(int tabIndex, string path)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PaneHistoryManager));

            lock (_lock)
            {
                if (_tabHistories.TryGetValue(tabIndex, out TabHistory history))
                {
                    history.AddPath(path);
                }
                else
                {
                    // If history doesn't exist for this tab, create it
                    InitTabHistory(tabIndex, path);
                }
            }
        }

        /// <summary>
        /// Push a new path with state information onto the history stack for a tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <param name="path">The path to add</param>
        /// <param name="scrollPosition">Scroll position</param>
        /// <param name="selectedItems">Selected items</param>
        public void PushPathWithState(int tabIndex, string path, double scrollPosition, List<string> selectedItems)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PaneHistoryManager));

            lock (_lock)
            {
                if (_tabHistories.TryGetValue(tabIndex, out TabHistory history))
                {
                    history.AddPathWithState(path, scrollPosition, selectedItems);
                }
                else
                {
                    // If history doesn't exist for this tab, create it
                    InitTabHistory(tabIndex, path);
                    if (_tabHistories.TryGetValue(tabIndex, out TabHistory newHistory))
                    {
                        newHistory.UpdateState(scrollPosition, selectedItems);
                    }
                }
            }
        }

        /// <summary>
        /// Get the current history entry with state
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <returns>History entry with state information</returns>
        public HistoryEntry? GetCurrentEntry(int tabIndex)
        {
            if (_disposed) return null;

            lock (_lock)
            {
                if (_tabHistories.TryGetValue(tabIndex, out TabHistory history))
                {
                    return history.GetCurrentEntry();
                }
                return null;
            }
        }

        /// <summary>
        /// Navigate back in the history for a tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <returns>The previous path, or null if at the beginning of history</returns>
        public string GoBack(int tabIndex)
        {
            if (_disposed) return null;

            lock (_lock)
            {
                if (_tabHistories.TryGetValue(tabIndex, out TabHistory history))
                {
                    return history.GoBack();
                }
                
                return null;
            }
        }

        /// <summary>
        /// Navigate forward in the history for a tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <returns>The next path, or null if at the end of history</returns>
        public string GoForward(int tabIndex)
        {
            if (_disposed) return null;

            lock (_lock)
            {
                if (_tabHistories.TryGetValue(tabIndex, out TabHistory history))
                {
                    return history.GoForward();
                }
                
                return null;
            }
        }

        /// <summary>
        /// Navigate up a directory level for a tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <returns>The parent directory path, or null if at the root</returns>
        public string GoUp(int tabIndex)
        {
            if (_disposed) return null;

            lock (_lock)
            {
                if (!_tabHistories.TryGetValue(tabIndex, out TabHistory history))
                {
                    return null;
                }
                
                string currentPath = history.GetCurrentPath();
                if (string.IsNullOrEmpty(currentPath))
                {
                    return null;
                }
                
                // Get parent directory
                DirectoryInfo parent = Directory.GetParent(currentPath);
                if (parent != null)
                {
                    string parentPath = parent.FullName;
                    history.AddPath(parentPath);
                    return parentPath;
                }
                
                return null;
            }
        }

        /// <summary>
        /// Remove history for a tab that was closed
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        public void RemoveTabHistory(int tabIndex)
        {
            if (_disposed) return;

            lock (_lock)
            {
                if (_tabHistories.ContainsKey(tabIndex))
                {
                    _tabHistories[tabIndex].Dispose();
                    _tabHistories.Remove(tabIndex);
                }
                
                // Update indices for tabs after the removed one
                Dictionary<int, TabHistory> newHistories = new Dictionary<int, TabHistory>();
                foreach (var kvp in _tabHistories)
                {
                    int index = kvp.Key;
                    if (index > tabIndex)
                    {
                        // Shift indices down
                        newHistories[index - 1] = kvp.Value;
                    }
                    else
                    {
                        // Keep as-is
                        newHistories[index] = kvp.Value;
                    }
                }
                
                _tabHistories = newHistories;
            }
        }

        /// <summary>
        /// Move tab history from source index to target index
        /// </summary>
        /// <param name="sourceIndex">Source tab index</param>
        /// <param name="targetIndex">Target tab index</param>
        public void MoveTabHistory(int sourceIndex, int targetIndex)
        {
            if (_disposed) return;

            lock (_lock)
            {
                if (!_tabHistories.ContainsKey(sourceIndex))
                {
                    return;
                }
                
                TabHistory history = _tabHistories[sourceIndex];
                
                // Create a new dictionary for the updated indices
                Dictionary<int, TabHistory> newHistories = new Dictionary<int, TabHistory>();
                
                // Handle the case when moving a tab forward (right)
                if (sourceIndex < targetIndex)
                {
                    // Copy entries before source
                    for (int i = 0; i < sourceIndex; i++)
                    {
                        if (_tabHistories.ContainsKey(i))
                        {
                            newHistories[i] = _tabHistories[i];
                        }
                    }
                    
                    // Shift entries between source and target down by 1
                    for (int i = sourceIndex + 1; i <= targetIndex; i++)
                    {
                        if (_tabHistories.ContainsKey(i))
                        {
                            newHistories[i - 1] = _tabHistories[i];
                        }
                    }
                    
                    // Place source at target
                    newHistories[targetIndex] = history;
                    
                    // Copy entries after target
                    for (int i = targetIndex + 1; i < _tabHistories.Keys.Max() + 1; i++)
                    {
                        if (_tabHistories.ContainsKey(i))
                        {
                            newHistories[i] = _tabHistories[i];
                        }
                    }
                }
                // Handle the case when moving a tab backward (left)
                else if (sourceIndex > targetIndex)
                {
                    // Copy entries before target
                    for (int i = 0; i < targetIndex; i++)
                    {
                        if (_tabHistories.ContainsKey(i))
                        {
                            newHistories[i] = _tabHistories[i];
                        }
                    }
                    
                    // Place source at target
                    newHistories[targetIndex] = history;
                    
                    // Shift entries between target and source up by 1
                    for (int i = targetIndex; i < sourceIndex; i++)
                    {
                        if (_tabHistories.ContainsKey(i))
                        {
                            newHistories[i + 1] = _tabHistories[i];
                        }
                    }
                    
                    // Copy entries after source
                    for (int i = sourceIndex + 1; i < _tabHistories.Keys.Max() + 1; i++)
                    {
                        if (_tabHistories.ContainsKey(i))
                        {
                            newHistories[i] = _tabHistories[i];
                        }
                    }
                }
                
                _tabHistories = newHistories;
            }
        }

        /// <summary>
        /// Get history statistics for monitoring purposes
        /// </summary>
        /// <returns>History statistics</returns>
        public HistoryStatistics GetStatistics()
        {
            if (_disposed) return new HistoryStatistics();

            lock (_lock)
            {
                var stats = new HistoryStatistics
                {
                    TotalTabs = _tabHistories.Count,
                    MaxHistorySize = _maxHistorySize,
                    MaxForwardSize = _maxForwardSize
                };

                foreach (var history in _tabHistories.Values)
                {
                    var tabStats = history.GetStatistics();
                    stats.TotalHistoryEntries += tabStats.HistoryCount;
                    stats.TotalForwardEntries += tabStats.ForwardCount;
                }

                return stats;
            }
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Represents a history entry with state information
        /// </summary>
        public class HistoryEntry : IDisposable
        {
            public string Path { get; set; } = string.Empty;
            public double ScrollPosition { get; set; }
            public List<string> SelectedItems { get; set; } = new List<string>();
            public DateTime Timestamp { get; set; } = DateTime.Now;
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                
                SelectedItems?.Clear();
                _disposed = true;
            }
        }

        public class HistoryStatistics
        {
            public int TotalTabs { get; set; }
            public int TotalHistoryEntries { get; set; }
            public int TotalForwardEntries { get; set; }
            public int MaxHistorySize { get; set; }
            public int MaxForwardSize { get; set; }
        }

        #endregion

        #region TabHistory Class

        /// <summary>
        /// Inner class that manages bounded history for a single tab
        /// Phase 5: Uses BoundedCollection to limit memory usage
        /// </summary>
        private class TabHistory : IDisposable
        {
            private BoundedCollection<HistoryEntry> _history;
            private BoundedCollection<HistoryEntry> _forwardHistory;
            private HistoryEntry _currentEntry;
            private readonly object _historyLock = new object();
            private bool _disposed;

            public TabHistory(int maxHistorySize, int maxForwardSize)
            {
                _history = new BoundedCollection<HistoryEntry>(maxHistorySize);
                _forwardHistory = new BoundedCollection<HistoryEntry>(maxForwardSize);
            }

            /// <summary>
            /// Add a new path to the history
            /// </summary>
            /// <param name="path">Path to add</param>
            public void AddPath(string path)
            {
                AddPathWithState(path, 0, new List<string>());
            }

            /// <summary>
            /// Add a new path with state information to the history
            /// </summary>
            /// <param name="path">Path to add</param>
            /// <param name="scrollPosition">Scroll position</param>
            /// <param name="selectedItems">Selected items</param>
            public void AddPathWithState(string path, double scrollPosition, List<string> selectedItems)
            {
                if (_disposed) return;

                lock (_historyLock)
                {
                    // Clear forward history on new navigation
                    _forwardHistory.Clear();
                    
                    // Don't add duplicate consecutive entries
                    if (_currentEntry != null && _currentEntry.Path == path)
                    {
                        // Update the state of the existing entry
                        _currentEntry.ScrollPosition = scrollPosition;
                        _currentEntry.SelectedItems = new List<string>(selectedItems);
                        _currentEntry.Timestamp = DateTime.Now;
                        return;
                    }
                    
                    // Move current entry to history if it exists
                    if (_currentEntry != null)
                    {
                        _history.Add(_currentEntry);
                    }
                    
                    // Create new current entry
                    _currentEntry = new HistoryEntry
                    {
                        Path = path,
                        ScrollPosition = scrollPosition,
                        SelectedItems = new List<string>(selectedItems),
                        Timestamp = DateTime.Now
                    };
                }
            }

            /// <summary>
            /// Update state of current entry
            /// </summary>
            /// <param name="scrollPosition">Scroll position</param>
            /// <param name="selectedItems">Selected items</param>
            public void UpdateState(double scrollPosition, List<string> selectedItems)
            {
                if (_disposed) return;

                lock (_historyLock)
                {
                    if (_currentEntry != null)
                    {
                        _currentEntry.ScrollPosition = scrollPosition;
                        _currentEntry.SelectedItems = new List<string>(selectedItems);
                        _currentEntry.Timestamp = DateTime.Now;
                    }
                }
            }

            /// <summary>
            /// Get current history entry
            /// </summary>
            /// <returns>Current entry or null</returns>
            public HistoryEntry? GetCurrentEntry()
            {
                if (_disposed) return null;

                lock (_historyLock)
                {
                    return _currentEntry;
                }
            }

            /// <summary>
            /// Go back in history
            /// </summary>
            /// <returns>Previous path or null</returns>
            public string GoBack()
            {
                if (_disposed) return null;

                lock (_historyLock)
                {
                    if (_history.IsEmpty) return null;
                    
                    // Move current to forward if it exists
                    if (_currentEntry != null)
                    {
                        _forwardHistory.AddFirst(_currentEntry);
                    }
                    
                    // Get last history entry
                    _currentEntry = _history.RemoveLast();
                    return _currentEntry?.Path;
                }
            }

            /// <summary>
            /// Go forward in history
            /// </summary>
            /// <returns>Next path or null</returns>
            public string GoForward()
            {
                if (_disposed) return null;

                lock (_historyLock)
                {
                    if (_forwardHistory.IsEmpty) return null;
                    
                    // Move current to history if it exists
                    if (_currentEntry != null)
                    {
                        _history.Add(_currentEntry);
                    }
                    
                    // Get first forward entry
                    _currentEntry = _forwardHistory.RemoveFirst();
                    return _currentEntry?.Path;
                }
            }

            /// <summary>
            /// Get current path in history
            /// </summary>
            /// <returns>Current path or null</returns>
            public string GetCurrentPath()
            {
                if (_disposed) return null;

                lock (_historyLock)
                {
                    return _currentEntry?.Path;
                }
            }

            /// <summary>
            /// Get statistics for this tab history
            /// </summary>
            /// <returns>Tab history statistics</returns>
            public (int HistoryCount, int ForwardCount) GetStatistics()
            {
                if (_disposed) return (0, 0);

                lock (_historyLock)
                {
                    return (_history.Count, _forwardHistory.Count);
                }
            }

            public void Dispose()
            {
                if (_disposed) return;

                lock (_historyLock)
                {
                    _currentEntry?.Dispose();
                    _history?.Dispose();
                    _forwardHistory?.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion
    }
}