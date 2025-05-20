// UI/FileTree/Services/IFileTreeCache.cs
using System;
using System.Collections.Generic;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Interface for file tree caching operations
    /// </summary>
    public interface IFileTreeCache
    {
        /// <summary>
        /// Gets an item from the cache
        /// </summary>
        /// <param name="key">Cache key (typically file path)</param>
        /// <returns>Cached item or null if not found</returns>
        FileTreeItem GetItem(string key);

        /// <summary>
        /// Adds or updates an item in the cache
        /// </summary>
        /// <param name="key">Cache key (typically file path)</param>
        /// <param name="item">Item to cache</param>
        void SetItem(string key, FileTreeItem item);

        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        /// <param name="key">Cache key to remove</param>
        /// <returns>True if item was removed, false if not found</returns>
        bool RemoveItem(string key);

        /// <summary>
        /// Checks if an item exists in the cache
        /// </summary>
        /// <param name="key">Cache key to check</param>
        /// <returns>True if item exists in cache</returns>
        bool ContainsKey(string key);

        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets the current number of items in the cache
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the maximum capacity of the cache
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// Removes items from cache that match a predicate
        /// </summary>
        /// <param name="predicate">Function to test each item</param>
        /// <returns>Number of items removed</returns>
        int RemoveWhere(Func<KeyValuePair<string, FileTreeItem>, bool> predicate);

        /// <summary>
        /// Event raised when an item is evicted from the cache
        /// </summary>
        event EventHandler<CacheEvictionEventArgs> ItemEvicted;
    }

    /// <summary>
    /// Event arguments for cache eviction events
    /// </summary>
    public class CacheEvictionEventArgs : EventArgs
    {
        public string Key { get; }
        public FileTreeItem Item { get; }
        public EvictionReason Reason { get; }

        public CacheEvictionEventArgs(string key, FileTreeItem item, EvictionReason reason)
        {
            Key = key;
            Item = item;
            Reason = reason;
        }
    }

    /// <summary>
    /// Reasons for cache eviction
    /// </summary>
    public enum EvictionReason
    {
        /// <summary>
        /// Item was removed manually
        /// </summary>
        Removed,
        
        /// <summary>
        /// Item was evicted due to cache capacity limits
        /// </summary>
        CapacityExceeded,
        
        /// <summary>
        /// Cache was cleared
        /// </summary>
        Cleared,
        
        /// <summary>
        /// Item was replaced with a newer version
        /// </summary>
        Replaced
    }
}