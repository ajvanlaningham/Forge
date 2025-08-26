using System.Globalization;

namespace Forge.Converters
{
    /// <summary>
    /// Converts a null → false, not-null → true (inverse of null check).
    /// </summary>
    public sealed class NullToBoolInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
