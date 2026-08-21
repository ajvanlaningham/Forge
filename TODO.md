# Forge v1 Backlog

This backlog takes Forge from a working quest/XP prototype to a personal daily driver.

The prototype proved the core loop: seed an exercise library from JSON, generate deterministic
daily quests, mark them complete, earn XP, and display it. What it does not yet do is the thing
the app exists for — track how the user is actually doing, and show them getting better.

Reference points:

- Build and deploy rules: [`AGENTS.md`](AGENTS.md) — source of truth over `README.md`
- Product description: [`README.md`](README.md) (Getting Started / Deployment sections are stale)
- Project: `Forge/Forge/Forge.csproj`, solution `Forge/Forge.sln`

## Target Outcome

By the end of v1:

- Daily weigh-ins are logged in seconds and displayed as a 7-day average with a goal line.
- The user's level advances from consistency, and the level actually changes on screen.
- Stat scores move, driven by a real baseline test taken roughly monthly.
- Protein and calories are visible next to weight, without Forge owning a food database.
- Resting heart rate, steps, and sleep arrive from a device, with manual entry as fallback.
- The repo has no duplicated or stale documentation, no orphan seed data, and a test project.
- The app is pleasant enough to open every morning that the user keeps opening it.

## Scope

### Included

- Android only (`net9.0-android`), single user, no accounts, no sync, no backend.
- Daily weight logging with trend display and goal tracking.
- Level progression driven by quest consistency.
- Baseline stat testing on a ~monthly cadence with logarithmic scoring.
- Manual nutrition entry (protein, calories) and Health Connect nutrition read.
- Health Connect integration for resting heart rate, steps, sleep, and body weight.
- Basic training session logging that feeds quest completion.
- Repo cleanup, drift control, and a unit test project.
- CI-built release APKs published to GitHub Releases, installed via an in-app update button.

### Deferred

- Guild / social / multiplayer. No other users exist. Dogfood first.
- Speed as a fourth stat. Revisit at a lower bodyweight where impact testing is safer.
- Food database, barcode scanning, recipe building. Forge reads nutrition; it does not own it.
- iOS and Windows builds. The csproj already guards these correctly; leave it alone.
- Cloud backup, export, multi-device.
- Maintenance / recomposition mode after the goal weight is reached.

## Design Decisions

These are settled. Changing one means revisiting the PBIs that depend on it.

1. **Three independent progress systems.** Levels measure *consistency* (time and adherence).
   Stat scores measure *capability* (periodic baseline tests). Weight is its own track and
   feeds neither. They answer different questions and must not be collapsed.

2. **Weight never earns XP.** XP is granted for *logging* a weigh-in, never for what the scale
   says. Daily weight is noisy; punishing a bad reading punishes something the user does not
   control.

3. **Weight is not the hero element.** The home screen shows the 7-day average; the raw daily
   number lives behind a tap. The stated goal is to feel better, and weight can easily become
   an obsession. The UI should not invite that.

4. **Three stats: STR, DEX, CON.** DEX stays mobility/flexibility as originally coded, which
   keeps the daily mobility quest attached to a number that moves. Speed is deferred.

5. **Logarithmic stat scoring.** `score = 10 + 25 * log2(current / baseline)`, clamped 1-100,
   with a maximum gain of +10 per retest. Doubling any metric is worth +25 points regardless
   of starting value. This replaces the current linear `ScoreFrom`, which awards +1 per 10%
   improvement from a floor of 1 and therefore requires ~990% improvement to reach 100.

6. **Relative strength.** STR scores on estimated 1RM divided by bodyweight, with absolute
   e1RM displayed alongside. A month spent holding lifts while losing weight is progress and
   should read as progress.

7. **Frozen test protocols.** Once a baseline test is set, its movements, reps, order, and
   loads never change. Improving the test destroys comparability with every prior result.

8. **Test week, not test day.** A test window opens on hitting every 4th level and stays open
   7 days, containing three separate sessions. Each grants XP so testing never breaks a streak.

9. **Read nutrition, do not build it.** MyFitnessPal has no usable public API. Forge takes
   manual entry and Health Connect totals. It will not attempt to out-database MFP.

10. **Manual entry ships before every integration.** Every synced value must have a working
    manual path first, because sync breaks and the user still needs to log.

## Assumptions

- Single user, single device, single phone. No migration story is needed for data loss beyond
  "reinstall and re-baseline."
- Build and deploy continue to follow `AGENTS.md`: build on the Linux host, deploy via
  `./deploy-android.sh` over Tailscale.
- The user starts around 230 lb, targets 218 lb as the near milestone (5% loss) and 200 lb
  within the following year. These are user data, not constants — they belong in settings.
- Baseline test equipment is limited to what is on hand: dumbbells, kettlebells, a bike,
  a mat, and a wall.
- Level cadence stays at 1050 XP per level, 50 XP per quest, 21 quests per week — one level
  per week at full adherence, so every 4 levels is roughly monthly.

## External Dependencies

- **Health Connect** — Android system app. Requires platform interop work; no first-party MAUI
  binding exists. Blocks all device sync PBIs.
