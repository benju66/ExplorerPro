using System;
using System.Windows;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Information about a detached window
    /// </summary>
    public class DetachedWindowInfo
    {
        public Window Window { get; set; }
        public ChromeStyleTabControl TabControl { get; set; }
        public TabModel OriginalTab { get; set; }
        public Window SourceWindow { get; set; }
        public DateTime DetachedAt { get; set; }
        public Point InitialPosition { get; set; }
        
        /// <summary>
        /// Unique identifier for this detached window
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Whether this window can accept dropped tabs
        /// </summary>
        public bool CanAcceptDrops { get; set; } = true;
    }
} 
