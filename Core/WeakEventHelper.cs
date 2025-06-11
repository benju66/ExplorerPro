using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using ExplorerPro.Core.Disposables;

namespace ExplorerPro.Core
{
    public static class WeakEventHelper
    {
        private class WeakEventHandler<TEventArgs> where TEventArgs : EventArgs
        {
            private readonly WeakReference _targetRef;
            private readonly MethodInfo _method;

            public WeakEventHandler(EventHandler<TEventArgs> handler)
            {
                _targetRef = new WeakReference(handler.Target);
                _method = handler.Method;
            }

            public void Handle(object sender, TEventArgs e)
            {
                var target = _targetRef.Target;
                if (target != null)
                {
                    _method.Invoke(target, new object[] { sender, e });
                }
            }

            public bool IsAlive => _targetRef.IsAlive;
        }

        public static IDisposable Subscribe<TEventArgs>(
            object source,
            string eventName,
            EventHandler<TEventArgs> handler) where TEventArgs : EventArgs
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(eventName)) throw new ArgumentNullException(nameof(eventName));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var weakHandler = new WeakEventHandler<TEventArgs>(handler);
            var eventInfo = source.GetType().GetEvent(eventName);
            
            if (eventInfo == null)
                throw new ArgumentException($"Event {eventName} not found on type {source.GetType().Name}");

            EventHandler<TEventArgs> internalHandler = (s, e) => weakHandler.Handle(s, e);
            eventInfo.AddEventHandler(source, internalHandler);
            
            return Disposable.Create(() => 
            {
                eventInfo.RemoveEventHandler(source, internalHandler);
            });
        }

        public static IDisposable SubscribePropertyChanged(
            System.ComponentModel.INotifyPropertyChanged source,
            System.ComponentModel.PropertyChangedEventHandler handler)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var weakHandler = new WeakReference(handler.Target);
            var method = handler.Method;

            System.ComponentModel.PropertyChangedEventHandler internalHandler = (s, e) =>
            {
                var target = weakHandler.Target;
                if (target != null)
                {
                    method.Invoke(target, new object[] { s, e });
                }
            };

            source.PropertyChanged += internalHandler;
            
            return Disposable.Create(() => source.PropertyChanged -= internalHandler);
        }
    }
} 