using Forge.Constants;

namespace Forge.Training
{
    /// <summary>
    /// The loads actually available for an implement (Decision 16). All values are pounds — the
    /// storage unit — but a range steps in the unit it was defined in, so a kg barbell steps 2.5 kg,
    /// not 5.5 lb.
    /// </summary>
    public abstract record LoadSet
    {
        /// <summary>Smallest available load strictly above <paramref name="lb"/>; null when nothing is heavier.</summary>
        public abstract double? Next(double lb);

        /// <summary>Largest available load strictly below <paramref name="lb"/>; null when nothing is lighter.</summary>
        public abstract double? Prev(double lb);

        /// <summary>The available load nearest to <paramref name="lb"/> (ties go lighter).</summary>
        public abstract double Snap(double lb);

        /// <summary>Heaviest available load; null when unbounded.</summary>
        public abstract double? Max { get; }

        protected const double Eps = 1e-6;

        /// <summary>A fixed set of weights you own (dumbbell pairs, kettlebells).</summary>
        public sealed record Discrete : LoadSet
        {
            public IReadOnlyList<double> Loads { get; }

            public Discrete(IEnumerable<double> loadsLb)
                => Loads = loadsLb.Where(l => l >= 0).Distinct().OrderBy(l => l).ToList();

            public override double? Next(double lb) => Loads.Where(l => l > lb + Eps).Select(l => (double?)l).FirstOrDefault();
            public override double? Prev(double lb) => Loads.Where(l => l < lb - Eps).Select(l => (double?)l).LastOrDefault();
            public override double Snap(double lb)
                => Loads.Count == 0 ? lb : Loads.OrderBy(l => Math.Abs(l - lb)).ThenBy(l => l).First();
            public override double? Max => Loads.Count == 0 ? null : Loads[^1];
        }

        /// <summary>A range in fixed steps: adjustable dumbbells, a barbell (bar + plates), machines.</summary>
        /// <param name="MinLb">Lightest load (a barbell's bar weight).</param>
        /// <param name="MaxLb">Heaviest load, or null for "anything".</param>
        /// <param name="Step">Step size in <paramref name="Unit"/>.</param>
        public sealed record Range(double MinLb, double? MaxLb, double Step, WeightUnit Unit) : LoadSet
        {
            private double ToUnit(double lb) => WeightMath.ToDisplay(lb, Unit);
            private double ToLb(double v) => WeightMath.ToStoredPounds(v, Unit);
            private double MinU => ToUnit(MinLb);

            /// <summary>Index of the step at or below a load (in the range's unit).</summary>
            private double StepsAbove(double lb) => (ToUnit(lb) - MinU) / Step;

            public override double? Next(double lb)
            {
                if (lb < MinLb - Eps) return MinLb;
                var k = Math.Floor(StepsAbove(lb) + Eps) + 1;
                var next = ToLb(MinU + k * Step);
                return MaxLb is double max && next > max + Eps ? null : next;
            }

            public override double? Prev(double lb)
            {
                if (lb <= MinLb + Eps) return null;
                var k = Math.Ceiling(StepsAbove(lb) - Eps) - 1;
                var prev = ToLb(MinU + Math.Max(0, k) * Step);
                return MaxLb is double max && prev > max ? max : prev;
            }

            public override double Snap(double lb)
            {
                if (lb <= MinLb) return MinLb;
                var s = StepsAbove(lb);
                var lower = ToLb(MinU + Math.Floor(s + Eps) * Step);
                var upper = ToLb(MinU + Math.Ceiling(s - Eps) * Step);
                var pick = upper - lb < lb - lower - Eps ? upper : lower; // ties go lighter
                if (MaxLb is double max && pick > max + Eps)
                    pick = Prev(max + Eps) ?? MinLb; // highest step not above the max
                return pick;
            }

            public override double? Max => MaxLb is double max ? Snap(max) : null;
        }

        /// <summary>Unbounded 5 lb steps from zero: machines, cables, load added to bodyweight moves.</summary>
        public static readonly LoadSet Anything5Lb = new Range(0, null, 5, WeightUnit.Pounds);
    }
}