- **A wearable.** Not yet chosen. Candidates: Amazfit or Mi Band paired with Gadgetbridge
  (open software layer, no vendor cloud); Garmin (better sensors and sleep tracking, Connect IQ
  SDK); Bangle.js 2 (fully hackable, JavaScript apps, weaker optical sensor). A BLE chest strap
  such as a Polar H10 is a separate option for accurate in-workout HR via the standard BLE
  heart-rate profile, readable directly with no vendor SDK.
- **GitHub Actions and a release keystore.** Blocks Epic 9. The keystore is unrecoverable if
  lost — every future update would fail to install over existing builds. Back it up off-machine.
- **Open Food Facts / USDA FoodData Central** — only if manual nutrition entry proves too
  tedious and barcode lookup becomes necessary. Not planned for v1.

## Done Criteria

v1 is done when:

- The user has logged weight daily for a month without the app getting in the way.
- The level shown on the home screen has advanced at least once from real quest completion.
- At least one baseline test has been taken, scored, and retested, and the stat cards moved.
- Protein and calories appear on the check-in screen from at least one source.
- Resting heart rate is visible in the app, whether synced or hand-entered.
- `dotnet build Forge/Forge/Forge.csproj -f net9.0-android` is clean and the test project passes.
- A merge to `main` produces a signed build the phone can install from the Settings screen,
  without the developer being at the build host.
- `README.md` exists once, is accurate, and does not contradict `AGENTS.md`.

## Sprint Shape

Six sprints, nominally two weeks each, but this is a solo hobby project worked on in evenings —
sprint boundaries are sequencing, not deadlines. Nothing here is time-critical except that the
user is trying to lose weight *now*, which is why weight logging is Sprint 2 and not Sprint 5.

- Sprint 1: cleanup, drift control, test project, constants restructure.
- Sprint 2: delivery pipeline, then weight logging and the check-in screen.
- Sprint 3: close the progression loop — levels advance, level-up is visible.
- Sprint 4: baseline testing and stat scoring.
- Sprint 5: nutrition and Health Connect.
- Sprint 6: training session logging.

---

## Epic 1: Backlog and Design Decisions

### PBI 1.1: Write the v1 backlog

**User story:** As the developer and sole user, I want the remaining work organized into epics
and PBIs so I can pick up the project after a two-week gap and know exactly what is next.

**Acceptance criteria:**

- `TODO.md` describes target outcome, scope, design decisions, assumptions, and done criteria.
- Remaining work is grouped into epics and PBIs with acceptance criteria and task checkboxes.
- A sprint plan maps PBIs into an execution order.
- Settled design decisions are captured so they are not relitigated.

**Tasks:**

- [x] Establish goals: weight loss to 200 lb, feeling better, strength/mobility/endurance.
- [x] Decide the three progress systems and how they differ.
- [x] Decide stat count (three) and what DEX measures (mobility).
- [x] Decide the scoring curve (logarithmic) and fix the broken linear scale.
- [x] Decide the nutrition strategy (read, do not build).
- [x] Write `TODO.md`.

### PBI 1.2: Record decisions that outlive the backlog

**User story:** As the developer, I want the non-obvious design decisions written where I will
find them, so a future me does not "simplify" the AMRAP circuit and destroy a year of data.

**Acceptance criteria:**

- `AGENTS.md` gains a short "Product rules" section covering: weight never earns XP, test
  protocols are frozen, manual entry precedes every integration.
- The frozen baseline protocols are documented with their exact movements, reps, and loads.
- The scoring formula and its constants are documented next to the code that implements them.

**Tasks:**

- [x] Add a "Product rules" section to `AGENTS.md`.
- [x] Create `docs/baseline-tests.md` with the three frozen protocols.
- [ ] Document the log scoring formula in XML comments on the scoring helper. _Blocked: the helper lands with PBI 5.1. Formula is documented in `docs/baseline-tests.md` meanwhile._

---

## Epic 2: Repo Cleanup and Drift Control

The user explicitly asked for this early, to stop drift before adding features on top of it.

### PBI 2.1: Collapse duplicated and stale documentation

**User story:** As the developer, I want one accurate README so I do not maintain two that
disagree with reality and with each other.

**Acceptance criteria:**

- Exactly one `README.md` remains. `Forge/README.md` is currently a byte-identical copy apart
  from a trailing newline.
- The "Getting Started" section no longer claims .NET 8; the project targets `net9.0-android`.
- The "Deployment" section no longer instructs the reader to build from Visual Studio. It
  points at `./deploy-android.sh` and defers to `AGENTS.md`.
- `README.md` and `AGENTS.md` do not contradict each other anywhere.

**Tasks:**

- [x] Delete `Forge/README.md`, keep the root copy.
- [x] Fix the .NET version and the `net8.0-android` build command.
- [x] Replace the Visual Studio deployment section with a pointer to `deploy-android.sh`.
- [x] Reconcile the Features list against what actually exists today.

### PBI 2.2: Clean up seed data

**User story:** As the developer, I want the exercise library to contain only files that are
actually loaded, so I stop wondering which ones matter.

**Acceptance criteria:**

