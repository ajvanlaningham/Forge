namespace Forge.Common
{
    /// <summary>
    /// Date helpers shared by the quest and conditioning services. Weeks run Monday to Sunday.
    /// Pure functions so the boundary behaviour is unit-testable.
    /// </summary>
    public static class WeekMath
    {
        /// <summary>Canonical key format for a day: <c>yyyy-MM-dd</c>.</summary>
        public static string DateKey(DateOnly date) => date.ToString("yyyy-MM-dd");

        /// <summary>
        /// The Monday on or before <paramref name="anyDay"/>. Sunday belongs to the week that
        /// started six days earlier, not the one starting tomorrow.
        /// </summary>
        public static DateOnly MondayOf(DateOnly anyDay)
        {
            // DayOfWeek is Sunday=0 .. Saturday=6, so shift to make Monday=0 and Sunday=6.
            int diff = ((int)anyDay.DayOfWeek + 6) % 7;
            return anyDay.AddDays(-diff);
        }

        /// <summary>The Sunday closing the week that <paramref name="anyDay"/> falls in.</summary>
        public static DateOnly SundayOf(DateOnly anyDay) => MondayOf(anyDay).AddDays(6);

        /// <summary>Canonical key for the week containing <paramref name="anyDay"/>.</summary>
        public static string WeekKey(DateOnly anyDay) => DateKey(MondayOf(anyDay));
    }
}
