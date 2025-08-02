using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows;
using System.Threading.Tasks;
using ExplorerPro.UI.MainWindow;
using ExplorerPro.Core.TabManagement;
using System.Collections.Generic;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Unified model for tab data that consolidates all tab-related properties.
    /// Replaces the multiple competing tab models (TabItemModel, TabColorData, etc.)
    /// </summary>
    public class TabModel : INotifyPropertyChanged, IDisposable
    {
        #region Private Fields
        
        private string _id;
        private string _title;
        private string _path;
        private Color _customColor;
        private bool _isPinned;
        private bool _isActive;
        private bool _hasUnsavedChanges;
        private object _content;
        private TabState _state;
        private DateTime _createdAt;
        private DateTime _lastActivated;
        private int _activationCount;
        private bool _isDisposed;
        private TabPriority _priority;
        private bool _isLoading;
        private string _iconPath;
        private string _groupId;
        private Dictionary<string, object> _metadata;
        
        #endregion

        #region Constructors
        
        /// <summary>
        /// Creates a new TabModel with default values
        /// </summary>
        public TabModel()
        {
            _id = Guid.NewGuid().ToString();
            _title = "New Tab";
            _path = string.Empty;
            _customColor = Colors.Transparent;
            _isPinned = false;
            _isActive = false;
            _hasUnsavedChanges = false;
            _state = TabState.Normal;
            _createdAt = DateTime.UtcNow;
            _lastActivated = DateTime.UtcNow;
            _activationCount = 0;
            _priority = TabPriority.Normal;
            _isLoading = false;
            _iconPath = string.Empty;
            _metadata = new Dictionary<string, object>();
        }
        
        /// <summary>
        /// Creates a new TabModel with specified title and path
        /// </summary>
        public TabModel(string title, string path = null) : this()
        {
            _title = title ?? "New Tab";
            _path = path ?? string.Empty;
        }
        
        #endregion

        #region Core Properties
        
        /// <summary>
        /// Unique identifier for this tab
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
        /// File system path associated with this tab
        /// </summary>
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }
        
        /// <summary>
        /// Content object for this tab (usually MainWindowContainer)
        /// </summary>
        public object Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }
        
        #endregion

        #region Visual Properties
        
        /// <summary>
        /// Custom color for the tab. Transparent means no custom color.
        /// </summary>
        public Color CustomColor
        {
            get => _customColor;
            set => SetProperty(ref _customColor, value);
        }
        
        /// <summary>
        /// Whether this tab has a custom color applied
        /// </summary>
        public bool HasCustomColor => _customColor != Colors.Transparent && _customColor.A > 0;
        
        /// <summary>
        /// Whether this tab is pinned
        /// </summary>
        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }
        
        /// <summary>
        /// Whether this tab is currently active/selected
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set 
            { 
                if (SetProperty(ref _isActive, value) && value)
                {
                    _lastActivated = DateTime.UtcNow;
                    _activationCount++;
                    OnPropertyChanged(nameof(LastActivated));
                    OnPropertyChanged(nameof(ActivationCount));
                }
            }
        }
        
        /// <summary>
        /// Whether this tab has unsaved changes
        /// </summary>
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => SetProperty(ref _hasUnsavedChanges, value);
        }
        
        /// <summary>
        /// Current state of the tab
        /// </summary>
        public TabState State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }
        
        /// <summary>
        /// Priority level of the tab for resource allocation
        /// </summary>
        public TabPriority Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }
        
        /// <summary>
        /// Whether the tab is currently loading content
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        /// <summary>
        /// Path to the tab's icon resource
        /// </summary>
        public string IconPath
        {
            get => _iconPath;
            set => SetProperty(ref _iconPath, value);
        }
        
        /// <summary>
        /// Group identifier for tab grouping functionality. 
        /// Tabs with the same GroupId belong to the same group.
        /// </summary>
        public string GroupId
        {
            get => _groupId;
            set => SetProperty(ref _groupId, value);
        }
        
        /// <summary>
        /// Additional metadata associated with the tab
        /// </summary>
        public Dictionary<string, object> Metadata
        {
            get => _metadata;
            set => SetProperty(ref _metadata, value);
        }
        
        #endregion

        #region Computed Properties
        
        /// <summary>
        /// Display title with indicators for unsaved changes, pin status, etc.
        /// </summary>
        public string DisplayTitle
        {
            get
            {
                var title = _title;
                if (_hasUnsavedChanges) title += "*";
                if (_isPinned) title = "📌 " + title;
                return title;
            }
        }
        
        /// <summary>
        /// Whether this tab can be closed (pinned tabs may have restrictions)
        /// </summary>
        public bool CanClose => !_isPinned || !HasUnsavedChanges;
        
        /// <summary>
        /// Whether this tab can be moved/reordered
        /// </summary>
        public bool CanMove => true; // May add restrictions later
        
        #endregion

        #region Metadata Properties
        
        /// <summary>
        /// When this tab was created
        /// </summary>
        public DateTime CreatedAt => _createdAt;
        
        /// <summary>
        /// When this tab was last activated
        /// </summary>
        public DateTime LastActivated => _lastActivated;
        
        /// <summary>
        /// How many times this tab has been activated
        /// </summary>
        public int ActivationCount => _activationCount;
        
        /// <summary>
        /// How long this tab has existed
        /// </summary>
        public TimeSpan Age => DateTime.UtcNow - _createdAt;
        
        /// <summary>
        /// How long since this tab was last activated
        /// </summary>
        public TimeSpan TimeSinceLastActivation => DateTime.UtcNow - _lastActivated;
        
        #endregion

        #region Methods
        
        /// <summary>
        /// Creates a deep copy of this tab model
        /// </summary>
        public TabModel Clone()
        {
            return new TabModel
            {
                _title = _title,
                _path = _path,
                _customColor = _customColor,
                _isPinned = _isPinned,
                _hasUnsavedChanges = _hasUnsavedChanges,
                _state = _state,
                // Don't copy: _id (new ID), _content (will be set separately), timestamps, activation data
            };
        }
        
        /// <summary>
        /// Resets the custom color to transparent
        /// </summary>
        public void ClearCustomColor()
        {
            CustomColor = Colors.Transparent;
        }
        
        /// <summary>
        /// Marks this tab as having unsaved changes
        /// </summary>
        public void MarkAsModified()
        {
            HasUnsavedChanges = true;
        }
        
        /// <summary>
        /// Marks this tab as saved (no unsaved changes)
        /// </summary>
        public void MarkAsSaved()
        {
            HasUnsavedChanges = false;
        }
        
        /// <summary>
        /// Activates this tab and updates activation metadata
        /// </summary>
        public void Activate()
        {
            IsActive = true;
        }
        
        /// <summary>
        /// Deactivates this tab
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
        }
        
        /// <summary>
        /// Asynchronously initializes the tab content
        /// </summary>
        public async Task InitializeAsync()
        {
            IsLoading = true;
            try
            {
                State = TabState.Loading;
                
                // Content initialization logic would go here
                await Task.Delay(100); // Simulate async initialization
                
                State = TabState.Normal;
            }
            catch
            {
                State = TabState.Error;
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        /// <summary>
        /// Creates a tab from a creation request
        /// </summary>
        public static TabModel FromCreationRequest(TabCreationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            
            var tab = new TabModel(request.Title, request.Path)
            {
                IsPinned = request.IsPinned,
                Priority = request.Priority,
                Content = request.Content
            };
            
            if (request.CustomColor.HasValue)
                tab.CustomColor = request.CustomColor.Value;
                
            return tab;
        }
        
        #endregion

        #region INotifyPropertyChanged Implementation
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            
            var oldValue = field;
            field = value;
            OnPropertyChanged(propertyName);
            
            // Notify dependent properties
            if (propertyName == nameof(Title) || propertyName == nameof(HasUnsavedChanges) || propertyName == nameof(IsPinned))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
            
            if (propertyName == nameof(IsPinned) || propertyName == nameof(HasUnsavedChanges))
            {
                OnPropertyChanged(nameof(CanClose));
            }
            
            if (propertyName == nameof(CustomColor))
            {
                OnPropertyChanged(nameof(HasCustomColor));
            }
            
            return true;
        }
        
        #endregion

        #region IDisposable Implementation
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                // Dispose content if it's disposable
                if (_content is IDisposable disposableContent)
                {
                    disposableContent.Dispose();
                }
                
                _content = null;
                _isDisposed = true;
            }
        }
        
        #endregion

        #region Object Overrides
        
        public override string ToString()
        {
            return $"TabModel: {DisplayTitle} ({Id})";
        }
        
        public override bool Equals(object obj)
        {
            return obj is TabModel other && _id == other._id;
        }
        
        public override int GetHashCode()
        {
            return _id?.GetHashCode() ?? 0;
        }
        
        #endregion
    }

    /// <summary>
    /// Enumeration of possible tab states
    /// </summary>
    public enum TabState
    {
        /// <summary>
        /// Normal operational state
        /// </summary>
        Normal,
        
        /// <summary>
        /// Tab is hibernated to save memory
        /// </summary>
        Hibernated,
        
        /// <summary>
        /// Tab is being loaded
        /// </summary>
        Loading,
        
        /// <summary>
        /// Tab has an error
        /// </summary>
        Error,
        
        /// <summary>
        /// Tab is being closed
        /// </summary>
        Closing
    }
} 