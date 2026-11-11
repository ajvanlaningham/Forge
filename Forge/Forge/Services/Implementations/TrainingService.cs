using Forge.Common;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Services.Implementations
{
    public sealed class TrainingService : ITrainingService
    {
        private readonly IRepository<TrainingSessionRow> _sessions;
        private readonly IRepository<SessionExerciseRow> _exercises;
        private readonly IRepository<SessionSetRow> _sets;
        private readonly IRepository<ExerciseRow> _library;
        private readonly IExerciseLibraryImporter _importer;
        private readonly IInventoryService _inventory;
        private readonly IProgramService _programs;

        private Dictionary<string, ExerciseInfo>? _libraryCache;

        public TrainingService(
            IRepository<TrainingSessionRow> sessions,
            IRepository<SessionExerciseRow> exercises,
            IRepository<SessionSetRow> sets,
            IRepository<ExerciseRow> library,
            IExerciseLibraryImporter importer,
            IInventoryService inventory,
            IProgramService programs)
        {
            _sessions = sessions;
            _exercises = exercises;
            _sets = sets;
            _library = library;
            _importer = importer;
            _inventory = inventory;
            _programs = programs;
        }

        // --- library & equipment -------------------------------------------------------------

        private async Task<Dictionary<string, ExerciseInfo>> LibraryAsync()
        {
            if (_libraryCache is not null) return _libraryCache;
            await _importer.EnsureSeededAsync();
            var rows = await _library.WhereAsync(r => r.IsActive);
            return _libraryCache = rows
                .Select(r => r.ToDomain())
                .ToDictionary(e => e.Name, e => new ExerciseInfo(
                    e.Name, e.Category, e.BodyZone, e.Movement, e.Modality, e.Equipment, e.Action));
        }

        public async Task<IReadOnlyList<ExerciseInfo>> GetLibraryAsync(ExerciseCategory? category = null)
            => (await LibraryAsync()).Values
                .Where(e => category is null || e.Category == category)
                .OrderBy(e => e.Name, StringComparer.CurrentCulture)
                .ToList();

        public async Task<Equipment> GetEquipmentAsync(TrainingLocation location)
        {
            if (location == TrainingLocation.Gym) return UserSettings.GymEquipment;
            await _inventory.InitializeAsync();
            return await _inventory.GetOwnedFlagsAsync();
        }

        // --- reading -------------------------------------------------------------------------

        public async Task<SessionSnapshot?> GetInProgressAsync()
        {
            var open = (await _sessions.WhereAsync(s => s.Status == (int)SessionStatus.InProgress))
                .OrderByDescending(s => s.Id)
                .FirstOrDefault();
            return open is null ? null : await SnapshotAsync(open);
        }

        public async Task<SessionSnapshot> GetSessionAsync(int sessionId)
        {
            var row = await _sessions.GetByIdAsync(sessionId)
                      ?? throw new InvalidOperationException($"No session {sessionId}");
            return await SnapshotAsync(row);
        }

        private async Task<SessionSnapshot> SnapshotAsync(TrainingSessionRow session)
        {
            var lib = await LibraryAsync();
            var exercises = (await _exercises.WhereAsync(e => e.SessionId == session.Id))
                .OrderBy(e => e.Position).ToList();
            var sets = (await _sets.WhereAsync(s => s.SessionId == session.Id))
                .GroupBy(s => s.SessionExerciseId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<SessionSetRow>)g.OrderBy(s => s.Position).ToList());

            return new SessionSnapshot
            {
                Session = session,
                Exercises = exercises.Select(e => new SessionExerciseSnapshot
                {
                    Row = e,
                    Sets = sets.TryGetValue(e.Id, out var list) ? list : Array.Empty<SessionSetRow>(),
                    Info = lib.GetValueOrDefault(e.ExerciseName)
                }).ToList()
            };
        }

        // --- starting ------------------------------------------------------------------------

        public async Task<SessionSnapshot> StartFromProgramAsync(DateOnly date, TrainingLocation location)
        {
            var existing = await GetInProgressAsync();
            if (existing is not null) return existing; // one session at a time

            var day = await _programs.GetDayForDateAsync(date)
                      ?? throw new InvalidOperationException("No active program");

            var lib = await LibraryAsync();
            var available = await GetEquipmentAsync(location);
            UserSettings.LastLocation = location;

            var session = NewSession(date, location);
            session.ProgramId = day.Program.Definition.Id;
            session.ProgramVersion = day.Program.Definition.Version;
            session.BlockId = day.Program.BlockId;
            session.DayIndex = day.DayIndex;
            session.Title = $"Day {day.DayNumber} · {day.Day.Name}";
            await _sessions.InsertAsync(session);

            // Prescribed exercises claim their names first, so a swap never duplicates one.
            var used = new HashSet<string>(day.Day.Slots.Select(s => s.Exercise), StringComparer.Ordinal);

            for (var i = 0; i < day.Day.Slots.Count; i++)
            {
                var slot = day.Day.Slots[i];
                var name = slot.Exercise;
                string? swappedFrom = null;

                // Swap gear-dependent slots for something doable here. If nothing unused qualifies
                // the prescribed exercise stays, and the user swaps or skips it by hand.
                if (lib.TryGetValue(slot.Exercise, out var prescribed)
                    && ExerciseSwap.Resolve(prescribed, lib.Values, available, used) is { } resolved
                    && resolved.Name != prescribed.Name)
                {
                    name = resolved.Name;
                    swappedFrom = prescribed.Name;
                    used.Add(resolved.Name);
                }

                await _exercises.InsertAsync(new SessionExerciseRow
                {
                    SessionId = session.Id,
                    Position = i,
                    ExerciseName = name,
                    SwappedFrom = swappedFrom,
                    SlotIndex = i,
                    TargetSets = slot.Sets,
                    RepsMin = slot.RepsMin,
                    RepsMax = slot.RepsMax,
                    TargetSeconds = slot.Seconds,
                    RestSeconds = slot.RestSeconds
                });
            }

            return await SnapshotAsync(session);
        }

        public async Task<SessionSnapshot> StartEmptyAsync(DateOnly date, TrainingLocation location)
        {
            var existing = await GetInProgressAsync();
            if (existing is not null) return existing;

            UserSettings.LastLocation = location;
            var session = NewSession(date, location);
            session.Title = "Open session";
            await _sessions.InsertAsync(session);
            return await SnapshotAsync(session);
        }

        private static TrainingSessionRow NewSession(DateOnly date, TrainingLocation location) => new()
        {
            DateKey = WeekMath.DateKey(date),
            StartedAtUtc = DateTime.UtcNow.Ticks,
            Status = (int)SessionStatus.InProgress,
            Location = (int)location
        };

        // --- editing -------------------------------------------------------------------------

        public async Task AddExerciseAsync(int sessionId, string exerciseName)
        {
            var existing = await _exercises.WhereAsync(e => e.SessionId == sessionId);
            var lib = await LibraryAsync();
            var timed = lib.TryGetValue(exerciseName, out var info) && info.Action is ActionType.Hold or ActionType.Time;

            await _exercises.InsertAsync(new SessionExerciseRow
            {
                SessionId = sessionId,
                Position = existing.Count == 0 ? 0 : existing.Max(e => e.Position) + 1,
                ExerciseName = exerciseName,
                // Ad-hoc exercises get a loose default prescription so progression has a range to work in.
                TargetSets = 3,
                RepsMin = timed ? null : 8,
                RepsMax = timed ? null : 12,
                TargetSeconds = timed ? 30 : null
            });
        }

        public async Task SwapExerciseAsync(int sessionExerciseId, string newExerciseName)
        {
            var row = await _exercises.GetByIdAsync(sessionExerciseId)
                      ?? throw new InvalidOperationException("No such exercise");
            if (row.ExerciseName == newExerciseName) return;

            // Remember the original prescription's exercise, not an intermediate swap.
            row.SwappedFrom ??= row.ExerciseName;
            if (row.SwappedFrom == newExerciseName) row.SwappedFrom = null; // swapped back
            row.ExerciseName = newExerciseName;
            await _exercises.UpdateAsync(row);
        }

        public async Task SetSkippedAsync(int sessionExerciseId, bool skipped)
        {
            var row = await _exercises.GetByIdAsync(sessionExerciseId);
            if (row is null) return;
            row.Skipped = skipped;
            await _exercises.UpdateAsync(row);
        }

        public async Task MoveExerciseAsync(int sessionExerciseId, int delta)
        {
            var row = await _exercises.GetByIdAsync(sessionExerciseId);
            if (row is null || delta == 0) return;

            var ordered = (await _exercises.WhereAsync(e => e.SessionId == row.SessionId))
                .OrderBy(e => e.Position).ToList();
            var from = ordered.FindIndex(e => e.Id == row.Id);
            var to = Math.Clamp(from + delta, 0, ordered.Count - 1);
            if (from == to) return;

            var moving = ordered[from];
            ordered.RemoveAt(from);
            ordered.Insert(to, moving);
            for (var i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Position == i) continue;
                ordered[i].Position = i;
                await _exercises.UpdateAsync(ordered[i]);
            }
        }

        public async Task<SessionSetRow> LogSetAsync(int sessionExerciseId, double loadLb, int? reps, int? seconds, bool isWarmup)
        {
            var ex = await _exercises.GetByIdAsync(sessionExerciseId)
                     ?? throw new InvalidOperationException("No such exercise");
            var existing = await _sets.WhereAsync(s => s.SessionExerciseId == sessionExerciseId);

            var set = new SessionSetRow
            {
                SessionExerciseId = sessionExerciseId,
                SessionId = ex.SessionId,
                Position = existing.Count == 0 ? 0 : existing.Max(s => s.Position) + 1,
                LoadLb = Math.Max(0, loadLb),
                Reps = reps,
                Seconds = seconds,
                IsWarmup = isWarmup,
                LoggedAtUtc = DateTime.UtcNow.Ticks
            };
            await _sets.InsertAsync(set);

            // Logging a set un-skips the exercise: the user evidently did it.
            if (ex.Skipped)
            {
                ex.Skipped = false;
                await _exercises.UpdateAsync(ex);
            }
            return set;
        }

        public async Task DeleteSetAsync(int setId)
        {
            var set = await _sets.GetByIdAsync(setId);
            if (set is not null) await _sets.DeleteAsync(set);
        }

        // --- ending --------------------------------------------------------------------------

        public async Task<SessionSnapshot> FinishAsync(int sessionId)
        {
            var row = await _sessions.GetByIdAsync(sessionId)
                      ?? throw new InvalidOperationException($"No session {sessionId}");
            if (row.Status != (int)SessionStatus.Finished)
            {
                row.Status = (int)SessionStatus.Finished;
                row.EndedAtUtc = DateTime.UtcNow.Ticks;
                row.RestEndsAtUtc = null;
                await _sessions.UpdateAsync(row);

                await CompleteProgramDayAsync(row);
            }
            return await SnapshotAsync(row);
        }

        /// <summary>
        /// PBI 8.2: finishing a program session completes that program day — the Strength quest —
        /// through the same path as the manual checkbox, so XP can't be double-awarded. Only when
        /// the session's day is still the one on offer for its date and at least one working set
        /// was logged: an empty or stale session completes nothing.
        /// </summary>
        private async Task CompleteProgramDayAsync(TrainingSessionRow row)
        {
            if (row.BlockId is null || row.DayIndex is null) return;

            var working = await _sets.WhereAsync(s => s.SessionId == row.Id && !s.IsWarmup);
            if (working.Count == 0) return;

            var date = DateOnly.ParseExact(row.DateKey, "yyyy-MM-dd");
            var offer = await _programs.GetDayForDateAsync(date);
            if (offer is null || offer.Program.BlockId != row.BlockId || offer.DayIndex != row.DayIndex) return;
            if (offer.CompletedOnDate) return;

            await _programs.CompleteDayAsync(date, row.Id);
            await _programs.ReconcileXpAsync(date);
        }

        public async Task DiscardAsync(int sessionId)
        {
            var row = await _sessions.GetByIdAsync(sessionId);
            if (row is null) return;
            row.Status = (int)SessionStatus.Discarded;
            row.EndedAtUtc = DateTime.UtcNow.Ticks;
            row.RestEndsAtUtc = null;
            await _sessions.UpdateAsync(row);
        }

        // --- history -------------------------------------------------------------------------

        public async Task<LastPerformance?> GetLastPerformanceAsync(string exerciseName, int? excludingSessionId = null)
        {
            var finished = (await _sessions.WhereAsync(s => s.Status == (int)SessionStatus.Finished))
                .Where(s => s.Id != excludingSessionId)
                .ToDictionary(s => s.Id);
            if (finished.Count == 0) return null;

            var candidates = (await _exercises.WhereAsync(e => e.ExerciseName == exerciseName))
                .Where(e => finished.ContainsKey(e.SessionId))
                .OrderByDescending(e => finished[e.SessionId].StartedAtUtc);

            foreach (var ex in candidates)
            {
                var working = (await _sets.WhereAsync(s => s.SessionExerciseId == ex.Id && !s.IsWarmup))
                    .OrderBy(s => s.Position).ToList();
                if (working.Count == 0) continue; // skipped there; look further back

                var date = DateOnly.ParseExact(finished[ex.SessionId].DateKey, "yyyy-MM-dd");
                return new LastPerformance(date, working);
            }
            return null;
        }

        public async Task<IReadOnlyList<IReadOnlyList<WorkSet>>> GetRecentWorkingSetsAsync(
            string exerciseName, int? excludingSessionId = null, int sessions = 4)
        {
            var finished = (await _sessions.WhereAsync(s => s.Status == (int)SessionStatus.Finished))
                .Where(s => s.Id != excludingSessionId)
                .ToDictionary(s => s.Id);
            if (finished.Count == 0) return Array.Empty<IReadOnlyList<WorkSet>>();

            var result = new List<IReadOnlyList<WorkSet>>();
            var exercises = (await _exercises.WhereAsync(e => e.ExerciseName == exerciseName))
                .Where(e => finished.ContainsKey(e.SessionId))
                .OrderByDescending(e => finished[e.SessionId].StartedAtUtc);

            foreach (var ex in exercises)
            {
                var working = (await _sets.WhereAsync(s => s.SessionExerciseId == ex.Id && !s.IsWarmup))
                    .OrderBy(s => s.Position)
                    .Select(s => new WorkSet(s.LoadLb, s.Reps, s.Seconds))
                    .ToList();
                if (working.Count == 0) continue;
                result.Add(working);
                if (result.Count >= sessions) break;
            }
            return result;
        }

        public async Task<IReadOnlyList<TrainingSessionRow>> GetHistoryAsync(int limit = 100)
            => (await _sessions.WhereAsync(s => s.Status == (int)SessionStatus.Finished))
                .OrderByDescending(s => s.StartedAtUtc)
                .Take(limit)
                .ToList();
    }
}
