# AGENTS.md — Forge

Guidance for AI agents working on this repo. Read this before building or deploying.

## What Forge is
A **.NET MAUI** fitness/RPG app, **Android-first** (`net9.0-android`). MVVM, local **SQLite** (`sqlite-net-pcl`), DI configured in `MauiProgram.cs`. Solution `Forge/Forge.sln` holds three projects:

| Project | Target | Purpose |
|---|---|---|
| `Forge/Forge/Forge.csproj` | `net9.0-android` | the app |
| `Forge/Forge.Core/Forge.Core.csproj` | `net9.0` | pure game logic + constants, **no MAUI types** |
| `Forge/Forge.Tests/Forge.Tests.csproj` | `net9.0` | xUnit tests over `Forge.Core` |

`Forge.Core` is plain `net9.0` on purpose: it makes progression and date math testable on this
headless Linux host with no emulator. Do not add a MAUI dependency to it — that breaks the tests.

iOS/maccatalyst target frameworks are conditionally enabled **only when building on macOS** (see the `Condition="$([MSBuild]::IsOSPlatform('osx'))"` line in `Forge.csproj`) — this is deliberate so the project builds on the Linux build host without an Apple toolchain. Don't "fix" it by removing the condition.

## Build host
The build host (asgard, Linux) already has: the `maui-android` dotnet workload, the Android SDK (`ANDROID_HOME=~/android-sdk`), and `adb` on PATH. No Visual Studio, no GUI.

## Build & test
```bash
dotnet restore Forge/Forge/Forge.csproj
dotnet build   Forge/Forge/Forge.csproj -f net9.0-android
dotnet test    Forge/Forge.Tests/Forge.Tests.csproj
```
Tests cover **pure logic only** — progression math, scoring, date/week helpers. They need no
emulator and no Android SDK, so run them. Put new pure logic in `Forge.Core` and test it;
"tests pass" is only a meaningful claim if `dotnet test` was actually run.

Note the host SDK is .NET 10 (10.0.111) building a `net9.0-android` target via the
`maui-android` workload. That is expected and works.

## Deploy — use the script, not the README
**Deploy with `./deploy-android.sh` only.** The deploy targets a physical Pixel over Tailscale via wireless `adb`.

```bash
./deploy-android.sh <connect-port>                       # normal case
./deploy-android.sh --pair <pair-port> <code> <connect-port>   # first run / trust lost
```

The `<connect-port>` (and the pair port + 6-digit code) are read off the phone:
**Settings → Developer options → Wireless debugging** (and "Pair device with pairing code").

Non-obvious facts the script encodes — **do not relearn these the hard way:**
- Android **rotates the wireless-debugging port every time it's toggled** → it can't be hardcoded; it's a script argument.
- The phone displays its **Wi-Fi LAN IP**, but you connect over the **tailnet IP** (`100.125.64.95`) with that port.
- `adb connect` often fails the first 1–2 tries right after pairing (TLS handshake) → the script retries.
- The deploy is **silent in MSBuild output** — `dotnet build -t:Run` prints only "Build succeeded" even on success. Verify the deploy via the device, not the log:
  ```bash
  adb -s 100.125.64.95:<port> shell pidof com.companyname.forge          # running?
  adb -s 100.125.64.95:<port> shell dumpsys package com.companyname.forge | grep lastUpdateTime
  ```

> `README.md` used to carry a stale Visual Studio / .NET 8 deployment section. It was corrected
> and now points here. This file remains the source of truth for build and deploy; if the two ever
> disagree again, this one wins — and fix the README.

## Conventions
- **MVVM**: Views bind to ViewModels; ViewModels depend only on service interfaces (`Services/Interfaces`).
- **DI**: register services in `MauiProgram.cs`.
- **Persistence**: models that persist have a corresponding `Row` class for SQLite; seeding is idempotent.
- Layout (app project): `Converters/ Data/ Models/ Services/ ViewModels/ Views/ Controls/ Resources/ Platforms/`.
- **Constants live in `Forge.Core`**, not the app project. Game rules are constants; anything the
  user can change is a setting in `Forge/Forge/Services/UserSettings.cs` backed by `Preferences`.
  Never make a personal target (goal weight, weekly goal) a compile-time constant.
- Exercise library seeding is gated on `GameConstants.Exercises.LibraryVersion` **alone**. Editing
  or adding a JSON file without bumping that version is a silent no-op on an existing install.
  `IExerciseLibraryImporter.ForceReseedAsync()` is the dev escape hatch.

## Delivery — CI builds, in-app updates

`deploy-android.sh` is the dev loop. **Delivery** is separate: merging to `main` runs
`.github/workflows/android-release.yml`, which builds a release-signed APK and publishes it as a
GitHub Release with a `version.json` manifest. The app's **Settings → Check for updates** button
reads that release straight from the public GitHub API and hands the APK to the OS installer.

This works only because the repo is **public** — no credentials are involved anywhere in the app.
If Forge is ever made private, do **not** fix it by embedding a token in the client.

### Two facts that will bite

1. **A release-signed APK cannot install over a debug build.** The signatures differ and Android
   refuses. Switching to release builds needs a one-time uninstall, and **that deletes the local
   SQLite database**. Do it before there is data worth keeping.
2. **`ApplicationId` is permanent.** It is `io.github.ajvanlaningham.forge`. Changing it is not an
   update — it is a different app, and the previous install's database becomes unreachable.

### One-time setup

Generate a release keystore (keep the passwords; you will need them as secrets):

```bash
keytool -genkeypair -v \
  -keystore forge-release.keystore \
  -alias forge \
  -keyalg RSA -keysize 2048 -validity 10000
```

**Back this file up somewhere off-machine.** If it is lost, no future build can install over an
existing one — the only recovery is uninstall-and-lose-the-data, forever.

Then add four repository secrets:

| Secret | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | `base64 -w0 forge-release.keystore` |
| `ANDROID_KEYSTORE_PASSWORD` | store password from `keytool` |
| `ANDROID_KEY_ALIAS` | `forge` |
| `ANDROID_KEY_PASSWORD` | key password from `keytool` |

The keystore itself must never be committed.

### Version numbering

`versionCode` comes from `github.run_number`, so it increases by one per CI run and the app's
update check is a single integer comparison. Do not set `ApplicationVersion` by hand in the csproj
for release builds — CI overrides it.

## Product rules

These are product decisions, not implementation details. Breaking one silently damages data or
the user's experience in ways a build will not catch. Full rationale in [`TODO.md`](TODO.md).

- **Weight never earns XP.** XP is granted for *logging* a weigh-in, never for the value logged.
  Daily weight is noisy; penalising a reading penalises something the user does not control.
- **Weight is not the hero element.** The 7-day average is what gets displayed; the raw daily
  number lives behind a tap. The goal is to feel better, and weight can become an obsession.
- **Baseline test protocols are frozen.** Once a protocol is set, its movements, reps, order, and
  loads never change. "Improving" a test destroys comparability with every prior result. Version a
  new protocol instead of editing one. See [`docs/baseline-tests.md`](docs/baseline-tests.md).
- **Manual entry ships before every integration.** Health Connect, wearables, and nutrition sync
  are all additive. The app must stay fully usable with every permission denied.
- **Three independent progress systems.** Levels measure consistency, stat scores measure
  capability, weight is its own track. They answer different questions — do not collapse them.
