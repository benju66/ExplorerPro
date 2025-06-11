using System;
using System.Threading;

namespace ExplorerPro.Core.Disposables
{
    public static class Disposable
    {
        private sealed class ActionDisposable : IDisposable
        {
            private Action _disposeAction;

            public ActionDisposable(Action disposeAction)
            {
                _disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
            }

            public void Dispose()
            {
                var action = Interlocked.Exchange(ref _disposeAction, null);
                action?.Invoke();
            }
        }

        public static readonly IDisposable Empty = new ActionDisposable(() => { });

        public static IDisposable Create(Action disposeAction)
        {
            return new ActionDisposable(disposeAction);
        }
    }
} 