- `Resources/Raw/exercises.v1.json` (24 entries, referenced by nothing, superseded by the four
  category files) is removed or explicitly documented as retained.
- Every file in `Resources/Raw/` is listed in `GameConstants.Exercises.LibraryFiles`.
- The importer's version gate is documented: adding a file requires bumping `LibraryVersion`,
  since the check is version-only and will otherwise skip seeding entirely.

**Tasks:**

- [x] Confirm nothing loads `exercises.v1.json`, then delete it.
- [x] Add a comment on `LibraryFiles` explaining the version-bump requirement.
- [x] Consider a debug-only "reseed library" action to avoid reinstalling during development.

### PBI 2.3: Untangle the constants

**User story:** As the developer, I want to reference a constant without deciphering which
`GameConstants` I am looking at.

**Acceptance criteria:**

- `GameMath.GameConstants` no longer shadows the top-level `GameConstants`. Call sites read
  clearly rather than as `GameMath.GameConstants.Quests.XpPerQuest`.
- The dead `const int xpPerLevel = 1000;` at `Forge/Forge/ViewModels/HomeViewModel.cs:58` is
  removed. It is unused and contradicts the real value of 1050.
- Personal targets (start weight, goal weight, milestone weight, weekly conditioning goal) are
  user settings, not compile-time constants.

**Tasks:**

- [x] Rename the nested class so it stops shadowing, and update call sites.
- [x] Delete the dead `xpPerLevel` local.
- [x] Move personal targets to `Preferences` or a settings row.

### PBI 2.4: Fix naming and comment drift in QuestService

**User story:** As the developer, I want method names to describe what they do, so I do not
trust a comment over the code.

**Acceptance criteria:**

- `AreAllQuestsCompletedAsync` either checks all three quest kinds or is renamed to reflect
  that it deliberately checks only Strength and Mobility (conditioning being weekly).
- `TryAwardDailyCompletionXpAsync` is renamed to reflect that it awards per-quest XP, not a
  daily completion bonus.
- The "If all three are now complete, award XP once" comment in `QuestsViewModel` is corrected.
- `QuestService._cache` either evicts old dates or is documented as intentionally unbounded.

**Tasks:**

- [x] Rename the two misleading methods and update call sites.
- [x] Fix the stale comment in `QuestsViewModel`.
- [x] Add simple eviction to the daily quest cache, keeping a few days at most.

### PBI 2.5: Stand up a test project

**User story:** As the developer, I want somewhere to put tests so that the pure logic — which
is where the real bugs will be — is actually covered.

**Acceptance criteria:**

- A test project exists in the solution and runs on the Linux build host without an emulator.
- It targets pure logic only: scoring math, level math, date/week helpers, quest assembly.
- `AGENTS.md`'s "there is no test project yet" note is updated.
- Tests pass in a clean checkout.

**Tasks:**

- [x] Add `Forge.Tests` (xUnit) targeting `net9.0` and add it to the solution.
- [x] Extract pure helpers so they are testable without MAUI types.
- [x] Cover `LevelFromXp`, `XpIntoLevel`, `LevelProgress`, and the Monday-of-week helper.
- [x] Update `AGENTS.md`.

### PBI 2.6: Fix or remove the equipment sprite path

**User story:** As the user, I want the gear screen to look intentional rather than broken.

**Acceptance criteria:**

- `GearItem.SpriteSource` derives filenames for all 20 equipment types, but only
  `Resources/Images/Equipment/dumbbell.png` exists. Either the missing sprites are added, or
  the UI falls back cleanly to a placeholder or an icon font glyph.
- No missing-image gaps appear on the My Gear screen.

**Tasks:**

- [ ] Decide: source 20 sprites, or fall back to a Font Awesome glyph per group.
- [ ] Implement the fallback path.
- [ ] Verify the My Gear screen on device.

---

## Epic 3: Check-In and Weight Tracking

The core of the app for its actual user, and currently a static stub.

### PBI 3.1: Log a daily weigh-in

**User story:** As the user, I want to log my weight in under five seconds each morning, so
that logging never becomes the reason I stop.

**Acceptance criteria:**

- A `WeightEntryRow` table stores date (`yyyy-MM-dd` primary key), weight, and optional note.
- One entry per day; re-logging the same day overwrites rather than duplicating.
- Entry is reachable in one tap from the home screen, with a numeric keypad and the last
  value pre-filled as the starting point.
- Units are configurable (lb/kg) and stored consistently, converting only for display.
- Logging a weigh-in grants XP. The value logged does not affect XP in any way.

**Tasks:**

- [ ] Add `WeightEntryRow` and register `IRepository<WeightEntryRow>` usage.
- [ ] Add `IWeightService` / `WeightService` with upsert-by-date semantics.
- [ ] Build the entry UI and wire the home-screen shortcut.
- [ ] Grant logging XP through the existing XP path.
- [x] Register the service in `MauiProgram.cs`.

### PBI 3.2: Show the trend, not the number

**User story:** As the user, I want to see my 7-day average rather than today's reading, so a
salty dinner does not read as failure.

**Acceptance criteria:**

