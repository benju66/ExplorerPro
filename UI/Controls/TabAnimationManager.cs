using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls.Interfaces;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Implementation of tab animation management.
    /// Provides smooth, modern animations with proper performance management.
    /// </summary>
    public class TabAnimationManager : ITabAnimationManager
    {
        #region Private Fields
        
        private readonly ILogger<TabAnimationManager> _logger;
        private readonly ConcurrentDictionary<TabItem, Storyboard> _activeAnimations;
        private bool _disposed;
        private IEasingFunction _defaultEasing;
        
        #endregion

        #region Constructor
        
        public TabAnimationManager(ILogger<TabAnimationManager> logger = null)
        {
            _logger = logger;
            _activeAnimations = new ConcurrentDictionary<TabItem, Storyboard>();
            
            // Initialize with Chrome-style defaults for 60fps performance
            AnimationsEnabled = true;
            DefaultDuration = AnimationTimings.Normal;
            _defaultEasing = new CubicEase { EasingMode = EasingMode.EaseOut };
            
            _logger?.LogDebug("TabAnimationManager initialized with Chrome-style settings");
        }
        
        #endregion

        #region ITabAnimationManager Implementation
        
        public bool AnimationsEnabled { get; set; }
        public double DefaultDuration { get; set; }
        public bool IsAnimating => _activeAnimations.Count > 0;

        public async Task AnimateTabCreationAsync(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            StopTabAnimations(tabItem);
            
            var storyboard = new Storyboard();
            
            // Start with zero opacity and scale
            tabItem.Opacity = 0;
            tabItem.RenderTransform = new ScaleTransform(0.8, 0.8);
            tabItem.RenderTransformOrigin = new Point(0.5, 0.5);
            
            // Fade in animation
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(DefaultDuration),
                EasingFunction = _defaultEasing
            };
            Storyboard.SetTarget(fadeIn, tabItem);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
            storyboard.Children.Add(fadeIn);
            
            // Scale in animation
            var scaleX = new DoubleAnimation
            {
                From = 0.8,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(DefaultDuration),
                EasingFunction = _defaultEasing
            };
            Storyboard.SetTarget(scaleX, tabItem);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("RenderTransform.ScaleX"));
            storyboard.Children.Add(scaleX);
            
            var scaleY = new DoubleAnimation
            {
                From = 0.8,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(DefaultDuration),
                EasingFunction = _defaultEasing
            };
            Storyboard.SetTarget(scaleY, tabItem);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("RenderTransform.ScaleY"));
            storyboard.Children.Add(scaleY);
            
            await PlayAnimationAsync(tabItem, storyboard);
            _logger?.LogDebug("Completed tab creation animation");
        }

        public async Task AnimateTabClosingAsync(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            StopTabAnimations(tabItem);
            
            var storyboard = new Storyboard();
            
            // Fade out animation
            var fadeOut = new DoubleAnimation
            {
                From = tabItem.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(AnimationTimings.Fast),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOut, tabItem);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
            storyboard.Children.Add(fadeOut);
            
            // Scale out animation
            if (tabItem.RenderTransform is ScaleTransform scale)
            {
                var scaleOut = new DoubleAnimation
                {
                    From = scale.ScaleX,
                    To = 0.8,
                    Duration = TimeSpan.FromMilliseconds(AnimationTimings.Fast),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                Storyboard.SetTarget(scaleOut, tabItem);
                Storyboard.SetTargetProperty(scaleOut, new PropertyPath("RenderTransform.ScaleX"));
                storyboard.Children.Add(scaleOut);
            }
            
            await PlayAnimationAsync(tabItem, storyboard);
            _logger?.LogDebug("Completed tab closing animation");
        }

        public async Task AnimateTabActivationAsync(TabItem tabItem, TabItem previousTab = null)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            var tasks = new List<Task>();
            
            // Animate the new active tab
            tasks.Add(AnimateTabToActiveState(tabItem));
            
            // Animate the previous tab to inactive state
            if (previousTab != null)
                tasks.Add(AnimateTabToInactiveState(previousTab));
                
            await Task.WhenAll(tasks);
            _logger?.LogDebug("Completed tab activation animation");
        }

        public void AnimateDragStart(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            StopTabAnimations(tabItem);
            
            var storyboard = new Storyboard();
            
            // Reduce opacity
            var fadeAnimation = new DoubleAnimation
            {
                To = 0.7,
                Duration = TimeSpan.FromMilliseconds(AnimationTimings.VeryFast),
                EasingFunction = _defaultEasing
            };
            Storyboard.SetTarget(fadeAnimation, tabItem);
            Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath("Opacity"));
            storyboard.Children.Add(fadeAnimation);
            
            // Slight scale down
            if (tabItem.RenderTransform is ScaleTransform || tabItem.RenderTransform == null)
            {
                if (tabItem.RenderTransform == null)
                {
                    tabItem.RenderTransform = new ScaleTransform(1, 1);
                    tabItem.RenderTransformOrigin = new Point(0.5, 0.5);
                }
                
                var scaleAnimation = new DoubleAnimation
                {
                    To = 0.95,
                    Duration = TimeSpan.FromMilliseconds(AnimationTimings.VeryFast),
                    EasingFunction = _defaultEasing
                };
                Storyboard.SetTarget(scaleAnimation, tabItem);
                Storyboard.SetTargetProperty(scaleAnimation, new PropertyPath("RenderTransform.ScaleX"));
                storyboard.Children.Add(scaleAnimation);
            }
            
            PlayAnimation(tabItem, storyboard);
        }

        public void AnimateDragProgress(TabItem tabItem, DragOperationType operationType)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            // Update visual feedback based on drag operation type
            var targetOpacity = operationType switch
            {
                DragOperationType.Reorder => 0.8,
                DragOperationType.Detach => 0.5,
                DragOperationType.Transfer => 0.6,
                _ => 0.7
            };
            
            var animation = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(AnimationTimings.VeryFast)
            };
            
            tabItem.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        public async Task AnimateDragEndAsync(TabItem tabItem, bool success)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            StopTabAnimations(tabItem);
            
            if (success)
            {
                await PlaySuccessAnimationAsync(tabItem);
            }
            else
            {
                await PlayErrorAnimationAsync(tabItem);
            }
            
            // Reset to normal state
            await RestoreNormalState(tabItem);
        }

        public async Task AnimateTabWidthChangeAsync(TabItem tabItem, double fromWidth, double toWidth)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null || Math.Abs(fromWidth - toWidth) < 1.0)
                return;
                
            StopTabAnimations(tabItem);
            
            var storyboard = new Storyboard();
            
            // Chrome-style width animation with smooth easing
            var widthAnimation = new DoubleAnimation
            {
                From = fromWidth,
                To = toWidth,
                Duration = TimeSpan.FromMilliseconds(200), // Chrome uses ~200ms for width changes
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(widthAnimation, tabItem);
            Storyboard.SetTargetProperty(widthAnimation, new PropertyPath("Width"));
            storyboard.Children.Add(widthAnimation);
            
            // Also animate MinWidth and MaxWidth for consistency
            var minWidthAnimation = new DoubleAnimation
            {
                From = fromWidth,
                To = toWidth,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(minWidthAnimation, tabItem);
            Storyboard.SetTargetProperty(minWidthAnimation, new PropertyPath("MinWidth"));
            storyboard.Children.Add(minWidthAnimation);
            
            var maxWidthAnimation = new DoubleAnimation
            {
                From = fromWidth,
                To = toWidth,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(maxWidthAnimation, tabItem);
            Storyboard.SetTargetProperty(maxWidthAnimation, new PropertyPath("MaxWidth"));
            storyboard.Children.Add(maxWidthAnimation);
            
            await PlayAnimationAsync(tabItem, storyboard);
            _logger?.LogTrace("Completed tab width animation: {FromWidth} -> {ToWidth}", fromWidth, toWidth);
        }

        public async Task AnimateTabReorderAsync(TabItem tabItem, int fromIndex, int toIndex)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            var storyboard = new Storyboard();
            
            // Calculate movement distance (simplified)
            var direction = toIndex > fromIndex ? 1 : -1;
            var distance = Math.Abs(toIndex - fromIndex) * 100; // Approximate tab width
            
            // Create translate transform if needed
            if (!(tabItem.RenderTransform is TransformGroup))
            {
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform(1, 1));
                transformGroup.Children.Add(new TranslateTransform(0, 0));
                tabItem.RenderTransform = transformGroup;
            }
            
            var slideAnimation = new DoubleAnimation
            {
                From = 0,
                To = distance * direction,
                Duration = TimeSpan.FromMilliseconds(DefaultDuration),
                EasingFunction = _defaultEasing,
                AutoReverse = true
            };
            
            Storyboard.SetTarget(slideAnimation, tabItem);
            Storyboard.SetTargetProperty(slideAnimation, new PropertyPath("RenderTransform.Children[1].X"));
            storyboard.Children.Add(slideAnimation);
            
            await PlayAnimationAsync(tabItem, storyboard);
        }

        public void AnimateHoverState(TabItem tabItem, bool isHovering)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            var duration = TimeSpan.FromMilliseconds(AnimationTimings.Fast);
            
            if (isHovering)
            {
                // Slight scale up and brightness increase on hover
                var scaleAnimation = new DoubleAnimation
                {
                    To = 1.02,
                    Duration = duration,
                    EasingFunction = _defaultEasing
                };
                tabItem.BeginAnimation(UIElement.RenderTransformProperty, null);
                if (tabItem.RenderTransform == null)
                {
                    tabItem.RenderTransform = new ScaleTransform(1, 1);
                    tabItem.RenderTransformOrigin = new Point(0.5, 0.5);
                }
                tabItem.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                tabItem.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
            }
            else
            {
                // Return to normal scale
                var scaleAnimation = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = duration,
                    EasingFunction = _defaultEasing
                };
                tabItem.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                tabItem.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
            }
        }

        public void AnimateFocusState(TabItem tabItem, bool hasFocus)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            // Add subtle glow effect for focus
            var duration = TimeSpan.FromMilliseconds(AnimationTimings.Fast);
            
            if (hasFocus)
            {
                // Add glow effect (implementation depends on visual requirements)
                var opacityAnimation = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = duration
                };
                tabItem.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            }
            else
            {
                var opacityAnimation = new DoubleAnimation
                {
                    To = 0.9,
                    Duration = duration
                };
                tabItem.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            }
        }

        public async Task AnimatePinnedStateAsync(TabItem tabItem, bool isPinned)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            var storyboard = new Storyboard();
            
            if (isPinned)
            {
                // Animate to pinned width
                var widthAnimation = new DoubleAnimation
                {
                    To = TabDimensions.PinnedWidth,
                    Duration = TimeSpan.FromMilliseconds(DefaultDuration),
                    EasingFunction = _defaultEasing
                };
                Storyboard.SetTarget(widthAnimation, tabItem);
                Storyboard.SetTargetProperty(widthAnimation, new PropertyPath("Width"));
                storyboard.Children.Add(widthAnimation);
            }
            else
            {
                // Animate to normal width
                var widthAnimation = new DoubleAnimation
                {
                    To = TabDimensions.PreferredTabWidth,
                    Duration = TimeSpan.FromMilliseconds(DefaultDuration),
                    EasingFunction = _defaultEasing
                };
                Storyboard.SetTarget(widthAnimation, tabItem);
                Storyboard.SetTargetProperty(widthAnimation, new PropertyPath("Width"));
                storyboard.Children.Add(widthAnimation);
            }
            
            await PlayAnimationAsync(tabItem, storyboard);
        }

        public async Task AnimateColorChangeAsync(TabItem tabItem, Color? newColor)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            // Color transition animation
            var duration = TimeSpan.FromMilliseconds(DefaultDuration);
            
            if (newColor.HasValue)
            {
                var colorAnimation = new ColorAnimation
                {
                    To = newColor.Value,
                    Duration = duration,
                    EasingFunction = _defaultEasing
                };
                
                if (tabItem.Background is SolidColorBrush brush)
                {
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
                }
            }
            
            await Task.Delay((int)duration.TotalMilliseconds);
        }

        public async Task PlaySuccessAnimationAsync(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            var storyboard = new Storyboard();
            
            // Brief green glow effect
            var glowAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(AnimationTimings.Fast),
                AutoReverse = true,
                EasingFunction = _defaultEasing
            };
            
            // Implementation would depend on specific visual requirements
            await PlayAnimationAsync(tabItem, storyboard);
        }

        public async Task PlayErrorAnimationAsync(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            var storyboard = new Storyboard();
            
            // Shake animation for error feedback
            var shakeAnimation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(AnimationTimings.Normal)
            };
            
            shakeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            shakeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(-3, KeyTime.FromPercent(0.25)));
            shakeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(3, KeyTime.FromPercent(0.5)));
            shakeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(-2, KeyTime.FromPercent(0.75)));
            shakeAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            
            if (!(tabItem.RenderTransform is TransformGroup))
            {
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform(1, 1));
                transformGroup.Children.Add(new TranslateTransform(0, 0));
                tabItem.RenderTransform = transformGroup;
            }
            
            Storyboard.SetTarget(shakeAnimation, tabItem);
            Storyboard.SetTargetProperty(shakeAnimation, new PropertyPath("RenderTransform.Children[1].X"));
            storyboard.Children.Add(shakeAnimation);
            
            await PlayAnimationAsync(tabItem, storyboard);
        }

        public async Task PlaySnapAnimationAsync(TabItem tabItem, Point targetPosition)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            // Implementation would depend on specific positioning requirements
            await Task.Delay((int)AnimationTimings.Fast);
        }

        public async Task PlayBounceAnimationAsync(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (!AnimationsEnabled || tabItem == null)
                return;
                
            var storyboard = new Storyboard();
            
            // Bounce scale animation
            var bounceAnimation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(AnimationTimings.Slow)
            };
            
            bounceAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0)));
            bounceAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.1, KeyTime.FromPercent(0.3), new BounceEase()));
            bounceAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(1)));
            
            if (tabItem.RenderTransform == null)
            {
                tabItem.RenderTransform = new ScaleTransform(1, 1);
                tabItem.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            
            Storyboard.SetTarget(bounceAnimation, tabItem);
            Storyboard.SetTargetProperty(bounceAnimation, new PropertyPath("RenderTransform.ScaleX"));
            storyboard.Children.Add(bounceAnimation);
            
            await PlayAnimationAsync(tabItem, storyboard);
        }

        public void StopAllAnimations()
        {
            ThrowIfDisposed();
            
            foreach (var kvp in _activeAnimations)
            {
                kvp.Value.Stop();
            }
            
            _activeAnimations.Clear();
            _logger?.LogDebug("Stopped all tab animations");
        }

        public void StopTabAnimations(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (tabItem != null && _activeAnimations.TryRemove(tabItem, out var storyboard))
            {
                storyboard.Stop();
            }
        }

        public Storyboard GetCurrentAnimation(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            return _activeAnimations.TryGetValue(tabItem, out var storyboard) ? storyboard : null;
        }

        public void SetEasingFunction(IEasingFunction easingFunction)
        {
            ThrowIfDisposed();
            
            _defaultEasing = easingFunction ?? new CubicEase { EasingMode = EasingMode.EaseOut };
        }
        
        #endregion

        #region Private Helper Methods
        
        private async Task PlayAnimationAsync(TabItem tabItem, Storyboard storyboard)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            EventHandler completedHandler = null;
            completedHandler = (s, e) =>
            {
                storyboard.Completed -= completedHandler;
                _activeAnimations.TryRemove(tabItem, out _);
                tcs.SetResult(true);
            };
            
            storyboard.Completed += completedHandler;
            _activeAnimations[tabItem] = storyboard;
            
            storyboard.Begin();
            await tcs.Task;
        }

        private void PlayAnimation(TabItem tabItem, Storyboard storyboard)
        {
            StopTabAnimations(tabItem);
            _activeAnimations[tabItem] = storyboard;
            
            storyboard.Completed += (s, e) => _activeAnimations.TryRemove(tabItem, out _);
            storyboard.Begin();
        }

        private async Task AnimateTabToActiveState(TabItem tabItem)
        {
            var storyboard = new Storyboard();
            
            // Brighten and scale slightly
            var opacityAnimation = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(AnimationTimings.Fast),
                EasingFunction = _defaultEasing
            };
            Storyboard.SetTarget(opacityAnimation, tabItem);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnimation);
            
            await PlayAnimationAsync(tabItem, storyboard);
        }

        private async Task AnimateTabToInactiveState(TabItem tabItem)
        {
            var storyboard = new Storyboard();
            
            // Dim slightly
            var opacityAnimation = new DoubleAnimation
            {
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(AnimationTimings.Fast),
                EasingFunction = _defaultEasing
            };
            Storyboard.SetTarget(opacityAnimation, tabItem);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnimation);
            
            await PlayAnimationAsync(tabItem, storyboard);
        }

        private async Task RestoreNormalState(TabItem tabItem)
        {
            var storyboard = new Storyboard();
            
            // Restore opacity
            var opacityAnimation = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(AnimationTimings.Fast)
            };
            Storyboard.SetTarget(opacityAnimation, tabItem);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnimation);
            
            // Restore scale
            var scaleAnimation = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(AnimationTimings.Fast)
            };
            Storyboard.SetTarget(scaleAnimation, tabItem);
            Storyboard.SetTargetProperty(scaleAnimation, new PropertyPath("RenderTransform.ScaleX"));
            storyboard.Children.Add(scaleAnimation);
            
            await PlayAnimationAsync(tabItem, storyboard);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TabAnimationManager));
        }
        
        #endregion

        #region IDisposable Implementation
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                StopAllAnimations();
                _disposed = true;
                _logger?.LogDebug("TabAnimationManager disposed");
            }
        }
        
        #endregion
    }
} 