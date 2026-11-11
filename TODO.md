# Forge v1 Backlog

Takes Forge from a quest/XP prototype to a personal daily driver. Build/deploy rules live in
[`AGENTS.md`](AGENTS.md) (source of truth over `README.md`).

## Target Outcome

- Workouts are logged set-by-set against a structured program, with progressive overload.
- Daily weigh-ins take seconds and display as a 7-day average with a goal line.
- Level advances from consistency; stat scores move from monthly baseline tests.
- Protein/calories and resting HR / steps / sleep are visible, manual entry first.
- Data is backed up daily and restorable; merges to `main` reach the phone via in-app update.

**Scope:** Android only, single user, no accounts/backend/sync. **Deferred:** Guild/social;
Speed stat; food database; Windows; cloud backup; maintenance mode. **iOS deferred for now** —
a Mac is on the tailnet and the developer account is quick to get once the app is worth a
second phone. Keep shared code free of Android-only APIs (`#if ANDROID` / `Platforms/`).

## Design Decisions

Settled. Changing one means revisiting the PBIs that depend on it.

1. **Three independent progress systems.** Levels = *consistency*. Stat scores = *capability*
   (baseline tests only — logged workout e1RM never moves a stat). Weight is its own track.
2. **Weight never earns XP for its value.** XP is for *logging*; daily weight is noise.
3. **Weight is not the hero element.** Show the 7-day average; raw number behind a tap.
4. **Three stats: STR, DEX (mobility), CON.** Speed deferred.
5. **Log scoring:** `score = 10 + 25 * log2(current / baseline)`, clamped 1–100, max +10 per
   retest. Replaces the linear `ScoreFrom`, which needs ~990% improvement to reach 100.
6. **Relative strength:** STR scores e1RM ÷ bodyweight; absolute e1RM shown alongside.
7. **Frozen test protocols.** Never edit; version a new one. See `docs/baseline-tests.md`.
8. **Test week, not test day:** a 7-day window opens every 4th level, three sessions, each
   granting quest XP.
9. **Read nutrition, don't build it.** Manual entry + Health Connect; no food database.
10. **Manual entry ships before every integration.** Fully usable with all permissions denied.
11. **The program becomes the quest** (2026-11-11). Quests are organized as days-of-program,
    advancing by completion order, not weekday. See PBI 8.4.

**Assumptions:** user starts ~230 lb, milestone 218 lb, goal 200 lb (settings, not constants).
Equipment on hand: dumbbells, kettlebells, bike, mat, wall. Level pacing must give ~1 level per
fully-adherent week so test windows stay ~monthly (currently broken — see 8.4).

**External dependencies:** Health Connect (no MAUI binding; blocks Epic 7). A wearable (not
chosen). Release keystore at `~/.forge-signing/`, deliberately local-only (see `AGENTS.md`).

## Done

- **Backlog & decisions (1.1, 1.2):** backlog written; Product rules in `AGENTS.md`; frozen
  protocols in `docs/baseline-tests.md`. _Remaining: XML docs on the log scoring helper once
  5.1 creates it._
- **Repo cleanup (2.1–2.5):** single accurate README; orphan `exercises.v1.json` removed and the
  `LibraryVersion` reseed gate documented, with `ForceReseedAsync` as escape hatch; constants
  untangled and personal targets moved to `UserSettings`; misleading QuestService names fixed
  and quest cache bounded; `Forge.Tests` (xUnit, `net9.0`) covering level, week, and weight math.
- **Delivery (9.1–9.4):** permanent `ApplicationId` `io.github.ajvanlaningham.forge`; release
  keystore + four repo secrets; CI publishes a signed APK + `version.json` per merge
  (`versionCode` = run number); `UpdateService` + Settings "Check for updates" verified on
  device (builds 2 → 4 → 5 upgraded in place). _Remaining (9.3): deliberately test no-network,
  no-release, and refused-install paths._
- **Levels advance (4.1):** level derived from XP; progress bar uses XP-to-next-level.
- **Weight logging (3.1, 3.2):** one `WeightEntryRow` per day in lb; 25 XP per save; Check-in
  entry pre-filled with last value; Home "log today" shortcut; `WeightTrendCard` (N-day avg,
  raw behind tap, 7-day sparkline via Microcharts) on Home and Check-in.

## Current: Training Push (week of 2026-11-11)

Bar: by end of week Forge holds its own against a finished workout app and the developer uses
it daily. **Machinery first, content last** — everything runs on a placeholder program; the
real 4-day split is written once it works, as data.

Order: 8.4 → 8.1 → 8.5 → 8.3 → 8.2 → 8.6 → 10.1, then 4.2/4.3 as stretch.

### Decisions for this push (2026-11-11)

