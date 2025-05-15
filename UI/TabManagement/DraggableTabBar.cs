// UI/TabManagement/DraggableTabBar.cs

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ExplorerPro.UI.TabManagement
{
    /// <summary>
    /// A custom TabPanel implementation that allows for drag-and-drop functionality to detach tabs.
    /// Works in conjunction with TabManager to enable tab detachment and reattachment.
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
        /// Event raised when a tab is detached via drag operation.
        /// </summary>
        public event EventHandler<TabDetachEventArgs> TabDetached;
        
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
            
            // Store tab index in the drag data
            int tabIndex = GetTabItemIndex(draggedTabItem);
            dragData.SetData("TabIndex", tabIndex);
            
            try
            {
                // Begin drag-drop operation
                DragDropEffects result = DragDrop.DoDragDrop(draggedTabItem, dragData, DragDropEffects.Move);
                
                // If the drag operation completed successfully and resulted in a "Move"
                if (result == DragDropEffects.Move)
                {
                    // Raise the TabDetached event to notify parent controls
                    OnTabDetached(tabIndex);
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
        /// Raises the TabDetached event
        /// </summary>
        protected virtual void OnTabDetached(int tabIndex)
        {
            TabDetached?.Invoke(this, new TabDetachEventArgs(tabIndex));
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
    /// Event arguments for the TabDetached event
    /// </summary>
    public class TabDetachEventArgs : EventArgs
    {
        /// <summary>
        /// The index of the tab that was detached
        /// </summary>
        public int TabIndex { get; }
        
        public TabDetachEventArgs(int tabIndex)
        {
            TabIndex = tabIndex;
        }
    }
}