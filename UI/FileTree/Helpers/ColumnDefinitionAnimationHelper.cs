// UI/FileTree/Helpers/ColumnDefinitionAnimationHelper.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ExplorerPro.UI.FileTree.Helpers
{
    /// <summary>
    /// Helper class to enable animation of ColumnDefinition Width property
    /// </summary>
    public static class ColumnDefinitionAnimationHelper
    {
        #region Width Animation Attached Property
        
        public static readonly DependencyProperty WidthProperty =
            DependencyProperty.RegisterAttached("Width", typeof(double), typeof(ColumnDefinitionAnimationHelper),
                new PropertyMetadata(0.0, OnWidthChanged));
        
        public static double GetWidth(DependencyObject obj)
        {
            return (double)obj.GetValue(WidthProperty);
        }
        
        public static void SetWidth(DependencyObject obj, double value)
        {
            obj.SetValue(WidthProperty, value);
        }
        
        private static void OnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColumnDefinition column)
            {
                column.Width = new GridLength((double)e.NewValue, GridUnitType.Pixel);
            }
        }
        
        #endregion
        
        #region Animation Extension Methods
        
        /// <summary>
        /// Animates the width of a ColumnDefinition
        /// </summary>
        public static void AnimateWidth(this ColumnDefinition column, double toWidth, Duration duration, IEasingFunction easingFunction = null)
        {
            // Get current width
            double fromWidth = column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
            
            // Create animation
            var animation = new DoubleAnimation
            {
                From = fromWidth,
                To = toWidth,
                Duration = duration,
                EasingFunction = easingFunction ?? new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            // Apply animation using the helper property
            column.BeginAnimation(WidthProperty, animation);
        }
        
        /// <summary>
        /// Animates the width of a ColumnDefinition with a completed callback
        /// </summary>
        public static void AnimateWidth(this ColumnDefinition column, double toWidth, Duration duration, Action onCompleted, IEasingFunction easingFunction = null)
        {
            // Get current width
            double fromWidth = column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
            
            // Create animation
            var animation = new DoubleAnimation
            {
                From = fromWidth,
                To = toWidth,
                Duration = duration,
                EasingFunction = easingFunction ?? new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            // Add completed handler
            if (onCompleted != null)
            {
                animation.Completed += (s, e) => onCompleted();
            }
            
            // Apply animation using the helper property
            column.BeginAnimation(WidthProperty, animation);
        }
        
        /// <summary>
        /// Stops any running animation on the column
        /// </summary>
        public static void StopAnimation(this ColumnDefinition column)
        {
            column.BeginAnimation(WidthProperty, null);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Extension methods for smooth column animations
    /// </summary>
    public static class GridExtensions
    {
        /// <summary>
        /// Animates multiple columns simultaneously
        /// </summary>
        public static void AnimateColumns(this Grid grid, params (int columnIndex, double toWidth)[] columnAnimations)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(300));
            var easingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            
            foreach (var (columnIndex, toWidth) in columnAnimations)
            {
                if (columnIndex >= 0 && columnIndex < grid.ColumnDefinitions.Count)
                {
                    grid.ColumnDefinitions[columnIndex].AnimateWidth(toWidth, duration, easingFunction);
                }
            }
        }
        
        /// <summary>
        /// Animates all columns to specific widths with staggered timing
        /// </summary>
        public static void StaggerAnimateColumns(this Grid grid, double[] widths, int staggerDelayMs = 50)
        {
            var baseDuration = TimeSpan.FromMilliseconds(200);
            var easingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            
            for (int i = 0; i < Math.Min(widths.Length, grid.ColumnDefinitions.Count); i++)
            {
                var column = grid.ColumnDefinitions[i];
                var delay = TimeSpan.FromMilliseconds(i * staggerDelayMs);
                var toWidth = widths[i];
                
                // Create delayed animation
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = delay
                };
                
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    column.AnimateWidth(toWidth, new Duration(baseDuration), easingFunction);
                };
                
                timer.Start();
            }
        }
    }
}