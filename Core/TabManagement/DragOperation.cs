using System.Windows;
using System.Windows.Controls;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Represents an active drag operation
    /// </summary>
    public class DragOperation
    {
        public TabItemModel Tab { get; set; }
        public Window SourceWindow { get; set; }
        public ChromeStyleTabControl SourceTabControl { get; set; }
        public TabItem DraggedTabItem { get; set; }
        public Point StartPoint { get; set; }
        public Point Offset { get; set; }
        public int OriginalIndex { get; set; }
        public bool IsActive { get; set; }
        public DragOperationType CurrentOperationType { get; set; }
        
        /// <summary>
        /// Distance dragged from start point
        /// </summary>
        public Vector DragDistance => Point.Subtract(CurrentPoint, StartPoint);
        
        /// <summary>
        /// Current mouse position
        /// </summary>
        public Point CurrentPoint { get; set; }
        
        /// <summary>
        /// Whether the tab has been torn off
        /// </summary>
        public bool IsTornOff { get; set; }
    }
} 