Answered by the user before the autonomous run. "Big" = changes something already decided:
pause and ask. Medium or smaller: decide, log it here or in the commit, keep going — UI is
cheap to change later.

- **Program library, one active at a time.** Programs are user-chosen from a library that will
  grow (a kettlebell suite, a barbell gym program, free-weight splits…). Running one is a block;
  switching starts a new block. Each program declares the equipment it expects.
- **First program: Push / Pull / Legs / Full.** Placeholder content to prove the machinery.
- **Locations + swaps.** Home and Gym, each with its own equipment list (Home seeds from the
  existing Inventory). Pick a location when starting a session; a slot whose gear is missing
  offers a swap with the same movement pattern (barbell bench → DB press). The rotation never
  breaks. Progress history is per exercise, so it carries across programs and swaps.
- **Warm-ups:** optional one-tap flag per set; warm-ups excluded from progression, volume, PRs.
- **PRs:** detected and shown neutrally in history. **No PR XP, no hype** — parked over
  injury/over-lifting risk; revisit deliberately.
- **XP:** a program day earns more than a mobility quest (proposed 150 vs 50). XP-per-level is
  re-derived so a fully adherent week ≈ one level (4 × 150 + 7 × 50 = 950 from quests alone);
  weigh-ins and the conditioning bonus are slack. Final numbers live in `GameMath` with tests.
- **Phone data is disposable.** Schema changes may reset tables; no migrations this push.
- **Gym UI:** in-workout screens must be readable mid-set at arm's length. **Numerals use
  Jersey 15** (`GymNum`; SIL OFL 1.1, license in `Resources/FontLicenses/`), picked by the user
  from four candidates on 2026-11-11.
- **Device QA:** the user provides a wireless-adb port at the start; deploy with
  `deploy-android.sh` and check screens via `adb exec-out screencap`.
- **Push cadence:** push to `main` once per finished feature (builds, tests pass, verified).

Defaults taken without asking (veto-able):

- Program JSON references exercises by **name** (the unique key) — `ExerciseRow.Id` is
  autoincrement and not stable across reseeds.
- Loads follow the lb/kg toggle, stored in lb. Dumbbell load = one handle; kettlebell = one bell.
- Bodyweight and timed exercises progress reps/seconds, not load.
- Rest-timer alert = vibration + local notification; if notification permission is denied,
  in-app alert only.
- Gym location's equipment defaults to "everything", editable.
- New screens use inline strings, not `AppResources.resx`: its `.Designer.cs` is generated by a
  Visual Studio tool that doesn't run on the Linux build host. Move them into resx if/when that
  tooling exists.
- With a program active, the Quests focus pill shows the program day and weekday "Recovery"
  days no longer apply to lifting (mobility still uses the recovery pool on Wed/Sat/Sun).

### 8.4 Training programs (the 4-day split)
- Program → days → slots (exercise, sets, rep range, rest). Seeded from versioned JSON in
  `Resources/Raw`, gated by a version constant like the exercise library.
- One active program; next day offered on Train and Home. Days advance by completion order —
  a missed Tuesday is still Day 2. At most one program day per calendar day.
- Strength quest = next program day; completing it advances the program and grants quest XP.
  Mobility stays daily and date-generated; conditioning stays weekly; the date-based body-zone
  generator is the fallback when no program is active.
- Editing program content = a new program version; history keeps what was prescribed.
- [x] Model programs/days/slots — `Forge.Core/Training`, parsed + validated from
      `Resources/Raw/programs/*.json`, read on demand (no seed gate). Tests validate shipped
      programs against the shipped library.
- [x] Program day completions keyed by block + date (`ProgramDayCompletionRow`); the date-based
      generator remains the fallback
- [x] Active program + next day (`ProgramService`, `ProgramRotation`); picker on the Train tab.
      The default program auto-starts on a fresh install only.
- [x] Placeholder PPL/Full JSON; 14 gym/free-weight lifts added to the library (`LibraryVersion` v2)
      and `SquatRack` / `CableMachine` / `Machine` equipment
- [x] Re-tune XP: program day 150, mobility 50, conditioning goal 300 → **1250 XP/level**
      (weigh-ins are slack on top)
- [ ] Later: real 4-day split content

### 8.1 Log a training session
- Session = date, start/end, exercises, sets (reps × load). Loads stored in lb.
- Start from the program day/quest (pre-filled) or empty (add from library).
- Each exercise shows last time's numbers and the 8.5 suggestion; one tap confirms a pre-filled
  set; reorder/skip/swap mid-session.
- In-progress session survives backgrounding and process death.
- [x] Session/exercise/set rows; `ITrainingService` / `TrainingService` registered
- [x] Active-session page (`ActiveSessionPage`), started/resumed from the Train tab; tap a number
      to type it; screen kept awake during a session
