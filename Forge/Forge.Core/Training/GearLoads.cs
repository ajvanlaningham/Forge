using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Constants;
using Forge.Models;

namespace Forge.Training
{
    /// <summary>
    /// Fixed weights owned, one entry per implement: a weight listed twice is a pair. Pounds and kilos
    /// are kept apart because a home collection is often mixed (5/10/15 lb bells plus 16/20 kg ones).
    /// </summary>
    public abstract class FixedWeights
    {
        public List<double> Lb { get; set; } = new();
        public List<double> Kg { get; set; } = new();

        /// <summary>
        /// Before mixed units: one list in a single unit. Read from old settings and folded into
        /// <see cref="Lb"/>/<see cref="Kg"/> by <see cref="GearLoads.FromJson"/>; never written.
        /// </summary>
        public List<double>? Weights { get; set; }

        /// <summary>The list for a unit.</summary>
        public List<double> In(WeightUnit unit) => unit == WeightUnit.Kilograms ? Kg : Lb;

        public bool Any => Lb.Count > 0 || Kg.Count > 0;

        internal void Migrate(WeightUnit legacyUnit)
        {
            if (Weights is { Count: > 0 }) In(legacyUnit).AddRange(Weights);
            Weights = null;
        }
    }

    /// <summary>Dumbbells you own: fixed weights (lb and/or kg), or one adjustable set in <see cref="Unit"/>.</summary>
    public sealed class DumbbellGear : FixedWeights
    {
        public bool Adjustable { get; set; }
        /// <summary>The adjustable set's unit.</summary>
        public WeightUnit Unit { get; set; } = WeightUnit.Pounds;
        /// <summary>Adjustable range.</summary>
        public double Min { get; set; } = 5;
        public double Max { get; set; } = 50;
        public double Step { get; set; } = 2.5;
    }

    /// <summary>Kettlebells you own, lb and/or kg; one entry per bell (listed twice = a pair).</summary>
    public sealed class KettlebellGear : FixedWeights
    {
        /// <summary>Unit of the legacy <see cref="FixedWeights.Weights"/> list only.</summary>
        public WeightUnit? Unit { get; set; }
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

        /// <summary>
        /// One-line summary of owned weights for an implement, for the My Gear tile:
        /// "15 · 25 · 35 lb", "5–52.5 lb adjustable", "16 · 24 kg", "bar 45 · up to 225 lb".
        /// Null when nothing is entered.
        /// </summary>
        public string? Summary(Equipment implement)
        {
            static string F(double v) => v.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
            return implement switch
            {
                Equipment.Dumbbell when Dumbbells is { Adjustable: true } d
                    => $"{F(d.Min)}–{F(d.Max)} {WeightMath.UnitLabel(d.Unit)} adjustable",
                Equipment.Dumbbell when Dumbbells is { Any: true } d => Counted(d),
                Equipment.Kettlebell when Kettlebells is { Any: true } k => Counted(k),
                Equipment.Barbell when Barbell is { } b
                    => $"bar {F(b.Bar)} · up to {F(b.Max)} {WeightMath.UnitLabel(b.Unit)}",
                _ => null
            };
        }

        /// <summary>
        /// Fits a small tile: a few weights in one unit are listed ("15×2 · 25 · 35×2 lb"); a longer or
        /// mixed collection shows each unit's range ("5–15 lb + 16–20 kg", "5–30 lb").
        /// </summary>
        private static string Counted(FixedWeights f)
        {
            var units = new[] { WeightUnit.Pounds, WeightUnit.Kilograms }.Where(u => f.In(u).Count > 0).ToList();
            if (units.Count == 1 && f.In(units[0]).Distinct().Count() <= 4)
                return Counted(f.In(units[0])) + " " + WeightMath.UnitLabel(units[0]);
            return string.Join(" + ", units.Select(u => Range(f.In(u)) + " " + WeightMath.UnitLabel(u)));
        }

        private static string Range(List<double> weights)
        {
            static string F(double v) => v.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
            var (min, max) = (weights.Min(), weights.Max());
            return min == max ? F(min) : $"{F(min)}–{F(max)}";
        }

        private static string Counted(List<double> weights)
            => string.Join(" · ", weights.GroupBy(w => w).OrderBy(g => g.Key).Select(g =>
                g.Key.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) + (g.Count() > 1 ? $"×{g.Count()}" : "")));

