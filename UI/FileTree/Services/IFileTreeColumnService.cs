// UI/FileTree/Services/IFileTreeColumnService.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ExplorerPro.UI.FileTree.Models;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Service interface for managing file tree column definitions, widths, visibility, and persistence
    /// </summary>
    public interface IFileTreeColumnService
    {
        #region Events

        /// <summary>
        /// Raised when a column width changes
        /// </summary>
        event EventHandler<ColumnWidthChangedEventArgs> ColumnWidthChanged;

        /// <summary>
        /// Raised when column visibility changes
        /// </summary>
        event EventHandler<ColumnVisibilityChangedEventArgs> ColumnVisibilityChanged;

        /// <summary>
        /// Raised when columns are reordered
        /// </summary>
        event EventHandler<ColumnsReorderedEventArgs> ColumnsReordered;

        #endregion

        #region Properties

        /// <summary>
        /// Gets all column definitions
        /// </summary>
        IReadOnlyList<FileTreeColumnDefinition> Columns { get; }

        /// <summary>
        /// Gets only the visible columns in display order
        /// </summary>
        IReadOnlyList<FileTreeColumnDefinition> VisibleColumns { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes columns in the specified GridView
        /// </summary>
        /// <param name="gridView">The GridView to initialize</param>
        void InitializeColumns(GridView gridView);

        /// <summary>
        /// Saves current column settings to persistent storage
        /// </summary>
        void SaveColumnSettings();

        /// <summary>
        /// Loads column settings from persistent storage
        /// </summary>
        void LoadColumnSettings();

        /// <summary>
        /// Gets a column definition by name
        /// </summary>
        /// <param name="columnName">The column name</param>
        /// <returns>The column definition or null if not found</returns>
        FileTreeColumnDefinition GetColumn(string columnName);

        /// <summary>
        /// Updates the width of a specific column
        /// </summary>
        /// <param name="columnName">The column name</param>
        /// <param name="newWidth">The new width</param>
        void UpdateColumnWidth(string columnName, double newWidth);

        /// <summary>
        /// Shows or hides a column
        /// </summary>
        /// <param name="columnName">The column name</param>
        /// <param name="isVisible">Whether the column should be visible</param>
        void SetColumnVisibility(string columnName, bool isVisible);

        /// <summary>
        /// Resets all columns to their default settings
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// Reorders columns based on new display indices
        /// </summary>
        /// <param name="newOrder">Dictionary mapping column names to new display indices</param>
        void ReorderColumns(Dictionary<string, int> newOrder);

        /// <summary>
        /// Makes columns resizable by attaching appropriate event handlers
        /// </summary>
        /// <param name="control">The control containing the columns</param>
        void MakeColumnsResizable(UIElement control);

        /// <summary>
        /// Refreshes column styling based on the current theme
        /// </summary>
        void RefreshColumnTheme();

        /// <summary>
        /// Gets the optimal width for a column based on its content
        /// </summary>
        /// <param name="columnName">The column name</param>
        /// <returns>The optimal width</returns>
        double GetOptimalColumnWidth(string columnName);

        /// <summary>
        /// Auto-sizes a column to fit its content
        /// </summary>
        /// <param name="columnName">The column name</param>
        void AutoSizeColumn(string columnName);

        /// <summary>
        /// Auto-sizes all visible columns
        /// </summary>
        void AutoSizeAllColumns();

        #endregion
    }

    #region Event Arguments

    /// <summary>
    /// Event arguments for column width change events
    /// </summary>
    public class ColumnWidthChangedEventArgs : EventArgs
    {
        public string ColumnName { get; }
        public double OldWidth { get; }
        public double NewWidth { get; }

        public ColumnWidthChangedEventArgs(string columnName, double oldWidth, double newWidth)
        {
            ColumnName = columnName;
            OldWidth = oldWidth;
            NewWidth = newWidth;
        }
    }

    /// <summary>
    /// Event arguments for column visibility change events
    /// </summary>
    public class ColumnVisibilityChangedEventArgs : EventArgs
    {
        public string ColumnName { get; }
        public bool IsVisible { get; }

        public ColumnVisibilityChangedEventArgs(string columnName, bool isVisible)
        {
            ColumnName = columnName;
            IsVisible = isVisible;
        }
    }

    /// <summary>
    /// Event arguments for columns reordered events
    /// </summary>
    public class ColumnsReorderedEventArgs : EventArgs
    {
        public IReadOnlyDictionary<string, int> NewOrder { get; }

        public ColumnsReorderedEventArgs(Dictionary<string, int> newOrder)
        {
            NewOrder = new Dictionary<string, int>(newOrder);
        }
    }

    #endregion
}