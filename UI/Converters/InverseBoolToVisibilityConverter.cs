using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerPro.UI.Converters
{
    /// <summary>
    /// Converts boolean values to Visibility enumeration values with inverse logic
    /// True becomes Collapsed, False becomes Visible
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Collapsed;
            }
            return true;
        }
    }
}