- [x] Last-time numbers (most recent finished session with working sets) and pre-fill
- [x] In-progress state persisted per set; verified resume across an app update on device
- [x] Locations: Home = owned Inventory, Gym = `UserSettings.GymEquipment` (default all).
      Swaps at session start never duplicate an exercise already in the session; empty Home
      gear prompts to set it up. Bodyweight moves log *added* load.
- [x] Gym equipment editor (Settings → Gym equipment)

### 8.5 Progressive overload
- Default **double progression**: when every working set hits the top of the rep range, next
  load goes up one increment and reps reset low. Missing the bottom holds the load; repeated
  misses hint at a deload, never act on it.
- Per-equipment increments, configurable. Suggestion is a pre-fill, never a constraint.
- [x] Rule in `Forge.Core` (`Progression`) with tests: no history, partial session, changed rep
      range, repeated misses (hint only), back-off sets, bodyweight, timed, kg steps
- [x] Suggestion pre-fills the first set and shows its reason on the card; the summary shows
      "Next time" per exercise
- [x] Per-equipment increments (`LoadIncrements`): 5 lb / 2.5 kg, kettlebells 9 lb / 4 kg
- [ ] Make increments user-configurable (defaults are fine until someone has odd plates)

### 8.3 Rest timer
- Auto-starts on set log (slot rest time or settings default); pause, +15 s, skip.
- Alerts with screen off / app backgrounded. Computed from a stored end time, not ticks.
- [x] Sticky rest bar on the session screen: big countdown, progress, −15 / +15 / pause / skip.
      Auto-starts after every set (slot rest, else `UserSettings.DefaultRestSeconds` = 90 s;
      warm-ups capped at 60 s)
- [x] End time (or paused seconds) persisted on the session row — survives process death
- [x] `IRestAlarm`: Android exact alarm (USE_EXACT_ALARM) → high-importance vibrating
      notification on a `rest_timer` channel; verified firing with the app backgrounded.
      Notification permission asked once at session start; if denied, the app vibrates in-app.
      iOS gets a no-op until it's built.
- [x] Default rest time editable in Settings (15 s steps)

### 8.2 Sessions complete quests
- Finishing a session completes its quest via the existing XP path. Manual checkbox still
  works. No double award.
- [x] Finishing a program session completes its program day via `ProgramService.CompleteDayAsync`
      — the same path as the checkbox, which no-ops if the day is already done today, so XP can't
      be double-awarded. Requires ≥1 working set and the session's day still being on offer.
- [ ] Unit test the double-award guard (needs the service logic pulled into Forge.Core or a
      repository fake — not yet)

### 8.6 Progress you can see
- Session history (date, program day, duration, volume); per-exercise history with best-e1RM
  chart (Epley `w * (1 + reps/30)`, shared with 5.2).
- PRs detected on save (heaviest load, best e1RM, most reps at a load); end-of-session summary
  with duration, volume, PRs, XP, next-time suggestions.
- [x] Epley + PR detection (`StrengthMath`) with tests. First session is a baseline, not records.
- [x] Session history (Train → History) → tap for the session summary
- [x] Per-exercise history: best-e1RM chart (bodyweight: reps, timed: seconds) + every set;
      reachable from the summary and the exercise ⋯ menu
- [x] Summary: duration, volume, "Day N complete · +150 XP", records stated plainly, next time

### 10.1 Daily backup
- `VACUUM INTO` (never a file copy of a live DB), written **outside** app-private storage so it
  survives uninstall. Triggered on foreground if >1 day old; keep ~14; never blocks startup or
  shows a modal. "Last backed up" shown in Settings.
- [x] Location: MediaStore `Download/Forge` (shared storage, no permission, outlives the app;
      Android 10+). SAF would need a one-time folder pick; not worth it for daily copies.
- [x] `BackupService`: `VACUUM INTO` → sink, on window Created/Resumed if none today (local
      calendar day), fire-and-forget; retention 14 (`BackupPolicy`, tested); schema version
      stamped in the file name (`-s2`) for 10.2
- [x] Settings → Backup: last backup time + location, "Back up now". Verified on device: launch
      backup written, `integrity_check` ok, contents match the live DB.
- [x] Proven 2026-11-11 with the user: uninstall → backups still in Download/Forge → reinstall
      → restore → data identical (XP, sessions, sets, program days, quest completions).
- [x] Auto-backup skips an empty database, so a fresh reinstall can't write a blank "newest"
      copy that someone would restore by mistake.

### 4.2 / 4.3 Level-up moment and streak (stretch)
- Visible level-up feedback, fired exactly once per level (persist last celebrated level);
  every 4th level announces a test window.
