// UI/FileTree/DragDrop/DragDropHelper.cs - Fixed with proper naming
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
                new PropertyMetadata(false, OnIsDropTargetChanged));
        
        public static bool GetIsDropTarget(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDropTargetProperty);
        }
        
        public static void SetIsDropTarget(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDropTargetProperty, value);
        }
        
        private static void OnIsDropTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Force visual update when property changes
            if (d is UIElement element)
            {
                element.InvalidateVisual();
            }
        }
        
        #endregion
        
        #region IsInvalidDropTarget Attached Property
        
        public static readonly DependencyProperty IsInvalidDropTargetProperty =
            DependencyProperty.RegisterAttached(
                "IsInvalidDropTarget",
                typeof(bool),
                typeof(DragDropHelper),
                new PropertyMetadata(false, OnIsInvalidDropTargetChanged));
        
        public static bool GetIsInvalidDropTarget(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsInvalidDropTargetProperty);
        }
        
        public static void SetIsInvalidDropTarget(DependencyObject obj, bool value)
        {
            obj.SetValue(IsInvalidDropTargetProperty, value);
        }
        
        private static void OnIsInvalidDropTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Force visual update when property changes
            if (d is UIElement element)
            {
                element.InvalidateVisual();
            }
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
        
        #region DropEffect Attached Property
        
        public static readonly DependencyProperty DropEffectProperty =
            DependencyProperty.RegisterAttached(
                "DropEffect",
                typeof(DragDropEffects),
                typeof(DragDropHelper),
                new PropertyMetadata(DragDropEffects.None));
        
        public static DragDropEffects GetDropEffect(DependencyObject obj)
        {
            return (DragDropEffects)obj.GetValue(DropEffectProperty);
        }
        
        public static void SetDropEffect(DependencyObject obj, DragDropEffects value)
        {
            obj.SetValue(DropEffectProperty, value);
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