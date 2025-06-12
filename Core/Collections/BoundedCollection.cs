using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ExplorerPro.Core.Collections
{
    /// <summary>
    /// Thread-safe bounded collection that maintains a maximum number of items
    /// Automatically removes oldest items when size limit is exceeded
    /// Phase 5: Resource Bounds - History and Collection Limits
    /// </summary>
    public class BoundedCollection<T> : IEnumerable<T>, IDisposable
    {
        private readonly LinkedList<T> _items = new LinkedList<T>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly int _maxSize;
        private bool _disposed;

        public BoundedCollection(int maxSize)
        {
            if (maxSize <= 0) 
                throw new ArgumentException("Max size must be greater than 0", nameof(maxSize));
            _maxSize = maxSize;
        }

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _disposed ? 0 : _items.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public int MaxSize => _maxSize;

        public bool IsEmpty
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _disposed || _items.Count == 0;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public void Add(T item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BoundedCollection<T>));

            _lock.EnterWriteLock();
            try
            {
                _items.AddLast(item);
                
                while (_items.Count > _maxSize)
                {
                    var removed = _items.First.Value;
                    _items.RemoveFirst();
                    OnItemRemoved(removed);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void AddFirst(T item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BoundedCollection<T>));

            _lock.EnterWriteLock();
            try
            {
                _items.AddFirst(item);
                
                while (_items.Count > _maxSize)
                {
                    var removed = _items.Last.Value;
                    _items.RemoveLast();
                    OnItemRemoved(removed);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public T RemoveLast()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BoundedCollection<T>));

            _lock.EnterWriteLock();
            try
            {
                if (_items.Count == 0)
                    throw new InvalidOperationException("Collection is empty");

                var item = _items.Last.Value;
                _items.RemoveLast();
                return item;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public T RemoveFirst()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BoundedCollection<T>));

            _lock.EnterWriteLock();
            try
            {
                if (_items.Count == 0)
                    throw new InvalidOperationException("Collection is empty");

                var item = _items.First.Value;
                _items.RemoveFirst();
                return item;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BoundedCollection<T>));

            _lock.EnterWriteLock();
            try
            {
                foreach (var item in _items)
                {
                    OnItemRemoved(item);
                }
                _items.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Contains(T item)
        {
            _lock.EnterReadLock();
            try
            {
                return !_disposed && _items.Contains(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public T[] ToArray()
        {
            _lock.EnterReadLock();
            try
            {
                if (_disposed) return new T[0];
                
                var array = new T[_items.Count];
                _items.CopyTo(array, 0);
                return array;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public T[] ToArrayReverse()
        {
            _lock.EnterReadLock();
            try
            {
                if (_disposed) return new T[0];
                
                return _items.Reverse().ToArray();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public T GetLast()
        {
            _lock.EnterReadLock();
            try
            {
                if (_disposed || _items.Count == 0)
                    throw new InvalidOperationException("Collection is empty or disposed");
                
                return _items.Last.Value;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public T GetFirst()
        {
            _lock.EnterReadLock();
            try
            {
                if (_disposed || _items.Count == 0)
                    throw new InvalidOperationException("Collection is empty or disposed");
                
                return _items.First.Value;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        protected virtual void OnItemRemoved(T item)
        {
            // Override to handle cleanup of removed items
            (item as IDisposable)?.Dispose();
        }

        public IEnumerator<T> GetEnumerator()
        {
            var snapshot = ToArray();
            foreach (var item in snapshot)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _lock.EnterWriteLock();
                try
                {
                    foreach (var item in _items)
                    {
                        OnItemRemoved(item);
                    }
                    _items.Clear();
                }
                finally
                {
                    _lock.ExitWriteLock();
                    _lock?.Dispose();
                }
            }

            _disposed = true;
        }
    }
} 