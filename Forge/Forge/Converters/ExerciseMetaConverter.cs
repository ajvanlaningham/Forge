using System.Globalization;
using Forge.Models;

namespace Forge.Converters
{
    public sealed class ExerciseMetaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Exercise e)
                return $"{e.Category} • {e.BodyZone} • {e.Modality}";
            return "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
