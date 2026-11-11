namespace Forge.Training
{
    /// <summary>
    /// Decision 18: a block forges its item if at least 75% of its training days were completed,
    /// judged at the end of the block.
    /// </summary>
    public static class Forging
    {
        public const double Threshold = 0.75;

        public static int TotalDays(int trainingDaysPerWeek) => trainingDaysPerWeek * ProgramDefinition.WeeksPerBlock;

        /// <summary>Training days needed to forge: 12 of 16 on a 4-day plan, 9 of 12 on a 3-day one.</summary>
        public static int Needed(int trainingDaysPerWeek) => (int)Math.Ceiling(TotalDays(trainingDaysPerWeek) * Threshold);

        public static bool Forges(int daysDone, int trainingDaysPerWeek) => daysDone >= Needed(trainingDaysPerWeek);
    }
}
