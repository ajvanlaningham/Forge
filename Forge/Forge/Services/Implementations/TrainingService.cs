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
            IProgramService programs,
            IActiveRestService rest)
        {
            _rest = rest;
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
            // Recovery-tagged entries (breathing drills, recovery flows) belong to the mobility
            // quest on recovery days — never to a lifting session's picker or its swaps.
            var rows = (await _library.WhereAsync(r => r.IsActive))
                .Where(r => !string.Equals(r.SourceTag, Forge.Constants.GameConstants.Exercises.ExSourceTag, StringComparison.OrdinalIgnoreCase));
            return _libraryCache = rows
                .Select(r => r.ToDomain())
                .ToDictionary(e => e.Name, e => new ExerciseInfo(
                    e.Name, e.Category, e.BodyZone, e.Movement, e.Modality, e.Equipment, e.Action, e.NeedsPair, e.Unilateral, e.Plane, e.Skill));
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
            return await StartFromPlanAsync(await PlanFromProgramAsync(date, location));
        }

        public async Task<SessionPlan> PlanFromProgramAsync(DateOnly date, TrainingLocation location)
        {
            var view = await _programs.GetViewAsync(date)
                       ?? throw new InvalidOperationException("No active program");
            var day = view.Day ?? throw new InvalidOperationException("This week's training is already done");
            return await PlanAsync(view.Program, day, view.Week, date, location);
        }

        public async Task<SessionPlan> PlanForDayAsync(int dayIndex, int week, TrainingLocation location)
        {
            var program = await _programs.GetActiveAsync() ?? throw new InvalidOperationException("No active program");
            var day = program.Definition.Resolve(dayIndex, week);
            return await PlanAsync(program, day, week, DateOnly.FromDateTime(DateTime.Today), location);
        }

        /// <summary>A day's exercises as they'll be done at <paramref name="location"/>: gear swaps applied.</summary>
        private async Task<SessionPlan> PlanAsync(ActiveProgram program, ResolvedDay day, int week, DateOnly date, TrainingLocation location)
        {
            var lib = await LibraryAsync();
            var available = await GetEquipmentAsync(location);

            // This week's slots: mains + this week's accessories; week 4 already has a set fewer.
            // Prescribed exercises claim their names first, so a swap never duplicates one.
            var used = new HashSet<string>(day.Slots.Select(s => s.Slot.Exercise), StringComparer.Ordinal);
            var planned = new List<PlannedExercise>();
            for (var i = 0; i < day.Slots.Count; i++)
            {
                var slot = day.Slots[i].Slot;
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
                planned.Add(new PlannedExercise
                {
                    Name = name, SwappedFrom = swappedFrom, Slot = slot, SlotIndex = i, IsMain = day.Slots[i].IsMain,
                    Info = lib.GetValueOrDefault(name)
                });
            }

            return new SessionPlan
            {
                Date = date, Location = location, Program = program, Day = day, Exercises = planned,
                Title = $"{ProgramDefinition.WeekName(week)} · {day.Name}" + (day.IsLight ? " (light)" : "")
            };
        }

        private readonly IActiveRestService _rest;

        /// <summary>Two easy rounds of the flow: about ten minutes.</summary>
        private const int ActiveRestRounds = 2;

        /// <summary>
        /// A guided active rest (side quests, 2026-11-13): the quest's rest-day flow run in the workout
        /// screen as a gentle circuit, each move on the clock with the library's description as its
        /// instructions. Finishing it marks the day's active rest done; it never completes training.
        /// </summary>
        public async Task<SessionSnapshot> StartActiveRestAsync(DateOnly date)
        {
            var existing = await GetInProgressAsync();
            if (existing is not null) return existing;

            var program = await _programs.GetActiveAsync();
            var flow = program?.Definition.ActiveRest.Flow is { Count: > 0 } f ? f : DefaultRestFlow;
            var lib = await LibraryAsync();
            var names = flow.Select(s => s.Exercise).ToHashSet(StringComparer.Ordinal);
            var descriptions = (await _library.WhereAsync(r => r.IsActive))
                .Where(r => names.Contains(r.Name)).GroupBy(r => r.Name).ToDictionary(g => g.Key, g => g.First().Description);

            var session = NewSession(date, UserSettings.LastLocation);
            session.Title = "Active rest";
            session.IsActiveRest = true;
            await _sessions.InsertAsync(session);
            for (var i = 0; i < flow.Count; i++)
            {
                var slot = flow[i];
                var perSide = lib.TryGetValue(slot.Exercise, out var info) && info.Unilateral;
                var seconds = slot.Seconds ?? (perSide ? 30 : 40);
                await _exercises.InsertAsync(new SessionExerciseRow
                {
                    SessionId = session.Id, Position = i, ExerciseName = slot.Exercise, SlotIndex = i,
                    TargetSets = ActiveRestRounds, RepsMin = slot.RepsMin, RepsMax = slot.RepsMax,
                    TargetSeconds = seconds, RestSeconds = 10, Superset = "R",
                    Notes = slot.Notes ?? descriptions.GetValueOrDefault(slot.Exercise),
                    PlannedLoadLb = 0, PlannedReps = slot.RepsMin, PlannedSeconds = seconds
                });
            }
            return await SnapshotAsync(session);
        }

        private static readonly IReadOnlyList<ProgramSlot> DefaultRestFlow =
        [
            new ProgramSlot { Exercise = "Cat-Cow", RepsMin = 8, RepsMax = 8 },
            new ProgramSlot { Exercise = "World's Greatest Stretch", Seconds = 30 },
            new ProgramSlot { Exercise = "90/90 Hip Switch", RepsMin = 10, RepsMax = 10 },
            new ProgramSlot { Exercise = "Deep Squat Hold", Seconds = 60 },
            new ProgramSlot { Exercise = "Child's Pose", Seconds = 45 },
        ];

        public async Task<SessionSnapshot> StartFromPlanAsync(SessionPlan plan)
        {
            var existing = await GetInProgressAsync();
            if (existing is not null) return existing; // one session at a time

            UserSettings.LastLocation = plan.Location;
            var session = NewSession(plan.Date, plan.Location);
            session.ProgramId = plan.Program.Definition.Id;
            session.ProgramVersion = plan.Program.Definition.Version;
            session.BlockId = plan.Program.BlockId;
            session.DayIndex = plan.Day.DayIndex;
            session.Title = plan.Title;
            await _sessions.InsertAsync(session);

            for (var i = 0; i < plan.Exercises.Count; i++)
            {
                var p = plan.Exercises[i];
                await _exercises.InsertAsync(new SessionExerciseRow
                {
                    SessionId = session.Id,
                    Position = i,
                    ExerciseName = p.Name,
                    SwappedFrom = p.SwappedFrom,
                    SlotIndex = p.SlotIndex,
                    TargetSets = p.Slot.Sets,
                    RepsMin = p.Slot.RepsMin,
                    RepsMax = p.Slot.RepsMax,
                    TargetSeconds = p.Slot.Seconds,
                    RestSeconds = p.Slot.RestSeconds,
                    IsMain = p.IsMain,
                    Superset = p.Slot.Superset,
                    Notes = p.Slot.Notes,
                    PlannedLoadLb = p.PlannedLoadLb,
                    PlannedReps = p.PlannedReps,
                    PlannedSeconds = p.PlannedSeconds
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

        public async Task UpdateSetAsync(int setId, double loadLb, int? reps, int? seconds)
        {
            var set = await _sets.GetByIdAsync(setId) ?? throw new InvalidOperationException("No such set");
            set.LoadLb = Math.Max(0, loadLb);
            set.Reps = reps;
            set.Seconds = seconds;
            await _sets.UpdateAsync(set);
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

        // --- rest timer (PBI 8.3) ----------------------------------------------------------
        // Computed from a stored end time, never by counting ticks, so backgrounding, screen-off
        // and process death can't make it drift or lose it.

        private async Task<TrainingSessionRow> SessionRowAsync(int id)
            => await _sessions.GetByIdAsync(id) ?? throw new InvalidOperationException($"No session {id}");

        public async Task<TrainingSessionRow> StartRestAsync(int sessionId, int seconds)
        {
            var row = await SessionRowAsync(sessionId);
            row.RestPausedSeconds = null;
            row.RestEndsAtUtc = seconds > 0 ? DateTime.UtcNow.AddSeconds(seconds).Ticks : null;
            await _sessions.UpdateAsync(row);
            return row;
        }

        public async Task<TrainingSessionRow> AdjustRestAsync(int sessionId, int deltaSeconds)
        {
            var row = await SessionRowAsync(sessionId);
            if (row.RestPausedSeconds is int paused)
                row.RestPausedSeconds = Math.Max(0, paused + deltaSeconds);
            else if (row.RestEndsAtUtc is long ends)
            {
                var next = new DateTime(ends, DateTimeKind.Utc).AddSeconds(deltaSeconds);
                row.RestEndsAtUtc = next <= DateTime.UtcNow ? null : next.Ticks;
            }
            await _sessions.UpdateAsync(row);
            return row;
        }

        public async Task<TrainingSessionRow> ToggleRestPauseAsync(int sessionId)
        {
            var row = await SessionRowAsync(sessionId);
            if (row.RestPausedSeconds is int paused)
            {
                row.RestEndsAtUtc = DateTime.UtcNow.AddSeconds(paused).Ticks;
                row.RestPausedSeconds = null;
            }
            else if (row.RestEndsAtUtc is long ends)
            {
                var left = (int)Math.Ceiling((new DateTime(ends, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds);
                row.RestEndsAtUtc = null;
                row.RestPausedSeconds = Math.Max(0, left);
            }
            await _sessions.UpdateAsync(row);
            return row;
        }

        public async Task<TrainingSessionRow> StopRestAsync(int sessionId)
        {
            var row = await SessionRowAsync(sessionId);
            row.RestEndsAtUtc = null;
            row.RestPausedSeconds = null;
            await _sessions.UpdateAsync(row);
            return row;
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
                row.RestPausedSeconds = null;
                await _sessions.UpdateAsync(row);

                if (row.IsActiveRest)
                    await _rest.SetDoneAsync(DateOnly.ParseExact(row.DateKey, "yyyy-MM-dd"), true);
                else
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
            var view = await _programs.GetViewAsync(date);
            if (view?.Day is null || view.Program.BlockId != row.BlockId || view.Day.DayIndex != row.DayIndex) return;
            if (view.CompletedOnDate) return;

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
            row.RestPausedSeconds = null;
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

        public async Task<PacedLast?> GetPacedLastAsync(string exerciseName)
        {
            var finished = (await _sessions.WhereAsync(s => s.Status == (int)SessionStatus.Finished)).ToDictionary(s => s.Id);
            if (finished.Count == 0) return null;
            var exercises = (await _exercises.WhereAsync(e => e.ExerciseName == exerciseName && !e.Skipped))
                .Where(e => finished.ContainsKey(e.SessionId))
                .OrderByDescending(e => finished[e.SessionId].StartedAtUtc);
            foreach (var ex in exercises)
            {
                var working = await _sets.WhereAsync(s => s.SessionExerciseId == ex.Id && !s.IsWarmup);
                if (working.Count == 0) continue;
                var session = finished[ex.SessionId];
                var reps = ex.PlannedReps ?? ex.RepsMin ?? 0;
                return new PacedLast(working.Max(s => s.LoadLb), reps, session.Feel is int f ? (Feel)f : null);
            }
            return null;
        }

        public async Task SetFeelAsync(int sessionId, Feel? feel)
        {
            var row = await _sessions.GetByIdAsync(sessionId);
            if (row is null) return;
            row.Feel = feel is Feel f ? (int)f : null;
            await _sessions.UpdateAsync(row);
        }

        public async Task<IReadOnlyList<ExerciseHistoryEntry>> GetExerciseHistoryAsync(string exerciseName)
        {
            var finished = (await _sessions.WhereAsync(s => s.Status == (int)SessionStatus.Finished)).ToDictionary(s => s.Id);
            var result = new List<ExerciseHistoryEntry>();
            foreach (var ex in (await _exercises.WhereAsync(e => e.ExerciseName == exerciseName))
                         .Where(e => finished.ContainsKey(e.SessionId))
                         .OrderBy(e => finished[e.SessionId].StartedAtUtc))
            {
                var working = (await _sets.WhereAsync(s => s.SessionExerciseId == ex.Id && !s.IsWarmup))
                    .OrderBy(s => s.Position).ToList();
                if (working.Count == 0) continue;
                var session = finished[ex.SessionId];
                result.Add(new ExerciseHistoryEntry(
                    DateOnly.ParseExact(session.DateKey, "yyyy-MM-dd"), session.Id, session.Title, working));
            }
            return result;
        }

        public async Task<IReadOnlyDictionary<string, IReadOnlyList<PersonalRecord>>> GetSessionRecordsAsync(int sessionId)
        {
            var result = new Dictionary<string, IReadOnlyList<PersonalRecord>>();
            var session = await _sessions.GetByIdAsync(sessionId);
            if (session is null || session.Status != (int)SessionStatus.Finished) return result;

            foreach (var name in (await _exercises.WhereAsync(e => e.SessionId == sessionId)).Select(e => e.ExerciseName).Distinct())
            {
                var history = await GetExerciseHistoryAsync(name);
                static WorkSet W(SessionSetRow r) => new(r.LoadLb, r.Reps, r.Seconds);
                var earlier = history.TakeWhile(h => h.SessionId != sessionId).SelectMany(h => h.WorkingSets).Select(W).ToList();
                var now = history.FirstOrDefault(h => h.SessionId == sessionId)?.WorkingSets.Select(W).ToList();
                if (now is null) continue;
                var prs = StrengthMath.FindRecords(earlier, now);
                if (prs.Count > 0) result[name] = prs;
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
