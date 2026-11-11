namespace Forge.Training
{
    /// <summary>
    /// Published norms, shown only as a coarse band and only where the table is credible and the
    /// protocol matches ours (Decision 23). Scores never use these.
    /// </summary>
    public static class Norms
    {
        public const string PushUpSource = "CSEP-PATH (Canadian Society for Exercise Physiology, 2003), as adopted by ACSM";

        // Lower bounds for Excellent, Very good, Good, Fair (below Fair: Needs improvement),
        // per age band 20–29, 30–39, 40–49, 50–59, 60–69. Men: full push-ups from the floor.
        // Women: knee push-ups. To failure, no rest. (One cell in the source handout, women 40–49
        // Very good, reads 15–26 against Excellent ≥ 24; the band edges use the lower bounds only.)
        private static readonly int[][] MenFloor =
        {
            new[] { 36, 29, 22, 17 }, new[] { 30, 22, 17, 12 }, new[] { 25, 17, 13, 10 }, new[] { 21, 13, 10, 7 }, new[] { 18, 11, 8, 5 }
        };
        private static readonly int[][] WomenKnees =
        {
            new[] { 30, 21, 15, 10 }, new[] { 27, 20, 13, 8 }, new[] { 24, 15, 11, 5 }, new[] { 21, 11, 7, 2 }, new[] { 17, 12, 5, 2 }
        };
        private static readonly string[] Bands = { "Excellent", "Very good", "Good", "Fair", "Needs improvement" };

        /// <summary>
        /// The CSEP band for a push-up result, or null when the norms don't apply: no group chosen,
        /// age outside 20–69, or a version of the test the table wasn't built for.
        /// </summary>
        /// <param name="group">"men" or "women".</param>
        /// <param name="version">Where the push-ups were done: "Floor", "Knees", "Hands on a box or step"…</param>
        public static string? PushUpBand(string group, int age, string version, int reps)
        {
            if (age is < 20 or > 69) return null;
            var table = group switch
            {
                "men" when version == "Floor" => MenFloor,
                "women" when version == "Knees" => WomenKnees,
                _ => null
            };
            if (table is null) return null;
            var row = table[(age - 20) / 10];
            for (var i = 0; i < row.Length; i++)
                if (reps >= row[i]) return Bands[i];
            return Bands[^1];
        }

        public static string AgeBand(int age) => $"{age / 10 * 10}–{age / 10 * 10 + 9}";
    }
}