- The check-in and home screens display a 7-day moving average as the primary weight figure.
- Today's raw reading is available behind a tap, not shown by default.
- The average handles gaps gracefully — a missed day does not blank the display.
- Fewer than 7 days of data shows an average over what exists, labelled as such.

**Tasks:**

- [ ] Implement the moving average with gap tolerance.
- [ ] Build the weight card with average primary and raw behind a tap.
- [ ] Decide and implement the "not enough data yet" state.

### PBI 3.3: Chart progress against the goal

**User story:** As the user, I want to see the trend line against my goal so I can tell whether
what I am doing is working.

**Acceptance criteria:**

- A chart shows the 7-day average over time with a goal line at the target weight and a marker
  at the near-term milestone.
- The chart is readable on a phone, in both light and dark themes.
- An estimated arrival date at the goal is derived from the recent trend, and is presented
  quietly. It must not become a countdown that induces pressure.
- The chart handles a single data point without crashing or looking broken.

**Tasks:**

- [ ] Choose a charting approach that works in MAUI on Android.
- [ ] Render the average series, goal line, and milestone marker.
- [ ] Compute the trend estimate and decide its presentation.
- [ ] Verify empty, one-point, and one-year data states.

### PBI 3.4: Log the rest of the daily check-in

**User story:** As the user, I want sleep, steps, and resting heart rate in the same place as
weight, so the check-in is one screen and one habit.

**Acceptance criteria:**

- A `DailyCheckInRow` stores date, sleep hours, steps, resting heart rate, and optional note.
- All fields are optional; a partial check-in is valid and still grants logging XP.
- Fields that will later be synced from a device are marked as manual-entered, so sync can
  overwrite them without destroying hand-entered history.
- The check-in screen replaces the current static stub.

**Tasks:**

- [ ] Add `DailyCheckInRow` with a source flag per field (manual vs synced).
- [ ] Add `ICheckInService` / `CheckInService`.
- [ ] Build the check-in UI over the existing `CheckInPage`.
- [ ] Register the service in `MauiProgram.cs`.

---

## Epic 4: Close the Progression Loop

XP accrues today, but `UserStats.Level` is seeded at 1 and nothing ever writes it. The user can
never level up. This is the single largest gap between what the README promises and what runs.

### PBI 4.1: Make levels advance

**User story:** As the user, I want my level to go up when I have earned it, because that is
the entire reward for consistency.

**Acceptance criteria:**

- `UserStats.Level` is derived from XP via `LevelFromXp` rather than being a stored value that
  drifts, or it is written every time XP changes. Derived is preferred.
- The home and stats screens show the correct level immediately after XP is awarded.
- `XpToNextLevel` drives the progress display instead of the raw XP total.
- Existing saved data with `Level = 1` and nonzero XP corrects itself on next load.

**Tasks:**

- [ ] Derive level from XP wherever it is displayed.
- [ ] Wire `XpToNextLevel` into the stat card progress bar.
- [ ] Verify against a seeded XP value that spans several levels.
- [ ] Add unit tests for the level math boundaries (0, 1049, 1050, 1051).

### PBI 4.2: Make leveling up feel like something

**User story:** As the user, I want to notice when I level up, so the milestone lands instead
of quietly incrementing a number.

**Acceptance criteria:**

- Crossing a level boundary triggers visible feedback distinct from the existing XP dialog.
- The feedback fires once per level, and does not re-fire on navigation or app restart.
- Reaching a level divisible by 4 announces that a test window has opened (see Epic 5).

**Tasks:**

- [ ] Detect level-boundary crossings in the XP award path.
- [ ] Build the level-up presentation.
- [ ] Persist "last celebrated level" so it fires exactly once.
- [ ] Hook the test-window announcement.

### PBI 4.3: Finish the quest XP path

**User story:** As the user, I want quest completion, check-in logging, and test sessions to
all feed one XP system, so consistency is measured consistently.

**Acceptance criteria:**

- XP sources are enumerated in one place: quests, check-in logging, test sessions, weekly
  conditioning goal.
- The `TODO` at `Forge/Forge/ViewModels/QuestsViewModel.cs:170` is resolved — completing the
  weekly conditioning goal notifies the user.
- Undo paths are symmetric: unchecking a quest removes exactly the XP it granted, as the
  existing implementation already does.
- A streak count exists, since consistency is what levels measure.

**Tasks:**

- [ ] Consolidate XP awards behind one service method.
- [ ] Add the weekly conditioning completion notification.
- [ ] Implement and display a daily streak.
- [ ] Unit test the award and undo symmetry.

---

## Epic 5: Baseline Testing and Stat Scores

### PBI 5.1: Replace the scoring math

**User story:** As the user, I want my stat scores to move by a meaningful amount when I
improve, on a scale that is actually reachable.

**Acceptance criteria:**

- Scoring is `score = 10 + 25 * log2(current / baseline)`, clamped to 1-100.
- A single retest cannot move a score by more than +10.
- The existing linear `ScoreFrom` — +1 point per 10% improvement from a floor of 1, requiring
  ~990% improvement to reach 100 — is removed, not left alongside.
- Scores can decrease. Detraining is real information and should be visible.
- Unit tests cover: baseline (10), doubling (35), halving, and the retest cap.

