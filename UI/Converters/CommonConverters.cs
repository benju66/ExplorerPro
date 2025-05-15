// Create in UI/Converters/CommonConverters.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerPro.UI.Converters
{
    /// <summary>
    /// Converter that returns Visibility.Collapsed when count equals 1, Visibility.Visible otherwise
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count <= 1 ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter that returns false when count equals 1, true otherwise
    /// </summary>
    public class CountToEnableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 1;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}