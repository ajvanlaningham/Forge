using Forge.Common;
using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Resources.Strings;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Services.Implementations
{
    public sealed class QuestService : IQuestService
    {
        private readonly IRepository<ExerciseRow> _exerciseRepo;
        private readonly IRepository<QuestCompletionRow> _completionRepo;
        private readonly IStatsStore _stats;
        private readonly IProgramService _programs;
        private bool _initialized;

        /// <summary>Days of assembled quests kept in memory either side of the requested date.</summary>
        private const int CacheRetentionDays = 7;

        private readonly Dictionary<DateOnly, DailyQuests> _cache = new();

        public QuestService(IRepository<ExerciseRow> exerciseRepo,
        IStatsStore stats,
        IRepository<QuestCompletionRow> completionRepo,
        IProgramService programs)
        {
            _exerciseRepo = exerciseRepo;
            _stats = stats;
            _completionRepo = completionRepo;
            _programs = programs;
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
            PruneCache(date);
        }

        public async Task<DailyQuests> GetDailyQuestsAsync(DateOnly date, CancellationToken ct = default)
        {
            await InitializeAsync();

            if (!_cache.TryGetValue(date, out var generated))
            {
                var allRows = await _exerciseRepo.GetAllAsync();
                var active = allRows.Where(r => r.IsActive).ToList();

                generated = AssembleDailyQuests(date, active);
                _cache[date] = generated;
                PruneCache(date);
            }

            // While a program is active the Strength quest IS today's training day and the
            // Mobility quest becomes the block's active rest (Decision 12). Never cached: what's
            // offered depends on completions, not just the date.
            var view = await _programs.GetViewAsync(date);
            if (view is null) return generated;

            return new DailyQuests
            {
                Date = generated.Date,
                BodyFocus = generated.BodyFocus,
                Strength = BuildTrainingQuest(view),
                Mobility = BuildActiveRestQuest(view),
                Conditioning = generated.Conditioning
            };
        }

        private static Quest BuildTrainingQuest(BlockView view)
        {
            if (view.Day is not { } day)
                return new Quest
                {
                    Kind = QuestKind.Strength, BodyFocus = BodyZone.FullBody, IsProgramDay = true,
                    Title = "Training done this week",
                    Tag = ProgramDefinition.WeekName(view.Week),
                    Notes = "Every training day this week is done. The rest of the week is active rest."
                };

            var notes = $"{view.Program.Definition.QuestName} · {view.Program.Definition.VersionName}";
            if (day.IsLight) notes += " · Honing: same weights, fewer sets";
            if (day.Test is { } test) notes += $" · ends with the {test.ToString().ToLowerInvariant()} test";
            return new Quest
            {
                Kind = QuestKind.Strength,
                BodyFocus = BodyZone.FullBody,
                IsProgramDay = true,
                Title = day.Name,
                Tag = $"{ProgramDefinition.WeekName(view.Week)} · {day.DayNumber}/{view.DayCount}",
                Notes = notes,
                Exercises = day.Slots.Select(r => new QuestExercise
                {
                    Name = r.IsMain ? r.Slot.Exercise : $"{r.Slot.Exercise} (accessory)",
                    Action = r.Slot.IsTimed ? ActionType.Hold : ActionType.Reps,
                    Sets = r.Slot.Sets,
                    RepsMin = r.Slot.RepsMin,
                    RepsMax = r.Slot.RepsMax,
                    Seconds = r.Slot.Seconds
                }).ToList()
            };
        }

        private static Quest BuildActiveRestQuest(BlockView view)
        {
            var rest = view.Program.Definition.ActiveRest;
            return new Quest
            {
                Kind = QuestKind.Mobility,
                BodyFocus = BodyZone.FullBody,
                IsProgramDay = true,
                Title = rest.Name,
                Tag = "Move",
                Notes = "Or: " + string.Join(" · ", rest.Alternatives),
                Exercises = rest.Flow.Select(slot => new QuestExercise
                {
                    Name = slot.Exercise,
                    Action = slot.IsTimed ? ActionType.Hold : ActionType.Reps,
                    Sets = slot.Sets,
                    RepsMin = slot.RepsMin,
                    RepsMax = slot.RepsMax,
                    Seconds = slot.Seconds
                }).ToList()
            };
        }

        public async Task CompleteQuestAsync(DateOnly date, QuestKind kind, CancellationToken ct = default)
        {
            await InitializeAsync();
            if (kind == QuestKind.Strength && await _programs.GetActiveAsync() is not null)
            {
                await _programs.CompleteDayAsync(date);
                return;
            }
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
            if (kind == QuestKind.Strength && await _programs.GetViewAsync(date) is { } view)
                return view.CompletedOnDate || view.WeekTrainingDone;
            var row = await GetRowAsync(date, kind);
            return row?.Completed ?? false;
        }

        public async Task UncompleteQuestAsync(DateOnly date, QuestKind kind, CancellationToken ct = default)
        {
            await InitializeAsync();
            if (kind == QuestKind.Strength && await _programs.GetActiveAsync() is not null)
            {
                await _programs.UncompleteDayAsync(date);
                return;
            }
            var row = await GetRowAsync(date, kind);
            if (row != null && row.Completed)
            {
                row.Completed = false;
                await _completionRepo.UpdateAsync(row);
            }
        }

        public async Task<bool> AreCoreQuestsCompletedAsync(DateOnly date, CancellationToken ct = default)
        {
            await InitializeAsync();
            var s = await IsQuestCompletedAsync(date, QuestKind.Strength, ct);
            var m = await IsQuestCompletedAsync(date, QuestKind.Mobility, ct);
            return s && m;

        }

        public async Task<int> TryAwardQuestXpAsync(DateOnly date, CancellationToken ct = default)
        {
            await InitializeAsync();

            // Program days carry their own (larger) award and their own completion rows.
            var programActive = await _programs.GetActiveAsync() is not null;
            var kinds = programActive
                ? new[] { QuestKind.Mobility }
                : new[] { QuestKind.Strength, QuestKind.Mobility };
            int netDelta = await _programs.ReconcileXpAsync(date);
            var multiplier = await _programs.MultiplierForAsync(date);

            await _stats.InitAsync();
            var user = await _stats.GetUserStatsAsync();

            foreach (var kind in kinds)
            {
                var row = await GetOrCreateRowAsync(date, kind);
                if (row.Completed && !row.XpGranted)
                {
                    // ×1.2 while the perfect-week streak is alive; the exact amount is recorded.
                    row.XpAwarded = GameMath.Quests.WithMultiplier(GameMath.Quests.XpPerQuest, multiplier);
                    row.XpGranted = true;
                    user.Xp += row.XpAwarded;
                    netDelta += row.XpAwarded;
                    await _completionRepo.UpdateAsync(row);
                }
                else if (!row.Completed && row.XpGranted)
                {
                    // Reclaim exactly what was granted (rows from before XpAwarded existed hold the base).
                    var amount = row.XpAwarded > 0 ? row.XpAwarded : GameMath.Quests.XpPerQuest;
                    user.Xp = Math.Max(0, user.Xp - amount);
                    row.XpGranted = false;
                    row.XpAwarded = 0;
                    netDelta -= amount;
                    await _completionRepo.UpdateAsync(row);
                }
            }

            if (netDelta != 0)
                await _stats.UpsertUserStatsAsync(user);

            return netDelta; // net of training-day and active-rest awards, multiplier included
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

            var conditioning = BuildWeeklyConditioningPlaceholder();

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

        /// <summary>
        /// Drops cached days far from the one in use. The cache is only an assembly shortcut —
        /// quests are deterministic for a given date, so evicting costs a rebuild and nothing more.
        /// </summary>
        private void PruneCache(DateOnly around)
        {
            if (_cache.Count <= (CacheRetentionDays * 2) + 1) return;

            var stale = _cache.Keys
                .Where(d => Math.Abs(d.DayNumber - around.DayNumber) > CacheRetentionDays)
                .ToList();

            foreach (var d in stale)
                _cache.Remove(d);
        }

        private async Task<QuestCompletionRow?> GetRowAsync(DateOnly date, QuestKind kind)
        {
            var dk = WeekMath.DateKey(date);
            var all = await _completionRepo.WhereAsync(r => r.DateKey == dk && r.Kind == (int)kind);
            return all.FirstOrDefault();
        }

        private async Task<QuestCompletionRow> GetOrCreateRowAsync(DateOnly date, QuestKind kind)
        {
            var existing = await GetRowAsync(date, kind);
            if (existing != null) return existing;
            var row = new QuestCompletionRow
            {
                DateKey = WeekMath.DateKey(date),
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
            // Priority: exact focus, then FullBody, then any focus — all within the category.
            // Distinct by id: when the focus IS FullBody the first two tiers are the same set, and
            // without de-duplication the quest listed the same exercise twice.
            static int Tier(ExerciseRow r, BodyZone focus) =>
                (BodyZone)r.BodyZone == focus ? 0 : (BodyZone)r.BodyZone == BodyZone.FullBody ? 1 : 2;

            return pool
                .Where(r => (ExerciseCategory)r.Category == category)
                .DistinctBy(r => r.Id)
                .OrderBy(r => Tier(r, focus))
                .ThenBy(r => r.Name)
                .ThenBy(r => r.Id)
                .Take(max)
                .ToList();
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
                    //conditioning handled by weekly builder
                    QuestKind.Conditioning => AppResources.QuestService_CategoryConditioning,
                    _ => AppResources.QuestService_CategoryDefault
                },
                Exercises = qex
            };
        }

        private static Quest BuildWeeklyConditioningPlaceholder()
        {
            return new Quest
            {
                Kind = QuestKind.Conditioning,
                BodyFocus = BodyZone.FullBody,
                Title = $"{AppResources.QuestService_CategoryConditioning} (Weekly)",
                Exercises = new List<QuestExercise>()
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

