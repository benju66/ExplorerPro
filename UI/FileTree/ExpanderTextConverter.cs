using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Converts IsExpanded boolean to an expander symbol
    /// </summary>
    [ValueConversion(typeof(bool), typeof(string))]
    public class ExpanderTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                // Unicode characters for triangle symbols
                return isExpanded ? "▼" : "▶"; // Down triangle when expanded, right triangle when collapsed
            }
            
            return "▶"; // Default to collapsed symbol
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter doesn't support two-way binding
            return DependencyProperty.UnsetValue;
        }
    }
}