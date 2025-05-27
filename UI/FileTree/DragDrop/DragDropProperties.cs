// UI/FileTree/DragDrop/DragDropProperties.cs
using System.Windows;

namespace ExplorerPro.UI.FileTree.DragDrop
{
    /// <summary>
    /// Attached properties for drag and drop visual states
    /// </summary>
    public static class DragDropHelper
    {
        #region IsDropTarget Attached Property
        
        public static readonly DependencyProperty IsDropTargetProperty =
            DependencyProperty.RegisterAttached(
                "IsDropTarget",
                typeof(bool),
                typeof(DragDropHelper),
                new PropertyMetadata(false));
        
        public static bool GetIsDropTarget(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDropTargetProperty);
        }
        
        public static void SetIsDropTarget(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDropTargetProperty, value);
        }
        
        #endregion
        
        #region IsInvalidDropTarget Attached Property
        
        public static readonly DependencyProperty IsInvalidDropTargetProperty =
            DependencyProperty.RegisterAttached(
                "IsInvalidDropTarget",
                typeof(bool),
                typeof(DragDropHelper),
                new PropertyMetadata(false));
        
        public static bool GetIsInvalidDropTarget(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsInvalidDropTargetProperty);
        }
        
        public static void SetIsInvalidDropTarget(DependencyObject obj, bool value)
        {
            obj.SetValue(IsInvalidDropTargetProperty, value);
        }
        
        #endregion
        
        #region IsDragSource Attached Property
        
        public static readonly DependencyProperty IsDragSourceProperty =
            DependencyProperty.RegisterAttached(
                "IsDragSource",
                typeof(bool),
                typeof(DragDropHelper),
                new PropertyMetadata(false));
        
        public static bool GetIsDragSource(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDragSourceProperty);
        }
        
        public static void SetIsDragSource(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragSourceProperty, value);
        }
        
        #endregion
        
        #region DropPosition Attached Property
        
        public static readonly DependencyProperty DropPositionProperty =
            DependencyProperty.RegisterAttached(
                "DropPosition",
                typeof(DropPosition),
                typeof(DragDropHelper),
                new PropertyMetadata(DropPosition.None));
        
        public static DropPosition GetDropPosition(DependencyObject obj)
        {
            return (DropPosition)obj.GetValue(DropPositionProperty);
        }
        
        public static void SetDropPosition(DependencyObject obj, DropPosition value)
        {
            obj.SetValue(DropPositionProperty, value);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Indicates where an item will be dropped relative to the target
    /// </summary>
    public enum DropPosition
    {
        None,
        Before,
        On,
        After
    }
}