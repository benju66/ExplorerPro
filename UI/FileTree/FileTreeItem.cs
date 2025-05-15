// UI/FileTree/FileTreeItem.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
        private SolidColorBrush _foreground;
        private FontWeight _fontWeight;
        private ObservableCollection<FileTreeItem> _children;

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
                    
                    // Load children when expanded
                    if (value && HasDummyChild())
                    {
                        LoadChildren?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this item is selected
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
        /// Gets or sets the icon for the item
        /// </summary>
        public ImageSource Icon { get; set; }

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
            
            // Add dummy child for directories to show expander
            AddDummyChild();
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
                IsDirectory = isDirectory
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
                    
                    // Add dummy child so folder has expander
                    item.AddDummyChild();
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
        /// Adds a dummy child to show expander
        /// </summary>
        public void AddDummyChild()
        {
            if (IsDirectory && Children.Count == 0)
            {
                Children.Add(new FileTreeItem { Name = "Loading..." });
            }
        }

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