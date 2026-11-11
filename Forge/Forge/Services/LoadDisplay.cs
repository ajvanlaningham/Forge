using Forge.Constants;

namespace Forge.Services
{
    /// <summary>
    /// Loads as you'd say them: a kilo bell you own reads "16 kg" even when you train in pounds,
    /// instead of "35.3 lb" (GearLoads.LoadLabel). Used wherever a single set's load is shown.
    /// </summary>
    public static class LoadDisplay
    {
        /// <summary>"16 kg", "25 lb".</summary>
        public static string Label(double lb)
            => UserSettings.HomeGear.LoadLabel(lb, UserSettings.WeightUnit);

        /// <summary>Compact: just the number in your display unit ("25"), with the unit only when it differs ("16 kg").</summary>
        public static string Short(double lb)
        {
            var label = Label(lb);
            var unit = " " + WeightMath.UnitLabel(UserSettings.WeightUnit);
            return label.EndsWith(unit, StringComparison.Ordinal) ? label[..^unit.Length] : label;
        }
    }
}
