using Forge.Common;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Services.Implementations
{
    /// <summary>
    /// Side quests feed the quest's own records (<see cref="SideQuests"/>): a training side quest
    /// completes the day on offer "via" its activity, a rest one marks active rest done. XP, the
    /// streak, the map, the chest and the guild all follow from those, so nothing is counted twice.
    /// </summary>
    public sealed class SideQuestService : ISideQuestService
    {
        private readonly IRepository<SideQuestRow> _rows;
        private readonly IProgramService _programs;
        private readonly IActiveRestService _rest;

        public SideQuestService(IRepository<SideQuestRow> rows, IProgramService programs, IActiveRestService rest)
        {
            _rows = rows;
            _programs = programs;
            _rest = rest;
        }

        public async Task<SideQuestRow?> GetAsync(DateOnly date)
        {
            var key = WeekMath.DateKey(date);
            return (await _rows.WhereAsync(r => r.DateKey == key)).OrderByDescending(r => r.Id).FirstOrDefault();
        }

        public async Task<IReadOnlyList<SideQuestActivity>> RecentAsync()
            => (await _rows.GetAllAsync()).OrderByDescending(r => r.LoggedAtUnix)
                .GroupBy(r => r.Activity, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(r => new SideQuestActivity(r.Activity, r.Minutes, (Effort)r.Effort))
                .ToList();

        public async Task<bool> CanStandInAsync(DateOnly date)
        {
            var view = await _programs.GetViewAsync(date);
            if (view is null) return false;
            var onOffer = view.Day is not null && !view.CompletedOnDate;
            var (start, end) = BlockSchedule.WeekRange(view.Program.StartedOn, view.Week);
            var filled = (await _rows.WhereAsync(r => r.CountsAs == (int)SideQuestCounts.Training))
                .Count(r => DateOnly.ParseExact(r.DateKey, "yyyy-MM-dd") is var d && d >= start && d <= end);
            return SideQuests.CanStandIn(onOffer, filled);
        }

        public async Task<int> LogAsync(DateOnly date, string activity, int minutes, Effort effort, SideQuestCounts countsAs, string? note)
        {
            var net = await UndoAsync(date); // one side quest a day: logging again replaces it
            var counts = countsAs == SideQuestCounts.Training && await CanStandInAsync(date)
                ? SideQuestCounts.Training : SideQuestCounts.Rest;
            await _rows.InsertAsync(new SideQuestRow
            {
                DateKey = WeekMath.DateKey(date), Activity = activity.Trim(), Minutes = minutes, Effort = (int)effort,
                CountsAs = (int)counts, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                LoggedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            if (counts == SideQuestCounts.Training)
            {
                await _programs.CompleteDayAsync(date, via: activity.Trim());
                net += await _programs.ReconcileXpAsync(date);
            }
            else net += await _rest.SetDoneAsync(date, true);
            App.SyncGuild();
            return net;
        }

        public async Task<int> UndoAsync(DateOnly date)
        {
            var row = await GetAsync(date);
            if (row is null) return 0;
            await _rows.DeleteAsync(row);
            int net;
            if (row.CountsAs == (int)SideQuestCounts.Training)
            {
                await _programs.UncompleteDayAsync(date);
                net = await _programs.ReconcileXpAsync(date);
            }
            else net = await _rest.SetDoneAsync(date, false);
            App.SyncGuild();
            return net;
        }
    }
}
