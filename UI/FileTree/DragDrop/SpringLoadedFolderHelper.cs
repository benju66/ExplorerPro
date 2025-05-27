// UI/FileTree/DragDrop/SpringLoadedFolderHelper.cs
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace ExplorerPro.UI.FileTree.DragDrop
{
    /// <summary>
    /// Manages spring-loaded folder expansion during drag operations
    /// </summary>
    public class SpringLoadedFolderHelper : IDisposable
    {
        #region Constants
        
        private const int HOVER_DELAY_MS = 700; // Time to hover before expanding
        private const int COLLAPSE_DELAY_MS = 500; // Time before collapsing after leave
        
        #endregion
        
        #region Fields
        
        private readonly DispatcherTimer _expandTimer;
        private readonly DispatcherTimer _collapseTimer;
        private readonly HashSet<FileTreeItem> _autoExpandedItems;
        private FileTreeItem _pendingExpandItem;
        private FileTreeItem _pendingCollapseItem;
        
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
            
            _expandTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(HOVER_DELAY_MS)
            };
            _expandTimer.Tick += OnExpandTimerTick;
            
            _collapseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(COLLAPSE_DELAY_MS)
            };
            _collapseTimer.Tick += OnCollapseTimerTick;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Starts hover tracking for a folder item
        /// </summary>
        public void StartHover(FileTreeItem item)
        {
            if (item == null || !item.IsDirectory || item.IsExpanded) 
                return;
            
            // Cancel any pending collapse for this item
            if (_pendingCollapseItem == item)
            {
                _collapseTimer.Stop();
                _pendingCollapseItem = null;
            }
            
            // Start new expand timer
            _pendingExpandItem = item;
            _expandTimer.Stop();
            _expandTimer.Start();
        }
        
        /// <summary>
        /// Stops hover tracking
        /// </summary>
        public void StopHover(FileTreeItem item)
        {
            if (item == null) return;
            
            // Cancel pending expand
            if (_pendingExpandItem == item)
            {
                _expandTimer.Stop();
                _pendingExpandItem = null;
            }
            
            // If this item was auto-expanded, start collapse timer
            if (_autoExpandedItems.Contains(item))
            {
                _pendingCollapseItem = item;
                _collapseTimer.Stop();
                _collapseTimer.Start();
            }
        }
        
        /// <summary>
        /// Cancels all pending operations
        /// </summary>
        public void CancelAll()
        {
            _expandTimer.Stop();
            _collapseTimer.Stop();
            _pendingExpandItem = null;
            _pendingCollapseItem = null;
        }
        
        /// <summary>
        /// Collapses all auto-expanded folders
        /// </summary>
        public void CollapseAll()
        {
            foreach (var item in _autoExpandedItems)
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
            return item != null && _autoExpandedItems.Contains(item);
        }
        
        #endregion
        
        #region Private Methods
        
        private void OnExpandTimerTick(object sender, EventArgs e)
        {
            _expandTimer.Stop();
            
            if (_pendingExpandItem != null && !_pendingExpandItem.IsExpanded)
            {
                _autoExpandedItems.Add(_pendingExpandItem);
                FolderExpanding?.Invoke(this, _pendingExpandItem);
            }
            
            _pendingExpandItem = null;
        }
        
        private void OnCollapseTimerTick(object sender, EventArgs e)
        {
            _collapseTimer.Stop();
            
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
                    _expandTimer.Stop();
                    _expandTimer.Tick -= OnExpandTimerTick;
                    
                    _collapseTimer.Stop();
                    _collapseTimer.Tick -= OnCollapseTimerTick;
                    
                    _autoExpandedItems.Clear();
                }
                
                _disposed = true;
            }
        }
        
        #endregion
    }
}