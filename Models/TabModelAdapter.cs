using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ExplorerPro.Models;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Adapter that provides TabItemModel compatibility for TabModel instances.
    /// This bridges the gap between modern TabModel and legacy ChromeStyleTabControl expectations.
    /// Automatically synchronizes properties between the two models.
    /// </summary>
    public class TabModelAdapter : TabItemModel, IDisposable
    {
        private readonly TabModel _sourceModel;
        private bool _isDisposed;
        private bool _isUpdating; // Prevents circular updates

        /// <summary>
        /// Creates an adapter that wraps a TabModel and exposes it as TabItemModel
        /// </summary>
        /// <param name="sourceModel">The modern TabModel to wrap</param>
        public TabModelAdapter(TabModel sourceModel) : base()
        {
            _sourceModel = sourceModel ?? throw new ArgumentNullException(nameof(sourceModel));
            
            // Subscribe to source model changes
            _sourceModel.PropertyChanged += OnSourceModelPropertyChanged;
            
            // Initialize properties from source
            SynchronizeFromSource();
        }

        /// <summary>
        /// Gets the underlying TabModel
        /// </summary>
        public TabModel SourceModel => _sourceModel;

        /// <summary>
        /// Synchronizes all properties from the source TabModel
        /// </summary>
        private void SynchronizeFromSource()
        {
            if (_isUpdating || _isDisposed) return;
            
            _isUpdating = true;
            try
            {
                // Core properties
                base.Id = _sourceModel.Id;
                base.Title = _sourceModel.Title;
                base.Content = _sourceModel.Content;
                base.IsPinned = _sourceModel.IsPinned;
                base.IsActive = _sourceModel.IsActive;
                base.HasUnsavedChanges = _sourceModel.HasUnsavedChanges;
                base.CreatedAt = _sourceModel.CreatedAt;
                base.LastAccessed = _sourceModel.LastActivated;
                base.IsClosable = _sourceModel.CanClose;
                
                // Convert Color to match TabItemModel expectations
                if (_sourceModel.HasCustomColor)
                {
                    base.TabColor = _sourceModel.CustomColor;
                }
                else
                {
                    base.TabColor = Colors.LightGray; // Default TabItemModel color
                }
                
                // Set tooltip with path information
                if (!string.IsNullOrEmpty(_sourceModel.Path))
                {
                    base.Tooltip = $"{_sourceModel.Title}\n{_sourceModel.Path}";
                }
                else
                {
                    base.Tooltip = _sourceModel.Title;
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Synchronizes changes back to the source TabModel
        /// </summary>
        private void SynchronizeToSource()
        {
            if (_isUpdating || _isDisposed) return;
            
            _isUpdating = true;
            try
            {
                // Only sync properties that should flow back to TabModel
                if (_sourceModel.Title != base.Title)
                    _sourceModel.Title = base.Title;
                
                if (_sourceModel.IsPinned != base.IsPinned)
                    _sourceModel.IsPinned = base.IsPinned;
                
                if (_sourceModel.IsActive != base.IsActive)
                    _sourceModel.IsActive = base.IsActive;
                
                if (_sourceModel.HasUnsavedChanges != base.HasUnsavedChanges)
                    _sourceModel.HasUnsavedChanges = base.HasUnsavedChanges;
                
                if (_sourceModel.Content != base.Content)
                    _sourceModel.Content = base.Content;
                
                // Sync color - convert LightGray back to transparent for TabModel
                if (base.TabColor == Colors.LightGray)
                {
                    if (_sourceModel.HasCustomColor)
                        _sourceModel.ClearCustomColor();
                }
                else
                {
                    if (_sourceModel.CustomColor != base.TabColor)
                        _sourceModel.CustomColor = base.TabColor;
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Handles property changes from the source TabModel
        /// </summary>
        private void OnSourceModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isDisposed || _isUpdating) return;

            // Synchronize specific properties that changed
            switch (e.PropertyName)
            {
                case nameof(TabModel.Id):
                case nameof(TabModel.Title):
                case nameof(TabModel.Content):
                case nameof(TabModel.IsPinned):
                case nameof(TabModel.IsActive):
                case nameof(TabModel.HasUnsavedChanges):
                case nameof(TabModel.CustomColor):
                case nameof(TabModel.Path):
                case nameof(TabModel.CanClose):
                    SynchronizeFromSource();
                    break;
            }
        }

        /// <summary>
        /// Override property setters to sync back to source model
        /// </summary>
        public new string Title
        {
            get => base.Title;
            set
            {
                if (base.Title != value)
                {
                    base.Title = value;
                    SynchronizeToSource();
                }
            }
        }

        public new bool IsPinned
        {
            get => base.IsPinned;
            set
            {
                if (base.IsPinned != value)
                {
                    base.IsPinned = value;
                    SynchronizeToSource();
                }
            }
        }

        public new bool IsActive
        {
            get => base.IsActive;
            set
            {
                if (base.IsActive != value)
                {
                    base.IsActive = value;
                    SynchronizeToSource();
                }
            }
        }

        public new bool HasUnsavedChanges
        {
            get => base.HasUnsavedChanges;
            set
            {
                if (base.HasUnsavedChanges != value)
                {
                    base.HasUnsavedChanges = value;
                    SynchronizeToSource();
                }
            }
        }

        public new Color TabColor
        {
            get => base.TabColor;
            set
            {
                if (base.TabColor != value)
                {
                    base.TabColor = value;
                    SynchronizeToSource();
                }
            }
        }

        public new object Content
        {
            get => base.Content;
            set
            {
                if (base.Content != value)
                {
                    base.Content = value;
                    SynchronizeToSource();
                }
            }
        }

        /// <summary>
        /// Creates an adapter for a TabModel, or returns existing adapter if already wrapped
        /// </summary>
        public static TabModelAdapter WrapTabModel(TabModel tabModel)
        {
            if (tabModel == null) return null;
            
            // If already wrapped, return the existing adapter
            // (This would require maintaining a weak reference cache in production)
            return new TabModelAdapter(tabModel);
        }

        /// <summary>
        /// Extracts the underlying TabModel from an adapter or returns null
        /// </summary>
        public static TabModel UnwrapTabModel(TabItemModel tabItemModel)
        {
            if (tabItemModel is TabModelAdapter adapter)
                return adapter.SourceModel;
            
            return null;
        }

        /// <summary>
        /// Cleanup and dispose
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            
            // Unsubscribe from source model
            if (_sourceModel != null)
            {
                _sourceModel.PropertyChanged -= OnSourceModelPropertyChanged;
            }
            
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer for cleanup if Dispose wasn't called
        /// </summary>
        ~TabModelAdapter()
        {
            Dispose();
        }
    }
} 