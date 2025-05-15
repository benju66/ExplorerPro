using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Manages metadata for files and folders including pinned items, recent items, tags, colors, and bold status.
    /// </summary>
    public class MetadataManager
    {
        private string metadataFile;
        private MetadataStructure metadata;
        
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
        }

        /// <summary>
        /// Loads metadata from the JSON file or creates a default structure if missing/corrupt.
        /// </summary>
        public void LoadMetadata()
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

        /// <summary>
        /// Saves the current metadata to the JSON file.
        /// </summary>
        public void SaveMetadata()
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
        
        /// <summary>
        /// Updates path references when a file or folder is renamed or moved.
        /// </summary>
        /// <param name="oldPath">Original path</param>
        /// <param name="newPath">New path</param>
        public void UpdatePathReferences(string oldPath, string newPath)
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
            return metadata.RecentColors;
        }

        /// <summary>
        /// Inserts a newly used color at the front of the list, removing duplicates, 
        /// and limits to 5 entries, then saves.
        /// </summary>
        /// <param name="colorHex">The color hex code to add.</param>
        public void AddRecentColor(string colorHex)
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

        /// <summary>
        /// Removes a single color from recent_colors, then saves.
        /// </summary>
        /// <param name="colorHex">The color hex code to remove.</param>
        public void RemoveRecentColor(string colorHex)
        {
            if (metadata.RecentColors.Contains(colorHex))
            {
                metadata.RecentColors.Remove(colorHex);
                SaveMetadata();
            }
        }

        /// <summary>
        /// Removes all recent colors at once, then saves.
        /// </summary>
        public void ClearRecentColors()
        {
            metadata.RecentColors.Clear();
            SaveMetadata();
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
            metadata.ItemColors[itemPath] = colorHex;
            SaveMetadata();
        }

        /// <summary>
        /// Retrieves a stored color hex for a path, or null if none set.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <returns>Color hex code or null if not found.</returns>
        public string GetItemColor(string itemPath)
        {
            return metadata.ItemColors.TryGetValue(itemPath, out string color) ? color : null;
        }

        /// <summary>
        /// Stores whether this path's text should be bold (True/False).
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <param name="boldFlag">Whether the item should be bold.</param>
        public void SetItemBold(string itemPath, bool boldFlag)
        {
            metadata.ItemBold[itemPath] = boldFlag;
            SaveMetadata();
        }

        /// <summary>
        /// Return True if a path is bold, else False.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <returns>True if the item is bold, otherwise False.</returns>
        public bool GetItemBold(string itemPath)
        {
            return metadata.ItemBold.TryGetValue(itemPath, out bool isBold) && isBold;
        }

        #endregion

        #region Pinned Items Methods

        /// <summary>
        /// Adds an item to the pinned items list if it doesn't already exist.
        /// </summary>
        /// <param name="itemPath">Path to the item to pin.</param>
        public void AddPinnedItem(string itemPath)
        {
            if (!metadata.PinnedItems.Contains(itemPath))
            {
                metadata.PinnedItems.Add(itemPath);
                SaveMetadata();
            }
        }

        /// <summary>
        /// Removes an item from the pinned items list.
        /// </summary>
        /// <param name="itemPath">Path to the item to unpin.</param>
        public void RemovePinnedItem(string itemPath)
        {
            if (metadata.PinnedItems.Contains(itemPath))
            {
                metadata.PinnedItems.Remove(itemPath);
                SaveMetadata();
            }
        }

        /// <summary>
        /// Retrieves pinned items, sorted for consistency.
        /// </summary>
        /// <returns>Sorted list of pinned items.</returns>
        public List<string> GetPinnedItems()
        {
            return metadata.PinnedItems.OrderBy(item => item).ToList();
        }

        #endregion

        #region Recent Items Methods

        /// <summary>
        /// Adds a new recent item, keeping the list at max length 10.
        /// </summary>
        /// <param name="itemPath">Path to the item to add to recent items.</param>
        public void AddRecentItem(string itemPath)
        {
            // Remove the item if it already exists in the list
            if (metadata.RecentItems.Contains(itemPath))
            {
                metadata.RecentItems.Remove(itemPath);
            }
            
            // Insert at the beginning of the list
            metadata.RecentItems.Insert(0, itemPath);
            
            // Trim the list to 10 items
            metadata.RecentItems = metadata.RecentItems.Take(10).ToList();
            
            SaveMetadata();
        }

        /// <summary>
        /// Gets the list of recent items.
        /// </summary>
        /// <returns>List of recent items.</returns>
        public List<string> GetRecentItems()
        {
            return metadata.RecentItems;
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
            metadata.Tags[itemPath] = tags;
            SaveMetadata();
        }

        /// <summary>
        /// Adds a single tag to an item.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <param name="tag">Tag to add.</param>
        public void AddTag(string itemPath, string tag)
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

        /// <summary>
        /// Removes a tag from an item.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <param name="tag">Tag to remove.</param>
        public void RemoveTag(string itemPath, string tag)
        {
            if (metadata.Tags.ContainsKey(itemPath) && metadata.Tags[itemPath].Contains(tag))
            {
                metadata.Tags[itemPath].Remove(tag);
                SaveMetadata();
            }
        }

        /// <summary>
        /// Retrieves all tags for a given item path.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <returns>List of tags for the item.</returns>
        public List<string> GetTags(string itemPath)
        {
            return metadata.Tags.TryGetValue(itemPath, out List<string> tags) ? tags : new List<string>();
        }

        /// <summary>
        /// Returns a list of all items that have the given tag.
        /// </summary>
        /// <param name="tag">Tag to search for.</param>
        /// <returns>List of items with the specified tag.</returns>
        public List<string> GetItemsWithTag(string tag)
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

        #endregion

        #region Last Accessed Methods

        /// <summary>
        /// Sets the last accessed time for a file/folder if it exists on disk.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        public void SetLastAccessed(string itemPath)
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

        /// <summary>
        /// Retrieves the last accessed time for a file or folder.
        /// </summary>
        /// <param name="itemPath">Path to the item.</param>
        /// <returns>Last accessed time as Unix timestamp, or null if not found.</returns>
        public double? GetLastAccessed(string itemPath)
        {
            return metadata.LastAccessed.TryGetValue(itemPath, out double timestamp) ? timestamp : (double?)null;
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
}