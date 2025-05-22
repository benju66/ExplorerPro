// UI/FileTree/Services/FileTreeColumnService.cs
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
        private readonly Dictionary<string, GridViewColumn> _gridViewColumns;
        private readonly Dictionary<GridViewColumnHeader, ColumnResizeState> _resizeStates;
        private GridView _gridView;
        private UIElement _parentControl;
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
            _gridViewColumns = new Dictionary<string, GridViewColumn>();
            _resizeStates = new Dictionary<GridViewColumnHeader, ColumnResizeState>();
            
            InitializeDefaultColumns();
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
        }

        public void InitializeColumns(GridView gridView)
        {
            if (gridView == null)
                throw new ArgumentNullException(nameof(gridView));

            _gridView = gridView;
            _gridView.Columns.Clear();
            _gridViewColumns.Clear();

            // Create GridViewColumns for each visible column in display order
            foreach (var columnDef in VisibleColumns)
            {
                var gridViewColumn = CreateGridViewColumn(columnDef);
                _gridView.Columns.Add(gridViewColumn);
                _gridViewColumns[columnDef.Name] = gridViewColumn;
            }

            System.Diagnostics.Debug.WriteLine($"[COLUMNS] Initialized {_gridView.Columns.Count} columns");
        }

        private GridViewColumn CreateGridViewColumn(FileTreeColumnDefinition columnDef)
        {
            var column = new GridViewColumn
            {
                Header = columnDef.DisplayName,
                Width = columnDef.Width
            };

            // Set up two-way binding for width
            var widthBinding = new Binding(nameof(FileTreeColumnDefinition.Width))
            {
                Source = columnDef,
                Mode = BindingMode.TwoWay
            };
            BindingOperations.SetBinding(column, GridViewColumn.WidthProperty, widthBinding);

            // Monitor width changes through the column definition
            columnDef.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileTreeColumnDefinition.Width))
                {
                    OnColumnWidthChanged(columnDef.Name, column.Width, columnDef.Width);
                }
            };

            return column;
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
                column.Width = newWidth;
                _isDirty = true;
                
                // The property changed event in the column definition will trigger ColumnWidthChanged
            }
        }

        public void SetColumnVisibility(string columnName, bool isVisible)
        {
            var column = GetColumn(columnName);
            if (column != null && column.CanHide)
            {
                column.IsVisible = isVisible;
                _isDirty = true;
                
                // Rebuild columns in GridView
                if (_gridView != null)
                {
                    InitializeColumns(_gridView);
                }
                
                ColumnVisibilityChanged?.Invoke(this, new ColumnVisibilityChangedEventArgs(columnName, isVisible));
            }
        }

        public void ResetToDefaults()
        {
            foreach (var column in _columns)
            {
                column.ResetToDefault();
                column.IsVisible = column.Type != ColumnType.DateCreated && column.Type != ColumnType.DateAccessed;
            }
            
            _isDirty = true;
            
            if (_gridView != null)
            {
                InitializeColumns(_gridView);
            }
            
            SaveColumnSettings();
        }

        public void ReorderColumns(Dictionary<string, int> newOrder)
        {
            if (newOrder == null) return;

            foreach (var kvp in newOrder)
            {
                var column = GetColumn(kvp.Key);
                if (column != null)
                {
                    column.DisplayIndex = kvp.Value;
                }
            }
            
            _isDirty = true;
            
            if (_gridView != null)
            {
                InitializeColumns(_gridView);
            }
            
            ColumnsReordered?.Invoke(this, new ColumnsReorderedEventArgs(newOrder));
        }

        #endregion

        #region Column Resizing

        public void MakeColumnsResizable(UIElement control)
        {
            _parentControl = control;
            
            // Find the GridViewHeaderRowPresenter
            var headerPresenter = FindVisualChild<GridViewHeaderRowPresenter>(control);
            if (headerPresenter == null)
            {
                System.Diagnostics.Debug.WriteLine("[COLUMNS] GridViewHeaderRowPresenter not found");
                return;
            }
            
            // Attach resize handlers to each column header
            foreach (var header in FindVisualChildren<GridViewColumnHeader>(headerPresenter))
            {
                var thumb = FindVisualChild<Thumb>(header);
                if (thumb != null)
                {
                    thumb.DragStarted -= ColumnHeader_DragStarted;
                    thumb.DragDelta -= ColumnHeader_DragDelta;
                    thumb.DragCompleted -= ColumnHeader_DragCompleted;
                    
                    thumb.DragStarted += ColumnHeader_DragStarted;
                    thumb.DragDelta += ColumnHeader_DragDelta;
                    thumb.DragCompleted += ColumnHeader_DragCompleted;
                }
            }
            
            System.Diagnostics.Debug.WriteLine("[COLUMNS] Column resize handlers attached");
        }

        private void ColumnHeader_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb)
            {
                var header = FindAncestor<GridViewColumnHeader>(thumb);
                if (header != null && header.Column != null)
                {
                    // Find the column definition
                    var columnDef = FindColumnByHeader(header);
                    if (columnDef != null)
                    {
                        _resizeStates[header] = new ColumnResizeState
                        {
                            Column = columnDef,
                            OriginalWidth = header.Column.ActualWidth
                        };
                    }
                }
            }
        }

        private void ColumnHeader_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb)
            {
                var header = FindAncestor<GridViewColumnHeader>(thumb);
                if (header != null && _resizeStates.TryGetValue(header, out var state))
                {
                    double newWidth = Math.Max(state.Column.MinWidth, 
                        Math.Min(state.Column.MaxWidth, state.OriginalWidth + e.HorizontalChange));
                    
                    // Update through the column definition
                    state.Column.Width = newWidth;
                }
            }
        }

        private void ColumnHeader_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (sender is Thumb thumb)
            {
                var header = FindAncestor<GridViewColumnHeader>(thumb);
                if (header != null)
                {
                    _resizeStates.Remove(header);
                    
                    // Save settings after resize is complete
                    if (_isDirty)
                    {
                        SaveColumnSettings();
                    }
                }
            }
        }

        private FileTreeColumnDefinition FindColumnByHeader(GridViewColumnHeader header)
        {
            if (header?.Column == null) return null;
            
            // Find column by matching the header text
            var headerText = header.Column.Header?.ToString();
            return _columns.FirstOrDefault(c => c.DisplayName == headerText);
        }

        #endregion

        #region Auto-sizing

        public double GetOptimalColumnWidth(string columnName)
        {
            var column = GetColumn(columnName);
            if (column == null) return 100;
            
            // This is a simplified implementation
            // In a full implementation, you would measure actual content
            switch (column.Type)
            {
                case ColumnType.Name:
                    return 250; // Reasonable default for file names
                case ColumnType.Size:
                    return 100; // Enough for "999.9 MB"
                case ColumnType.Type:
                    return 120; // Enough for most file type descriptions
                case ColumnType.DateModified:
                case ColumnType.DateCreated:
                case ColumnType.DateAccessed:
                    return 150; // Enough for date/time display
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
            foreach (var column in VisibleColumns)
            {
                AutoSizeColumn(column.Name);
            }
        }

        #endregion

        #region Theme Support

        public void RefreshColumnTheme()
        {
            if (_parentControl == null) return;
            
            try
            {
                var headerPresenter = FindVisualChild<GridViewHeaderRowPresenter>(_parentControl);
                if (headerPresenter != null)
                {
                    // Update header presenter background
                    var parent = VisualTreeHelper.GetParent(headerPresenter) as Border;
                    if (parent != null)
                    {
                        parent.Background = GetThemeResource<Brush>("BackgroundColor");
                        parent.BorderBrush = GetThemeResource<Brush>("BorderColor");
                    }
                    
                    // Update column headers
                    foreach (var header in FindVisualChildren<GridViewColumnHeader>(headerPresenter))
                    {
                        header.Background = GetThemeResource<Brush>("BackgroundColor");
                        header.Foreground = GetThemeResource<Brush>("TextColor");
                        header.BorderBrush = GetThemeResource<Brush>("BorderColor");
                        
                        // Update resize grippers
                        var thumbs = FindVisualChildren<Thumb>(header);
                        foreach (var thumb in thumbs)
                        {
                            var divider = FindVisualChild<Rectangle>(thumb);
                            if (divider != null)
                            {
                                divider.Fill = GetThemeResource<Brush>("BorderColor");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COLUMNS] Error refreshing theme: {ex.Message}");
            }
        }

        private T GetThemeResource<T>(string resourceKey) where T : class
        {
            try
            {
                return ThemeManager.Instance.GetResource<T>(resourceKey);
            }
            catch
            {
                return default(T);
            }
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
                
                _settingsManager.UpdateSetting($"{SETTINGS_KEY_PREFIX}", settings);
                _isDirty = false;
                
                System.Diagnostics.Debug.WriteLine("[COLUMNS] Settings saved");
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
                var settings = _settingsManager.GetSetting<Dictionary<string, object>>($"{SETTINGS_KEY_PREFIX}", null);
                if (settings == null) return;
                
                foreach (var kvp in settings)
                {
                    var column = GetColumn(kvp.Key);
                    if (column == null) continue;
                    
                    if (kvp.Value is Dictionary<string, object> columnSettings)
                    {
                        if (columnSettings.TryGetValue("width", out var width))
                        {
                            column.Width = Convert.ToDouble(width);
                        }
                        
                        if (columnSettings.TryGetValue("visible", out var visible))
                        {
                            column.IsVisible = Convert.ToBoolean(visible);
                        }
                        
                        if (columnSettings.TryGetValue("index", out var index))
                        {
                            column.DisplayIndex = Convert.ToInt32(index);
                        }
                    }
                }
                
                // Reinitialize GridView if it exists
                if (_gridView != null)
                {
                    InitializeColumns(_gridView);
                }
                
                System.Diagnostics.Debug.WriteLine("[COLUMNS] Settings loaded");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COLUMNS] Error loading settings: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private void OnColumnWidthChanged(string columnName, double oldWidth, double newWidth)
        {
            _isDirty = true;
            ColumnWidthChanged?.Invoke(this, new ColumnWidthChangedEventArgs(columnName, oldWidth, newWidth));
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t)
                    return t;
                
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            
            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t)
                    yield return t;
                
                foreach (var grandChild in FindVisualChildren<T>(child))
                {
                    yield return grandChild;
                }
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            
            return current as T;
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
                    _gridViewColumns.Clear();
                    _resizeStates.Clear();
                }
                
                _disposed = true;
            }
        }

        #endregion

        #region Private Classes

        private class ColumnResizeState
        {
            public FileTreeColumnDefinition Column { get; set; }
            public double OriginalWidth { get; set; }
        }

        #endregion
    }
}