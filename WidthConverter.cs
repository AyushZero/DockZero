using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DockZero
{
    public class WidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string ratio)
            {
                double multiplier = double.Parse(ratio);
                return new Point(width * multiplier, 5);
            }
            return new Point(0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 