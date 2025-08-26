using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Resources.Strings;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations
{
    public sealed class QuestService : IQuestService
    {
        private readonly IRepository<ExerciseRow> _exerciseRepo;
        private readonly IRepository<QuestCompletionRow> _completionRepo;
        private readonly IStatsStore _stats;
        private bool _initialized;
        private static string DateKey(DateOnly d) => d.ToString("yyyy-MM-dd");
        private readonly Dictionary<DateOnly, DailyQuests> _cache = new();

        public QuestService(IRepository<ExerciseRow> exerciseRepo,
        IStatsStore stats,
        IRepository<QuestCompletionRow> completionRepo)
        {
            _exerciseRepo = exerciseRepo;
            _stats = stats;
            _completionRepo = completionRepo;
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            await _exerciseRepo.GetAllAsync();
            await _completionRepo.GetAllAsync();

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
            var row = await GetOrCreateRowAsync(date, kind);
            if (!row.Completed)
            {
                row.Completed = true;
                await _completionRepo.InsertOrReplaceAsync(row);
            }
        }

        public async Task<bool> IsQuestCompletedAsync(DateOnly date, QuestKind kind, CancellationToken ct = default)
        {
            await InitializeAsync();
            var row = await GetRowAsync(date, kind);
            return row?.Completed ?? false;
        }

        public async Task UncompleteQuestAsync(DateOnly date, QuestKind kind, CancellationToken ct = default)
        {
            await InitializeAsync();
            var row = await GetRowAsync(date, kind);
            if (row != null && row.Completed)
            {
                row.Completed = false;
                await _completionRepo.UpdateAsync(row);
            }
        }

        public async Task<bool> AreAllQuestsCompletedAsync(DateOnly date, CancellationToken ct = default)
        {
            await InitializeAsync();
            var s = await IsQuestCompletedAsync(date, QuestKind.Strength, ct);
            var m = await IsQuestCompletedAsync(date, QuestKind.Mobility, ct);
            var c = await IsQuestCompletedAsync(date, QuestKind.Conditioning, ct);
            return s && m && c;

        }

        public async Task<int> TryAwardDailyCompletionXpAsync(DateOnly date, CancellationToken ct = default)
        {
            await InitializeAsync();
            var kinds = new[] { QuestKind.Strength, QuestKind.Mobility, QuestKind.Conditioning };
            int netDelta = 0;
            
            await _stats.InitAsync();
            var user = await _stats.GetUserStatsAsync();
            
            foreach (var kind in kinds)
            {
                var row = await GetOrCreateRowAsync(date, kind);
                // If user marked complete and hasn't been granted XP yet -> grant +50
                if (row.Completed && !row.XpGranted)
                {
                    user.Xp += GameMath.GameConstants.Quests.XpPerQuest; // expected 50
                    row.XpGranted = true;
                    netDelta += GameMath.GameConstants.Quests.XpPerQuest;
                    await _completionRepo.UpdateAsync(row);
                }
                // If user undid completion but XP was already granted -> remove -50
                else if (!row.Completed && row.XpGranted)
                {
                    user.Xp = Math.Max(0, user.Xp - GameMath.GameConstants.Quests.XpPerQuest);
                    row.XpGranted = false;
                    netDelta -= GameMath.GameConstants.Quests.XpPerQuest;
                    await _completionRepo.UpdateAsync(row);
                }
            }
            
            if (netDelta != 0)
                await _stats.UpsertUserStatsAsync(user);
            
            return netDelta; // +50, -50, or 0
        }

        private DailyQuests AssembleDailyQuests(DateOnly date, List<ExerciseRow> active)
        {
            var isRecovery = IsRecoveryDay(date);

            // Keep FullBody label on recovery; otherwise use your weekly mapping
            var theme = isRecovery ? BodyZone.FullBody : GetBodyFocusForDate(date);

            // Strict pool rules:
            //  - Recovery day  => ONLY recovery-tagged
            //  - Normal day    => EXCLUDE recovery-tagged
            var pool = isRecovery
                ? active.Where(r =>
                      string.Equals(
                          r.SourceTag,
                          GameConstants.Exercises.ExSourceTag,
                          StringComparison.OrdinalIgnoreCase))
                    .ToList()
                : active.Where(r =>
                      !string.Equals(
                          r.SourceTag,
                          GameConstants.Exercises.ExSourceTag,
                          StringComparison.OrdinalIgnoreCase))
                    .ToList();

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

        private async Task<QuestCompletionRow?> GetRowAsync(DateOnly date, QuestKind kind)
        {
            var dk = DateKey(date);
            var all = await _completionRepo.WhereAsync(r => r.DateKey == dk && r.Kind == (int)kind);
            return all.FirstOrDefault();
        }

        private async Task<QuestCompletionRow> GetOrCreateRowAsync(DateOnly date, QuestKind kind)
        {
            var existing = await GetRowAsync(date, kind);
            if (existing != null) return existing;
            var row = new QuestCompletionRow
            {
                DateKey = DateKey(date),
                Kind = (int) kind,
                Completed = false,
                XpGranted = false
            };
            await _completionRepo.InsertAsync(row);
            return row;
        }

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
        List<ExerciseRow> pool,
        ExerciseCategory category,
        BodyZone focus,
        int max)
        {
            // Primary: exact category + focus
            var primary = pool
                .Where(r => (ExerciseCategory)r.Category == category && (BodyZone)r.BodyZone == focus)
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .Take(max)
                .ToList();

            if (primary.Count >= max) return primary;

            // Secondary: same category + FullBody
            var need = max - primary.Count;
            var secondary = pool
                .Where(r => (ExerciseCategory)r.Category == category && (BodyZone)r.BodyZone == BodyZone.FullBody)
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .Take(need)
                .ToList();

            primary.AddRange(secondary);
            if (primary.Count >= max) return primary;

            // Tertiary: same category, any focus (still within pool)
            need = max - primary.Count;
            var tertiary = pool
                .Where(r => (ExerciseCategory)r.Category == category)
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .Take(need)
                .ToList();

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

