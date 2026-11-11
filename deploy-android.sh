#!/usr/bin/env bash
#
# Wireless build + deploy of Forge to the Pixel 9 Pro XL over Tailscale.
#
# NOTE: Android "Wireless debugging" rotates its port every time it's toggled,
# so the connect port can't be hardcoded — read it off the phone:
#   Settings > Developer options > Wireless debugging  ->  "IP address & Port"
# (We connect over the *tailnet* IP, not the Wi-Fi IP it displays.)
#
# Usage:
#   ./deploy-android.sh <connect-port>
#       e.g.  ./deploy-android.sh 46143
#
#   First time / after trust is lost (adb connect fails on an open port),
#   pair first using "Pair device with pairing code" (a DIFFERENT port + code):
#   ./deploy-android.sh --pair <pair-port> <code> <connect-port>
#       e.g.  ./deploy-android.sh --pair 39085 138794 46143
#
set -euo pipefail

PHONE_IP="${PHONE_IP:-100.125.64.95}"   # pixel-9-pro-xl tailnet IP
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ="$SCRIPT_DIR/Forge/Forge/Forge.csproj"

if [[ "${1:-}" == "--pair" ]]; then
  [[ $# -eq 4 ]] || { echo "usage: $0 --pair <pair-port> <code> <connect-port>" >&2; exit 2; }
  PAIR_PORT="$2"; PAIR_CODE="$3"; PORT="$4"
  echo ">> pairing with $PHONE_IP:$PAIR_PORT"
  adb pair "$PHONE_IP:$PAIR_PORT" "$PAIR_CODE"
else
  PORT="${1:-${ADB_PORT:-}}"
  [[ -n "$PORT" ]] || { echo "usage: $0 <connect-port>   (see header)" >&2; exit 2; }
fi

# Connect — the TLS handshake can need a couple tries right after pairing.
for i in 1 2 3 4 5; do
  out="$(adb connect "$PHONE_IP:$PORT" || true)"
  echo "   $out"
  [[ "$out" == *"connected"* ]] && break
  sleep 2
done

adb devices | grep -q "$PHONE_IP:$PORT[[:space:]]*device" \
  || { echo "!! device $PHONE_IP:$PORT not connected — check the port on the phone" >&2; exit 1; }

# The phone runs release-signed CI builds (installed via Settings > Check for updates).
# A debug-signed build cannot install over those — Android rejects the signature
# mismatch, and the only way round it is an uninstall that deletes the database. So when
# the release keystore is present, sign local builds with it too, and stamp them with the
# latest published CI build number: an equal versionCode reinstalls cleanly, and the next
# CI build (+1) still arrives as an update. The password is read into the environment and
# passed by name (env:), so it never appears on a command line or in the build log.
SIGNING_DIR="${FORGE_SIGNING_DIR:-$HOME/.forge-signing}"
SIGN_ARGS=()
if [[ -f "$SIGNING_DIR/forge-release.keystore" && -f "$SIGNING_DIR/credentials.txt" ]]; then
  FORGE_SIGN_PASS="$(awk '$1 == "ANDROID_KEYSTORE_PASSWORD" { print $2 }' "$SIGNING_DIR/credentials.txt")"
  export FORGE_SIGN_PASS
  [[ -n "$FORGE_SIGN_PASS" ]] || { echo "!! could not read the keystore password from $SIGNING_DIR/credentials.txt" >&2; exit 1; }
  VERSION_CODE="${FORGE_VERSION_CODE:-}"
  [[ -n "$VERSION_CODE" ]] || VERSION_CODE="$(gh release list --repo ajvanlaningham/Forge --limit 1 --json tagName -q '.[0].tagName' 2>/dev/null | sed 's/^android-build-//')"
  [[ "$VERSION_CODE" =~ ^[0-9]+$ ]] || { echo "!! could not read the latest CI build number (gh release list)" >&2; exit 1; }
  echo ">> release-signing with $SIGNING_DIR, versionCode $VERSION_CODE (latest CI build)"
  SIGN_ARGS=(
    -p:AndroidKeyStore=true
    -p:AndroidSigningKeyStore="$SIGNING_DIR/forge-release.keystore"
    -p:AndroidSigningKeyAlias=forge
    -p:AndroidSigningStorePass=env:FORGE_SIGN_PASS
    -p:AndroidSigningKeyPass=env:FORGE_SIGN_PASS
    -p:ApplicationVersion="$VERSION_CODE"
  )
else
  echo ">> no release keystore at $SIGNING_DIR — debug-signed build (won't install over a CI build)"
fi

# AndroidPreserveUserData: fast deployment falls back to a full reinstall whenever the build
# configuration changes — including a new versionCode, i.e. after every CI release. Without this
# that reinstall is uninstall + install, and silently deletes the SQLite database on the phone.
echo ">> build + install + launch (net9.0-android) on $PHONE_IP:$PORT"
dotnet build "$CSPROJ" -f net9.0-android -t:Run -p:AdbTarget="-s $PHONE_IP:$PORT" \
  -p:AndroidPreserveUserData=true "${SIGN_ARGS[@]}"
echo ">> done. App should be running on the device."
