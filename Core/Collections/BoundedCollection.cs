using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace ExplorerPro.Core.Collections
{
    /// <summary>
    /// Thread-safe bounded collection with Observable notifications.
    /// Provides enterprise-level collection management with capacity limits.
    /// </summary>
    /// <typeparam name="T">Type of items in the collection</typeparam>
    public class BoundedCollection<T> : INotifyCollectionChanged, INotifyPropertyChanged, IEnumerable<T>, IDisposable
    {
        private readonly ObservableCollection<T> _collection;
        private readonly object _lock = new object();
        private readonly int _maxSize;
        private bool _isDisposed;

        public BoundedCollection(int maxSize = 100)
        {
            if (maxSize <= 0)
                throw new ArgumentException("Max size must be greater than zero", nameof(maxSize));
                
            _maxSize = maxSize;
            _collection = new ObservableCollection<T>();
            _collection.CollectionChanged += OnCollectionChanged;
            ((INotifyPropertyChanged)_collection).PropertyChanged += OnPropertyChanged;
        }

        /// <summary>
        /// Gets the underlying observable collection for data binding
        /// </summary>
        public ObservableCollection<T> Collection => _collection;

        /// <summary>
        /// Gets the number of items in the collection
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _collection.Count;
                }
            }
        }

        /// <summary>
        /// Gets the maximum capacity of the collection
        /// </summary>
        public int MaxSize => _maxSize;

        /// <summary>
        /// Checks if a new item can be added without exceeding capacity
        /// </summary>
        public bool CanAdd()
        {
            lock (_lock)
            {
                return _collection.Count < _maxSize;
            }
        }

        /// <summary>
        /// Gets or sets the item at the specified index
        /// </summary>
        public T this[int index]
        {
            get
            {
                lock (_lock)
                {
                    return _collection[index];
                }
            }
            set
            {
                lock (_lock)
                {
                    _collection[index] = value;
                }
            }
        }

        /// <summary>
        /// Adds an item to the collection if capacity allows
        /// </summary>
        public void Add(T item)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                
                if (_collection.Count >= _maxSize)
                    throw new InvalidOperationException($"Collection has reached maximum capacity of {_maxSize}");
                    
                _collection.Add(item);
            }
        }

        /// <summary>
        /// Tries to add an item to the collection
        /// </summary>
        public bool TryAdd(T item)
        {
            lock (_lock)
            {
                if (_isDisposed || _collection.Count >= _maxSize)
                    return false;
                    
                _collection.Add(item);
                return true;
            }
        }

        /// <summary>
        /// Inserts an item at the specified index
        /// </summary>
        public void Insert(int index, T item)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                
                if (_collection.Count >= _maxSize)
                    throw new InvalidOperationException($"Collection has reached maximum capacity of {_maxSize}");
                    
                _collection.Insert(index, item);
            }
        }

        /// <summary>
        /// Removes the specified item from the collection
        /// </summary>
        public bool Remove(T item)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                return _collection.Remove(item);
            }
        }

        /// <summary>
        /// Removes the item at the specified index
        /// </summary>
        public void RemoveAt(int index)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                _collection.RemoveAt(index);
            }
        }

        /// <summary>
        /// Moves an item from one index to another
        /// </summary>
        public void Move(int oldIndex, int newIndex)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                _collection.Move(oldIndex, newIndex);
            }
        }

        /// <summary>
        /// Clears all items from the collection
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                _collection.Clear();
            }
        }

        /// <summary>
        /// Checks if the collection contains the specified item
        /// </summary>
        public bool Contains(T item)
        {
            lock (_lock)
            {
                return _collection.Contains(item);
            }
        }

        /// <summary>
        /// Gets the index of the specified item
        /// </summary>
        public int IndexOf(T item)
        {
            lock (_lock)
            {
                return _collection.IndexOf(item);
            }
        }

        /// <summary>
        /// Creates a new array containing all items in the collection
        /// </summary>
        public T[] ToArray()
        {
            lock (_lock)
            {
                return _collection.ToArray();
            }
        }

        /// <summary>
        /// Creates a new list containing all items in the collection
        /// </summary>
        public List<T> ToList()
        {
            lock (_lock)
            {
                return new List<T>(_collection);
            }
        }

        /// <summary>
        /// Gets an enumerator for the collection
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            List<T> snapshot;
            lock (_lock)
            {
                snapshot = new List<T>(_collection);
            }
            return snapshot.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Applies a LINQ Where clause safely
        /// </summary>
        public IEnumerable<T> Where(Func<T, bool> predicate)
        {
            lock (_lock)
            {
                return _collection.Where(predicate).ToList();
            }
        }

        /// <summary>
        /// Gets the first item matching the predicate
        /// </summary>
        public T FirstOrDefault(Func<T, bool> predicate = null)
        {
            lock (_lock)
            {
                return predicate == null ? _collection.FirstOrDefault() : _collection.FirstOrDefault(predicate);
            }
        }

        /// <summary>
        /// Gets whether the collection is empty
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                lock (_lock)
                {
                    return _collection.Count == 0;
                }
            }
        }

        /// <summary>
        /// Adds an item to the beginning of the collection
        /// </summary>
        public void AddFirst(T item)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                
                if (_collection.Count >= _maxSize)
                    throw new InvalidOperationException($"Collection has reached maximum capacity of {_maxSize}");
                    
                _collection.Insert(0, item);
            }
        }

        /// <summary>
        /// Removes and returns the last item from the collection
        /// </summary>
        public T RemoveLast()
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                
                if (_collection.Count == 0)
                    throw new InvalidOperationException("Collection is empty");
                    
                var lastIndex = _collection.Count - 1;
                var item = _collection[lastIndex];
                _collection.RemoveAt(lastIndex);
                return item;
            }
        }

        /// <summary>
        /// Removes and returns the first item from the collection
        /// </summary>
        public T RemoveFirst()
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                
                if (_collection.Count == 0)
                    throw new InvalidOperationException("Collection is empty");
                    
                var item = _collection[0];
                _collection.RemoveAt(0);
                return item;
            }
        }

        #region Events

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        #endregion

        #region Disposal

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BoundedCollection<T>));
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            lock (_lock)
            {
                if (_isDisposed) return;

                _collection.CollectionChanged -= OnCollectionChanged;
                ((INotifyPropertyChanged)_collection).PropertyChanged -= OnPropertyChanged;
                
                _collection.Clear();
                _isDisposed = true;
            }

            GC.SuppressFinalize(this);
        }

        #endregion
    }
} 