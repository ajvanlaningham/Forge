using Forge.Data;
using Forge.Models;
using Forge.Resources.Strings;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations
{
    public sealed class QuestService : IQuestService
    {
        private readonly IRepository<ExerciseRow> _exerciseRepo;
        private readonly IStatsStore _stats;
        private bool _initialized;
        private readonly Dictionary<DateOnly, DailyQuests> _cache = new();
        private readonly HashSet<(DateOnly date, QuestKind kind)> _completed = new();

        private const int DailyCompletionXp = 50; // tweak later if you want
        private const string XpTotalKey = "Forge.XP.Total";
        private static string AwardKey(DateOnly d) => $"Forge.XP.Awarded.{d:yyyy-MM-dd}";

        public QuestService(IRepository<ExerciseRow> exerciseRepo, IStatsStore stats)
        {
            _exerciseRepo = exerciseRepo;
            _stats = stats;
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            await _exerciseRepo.GetAllAsync();

            _initialized = true;
        }

        public async Task<bool> HasDailyQuestAsync(DateOnly date, CancellationToken ct = default)
        {
            await InitializeAsync();
            return _cache.ContainsKey(date);
        }

        public async Task GenerateDailyQuestAsync(DateOnly date, CancellationToken ct = default)
        {
            await InitializeAsync();

            var allRows = await _exerciseRepo.GetAllAsync();
            var active = allRows.Where(r => r.IsActive).ToList();

            _cache[date] = AssembleDailyQuests(date, active);
        }

        public async Task<DailyQuests> GetDailyQuestsAsync(DateOnly date, CancellationToken ct = default)
        {
            await InitializeAsync();

            if (_cache.TryGetValue(date, out var cached))
                return cached;

            var allRows = await _exerciseRepo.GetAllAsync();
            var active = allRows.Where(r => r.IsActive).ToList();

            var day = AssembleDailyQuests(date, active);
            _cache[date] = day;
            return day;
        }

        public async Task CompleteQuestAsync(DateOnly date, QuestKind kind, CancellationToken ct = default)
        {
            await InitializeAsync();
            _completed.Add((date, kind));
        }

        public async Task<bool> IsQuestCompletedAsync(DateOnly date, QuestKind kind, CancellationToken ct = default)
        {
            await InitializeAsync();
            return _completed.Contains((date, kind));
        }

        public async Task UncompleteQuestAsync(DateOnly date, QuestKind kind, CancellationToken ct = default)
        {
            await InitializeAsync();
            _completed.Remove((date, kind));
        }

        public async Task<bool> AreAllQuestsCompletedAsync(DateOnly date, CancellationToken ct = default)
        {
            await InitializeAsync();
            return _completed.Contains((date, QuestKind.Strength))
                && _completed.Contains((date, QuestKind.Mobility))
                && _completed.Contains((date, QuestKind.Conditioning));
        }

        public async Task<int> TryAwardDailyCompletionXpAsync(DateOnly date, CancellationToken ct = default)
        {
            await InitializeAsync();

            if (!await AreAllQuestsCompletedAsync(date, ct))
                return 0;

            // Prevent double-award per date
            var flagKey = AwardKey(date);
            if (Preferences.Get(flagKey, false))
                return 0;

            await _stats.InitAsync();
            var user = await _stats.GetUserStatsAsync();
            user.Xp += DailyCompletionXp;
            await _stats.UpsertUserStatsAsync(user);

            // Mark awarded
            Preferences.Set(flagKey, true);
            return DailyCompletionXp;
        }

        private DailyQuests AssembleDailyQuests(DateOnly date, List<ExerciseRow> active)
        {
            var isRecovery = IsRecoveryDay(date);
            var theme = GetBodyFocusForDate(date);

            // Choose the pool: recovery-only on recovery days, otherwise all active
            var pool = isRecovery
                ? active.Where(r => string.Equals(r.SourceTag, Constants.GameConstants.Exercises.ExSourceTag, StringComparison.OrdinalIgnoreCase)).ToList()
                : active;

            var strength = BuildQuest(
                QuestKind.Strength, theme,
                PickByCategoryAndFocus(pool, ExerciseCategory.Strength, theme, max: 3));

            var mobility = BuildQuest(
                QuestKind.Mobility, theme,
                PickByCategoryAndFocus(pool, ExerciseCategory.Mobility, theme, max: 3));

            var conditioning = BuildQuest(
                QuestKind.Conditioning, theme,
                PickByCategoryAndFocus(pool, ExerciseCategory.Conditioning, theme, max: 3));

            return new DailyQuests
            {
                Date = date,
                BodyFocus = theme,
                Strength = strength,
                Mobility = mobility,
                Conditioning = conditioning
            };
        }

        // --- helpers ---

        private static BodyZone GetBodyFocusForDate(DateOnly date)
        {
            // Weekly cycle (Mon..Sun):
            // Mon=Lower, Tue=Upper, Wed=Recovery(FullBody), Thu=Core, Fri=FullBody, Sat=Recovery(FullBody), Sun=Recovery(FullBody)
            return date.DayOfWeek switch
            {
                DayOfWeek.Monday => BodyZone.Lower,
                DayOfWeek.Tuesday => BodyZone.Upper,
                DayOfWeek.Wednesday => BodyZone.FullBody, // recovery day
                DayOfWeek.Thursday => BodyZone.Core,
                DayOfWeek.Friday => BodyZone.FullBody,
                DayOfWeek.Saturday => BodyZone.FullBody, // recovery day
                DayOfWeek.Sunday => BodyZone.FullBody, // recovery day
                _ => BodyZone.FullBody
            };
        }

        private static bool IsRecoveryDay(DateOnly date) =>
            date.DayOfWeek is DayOfWeek.Wednesday or DayOfWeek.Saturday or DayOfWeek.Sunday;

        private static IReadOnlyList<ExerciseRow> PickByCategoryAndFocus(
        List<ExerciseRow> all,
        ExerciseCategory category,
        BodyZone focus,
        int max)
        {
            // Primary pool: exact category + focus
            var primary = all
                .Where(r => (ExerciseCategory)r.Category == category && (BodyZone)r.BodyZone == focus)
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .Take(max)
                .ToList();

            if (primary.Count >= max) return primary;

            // Secondary pool: same category + FullBody (good fillers for any day)
            var need = max - primary.Count;
            var secondary = all
                .Where(r => (ExerciseCategory)r.Category == category && (BodyZone)r.BodyZone == BodyZone.FullBody)
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .Take(need)
                .ToList();

            primary.AddRange(secondary);
            if (primary.Count >= max) return primary;

            // Tertiary pool: same category, any focus (as a last resort)
            need = max - primary.Count;
            var tertiary = all
                .Where(r => (ExerciseCategory)r.Category == category)
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .Take(need)
                .ToList();

            // Avoid duplicates if overlaps ever occur
            foreach (var row in tertiary)
                if (!primary.Any(p => p.Id == row.Id))
                    primary.Add(row);

            return primary;
        }

        private static Quest BuildQuest(QuestKind kind, BodyZone focus, IReadOnlyList<ExerciseRow> rows)
        {
            var qex = rows.Select(ToQuestExercise).ToList();
            return new Quest
            {
                Kind = kind,
                BodyFocus = focus,
                Title = kind switch
                {
                    QuestKind.Strength => AppResources.QuestService_CategoryStrength,
                    QuestKind.Mobility => AppResources.QuestService_CategoryMobility,
                    QuestKind.Conditioning => AppResources.QuestService_CategoryConditioning,
                    _ => AppResources.QuestService_CategoryDefault
                },
                Exercises = qex
            };
        }

        private static QuestExercise ToQuestExercise(ExerciseRow r)
        {
            var action = (ActionType)r.Action;

            return new QuestExercise
            {
                ExerciseId = r.Id,
                Name = r.Name,
                Action = action,
                Reps = action == ActionType.Reps ? r.DefaultReps : null,
                Seconds = action == ActionType.Time || action == ActionType.Hold ? r.DefaultSeconds : null,
                Distance = action == ActionType.Distance ? r.DefaultDistance : null,
                Breaths = action == ActionType.Hold ? r.DefaultBreaths : null
            };
        }
    }
}

