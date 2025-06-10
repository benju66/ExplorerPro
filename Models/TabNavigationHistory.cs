using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Manages navigation history for individual tabs with memory bounds and persistence.
    /// Implements INotifyPropertyChanged for UI binding support.
    /// </summary>
    public class TabNavigationHistory : INotifyPropertyChanged
    {
        private readonly LinkedList<NavigationHistoryItem> _history = new();
        private LinkedListNode<NavigationHistoryItem> _current;
        private readonly int _maxItems;
        private readonly long _maxMemorySize;
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        public TabNavigationHistory(int maxItems = 50, long maxMemoryBytes = 10_485_760) // 10MB default
        {
            _maxItems = maxItems;
            _maxMemorySize = maxMemoryBytes;
        }
        
        /// <summary>
        /// Indicates if backward navigation is possible
        /// </summary>
        public bool CanGoBack => _current?.Previous != null;
        
        /// <summary>
        /// Indicates if forward navigation is possible
        /// </summary>
        public bool CanGoForward => _current?.Next != null;
        
        /// <summary>
        /// Gets the current navigation item
        /// </summary>
        public NavigationHistoryItem CurrentItem => _current?.Value;
        
        /// <summary>
        /// Gets the total number of history entries
        /// </summary>
        public int Count => _history.Count;
        
        /// <summary>
        /// Adds a new navigation entry, removing any forward history
        /// </summary>
        public void AddEntry(string path, string title = null)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            // Don't add duplicate consecutive entries
            if (_current?.Value?.Path?.Equals(path, StringComparison.OrdinalIgnoreCase) == true)
                return;
            
            // Remove forward history when adding new entry
            while (_current?.Next != null)
            {
                _history.Remove(_current.Next);
            }
            
            // Add new entry
            var item = new NavigationHistoryItem(path, title);
            _current = _history.AddLast(item);
            
            // Enforce memory and count limits
            EnforceLimits();
            
            // Notify UI of changes
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(CurrentItem));
        }
        
        /// <summary>
        /// Navigates backward in history
        /// </summary>
        public NavigationHistoryItem GoBack()
        {
            if (_current?.Previous != null)
            {
                _current = _current.Previous;
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
                OnPropertyChanged(nameof(CurrentItem));
                return _current.Value;
            }
            return null;
        }
        
        /// <summary>
        /// Navigates forward in history
        /// </summary>
        public NavigationHistoryItem GoForward()
        {
            if (_current?.Next != null)
            {
                _current = _current.Next;
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
                OnPropertyChanged(nameof(CurrentItem));
                return _current.Value;
            }
            return null;
        }
        
        /// <summary>
        /// Gets all history items for display
        /// </summary>
        public IEnumerable<NavigationHistoryItem> GetAllItems()
        {
            return _history.ToList();
        }
        
        /// <summary>
        /// Clears all navigation history
        /// </summary>
        public void Clear()
        {
            _history.Clear();
            _current = null;
            
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(CurrentItem));
        }
        
        /// <summary>
        /// Serializes history for persistence (saves last 10 entries)
        /// </summary>
        public List<NavigationHistoryItem> Serialize()
        {
            return _history.Take(10).ToList();
        }
        
        /// <summary>
        /// Restores history from serialized data
        /// </summary>
        public void Restore(List<NavigationHistoryItem> items)
        {
            if (items == null || !items.Any())
                return;
                
            _history.Clear();
            
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.Path))
                {
                    _history.AddLast(item);
                }
            }
            
            _current = _history.Last;
            
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(CurrentItem));
        }
        
        /// <summary>
        /// Gets total memory usage of all history items
        /// </summary>
        public long GetTotalMemorySize()
        {
            return _history.Sum(item => item.MemorySize);
        }
        
        /// <summary>
        /// Enforces memory and count limits by removing oldest entries
        /// </summary>
        private void EnforceLimits()
        {
            // Remove oldest entries if over limits
            while ((_history.Count > _maxItems || GetTotalMemorySize() > _maxMemorySize) 
                   && _history.Count > 1) // Always keep at least one item
            {
                if (_history.First != null && _history.First != _current)
                {
                    _history.RemoveFirst();
                }
                else
                {
                    break; // Current is the first item, can't remove it
                }
            }
        }
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 