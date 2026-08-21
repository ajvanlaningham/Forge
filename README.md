# Forge

Forge is a **.NET MAUI app** that gamifies workouts to build strength, endurance, mobility, and resolve.
It is both a fitness tracker and an RPG-style progression system: quests completed and milestones hit
earn XP and level your character.

It is a personal, single-user, Android-first app. There are no accounts, no backend, and no sync —
all data lives in a local SQLite database on the device.

---

## Status

Forge is mid-build. This section is deliberately honest about what runs today, because the gap
between the two lists is where the work is. See [`TODO.md`](TODO.md) for the full backlog.

### Working today

- **Daily quests** — Strength and Mobility quests generated deterministically from the date, on a
  Mon–Sun body-zone cycle, with Wed/Sat/Sun drawing from a recovery-only exercise pool.
- **Quest completion and XP** — mark a quest done to earn XP; un-completing reclaims it.
- **Weekly conditioning** — cardio minutes tracked against a weekly goal, with a bonus on completion.
- **Exercise library** — ~71 exercises seeded from JSON into SQLite, filterable by category, body
  zone, and owned equipment.
- **Inventory** — track which equipment you own; the library filters to what you can actually do.
- **Persistence** — SQLite via a generic repository, idempotent seeding, stat initialisation.

### Not working yet

- **Levelling** — XP accrues but `UserStats.Level` never advances, and the XP bar pins at 100%
  once the first level is cleared. (PBI 4.1)
- **Stat scores** — STR/DEX/CON are seeded at 1 and nothing moves them. Baseline testing is
  designed but not built. (Epic 5)
- **Check-in** — weight, sleep, steps, and resting heart rate are a static placeholder page.
  This is the app's core purpose and the top build priority. (Epic 3)
- **Training session logging** — the Train tab is a placeholder with navigation buttons. (Epic 8)
- **Quest generation does not consider inventory.** Only the library browser filters by owned gear.
- **Guild** — a tab exists; nothing is behind it. Deliberately deferred.

---

## Project Structure

The solution is `Forge/Forge.sln` and contains three projects:

| Project | Target | Purpose |
|---|---|---|
| `Forge/Forge` | `net9.0-android` | The MAUI app |
| `Forge/Forge.Core` | `net9.0` | Pure game logic and constants — no MAUI types |
| `Forge/Forge.Tests` | `net9.0` | xUnit tests over `Forge.Core` |

`Forge.Core` exists so progression and date math can be unit-tested on the Linux build host
without an emulator. Keep it free of MAUI dependencies.

Within the app project:

- **Controls/** — reusable UI elements (StatCard, QuestCard, WeeklyConditioningCard).
- **Converters/** — value converters for UI binding.
- **Data/** — SQLite abstraction (`IAppDatabase`) and the generic repository.
- **Models/** — domain models and their SQLite `Row` counterparts.
- **Services/**
  - **Interfaces/** — service contracts; ViewModels depend on these and nothing else.
  - **Implementations/** — quests, stats, conditioning, inventory, exercise library.
  - `UserSettings.cs` — user-tunable targets, backed by `Preferences`.
- **ViewModels/** — page and control view models.
- **Views/** — pages and sub-pages.
- **Resources/** — strings (`AppResources.resx`), styles, fonts, images, and the seed JSON
  under `Resources/Raw`.
- **Platforms/** — platform entry points and manifests.

---

## Getting Started

### Requirements

- .NET SDK with the `maui-android` workload installed.
  The app targets `net9.0-android`; it currently builds on the .NET 10 SDK (10.0.111) with the
  `maui-android` workload, and needs the .NET 9 runtime present to run the tests.
- Android SDK, with `ANDROID_HOME` set and `adb` on `PATH`.
- A physical Android device or emulator.

No Visual Studio is required. The project builds and deploys headlessly on Linux.

### Build

```bash
dotnet restore Forge/Forge/Forge.csproj
dotnet build   Forge/Forge/Forge.csproj -f net9.0-android
```

### Test

```bash
dotnet test Forge/Forge.Tests/Forge.Tests.csproj
```

Tests cover pure logic only — progression math, scoring, and date handling. They need no
emulator and no Android SDK.

### Deploy

Deploy with the script, over wireless `adb`:

```bash
./deploy-android.sh <connect-port>                             # normal case
./deploy-android.sh --pair <pair-port> <code> <connect-port>   # first run / trust lost
```

The port is read off the phone under **Settings → Developer options → Wireless debugging**, and
it changes every time wireless debugging is toggled.

**[`AGENTS.md`](AGENTS.md) is the source of truth for build and deploy**, including why the
deploy looks silent in MSBuild output and how to verify it actually landed. Read it before
deploying.

---

## Conventions

- **MVVM** — Views bind to ViewModels; ViewModels depend only on service interfaces.
- **Dependency injection** — everything is registered in `MauiProgram.cs`.
- **Enums** — defined in `Models/TrainingEnums.cs` and reused across services and UI.
- **Persistence** — models that persist have a corresponding `Row` class for SQLite, with
  `ToDomain()` / `FromDomain()` mapping. Enums are stored as ints.
- **Constants vs settings** — game rules are constants in `Forge.Core`; anything the user can
  change belongs in `UserSettings`, never a compile-time constant.
- **Pure logic goes in `Forge.Core`** so it can be tested.

---

## Motivation

Forge is about forging discipline and resilience through training: building functional strength
and endurance, and feeling better. Weight loss is part of that, but deliberately not the whole
of it — and deliberately not the thing the app rewards you for. XP is earned for showing up and
logging, never for what the scale says.

## Roadmap

See [`TODO.md`](TODO.md) for the full backlog: target outcome, scope, settled design decisions,
epics and PBIs, and the sprint plan.

Headline items: daily weight tracking with a trend view, a working levelling loop, baseline stat
testing on a monthly cadence, nutrition read from Health Connect rather than re-implemented, and
resting heart rate from a wearable.

## License

MIT (TBD)