**Tasks:**

- [ ] Implement the log scoring helper with XML documentation.
- [ ] Delete `ScoreFrom` and its `ScoreStepRatio` constant.
- [ ] Add the per-retest cap.
- [ ] Write the unit tests.

### PBI 5.2: Define and store the frozen test protocols

**User story:** As the user, I want the test to be identical every time, so the comparison
across months is real.

**Acceptance criteria:**

- Three protocols are defined and frozen:
  - **STR** — goblet squat, dumbbell floor press, dumbbell row; max clean reps at a fixed load
    per lift. Estimated 1RM via Epley (`w * (1 + reps/30)`), divided by bodyweight, averaged
    across the three lifts.
  - **DEX** — deep squat hold, measured in seconds. Optional sit-and-reach as a second measure.
  - **CON** — 10-minute AMRAP of a fixed circuit, scored as **total reps**, not rounds.
- The CON circuit is sized to yield roughly 6-10 rounds, so a round is a small unit of work and
  partial rounds give fine resolution. Scoring rounds rather than reps is explicitly rejected:
  2 to 3 rounds reads as a 50% improvement and would swamp the scale.
- Protocol definitions are stored as data, versioned, and never edited in place.
- Fixed loads are recorded per lift at baseline and reused for every retest.

**Tasks:**

- [ ] Write `docs/baseline-tests.md` with exact protocols.
- [ ] Model protocols as versioned seed data.
- [ ] Record per-lift fixed loads at first test.
- [ ] Implement the Epley e1RM helper with unit tests.

### PBI 5.3: Run a test session

**User story:** As the user, I want the app to walk me through a test and record it, so I am
not tracking reps on a napkin.

**Acceptance criteria:**

- A `StatTestRow` stores date, stat kind, protocol version, raw measures, bodyweight at test
  time, and the resulting score.
- The test flow presents one movement at a time with a timer where relevant.
- Bodyweight at test time is captured, since STR scoring depends on it.
- Completing a test session grants XP equal to a quest, so testing never breaks a streak.
- A test session can be abandoned partway without corrupting stored history.

**Tasks:**

- [ ] Add `StatTestRow` and its repository usage.
- [ ] Build the guided test flow with timers.
- [ ] Capture bodyweight at test time.
- [ ] Grant test-session XP.
- [ ] Handle abandonment and resume.

### PBI 5.4: Open a test window every four levels

**User story:** As the user, I want the app to tell me when it is time to retest, spread across
a week, so I do not have to do an hour of maximal work in one session.

**Acceptance criteria:**

- Reaching a level divisible by 4 opens a 7-day test window.
- The window contains three sessions (STR, DEX, CON) completable on any days within it.
- The app suggests spacing strength and conditioning apart; mobility can pair with anything.
- An unfinished window closes without penalty and the stats simply keep their prior scores.
- The next window opens at the next multiple of 4 regardless of whether the last was completed.

**Tasks:**

- [ ] Model the test window with open/close dates and per-session completion.
- [ ] Trigger window opening from the level-up path.
- [ ] Build the window UI showing which sessions remain.
- [ ] Define and implement expiry behavior.

### PBI 5.5: Establish a trustworthy baseline

**User story:** As the user, I want my baseline to be accurate, because an artificially low
first test inflates every score I ever earn afterward.

**Acceptance criteria:**

- The first baseline runs twice, roughly a week apart, and the **second** result is stored as
  the permanent baseline.
- The app explains why, so the second test does not feel like a bug.
- After the calibration period, baselines are immutable except through an explicit,
  clearly-labelled reset that warns about losing comparability.

**Tasks:**

- [ ] Implement the two-pass calibration flow.
- [ ] Write the explanatory copy.
- [ ] Add a guarded baseline reset.

### PBI 5.6: Show stat history

**User story:** As the user, I want to see that I am stronger and more mobile than I was in
the spring, because that is the actual point of the app.

**Acceptance criteria:**

- Each stat card shows current score, change since last test, and a sparkline of history.
- Raw measures are visible alongside scores — "14 reps at 50 lb", not only "STR 23".
- For STR, both relative and absolute e1RM are shown.
- The next test window date is visible.

**Tasks:**

- [ ] Extend `StatCardViewModel` with history and delta.
- [ ] Build the sparkline.
- [ ] Surface raw measures alongside scores.
- [ ] Show the next-test date.

---

## Epic 6: Nutrition

### PBI 6.1: Manual protein and calorie entry

**User story:** As the user, I want to record protein and calories without building a food
diary, so the number is present without the logging burden.

**Acceptance criteria:**

- Daily protein and calorie totals are entered as two numbers on the check-in screen.
- Targets are configurable and progress against them is displayed.
- Entry is optional; skipping it does not break the check-in or forfeit logging XP.
- Totals appear alongside the weight trend, since the two together are the useful picture.

**Tasks:**

- [ ] Add protein and calorie fields to the daily check-in.
- [ ] Add configurable targets in settings.
- [ ] Display progress against targets.

### PBI 6.2: Read nutrition totals from Health Connect

