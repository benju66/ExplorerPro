// UI/FileTree/FileTreeItem.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using ExplorerPro.Utilities;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Represents an item in the file tree with additional properties for columns
    /// </summary>
    public class FileTreeItem : INotifyPropertyChanged
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
        private bool _isSelectedInMulti;
        private SolidColorBrush _foreground;
        private FontWeight _fontWeight;
        private ObservableCollection<FileTreeItem> _children;
        private int _level;
        private bool _hasChildren;
        private WeakReference _parentRef;

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
                    if (value && IsDirectory)
                    {
                        System.Diagnostics.Debug.WriteLine($"Expanding: {Name}, Path: {Path}");
                        LoadChildren?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets whether this item is selected
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether this item is selected in multi-select mode
        /// Used for checkbox state in the UI
        /// </summary>
        public bool IsSelectedInMulti
        {
            get => _isSelectedInMulti;
            set
            {
                if (_isSelectedInMulti != value)
                {
                    _isSelectedInMulti = value;
                    OnPropertyChanged(nameof(IsSelectedInMulti));
                }
            }
        }

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
                    _children = value;
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
        public event EventHandler LoadChildren;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the FileTreeItem class
        /// </summary>
        public FileTreeItem()
        {
            Children = new ObservableCollection<FileTreeItem>();
            Foreground = SystemColors.WindowTextBrush;
            FontWeight = FontWeights.Normal;
            _level = 0; // Default level is 0 (root level)
            _hasChildren = false; // Initialize as false
            _isSelected = false;
            _isSelectedInMulti = false;
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

        #region Helper Methods

        /// <summary>
        /// Checks if this item has a dummy child
        /// </summary>
        public bool HasDummyChild()
        {
            return Children.Count == 1 && Children[0].Name == "Loading...";
        }

        /// <summary>
        /// Clears all children
        /// </summary>
        public void ClearChildren()
        {
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

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}