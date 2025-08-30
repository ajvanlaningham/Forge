using SQLite;

namespace Forge.Models;

[Table("ConditioningWeek")]
public sealed class ConditioningWeekRow
{
    /// <summary>
    /// Monday date of the week as "yyyy-MM-dd" (e.g., 2025-08-25).
    /// Acts as the primary key to keep one row per week.
    /// </summary>
    [PrimaryKey]
    public string WeekStartKey { get; set; } = string.Empty;

    /// <summary>
    /// Minutes of cardio logged so far this week.
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// Goal in minutes for the week (default 180 = 3 hours).
    /// Can be adjusted later if you want dynamic goals.
    /// </summary>
    public int GoalMinutes { get; set; } = 180;

    /// <summary>
    /// Whether weekly XP for completing the goal has been granted.
    /// </summary>
    public bool XpGranted { get; set; } = false;

    /// <summary>
    /// Optional: bookkeeping timestamps.
    /// </summary>
    public long CreatedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
    public long UpdatedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
}