using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChessInsight.UI.Converters
{
    public class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => new GridLength(value is double d ? d : 50.0, GridUnitType.Star);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
