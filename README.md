
# Forge

Forge is a **.NET MAUI app** that gamifies workouts to build strength, endurance, mobility, and resolve.  
It’s both a fitness tracker and an RPG-style progression system: every set logged, every quest completed, and every milestone achieved earns XP and levels your character.

---
##  Features

- **RPG Progression**
	-  Earn XP for completing daily quests, weekly goals, and training sessions.
	-  Level up Strength, Dexterity (Mobility), and Constitution (Endurance).

- **Daily Quests**
	-  Automatically generated Strength & Mobility quests.
	-  Weekly Conditioning goal (cardio minutes) with XP reward.
	-  Recovery-only days to balance training load.

- **Exercise Library**
	-  Seeded from JSON and stored in SQLite.
	-  Filter by category, body zone, modality, and owned equipment.

- **Inventory**
	-  Track which equipment you own (dumbbells, kettlebells, bands, etc.).
	-  Library and quest generation adapts to your gear.

- **Check-ins**
	-  Log weight, macros, sleep, and steps.

- **UI**
	-  Dashboard with mascot, stats, and quick navigation.
	-  Reusable card controls for stats, quests, and weekly goals.

- **Persistence**
	-  SQLite local storage with repository pattern.
	-  Idempotent exercise seeding and stat initialization.

  

---

  

##  Project Structure


The project is organized into clear layers:

- **Constants/** — Game tuning values, UI config, math helpers.
- **Converters/** — Value converters for UI binding.
- **Data/** — SQLite abstraction and repository implementations.
- **Models/** — Domain and persistence models (Exercises, Stats, Quests, Inventory).
- **Services/**
	-  **Interfaces/** — Contracts for service logic.
	-  **Implementations/** — Business logic (Quests, Stats, Conditioning, Inventory, Exercise Library).
- **ViewModels/**
	-  Page VMs (Home, Stats, Quests, ExerciseLibrary, MyGear).
	-  Control VMs (StatCard, WeeklyConditioningCard).
- **Views/**
	-  Pages: Home, Stats, Train, Quests, CheckIn, Settings.
	-  SubPages: Exercise Library, My Gear.
- **Controls/**
	-  Reusable UI elements (StatCard, QuestCard).
- **Resources/**
	-  Strings (localized via `AppResources.resx`).
	-  Styles (Colors, Styles, Icons).
	-  Fonts (MedievalSharp, OpenSans, PressStart2P, VT323, FontAwesome).
	-  Images (mascots, sprites).
	- Raw data (seed JSON for exercise library).
- **Platforms/** — Platform-specific entry points and manifests (Android, iOS).

  
---

  

##  Getting Started

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/)
- Visual Studio 2022 or Rider with MAUI workload installed.
- Android SDK/emulator or physical device.

### Build & Run

```bash

# Restore dependencies

dotnet restore

# Build the app

dotnet build 

# Run on Android

dotnet build -t:Run -f net8.0-android

```

### Deployment
- Connect your Android device (or start an emulator).
- From Visual Studio, select **Forge (Android)** as the startup project and hit **Run**.


---

##  Conventions

- **MVVM pattern**: All Views bind to ViewModels; ViewModels depend only on service interfaces.
- **Dependency Injection**: Configured in `MauiProgram.cs`.
- **Enums**: Defined in `Models/TrainingEnums.cs` and reused across services and UI.
- **Persistence**: All models that persist implement corresponding `Row` classes (`ExerciseRow`, `StatRow`, etc.) for SQLite.

##  Motivation

Forge is about forging discipline and resilience through training:  
- Build functional strength and endurance.  
- Train with dumbbells, kettlebells, and bike.  

---

  

## Future State

Planned and upcoming features include:

- **Weight / Check-ins**  
	- Track bodyweight, macros, sleep, and daily habits with richer analytics. 
- **Training Session Logging**  
	-  Record sets, reps, and RPEs. Logged work will automatically apply toward daily/weekly quests.  
- **Baseline Stat Tests**  
	-  Every **4 levels**, unlock a structured test to measure current baseline Strength, Mobility, and Conditioning.
	- Results will recalibrate stat scores and progression scaling.
##  License

MIT (TBD)
