using System;
using System.Collections.Generic;
using System.Threading;

namespace ExplorerPro.Core.Disposables
{
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly object _lock = new object();
        private List<IDisposable> _disposables;
        private bool _disposed;

        public CompositeDisposable()
        {
            _disposables = new List<IDisposable>();
        }

        public CompositeDisposable(params IDisposable[] disposables)
        {
            if (disposables == null) throw new ArgumentNullException(nameof(disposables));
            _disposables = new List<IDisposable>(disposables);
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _disposed ? 0 : _disposables?.Count ?? 0;
                }
            }
        }

        public void Add(IDisposable disposable)
        {
            if (disposable == null) throw new ArgumentNullException(nameof(disposable));

            bool shouldDispose = false;
            lock (_lock)
            {
                if (_disposed)
                {
                    shouldDispose = true;
                }
                else
                {
                    _disposables.Add(disposable);
                }
            }

            if (shouldDispose)
            {
                disposable.Dispose();
            }
        }

        public void Clear()
        {
            IDisposable[] disposables = null;
            lock (_lock)
            {
                if (!_disposed && _disposables != null)
                {
                    disposables = _disposables.ToArray();
                    _disposables.Clear();
                }
            }

            if (disposables != null)
            {
                foreach (var disposable in disposables)
                {
                    disposable?.Dispose();
                }
            }
        }

        public void Dispose()
        {
            IDisposable[] disposables = null;
            lock (_lock)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    disposables = _disposables?.ToArray();
                    _disposables = null;
                }
            }

            if (disposables != null)
            {
                foreach (var disposable in disposables)
                {
                    disposable?.Dispose();
                }
            }
        }
    }
} 