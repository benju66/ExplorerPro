using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ExplorerPro.Core
{
    public static class UIThreadHelper
    {
        private static Dispatcher UIDispatcher => Application.Current?.Dispatcher;

        public static bool CheckAccess()
        {
            return UIDispatcher?.CheckAccess() ?? false;
        }

        public static void ExecuteOnUIThread(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            
            var dispatcher = UIDispatcher;
            if (dispatcher == null) return;
            
            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
            }
        }

        public static async Task ExecuteOnUIThreadAsync(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            
            var dispatcher = UIDispatcher;
            if (dispatcher == null) return;
            
            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                await dispatcher.InvokeAsync(action, DispatcherPriority.Normal);
            }
        }

        public static T ExecuteOnUIThread<T>(Func<T> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            
            var dispatcher = UIDispatcher;
            if (dispatcher == null) return default(T);
            
            if (dispatcher.CheckAccess())
            {
                return func();
            }
            else
            {
                return dispatcher.Invoke(func, DispatcherPriority.Normal);
            }
        }

        public static void VerifyUIThread()
        {
            if (!CheckAccess())
            {
                throw new InvalidOperationException("This operation must be performed on the UI thread.");
            }
        }
    }
} 