using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ExplorerPro.Utilities;
using ExplorerPro.Models;

namespace ExplorerPro.UI.FileTree.Managers
{
    /// <summary>
    /// Handles all column management for the FileTreeListView including width persistence,
    /// resizing, and column configuration.
    /// </summary>
    public class FileTreeColumnManager : IDisposable
    {
        #region Constants

        private const string NAME_COLUMN_WIDTH_KEY = "file_tree.name_column_width";
        private const double DEFAULT_NAME_COLUMN_WIDTH = 250.0;
        private const double MIN_COLUMN_WIDTH = 100.0;
        private const double MAX_COLUMN_WIDTH = 600.0;

        #endregion

        #region Private Fields

        private readonly SettingsManager _settingsManager;
        private readonly ColumnDefinition _nameColumn;
        private readonly GridSplitter _nameColumnSplitter;
        
        private double _nameColumnWidth = DEFAULT_NAME_COLUMN_WIDTH;
        private bool _disposed = false;

        #endregion

        #region Events

        public event EventHandler<double> NameColumnWidthChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current name column width
        /// </summary>
        public double NameColumnWidth => _nameColumnWidth;

        /// <summary>
        /// Gets whether the column manager is properly initialized
        /// </summary>
        public bool IsInitialized => _nameColumn != null;

        #endregion

        #region Constructor

        public FileTreeColumnManager(
            SettingsManager settingsManager,
            ColumnDefinition nameColumn,
            GridSplitter nameColumnSplitter = null)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _nameColumn = nameColumn ?? throw new ArgumentNullException(nameof(nameColumn));
            _nameColumnSplitter = nameColumnSplitter;

            Initialize();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            LoadColumnWidths();
            AttachEventHandlers();
        }

        private void AttachEventHandlers()
        {
            if (_nameColumnSplitter != null)
            {
                _nameColumnSplitter.DragCompleted += OnNameColumnSplitterDragCompleted;
            }
        }

        private void DetachEventHandlers()
        {
            if (_nameColumnSplitter != null)
            {
                _nameColumnSplitter.DragCompleted -= OnNameColumnSplitterDragCompleted;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads column widths from settings
        /// </summary>
        public void LoadColumnWidths()
        {
            try
            {
                var savedWidth = _settingsManager.GetSetting(NAME_COLUMN_WIDTH_KEY, DEFAULT_NAME_COLUMN_WIDTH);
                _nameColumnWidth = Math.Max(MIN_COLUMN_WIDTH, Math.Min(MAX_COLUMN_WIDTH, savedWidth));
                
                ApplyColumnWidth();
                
                System.Diagnostics.Debug.WriteLine($"[COLUMNS] Loaded name column width: {_nameColumnWidth}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load column widths: {ex.Message}");
                _nameColumnWidth = DEFAULT_NAME_COLUMN_WIDTH;
                ApplyColumnWidth();
            }
        }

        /// <summary>
        /// Saves current column widths to settings
        /// </summary>
        public void SaveColumnWidths()
        {
            try
            {
                if (_nameColumn?.Width.Value > 0)
                {
                    _nameColumnWidth = _nameColumn.Width.Value;
                    _settingsManager.UpdateSetting(NAME_COLUMN_WIDTH_KEY, _nameColumnWidth);
                    _settingsManager.SaveSettings();
                    
                    System.Diagnostics.Debug.WriteLine($"[COLUMNS] Saved name column width: {_nameColumnWidth}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to save column widths: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the name column width programmatically
        /// </summary>
        public void SetNameColumnWidth(double width)
        {
            if (width < MIN_COLUMN_WIDTH || width > MAX_COLUMN_WIDTH)
            {
                throw new ArgumentOutOfRangeException(nameof(width), 
                    $"Width must be between {MIN_COLUMN_WIDTH} and {MAX_COLUMN_WIDTH}");
            }

            _nameColumnWidth = width;
            ApplyColumnWidth();
            
            NameColumnWidthChanged?.Invoke(this, _nameColumnWidth);
        }

        /// <summary>
        /// Resets columns to default widths
        /// </summary>
        public void ResetToDefaults()
        {
            _nameColumnWidth = DEFAULT_NAME_COLUMN_WIDTH;
            ApplyColumnWidth();
            SaveColumnWidths();
            
            NameColumnWidthChanged?.Invoke(this, _nameColumnWidth);
        }

        /// <summary>
        /// Gets column configuration information
        /// </summary>
        public ColumnConfiguration GetConfiguration()
        {
            return new ColumnConfiguration
            {
                NameColumnWidth = _nameColumnWidth,
                MinColumnWidth = MIN_COLUMN_WIDTH,
                MaxColumnWidth = MAX_COLUMN_WIDTH,
                DefaultColumnWidth = DEFAULT_NAME_COLUMN_WIDTH
            };
        }

        #endregion

        #region Private Methods

        private void ApplyColumnWidth()
        {
            if (_nameColumn != null)
            {
                _nameColumn.Width = new GridLength(_nameColumnWidth);
            }
        }

        #endregion

        #region Event Handlers

        private void OnNameColumnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            try
            {
                // Update the stored width and save it
                if (_nameColumn?.Width.Value > 0)
                {
                    var newWidth = Math.Max(MIN_COLUMN_WIDTH, Math.Min(MAX_COLUMN_WIDTH, _nameColumn.Width.Value));
                    
                    if (Math.Abs(newWidth - _nameColumnWidth) > 1.0) // Only update if significantly changed
                    {
                        _nameColumnWidth = newWidth;
                        ApplyColumnWidth(); // Ensure it's within bounds
                        SaveColumnWidths();
                        
                        NameColumnWidthChanged?.Invoke(this, _nameColumnWidth);
                        
                        System.Diagnostics.Debug.WriteLine($"[COLUMNS] Column resized to: {_nameColumnWidth}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Column resize failed: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                SaveColumnWidths();
                DetachEventHandlers();
                
                NameColumnWidthChanged = null;
            }
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Configuration information for columns
        /// </summary>
        public class ColumnConfiguration
        {
            public double NameColumnWidth { get; set; }
            public double MinColumnWidth { get; set; }
            public double MaxColumnWidth { get; set; }
            public double DefaultColumnWidth { get; set; }
        }

        #endregion
    }
} 