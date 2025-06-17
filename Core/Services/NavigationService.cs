using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.Services
{
    /// <summary>
    /// Service responsible for handling navigation history and operations in ExplorerPro.
    /// Extracted from MainWindow.xaml.cs to improve separation of concerns and testability.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Event fired when navigation state changes
        /// </summary>
        event EventHandler<NavigationChangedEventArgs> NavigationChanged;
        
        /// <summary>
        /// Navigates to the specified path and adds it to history
        /// </summary>
        void NavigateTo(string path);
        
        /// <summary>
        /// Navigates back in history
        /// </summary>
        bool GoBack();
        
        /// <summary>
        /// Navigates forward in history
        /// </summary>
        bool GoForward();
        
        /// <summary>
        /// Navigates up one directory level
        /// </summary>
        bool GoUp(string currentPath);
        
        /// <summary>
        /// Checks if back navigation is possible
        /// </summary>
        bool CanGoBack { get; }
        
        /// <summary>
        /// Checks if forward navigation is possible
        /// </summary>
        bool CanGoForward { get; }
        
        /// <summary>
        /// Gets the current path
        /// </summary>
        string CurrentPath { get; }
        
        /// <summary>
        /// Gets navigation history count
        /// </summary>
        int HistoryCount { get; }
        
        /// <summary>
        /// Gets estimated memory usage of navigation history
        /// </summary>
        long HistoryMemoryUsage { get; }
        
        /// <summary>
        /// Clears all navigation history
        /// </summary>
        void ClearHistory();
        
        /// <summary>
        /// Validates navigation history bounds
        /// </summary>
        bool ValidateHistoryBounds();
    }

    /// <summary>
    /// Event arguments for navigation changes
    /// </summary>
    public class NavigationChangedEventArgs : EventArgs
    {
        public string OldPath { get; set; }
        public string NewPath { get; set; }
        public NavigationType Type { get; set; }
    }

    /// <summary>
    /// Types of navigation operations
    /// </summary>
    public enum NavigationType
    {
        Forward,
        Back,
        Up,
        Direct
    }

    /// <summary>
    /// Implementation of INavigationService
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly ILogger<NavigationService> _logger;
        private readonly LinkedList<NavigationEntry> _navigationHistory = new LinkedList<NavigationEntry>();
        private LinkedListNode<NavigationEntry>? _currentHistoryNode;
        private readonly object _historyLock = new object();
        
        // Configuration constants
        private const int MaxHistorySize = 1000;
        private const int HistoryTrimSize = 100;

        public NavigationService(ILogger<NavigationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Event fired when navigation state changes
        /// </summary>
        public event EventHandler<NavigationChangedEventArgs> NavigationChanged;

        /// <summary>
        /// Checks if back navigation is possible
        /// </summary>
        public bool CanGoBack
        {
            get
            {
                lock (_historyLock)
                {
                    return _currentHistoryNode?.Previous != null;
                }
            }
        }

        /// <summary>
        /// Checks if forward navigation is possible
        /// </summary>
        public bool CanGoForward
        {
            get
            {
                lock (_historyLock)
                {
                    return _currentHistoryNode?.Next != null;
                }
            }
        }

        /// <summary>
        /// Gets the current path
        /// </summary>
        public string CurrentPath
        {
            get
            {
                lock (_historyLock)
                {
                    return _currentHistoryNode?.Value?.Path ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// Gets navigation history count
        /// </summary>
        public int HistoryCount
        {
            get
            {
                lock (_historyLock)
                {
                    return _navigationHistory.Count;
                }
            }
        }

        /// <summary>
        /// Gets estimated memory usage of navigation history
        /// </summary>
        public long HistoryMemoryUsage
        {
            get
            {
                lock (_historyLock)
                {
                    return _navigationHistory.Sum(e => e.MemorySize);
                }
            }
        }

        /// <summary>
        /// Navigates to the specified path and adds it to history
        /// </summary>
        public void NavigateTo(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var oldPath = CurrentPath;
            
            lock (_historyLock)
            {
                try
                {
                    // Remove any forward history when navigating to a new path
                    if (_currentHistoryNode?.Next != null)
                    {
                        var node = _currentHistoryNode.Next;
                        while (node != null)
                        {
                            var next = node.Next;
                            _navigationHistory.Remove(node);
                            node = next;
                        }
                    }

                    // Add new entry to history
                    var entry = new NavigationEntry(path);
                    _currentHistoryNode = _navigationHistory.AddLast(entry);

                    // Trim history if it gets too large
                    if (_navigationHistory.Count > MaxHistorySize)
                    {
                        _logger?.LogDebug($"Navigation history exceeded {MaxHistorySize} entries, trimming to {MaxHistorySize - HistoryTrimSize}");
                        
                        for (int i = 0; i < HistoryTrimSize && _navigationHistory.Count > 0; i++)
                        {
                            _navigationHistory.RemoveFirst();
                        }
                        
                        // Update current node reference
                        _currentHistoryNode = _navigationHistory.Last;
                    }

                    // Log memory usage periodically
                    if (_navigationHistory.Count % 100 == 0)
                    {
                        var totalMemory = _navigationHistory.Sum(e => e.MemorySize);
                        _logger?.LogDebug($"Navigation history: {_navigationHistory.Count} entries, ~{totalMemory / 1024}KB");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error adding path to navigation history: {path}");
                }
            }

            // Fire navigation changed event
            NavigationChanged?.Invoke(this, new NavigationChangedEventArgs
            {
                OldPath = oldPath,
                NewPath = path,
                Type = NavigationType.Direct
            });
        }

        /// <summary>
        /// Navigates back in history
        /// </summary>
        public bool GoBack()
        {
            lock (_historyLock)
            {
                if (_currentHistoryNode?.Previous != null)
                {
                    var oldPath = _currentHistoryNode.Value.Path;
                    _currentHistoryNode = _currentHistoryNode.Previous;
                    var newPath = _currentHistoryNode.Value.Path;

                    _logger?.LogDebug($"Navigation: Back from {oldPath} to {newPath}");

                    // Fire navigation changed event
                    NavigationChanged?.Invoke(this, new NavigationChangedEventArgs
                    {
                        OldPath = oldPath,
                        NewPath = newPath,
                        Type = NavigationType.Back
                    });

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Navigates forward in history
        /// </summary>
        public bool GoForward()
        {
            lock (_historyLock)
            {
                if (_currentHistoryNode?.Next != null)
                {
                    var oldPath = _currentHistoryNode.Value.Path;
                    _currentHistoryNode = _currentHistoryNode.Next;
                    var newPath = _currentHistoryNode.Value.Path;

                    _logger?.LogDebug($"Navigation: Forward from {oldPath} to {newPath}");

                    // Fire navigation changed event
                    NavigationChanged?.Invoke(this, new NavigationChangedEventArgs
                    {
                        OldPath = oldPath,
                        NewPath = newPath,
                        Type = NavigationType.Forward
                    });

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Navigates up one directory level
        /// </summary>
        public bool GoUp(string currentPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentPath))
                    return false;

                var parentPath = Path.GetDirectoryName(currentPath);
                if (!string.IsNullOrWhiteSpace(parentPath) && Directory.Exists(parentPath))
                {
                    NavigateTo(parentPath);

                    // Update the event type to Up
                    var args = new NavigationChangedEventArgs
                    {
                        OldPath = currentPath,
                        NewPath = parentPath,
                        Type = NavigationType.Up
                    };

                    NavigationChanged?.Invoke(this, args);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error navigating up from path: {currentPath}");
            }

            return false;
        }

        /// <summary>
        /// Clears all navigation history
        /// </summary>
        public void ClearHistory()
        {
            lock (_historyLock)
            {
                _navigationHistory.Clear();
                _currentHistoryNode = null;
                _logger?.LogDebug("Navigation history cleared");
            }
        }

        /// <summary>
        /// Validates navigation history bounds
        /// </summary>
        public bool ValidateHistoryBounds()
        {
            lock (_historyLock)
            {
                var count = _navigationHistory.Count;
                var memoryUsage = _navigationHistory.Sum(e => e.MemorySize);
                
                bool isValid = count <= MaxHistorySize * 2 && memoryUsage <= 50 * 1024 * 1024; // 50MB limit
                
                if (!isValid)
                {
                    _logger?.LogWarning($"Navigation history bounds exceeded: {count} entries, {memoryUsage / 1024 / 1024}MB");
                }
                
                return isValid;
            }
        }

        #region Private Classes

        /// <summary>
        /// Represents a navigation history entry
        /// </summary>
        private class NavigationEntry
        {
            public string Path { get; set; }
            public DateTime Timestamp { get; set; }
            public long MemorySize { get; set; } // Approximate memory usage

            public NavigationEntry(string path)
            {
                Path = path ?? throw new ArgumentNullException(nameof(path));
                Timestamp = DateTime.Now;
                MemorySize = EstimateMemorySize(path);
            }

            private static long EstimateMemorySize(string path)
            {
                // Rough estimate: string length * 2 (Unicode) + object overhead
                return (path?.Length ?? 0) * 2 + 64;
            }
        }

        #endregion
    }
} 