**User story:** As the user, I want to keep logging food in MyFitnessPal and have Forge read
the totals, so I get a good food database without writing one.

**Acceptance criteria:**

- Forge reads daily nutrition totals from Health Connect where available.
- Whether MyFitnessPal actually writes nutrition to Health Connect is **verified first** — this
  PBI is contingent on that spike, and is dropped if the answer is no.
- Synced values overwrite the synced field only; hand-entered values are preserved and
  distinguishable.
- Manual entry continues to work when the sync is unavailable.

**Tasks:**

- [ ] Spike: confirm whether MFP writes nutrition records to Health Connect.
- [ ] If yes, read daily totals and merge with manual entry.
- [ ] If no, evaluate Open Food Facts barcode lookup as a separate PBI.

---

## Epic 7: Device and Health Connect Integration

Everything here is behind manual entry, which must already work.

### PBI 7.1: Choose a wearable

**User story:** As the user, I want a device that gives trustworthy resting heart rate and is
fun to tinker with, without those two goals fighting.

**Acceptance criteria:**

- A device is selected and its data path documented.
- The tradeoff is explicit: fully open platforms (PineTime, Bangle.js 2) have weaker optical
  sensors; mainstream devices have better sensors and sleep tracking. Resting heart rate needs
  overnight wear and a decent sensor, so sensor quality is the binding constraint.
- The chosen path reaches Health Connect, whether directly or via Gadgetbridge.

**Tasks:**

- [ ] Evaluate Amazfit or Mi Band with Gadgetbridge (open software layer, no vendor cloud).
- [ ] Evaluate Garmin (better sleep and RHR, Connect IQ SDK, FIT file access).
- [ ] Decide whether a Bangle.js 2 is a parallel tinkering project rather than the data source.
- [ ] Document the selection and its sync path.

### PBI 7.2: Read health data from Health Connect

**User story:** As the user, I want resting heart rate, steps, sleep, and weight to arrive
automatically, so the daily check-in becomes confirmation rather than data entry.

**Acceptance criteria:**

- Forge requests and handles Health Connect permissions, including refusal and revocation.
- Resting heart rate, steps, sleep duration, and body weight are read on app foreground.
- Synced values fill only fields not manually entered for that day, unless the user opts into
  sync taking precedence.
- The app is fully usable with permissions denied. This is a non-negotiable fallback.
- Health Connect being absent on the device does not crash or block anything.

**Tasks:**

- [ ] Add the Android platform interop for Health Connect.
- [ ] Implement the permission request and denial flows.
- [ ] Read the four record types and merge with manual entries.
- [ ] Verify behavior with permissions denied and with Health Connect uninstalled.

### PBI 7.3: Surface resting heart rate as a trend

**User story:** As the user, I want to watch my resting heart rate come down, because it is one
of the clearest signals that I am actually getting healthier.

**Acceptance criteria:**

- Resting heart rate is displayed as a trend, averaged the same way as weight, not as a daily
  reading.
- It appears on the check-in screen and optionally on home.
- Missing days are handled without breaking the series.

**Tasks:**

- [ ] Add the RHR trend card.
- [ ] Reuse the moving-average helper from PBI 3.2.
- [ ] Decide whether RHR earns a place on the home screen.

---

## Epic 8: Training Session Logging

`TrainPage` is currently a placeholder with two navigation buttons.

### PBI 8.1: Log a training session

**User story:** As the user, I want to record what I actually did, so quest completion reflects
real work rather than a checkbox.

**Acceptance criteria:**

- A session records date, exercises performed, and sets with reps and load.
- Sessions are startable from a quest, pre-populated with that quest's exercises.
- A session can be logged without a quest, for unplanned training.
- Sessions persist and are viewable in a history list.

**Tasks:**

- [ ] Model sessions and sets.
- [ ] Build the logging UI over `TrainPage`.
- [ ] Pre-populate from a quest.
- [ ] Add a session history view.

### PBI 8.2: Feed sessions into quest completion

**User story:** As the user, I want logging a session to complete the matching quest, so I am
not recording the same thing twice.

**Acceptance criteria:**

- Completing a session that covers a quest's exercises marks that quest complete and grants XP
  through the existing path.
- Manual quest checkboxes still work for days when logging is too much friction.
- No double-award: a quest completed by session logging cannot also be completed manually for
  additional XP.

**Tasks:**

- [ ] Match logged sessions against the day's quests.
- [ ] Route completion through the consolidated XP service from PBI 4.3.
- [ ] Test the double-award guard.

---

## Epic 9: Delivery and In-App Updates

Today the only way onto the phone is `./deploy-android.sh` from the Linux host, over Tailscale,
with a port that has to be read off the phone every time wireless debugging is toggled. That is a
fine dev loop and a terrible delivery mechanism — it means new work only reaches the phone when
the developer is sitting at the build host.

This epic mirrors the pattern used in the huginn project: merging to `main` produces a
release-signed APK published as a GitHub Release with a `version.json` manifest, and an **Update**
button in the app fetches it and hands the APK to the OS installer.

