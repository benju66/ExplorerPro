// UI/FileTree/Converters.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Converts boolean values to visibility
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            
            return false;
        }
    }

    /// <summary>
    /// Converts boolean values to visibility (inverted)
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            
            return true;
        }
    }

    /// <summary>
    /// Converts expanded state to arrow text
    /// </summary>
    [ValueConversion(typeof(bool), typeof(string))]
    public class ExpanderTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "▼" : "▶"; // Down triangle when expanded, right triangle when collapsed
            }
            
            return "▶"; // Default to collapsed symbol
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts tree level to left margin for indentation
    /// </summary>
    [ValueConversion(typeof(int), typeof(Thickness))]
    public class LevelToIndentConverter : IValueConverter
    {
        private const double IndentationPerLevel = 19.0;
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                // Return left margin only - other columns will stay aligned
                return new Thickness(IndentationPerLevel * level, 0, 0, 0);
            }
            
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts tree level to expander position (to the right of connecting lines)
    /// </summary>
    [ValueConversion(typeof(int), typeof(Thickness))]
    public class ExpanderRightIndentConverter : IValueConverter
    {
        private const double IndentationPerLevel = 19.0;
        private const double LineSpace = 25.0; // Extra space to move expander right of the line
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                // Position expander to the right of the connecting lines
                double leftMargin = (IndentationPerLevel * level) + LineSpace;
                return new Thickness(leftMargin, 0, 0, 0);
            }
            
            return new Thickness(LineSpace, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}