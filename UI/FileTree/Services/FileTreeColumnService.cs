// UI/FileTree/Services/FileTreeColumnService.cs - Enhanced Column Management
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Models;
using ExplorerPro.Themes;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Service for managing file tree column definitions, widths, visibility, and persistence
    /// </summary>
    public class FileTreeColumnService : IFileTreeColumnService, IDisposable
    {
        #region Constants

        private const string SETTINGS_KEY_PREFIX = "file_tree.columns";
        private const double DEFAULT_NAME_WIDTH = 250;
        private const double DEFAULT_SIZE_WIDTH = 100;
        private const double DEFAULT_TYPE_WIDTH = 120;
        private const double DEFAULT_DATE_WIDTH = 150;

        #endregion

        #region Fields

        private readonly SettingsManager _settingsManager;
        private readonly List<FileTreeColumnDefinition> _columns;
        private bool _isDirty;
        private bool _disposed;

        #endregion

        #region Events

        public event EventHandler<ColumnWidthChangedEventArgs> ColumnWidthChanged;
        public event EventHandler<ColumnVisibilityChangedEventArgs> ColumnVisibilityChanged;
        public event EventHandler<ColumnsReorderedEventArgs> ColumnsReordered;

        #endregion

        #region Properties

        public IReadOnlyList<FileTreeColumnDefinition> Columns => _columns.AsReadOnly();

        public IReadOnlyList<FileTreeColumnDefinition> VisibleColumns => 
            _columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList().AsReadOnly();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the FileTreeColumnService
        /// </summary>
        /// <param name="settingsManager">Settings manager for persistence</param>
        public FileTreeColumnService(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _columns = new List<FileTreeColumnDefinition>();
            
            InitializeDefaultColumns();
            
            System.Diagnostics.Debug.WriteLine("[COLUMNS] FileTreeColumnService initialized");
        }

        #endregion

        #region Initialization

        private void InitializeDefaultColumns()
        {
            _columns.Clear();
            
            // Name column - always visible, cannot be hidden
            _columns.Add(new FileTreeColumnDefinition("Name", "Name", DEFAULT_NAME_WIDTH, ColumnType.Name)
            {
                DisplayIndex = 0,
                CanHide = false,
                MinWidth = 100,
                MaxWidth = 600
            });

            // Size column
            _columns.Add(new FileTreeColumnDefinition("Size", "Size", DEFAULT_SIZE_WIDTH, ColumnType.Size)
            {
                DisplayIndex = 1,
                MinWidth = 60,
                MaxWidth = 150
            });

            // Type column
            _columns.Add(new FileTreeColumnDefinition("Type", "Type", DEFAULT_TYPE_WIDTH, ColumnType.Type)
            {
                DisplayIndex = 2,
                MinWidth = 80,
                MaxWidth = 200
            });

            // Date Modified column
            _columns.Add(new FileTreeColumnDefinition("DateModified", "Date Modified", DEFAULT_DATE_WIDTH, ColumnType.DateModified)
            {
                DisplayIndex = 3,
                MinWidth = 100,
                MaxWidth = 250
            });

            // Future columns (initially hidden)
            _columns.Add(new FileTreeColumnDefinition("DateCreated", "Date Created", 150, ColumnType.DateCreated)
            {
                DisplayIndex = 4,
                IsVisible = false,
                MinWidth = 100,
                MaxWidth = 250
            });

            _columns.Add(new FileTreeColumnDefinition("DateAccessed", "Date Accessed", 150, ColumnType.DateAccessed)
            {
                DisplayIndex = 5,
                IsVisible = false,
                MinWidth = 100,
                MaxWidth = 250
            });
            
            System.Diagnostics.Debug.WriteLine($"[COLUMNS] Initialized {_columns.Count} column definitions");
        }

        public void InitializeColumns(GridView gridView)
        {
            // Not used in the new implementation since we're not using GridView
            System.Diagnostics.Debug.WriteLine("[COLUMNS] InitializeColumns called (not used in new implementation)");
        }

        #endregion

        #region Column Management

        public FileTreeColumnDefinition GetColumn(string columnName)
        {
            return _columns.FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        }

        public void UpdateColumnWidth(string columnName, double newWidth)
        {
            var column = GetColumn(columnName);
            if (column != null)
            {
                var oldWidth = column.Width;
                
                // Ensure width is within constraints
                newWidth = Math.Max(column.MinWidth, Math.Min(column.MaxWidth, newWidth));
                
                if (Math.Abs(oldWidth - newWidth) > 0.1) // Only update if significantly different
                {
                    column.Width = newWidth;
                    _isDirty = true;
                    
                    System.Diagnostics.Debug.WriteLine($"[COLUMNS] Updated '{columnName}' width from {oldWidth} to {newWidth}");
                    
                    // Raise event
                    ColumnWidthChanged?.Invoke(this, new ColumnWidthChangedEventArgs(columnName, oldWidth, newWidth));
                }
            }
        }

        public void SetColumnVisibility(string columnName, bool isVisible)
        {
            var column = GetColumn(columnName);
            if (column != null && column.CanHide)
            {
                if (column.IsVisible != isVisible)
                {
                    column.IsVisible = isVisible;
                    _isDirty = true;
                    
                    System.Diagnostics.Debug.WriteLine($"[COLUMNS] Set '{columnName}' visibility to {isVisible}");
                    
                    ColumnVisibilityChanged?.Invoke(this, new ColumnVisibilityChangedEventArgs(columnName, isVisible));
                }
            }
        }

        public void ResetToDefaults()
        {
            System.Diagnostics.Debug.WriteLine("[COLUMNS] Resetting all columns to defaults");
            
            foreach (var column in _columns)
            {
                column.ResetToDefault();
                column.IsVisible = column.Type != ColumnType.DateCreated && column.Type != ColumnType.DateAccessed;
            }
            
            _isDirty = true;
            SaveColumnSettings();
        }

        public void ReorderColumns(Dictionary<string, int> newOrder)
        {
            if (newOrder == null) return;

            System.Diagnostics.Debug.WriteLine("[COLUMNS] Reordering columns");
            
            bool changed = false;
            foreach (var kvp in newOrder)
            {
                var column = GetColumn(kvp.Key);
                if (column != null && column.DisplayIndex != kvp.Value)
                {
                    column.DisplayIndex = kvp.Value;
                    changed = true;
                }
            }
            
            if (changed)
            {
                _isDirty = true;
                ColumnsReordered?.Invoke(this, new ColumnsReorderedEventArgs(newOrder));
            }
        }

        #endregion

        #region Column Resizing

        public void MakeColumnsResizable(UIElement control)
        {
            // Not used in the new implementation since resize is handled in the XAML
            System.Diagnostics.Debug.WriteLine("[COLUMNS] MakeColumnsResizable called (not used in new implementation)");
        }

        #endregion

        #region Auto-sizing

        public double GetOptimalColumnWidth(string columnName)
        {
            var column = GetColumn(columnName);
            if (column == null) return 100;
            
            // These are reasonable defaults for typical content
            // In a full implementation, you would measure actual content
            switch (column.Type)
            {
                case ColumnType.Name:
                    return 300; // Generous width for long file names
                case ColumnType.Size:
                    return 100; // Enough for "999.9 MB"
                case ColumnType.Type:
                    return 140; // Enough for longer file type descriptions
                case ColumnType.DateModified:
                case ColumnType.DateCreated:
                case ColumnType.DateAccessed:
                    return 160; // Enough for full date/time display
                default:
                    return column.DefaultWidth;
            }
        }

        public void AutoSizeColumn(string columnName)
        {
            var optimalWidth = GetOptimalColumnWidth(columnName);
            UpdateColumnWidth(columnName, optimalWidth);
        }

        public void AutoSizeAllColumns()
        {
            System.Diagnostics.Debug.WriteLine("[COLUMNS] Auto-sizing all columns");
            
            foreach (var column in VisibleColumns)
            {
                AutoSizeColumn(column.Name);
            }
        }

        #endregion

        #region Theme Support

        public void RefreshColumnTheme()
        {
            // Theme refresh is handled by the ImprovedFileTreeListView
            System.Diagnostics.Debug.WriteLine("[COLUMNS] Theme refresh requested");
        }

        #endregion

        #region Persistence

        public void SaveColumnSettings()
        {
            try
            {
                var settings = new Dictionary<string, object>();
                
                foreach (var column in _columns)
                {
                    var columnSettings = new Dictionary<string, object>
                    {
                        ["width"] = column.Width,
                        ["visible"] = column.IsVisible,
                        ["index"] = column.DisplayIndex
                    };
                    
                    settings[column.Name] = columnSettings;
                }
                
                _settingsManager.UpdateSetting(SETTINGS_KEY_PREFIX, settings);
                _isDirty = false;
                
                System.Diagnostics.Debug.WriteLine("[COLUMNS] Settings saved successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COLUMNS] Error saving settings: {ex.Message}");
            }
        }

        public void LoadColumnSettings()
        {
            try
            {
                var settings = _settingsManager.GetSetting<Dictionary<string, object>>(SETTINGS_KEY_PREFIX, null);
                if (settings == null)
                {
                    System.Diagnostics.Debug.WriteLine("[COLUMNS] No saved settings found, using defaults");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"[COLUMNS] Loading settings for {settings.Count} columns");
                
                foreach (var kvp in settings)
                {
                    var column = GetColumn(kvp.Key);
                    if (column == null) continue;
                    
                    if (kvp.Value is Dictionary<string, object> columnSettings)
                    {
                        if (columnSettings.TryGetValue("width", out var width))
                        {
                            double widthValue = Convert.ToDouble(width);
                            // Ensure loaded width respects constraints
                            widthValue = Math.Max(column.MinWidth, Math.Min(column.MaxWidth, widthValue));
                            column.Width = widthValue;
                            System.Diagnostics.Debug.WriteLine($"[COLUMNS] Loaded width for '{kvp.Key}': {widthValue}");
                        }
                        
                        if (columnSettings.TryGetValue("visible", out var visible) && column.CanHide)
                        {
                            column.IsVisible = Convert.ToBoolean(visible);
                        }
                        
                        if (columnSettings.TryGetValue("index", out var index))
                        {
                            column.DisplayIndex = Convert.ToInt32(index);
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("[COLUMNS] Settings loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COLUMNS] Error loading settings: {ex.Message}");
            }
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
            if (!_disposed)
            {
                if (disposing)
                {
                    // Save any pending changes
                    if (_isDirty)
                    {
                        SaveColumnSettings();
                    }
                    
                    // Clear collections
                    _columns.Clear();
                }
                
                _disposed = true;
            }
        }

        #endregion
    }
}