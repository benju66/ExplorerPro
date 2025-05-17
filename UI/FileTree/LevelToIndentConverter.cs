// UI/FileTree/LevelToIndentConverter.cs

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Converts a hierarchy level to an appropriate left margin for tree indentation
    /// </summary>
    [ValueConversion(typeof(int), typeof(Thickness))]
    public class LevelToIndentConverter : IValueConverter
    {
        /// <summary>
        /// Converts an integer level to a margin thickness
        /// </summary>
        /// <param name="value">The level value to convert (integer)</param>
        /// <param name="targetType">The type of the binding target property</param>
        /// <param name="parameter">Optional converter parameter</param>
        /// <param name="culture">Culture info</param>
        /// <returns>A Thickness with appropriate left margin</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int level = 0;
            
            // Try to extract level value
            if (value is int intValue)
            {
                level = intValue;
            }
            else if (value is FileTreeItem item)
            {
                // Calculate level based on path segments
                string path = item.Path;
                if (!string.IsNullOrEmpty(path))
                {
                    // Count path separators to estimate level
                    // This is a simple approach - might need refinement
                    int rootSeparators = GetRootPathSeparatorCount();
                    int pathSeparators = CountPathSeparators(path);
                    level = Math.Max(0, pathSeparators - rootSeparators);
                }
            }
            
            // Create margin with indentation (16 pixels per level)
            double indentSize = 16.0;
            return new Thickness(level * indentSize, 0, 0, 0);
        }

        /// <summary>
        /// Converts a margin thickness back to an integer level (not implemented)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
        
        /// <summary>
        /// Counts the number of path separators in a string
        /// </summary>
        private int CountPathSeparators(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;
                
            int count = 0;
            foreach (char c in path)
            {
                if (c == '\\' || c == '/')
                    count++;
            }
            
            return count;
        }
        
        /// <summary>
        /// Gets the typical number of separators in a root path
        /// </summary>
        private int GetRootPathSeparatorCount()
        {
            string rootExample = "C:\\";
            return CountPathSeparators(rootExample);
        }
    }
}
