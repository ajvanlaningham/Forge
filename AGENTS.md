# AGENTS.md — Forge

Guidance for AI agents working on this repo. Read this before building or deploying.

## What Forge is
A **.NET MAUI** fitness/RPG app, **Android-first** (`net10.0-android`). MVVM, local **SQLite** (`sqlite-net-pcl`), DI configured in `MauiProgram.cs`. Solution `Forge/Forge.sln` holds three projects:

| Project | Target | Purpose |
|---|---|---|
| `Forge/Forge/Forge.csproj` | `net10.0-android` | the app |
| `Forge/Forge.Core/Forge.Core.csproj` | `net10.0` | pure game logic + constants, **no MAUI types** |
| `Forge/Forge.Tests/Forge.Tests.csproj` | `net10.0` | xUnit tests over `Forge.Core` |

`Forge.Core` is plain `net10.0` on purpose: it makes progression and date math testable on this
headless Linux host with no emulator. Do not add a MAUI dependency to it — that breaks the tests.

iOS/maccatalyst target frameworks are conditionally enabled **only when building on macOS** (see the `Condition="$([MSBuild]::IsOSPlatform('osx'))"` line in `Forge.csproj`) — this is deliberate so the project builds on the Linux build host without an Apple toolchain. Don't "fix" it by removing the condition.

## Build host
The build host (asgard, Linux) already has: the `maui-android` dotnet workload, the Android SDK (`ANDROID_HOME=~/android-sdk`), and `adb` on PATH. No Visual Studio, no GUI.

## Build & test
```bash
dotnet restore Forge/Forge/Forge.csproj
dotnet build   Forge/Forge/Forge.csproj -f net10.0-android
dotnet test    Forge/Forge.Tests/Forge.Tests.csproj
```
Tests cover **pure logic only** — progression math, scoring, date/week helpers. They need no
emulator and no Android SDK, so run them. Put new pure logic in `Forge.Core` and test it;
"tests pass" is only a meaningful claim if `dotnet test` was actually run.

Forge is on **.NET 10 (LTS, supported to Nov 2028)** with MAUI 10, moved from .NET 9 on
2026-11-12 when .NET 9 left support. Stay on LTS releases; the next is .NET 12 (Nov 2027).
Two dependencies are held back on purpose:
- **Health Connect binding `1.1.0-alpha07`.** Newer bindings need AndroidX Lifecycle 2.11,
  while MAUI 10 (through 10.0.130) pins 2.9; forcing it risks runtime breakage. Forge only uses
  the stable read calls. Revisit when MAUI moves its AndroidX baseline.
- **Android minimum 8.0 (API 26)**, which Health Connect requires.

### Building in the throwaway `/tmp/coder/repo` container

The remote coder container is not asgard. It ships the .NET 10 SDK at
`/usr/local/dotnet` **without** the `maui-android` workload, Android SDK, JDK, or `adb`,
and `/usr/local/dotnet` is root-owned so `dotnet workload install` fails with
"Inadequate permissions." The tests run there as-is (Forge.Tests targets net10.0).

