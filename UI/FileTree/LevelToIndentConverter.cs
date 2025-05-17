using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Converts a TreeView item's hierarchical level to an indentation width
    /// </summary>
    [ValueConversion(typeof(int), typeof(double))]
    public class LevelToIndentConverter : IValueConverter
    {
        // Base indentation per level (in pixels)
        private const double IndentationPerLevel = 19.0;
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Get the level from a TreeViewItem
            if (value is int level)
            {
                // Calculate indentation based on level
                return IndentationPerLevel * level;
            }
            
            // Default indentation for level 0
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter doesn't support two-way binding
            return DependencyProperty.UnsetValue;
        }
    }
}