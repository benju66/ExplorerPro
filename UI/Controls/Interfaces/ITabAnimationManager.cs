using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using ExplorerPro.Models;

namespace ExplorerPro.UI.Controls.Interfaces
{
    /// <summary>
    /// Interface for managing tab animations and visual effects.
    /// Provides smooth, modern animations with proper performance management.
    /// </summary>
    public interface ITabAnimationManager : IDisposable
    {
        #region Properties
        
        /// <summary>
        /// Whether animations are currently enabled
        /// </summary>
        bool AnimationsEnabled { get; set; }
        
        /// <summary>
        /// Default animation duration in milliseconds
        /// </summary>
        double DefaultDuration { get; set; }
        
        /// <summary>
        /// Whether an animation is currently running
        /// </summary>
        bool IsAnimating { get; }
        
        #endregion

        #region Tab Lifecycle Animations
        
        /// <summary>
        /// Animates a new tab appearing
        /// </summary>
        Task AnimateTabCreationAsync(TabItem tabItem);
        
        /// <summary>
        /// Animates a tab being removed
        /// </summary>
        Task AnimateTabClosingAsync(TabItem tabItem);
        
        /// <summary>
        /// Animates tab activation/selection
        /// </summary>
        Task AnimateTabActivationAsync(TabItem tabItem, TabItem previousTab = null);
        
        #endregion

        #region Drag Animations
        
        /// <summary>
        /// Animates the start of a drag operation
        /// </summary>
        void AnimateDragStart(TabItem tabItem);
        
        /// <summary>
        /// Animates during drag operation
        /// </summary>
        void AnimateDragProgress(TabItem tabItem, DragOperationType operationType);
        
        /// <summary>
        /// Animates the end of a drag operation
        /// </summary>
        Task AnimateDragEndAsync(TabItem tabItem, bool success);
        
        /// <summary>
        /// Animates tab width changes with Chrome-style smoothness
        /// </summary>
        Task AnimateTabWidthChangeAsync(TabItem tabItem, double fromWidth, double toWidth);
        
        /// <summary>
        /// Animates tab reordering
        /// </summary>
        Task AnimateTabReorderAsync(TabItem tabItem, int fromIndex, int toIndex);
        
        #endregion

        #region Visual State Animations
        
        /// <summary>
        /// Animates hover state changes
        /// </summary>
        void AnimateHoverState(TabItem tabItem, bool isHovering);
        
        /// <summary>
        /// Animates focus state changes
        /// </summary>
        void AnimateFocusState(TabItem tabItem, bool hasFocus);
        
        /// <summary>
        /// Animates pinned state changes
        /// </summary>
        Task AnimatePinnedStateAsync(TabItem tabItem, bool isPinned);
        
        /// <summary>
        /// Animates color changes
        /// </summary>
        Task AnimateColorChangeAsync(TabItem tabItem, System.Windows.Media.Color? newColor);
        
        #endregion

        #region Feedback Animations
        
        /// <summary>
        /// Plays a success feedback animation
        /// </summary>
        Task PlaySuccessAnimationAsync(TabItem tabItem);
        
        /// <summary>
        /// Plays an error feedback animation
        /// </summary>
        Task PlayErrorAnimationAsync(TabItem tabItem);
        
        /// <summary>
        /// Plays a snap-to-position animation
        /// </summary>
        Task PlaySnapAnimationAsync(TabItem tabItem, Point targetPosition);
        
        /// <summary>
        /// Plays a bounce animation
        /// </summary>
        Task PlayBounceAnimationAsync(TabItem tabItem);
        
        #endregion

        #region Animation Control
        
        /// <summary>
        /// Stops all current animations
        /// </summary>
        void StopAllAnimations();
        
        /// <summary>
        /// Stops animations for a specific tab
        /// </summary>
        void StopTabAnimations(TabItem tabItem);
        
        /// <summary>
        /// Gets the current animation for a tab
        /// </summary>
        Storyboard GetCurrentAnimation(TabItem tabItem);
        
        /// <summary>
        /// Sets a custom easing function for animations
        /// </summary>
        void SetEasingFunction(IEasingFunction easingFunction);
        
        #endregion
    }

    /// <summary>
    /// Animation timing presets
    /// </summary>
    public static class AnimationTimings
    {
        public const double Fast = 150.0;
        public const double Normal = 250.0;
        public const double Slow = 400.0;
        public const double VeryFast = 100.0;
        public const double VerySlow = 600.0;
    }

    /// <summary>
    /// Animation configuration options
    /// </summary>
    public class AnimationOptions
    {
        public double Duration { get; set; } = AnimationTimings.Normal;
        public IEasingFunction EasingFunction { get; set; }
        public bool FadeEffect { get; set; } = true;
        public bool ScaleEffect { get; set; } = false;
        public bool SlideEffect { get; set; } = false;
        public double DelayBefore { get; set; } = 0;
        public double DelayAfter { get; set; } = 0;
    }
} 