using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExplorerPro.UI.TabManagement
{
    /// <summary>
    /// Manages navigation history for tabs in the TabManager.
    /// Tracks back/forward history for each tab and provides navigation methods.
    /// </summary>
    public class TabHistoryManager
    {
        #region Fields

        // Dictionary mapping tab index to its history
        private Dictionary<int, TabHistory> _tabHistories = new Dictionary<int, TabHistory>();

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize history for a new tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <param name="initialPath">The initial path for the tab</param>
        public void InitTabHistory(int tabIndex, string initialPath)
        {
            if (!_tabHistories.ContainsKey(tabIndex))
            {
                _tabHistories[tabIndex] = new TabHistory();
            }
            
            _tabHistories[tabIndex].AddPath(initialPath);
        }

        /// <summary>
        /// Push a new path onto the history stack for a tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <param name="path">The path to add</param>
        public void PushPath(int tabIndex, string path)
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

        /// <summary>
        /// Navigate back in the history for a tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <returns>The previous path, or null if at the beginning of history</returns>
        public string GoBack(int tabIndex)
        {
            if (_tabHistories.TryGetValue(tabIndex, out TabHistory history))
            {
                return history.GoBack();
            }
            
            return null;
        }

        /// <summary>
        /// Navigate forward in the history for a tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <returns>The next path, or null if at the end of history</returns>
        public string GoForward(int tabIndex)
        {
            if (_tabHistories.TryGetValue(tabIndex, out TabHistory history))
            {
                return history.GoForward();
            }
            
            return null;
        }

        /// <summary>
        /// Navigate up a directory level for a tab
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        /// <returns>The parent directory path, or null if at the root</returns>
        public string GoUp(int tabIndex)
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

        /// <summary>
        /// Remove history for a tab that was closed
        /// </summary>
        /// <param name="tabIndex">The tab index</param>
        public void RemoveTabHistory(int tabIndex)
        {
            if (_tabHistories.ContainsKey(tabIndex))
            {
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

        /// <summary>
        /// Move tab history from source index to target index
        /// </summary>
        /// <param name="sourceIndex">Source tab index</param>
        /// <param name="targetIndex">Target tab index</param>
        public void MoveTabHistory(int sourceIndex, int targetIndex)
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

        #endregion

        #region TabHistory Class

        /// <summary>
        /// Inner class that manages history for a single tab
        /// </summary>
        private class TabHistory
        {
            private List<string> _history = new List<string>();
            private int _currentIndex = -1;

            /// <summary>
            /// Add a new path to the history
            /// </summary>
            /// <param name="path">Path to add</param>
            public void AddPath(string path)
            {
                // If we're in the middle of the history and navigating elsewhere,
                // truncate the forward history
                if (_currentIndex >= 0 && _currentIndex < _history.Count - 1)
                {
                    _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
                }
                
                // Don't add duplicate consecutive entries
                if (_history.Count > 0 && _history[_history.Count - 1] == path)
                {
                    return;
                }
                
                _history.Add(path);
                _currentIndex = _history.Count - 1;
            }

            /// <summary>
            /// Go back in history
            /// </summary>
            /// <returns>Previous path or null</returns>
            public string GoBack()
            {
                if (_currentIndex > 0)
                {
                    _currentIndex--;
                    return _history[_currentIndex];
                }
                
                return null;
            }

            /// <summary>
            /// Go forward in history
            /// </summary>
            /// <returns>Next path or null</returns>
            public string GoForward()
            {
                if (_currentIndex < _history.Count - 1)
                {
                    _currentIndex++;
                    return _history[_currentIndex];
                }
                
                return null;
            }

            /// <summary>
            /// Get current path in history
            /// </summary>
            /// <returns>Current path or null</returns>
            public string GetCurrentPath()
            {
                if (_currentIndex >= 0 && _currentIndex < _history.Count)
                {
                    return _history[_currentIndex];
                }
                
                return null;
            }
        }

        #endregion
    }
}