namespace Forge.Training
{
    /// <summary>
    /// The first quest suggested at onboarding: the starter quest of the chosen discipline, in the
    /// version that fits the gear (dumbbells → Dumbbell; kettlebells only → Kettlebell; neither →
    /// Dumbbell, whose swaps fall back to bodyweight moves) and the days per week chosen.
    /// </summary>
    public static class FirstQuest
    {
        public static string ProgramId(string discipline, bool hasDumbbells, bool hasKettlebell, int daysPerWeek)
        {
            var quest = discipline switch { "Anvil" => "copper-hammer", "Bellows" => "copper-boots", _ => "copper-helm" };
            var implement = !hasDumbbells && hasKettlebell ? "kb" : "db";
            var days = daysPerWeek <= 3 ? "3day" : "4day";
            return $"{quest}-{implement}-{days}";
        }

        /// <summary>The starter quest's title, as its program files name it (checked by FirstQuestTests).</summary>
        public static string Title(string discipline) => discipline switch
        {
            "Anvil" => "Iron Will",
            "Bellows" => "Second Wind",
            _ => "Foundations"
        };
    }
}
