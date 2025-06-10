using System;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Represents a single navigation history entry for tab-level navigation.
    /// Includes memory size calculation for bounded history management.
    /// </summary>
    [Serializable]
    public class NavigationHistoryItem
    {
        public string Path { get; set; }
        public DateTime NavigatedAt { get; set; }
        public string Title { get; set; }
        
        /// <summary>
        /// Calculates approximate memory usage of this navigation item
        /// </summary>
        public long MemorySize => 
            (Path?.Length ?? 0) * 2 + // Unicode string
            (Title?.Length ?? 0) * 2 + // Unicode string  
            16; // DateTime + object overhead
        
        public NavigationHistoryItem(string path, string title = null)
        {
            Path = path ?? string.Empty;
            Title = title ?? System.IO.Path.GetFileName(path) ?? string.Empty;
            NavigatedAt = DateTime.Now;
        }
        
        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        public NavigationHistoryItem()
        {
            Path = string.Empty;
            Title = string.Empty;
            NavigatedAt = DateTime.Now;
        }
        
        public override string ToString()
        {
            return $"{Title} ({Path}) - {NavigatedAt:HH:mm:ss}";
        }
        
        public override bool Equals(object obj)
        {
            if (obj is NavigationHistoryItem other)
            {
                return string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            return Path?.ToLowerInvariant().GetHashCode() ?? 0;
        }
    }
} 