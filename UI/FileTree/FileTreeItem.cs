// UI/FileTree/FileTreeItem.cs - Fixed version with proper memory management
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ExplorerPro.Utilities;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Represents an item in the file tree with additional properties for columns
    /// Fixed version with proper memory management and IDisposable pattern
    /// </summary>
    public class FileTreeItem : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        private string _name;
        private string _path;
        private string _type;
        private string _size;
        private long _rawSize;
        private DateTime _lastModified;
        private string _lastModifiedStr;
        private bool _isDirectory;
        private bool _isExpanded;
        private bool _isSelected;
        private SolidColorBrush _foreground;
        private FontWeight _fontWeight;
        private ObservableCollection<FileTreeItem> _children;
        private int _level;
        private bool _hasChildren;
        private WeakReference _parentRef;
        private bool _disposed;
        
        // Store event handler to allow proper cleanup
        private EventHandler _loadChildrenHandler;
        private NotifyCollectionChangedEventHandler _childrenChangedHandler;
        
                // List to store weak references to event handlers to prevent memory leaks
        private readonly List<WeakReference> _loadChildrenHandlers = new List<WeakReference>();
        
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the file or folder name
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        /// <summary>
        /// Gets or sets the full path
        /// </summary>
        public string Path
        {
            get => _path;
            set
            {
                if (_path != value)
                {
                    _path = value;
                    OnPropertyChanged(nameof(Path));
                }
            }
        }

        /// <summary>
        /// Gets or sets the file type
        /// </summary>
        public string Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged(nameof(Type));
                }
            }
        }

        /// <summary>
        /// Gets or sets the formatted file size
        /// </summary>
        public string Size
        {
            get => _size;
            set
            {
                if (_size != value)
                {
                    _size = value;
                    OnPropertyChanged(nameof(Size));
                }
            }
        }

        /// <summary>
        /// Gets or sets the raw file size in bytes (for sorting)
        /// </summary>
        public long RawSize
        {
            get => _rawSize;
            set
            {
                if (_rawSize != value)
                {
                    _rawSize = value;
                    OnPropertyChanged(nameof(RawSize));
                }
            }
        }

        /// <summary>
        /// Gets or sets the last modified date
        /// </summary>
        public DateTime LastModified
        {
            get => _lastModified;
            set
            {
                if (_lastModified != value)
                {
                    _lastModified = value;
                    OnPropertyChanged(nameof(LastModified));
                }
            }
        }

        /// <summary>
        /// Gets or sets the formatted last modified date
        /// </summary>
        public string LastModifiedStr
        {
            get => _lastModifiedStr;
            set
            {
                if (_lastModifiedStr != value)
                {
                    _lastModifiedStr = value;
                    OnPropertyChanged(nameof(LastModifiedStr));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this item is a directory
        /// </summary>
        public bool IsDirectory
        {
            get => _isDirectory;
            set
            {
                if (_isDirectory != value)
                {
                    _isDirectory = value;
                    OnPropertyChanged(nameof(IsDirectory));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this item is expanded
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));

                    // Call LoadChildren directly when a directory is expanded
                    if (value && IsDirectory && !_disposed)
                    {
                        System.Diagnostics.Debug.WriteLine($"Expanding: {Name}, Path: {Path}");
                        OnLoadChildren();
                    }
                }
            }
        }

        /// <summary>
        /// Gets whether this item is selected.
        /// This property can only be set by SelectionService through SetSelectionState method.
        /// This ensures SelectionService remains the single source of truth for selection.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            private set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    // Also notify IsSelectedInMulti for backward compatibility with bindings
                    OnPropertyChanged(nameof(IsSelectedInMulti));
                }
            }
        }

        /// <summary>
        /// Gets whether this item is selected in multi-select mode.
        /// This is now just an alias for IsSelected for binding compatibility.
        /// </summary>
        public bool IsSelectedInMulti => _isSelected;

        /// <summary>
        /// Gets or sets the text color
        /// </summary>
        public SolidColorBrush Foreground
        {
            get => _foreground;
            set
            {
                if (_foreground != value)
                {
                    _foreground = value;
                    OnPropertyChanged(nameof(Foreground));
                }
            }
        }

        /// <summary>
        /// Gets or sets the font weight
        /// </summary>
        public FontWeight FontWeight
        {
            get => _fontWeight;
            set
            {
                if (_fontWeight != value)
                {
                    _fontWeight = value;
                    OnPropertyChanged(nameof(FontWeight));
                }
            }
        }

        /// <summary>
        /// Gets or sets the child items
        /// </summary>
        public ObservableCollection<FileTreeItem> Children
        {
            get => _children;
            set
            {
                if (_children != value)
                {
                    // Unsubscribe from old collection
                    if (_children != null && _childrenChangedHandler != null)
                    {
                        _children.CollectionChanged -= _childrenChangedHandler;
                    }
                    
                    _children = value;
                    
                    // Subscribe to new collection
                    if (_children != null && !_disposed)
                    {
                        _childrenChangedHandler = Children_CollectionChanged;
                        _children.CollectionChanged += _childrenChangedHandler;
                    }
                    
                    OnPropertyChanged(nameof(Children));
                }
            }
        }

        /// <summary>
        /// Gets or sets the hierarchical level (depth) of this item in the tree
        /// </summary>
        public int Level
        {
            get => _level;
            set
            {
                if (_level != value)
                {
                    _level = value;
                    OnPropertyChanged(nameof(Level));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this directory has children
        /// </summary>
        public bool HasChildren
        {
            get => _hasChildren;
            set
            {
                if (_hasChildren != value)
                {
                    _hasChildren = value;
                    OnPropertyChanged(nameof(HasChildren));
                }
            }
        }

        /// <summary>
        /// Gets or sets the icon for the item
        /// </summary>
        public ImageSource Icon { get; set; }

        /// <summary>
        /// Gets or sets a weak reference to the parent item
        /// Used for efficient parent/child selection operations
        /// </summary>
        public FileTreeItem Parent
        {
            get => _parentRef?.Target as FileTreeItem;
            set => _parentRef = value != null ? new WeakReference(value) : null;
        }

        /// <summary>
        /// Event raised when children need to be loaded
        /// </summary>
        public event EventHandler LoadChildren
        {
            add 
            { 
                if (value != null)
                    _loadChildrenHandlers.Add(new WeakReference(value)); 
            }
            remove 
            { 
                if (value != null)
                {
                    for (int i = _loadChildrenHandlers.Count - 1; i >= 0; i--)
                    {
                        var handler = _loadChildrenHandlers[i].Target as EventHandler;
                        if (handler == null || handler.Equals(value))
                        {
                            _loadChildrenHandlers.RemoveAt(i);
                        }
                    }
                }
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the FileTreeItem class
        /// </summary>
        public FileTreeItem()
        {
            _children = new ObservableCollection<FileTreeItem>();
            
            // Subscribe to children collection changes
            _childrenChangedHandler = Children_CollectionChanged;
            _children.CollectionChanged += _childrenChangedHandler;
            
            Foreground = SystemColors.WindowTextBrush;
            FontWeight = FontWeights.Normal;
            _level = 0; // Default level is 0 (root level)
            _hasChildren = false; // Initialize as false
            _isSelected = false;
        }

        /// <summary>
        /// Creates a file tree item from a file or directory path
        /// </summary>
        public static FileTreeItem FromPath(string path)
        {
            bool isDirectory = Directory.Exists(path);

            var item = new FileTreeItem
            {
                Name = System.IO.Path.GetFileName(path),
                Path = path,
                IsDirectory = isDirectory,
                Level = 0, // Will be set by the calling code
                HasChildren = false // Will be set when children are checked
            };

            // For root paths like "C:\", use the path as the name
            if (string.IsNullOrEmpty(item.Name))
            {
                item.Name = path;
            }

            try
            {
                if (isDirectory)
                {
                    var dirInfo = new DirectoryInfo(path);
                    item.Type = "Folder";
                    item.Size = "";
                    item.RawSize = 0; // We don't calculate folder size by default
                    item.LastModified = dirInfo.LastWriteTime;
                    item.LastModifiedStr = DateFormatter.FormatFileDate(dirInfo.LastWriteTime);
                }
                else
                {
                    var fileInfo = new FileInfo(path);
                    item.Type = System.IO.Path.GetExtension(path).ToUpperInvariant().TrimStart('.') + " File";
                    item.RawSize = fileInfo.Length;
                    item.Size = FileSizeFormatter.FormatSize(fileInfo.Length);
                    item.LastModified = fileInfo.LastWriteTime;
                    item.LastModifiedStr = DateFormatter.FormatFileDate(fileInfo.LastWriteTime);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting file info: {ex.Message}");
            }

            return item;
        }

        #endregion

        #region Selection Management

        /// <summary>
        /// Sets the selection state of this item.
        /// This method should only be called by SelectionService to maintain single source of truth.
        /// </summary>
        /// <param name="selected">The new selection state</param>
        internal void SetSelectionState(bool selected)
        {
            IsSelected = selected;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Checks if this item has a dummy child
        /// </summary>
        public bool HasDummyChild()
        {
            return Children.Count == 1 && Children[0].Name == "Loading...";
        }

        /// <summary>
        /// Clears all children and properly disposes them
        /// </summary>
        public void ClearChildren()
        {
            // Dispose all children first
            foreach (var child in Children.ToList())
            {
                child.Parent = null; // Clear parent reference
                child.Dispose();
            }
            
            Children.Clear();
        }

        /// <summary>
        /// Gets all descendants of this item (recursive)
        /// </summary>
        public IEnumerable<FileTreeItem> GetAllDescendants()
        {
            foreach (var child in Children)
            {
                yield return child;
                foreach (var descendant in child.GetAllDescendants())
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Checks if this item is an ancestor of the given item
        /// </summary>
        public bool IsAncestorOf(FileTreeItem item)
        {
            if (item == null || !IsDirectory) return false;
            
            var current = item.Parent;
            while (current != null)
            {
                if (current == this) return true;
                current = current.Parent;
            }
            return false;
        }

        #endregion

        #region Weak Event Management

        /// <summary>
        /// Raises the LoadChildren event using weak references to prevent memory leaks
        /// </summary>
        private void OnLoadChildren()
        {
            // Clean up dead references and invoke living handlers
            for (int i = _loadChildrenHandlers.Count - 1; i >= 0; i--)
            {
                var handler = _loadChildrenHandlers[i].Target as EventHandler;
                if (handler == null)
                {
                    // Remove dead reference
                    _loadChildrenHandlers.RemoveAt(i);
                }
                else
                {
                    // Invoke the handler
                    try
                    {
                        handler(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other handlers
                        System.Diagnostics.Debug.WriteLine($"Error invoking LoadChildren handler: {ex.Message}");
                    }
                }
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles changes to the children collection
        /// </summary>
        private void Children_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Update parent references when children are added/removed
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (FileTreeItem child in e.NewItems)
                {
                    child.Parent = this;
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (FileTreeItem child in e.OldItems)
                {
                    child.Parent = null;
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // All items removed - handled by ClearChildren
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (!_disposed)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes resources used by the FileTreeItem
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clear event handlers
                    _loadChildrenHandlers.Clear();
                    PropertyChanged = null;
                    
                    // Unsubscribe from collection events
                    if (_children != null && _childrenChangedHandler != null)
                    {
                        _children.CollectionChanged -= _childrenChangedHandler;
                        _childrenChangedHandler = null;
                    }
                    
                    // Clear children
                    ClearChildren();
                    
                    // Clear parent reference
                    _parentRef = null;
                    
                    // Clear other references
                    _foreground = null;
                    Icon = null;
                    
                    // Clear handler references
                    _loadChildrenHandler = null;
                }
                
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~FileTreeItem()
        {
            Dispose(false);
        }

        #endregion
    }
}