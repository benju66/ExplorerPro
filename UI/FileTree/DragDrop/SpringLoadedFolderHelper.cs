// UI/FileTree/DragDrop/SpringLoadedFolderHelper.cs - Fixed version with proper cleanup
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace ExplorerPro.UI.FileTree.DragDrop
{
    /// <summary>
    /// Manages spring-loaded folder expansion during drag operations
    /// Fixed version with proper timer management and disposal
    /// </summary>
    public class SpringLoadedFolderHelper : IDisposable
    {
        #region Constants
        
        private const int HOVER_DELAY_MS = 700; // Time to hover before expanding
        private const int COLLAPSE_DELAY_MS = 500; // Time before collapsing after leave
        
        #endregion
        
        #region Fields
        
        private DispatcherTimer _expandTimer;
        private DispatcherTimer _collapseTimer;
        private readonly HashSet<FileTreeItem> _autoExpandedItems;
        private FileTreeItem _pendingExpandItem;
        private FileTreeItem _pendingCollapseItem;
        
        // Store event handlers for cleanup
        private EventHandler _expandTimerTickHandler;
        private EventHandler _collapseTimerTickHandler;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Raised when a folder should be expanded
        /// </summary>
        public event EventHandler<FileTreeItem> FolderExpanding;
        
        /// <summary>
        /// Raised when a folder should be collapsed
        /// </summary>
        public event EventHandler<FileTreeItem> FolderCollapsing;
        
        #endregion
        
        #region Constructor
        
        public SpringLoadedFolderHelper()
        {
            _autoExpandedItems = new HashSet<FileTreeItem>();
            
            // Create event handlers
            _expandTimerTickHandler = OnExpandTimerTick;
            _collapseTimerTickHandler = OnCollapseTimerTick;
            
            // Initialize timers
            _expandTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(HOVER_DELAY_MS)
            };
            _expandTimer.Tick += _expandTimerTickHandler;
            
            _collapseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(COLLAPSE_DELAY_MS)
            };
            _collapseTimer.Tick += _collapseTimerTickHandler;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Starts hover tracking for a folder item
        /// </summary>
        public void StartHover(FileTreeItem item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SpringLoadedFolderHelper));
                
            if (item == null || !item.IsDirectory || item.IsExpanded) 
                return;
            
            // Cancel any pending collapse for this item
            if (_pendingCollapseItem == item)
            {
                _collapseTimer?.Stop();
                _pendingCollapseItem = null;
            }
            
            // Start new expand timer
            _pendingExpandItem = item;
            _expandTimer?.Stop();
            _expandTimer?.Start();
        }
        
        /// <summary>
        /// Stops hover tracking
        /// </summary>
        public void StopHover(FileTreeItem item)
        {
            if (_disposed) return;
            
            if (item == null) return;
            
            // Cancel pending expand
            if (_pendingExpandItem == item)
            {
                _expandTimer?.Stop();
                _pendingExpandItem = null;
            }
            
            // If this item was auto-expanded, start collapse timer
            if (_autoExpandedItems.Contains(item))
            {
                _pendingCollapseItem = item;
                _collapseTimer?.Stop();
                _collapseTimer?.Start();
            }
        }
        
        /// <summary>
        /// Cancels all pending operations
        /// </summary>
        public void CancelAll()
        {
            if (_disposed) return;
            
            _expandTimer?.Stop();
            _collapseTimer?.Stop();
            _pendingExpandItem = null;
            _pendingCollapseItem = null;
        }
        
        /// <summary>
        /// Collapses all auto-expanded folders
        /// </summary>
        public void CollapseAll()
        {
            if (_disposed) return;
            
            // Create a copy to avoid modification during iteration
            var itemsToCollapse = new List<FileTreeItem>(_autoExpandedItems);
            
            foreach (var item in itemsToCollapse)
            {
                FolderCollapsing?.Invoke(this, item);
            }
            
            _autoExpandedItems.Clear();
            CancelAll();
        }
        
        /// <summary>
        /// Checks if an item was auto-expanded
        /// </summary>
        public bool WasAutoExpanded(FileTreeItem item)
        {
            if (_disposed) return false;
            return item != null && _autoExpandedItems.Contains(item);
        }
        
        #endregion
        
        #region Private Methods
        
        private void OnExpandTimerTick(object sender, EventArgs e)
        {
            if (_disposed) return;
            
            _expandTimer?.Stop();
            
            if (_pendingExpandItem != null && !_pendingExpandItem.IsExpanded)
            {
                _autoExpandedItems.Add(_pendingExpandItem);
                FolderExpanding?.Invoke(this, _pendingExpandItem);
            }
            
            _pendingExpandItem = null;
        }
        
        private void OnCollapseTimerTick(object sender, EventArgs e)
        {
            if (_disposed) return;
            
            _collapseTimer?.Stop();
            
            if (_pendingCollapseItem != null && _autoExpandedItems.Contains(_pendingCollapseItem))
            {
                _autoExpandedItems.Remove(_pendingCollapseItem);
                FolderCollapsing?.Invoke(this, _pendingCollapseItem);
            }
            
            _pendingCollapseItem = null;
        }
        
        #endregion
        
        #region IDisposable
        
        private bool _disposed;
        
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
                    // Stop timers first
                    if (_expandTimer != null)
                    {
                        _expandTimer.Stop();
                        
                        // Unsubscribe event handler
                        if (_expandTimerTickHandler != null)
                        {
                            _expandTimer.Tick -= _expandTimerTickHandler;
                        }
                        
                        // Dispose timer
                        _expandTimer = null;
                    }
                    
                    if (_collapseTimer != null)
                    {
                        _collapseTimer.Stop();
                        
                        // Unsubscribe event handler
                        if (_collapseTimerTickHandler != null)
                        {
                            _collapseTimer.Tick -= _collapseTimerTickHandler;
                        }
                        
                        // Dispose timer
                        _collapseTimer = null;
                    }
                    
                    // Clear collections
                    _autoExpandedItems?.Clear();
                    
                    // Clear references
                    _pendingExpandItem = null;
                    _pendingCollapseItem = null;
                    
                    // Clear event handlers
                    FolderExpanding = null;
                    FolderCollapsing = null;
                    _expandTimerTickHandler = null;
                    _collapseTimerTickHandler = null;
                }
                
                _disposed = true;
            }
        }
        
        ~SpringLoadedFolderHelper()
        {
            Dispose(false);
        }
        
        #endregion
    }
}