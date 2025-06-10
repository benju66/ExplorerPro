using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using ExplorerPro.UI.FileTree;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Custom virtualizing tree control for improved performance with large file collections.
    /// Implements data virtualization for directories with thousands of items.
    /// </summary>
    public class VirtualizingTreeView : TreeView
    {
        private VirtualizingCollection<FileTreeItem> _virtualItems;
        private readonly int _pageSize = 100;
        private readonly int _virtualizationThreshold = 1000;
        
        protected override void OnItemsSourceChanged(
            IEnumerable oldValue, 
            IEnumerable newValue)
        {
            if (newValue is IList<FileTreeItem> items && items.Count > _virtualizationThreshold)
            {
                // Use virtualizing collection for large lists
                _virtualItems = new VirtualizingCollection<FileTreeItem>(
                    items, 
                    _pageSize,
                    LoadPage);
                
                base.OnItemsSourceChanged(oldValue, _virtualItems);
            }
            else
            {
                base.OnItemsSourceChanged(oldValue, newValue);
            }
        }
        
        private async Task<IList<FileTreeItem>> LoadPage(int pageIndex)
        {
            // Load page of items asynchronously
            var startIndex = pageIndex * _pageSize;
            var items = new List<FileTreeItem>();
            
            // Simulate async loading with minimal blocking
            await Task.Run(() =>
            {
                // Items are already loaded, just slice the appropriate page
                if (ItemsSource is IList<FileTreeItem> sourceItems)
                {
                    var endIndex = Math.Min(startIndex + _pageSize, sourceItems.Count);
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        items.Add(sourceItems[i]);
                    }
                }
            });
            
            return items;
        }
        
        /// <summary>
        /// Scroll to a specific item efficiently
        /// </summary>
        public void ScrollToItem(FileTreeItem item)
        {
            if (_virtualItems != null)
            {
                _virtualItems.ScrollToItem(item);
            }
            else
            {
                // Fallback to standard scrolling
                var container = (TreeViewItem)ItemContainerGenerator.ContainerFromItem(item);
                container?.BringIntoView();
            }
        }
    }
    
    /// <summary>
    /// Virtualizing collection that loads items on demand for improved performance.
    /// </summary>
    public class VirtualizingCollection<T> : ObservableCollection<T>, IList
    {
        private readonly IList<T> _sourceItems;
        private readonly int _pageSize;
        private readonly Func<int, Task<IList<T>>> _pageLoader;
        private readonly Dictionary<int, bool> _loadedPages = new();
        private readonly object _lock = new object();
        
        public VirtualizingCollection(IList<T> sourceItems, int pageSize, Func<int, Task<IList<T>>> pageLoader)
        {
            _sourceItems = sourceItems ?? throw new ArgumentNullException(nameof(sourceItems));
            _pageSize = pageSize;
            _pageLoader = pageLoader ?? throw new ArgumentNullException(nameof(pageLoader));
            
            // Initialize with placeholders
            for (int i = 0; i < sourceItems.Count; i++)
            {
                Add(default(T));
            }
        }
        
        public new T this[int index]
        {
            get
            {
                EnsurePageLoaded(index);
                return base[index];
            }
            set => base[index] = value;
        }
        
        private void EnsurePageLoaded(int index)
        {
            var pageIndex = index / _pageSize;
            
            lock (_lock)
            {
                if (_loadedPages.ContainsKey(pageIndex))
                    return;
                    
                _loadedPages[pageIndex] = true;
            }
            
            // Load page asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    var items = await _pageLoader(pageIndex);
                    var startIndex = pageIndex * _pageSize;
                    
                    // Update items on UI thread
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        for (int i = 0; i < items.Count && startIndex + i < Count; i++)
                        {
                            this[startIndex + i] = items[i];
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading page {pageIndex}: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Scroll to a specific item in the virtualized collection
        /// </summary>
        public void ScrollToItem(T item)
        {
            var index = _sourceItems.IndexOf(item);
            if (index >= 0)
            {
                EnsurePageLoaded(index);
            }
        }
        
        /// <summary>
        /// Pre-load pages around a specific index for smoother scrolling
        /// </summary>
        public void PreloadAroundIndex(int index, int pageRadius = 1)
        {
            var centerPage = index / _pageSize;
            var startPage = Math.Max(0, centerPage - pageRadius);
            var endPage = Math.Min((Count - 1) / _pageSize, centerPage + pageRadius);
            
            for (int page = startPage; page <= endPage; page++)
            {
                EnsurePageLoaded(page * _pageSize);
            }
        }
    }
} 