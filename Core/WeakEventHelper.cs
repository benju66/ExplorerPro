using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Helper for creating weak event subscriptions to prevent memory leaks
    /// </summary>
    public static class WeakEventHelper
    {
        /// <summary>
        /// Subscribe to event with weak reference
        /// </summary>
        public static IDisposable SubscribeWeak<TEventArgs>(
            object source,
            string eventName,
            EventHandler<TEventArgs> handler) where TEventArgs : EventArgs
        {
            if (source == null || handler == null) return new NullDisposable();
            return new WeakEventSubscription<TEventArgs>(source, eventName, handler);
        }
        
        /// <summary>
        /// Subscribe to routed event with weak reference
        /// </summary>
        public static IDisposable SubscribeRoutedWeak(
            UIElement element,
            RoutedEvent routedEvent,
            RoutedEventHandler handler)
        {
            if (element == null || routedEvent == null || handler == null) return new NullDisposable();
            return new WeakRoutedEventSubscription(element, routedEvent, handler);
        }
        
        /// <summary>
        /// Subscribe to command with weak reference
        /// </summary>
        public static IDisposable SubscribeCommandWeak(
            ICommand command,
            EventHandler handler)
        {
            if (command == null || handler == null) return new NullDisposable();
            
            command.CanExecuteChanged += handler;
            return new ActionDisposable(() => 
            {
                try 
                { 
                    command.CanExecuteChanged -= handler; 
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error unsubscribing from command: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Subscribe to property changed with weak reference
        /// </summary>
        public static IDisposable SubscribePropertyChangedWeak(
            System.ComponentModel.INotifyPropertyChanged source,
            System.ComponentModel.PropertyChangedEventHandler handler)
        {
            if (source == null || handler == null) return new NullDisposable();
            
            source.PropertyChanged += handler;
            return new ActionDisposable(() =>
            {
                try
                {
                    source.PropertyChanged -= handler;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error unsubscribing from PropertyChanged: {ex.Message}");
                }
            });
        }
        
        private class WeakEventSubscription<TEventArgs> : IDisposable where TEventArgs : EventArgs
        {
            private readonly WeakReference _targetRef;
            private readonly MethodInfo _method;
            private readonly object _source;
            private readonly string _eventName;
            private EventHandler<TEventArgs> _handler;
            private bool _disposed;
            
            public WeakEventSubscription(object source, string eventName, EventHandler<TEventArgs> handler)
            {
                _source = source;
                _eventName = eventName;
                _targetRef = new WeakReference(handler.Target);
                _method = handler.Method;
                
                // Create weak handler
                _handler = CreateWeakHandler();
                
                // Subscribe
                try
                {
                    var eventInfo = source.GetType().GetEvent(eventName);
                    if (eventInfo != null)
                    {
                        eventInfo.AddEventHandler(source, _handler);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Event '{eventName}' not found on type '{source.GetType().Name}'");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error subscribing to event '{eventName}': {ex.Message}");
                }
            }
            
            private EventHandler<TEventArgs> CreateWeakHandler()
            {
                return (sender, args) =>
                {
                    var target = _targetRef.Target;
                    if (target != null)
                    {
                        try
                        {
                            _method.Invoke(target, new object[] { sender, args });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error invoking weak event handler: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Target has been collected, unsubscribe
                        Dispose();
                    }
                };
            }
            
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                
                if (_handler != null)
                {
                    try
                    {
                        var eventInfo = _source.GetType().GetEvent(_eventName);
                        eventInfo?.RemoveEventHandler(_source, _handler);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error unsubscribing from event '{_eventName}': {ex.Message}");
                    }
                    finally
                    {
                        _handler = null;
                    }
                }
            }
        }
        
        private class WeakRoutedEventSubscription : IDisposable
        {
            private readonly WeakReference _elementRef;
            private readonly RoutedEvent _routedEvent;
            private readonly RoutedEventHandler _handler;
            private bool _disposed;
            
            public WeakRoutedEventSubscription(UIElement element, RoutedEvent routedEvent, RoutedEventHandler handler)
            {
                _elementRef = new WeakReference(element);
                _routedEvent = routedEvent;
                _handler = handler;
                
                try
                {
                    element.AddHandler(routedEvent, handler);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error subscribing to routed event: {ex.Message}");
                }
            }
            
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                
                try
                {
                    if (_elementRef.Target is UIElement element)
                    {
                        element.RemoveHandler(_routedEvent, _handler);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error unsubscribing from routed event: {ex.Message}");
                }
            }
        }
        
        private class ActionDisposable : IDisposable
        {
            private Action _action;
            private bool _disposed;
            
            public ActionDisposable(Action action)
            {
                _action = action;
            }
            
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                
                try
                {
                    _action?.Invoke();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in ActionDisposable: {ex.Message}");
                }
                finally
                {
                    _action = null;
                }
            }
        }
        
        private class NullDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
    
    /// <summary>
    /// Manages multiple disposable subscriptions with thread-safe disposal
    /// </summary>
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private bool _disposed;
        private readonly object _lock = new object();
        
        /// <summary>
        /// Add a disposable to be managed
        /// </summary>
        public void Add(IDisposable disposable)
        {
            if (disposable == null) return;
            
            lock (_lock)
            {
                if (_disposed)
                {
                    // Already disposed, dispose the new item immediately
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing immediately: {ex.Message}");
                    }
                    return;
                }
                
                _disposables.Add(disposable);
            }
        }
        
        /// <summary>
        /// Remove and dispose a specific disposable
        /// </summary>
        public void Remove(IDisposable disposable)
        {
            if (disposable == null) return;
            
            lock (_lock)
            {
                if (_disposables.Remove(disposable))
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing removed item: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Clear all disposables without disposing them
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _disposables.Clear();
            }
        }
        
        /// <summary>
        /// Get count of managed disposables
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _disposables.Count;
                }
            }
        }
        
        /// <summary>
        /// Dispose all managed disposables
        /// </summary>
        public void Dispose()
        {
            List<IDisposable> disposables;
            
            lock (_lock)
            {
                if (_disposed) return;
                
                disposables = _disposables.ToList();
                _disposables.Clear();
                _disposed = true;
            }
            
            foreach (var disposable in disposables)
            {
                try
                {
                    disposable?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing managed item: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// Extension methods for easier weak event management
    /// </summary>
    public static class WeakEventExtensions
    {
        /// <summary>
        /// Subscribe to an event using weak references and add to composite disposable
        /// </summary>
        public static void SubscribeWeak<TEventArgs>(
            this CompositeDisposable composite,
            object source,
            string eventName,
            EventHandler<TEventArgs> handler) where TEventArgs : EventArgs
        {
            composite.Add(WeakEventHelper.SubscribeWeak(source, eventName, handler));
        }
        
        /// <summary>
        /// Subscribe to a routed event using weak references and add to composite disposable
        /// </summary>
        public static void SubscribeRoutedWeak(
            this CompositeDisposable composite,
            UIElement element,
            RoutedEvent routedEvent,
            RoutedEventHandler handler)
        {
            composite.Add(WeakEventHelper.SubscribeRoutedWeak(element, routedEvent, handler));
        }
        
        /// <summary>
        /// Subscribe to a command using weak references and add to composite disposable
        /// </summary>
        public static void SubscribeCommandWeak(
            this CompositeDisposable composite,
            ICommand command,
            EventHandler handler)
        {
            composite.Add(WeakEventHelper.SubscribeCommandWeak(command, handler));
        }
    }
} 