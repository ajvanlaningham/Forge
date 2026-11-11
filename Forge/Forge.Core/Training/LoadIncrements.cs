using Forge.Constants;
using Forge.Models;

namespace Forge.Training
{
    /// <summary>
    /// The smallest sensible load jump per kind of equipment, in the user's display unit. Used by
    /// the +/− steppers while logging and by progressive overload (PBI 8.5).
    /// </summary>
    /// <remarks>
    /// Kettlebells jump in bell sizes (4 kg / ~9 lb), not plates. Dumbbells and barbells use the
    /// common 5 lb / 2.5 kg steps. Loads are always <i>stored</i> in pounds; this only decides the
    /// step in whichever unit is on screen, so a kg user steps 2.5 kg rather than 2.27.
    /// </remarks>
    public static class LoadIncrements
    {
        public static double StepFor(Modality modality, WeightUnit unit) => (modality, unit) switch
        {
            (Modality.Kettlebell, WeightUnit.Kilograms) => 4.0,
            (Modality.Kettlebell, _) => 9.0,
            (_, WeightUnit.Kilograms) => 2.5,
            _ => 5.0
        };

        /// <summary>Rounds a display-unit load to the nearest step (never below zero).</summary>
        public static double Snap(double displayLoad, double step)
            => step <= 0 ? Math.Max(0, displayLoad) : Math.Max(0, Math.Round(displayLoad / step) * step);
    }
}