**One important difference from huginn:** huginn's repo is private, so its app had to fetch the
manifest and APK through its own backend, which held the credentials. Forge is a public repo with
no backend at all, so the app can read the GitHub Releases API directly and unauthenticated. No
server, no token shipped in the app. If Forge is ever made private, this epic needs rethinking —
do not solve it by embedding a token.

`deploy-android.sh` stays as the in-hand dev loop. This is the delivery path, not a replacement.

### PBI 9.1: Settle app identity and release signing

**User story:** As the user, I want app updates to install over my existing app without wiping my
data, so that six months of weigh-ins survive a version bump.

**Acceptance criteria:**

- `ApplicationId` is changed off the template default `com.companyname.forge` to a real,
  permanently-owned identifier. **This must happen before the first signed release is installed** —
  changing the application id later is not an update, it is a different app, and the old one's
  SQLite database becomes unreachable.
- A release keystore exists, is backed up somewhere the developer will still have in a year, and
  is **not** in the repo. Losing it means no further updates can install over existing builds.
- Keystore, key alias, and both passwords are stored as GitHub Actions secrets.
- The signature mismatch is documented: a release-signed APK will not install over a debug build
  from `deploy-android.sh`. The first release install needs a one-time uninstall, which **will**
  delete the local database. Do this before there is data worth keeping.
- Confirmed: the SQLite database in `FileSystem.AppDataDirectory` survives an update that keeps
  the same application id and signing key.

**Tasks:**

- [x] Choose and set the permanent `ApplicationId`.
- [ ] Generate a release keystore and back it up off-machine. _Yours — `keytool` command is in `AGENTS.md`._
- [ ] Add `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, and
      `ANDROID_KEY_PASSWORD` as repository secrets.
- [ ] Document the one-time uninstall in `AGENTS.md` and do it before Epic 3 ships. _Documented; the uninstall itself is still to do._
- [ ] Verify data survives an update between two release-signed builds. _Needs two published releases._

### PBI 9.2: Build and publish a release APK on merge to main

**User story:** As the user, I want every merge to `main` to produce an installable build, so that
finished work reaches my phone without me being at the build host.

**Acceptance criteria:**

- A GitHub Actions workflow builds a release-signed APK on push to `main`, and on
  `workflow_dispatch` for manual runs.
- `ApplicationVersion` (the Android `versionCode`) is set from `github.run_number`, so it
  increases monotonically and the app can compare builds with a single integer.
- The workflow publishes a GitHub Release containing the APK and a `version.json` manifest with
  at least: `version_name`, `version_code`, `sha`, `built_at`, `notes`, and the APK's filename.
- A `concurrency` group serialises runs so two merges cannot race the run-number-derived version
  code or clobber each other's assets.
- Secrets never appear in logs or on a command line.
- The workflow fails loudly if no signed APK is found, rather than publishing an empty release.

**Tasks:**

- [x] Add `.github/workflows/android-release.yml`.
- [x] Decode the keystore from the base64 secret at build time.
- [x] Publish with `-p:ApplicationVersion=${{ github.run_number }}` and the signing properties.
- [x] Generate `version.json` and attach it alongside the APK.
- [ ] Verify a full run end to end, including the release actually appearing. _Needs the secrets in place._

### PBI 9.3: Fetch and install updates from inside the app

**User story:** As the user, I want to tap a button and get the newest build, so that updating does
not require a laptop, a cable, or a rotating debug port.

**Acceptance criteria:**

- `IUpdateService` / `UpdateService` fetch the latest release manifest from the GitHub Releases
  API, unauthenticated, and compare `version_code` against the installed build
  (`AppInfo.Current.BuildString`).
- Reading the installed version code never throws — if it cannot be determined, it is treated as
  unknown rather than crashing.
- The APK downloads to the app cache with progress reported, using a client whose timeout is sized
  for a multi-megabyte download rather than the default for ordinary requests.
- Install is handed to the OS package installer via a `FileProvider` content URI and an
  `ACTION_VIEW` intent with read permission granted.
- `AndroidManifest.xml` declares the `FileProvider` (authority `${applicationId}.fileprovider`,
  paths in `Resources/xml/file_paths.xml`) and the `REQUEST_INSTALL_PACKAGES` permission.
- Install is Android-only and throws a clear `PlatformNotSupportedException` elsewhere.
- Every failure path — no network, no release published, GitHub unreachable, rate limited,
  user cancels the OS installer — produces a readable message, never a crash.
- The app is fully usable with no network. Update checking is never on the startup path.

**Tasks:**

- [x] Add the manifest contract type matching `version.json`.
- [x] Implement `IUpdateService` / `UpdateService` with check, download, and install.
- [x] Add the `FileProvider`, `file_paths.xml`, and `REQUEST_INSTALL_PACKAGES` to the manifest.
- [x] Guard install behind `#if ANDROID`.
- [ ] Register the service in `MauiProgram.cs`.
- [ ] Test the unhappy paths deliberately, including airplane mode. _Needs a device._

### PBI 9.4: Surface updates in Settings

**User story:** As the user, I want to see which build I am on and whether a newer one exists, so
updating is a deliberate act rather than something that surprises me.

**Acceptance criteria:**

