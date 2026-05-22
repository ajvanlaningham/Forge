# AGENTS.md — Forge

Guidance for AI agents working on this repo. Read this before building or deploying.

## What Forge is
A **.NET MAUI** fitness/RPG app, **Android-first** (`net9.0-android`). MVVM, local **SQLite** (`sqlite-net-pcl`), DI configured in `MauiProgram.cs`. Single project: `Forge/Forge/Forge.csproj` (solution `Forge/Forge.sln`).

iOS/maccatalyst target frameworks are conditionally enabled **only when building on macOS** (see the `Condition="$([MSBuild]::IsOSPlatform('osx'))"` line in `Forge.csproj`) — this is deliberate so the project builds on the Linux build host without an Apple toolchain. Don't "fix" it by removing the condition.

## Build host
The build host (asgard, Linux) already has: the `maui-android` dotnet workload, the Android SDK (`ANDROID_HOME=~/android-sdk`), and `adb` on PATH. No Visual Studio, no GUI.

## Build & test
```bash
dotnet restore Forge/Forge/Forge.csproj
dotnet build   Forge/Forge/Forge.csproj -f net9.0-android
```
There is **no test project** yet. If you add testable logic, add a test project rather than asserting "tests pass."

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

> ⚠️ The `README.md` "Deployment" section ("build from Visual Studio, hit Run", references .NET 8) is **stale/wrong** for this headless-Linux setup. Ignore it. This file is the source of truth for build/deploy.

## Conventions
- **MVVM**: Views bind to ViewModels; ViewModels depend only on service interfaces (`Services/Interfaces`).
- **DI**: register services in `MauiProgram.cs`.
- **Persistence**: models that persist have a corresponding `Row` class for SQLite; seeding is idempotent.
- Layout: `Constants/ Converters/ Data/ Models/ Services/ ViewModels/ Views/ Controls/ Resources/ Platforms/`.
