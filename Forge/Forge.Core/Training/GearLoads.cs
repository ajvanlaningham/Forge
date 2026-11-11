using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Constants;
using Forge.Models;

namespace Forge.Training
{
    /// <summary>Dumbbells you own: fixed pairs, or one adjustable set. Values are in <see cref="Unit"/>.</summary>
    public sealed class DumbbellGear
    {
        public bool Adjustable { get; set; }
        public WeightUnit Unit { get; set; } = WeightUnit.Pounds;
        /// <summary>Fixed pairs (one handle's weight each).</summary>
        public List<double> Weights { get; set; } = new();
        /// <summary>Adjustable range.</summary>
        public double Min { get; set; } = 5;
        public double Max { get; set; } = 50;
        public double Step { get; set; } = 2.5;
    }

    /// <summary>Kettlebells you own, in <see cref="Unit"/>.</summary>
    public sealed class KettlebellGear
    {
        public WeightUnit Unit { get; set; } = WeightUnit.Kilograms;
        public List<double> Weights { get; set; } = new();
    }

    /// <summary>A barbell: bar weight, heaviest load you can make, smallest jump (a pair of the smallest plates).</summary>
    public sealed class BarbellGear
    {
        public WeightUnit Unit { get; set; } = WeightUnit.Pounds;
        public double Bar { get; set; } = 45;
        public double Max { get; set; } = 225;
        public double Step { get; set; } = 5;
    }

    /// <summary>
    /// The loads available at one location (Decision 16). A null section means "not specified":
    /// at Home that falls back to plain 5 lb / 2.5 kg steps until the user enters their weights;
    /// at the Gym it means everything.
    /// </summary>
    public sealed class GearLoads
    {
        public DumbbellGear? Dumbbells { get; set; }
        public KettlebellGear? Kettlebells { get; set; }
        public BarbellGear? Barbell { get; set; }

        // Standard sizes for the pickers.
        public static readonly double[] StandardDumbbellsLb = { 3, 5, 8, 10, 12, 15, 17.5, 20, 22.5, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 90, 100 };
        public static readonly double[] StandardDumbbellsKg = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18, 20, 22.5, 25, 27.5, 30, 32.5, 35, 40, 45 };
        public static readonly double[] StandardKettlebellsKg = { 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 28, 32, 36, 40, 44, 48 };
        public static readonly double[] StandardKettlebellsLb = { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 53, 60, 62, 70, 80, 88, 97, 106 };

        /// <summary>The loads an exercise's modality can use here.</summary>
        public LoadSet For(Modality modality, WeightUnit displayUnit) => modality switch
        {
            Modality.Dumbbell => Dumbbells switch
            {
                null => Steps(displayUnit, min: 0),
                { Adjustable: true } d => new LoadSet.Range(Lb(d.Min, d.Unit), Lb(d.Max, d.Unit), d.Step, d.Unit),
                { Weights.Count: 0 } => Steps(displayUnit, min: 0),
                var d => new LoadSet.Discrete(d.Weights.Select(w => Lb(w, d.Unit)))
            },
            Modality.Kettlebell => Kettlebells is { Weights.Count: > 0 } k
                ? new LoadSet.Discrete(k.Weights.Select(w => Lb(w, k.Unit)))
                : Steps(displayUnit, min: 0),
            Modality.Barbell => Barbell is { } b
                ? new LoadSet.Range(Lb(b.Bar, b.Unit), Lb(b.Max, b.Unit), b.Step, b.Unit)
                : new LoadSet.Range(displayUnit == WeightUnit.Kilograms ? Lb(20, WeightUnit.Kilograms) : 45, null,
                    displayUnit == WeightUnit.Kilograms ? 2.5 : 5, displayUnit),
            _ => Steps(displayUnit, min: 0) // machines, cables, load added to bodyweight moves
        };

        /// <summary>
        /// Everything you could ever want (the Gym, Decision 16): unbounded dumbbell and barbell ranges
        /// in the display unit's usual steps, and every standard kettlebell.
        /// </summary>
        public static GearLoads Everything(WeightUnit unit) => unit == WeightUnit.Kilograms
            ? new GearLoads
            {
                Kettlebells = new KettlebellGear { Unit = WeightUnit.Kilograms, Weights = StandardKettlebellsKg.ToList() }
            }
            : new GearLoads
            {
                Kettlebells = new KettlebellGear { Unit = WeightUnit.Pounds, Weights = StandardKettlebellsLb.ToList() }
            };

        private static LoadSet Steps(WeightUnit unit, double min)
            => new LoadSet.Range(min, null, unit == WeightUnit.Kilograms ? 2.5 : 5, unit);

        private static double Lb(double v, WeightUnit u) => WeightMath.ToStoredPounds(v, u);

        // --- persistence ---

        private static readonly JsonSerializerOptions Json = new()
        {
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public string ToJson() => JsonSerializer.Serialize(this, Json);

        public static GearLoads FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new GearLoads();
            try { return JsonSerializer.Deserialize<GearLoads>(json, Json) ?? new GearLoads(); }
            catch (JsonException) { return new GearLoads(); }
        }
    }
}
