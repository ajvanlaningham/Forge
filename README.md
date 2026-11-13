# Forge

Forge is a **.NET MAUI app** that gamifies training to build strength, endurance, mobility, and
resolve. It is both a fitness tracker and an RPG: you follow four-week quests drawn as a path on a
fantasy map, finishing training days levels your hero, and finishing a quest earns a forging that
raises a piece of your gear one metal.

It is a personal, Android-first app for two people. Training, health and weight data live in a
local SQLite database on the phone. The one server, nidavellir, holds guilds and end-to-end
encrypted backups, never health data (see `AGENTS.md`).

---

## Status

See [`TODO.md`](TODO.md) for the decisions behind all of this and what's next.

### Working today

Four tabs (Decision 27 in `TODO.md`):

- **Map**: the active quest as a winding path on a parchment map, one region per week (Kindling,
  Tempering, Quenching, Honing). Today's stone has one START; days ahead are inked sketches,
  painted as they're done; a chest per week opens on a perfect week; the anvil ends the quest. The
  ＋ sheet logs cardio, a weigh-in, a **side quest** (training done your own way: HEMA, a gym
  class, yoga), an open session, or changes the quest. The quest board is a page of scrolls.
- **Hero**: level, XP, streak, STR/DEX/CON, the five-slot gear rack (Head, Body, Weapon, Feet,
  Trinket), and your real equipment and weights.
- **Chronicle**: weigh-ins as a moving-average trend, and every finished session.
- **Guild**: a small group (official at three members) with a daily campfire bonus, synced
  through nidavellir.
- **Settings**: a cog on each tab: units, rest days (guided or freestyle), equipment, backups
  (daily local, encrypted offsite), Strava, Health Connect, in-app updates.

**Workouts** are timed circuits (Decision 28): a warm-up, a strength circuit, an accessory circuit
and a finisher, 30–40 minutes a day, with reps shown only as a suggested pace. A started workout
runs itself: each set ends on the clock, logs itself, rests and moves on, with a draining ring
timer and the day's foe health bar. After it, one optional Easy / Right / Hard tap tunes the next
suggested weights and reps. On a test day the stat test is the last block.

### Not working yet

- **Five-slot gear art**: forging works, but only three of the five slots have drawn pieces.
- **iOS**: deferred until Miche joins (December).
- **Condition**: injuries and limitations are designed, not built.

---

## Project Structure

The solution is `Forge/Forge.sln` and contains three projects:

| Project | Target | Purpose |
|---|---|---|
| `Forge/Forge` | `net10.0-android` | The MAUI app |
| `Forge/Forge.Core` | `net10.0` | Pure game logic and constants — no MAUI types |
| `Forge/Forge.Tests` | `net10.0` | xUnit tests over `Forge.Core` |

`Forge.Core` exists so progression and date math can be unit-tested on the Linux build host
without an emulator. Keep it free of MAUI dependencies.

Within the app project:

- **Controls/Cards/** — reusable cards (StatCard, WeeklyConditioningCard, WeightTrendCard).
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

Beside the solution:

- **`Forge/Forge.Server`**: nidavellir, the guild and encrypted-backup server (`./deploy-server.sh`).
- **`content/gen_programs.py`**: generates every quest's programs from short recipes and checks
  each day fits 30–40 minutes. Edit the recipe, not the generated JSON.
- **`art/`**: the pixel art as text-grid sprites (`gen_*.py`), rendered to PNG by
  `art/render.py --install`. The map's landscape is `art/gen_mapscape.py`.

---

## Getting Started

### Requirements

- .NET SDK with the `maui-android` workload installed.
  The app targets `net10.0-android` (.NET 10 LTS, MAUI 10).
- Android SDK, with `ANDROID_HOME` set and `adb` on `PATH`.
- A physical Android device or emulator.

No Visual Studio is required. The project builds and deploys headlessly on Linux.

### Build

```bash
dotnet restore Forge/Forge/Forge.csproj
dotnet build   Forge/Forge/Forge.csproj -f net10.0-android
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
- **Enums** — defined in `Forge.Core/TrainingEnums.cs` and reused across services and UI.
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
what is done, the current push, and the remaining work.

Next: the five-slot gear art, a view of each lift's climb from its starting weight, guilds and
iOS for a second user, and later nutrition from Health Connect and resting heart rate from a
wearable.

## License

MIT (TBD)
