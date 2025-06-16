using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ExplorerPro.UI.Converters
{
    /// <summary>
    /// Converter that transforms Color values to SolidColorBrush objects
    /// Used for binding tab colors to visual elements
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        /// <summary>
        /// Converts a Color value to a SolidColorBrush
        /// </summary>
        /// <param name="value">The Color value to convert</param>
        /// <param name="targetType">The target type (should be Brush)</param>
        /// <param name="parameter">Optional parameter for conversion customization</param>
        /// <param name="culture">Culture information</param>
        /// <returns>A SolidColorBrush created from the Color value</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                // Apply opacity if specified in parameter
                if (parameter is string opacityStr && double.TryParse(opacityStr, out double opacity))
                {
                    color.A = (byte)(255 * Math.Clamp(opacity, 0.0, 1.0));
                }
                
                return new SolidColorBrush(color);
            }

            // Fallback for string color names
            if (value is string colorName)
            {
                try
                {
                    var colorProperty = typeof(Colors).GetProperty(colorName);
                    if (colorProperty != null)
                    {
                        var colorValue = (Color)colorProperty.GetValue(null);
                        return new SolidColorBrush(colorValue);
                    }
                }
                catch
                {
                    // Fall through to default
                }
            }

            // Default fallback
            return new SolidColorBrush(Colors.LightGray);
        }

        /// <summary>
        /// Converts a SolidColorBrush back to a Color value
        /// </summary>
        /// <param name="value">The Brush value to convert back</param>
        /// <param name="targetType">The target type (should be Color)</param>
        /// <param name="parameter">Optional parameter for conversion customization</param>
        /// <param name="culture">Culture information</param>
        /// <returns>The Color value from the Brush</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return Colors.LightGray;
        }
    }

    /// <summary>
    /// Converter that creates a darker shade of a color for borders and accents
    /// </summary>
    public class ColorToDarkerBrushConverter : IValueConverter
    {
        /// <summary>
        /// Converts a Color to a darker SolidColorBrush
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                // Calculate darker color by reducing RGB values
                double factor = 0.8; // 20% darker
                if (parameter is string factorStr && double.TryParse(factorStr, out double customFactor))
                {
                    factor = Math.Clamp(customFactor, 0.1, 1.0);
                }

                var darkerColor = Color.FromArgb(
                    color.A,
                    (byte)(color.R * factor),
                    (byte)(color.G * factor),
                    (byte)(color.B * factor)
                );

                return new SolidColorBrush(darkerColor);
            }

            return new SolidColorBrush(Colors.Gray);
        }

        /// <summary>
        /// Converts back (not implemented for this one-way converter)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ColorToDarkerBrushConverter is a one-way converter");
        }
    }

    /// <summary>
    /// Converter that determines if a color is light or dark and returns appropriate foreground color
    /// </summary>
    public class ColorToForegroundConverter : IValueConverter
    {
        /// <summary>
        /// Converts a Color to an appropriate foreground Brush (black for light colors, white for dark colors)
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                // Calculate luminance using standard formula
                double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
                
                // Return black for light colors, white for dark colors
                return luminance > 0.5 ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.White);
            }

            return new SolidColorBrush(Colors.Black);
        }

        /// <summary>
        /// Converts back (not implemented for this one-way converter)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ColorToForegroundConverter is a one-way converter");
        }
    }

    /// <summary>
    /// Converter that transforms a boolean IsPinned value to appropriate visual indicators
    /// </summary>
    public class PinnedToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean IsPinned value to Visibility
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPinned)
            {
                return isPinned ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }

            return System.Windows.Visibility.Collapsed;
        }

        /// <summary>
        /// Converts back from Visibility to boolean
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Visibility visibility)
            {
                return visibility == System.Windows.Visibility.Visible;
            }

            return false;
        }
    }

    /// <summary>
    /// Converter that checks if a color is not the default color (LightGray)
    /// </summary>
    public class IsNotDefaultColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts a Color to a boolean indicating if it's not the default color
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                // Check if color is not the default LightGray or Transparent
                return color != Colors.LightGray && color != Colors.Transparent && color.A > 0;
            }

            return false;
        }

        /// <summary>
        /// Converts back (not implemented for this one-way converter)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("IsNotDefaultColorConverter is a one-way converter");
        }
    }
} 