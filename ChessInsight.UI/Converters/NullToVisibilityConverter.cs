using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChessInsight.UI.Converters
{
    /// <summary>
    /// null → Visible (prikaži unicode fallback), non-null → Collapsed (SVG postoji)
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value == null ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
