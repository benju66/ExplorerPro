// UI/FileTree/Services/FileTreeCacheService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// LRU (Least Recently Used) cache implementation for file tree items
    /// </summary>
    public class FileTreeCacheService : IFileTreeCache
    {
        private readonly Dictionary<string, CacheNode> _cache;
        private readonly Dictionary<string, LinkedListNode<CacheNode>> _accessOrder;
        private readonly LinkedList<CacheNode> _accessList;
        private readonly int _capacity;
        private readonly ReaderWriterLockSlim _lock;

        public event EventHandler<CacheEvictionEventArgs> ItemEvicted;

        public int Count 
        { 
            get 
            { 
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

        public FileTreeCacheService(int capacity = 1000)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than zero", nameof(capacity));

            _capacity = capacity;
            _cache = new Dictionary<string, CacheNode>(capacity);
            _accessOrder = new Dictionary<string, LinkedListNode<CacheNode>>(capacity);
            _accessList = new LinkedList<CacheNode>();
            _lock = new ReaderWriterLockSlim();
        }

        public FileTreeItem GetItem(string key)
        {
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

        public void SetItem(string key, FileTreeItem item)
        {
            if (string.IsNullOrEmpty(key) || item == null)
                return;

            _lock.EnterWriteLock();
            try
            {
                if (_cache.TryGetValue(key, out CacheNode existingNode))
                {
                    // Update existing item
                    var oldItem = existingNode.Item;
                    existingNode.Item = item;
                    UpdateAccessOrder(key, existingNode);
                    
                    // Raise replaced event
                    OnItemEvicted(key, oldItem, EvictionReason.Replaced);
                }
                else
                {
                    // Add new item
                    if (_cache.Count >= _capacity)
                    {
                        // Remove least recently used item
                        var lru = _accessList.Last;
                        if (lru != null)
                        {
                            var lruKey = lru.Value.Key;
                            var lruItem = lru.Value.Item;
                            
                            _cache.Remove(lruKey);
                            _accessOrder.Remove(lruKey);
                            _accessList.RemoveLast();
                            
                            // Raise evicted event
                            OnItemEvicted(lruKey, lruItem, EvictionReason.CapacityExceeded);
                        }
                    }

                    var newNode = new CacheNode { Key = key, Item = item };
                    _cache[key] = newNode;
                    var listNode = _accessList.AddFirst(newNode);
                    _accessOrder[key] = listNode;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool RemoveItem(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            _lock.EnterWriteLock();
            try
            {
                if (_cache.TryGetValue(key, out CacheNode node))
                {
                    _cache.Remove(key);
                    var listNode = _accessOrder[key];
                    _accessOrder.Remove(key);
                    _accessList.Remove(listNode);
                    
                    // Raise removed event
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

        public bool ContainsKey(string key)
        {
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
            _lock.EnterWriteLock();
            try
            {
                // Raise cleared event for all items
                foreach (var kvp in _cache)
                {
                    OnItemEvicted(kvp.Key, kvp.Value.Item, EvictionReason.Cleared);
                }
                
                _cache.Clear();
                _accessOrder.Clear();
                _accessList.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public int RemoveWhere(Func<KeyValuePair<string, FileTreeItem>, bool> predicate)
        {
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
                    RemoveItemInternal(key);
                }

                return keysToRemove.Count;
            }
            finally
            {
                _lock.ExitWriteLock();
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
            
            var newListNode = _accessList.AddFirst(node);
            _accessOrder[key] = newListNode;
        }

        /// <summary>
        /// Removes an item from the cache without raising events (internal use)
        /// </summary>
        private void RemoveItemInternal(string key)
        {
            if (_cache.TryGetValue(key, out CacheNode node))
            {
                _cache.Remove(key);
                var listNode = _accessOrder[key];
                _accessOrder.Remove(key);
                _accessList.Remove(listNode);
                
                // Raise removed event
                OnItemEvicted(key, node.Item, EvictionReason.Removed);
            }
        }

        protected virtual void OnItemEvicted(string key, FileTreeItem item, EvictionReason reason)
        {
            ItemEvicted?.Invoke(this, new CacheEvictionEventArgs(key, item, reason));
        }

        /// <summary>
        /// Cache node containing key and item data
        /// </summary>
        private class CacheNode
        {
            public string Key { get; set; }
            public FileTreeItem Item { get; set; }
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _lock?.Dispose();
            }
        }

        #endregion
    }
}