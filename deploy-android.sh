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

echo ">> build + install + launch (net9.0-android) on $PHONE_IP:$PORT"
dotnet build "$CSPROJ" -f net9.0-android -t:Run -p:AdbTarget="-s $PHONE_IP:$PORT"
echo ">> done. App should be running on the device."
