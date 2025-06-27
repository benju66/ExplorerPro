using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.Events
{
    /// <summary>
    /// Weak event manager for loose coupling between services.
    /// Prevents memory leaks by using weak references for event subscriptions.
    /// </summary>
    public class WeakEventManager : IDisposable
    {
        private readonly ILogger<WeakEventManager> _logger;
        private readonly ConcurrentDictionary<string, WeakEventSubscription> _subscriptions;
        private bool _disposed;
        
        public WeakEventManager(ILogger<WeakEventManager> logger = null)
        {
            _logger = logger;
            _subscriptions = new ConcurrentDictionary<string, WeakEventSubscription>();
        }
        
        /// <summary>
        /// Subscribes to an event using weak references
        /// </summary>
        public void Subscribe<T>(object source, string eventName, EventHandler<T> handler) where T : EventArgs
        {
            if (_disposed) return;
            
            var subscriptionKey = GetSubscriptionKey(source, eventName, handler);
            
            var subscription = new WeakEventSubscription
            {
                SourceReference = new WeakReference(source),
                HandlerReference = new WeakReference(handler),
                EventName = eventName,
                HandlerMethodName = handler.Method.Name,
                TargetReference = handler.Target != null ? new WeakReference(handler.Target) : null
            };
            
            // Get the event info
            var eventInfo = source.GetType().GetEvent(eventName);
            if (eventInfo != null)
            {
                // Create a proxy handler that checks weak references
                EventHandler<T> proxyHandler = (sender, args) =>
                {
                    var targetHandler = subscription.HandlerReference.Target as EventHandler<T>;
                    if (targetHandler != null)
                    {
                        try
                        {
                            targetHandler(sender, args);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error in weak event handler {HandlerName}", subscription.HandlerMethodName);
                        }
                    }
                    else
                    {
                        // Handler has been collected, remove subscription
                        CleanupSubscription(subscriptionKey);
                    }
                };
                
                subscription.ProxyHandler = proxyHandler;
                
                // Subscribe to the actual event
                eventInfo.AddEventHandler(source, proxyHandler);
                
                _subscriptions.TryAdd(subscriptionKey, subscription);
                
                _logger?.LogDebug("Subscribed to event {EventName} on {SourceType}", eventName, source.GetType().Name);
            }
        }
        
        /// <summary>
        /// Unsubscribes from an event
        /// </summary>
        public void Unsubscribe(object source, string eventName, Delegate handler)
        {
            if (_disposed) return;
            
            var subscriptionKey = GetSubscriptionKey(source, eventName, handler);
            
            if (_subscriptions.TryRemove(subscriptionKey, out var subscription))
            {
                // Remove the actual event subscription
                var eventInfo = source.GetType().GetEvent(eventName);
                if (eventInfo != null && subscription.ProxyHandler != null)
                {
                    eventInfo.RemoveEventHandler(source, subscription.ProxyHandler);
                    _logger?.LogDebug("Unsubscribed from event {EventName} on {SourceType}", eventName, source.GetType().Name);
                }
            }
        }
        
        /// <summary>
        /// Cleans up dead references
        /// </summary>
        public int CleanupDeadReferences()
        {
            if (_disposed) return 0;
            
            var cleanedCount = 0;
            var keysToRemove = new System.Collections.Generic.List<string>();
            
            foreach (var kvp in _subscriptions)
            {
                var subscription = kvp.Value;
                
                // Check if source or handler is dead
                if (subscription.SourceReference.Target == null || 
                    subscription.HandlerReference.Target == null ||
                    (subscription.TargetReference != null && subscription.TargetReference.Target == null))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                CleanupSubscription(key);
                cleanedCount++;
            }
            
            if (cleanedCount > 0)
            {
                _logger?.LogDebug("Cleaned up {Count} dead event subscriptions", cleanedCount);
            }
            
            return cleanedCount;
        }
        
        private void CleanupSubscription(string subscriptionKey)
        {
            if (_subscriptions.TryRemove(subscriptionKey, out var subscription))
            {
                var source = subscription.SourceReference.Target;
                if (source != null)
                {
                    var eventInfo = source.GetType().GetEvent(subscription.EventName);
                    if (eventInfo != null && subscription.ProxyHandler != null)
                    {
                        try
                        {
                            eventInfo.RemoveEventHandler(source, subscription.ProxyHandler);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error removing event handler during cleanup");
                        }
                    }
                }
            }
        }
        
        private string GetSubscriptionKey(object source, string eventName, Delegate handler)
        {
            var sourceHash = RuntimeHelpers.GetHashCode(source);
            var handlerHash = RuntimeHelpers.GetHashCode(handler);
            return $"{sourceHash}_{eventName}_{handlerHash}";
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                // Clean up all subscriptions
                var keys = new string[_subscriptions.Count];
                _subscriptions.Keys.CopyTo(keys, 0);
                
                foreach (var key in keys)
                {
                    CleanupSubscription(key);
                }
                
                _subscriptions.Clear();
                _disposed = true;
                
                _logger?.LogDebug("WeakEventManager disposed");
            }
        }
    }
    
    internal class WeakEventSubscription
    {
        public WeakReference SourceReference { get; set; }
        public WeakReference HandlerReference { get; set; }
        public WeakReference TargetReference { get; set; }
        public string EventName { get; set; }
        public string HandlerMethodName { get; set; }
        public Delegate ProxyHandler { get; set; }
    }
} 