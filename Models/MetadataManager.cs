// Models/MetadataManager.cs - Enhanced with batch operations
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Manages metadata for files and folders including pinned items, recent items, tags, colors, and bold status.
    /// Enhanced version with batch operations for better performance.
    /// </summary>
    public class MetadataManager : IDisposable
    {
        private string metadataFile;
        private MetadataStructure metadata;
        private readonly object _syncLock = new object();
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
        private readonly int _maxRecentItems = 100;
        private readonly int _maxPinnedItems = 50;
        private readonly int _maxMetadataEntries = 10000;
        private bool _disposed;
        
        // Singleton pattern implementation
        private static MetadataManager? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the singleton instance of the MetadataManager.
        /// </summary>
        public static MetadataManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            // Use the app-wide instance if available
                            _instance = App.MetadataManager ?? new MetadataManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initializes a new instance of the MetadataManager class.
        /// </summary>
        /// <param name="metadataFile">Path to the metadata JSON file.</param>
        public MetadataManager(string metadataFile = @"data\metadata.json")
        {
            this.metadataFile = metadataFile;
            
            // Provide defaults for any keys used later
            metadata = new MetadataStructure
            {
                PinnedItems = new List<string>(),
                RecentItems = new List<string>(),
                Tags = new Dictionary<string, List<string>>(),
                LastAccessed = new Dictionary<string, double>(),
                ItemColors = new Dictionary<string, string>(),
                ItemBold = new Dictionary<string, bool>(),
                RecentColors = new List<string>()
            };

            LoadMetadata();
            
            // Initialize cleanup timer
            _cleanupTimer = new Timer(PerformCleanup, null, _cleanupInterval, _cleanupInterval);
        }

        /// <summary>
        /// Loads metadata from the JSON file or creates a default structure if missing/corrupt.
        /// </summary>
        public void LoadMetadata()
        {
            lock (_syncLock)
            {
                try
                {
                    if (File.Exists(metadataFile))
                    {
                        string jsonContent = File.ReadAllText(metadataFile);
                        metadata = JsonConvert.DeserializeObject<MetadataStructure>(jsonContent);

                        // Ensure all required properties exist in the loaded metadata
                        if (metadata.PinnedItems == null) metadata.PinnedItems = new List<string>();
                        if (metadata.RecentItems == null) metadata.RecentItems = new List<string>();
                        if (metadata.Tags == null) metadata.Tags = new Dictionary<string, List<string>>();
                        if (metadata.LastAccessed == null) metadata.LastAccessed = new Dictionary<string, double>();
                        if (metadata.ItemColors == null) metadata.ItemColors = new Dictionary<string, string>();
                        if (metadata.ItemBold == null) metadata.ItemBold = new Dictionary<string, bool>();
                        if (metadata.RecentColors == null) metadata.RecentColors = new List<string>();
                        
                        // Perform initial cleanup
                        CleanupMetadata();
                    }
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException)
                {
                    Console.WriteLine($"Metadata file is corrupt or missing. Recreating with defaults. Error: {ex.Message}");
                    
                    // Reset to defaults if there's an error
                    metadata = new MetadataStructure
                    {
                        PinnedItems = new List<string>(),
                        RecentItems = new List<string>(),
                        Tags = new Dictionary<string, List<string>>(),
                        LastAccessed = new Dictionary<string, double>(),
                        ItemColors = new Dictionary<string, string>(),
                        ItemBold = new Dictionary<string, bool>(),
                        RecentColors = new List<string>()
                    };
                }

                SaveMetadata();
            }
        }

        /// <summary>
        /// Saves the current metadata to the JSON file.
        /// </summary>
        public void SaveMetadata()
        {
            if (_disposed) return;
            
            lock (_syncLock)
            {
                try
                {
                    // Create directory if it doesn't exist
                    Directory.CreateDirectory(Path.GetDirectoryName(metadataFile));
                    
                    // Serialize and save the metadata
                    string jsonContent = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                    File.WriteAllText(metadataFile, jsonContent);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Error saving metadata: {ex.Message}");
                }
            }
        }

        #region Batch Operations

        /// <summary>
        /// Gets metadata for multiple items in a single batch operation
        /// </summary>
        /// <param name="paths">Collection of paths to get metadata for</param>
        /// <returns>Dictionary of path to metadata info</returns>
        public Dictionary<string, MetadataInfo> GetBatchMetadata(IEnumerable<string> paths)
        {
            if (paths == null)
                return new Dictionary<string, MetadataInfo>();

            lock (_syncLock)
            {
                var result = new Dictionary<string, MetadataInfo>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var path in paths)
                {
                    if (string.IsNullOrEmpty(path))
                        continue;
                    
                    var info = new MetadataInfo
                    {
                        Path = path,
                        IsPinned = metadata.PinnedItems.Contains(path),
                        IsRecent = metadata.RecentItems.Contains(path),
                        Tags = metadata.Tags.TryGetValue(path, out var tags) ? new List<string>(tags) : new List<string>(),
                        Color = metadata.ItemColors.TryGetValue(path, out var color) ? color : null,
                        IsBold = metadata.ItemBold.TryGetValue(path, out var bold) && bold,
                        LastAccessed = metadata.LastAccessed.TryGetValue(path, out var accessed) ? accessed : (double?)null
                    };
                    
                    result[path] = info;
                }
                
                return result;
            }
        }

        /// <summary>
        /// Sets metadata for multiple items in a single batch operation
        /// </summary>
        /// <param name="items">Collection of metadata info to set</param>
        public void SetBatchMetadata(IEnumerable<MetadataInfo> items)
        {
            if (items == null)
                return;

            lock (_syncLock)
            {
                foreach (var info in items)
                {
                    if (string.IsNullOrEmpty(info.Path))
                        continue;
                    
                    // Update pinned status
                    if (info.IsPinned && !metadata.PinnedItems.Contains(info.Path))
                    {
                        metadata.PinnedItems.Add(info.Path);
                    }
                    else if (!info.IsPinned)
                    {
                        metadata.PinnedItems.Remove(info.Path);
                    }
                    
                    // Update tags
                    if (info.Tags != null && info.Tags.Count > 0)
                    {
                        metadata.Tags[info.Path] = new List<string>(info.Tags);
                    }
                    else
                    {
                        metadata.Tags.Remove(info.Path);
                    }
                    
                    // Update color
                    if (!string.IsNullOrEmpty(info.Color))
                    {
                        metadata.ItemColors[info.Path] = info.Color;
                    }
                    else
                    {
                        metadata.ItemColors.Remove(info.Path);
                    }
                    
                    // Update bold status
                    if (info.IsBold)
                    {
                        metadata.ItemBold[info.Path] = true;
                    }
                    else
                    {
                        metadata.ItemBold.Remove(info.Path);
                    }
                    
                    // Update last accessed if provided
                    if (info.LastAccessed.HasValue)
                    {
                        metadata.LastAccessed[info.Path] = info.LastAccessed.Value;
                    }
                }
                
                // Save after batch update
                SaveMetadata();
            }
        }

        /// <summary>
        /// Adds multiple recent items in a single batch operation
        /// </summary>
        /// <param name="paths">Collection of paths to add as recent</param>
        public void AddBatchRecentItems(IEnumerable<string> paths)
        {
            if (paths == null)
                return;

            lock (_syncLock)
            {
                foreach (var path in paths)
                {
                    if (string.IsNullOrEmpty(path))
                        continue;
                    
                    // Remove if already exists
                    metadata.RecentItems.Remove(path);
                    
                    // Insert at beginning
                    metadata.RecentItems.Insert(0, path);
                }
                
                // Trim to max items
                if (metadata.RecentItems.Count > _maxRecentItems)
                {
                    metadata.RecentItems = metadata.RecentItems.Take(_maxRecentItems).ToList();
                }
                
                SaveMetadata();
            }
        }

        #endregion

        /// <summary>
        /// Performs cleanup of metadata to prevent unbounded growth
        /// </summary>
        private void PerformCleanup(object state)
        {
            if (_disposed) return;
            
            try
            {
                lock (_syncLock)
                {
                    CleanupMetadata();
                    SaveMetadata();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during metadata cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up metadata by removing old, invalid, or excess entries
        /// </summary>
        private void CleanupMetadata()
        {
            // Clean up recent items
            if (metadata.RecentItems.Count > _maxRecentItems)
            {
                metadata.RecentItems = metadata.RecentItems.Take(_maxRecentItems).ToList();
            }
            
            // Clean up pinned items
            if (metadata.PinnedItems.Count > _maxPinnedItems)
            {
                metadata.PinnedItems = metadata.PinnedItems.Take(_maxPinnedItems).ToList();
            }
            
            // Remove metadata for non-existent files/folders
            RemoveInvalidPaths();
            
            // Clean up old last accessed entries (older than 30 days)
            CleanupOldLastAccessed();
            
            // Limit total metadata entries
            if (GetTotalMetadataCount() > _maxMetadataEntries)
            {
                TrimOldestMetadata();
            }
        }

        /// <summary>
        /// Removes metadata for paths that no longer exist
        /// </summary>
        private void RemoveInvalidPaths()
        {
            // Clean recent items
            metadata.RecentItems = metadata.RecentItems
                .Where(path => File.Exists(path) || Directory.Exists(path))
                .ToList();
            
            // Clean pinned items
            metadata.PinnedItems = metadata.PinnedItems
                .Where(path => File.Exists(path) || Directory.Exists(path))
                .ToList();
            
            // Clean tags
            var invalidTagPaths = metadata.Tags.Keys
                .Where(path => !File.Exists(path) && !Directory.Exists(path))
                .ToList();
            foreach (var path in invalidTagPaths)
            {
                metadata.Tags.Remove(path);
            }
            
            // Clean colors
            var invalidColorPaths = metadata.ItemColors.Keys
                .Where(path => !File.Exists(path) && !Directory.Exists(path))
                .ToList();
            foreach (var path in invalidColorPaths)
            {
                metadata.ItemColors.Remove(path);
            }
            
            // Clean bold settings
            var invalidBoldPaths = metadata.ItemBold.Keys
                .Where(path => !File.Exists(path) && !Directory.Exists(path))
                .ToList();
            foreach (var path in invalidBoldPaths)
            {
                metadata.ItemBold.Remove(path);
            }
            
            // Clean last accessed
            var invalidAccessPaths = metadata.LastAccessed.Keys
                .Where(path => !File.Exists(path) && !Directory.Exists(path))
                .ToList();
            foreach (var path in invalidAccessPaths)
            {
                metadata.LastAccessed.Remove(path);
            }
        }

        /// <summary>
        /// Removes last accessed entries older than 30 days
        /// </summary>
        private void CleanupOldLastAccessed()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var cutoffTimestamp = (cutoffDate - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            
            var oldEntries = metadata.LastAccessed
                .Where(kvp => kvp.Value < cutoffTimestamp)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in oldEntries)
            {
                metadata.LastAccessed.Remove(key);
            }
        }

        /// <summary>
        /// Gets the total count of metadata entries
        /// </summary>
        private int GetTotalMetadataCount()
        {
            return metadata.Tags.Count + 
                   metadata.ItemColors.Count + 
                   metadata.ItemBold.Count + 
                   metadata.LastAccessed.Count;
        }

        /// <summary>
        /// Trims the oldest metadata entries when limit is exceeded
        /// </summary>
        private void TrimOldestMetadata()
        {
            // Sort last accessed entries by timestamp and remove oldest
            var sortedAccess = metadata.LastAccessed
                .OrderBy(kvp => kvp.Value)
                .ToList();
            
            // Remove oldest 20% of entries
            int removeCount = sortedAccess.Count / 5;
            foreach (var entry in sortedAccess.Take(removeCount))
            {
                RemoveAllMetadataForPath(entry.Key);
            }
        }

        /// <summary>
        /// Removes all metadata for a specific path
        /// </summary>
        public void RemoveAllMetadataForPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            
            lock (_syncLock)
            {
                metadata.PinnedItems.Remove(path);
                metadata.RecentItems.Remove(path);
                metadata.Tags.Remove(path);
                metadata.LastAccessed.Remove(path);
                metadata.ItemColors.Remove(path);
                metadata.ItemBold.Remove(path);
            }
        }

        /// <summary>
        /// Forces an immediate cleanup of metadata
        /// </summary>
        public void ForceCleanup()
        {
            PerformCleanup(null);
        }

        /// <summary>
        /// Updates path references when a file or folder is renamed or moved.
        /// </summary>
        /// <param name="oldPath">Original path</param>
        /// <param name="newPath">New path</param>
        public void UpdatePathReferences(string oldPath, string newPath)
        {
            lock (_syncLock)
            {
                // Update pinned items
                UpdateItemPathInCollection(oldPath, newPath, metadata.PinnedItems);
                
                // Update recent items
                UpdateItemPathInCollection(oldPath, newPath, metadata.RecentItems);
                
                // Update tags
                if (metadata.Tags.TryGetValue(oldPath, out var tags))
                {
                    metadata.Tags.Remove(oldPath);
                    metadata.Tags[newPath] = tags;
                }
                
                // Update last accessed
                if (metadata.LastAccessed.TryGetValue(oldPath, out var timestamp))
                {
                    metadata.LastAccessed.Remove(oldPath);
                    metadata.LastAccessed[newPath] = timestamp;
                }
                
                // Update item colors
                if (metadata.ItemColors.TryGetValue(oldPath, out var color))
                {
                    metadata.ItemColors.Remove(oldPath);
                    metadata.ItemColors[newPath] = color;
                }
                
                // Update item bold settings
                if (metadata.ItemBold.TryGetValue(oldPath, out var isBold))
                {
                    metadata.ItemBold.Remove(oldPath);
                    metadata.ItemBold[newPath] = isBold;
                }
                
                // Save metadata to persist changes
                SaveMetadata();
            }
        }
        
        /// <summary>
        /// Updates a path in a collection
        /// </summary>
        private void UpdateItemPathInCollection(string oldPath, string newPath, List<string> collection)
        {
            int index = collection.IndexOf(oldPath);
            if (index >= 0)
            {
                collection[index] = newPath;
            }
        }

        #region Recent Colors Methods

        /// <summary>
        /// Returns the list of recent color hex codes (e.g., ['#FF0000', '#00FF00']).
        /// </summary>
        /// <returns>List of recent color hex codes.</returns>
        public List<string> GetRecentColors()
        {
            lock (_syncLock)
            {
                return new List<string>(metadata.RecentColors);
            }
        }

        /// <summary>
        /// Inserts a newly used color at the front of the list, removing duplicates, 
        /// and limits to 5 entries, then saves.
        /// </summary>
        /// <param name="colorHex">The color hex code to add.</param>
        public void AddRecentColor(string colorHex)
        {
            lock (_syncLock)
            {
                // If color is already in the list, remove it so we can re-insert at front
                if (metadata.RecentColors.Contains(colorHex))
                {
                    metadata.RecentColors.Remove(colorHex);
                }
                
                // Insert at the beginning of the list
                metadata.RecentColors.Insert(0, colorHex);
                
                // Cap at 5 entries
                metadata.RecentColors = metadata.RecentColors.Take(5).ToList();
                
                SaveMetadata();
            }
        }

        /// <summary>
        /// Removes a single color from recent_colors, then saves.
        /// </summary>
        /// <param name="colorHex">The color hex code to remove.</param>
        public void RemoveRecentColor(string colorHex)
        {
            lock (_syncLock)
            {
                if (metadata.RecentColors.Contains(colorHex))
                {
                    metadata.RecentColors.Remove(colorHex);
                    SaveMetadata();
                }
            }
        }

        /// <summary>
        /// Removes all recent colors at once, then saves.
        /// </summary>
        public void ClearRecentColors()
        {
            lock (_syncLock)
            {
                metadata.RecentColors.Clear();
                SaveMetadata();
            }
        }

        #endregion

        #region Item-based Color & Bold Methods

        /// <summary>
        /// Stores a color (e.g. '#FF0000') for any path (file or folder).
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <param name="colorHex">Color hex code to set.</param>
        public void SetItemColor(string itemPath, string colorHex)
        {
            lock (_syncLock)
            {
                metadata.ItemColors[itemPath] = colorHex;
                SaveMetadata();
            }
        }

        /// <summary>
        /// Retrieves a stored color hex for a path, or null if none set.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <returns>Color hex code or null if not found.</returns>
        public string GetItemColor(string itemPath)
        {
            lock (_syncLock)
            {
                return metadata.ItemColors.TryGetValue(itemPath, out string color) ? color : null;
            }
        }

        /// <summary>
        /// Stores whether this path's text should be bold (True/False).
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <param name="boldFlag">Whether the item should be bold.</param>
        public void SetItemBold(string itemPath, bool boldFlag)
        {
            lock (_syncLock)
            {
                metadata.ItemBold[itemPath] = boldFlag;
                SaveMetadata();
            }
        }

        /// <summary>
        /// Return True if a path is bold, else False.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <returns>True if the item is bold, otherwise False.</returns>
        public bool GetItemBold(string itemPath)
        {
            lock (_syncLock)
            {
                return metadata.ItemBold.TryGetValue(itemPath, out bool isBold) && isBold;
            }
        }

        #endregion

        #region Pinned Items Methods

        /// <summary>
        /// Adds an item to the pinned items list if it doesn't already exist.
        /// </summary>
        /// <param name="itemPath">Path to the item to pin.</param>
        public void AddPinnedItem(string itemPath)
        {
            lock (_syncLock)
            {
                if (!metadata.PinnedItems.Contains(itemPath))
                {
                    metadata.PinnedItems.Add(itemPath);
                    
                    // Enforce limit
                    if (metadata.PinnedItems.Count > _maxPinnedItems)
                    {
                        metadata.PinnedItems.RemoveAt(0); // Remove oldest
                    }
                    
                    SaveMetadata();
                }
            }
        }

        /// <summary>
        /// Removes an item from the pinned items list.
        /// </summary>
        /// <param name="itemPath">Path to the item to unpin.</param>
        public void RemovePinnedItem(string itemPath)
        {
            lock (_syncLock)
            {
                if (metadata.PinnedItems.Contains(itemPath))
                {
                    metadata.PinnedItems.Remove(itemPath);
                    SaveMetadata();
                }
            }
        }

        /// <summary>
        /// Retrieves pinned items, sorted for consistency.
        /// </summary>
        /// <returns>Sorted list of pinned items.</returns>
        public List<string> GetPinnedItems()
        {
            lock (_syncLock)
            {
                return metadata.PinnedItems.OrderBy(item => item).ToList();
            }
        }

        #endregion

        #region Recent Items Methods

        /// <summary>
        /// Adds a new recent item, keeping the list at max length 10.
        /// </summary>
        /// <param name="itemPath">Path to the item to add to recent items.</param>
        public void AddRecentItem(string itemPath)
        {
            lock (_syncLock)
            {
                // Remove the item if it already exists in the list
                if (metadata.RecentItems.Contains(itemPath))
                {
                    metadata.RecentItems.Remove(itemPath);
                }
                
                // Insert at the beginning of the list
                metadata.RecentItems.Insert(0, itemPath);
                
                // Trim the list to max items
                metadata.RecentItems = metadata.RecentItems.Take(_maxRecentItems).ToList();
                
                SaveMetadata();
            }
        }

        /// <summary>
        /// Gets the list of recent items.
        /// </summary>
        /// <returns>List of recent items.</returns>
        public List<string> GetRecentItems()
        {
            lock (_syncLock)
            {
                return new List<string>(metadata.RecentItems);
            }
        }

        #endregion

        #region Tagging Methods

        /// <summary>
        /// Overwrites all tags for a specific item with a new list.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <param name="tags">List of tags to set.</param>
        public void SetTags(string itemPath, List<string> tags)
        {
            lock (_syncLock)
            {
                metadata.Tags[itemPath] = tags;
                SaveMetadata();
            }
        }

        /// <summary>
        /// Adds a single tag to an item.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <param name="tag">Tag to add.</param>
        public void AddTag(string itemPath, string tag)
        {
            lock (_syncLock)
            {
                if (!metadata.Tags.ContainsKey(itemPath))
                {
                    metadata.Tags[itemPath] = new List<string>();
                }
                
                if (!metadata.Tags[itemPath].Contains(tag))
                {
                    metadata.Tags[itemPath].Add(tag);
                    SaveMetadata();
                }
            }
        }

        /// <summary>
        /// Removes a tag from an item.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <param name="tag">Tag to remove.</param>
        public void RemoveTag(string itemPath, string tag)
        {
            lock (_syncLock)
            {
                if (metadata.Tags.ContainsKey(itemPath) && metadata.Tags[itemPath].Contains(tag))
                {
                    metadata.Tags[itemPath].Remove(tag);
                    SaveMetadata();
                }
            }
        }

        /// <summary>
        /// Retrieves all tags for a given item path.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <returns>List of tags for the item.</returns>
        public List<string> GetTags(string itemPath)
        {
            lock (_syncLock)
            {
                return metadata.Tags.TryGetValue(itemPath, out List<string> tags) 
                    ? new List<string>(tags) 
                    : new List<string>();
            }
        }

        /// <summary>
        /// Returns a list of all items that have the given tag.
        /// </summary>
        /// <param name="tag">Tag to search for.</param>
        /// <returns>List of items with the specified tag.</returns>
        public List<string> GetItemsWithTag(string tag)
        {
            lock (_syncLock)
            {
                List<string> matchingItems = new List<string>();
                
                foreach (var kvp in metadata.Tags)
                {
                    if (kvp.Value.Contains(tag))
                    {
                        matchingItems.Add(kvp.Key);
                    }
                }
                
                return matchingItems;
            }
        }

        #endregion

        #region Last Accessed Methods

        /// <summary>
        /// Sets the last accessed time for a file/folder if it exists on disk.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        public void SetLastAccessed(string itemPath)
        {
            lock (_syncLock)
            {
                if (File.Exists(itemPath) || Directory.Exists(itemPath))
                {
                    // Convert DateTime to Unix timestamp (seconds since epoch)
                    DateTime lastAccessTime = File.GetLastAccessTime(itemPath);
                    double unixTimestamp = (lastAccessTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    
                    metadata.LastAccessed[itemPath] = unixTimestamp;
                    SaveMetadata();
                }
            }
        }

        /// <summary>
        /// Retrieves the last accessed time for a file or folder.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <returns>Last accessed time as Unix timestamp, or null if not found.</returns>
        public double? GetLastAccessed(string itemPath)
        {
            lock (_syncLock)
            {
                return metadata.LastAccessed.TryGetValue(itemPath, out double timestamp) ? timestamp : (double?)null;
            }
        }

        #endregion

        #region IDisposable Implementation

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
                    // Stop and dispose the cleanup timer
                    _cleanupTimer?.Dispose();
                    
                    // Save any pending changes
                    SaveMetadata();
                    
                    // Clear the singleton instance if it's this instance
                    if (_instance == this)
                    {
                        lock (_lock)
                        {
                            if (_instance == this)
                            {
                                _instance = null;
                            }
                        }
                    }
                }
                
                _disposed = true;
            }
        }

        ~MetadataManager()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// Class to represent the structure of the metadata JSON file.
    /// </summary>
    public class MetadataStructure
    {
        [JsonProperty("pinned_items")]
        public List<string> PinnedItems { get; set; }

        [JsonProperty("recent_items")]
        public List<string> RecentItems { get; set; }

        [JsonProperty("tags")]
        public Dictionary<string, List<string>> Tags { get; set; }

        [JsonProperty("last_accessed")]
        public Dictionary<string, double> LastAccessed { get; set; }

        [JsonProperty("item_colors")]
        public Dictionary<string, string> ItemColors { get; set; }

        [JsonProperty("item_bold")]
        public Dictionary<string, bool> ItemBold { get; set; }

        [JsonProperty("recent_colors")]
        public List<string> RecentColors { get; set; }
    }

    /// <summary>
    /// Container class for metadata information about a single item
    /// </summary>
    public class MetadataInfo
    {
        public string Path { get; set; }
        public bool IsPinned { get; set; }
        public bool IsRecent { get; set; }
        public List<string> Tags { get; set; }
        public string Color { get; set; }
        public bool IsBold { get; set; }
        public double? LastAccessed { get; set; }
        
        public MetadataInfo()
        {
            Tags = new List<string>();
        }
    }
}