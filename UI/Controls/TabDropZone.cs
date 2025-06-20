using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Visual indicator for tab drop zones
    /// </summary>
    public class TabDropZone : Control
    {
        static TabDropZone()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(TabDropZone), 
                new FrameworkPropertyMetadata(typeof(TabDropZone)));
        }

        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(
                nameof(IsActive), 
                typeof(bool), 
                typeof(TabDropZone),
                new PropertyMetadata(false, OnIsActiveChanged));

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TabDropZone zone)
            {
                zone.UpdateVisualState((bool)e.NewValue);
            }
        }

        private void UpdateVisualState(bool isActive)
        {
            if (isActive)
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
                BeginAnimation(OpacityProperty, fadeIn);
            }
            else
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                BeginAnimation(OpacityProperty, fadeOut);
            }
        }
    }
} 