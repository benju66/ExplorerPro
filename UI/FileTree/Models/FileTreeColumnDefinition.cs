// UI/FileTree/Models/FileTreeColumnDefinition.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExplorerPro.UI.FileTree.Models
{
    /// <summary>
    /// Represents a column definition for the file tree view with support for 
    /// user customization and persistence
    /// </summary>
    public class FileTreeColumnDefinition : INotifyPropertyChanged
    {
        #region Fields

        private string _name;
        private string _displayName;
        private double _width;
        private double _minWidth;
        private double _maxWidth;
        private bool _isResizable;
        private bool _isVisible;
        private int _displayIndex;
        private ColumnType _type;
        private bool _canHide;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the unique name/key for the column
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the display name shown in the column header
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current width of the column
        /// </summary>
        public double Width
        {
            get => _width;
            set
            {
                // Ensure width stays within bounds
                var newWidth = Math.Max(_minWidth, Math.Min(value, _maxWidth));
                if (_width != newWidth)
                {
                    _width = newWidth;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimum width allowed for the column
        /// </summary>
        public double MinWidth
        {
            get => _minWidth;
            set
            {
                if (_minWidth != value && value >= 0)
                {
                    _minWidth = value;
                    // Ensure current width respects new minimum
                    if (_width < _minWidth)
                    {
                        Width = _minWidth;
                    }
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum width allowed for the column
        /// </summary>
        public double MaxWidth
        {
            get => _maxWidth;
            set
            {
                if (_maxWidth != value && value > 0)
                {
                    _maxWidth = value;
                    // Ensure current width respects new maximum
                    if (_width > _maxWidth)
                    {
                        Width = _maxWidth;
                    }
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the column can be resized by the user
        /// </summary>
        public bool IsResizable
        {
            get => _isResizable;
            set
            {
                if (_isResizable != value)
                {
                    _isResizable = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the column is currently visible
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the display order index of the column
        /// </summary>
        public int DisplayIndex
        {
            get => _displayIndex;
            set
            {
                if (_displayIndex != value && value >= 0)
                {
                    _displayIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the type of data displayed in the column
        /// </summary>
        public ColumnType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the user can hide this column
        /// </summary>
        public bool CanHide
        {
            get => _canHide;
            set
            {
                if (_canHide != value)
                {
                    _canHide = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the default width for this column
        /// </summary>
        public double DefaultWidth { get; private set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the FileTreeColumnDefinition class
        /// </summary>
        public FileTreeColumnDefinition()
        {
            // Set defaults
            _minWidth = 50;
            _maxWidth = 1000;
            _isResizable = true;
            _isVisible = true;
            _canHide = true;
            _width = 100;
            DefaultWidth = 100;
        }

        /// <summary>
        /// Initializes a new instance with specified parameters
        /// </summary>
        public FileTreeColumnDefinition(string name, string displayName, double defaultWidth, ColumnType type)
            : this()
        {
            Name = name;
            DisplayName = displayName;
            Width = defaultWidth;
            DefaultWidth = defaultWidth;
            Type = type;

            // Set type-specific defaults
            switch (type)
            {
                case ColumnType.Name:
                    MinWidth = 100;
                    MaxWidth = 600;
                    CanHide = false; // Name column should always be visible
                    break;
                    
                case ColumnType.Size:
                    MinWidth = 60;
                    MaxWidth = 150;
                    break;
                    
                case ColumnType.Type:
                    MinWidth = 80;
                    MaxWidth = 200;
                    break;
                    
                case ColumnType.DateModified:
                    MinWidth = 100;
                    MaxWidth = 250;
                    break;
                    
                case ColumnType.Custom:
                    MinWidth = 50;
                    MaxWidth = 500;
                    break;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Resets the column width to its default value
        /// </summary>
        public void ResetToDefault()
        {
            Width = DefaultWidth;
        }

        /// <summary>
        /// Creates a copy of this column definition
        /// </summary>
        public FileTreeColumnDefinition Clone()
        {
            return new FileTreeColumnDefinition
            {
                Name = this.Name,
                DisplayName = this.DisplayName,
                Width = this.Width,
                MinWidth = this.MinWidth,
                MaxWidth = this.MaxWidth,
                IsResizable = this.IsResizable,
                IsVisible = this.IsVisible,
                DisplayIndex = this.DisplayIndex,
                Type = this.Type,
                CanHide = this.CanHide,
                DefaultWidth = this.DefaultWidth
            };
        }

        /// <summary>
        /// Updates this definition from another instance (for settings restoration)
        /// </summary>
        public void UpdateFrom(FileTreeColumnDefinition other)
        {
            if (other == null) return;

            // Only update user-customizable properties
            Width = other.Width;
            IsVisible = other.IsVisible;
            DisplayIndex = other.DisplayIndex;
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Defines the types of columns available in the file tree
    /// </summary>
    public enum ColumnType
    {
        /// <summary>
        /// Name column showing file/folder names
        /// </summary>
        Name,

        /// <summary>
        /// Size column showing file sizes
        /// </summary>
        Size,

        /// <summary>
        /// Type column showing file types/extensions
        /// </summary>
        Type,

        /// <summary>
        /// Date modified column
        /// </summary>
        DateModified,

        /// <summary>
        /// Date created column (future feature)
        /// </summary>
        DateCreated,

        /// <summary>
        /// Date accessed column (future feature)
        /// </summary>
        DateAccessed,

        /// <summary>
        /// Attributes column (future feature)
        /// </summary>
        Attributes,

        /// <summary>
        /// Custom user-defined column
        /// </summary>
        Custom
    }
}