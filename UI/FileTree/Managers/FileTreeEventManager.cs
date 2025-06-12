using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Services;
using ExplorerPro.UI.FileTree.Utilities;

namespace ExplorerPro.UI.FileTree.Managers
{
    /// <summary>
    /// Manages all TreeView-related events for the file tree
    /// </summary>
    public class FileTreeEventManager : IDisposable
    {
        private readonly TreeView _treeView;
        private readonly IFileTreeService _fileTreeService;
        private readonly SelectionService _selectionService;
        private bool _isHandlingDoubleClick = false;
        private bool _disposed = false;

        public event EventHandler<string>? ItemDoubleClicked;
        public event EventHandler<string>? ItemClicked;
        public event EventHandler<FileTreeItem>? ItemExpanded;
        public event EventHandler<FileTreeContextMenuEventArgs>? ContextMenuRequested;
        public event EventHandler<MouseEventArgs>? MouseEvent;
        public event EventHandler<KeyEventArgs>? KeyboardEvent;

        public FileTreeEventManager(TreeView treeView, IFileTreeService fileTreeService, SelectionService selectionService)
        {
            _treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
            _fileTreeService = fileTreeService ?? throw new ArgumentNullException(nameof(fileTreeService));
            _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));

            AttachEventHandlers();
        }

        private void AttachEventHandlers()
        {
            _treeView.SelectedItemChanged += OnSelectedItemChanged;
            _treeView.MouseDoubleClick += OnMouseDoubleClick;
            _treeView.ContextMenuOpening += OnContextMenuOpening;
            _treeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnTreeViewItemExpanded));
            _treeView.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            _treeView.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            _treeView.PreviewMouseMove += OnPreviewMouseMove;
            _treeView.PreviewKeyDown += OnPreviewKeyDown;
        }

        private void DetachEventHandlers()
        {
            if (_treeView != null)
            {
                _treeView.SelectedItemChanged -= OnSelectedItemChanged;
                _treeView.MouseDoubleClick -= OnMouseDoubleClick;
                _treeView.ContextMenuOpening -= OnContextMenuOpening;
                _treeView.RemoveHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnTreeViewItemExpanded));
                _treeView.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                _treeView.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
                _treeView.PreviewMouseMove -= OnPreviewMouseMove;
                _treeView.PreviewKeyDown -= OnPreviewKeyDown;
            }
        }

        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_isHandlingDoubleClick || _disposed) return;

            if (e.NewValue is FileTreeItem item)
            {
                ItemClicked?.Invoke(this, item.Path);
            }
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_disposed) return;

            var originalSource = e.OriginalSource as DependencyObject;
            var treeViewItem = VisualTreeHelperEx.FindAncestor<TreeViewItem>(originalSource);

            if (treeViewItem?.DataContext is FileTreeItem item && !item.IsDirectory)
            {
                _isHandlingDoubleClick = true;
                ItemDoubleClicked?.Invoke(this, item.Path);
                e.Handled = true;

                // Reset flag after a delay
                _treeView.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, 
                    new Action(() => _isHandlingDoubleClick = false));
            }
        }

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (_disposed) return;
            
            // Extract the clicked item information
            var treeViewItem = e.Source as TreeViewItem;
            var fileItem = treeViewItem?.DataContext as FileTreeItem;
            
            // Create event args with proper context
            var eventArgs = new FileTreeContextMenuEventArgs(e)
            {
                ClickedItem = fileItem,
                ClickedTreeViewItem = treeViewItem,
                SelectedPaths = _selectionService.SelectedPaths
            };
            
            ContextMenuRequested?.Invoke(this, eventArgs);
            
            // If a handler set Handled to true, we honor that
            if (eventArgs.Handled)
            {
                e.Handled = true;
            }
        }

        private void OnTreeViewItemExpanded(object sender, RoutedEventArgs e)
        {
            if (_disposed) return;

            if (e.OriginalSource is TreeViewItem treeViewItem && 
                treeViewItem.DataContext is FileTreeItem item && 
                item.IsDirectory)
            {
                ItemExpanded?.Invoke(this, item);
            }
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_disposed) return;
            MouseEvent?.Invoke(this, e);
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_disposed) return;
            MouseEvent?.Invoke(this, e);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_disposed) return;
            MouseEvent?.Invoke(this, e);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_disposed) return;
            KeyboardEvent?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                DetachEventHandlers();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Event arguments for file tree context menu requests
    /// </summary>
    public class FileTreeContextMenuEventArgs : EventArgs
    {
        public ContextMenuEventArgs OriginalArgs { get; }
        public FileTreeItem? ClickedItem { get; set; }
        public TreeViewItem? ClickedTreeViewItem { get; set; }
        public IReadOnlyList<string> SelectedPaths { get; set; }
        public bool Handled { get; set; }
        
        public FileTreeContextMenuEventArgs(ContextMenuEventArgs originalArgs)
        {
            OriginalArgs = originalArgs;
        }
    }
}