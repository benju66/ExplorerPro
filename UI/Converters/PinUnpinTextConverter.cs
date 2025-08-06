using System;
using System.Globalization;
using System.Windows.Data;

namespace ExplorerPro.UI.Converters
{
    /// <summary>
    /// Converter to display "Pin Tab" or "Unpin Tab" based on the pinned state
    /// </summary>
    public class PinUnpinTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPinned)
            {
                return isPinned ? "Unpin Tab" : "Pin Tab";
            }
            return "Pin Tab";
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}