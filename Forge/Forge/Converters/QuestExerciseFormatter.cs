using System.Globalization;
using Forge.Models;

namespace Forge.Converters
{
    /// <summary>
    /// Formats a QuestExercise into a single display line, e.g.:
    /// "Goblet Squat — 3x10" or "Deep Squat Hold — 45s (5 breaths)"
    /// </summary>
    public sealed class QuestExerciseFormatter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not QuestExercise qx) return string.Empty;

            var name = qx.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return string.Empty;

            // Program prescription: "Barbell Row — 3 × 6–10" / "Plank — 3 × 45s"
            if (qx.Sets is int sets)
            {
                var per = qx.RepsMin is int lo && qx.RepsMax is int hi
                    ? (lo == hi ? $"{lo}" : $"{lo}–{hi}")
                    : qx.Seconds is int sec ? $"{sec}s" : "";
                return string.IsNullOrEmpty(per) ? $"{name} — {sets} sets" : $"{name} — {sets} × {per}";
            }

            string details = qx.Action switch
            {
                ActionType.Reps => qx.Reps is int r ? $"{r} reps" : "reps",
                ActionType.Time => qx.Seconds is int s ? $"{s}s" : "time",
                ActionType.Hold => FormatHold(qx),
                ActionType.Distance => qx.Distance is double d ? $"{d:g} m" : "distance",
                ActionType.Calories => qx.Reps is int c ? $"{c} cal" : "calories",
                _ => ""
            };

            return string.IsNullOrWhiteSpace(details) ? name : $"{name} — {details}";
        }

        private static string FormatHold(QuestExercise qx)
        {
            var hasSec = qx.Seconds is int s && s > 0;
            var hasBreaths = qx.Breaths is int b && b > 0;

            if (hasSec && hasBreaths) return $"{qx.Seconds}s ({qx.Breaths} breaths)";
            if (hasSec) return $"{qx.Seconds}s";
            if (hasBreaths) return $"{qx.Breaths} breaths";
            return "hold";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
