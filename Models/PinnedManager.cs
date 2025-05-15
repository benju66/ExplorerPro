using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Manages pinned items (and optional favorites) across all main windows and tabs.
    /// </summary>
    public class PinnedManager
    {
        #region Events

        /// <summary>
        /// Event raised when pinned items are updated.
        /// </summary>
        public event EventHandler? PinnedItemsUpdated;

        #endregion

        #region Fields

        private readonly ILogger<PinnedManager>? _logger;
        private HashSet<string> _pinnedItems;
        private HashSet<string> _favoriteItems;
        private readonly string _pinnedFile;

        #endregion

        #region Singleton Implementation

        private static PinnedManager? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the singleton instance of the PinnedManager.
        /// </summary>
        public static PinnedManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            string pinnedFilePath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "ExplorerPro", "Data", "pinned_items.json");
                                
                            _instance = new PinnedManager(null, pinnedFilePath);
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new PinnedManager instance with the specified file path.
        /// </summary>
        /// <param name="pinnedFilePath">Path to the pinned items JSON file</param>
        public PinnedManager(string pinnedFilePath)
        {
            _pinnedItems = new HashSet<string>();
            _favoriteItems = new HashSet<string>();
            _pinnedFile = pinnedFilePath;
            
            // Create a logger factory without console logging to avoid the error
            var loggerFactory = LoggerFactory.Create(builder => { });
            _logger = loggerFactory.CreateLogger<PinnedManager>();
            
            // Load any existing pinned items
            LoadPinnedItems();
        }

        /// <summary>
        /// Creates a new PinnedManager instance with the specified logger and file path.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="pinnedFilePath">Path to the pinned items JSON file</param>
        public PinnedManager(ILogger<PinnedManager>? logger, string pinnedFilePath)
        {
            _pinnedItems = new HashSet<string>();
            _favoriteItems = new HashSet<string>();
            _pinnedFile = pinnedFilePath;
            _logger = logger;
            
            // Load any existing pinned items
            LoadPinnedItems();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the path to the pinned items file.
        /// </summary>
        public string PinnedFilePath => _pinnedFile;

        /// <summary>
        /// Gets the list of pinned items.
        /// </summary>
        public IReadOnlyCollection<string> PinnedItems => _pinnedItems.ToList().AsReadOnly();

        /// <summary>
        /// Gets the list of favorite items.
        /// </summary>
        public IReadOnlyCollection<string> FavoriteItems => _favoriteItems.ToList().AsReadOnly();

        #endregion

        #region Public Methods

        /// <summary>
        /// Load pinned items and favorites from a JSON file.
        /// </summary>
        public void LoadPinnedItems()
        {
            if (!File.Exists(_pinnedFile))
            {
                _logger?.LogDebug($"Pinned file does not exist: {_pinnedFile}");
                return;
            }

            try
            {
                string json = File.ReadAllText(_pinnedFile);
                JToken data = JToken.Parse(json);

                // If data is just an array, it's the old format: all are pinned, no favorites
                if (data is JArray array)
                {
                    var pinnedList = array.ToObject<List<string>>();
                    _pinnedItems = pinnedList != null ? new HashSet<string>(pinnedList) : new HashSet<string>();
                    _favoriteItems = new HashSet<string>();
                }
                else if (data is JObject obj)
                {
                    var pinnedList = obj["pinned"]?.ToObject<List<string>>();
                    var favoritesList = obj["favorites"]?.ToObject<List<string>>();
                    _pinnedItems = pinnedList != null ? new HashSet<string>(pinnedList) : new HashSet<string>();
                    _favoriteItems = favoritesList != null ? new HashSet<string>(favoritesList) : new HashSet<string>();
                }
                else
                {
                    _logger?.LogWarning("Unexpected data format in pinned file, ignoring.");
                }

                _logger?.LogDebug($"Loaded pinned items: {string.Join(", ", _pinnedItems)}");
                _logger?.LogDebug($"Loaded favorite items: {string.Join(", ", _favoriteItems)}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to load pinned items from {_pinnedFile}");
            }
        }

        /// <summary>
        /// Persist pinned items and favorites to a JSON file.
        /// </summary>
        public void SavePinnedItems()
        {
            string? directoryName = Path.GetDirectoryName(_pinnedFile);
            if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            try
            {
                var data = new
                {
                    pinned = _pinnedItems.ToList(),
                    favorites = _favoriteItems.ToList()
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_pinnedFile, json);
                _logger?.LogDebug("Successfully saved pinned & favorite items.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to save pinned items to {_pinnedFile}");
            }
        }

        /// <summary>
        /// Add a new pinned item, save, and raise an update event.
        /// </summary>
        /// <param name="itemPath">Path to pin</param>
        public void AddPinnedItem(string itemPath)
        {
            if (string.IsNullOrWhiteSpace(itemPath))
            {
                _logger?.LogWarning("Cannot pin null or empty path");
                return;
            }

            if (!File.Exists(itemPath) && !Directory.Exists(itemPath))
            {
                _logger?.LogWarning($"Cannot pin non-existent path: {itemPath}");
                return;
            }

            if (!_pinnedItems.Contains(itemPath))
            {
                _pinnedItems.Add(itemPath);
                SavePinnedItems();
                PinnedItemsUpdated?.Invoke(this, EventArgs.Empty);
                _logger?.LogDebug($"Pinned item: {itemPath}");
            }
            else
            {
                _logger?.LogDebug($"Item already pinned: {itemPath}");
            }
        }

        /// <summary>
        /// Remove a pinned item, and if it's a favorite, remove it there too. Then save and raise event.
        /// </summary>
        /// <param name="itemPath">Path to unpin</param>
        public void RemovePinnedItem(string itemPath)
        {
            if (string.IsNullOrWhiteSpace(itemPath))
            {
                _logger?.LogWarning("Cannot unpin null or empty path");
                return;
            }

            if (_pinnedItems.Contains(itemPath))
            {
                _pinnedItems.Remove(itemPath);

                // Also remove it from favorites if present
                if (_favoriteItems.Contains(itemPath))
                {
                    _favoriteItems.Remove(itemPath);
                }

                SavePinnedItems();
                PinnedItemsUpdated?.Invoke(this, EventArgs.Empty);
                _logger?.LogDebug($"Unpinned item: {itemPath}");
            }
            else
            {
                _logger?.LogDebug($"Cannot unpin; item not found: {itemPath}");
            }
        }

        /// <summary>
        /// Return the pinned items as a list.
        /// </summary>
        /// <returns>List of pinned items</returns>
        public List<string> GetPinnedItems()
        {
            return _pinnedItems.ToList();
        }

        /// <summary>
        /// Check if a path is pinned.
        /// </summary>
        /// <param name="itemPath">Path to check</param>
        /// <returns>True if the path is pinned, false otherwise</returns>
        public bool IsPinned(string itemPath)
        {
            if (string.IsNullOrWhiteSpace(itemPath))
                return false;
                
            return _pinnedItems.Contains(itemPath);
        }
        
        /// <summary>
        /// Check if a path exists in pinned items.
        /// </summary>
        /// <param name="itemPath">Path to check</param>
        /// <returns>True if the path exists in pinned items, false otherwise</returns>
        public bool HasPinnedItem(string itemPath)
        {
            if (string.IsNullOrWhiteSpace(itemPath))
                return false;
                
            return _pinnedItems.Contains(itemPath);
        }
        
        /// <summary>
        /// Updates path references when a file or folder is renamed or moved.
        /// </summary>
        /// <param name="oldPath">Original path</param>
        /// <param name="newPath">New path</param>
        public void UpdatePathReferences(string oldPath, string newPath)
        {
            if (string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath))
            {
                _logger?.LogWarning("Cannot update path references with null or empty paths");
                return;
            }
            
            bool updated = false;
            
            // Update pinned items
            if (_pinnedItems.Contains(oldPath))
            {
                _pinnedItems.Remove(oldPath);
                _pinnedItems.Add(newPath);
                updated = true;
            }
            
            // Update favorites
            if (_favoriteItems.Contains(oldPath))
            {
                _favoriteItems.Remove(oldPath);
                _favoriteItems.Add(newPath);
                updated = true;
            }
            
            if (updated)
            {
                SavePinnedItems();
                PinnedItemsUpdated?.Invoke(this, EventArgs.Empty);
                _logger?.LogDebug($"Updated path references from {oldPath} to {newPath}");
            }
        }

        #endregion

        #region Favorites Handling

        /// <summary>
        /// Mark a pinned item as a favorite. If the item isn't pinned, automatically pin it first.
        /// </summary>
        /// <param name="itemPath">Path to favorite</param>
        public void FavoriteItem(string itemPath)
        {
            if (string.IsNullOrWhiteSpace(itemPath))
            {
                _logger?.LogWarning("Cannot favorite null or empty path");
                return;
            }

            if (!File.Exists(itemPath) && !Directory.Exists(itemPath))
            {
                _logger?.LogWarning($"Cannot favorite non-existent path: {itemPath}");
                return;
            }

            // Auto-pin if not already pinned
            if (!_pinnedItems.Contains(itemPath))
            {
                AddPinnedItem(itemPath);
            }

            if (!_favoriteItems.Contains(itemPath))
            {
                _favoriteItems.Add(itemPath);
                SavePinnedItems();
                PinnedItemsUpdated?.Invoke(this, EventArgs.Empty);
                _logger?.LogDebug($"Favorited item: {itemPath}");
            }
            else
            {
                _logger?.LogDebug($"Item already a favorite: {itemPath}");
            }
        }

        /// <summary>
        /// Remove from favorites only; remains pinned unless unpinned separately.
        /// </summary>
        /// <param name="itemPath">Path to unfavorite</param>
        public void UnfavoriteItem(string itemPath)
        {
            if (string.IsNullOrWhiteSpace(itemPath))
            {
                _logger?.LogWarning("Cannot unfavorite null or empty path");
                return;
            }

            if (_favoriteItems.Contains(itemPath))
            {
                _favoriteItems.Remove(itemPath);
                SavePinnedItems();
                PinnedItemsUpdated?.Invoke(this, EventArgs.Empty);
                _logger?.LogDebug($"Unfavorited item: {itemPath}");
            }
            else
            {
                _logger?.LogDebug($"Cannot unfavorite; item not in favorites: {itemPath}");
            }
        }

        /// <summary>
        /// Return the favorite items as a list.
        /// </summary>
        /// <returns>List of favorite items</returns>
        public List<string> GetFavoriteItems()
        {
            return _favoriteItems.ToList();
        }

        /// <summary>
        /// Check if a path is in favorites.
        /// </summary>
        /// <param name="itemPath">Path to check</param>
        /// <returns>True if the path is a favorite, false otherwise</returns>
        public bool IsFavorite(string itemPath)
        {
            if (string.IsNullOrWhiteSpace(itemPath))
                return false;
                
            return _favoriteItems.Contains(itemPath);
        }

        #endregion
    }
}