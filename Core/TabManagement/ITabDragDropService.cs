using System.Windows;
using ExplorerPro.Models;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Service interface for tab drag and drop operations
    /// </summary>
    public interface ITabDragDropService
    {
        /// <summary>
        /// Initiates a drag operation for a tab
        /// </summary>
        void StartDrag(TabItemModel tab, Point startPoint, Window sourceWindow);

        /// <summary>
        /// Updates the current drag operation
        /// </summary>
        void UpdateDrag(Point currentPoint);

        /// <summary>
        /// Completes the drag operation
        /// </summary>
        bool CompleteDrag(Window targetWindow, Point dropPoint);

        /// <summary>
        /// Cancels the current drag operation
        /// </summary>
        void CancelDrag();

        /// <summary>
        /// Determines if a drop is valid at the specified location
        /// </summary>
        bool CanDrop(Window targetWindow, Point dropPoint);

        /// <summary>
        /// Gets the current drag state
        /// </summary>
        bool IsDragging { get; }

        /// <summary>
        /// Gets the type of operation that will occur at current position
        /// </summary>
        DragOperationType GetOperationType(Point currentPoint);
    }

    /// <summary>
    /// Types of drag operations
    /// </summary>
    public enum DragOperationType
    {
        None,
        Reorder,
        Detach,
        Transfer
    }
} 