        /// <summary>
        /// A load as you'd say it: a kilo bell you own reads "16 kg" even when you train in pounds,
        /// instead of "35.3 lb". Anything else is shown in <paramref name="unit"/>.
        /// </summary>
        public string LoadLabel(double lb, WeightUnit unit)
        {
            static string F(double v) => v.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);
            foreach (var f in new FixedWeights?[] { Kettlebells, Dumbbells })
            {
                if (f is null) continue;
                if (f.Kg.FirstOrDefault(k => Math.Abs(Lb(k, WeightUnit.Kilograms) - lb) < 0.01) is var kg and > 0)
                    return $"{F(kg)} kg";
                if (f.Lb.FirstOrDefault(p => Math.Abs(p - lb) < 0.01) is var lbs and > 0)
                    return $"{F(lbs)} lb";
            }
            return $"{F(WeightMath.ToDisplay(lb, unit))} {WeightMath.UnitLabel(unit)}";
        }

        /// <summary>Implements whose weights matter: tapping them opens a weights editor.</summary>
        public static bool IsWeighted(Equipment e) => e is Equipment.Dumbbell or Equipment.Kettlebell or Equipment.Barbell;

        /// <summary>The loads an exercise's modality can use here.</summary>
        /// <param name="needsPair">The exercise uses two implements (DB bench press, curls): only
        /// weights owned as a pair qualify. One-implement exercises (goblet squat, one-arm row) can
        /// use any. If no pair is owned at all, every owned weight is offered rather than nothing.</param>
        public LoadSet For(Modality modality, WeightUnit displayUnit, bool needsPair = false) => modality switch
        {
            Modality.Dumbbell => Dumbbells switch
            {
                null => Steps(displayUnit, min: 0),
                // An adjustable set is a pair of handles.
                { Adjustable: true } d => new LoadSet.Range(Lb(d.Min, d.Unit), Lb(d.Max, d.Unit), d.Step, d.Unit),
                { Any: false } => Steps(displayUnit, min: 0),
                var d => new LoadSet.Discrete(Usable(d, needsPair))
            },
            Modality.Kettlebell => Kettlebells is { Any: true } k
                ? new LoadSet.Discrete(Usable(k, needsPair))
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
                Kettlebells = new KettlebellGear { Kg = StandardKettlebellsKg.ToList() }
            }
            : new GearLoads
            {
                Kettlebells = new KettlebellGear { Lb = StandardKettlebellsLb.ToList() }
            };

        /// <summary>
        /// Owned weights an exercise can use, in pounds. A pair has to be two of the same weight in
        /// the same unit (a 10 lb and a 4.5 kg bell are not a pair).
        /// </summary>
        private static IEnumerable<double> Usable(FixedWeights f, bool needsPair)
        {
            var all = Each(f).ToList();
            if (!needsPair) return all.Select(x => x.Lb).Distinct();
            var pairs = all.GroupBy(x => (x.W, x.U)).Where(g => g.Count() >= 2).Select(g => g.First().Lb).ToList();
            return pairs.Count > 0 ? pairs : all.Select(x => x.Lb).Distinct();
        }

        private static IEnumerable<(double W, WeightUnit U, double Lb)> Each(FixedWeights f)
            => f.Lb.Select(w => (w, WeightUnit.Pounds, w))
                .Concat(f.Kg.Select(w => (w, WeightUnit.Kilograms, Lb(w, WeightUnit.Kilograms))));

        /// <summary>How many of a weight are owned (0, 1, 2…).</summary>
        public static int CountOf(List<double> owned, double weight) => owned.Count(w => Math.Abs(w - weight) < 0.001);

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
            GearLoads gear;
            try { gear = JsonSerializer.Deserialize<GearLoads>(json, Json) ?? new GearLoads(); }
            catch (JsonException) { return new GearLoads(); }
            // Settings saved before mixed units had one list in one unit.
            gear.Dumbbells?.Migrate(gear.Dumbbells.Unit);
            gear.Kettlebells?.Migrate(gear.Kettlebells.Unit ?? WeightUnit.Kilograms);
            if (gear.Kettlebells != null) gear.Kettlebells.Unit = null;
            return gear;
        }
    }
}
