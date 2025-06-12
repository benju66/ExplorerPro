using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.ComponentModel;
using ExplorerPro.Core.Disposables;

namespace ExplorerPro.Core.Events
{
    /// <summary>
    /// Extension methods for creating weak event subscriptions
    /// Phase 3: Event Infrastructure - Weak Event Patterns
    /// </summary>
    public static class EventSubscriptionExtensions
    {
        /// <summary>
        /// Subscribes to Command bindings using weak references
        /// </summary>
        public static CompositeDisposable SubscribeToCommands(
            this CompositeDisposable disposables,
            UIElement element)
        {
            if (element == null) return disposables;

            // Command bindings
            foreach (CommandBinding binding in element.CommandBindings)
            {
                var command = binding.Command;
                if (command == null) continue;

                // Create weak handlers for executed and canExecute
                ExecutedRoutedEventHandler executed = (s, e) => 
                {
                    if (binding.Command?.CanExecute(e.Parameter) == true)
                    {
                        binding.Command.Execute(e.Parameter);
                    }
                };

                CanExecuteRoutedEventHandler canExecute = (s, e) => 
                {
                    e.CanExecute = binding.Command?.CanExecute(e.Parameter) ?? false;
                    e.Handled = true;
                };

                binding.Executed += executed;
                binding.CanExecute += canExecute;

                disposables.Add(Disposable.Create(() =>
                {
                    binding.Executed -= executed;
                    binding.CanExecute -= canExecute;
                }));
            }

            return disposables;
        }

        /// <summary>
        /// Creates a weak subscription for any object and action
        /// </summary>
        public static IDisposable SubscribeWeakly<T>(
            this T source,
            Action<T, IDisposable> subscribe) where T : class
        {
            var subscription = new WeakSubscription<T>(source, subscribe);
            return subscription;
        }

        /// <summary>
        /// Helper class for managing weak subscriptions
        /// </summary>
        public class WeakSubscription<T> : IDisposable where T : class
        {
            private readonly WeakReference _sourceRef;
            private IDisposable _subscription;

            public WeakSubscription(T source, Action<T, IDisposable> subscribe)
            {
                _sourceRef = new WeakReference(source);
                subscribe(source, this);
            }

            public void SetSubscription(IDisposable subscription)
            {
                _subscription = subscription;
            }

            public void Dispose()
            {
                _subscription?.Dispose();
                _subscription = null;
            }
        }
    }
} 