using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Manages tab previews and hover functionality
    /// </summary>
    public class TabPreviewManager
    {
        private readonly ILogger<TabPreviewManager> _logger;
        private readonly TabStateManager _stateManager;
        private readonly Dictionary<string, TabPreview> _previewCache;
        private readonly object _lock = new object();
        private readonly int _maxCacheSize = 50;

        public TabPreviewManager(
            ILogger<TabPreviewManager> logger,
            TabStateManager stateManager)
        {
            _logger = logger;
            _stateManager = stateManager;
            _previewCache = new Dictionary<string, TabPreview>();
        }

        /// <summary>
        /// Get a preview for a tab
        /// </summary>
        public async Task<TabPreview?> GetPreviewAsync(string tabId)
        {
            try
            {
                // Check cache first
                lock (_lock)
                {
                    if (_previewCache.TryGetValue(tabId, out var cachedPreview))
                    {
                        return cachedPreview;
                    }
                }

                // Generate new preview
                var state = _stateManager.GetTabState(tabId);
                if (state == null)
                {
                    return null;
                }

                var preview = await GeneratePreviewAsync(state);
                if (preview != null)
                {
                    // Cache the preview
                    lock (_lock)
                    {
                        if (_previewCache.Count >= _maxCacheSize)
                        {
                            // Remove oldest preview
                            var oldestKey = _previewCache.Keys.First();
                            _previewCache.Remove(oldestKey);
                        }
                        _previewCache[tabId] = preview;
                    }
                }

                return preview;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting preview for tab {TabId}", tabId);
                return null;
            }
        }

        /// <summary>
        /// Clear the preview cache
        /// </summary>
        public void ClearCache()
        {
            lock (_lock)
            {
                _previewCache.Clear();
            }
        }

        /// <summary>
        /// Remove a preview from cache
        /// </summary>
        public void RemoveFromCache(string tabId)
        {
            lock (_lock)
            {
                _previewCache.Remove(tabId);
            }
        }

        /// <summary>
        /// Generate a preview for a tab state
        /// </summary>
        private async Task<TabPreview?> GeneratePreviewAsync(TabState state)
        {
            try
            {
                // This is where you would implement the actual preview generation
                // For example, for file tabs, you might generate a thumbnail
                // For web tabs, you might capture a screenshot
                // For now, we'll return a simple preview with basic info

                return new TabPreview
                {
                    Title = state.Title,
                    Path = state.Path,
                    LastAccessed = state.LastAccessed,
                    PreviewImage = null, // You would generate this based on the tab type
                    PreviewText = GeneratePreviewText(state)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating preview for tab {Title}", state.Title);
                return null;
            }
        }

        /// <summary>
        /// Generate preview text for a tab state
        /// </summary>
        private string GeneratePreviewText(TabState state)
        {
            var text = new List<string>();

            if (!string.IsNullOrEmpty(state.Title))
            {
                text.Add($"Title: {state.Title}");
            }

            if (!string.IsNullOrEmpty(state.Path))
            {
                text.Add($"Path: {state.Path}");
            }

            if (state.LastAccessed != default)
            {
                text.Add($"Last accessed: {state.LastAccessed:g}");
            }

            if (state.IsPinned)
            {
                text.Add("Pinned");
            }

            if (state.IsHibernated)
            {
                text.Add("Hibernated");
            }

            return string.Join("\n", text);
        }
    }

    /// <summary>
    /// Represents a tab preview
    /// </summary>
    public class TabPreview
    {
        public string Title { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime LastAccessed { get; set; }
        public BitmapSource? PreviewImage { get; set; }
        public string PreviewText { get; set; } = string.Empty;
    }
} 