// UI/TabManagement/DraggableTabBar.cs

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ExplorerPro.UI.PaneManagement;

namespace ExplorerPro.UI.TabManagement
{
    /// <summary>
    /// A custom TabPanel implementation that allows for drag-and-drop functionality to detach tabs.
    /// Works in conjunction with PaneManager to enable pane detachment and reattachment.
    /// </summary>
    public class DraggableTabBar : TabPanel
    {
        // Point where drag operation started
        private Point? dragStartPosition;
        
        // The tab item being dragged
        private TabItem draggedTabItem;
        
        // Threshold in pixels for initiating drag operation
        private const double DragThreshold = 10.0;
        
        /// <summary>
        /// Event raised when a pane is detached via drag operation.
        /// </summary>
        public event EventHandler<PaneDetachEventArgs> PaneDetached;
        
        public DraggableTabBar() : base()
        {
            // Enable drop functionality
            AllowDrop = true;
        }
        
        /// <summary>
        /// Handles mouse button down events to initialize drag operations
        /// </summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            
            // Record starting position for potential drag operation
            dragStartPosition = e.GetPosition(this);
            
            // Determine which tab was clicked
            draggedTabItem = FindTabItem(dragStartPosition.Value);
            
            // Capture mouse to receive events even if cursor moves outside control
            CaptureMouse();
        }
        
        /// <summary>
        /// Handles mouse movement to initiate drag operations
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            // Only proceed if we have a valid drag start position and the mouse button is pressed
            if (dragStartPosition.HasValue && e.LeftButton == MouseButtonState.Pressed && draggedTabItem != null)
            {
                // Calculate distance moved
                Point currentPosition = e.GetPosition(this);
                Vector dragVector = currentPosition - dragStartPosition.Value;
                double dragDistance = Math.Sqrt(Math.Pow(dragVector.X, 2) + Math.Pow(dragVector.Y, 2));
                
                // If moved beyond threshold, start drag operation
                if (dragDistance > DragThreshold)
                {
                    StartDrag();
                }
            }
        }
        
        /// <summary>
        /// Handles mouse button release events
        /// </summary>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            
            // Reset drag tracking
            dragStartPosition = null;
            draggedTabItem = null;
            
            // Release mouse capture
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
        }
        
        /// <summary>
        /// Initiates the drag operation
        /// </summary>
        private void StartDrag()
        {
            // Release mouse capture so other controls can receive mouse events during drag
            ReleaseMouseCapture();
            
            // Create data object for drag operation
            DataObject dragData = new DataObject();
            
            // Store tab item in the drag data
            dragData.SetData("TabItem", draggedTabItem);
            
            try
            {
                // Begin drag-drop operation
                DragDropEffects result = DragDrop.DoDragDrop(draggedTabItem, dragData, DragDropEffects.Move);
                
                // If the drag operation completed successfully and resulted in a "Move"
                if (result == DragDropEffects.Move)
                {
                    // Raise the PaneDetached event to notify parent controls
                    OnPaneDetached(draggedTabItem);
                }
            }
            finally
            {
                // Clear drag tracking state
                dragStartPosition = null;
                draggedTabItem = null;
            }
        }
        
        /// <summary>
        /// Raises the PaneDetached event
        /// </summary>
        protected virtual void OnPaneDetached(TabItem detachedPane)
        {
            // Find the source PaneManager
            PaneManager sourceManager = FindSourcePaneManager(detachedPane);
            PaneDetached?.Invoke(this, new PaneDetachEventArgs(detachedPane, sourceManager));
        }
        
        /// <summary>
        /// Finds the PaneManager that contains this draggable tab bar
        /// </summary>
        private PaneManager FindSourcePaneManager(TabItem tabItem)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is PaneManager paneManager)
                {
                    return paneManager;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
        
        /// <summary>
        /// Finds the TabItem at the specified position
        /// </summary>
        private TabItem FindTabItem(Point position)
        {
            // Use hit testing to find the tab at the specified position
            HitTestResult result = VisualTreeHelper.HitTest(this, position);
            if (result != null)
            {
                DependencyObject element = result.VisualHit;
                
                // Walk up the visual tree to find the TabItem
                while (element != null && !(element is TabItem))
                {
                    element = VisualTreeHelper.GetParent(element);
                }
                
                return element as TabItem;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets the index of the specified TabItem within its TabControl
        /// </summary>
        private int GetTabItemIndex(TabItem tabItem)
        {
            if (tabItem == null)
                return -1;
                
            // Find parent TabControl
            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null && !(parent is TabControl))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            if (parent is TabControl tabControl)
            {
                return tabControl.Items.IndexOf(tabItem);
            }
            
            return -1;
        }
    }
    
    /// <summary>
    /// Event arguments for the PaneDetached event
    /// </summary>
    public class PaneDetachEventArgs : EventArgs
    {
        /// <summary>
        /// The TabItem representing the detached pane
        /// </summary>
        public TabItem DetachedPane { get; set; }
        
        /// <summary>
        /// The PaneManager that the pane was detached from
        /// </summary>
        public PaneManager SourceManager { get; set; }
        
        public PaneDetachEventArgs(TabItem pane, PaneManager source)
        {
            DetachedPane = pane;
            SourceManager = source;
        }
    }
}