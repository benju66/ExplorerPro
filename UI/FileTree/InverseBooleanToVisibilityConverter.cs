// UI/FileTree/InverseBooleanToVisibilityConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Converts a boolean value to a Visibility value (with the inverse of BooleanToVisibilityConverter)
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a Visibility value
        /// </summary>
        /// <param name="value">The boolean value to convert</param>
        /// <param name="targetType">The type of the binding target property</param>
        /// <param name="parameter">Optional converter parameter</param>
        /// <param name="culture">Culture info</param>
        /// <returns>Visibility.Visible if value is false, Visibility.Collapsed if value is true</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a Visibility value back to a boolean value
        /// </summary>
        /// <param name="value">The Visibility value to convert</param>
        /// <param name="targetType">The type of the binding target property</param>
        /// <param name="parameter">Optional converter parameter</param>
        /// <param name="culture">Culture info</param>
        /// <returns>false if Visibility.Visible, true otherwise</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            
            return true;
        }
    }
}