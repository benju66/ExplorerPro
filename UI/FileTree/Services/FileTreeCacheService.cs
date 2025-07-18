// UI/FileTree/Services/FileTreeCacheService.cs - Enhanced with batch operations and optimized eviction
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// LRU (Least Recently Used) cache implementation for file tree items
    /// Enhanced version with batch operations and optimized eviction
    /// </summary>
    public class FileTreeCacheService : IFileTreeCache, IDisposable
    {
        private readonly Dictionary<string, CacheNode> _cache;
        private readonly Dictionary<string, LinkedListNode<CacheNode>> _accessOrder;
        private readonly LinkedList<CacheNode> _accessList;
        private readonly int _capacity;
        private readonly int _maxCapacity; // Allow 10% overflow
        private readonly ReaderWriterLockSlim _lock;
        private bool _disposed;
        
        // Track total memory usage (approximate)
        private long _approximateMemoryUsage;
        private readonly long _maxMemoryUsage;
        private const long ESTIMATED_ITEM_SIZE = 1024; // Estimated bytes per FileTreeItem
        
        // Batch operation support
        private readonly List<KeyValuePair<string, FileTreeItem>> _pendingBatch;
        private bool _batchMode = false;

        public event EventHandler<CacheEvictionEventArgs> ItemEvicted;

        public int Count 
        { 
            get 
            { 
                ThrowIfDisposed();
                _lock.EnterReadLock();
                try 
                { 
                    return _cache.Count; 
                }
                finally 
                { 
                    _lock.ExitReadLock(); 
                }
            } 
        }

        public int Capacity => _capacity;

        public FileTreeCacheService(int capacity = 1000, long maxMemoryUsageMB = 100)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than zero", nameof(capacity));

            _capacity = capacity;
            _maxCapacity = (int)(capacity * 1.1); // Allow 10% overflow
            _maxMemoryUsage = maxMemoryUsageMB * 1024 * 1024; // Convert MB to bytes
            _cache = new Dictionary<string, CacheNode>(capacity, StringComparer.OrdinalIgnoreCase);
            _accessOrder = new Dictionary<string, LinkedListNode<CacheNode>>(capacity, StringComparer.OrdinalIgnoreCase);
            _accessList = new LinkedList<CacheNode>();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _approximateMemoryUsage = 0;
            _pendingBatch = new List<KeyValuePair<string, FileTreeItem>>(100);
        }

        public FileTreeItem? GetItem(string? key)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(key))
                return null;

            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_cache.TryGetValue(key, out CacheNode node))
                {
                    // Move to front (most recently used)
                    _lock.EnterWriteLock();
                    try
                    {
                        UpdateAccessOrder(key, node);
                        return node.Item;
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                }
                return null;
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public void SetItem(string? key, FileTreeItem? item)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(key) || item == null)
                return;

            _lock.EnterWriteLock();
            try
            {
                // Check if item already exists
                if (_cache.TryGetValue(key, out CacheNode existingNode))
                {
                    // Update existing item
                    existingNode.Item = item;
                    existingNode.LastAccessed = DateTime.Now;
                    existingNode.EstimatedSize = EstimateItemSize(item);
                    UpdateAccessOrder(key, existingNode);
                    OnItemEvicted(key, existingNode.Item, EvictionReason.Replaced);
                }
                else
                {
                    // Add new item
                    var newNode = new CacheNode
                    {
                        Key = key,
                        Item = item,
                        LastAccessed = DateTime.Now,
                        EstimatedSize = EstimateItemSize(item)
                    };

                    // Check capacity and evict if necessary
                    EnsureCapacity();

                    _cache[key] = newNode;
                    var node = _accessList.AddFirst(newNode);
                    _accessOrder[key] = node;
                    _approximateMemoryUsage += newNode.EstimatedSize;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Sets multiple items in batch for better performance
        /// </summary>
        public void SetItemBatch(IEnumerable<KeyValuePair<string, FileTreeItem>> items)
        {
            ThrowIfDisposed();
            
            if (items == null)
                return;

            _lock.EnterWriteLock();
            try
            {
                _batchMode = true;
                
                foreach (var kvp in items)
                {
                    if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                    {
                        SetItemInternal(kvp.Key, kvp.Value);
                    }
                }
                
                // Perform batch eviction if needed
                PerformBatchEviction();
            }
            finally
            {
                _batchMode = false;
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Internal set method without locking for batch operations
        /// </summary>
        private void SetItemInternal(string key, FileTreeItem item)
        {
            if (_cache.TryGetValue(key, out CacheNode existingNode))
            {
                // Update existing item
                var oldItem = existingNode.Item;
                existingNode.Item = item;
                existingNode.LastAccessed = DateTime.UtcNow;
                existingNode.EstimatedSize = EstimateItemSize(item);
                UpdateAccessOrder(key, existingNode);
                
                // Update memory usage
                _approximateMemoryUsage = _approximateMemoryUsage - existingNode.EstimatedSize + EstimateItemSize(item);
                
                // Raise replaced event only if not in batch mode
                if (!_batchMode)
                {
                    OnItemEvicted(key, oldItem, EvictionReason.Replaced);
                }
            }
            else
            {
                // Check if we need to evict based on capacity or memory
                // Allow temporary overflow during batch operations
                if (!_batchMode && ShouldEvictImmediate())
                {
                    EvictLeastRecentlyUsed();
                }

                var newNode = new CacheNode 
                { 
                    Key = key, 
                    Item = item,
                    LastAccessed = DateTime.UtcNow,
                    EstimatedSize = EstimateItemSize(item)
                };
                
                _cache[key] = newNode;
                var listNode = _accessList.AddFirst(newNode);
                _accessOrder[key] = listNode;
                _approximateMemoryUsage += newNode.EstimatedSize;
            }
        }

        /// <summary>
        /// Performs batch eviction after batch operations
        /// </summary>
        private void PerformBatchEviction()
        {
            // Only evict if we're over the normal capacity (not the max capacity)
            while (_cache.Count > _capacity || _approximateMemoryUsage > _maxMemoryUsage)
            {
                EvictLeastRecentlyUsedBatch();
            }
        }

        /// <summary>
        /// Optimized batch eviction that removes multiple items at once
        /// </summary>
        private void EvictLeastRecentlyUsedBatch()
        {
            // Calculate how many items to evict (5% of capacity or at least 10)
            int evictCount = Math.Max(10, _capacity / 20);
            var itemsToEvict = new List<string>();
            
            // Collect items to evict from the end of the access list
            var current = _accessList.Last;
            while (current != null && itemsToEvict.Count < evictCount)
            {
                itemsToEvict.Add(current.Value.Key);
                current = current.Previous;
            }
            
            // Evict collected items
            foreach (var key in itemsToEvict)
            {
                RemoveItemInternal(key, EvictionReason.CapacityExceeded);
            }
        }

        public bool RemoveItem(string? key)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(key))
                return false;

            _lock.EnterWriteLock();
            try
            {
                if (_cache.TryGetValue(key, out CacheNode node))
                {
                    _cache.Remove(key);
                    _accessOrder.Remove(key);
                    _accessList.Remove(node);
                    _approximateMemoryUsage -= node.EstimatedSize;
                    OnItemEvicted(key, node.Item, EvictionReason.Removed);
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool ContainsKey(string? key)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(key))
                return false;

            _lock.EnterReadLock();
            try
            {
                return _cache.ContainsKey(key);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Clear()
        {
            ThrowIfDisposed();
            
            _lock.EnterWriteLock();
            try
            {
                // Raise cleared event for all items
                var itemsToNotify = _cache.ToList(); // Create a copy for thread safety
                
                _cache.Clear();
                _accessOrder.Clear();
                _accessList.Clear();
                _approximateMemoryUsage = 0;
                
                // Notify after clearing to avoid deadlocks
                if (!_batchMode)
                {
                    foreach (var kvp in itemsToNotify)
                    {
                        OnItemEvicted(kvp.Key, kvp.Value.Item, EvictionReason.Cleared);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public int RemoveWhere(Func<KeyValuePair<string, FileTreeItem>, bool> predicate)
        {
            ThrowIfDisposed();
            
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _lock.EnterWriteLock();
            try
            {
                var keysToRemove = new List<string>();
                
                foreach (var kvp in _cache)
                {
                    var item = new KeyValuePair<string, FileTreeItem>(kvp.Key, kvp.Value.Item);
                    if (predicate(item))
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    RemoveItemInternal(key, EvictionReason.Removed);
                }

                return keysToRemove.Count;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Trims the cache to a specific percentage of its current size
        /// </summary>
        public void TrimToPercentage(int percentage)
        {
            ThrowIfDisposed();
            
            if (percentage < 0 || percentage > 100)
                throw new ArgumentOutOfRangeException(nameof(percentage));
            
            _lock.EnterWriteLock();
            try
            {
                int targetCount = (_cache.Count * percentage) / 100;
                while (_cache.Count > targetCount)
                {
                    EvictLeastRecentlyUsed();
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        #region Private Methods

        /// <summary>
        /// Checks if we should evict items immediately (not in batch mode)
        /// </summary>
        private bool ShouldEvictImmediate()
        {
            return _cache.Count >= _maxCapacity || _approximateMemoryUsage >= _maxMemoryUsage;
        }

        /// <summary>
        /// Checks if we should evict items based on capacity or memory constraints
        /// </summary>
        private bool ShouldEvict()
        {
            return _cache.Count >= _capacity || _approximateMemoryUsage >= _maxMemoryUsage;
        }

        /// <summary>
        /// Evicts the least recently used item
        /// </summary>
        private void EvictLeastRecentlyUsed()
        {
            var lru = _accessList.Last;
            if (lru != null)
            {
                RemoveItemInternal(lru.Value.Key, EvictionReason.CapacityExceeded);
            }
        }

        /// <summary>
        /// Updates the access order for an item (moves to front)
        /// </summary>
        private void UpdateAccessOrder(string key, CacheNode node)
        {
            if (_accessOrder.TryGetValue(key, out LinkedListNode<CacheNode> listNode))
            {
                _accessList.Remove(listNode);
            }
            
            node.LastAccessed = DateTime.UtcNow;
            var newListNode = _accessList.AddFirst(node);
            _accessOrder[key] = newListNode;
        }

        /// <summary>
        /// Removes an item from the cache without locking (must be called within write lock)
        /// </summary>
        private bool RemoveItemInternal(string key, EvictionReason reason)
        {
            if (_cache.TryGetValue(key, out CacheNode node))
            {
                _cache.Remove(key);
                
                if (_accessOrder.TryGetValue(key, out var listNode))
                {
                    _accessOrder.Remove(key);
                    _accessList.Remove(listNode);
                }
                
                _approximateMemoryUsage -= node.EstimatedSize;
                
                // Raise removed event only if not in batch mode
                if (!_batchMode)
                {
                    OnItemEvicted(key, node.Item, reason);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Estimates the memory size of a FileTreeItem
        /// </summary>
        private long EstimateItemSize(FileTreeItem item)
        {
            if (item == null) return 0;
            
            // Base size for object overhead
            long size = ESTIMATED_ITEM_SIZE;
            
            // Add string sizes
            size += (item.Name?.Length ?? 0) * 2; // Unicode chars
            size += (item.Path?.Length ?? 0) * 2;
            size += (item.Type?.Length ?? 0) * 2;
            
            // Add estimated size for children collection
            size += item.Children.Count * 8; // Reference size
            
            return size;
        }

        protected virtual void OnItemEvicted(string key, FileTreeItem item, EvictionReason reason)
        {
            if (!_disposed && !_batchMode)
            {
                try
                {
                    ItemEvicted?.Invoke(this, new CacheEvictionEventArgs(key, item, reason));
                }
                catch (Exception ex)
                {
                    // Log but don't throw from event handlers
                    System.Diagnostics.Debug.WriteLine($"[CACHE] Error in ItemEvicted handler: {ex.Message}");
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTreeCacheService));
        }

        private void EnsureCapacity()
        {
            while (_cache.Count >= _maxCapacity || _approximateMemoryUsage > _maxMemoryUsage)
            {
                var oldestNode = _accessList.Last;
                if (oldestNode == null) break;

                var key = oldestNode.Value.Key;
                var item = oldestNode.Value.Item;
                
                _cache.Remove(key);
                _accessOrder.Remove(key);
                _accessList.RemoveLast();
                _approximateMemoryUsage -= oldestNode.Value.EstimatedSize;
                
                OnItemEvicted(key, item, EvictionReason.CapacityExceeded);
            }
        }

        #endregion

        #region Nested Types
        
        /// <summary>
        /// Cache node containing key and item data with additional metadata
        /// </summary>
        private class CacheNode
        {
            public string Key { get; set; }
            public FileTreeItem Item { get; set; }
            public DateTime LastAccessed { get; set; }
            public long EstimatedSize { get; set; }
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
                    System.Diagnostics.Debug.WriteLine("[DISPOSE] Disposing FileTreeCacheService");
                    
                    // Enter write lock for final cleanup
                    _lock?.EnterWriteLock();
                    try
                    {
                        // Clear all collections
                        _cache?.Clear();
                        _accessOrder?.Clear();
                        _accessList?.Clear();
                        _pendingBatch?.Clear();
                        
                        // Clear event handlers
                        ItemEvicted = null;
                        
                        System.Diagnostics.Debug.WriteLine($"[DISPOSE] Cache cleared. Final count: {_cache?.Count ?? 0}");
                    }
                    finally
                    {
                        _lock?.ExitWriteLock();
                    }
                    
                    // Dispose the lock
                    _lock?.Dispose();
                    
                    System.Diagnostics.Debug.WriteLine("[DISPOSE] FileTreeCacheService disposed");
                }
                
                _disposed = true;
            }
        }

        ~FileTreeCacheService()
        {
            Dispose(false);
        }

        #endregion
    }
}