If a container-side Android build is actually needed (e.g. to satisfy a spec that says
"`dotnet build ... -f net10.0-android` succeeds"), the workaround is:
`cp -r /usr/local/dotnet /tmp/user-dotnet`, install `maui-android` into that copy,
download Microsoft OpenJDK 21 + Android cmdline-tools into `/tmp`, accept the licenses,
`sdkmanager "platforms;android-35" "build-tools;35.0.0" "platform-tools"`, then build with
`DOTNET_ROOT=/tmp/user-dotnet`, `JAVA_HOME`, and `ANDROID_HOME` pointing at the copies.
Roughly a 1 GB download; don't do it unless the task requires validating the Android target.

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
  adb -s 100.125.64.95:<port> shell pidof io.github.ajvanlaningham.forge          # running?
  adb -s 100.125.64.95:<port> shell dumpsys package io.github.ajvanlaningham.forge | grep -E 'versionCode|lastUpdateTime'
  ```
- **Local deploys are release-signed when the keystore is present.** The phone runs CI release
  builds, and a debug-signed build cannot install over one (signature mismatch → uninstall →
  data loss). So when `~/.forge-signing/` exists, the script signs with the release key (password
  passed via `env:`, never on a command line) and stamps `versionCode` with the latest CI build
  number from `gh release list`. An equal versionCode reinstalls in place; the next CI build is +1
  and still arrives as an in-app update. Without the keystore it falls back to debug signing.
- **The script refuses to install a stale APK.** Incremental builds don't treat signing
  properties or `ApplicationVersion` as inputs, so after a plain `dotnet build` (debug key,
  versionCode 1) a signed deploy would reuse the debug APK — signature mismatch, forced uninstall,
  data gone. The script builds with `-t:SignAndroidPackage` first, checks the APK's certificate
  (`apksigner`) and versionCode (`aapt`), cleans and rebuilds if either is wrong, and after
  installing fails loudly if `firstInstallTime` moved (i.e. it was a reinstall, not an update).
- **`AndroidPreserveUserData=true` is load-bearing.** Fast deployment silently falls back to a
  full reinstall whenever the build configuration changes — including a new versionCode, which
  happens after every CI release. Without the flag that reinstall is uninstall + install and wipes
  the phone's database (observed: `firstInstallTime` reset mid-session). The script passes it;
  never deploy with a bare `dotnet build -t:Run`. Check with
  `dumpsys package … | grep firstInstallTime` — it must not move on a deploy.
- **After changing Android resources, clean before deploying.** Editing
  `Platforms/Android/Resources/**` or the `MauiIcon`/splash assets can leave an incremental build
  whose generated resource IDs disagree with the packaged ones: the app then crashes on launch
  with `IllegalArgumentException: No view found for id … for fragment
  NavigationRootManager_ElementBasedFragment` (seen 2026-11-11). `rm -rf Forge/Forge/obj
  Forge/Forge/bin` and deploy again. CI builds clean, so it's a local-only hazard; it's why
  local changes are checked on the device before they're pushed.
- **Inspecting the phone's database:** local builds are debuggable, so
  `adb shell run-as io.github.ajvanlaningham.forge cat files/forge.db3 > forge.db3` pulls it
  (journal mode is `delete`, so the single file is complete); `sqlite3` is in platform-tools.
- Local builds are still **Debug**, so Android may show a "not 16 KB compatible" dialog on
  launch (debuggable apps only; it lists the debug runtime's own libraries). Tap
  *Don't Show Again*. CI release builds don't show it.
- **Pairing vs connect port.** If only a pairing port + code is available, pair, then find the
  connect port by probing the phone's 30000–49999 TCP range over the tailnet (one port answers).
  Faster than a round trip to read it off the phone.
- **Screenshots for QA:** the phone can report more than one display (e.g. when casting), and
  `screencap` without `-d` then writes a warning instead of a PNG. List displays with
  `adb shell dumpsys SurfaceFlinger --display-id` and capture the built-in one (`KM4-…`):
  `adb -s <dev> exec-out screencap -d <display-id> -p > shot.png`.

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
- **Weights are stored in pounds always.** The lb/kg toggle in `UserSettings.WeightUnit` only
  affects display — convert at the view/ViewModel boundary via `WeightMath.ToDisplay` /
  `WeightMath.ToStoredPounds`. Never persist the currently-selected display unit.
- Exercise library seeding is gated on `GameConstants.Exercises.LibraryVersion` **alone**. Editing
  or adding a JSON file without bumping that version is a silent no-op on an existing install.
  `IExerciseLibraryImporter.ForceReseedAsync()` is the dev escape hatch.
- **Legibility first** (Miche's review, 2026-11-13). Fonts: `Display` (Jersey 15) for titles,
  numbers and buttons; `Body`/`BodyBold` (Nunito) for sentences; `FantasyH` only for quest titles
  and banners. VT323 and Press Start 2P are retired. Nothing below 15sp; body text 17. Use the
  named styles in `Styles.xaml` (`Title`, `Heading`, `BodyText`, `Caption`, `BigNumber`,
  `PrimaryButton`, `SecondaryButton`). Each screen has one primary action; details go behind a
  tap. Text colours must pass WCAG AA: `ContrastTests` reads `Colors.xaml`, so add any new
  text/background pair there.
- **Programs are generated from recipes** (Decision 28). `content/gen_programs.py` builds every
  timed quest (Foundations, Iron Will, Second Wind; dumbbell and kettlebell, 3 and 4 days) from
  exercise pools by role and a per-quest day recipe, times every day with the app's formula and
  refuses anything outside 30–40 minutes. Change the recipe and re-run it; don't hand-edit the
  generated JSON. A new version is a new file listed in `GameConstants.Programs.Files` (old
  versions stay listed for history). `TimedQuests_FitEveryDayIn30To40Minutes` re-checks the
  shipped files. The Fourfold Path (Miche's plan) is a recipe too, naming its exact exercises
  instead of roles; keep their choices when tuning it.
- **Charts** are MAUI `GraphicsView` drawables: `Sparkline` (weight card, Hero stats) and
  `LineChart` (exercise history) in `ViewModels/Controls`. Microcharts and SkiaSharp were
  removed in the .NET 10 move (Microcharts was unmaintained and pinned SkiaSharp 2.x, which
  wasn't 16 KB-page aligned). Bind a new drawable instance to redraw.
- **DI on the card VMs**: reusable card view models (see `WeightTrendCardViewModel`) are
  registered `AddTransient` and injected into the page view models that host them. Each
  page holds its own instance so per-page UI state (expansion, animation) does not leak
  across tabs. Refresh the card in the page view model's Initialize/Refresh, not in the
  card VM's constructor.

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

**Use the same password for the store and the key.** `keytool` defaults to PKCS12 since JDK 9, and
PKCS12 has no separate key password — given a different `-keypass` it prints *"Different store and
key passwords not supported for PKCS12 KeyStores"* and silently ignores it, leaving the key
protected by the store password. If `ANDROID_KEY_PASSWORD` is then set to that imaginary second
password, the build fails at signing with a bare `error MSB6006: "java" exited with code 2` —
MSBuild swallows apksigner's real message, `Failed to obtain key with alias "forge". Wrong
password?`.

Do not redirect `keytool`'s stderr when generating. That warning is the only hint you get.

**Backup status: local only, deliberately.** The keystore lives at `~/.forge-signing/` on asgard
and nowhere else. This is an accepted risk, not an oversight.

Note that **GitHub Actions secrets are write-only** — `ANDROID_KEYSTORE_BASE64` cannot be read back
out, so the secrets are not a second copy. If asgard's disk dies, the signing key is gone.

The consequence is bounded, which is why local-only is a reasonable call for a sideloaded personal
app: you would uninstall, install a fresh build signed with a new key, and lose whatever was in the
app's database. Annoying after months of check-ins, harmless before then. It does not end the
project. Revisit this once there is weight history worth keeping — the whole directory is under
7 KB and fits in a password manager note.

The keystore for this project already exists at `~/.forge-signing/` (mode 700), mirroring the
`~/.huginn-signing/` layout:

| File | Contents |
|---|---|
| `forge-release.keystore` | the key itself, alias `forge`, valid to 2054 |
| `keystore.base64` | the same file base64-encoded, for `ANDROID_KEYSTORE_BASE64` |
| `credentials.txt` | alias and both passwords, ready to paste into the secrets |

Certificate SHA-256: `FF:47:DB:C7:89:20:01:AA:A0:91:E1:6A:1A:D5:07:2E:ED:8F:D3:22:7F:8D:33:F6:3B:52:42:A4:12:01:73:9C`

The certificate's subject is deliberately `CN=Forge, O=Forge, C=US` with no personal details —
it is embedded in every APK, and those APKs are published on a public repo.

Then add four repository secrets:

| Secret | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | `base64 -w0 forge-release.keystore` |
| `ANDROID_KEYSTORE_PASSWORD` | store password from `keytool` |
| `ANDROID_KEY_ALIAS` | `forge` |
| `ANDROID_KEY_PASSWORD` | key password from `keytool` |

The keystore itself must never be committed.

### Nidavellir (guild + encrypted backup server)
`Forge/Forge.Server` is a small ASP.NET minimal API (SQLite) for guilds and end-to-end encrypted
backups (Decision 24). It runs on **nidavellir**, an unprivileged Debian 13 LXC (id 107) on the
Proxmox host yggdrasil, as the `forge-server` systemd unit (user `forge`, data in
`/var/lib/forge-server/nidavellir.db`, app in `/opt/forge-server`), listening on
`127.0.0.1:5080`. `ssh nidavellir` works directly from asgard (key-only); `nidavellir.home`
resolves via bifrost's Pi-hole. Deploy with `./deploy-server.sh` (tests, publish, copy,
restart, health check); `--setup` is the one-time unit + `tailscale serve` step. It does **not**
ship with the Android CI (server-only changes are path-ignored there).

Reachability: `https://nidavellir.tail625ec6.ts.net` (`GameConstants.Nidavellir.BaseUrl`) is
**tailnet-only** via `tailscale serve`. Making it public (needed once a guild member's phone isn't
on this tailnet) is `ssh nidavellir tailscale funnel --bg 5080`, a deliberate human step.

Contracts and rules live in `Forge.Core/Guild` (shared by app and server); the server is
covered by in-process integration tests (`GuildServerTests`). The server never stores health
data: member status is game state only, tokens are stored as SHA-256 hashes, and backups are
AES-256-GCM ciphertext keyed from a recovery key only the user holds (`BackupCrypto`).

### Strava credentials
Strava has no PKCE, so its sign-in needs the API app's client secret, and the APKs are public
downloads. So the secret is **never built in or committed**: each phone pastes it once (Settings →
Strava → Connect asks for it) and keeps it in SecureStorage, out of backups. Only the client ID,
which isn't secret, is in code (`StravaAuth.ClientId`). The API app (strava.com/settings/api) has
Authorization Callback Domain `localhost`, matching `StravaAuth.RedirectUri`
(`forgestrava://localhost/strava`). It starts in single-player mode (owner only); a second athlete
needs the upgrade to 10 athletes on that page.

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