- `SettingsPage` — currently a static stub — shows the installed version name and build number.
- A **Check for updates** action reports one of: up to date, update available with its version and
  notes, or a readable error.
- When an update is available, a second action downloads and installs it, showing progress.
- Updates are **never** downloaded automatically. The user taps, every time.
- The screen is usable while a download is in progress and survives backgrounding the app.

**Tasks:**

- [x] Add `SettingsViewModel` with check and update commands plus progress state.
- [x] Build the Settings UI over the existing stub page.
- [x] Register the view model and page in `MauiProgram.cs`.
- [ ] Verify on device against a real published release. _Needs the first release._

## Future Milestones

### Guild

Social features — parties, shared progress, accountability. Explicitly deferred: no other users
exist, and the app should be proven on a single user first. Note that `GuildPage` is currently a
tab in the shell, which overstates its readiness; consider hiding the tab until it does something.

### Speed as a fourth stat

Deferred from v1. Revisit at a lower bodyweight, where impact testing carries less risk. Likely
tests: a 30-second bike sprint measured by distance or average watts, or a 30-second sit-to-stand.
Adding a fourth `StatKind` is cheap now and annoying later, so if it is going to happen, doing it
before there is a year of history is preferable.

### Accurate in-workout heart rate

A BLE chest strap such as a Polar H10 speaks the standard Bluetooth heart-rate profile and can be
read directly from MAUI over BLE with no vendor SDK and no cloud. Optical wrist sensors are the
weak link during movement. This is a self-contained project, largely independent of Health Connect.

### Maintenance and recomposition

What the app does once the goal weight is reached. Out of scope for v1, but it will arrive before
the app is finished being useful.

### Nutrition database

If manual entry proves too tedious and MyFitnessPal does not write to Health Connect: Open Food
Facts for barcoded products, USDA FoodData Central for whole foods. Explicitly a last resort.

---

## Sprint Plan

A PBI is checked only when every task under it is done or explicitly deferred. Partly-delivered
PBIs stay unchecked with the remaining work called out, so the trailing 10% does not disappear.

### Sprint 1: cleanup and foundations

- [x] PBI 1.1: Write the v1 backlog
- [ ] PBI 1.2: Record decisions that outlive the backlog — _remaining: XML docs on the scoring helper, blocked until PBI 5.1 creates it_
- [x] PBI 2.1: Collapse duplicated and stale documentation
- [x] PBI 2.2: Clean up seed data
- [x] PBI 2.3: Untangle the constants
- [x] PBI 2.4: Fix naming and comment drift in QuestService
- [x] PBI 2.5: Stand up a test project

### Sprint 2: delivery, weight and check-in

The delivery PBIs are independent of every app feature and pay off across all remaining sprints —
do them first, so everything built afterwards reaches the phone without a cable. PBI 9.1 in
particular is time-sensitive: the one-time uninstall it requires destroys the local database, so it
must happen before there is any weight history worth keeping.

- [ ] PBI 9.1: Settle app identity and release signing — _code complete; remaining: keystore + GitHub secrets (yours), then the one-time uninstall_
- [ ] PBI 9.2: Build and publish a release APK on merge to main — _code complete; remaining: first end-to-end run, blocked on the secrets_
- [ ] PBI 9.3: Fetch and install updates from inside the app — _code complete; remaining: on-device verification of the unhappy paths_
- [ ] PBI 9.4: Surface updates in Settings — _code complete; remaining: on-device verification against a real release_
- [ ] PBI 3.1: Log a daily weigh-in
- [ ] PBI 3.2: Show the trend, not the number
- [ ] PBI 3.3: Chart progress against the goal
- [ ] PBI 3.4: Log the rest of the daily check-in

### Sprint 3: the progression loop

- [ ] PBI 4.1: Make levels advance
- [ ] PBI 4.2: Make leveling up feel like something
- [ ] PBI 4.3: Finish the quest XP path
- [ ] PBI 2.6: Fix or remove the equipment sprite path

### Sprint 4: baseline testing

- [ ] PBI 5.1: Replace the scoring math
- [ ] PBI 5.2: Define and store the frozen test protocols
- [ ] PBI 5.3: Run a test session
- [ ] PBI 5.4: Open a test window every four levels
- [ ] PBI 5.5: Establish a trustworthy baseline
- [ ] PBI 5.6: Show stat history

### Sprint 5: nutrition and device sync

- [ ] PBI 6.1: Manual protein and calorie entry
- [ ] PBI 7.1: Choose a wearable
- [ ] PBI 7.2: Read health data from Health Connect
- [ ] PBI 7.3: Surface resting heart rate as a trend
- [ ] PBI 6.2: Read nutrition totals from Health Connect

### Sprint 6: session logging

- [ ] PBI 8.1: Log a training session
- [ ] PBI 8.2: Feed sessions into quest completion

## Ongoing

- Deploy with `./deploy-android.sh` and verify on device, not from MSBuild output — the deploy
  is silent in the build log even on success. See `AGENTS.md`.
- Keep `README.md` and `AGENTS.md` in agreement whenever either changes.
- Add a unit test whenever pure logic is added, particularly to scoring and date math.
- Do not modify a frozen test protocol. Version a new one instead.