- XP sources enumerated in one place (quests, check-ins, tests, conditioning goal); symmetric
  undo; daily streak displayed; weekly conditioning completion notifies
  (`QuestsViewModel.cs` TODO).
- [x] Level-up screen (`LevelUpPage`), fired once per level via a persisted last-celebrated
      level (`ProgressService`); checked on Home, after quest XP, and on the session summary.
      Fresh installs record their level silently; undo-then-re-earn doesn't re-celebrate.
      Verified on device (crossed 1250 → level 2; no repeat on revisit).
- [x] Daily streak (`StreakMath`, tested): days with any completed quest or program day; today
      not done yet doesn't break it. Shown on Home with the best run.
- [x] Weekly conditioning goal notifies when reached (the old `QuestsViewModel` TODO)
- [ ] Consolidate XP awards behind one service (quests, program days, weigh-ins, conditioning
      each still grant their own) and unit-test undo symmetry
- [ ] Test-window announcement is a placeholder line until Epic 5 exists

## Later

### Weight & check-in
- **3.3 Goal chart:** add start/milestone/goal weight to `UserSettings`; chart the average
  with goal line and milestone marker; quiet trend-based ETA (never a pressure countdown);
  verify empty / one-point / one-year states.
- **3.4 Rest of check-in:** `DailyCheckInRow` (sleep, steps, RHR, note), all optional, with a
  per-field manual-vs-synced flag so sync never overwrites hand entries.

### Baseline testing (Epic 5)
- **5.1** Implement log scoring (Decision 5) with XML docs; delete `ScoreFrom`/`ScoreStepRatio`;
  scores can fall; tests for baseline=10, doubling=35, halving, retest cap.
- **5.2** Protocols from `docs/baseline-tests.md` as versioned seed data; record per-lift fixed
  loads at first test. CON is scored in total reps, not rounds.
- **5.3** Guided test flow with timers; `StatTestRow` (date, kind, protocol version, raw
  measures, bodyweight, score); grants quest XP; abandon/resume safely.
- **5.4** 7-day test window on every 4th level; three sessions; expires without penalty.
- **5.5** First baseline taken twice a week apart, second one kept; guarded reset only.
- **5.6** Stat cards: score, delta, sparkline, raw measures, relative + absolute e1RM, next
  window date.

### Nutrition & devices (Epics 6, 7)
- **6.1** Manual protein/calorie fields on check-in, configurable targets, shown next to weight.
- **6.2** Spike first: does MyFitnessPal write nutrition to Health Connect? If yes, read and
  merge; if no, consider Open Food Facts as its own PBI.
- **7.1** Choose a wearable (Amazfit/Mi Band + Gadgetbridge, Garmin, Bangle.js 2); sensor
  quality for overnight RHR is the binding constraint.
- **7.2** Health Connect read of RHR, steps, sleep, weight on foreground; full permission
  denial/revocation handling; absent Health Connect never crashes.
- **7.3** RHR trend card reusing the moving-average helper.

### Data safety
- **10.2 Restore:** ✅ done 2026-11-11. Settings → Restore opens the system picker directly in
  Download/Forge; validates (SQLite, Forge tables, integrity, schema ≤ current); confirms with a
  summary; backs up current data first; swaps the DB; closes the app. Verified end to end.
- **Decide: Android Auto Backup.** The manifest has `allowBackup="true"`, so Android silently
  restored an older cloud snapshot (225 XP from that morning) on reinstall. Keep it as a free
  second copy, or turn it off so a reinstall is clean and restore is explicit?
- **10.3 Export:** share latest backup via share sheet; schema-versioned JSON export; off the UI
  thread.

### Small fixes
- **2.6** My Gear: only `dumbbell.png` exists for 20 equipment types — add sprites or fall back
  to a Font Awesome glyph per group.
- **2.7** `App.xaml:11` registers `NullToBoolInverseConverter` under the `QuestExerciseFormatter`
  key. Latent (local declarations win), but fix it and drop redundant local copies.

### Future milestones
- **Guild:** deferred; consider hiding the tab until it does something.
- **Speed stat:** at lower bodyweight; adding a `StatKind` is cheaper before a year of history.
- **BLE chest strap** (e.g. Polar H10) for in-workout HR via the standard BLE HR profile.
- **Maintenance/recomposition** mode after goal weight.
- **Nutrition database** (Open Food Facts / USDA) only as a last resort.

## Ongoing

- Verify deploys on device, not from MSBuild output (see `AGENTS.md`).
- Keep `README.md` and `AGENTS.md` in agreement.
- Unit-test new pure logic in `Forge.Core`, especially scoring, progression, and date math.
