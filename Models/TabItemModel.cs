using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Model class for tab metadata in Chrome-style tab system
    /// Stores tab information including title, color, pin state, and content
    /// </summary>
    public class TabItemModel : INotifyPropertyChanged
    {
        private string _id;
        private string _title;
        private Color _tabColor;
        private bool _isPinned;
        private object _content;
        private bool _hasUnsavedChanges;
        private string _tooltip;
        private DateTime _createdAt;
        private DateTime _lastAccessed;
        private bool _isActive;
        private bool _isClosable;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of TabItemModel
        /// </summary>
        public TabItemModel()
        {
            _id = Guid.NewGuid().ToString();
            _title = "New Tab";
            _tabColor = Colors.LightGray;
            _isPinned = false;
            _hasUnsavedChanges = false;
            _tooltip = string.Empty;
            _createdAt = DateTime.Now;
            _lastAccessed = DateTime.Now;
            _isActive = false;
            _isClosable = true;
        }

        /// <summary>
        /// Initializes a new instance of TabItemModel with specified parameters
        /// </summary>
        /// <param name="id">Unique identifier for the tab</param>
        /// <param name="title">Title of the tab</param>
        /// <param name="content">Content object for the tab</param>
        public TabItemModel(string id, string title, object content = null) : this()
        {
            _id = id ?? Guid.NewGuid().ToString();
            _title = title ?? "New Tab";
            _content = content;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Unique identifier for the tab
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// Display title of the tab
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Color theme for the tab
        /// </summary>
        public Color TabColor
        {
            get => _tabColor;
            set => SetProperty(ref _tabColor, value);
        }

        /// <summary>
        /// Whether the tab is pinned (always visible)
        /// </summary>
        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

        /// <summary>
        /// Content object hosted by this tab
        /// </summary>
        public object Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        /// <summary>
        /// Whether the tab has unsaved changes
        /// </summary>
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => SetProperty(ref _hasUnsavedChanges, value);
        }

        /// <summary>
        /// Tooltip text for the tab
        /// </summary>
        public string Tooltip
        {
            get => _tooltip;
            set => SetProperty(ref _tooltip, value);
        }

        /// <summary>
        /// When the tab was created
        /// </summary>
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        /// <summary>
        /// When the tab was last accessed
        /// </summary>
        public DateTime LastAccessed
        {
            get => _lastAccessed;
            set => SetProperty(ref _lastAccessed, value);
        }

        /// <summary>
        /// Whether this tab is currently active/selected
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        /// Whether this tab can be closed by the user
        /// </summary>
        public bool IsClosable
        {
            get => _isClosable;
            set => SetProperty(ref _isClosable, value);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Updates the last accessed time to now
        /// </summary>
        public void UpdateLastAccessed()
        {
            LastAccessed = DateTime.Now;
        }

        /// <summary>
        /// Creates a copy of this tab model
        /// </summary>
        /// <returns>A new TabItemModel with the same properties</returns>
        public TabItemModel Clone()
        {
            return new TabItemModel(Id, Title, Content)
            {
                TabColor = TabColor,
                IsPinned = IsPinned,
                HasUnsavedChanges = HasUnsavedChanges,
                Tooltip = Tooltip,
                CreatedAt = CreatedAt,
                LastAccessed = LastAccessed,
                IsActive = IsActive,
                IsClosable = IsClosable
            };
        }

        /// <summary>
        /// Returns a string representation of the tab
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"TabItemModel: {Title} ({Id})";
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        /// <summary>
        /// Event fired when a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets a property value and raises PropertyChanged if the value changed
        /// </summary>
        /// <typeparam name="T">Type of the property</typeparam>
        /// <param name="field">Reference to the backing field</param>
        /// <param name="value">New value</param>
        /// <param name="propertyName">Name of the property (automatically filled)</param>
        /// <returns>True if the property value changed, false otherwise</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
} 