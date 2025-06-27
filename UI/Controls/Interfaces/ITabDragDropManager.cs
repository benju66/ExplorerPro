using System;
using System.Windows;
using System.Windows.Controls;
using ExplorerPro.Models;

namespace ExplorerPro.UI.Controls.Interfaces
{
    /// <summary>
    /// Interface for managing tab drag and drop operations.
    /// Provides enterprise-level drag-drop functionality with proper separation of concerns.
    /// </summary>
    public interface ITabDragDropManager : IDisposable
    {
        #region Events
        
        /// <summary>
        /// Fired when a tab drag operation starts
        /// </summary>
        event EventHandler<TabDragEventArgs> DragStarted;
        
        /// <summary>
        /// Fired during tab dragging
        /// </summary>
        event EventHandler<TabDragEventArgs> Dragging;
        
        /// <summary>
        /// Fired when a tab drag operation completes
        /// </summary>
        event EventHandler<TabDragEventArgs> DragCompleted;
        
        /// <summary>
        /// Fired when a tab reorder is requested
        /// </summary>
        event EventHandler<TabReorderRequestedEventArgs> ReorderRequested;
        
        /// <summary>
        /// Fired when a tab detach is requested
        /// </summary>
        event EventHandler<TabDetachRequestedEventArgs> DetachRequested;
        
        #endregion

        #region Properties
        
        /// <summary>
        /// Whether a drag operation is currently active
        /// </summary>
        bool IsDragging { get; }
        
        /// <summary>
        /// Currently dragged tab (if any)
        /// </summary>
        TabModel DraggedTab { get; }
        
        /// <summary>
        /// Drag threshold before starting drag operation
        /// </summary>
        double DragThreshold { get; set; }
        
        /// <summary>
        /// Threshold for tab detachment
        /// </summary>
        double DetachThreshold { get; set; }
        
        #endregion

        #region Core Operations
        
        /// <summary>
        /// Initializes the drag-drop manager with the parent tab control
        /// </summary>
        void Initialize(TabControl tabControl);
        
        /// <summary>
        /// Starts a drag operation for the specified tab
        /// </summary>
        bool StartDrag(TabModel tab, Point startPoint);
        
        /// <summary>
        /// Updates the current drag operation
        /// </summary>
        void UpdateDrag(Point currentPoint);
        
        /// <summary>
        /// Completes the current drag operation
        /// </summary>
        bool CompleteDrag(Point endPoint);
        
        /// <summary>
        /// Cancels the current drag operation
        /// </summary>
        void CancelDrag();
        
        /// <summary>
        /// Determines if a point is valid for starting a drag
        /// </summary>
        bool CanStartDrag(Point point);
        
        /// <summary>
        /// Determines the drag operation type based on current position
        /// </summary>
        DragOperationType GetDragOperationType(Point point);
        
        #endregion

        #region Visual Feedback
        
        /// <summary>
        /// Shows visual feedback for the current drag operation
        /// </summary>
        void ShowDragFeedback(DragOperationType operationType);
        
        /// <summary>
        /// Hides all drag visual feedback
        /// </summary>
        void HideDragFeedback();
        
        /// <summary>
        /// Updates the insertion indicator position
        /// </summary>
        void UpdateInsertionIndicator(Point position, int insertIndex);
        
        #endregion
    }

    /// <summary>
    /// Event arguments for tab reorder requests
    /// </summary>
    public class TabReorderRequestedEventArgs : EventArgs
    {
        public TabModel Tab { get; }
        public int FromIndex { get; }
        public int ToIndex { get; }
        public bool Cancel { get; set; }

        public TabReorderRequestedEventArgs(TabModel tab, int fromIndex, int toIndex)
        {
            Tab = tab;
            FromIndex = fromIndex;
            ToIndex = toIndex;
        }
    }

    /// <summary>
    /// Event arguments for tab detach requests
    /// </summary>
    public class TabDetachRequestedEventArgs : EventArgs
    {
        public TabModel Tab { get; }
        public Point DetachPoint { get; }
        public bool Cancel { get; set; }

        public TabDetachRequestedEventArgs(TabModel tab, Point detachPoint)
        {
            Tab = tab;
            DetachPoint = detachPoint;
        }
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