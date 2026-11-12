#!/usr/bin/env bash
#
# Builds the nidavellir API (Forge.Server) and deploys it to the nidavellir LXC (Decision 24).
#
#   ./deploy-server.sh            # publish, copy, restart, health-check
#   ./deploy-server.sh --setup    # first time: also create the service user, unit, and
#                                 # tailnet-only HTTPS (tailscale serve)
#
# Public access (needed once a guild member's phone isn't on this tailnet) is a deliberate,
# human step, not part of this script:
#   ssh nidavellir tailscale funnel --bg 5080
#
# Needs `ssh nidavellir` to work (key in ~/.ssh/config). The server keeps its data in
# /var/lib/forge-server/nidavellir.db; this script never touches it.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOST="${NIDAVELLIR_HOST:-nidavellir}"
OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT

echo ">> test"
dotnet test "$SCRIPT_DIR/Forge/Forge.Tests/Forge.Tests.csproj" -v:q -nologo --filter "FullyQualifiedName~Guild|FullyQualifiedName~Backup" | tail -1

echo ">> publish"
dotnet publish "$SCRIPT_DIR/Forge/Forge.Server/Forge.Server.csproj" -c Release -o "$OUT/app" -v:q -nologo
tar -C "$OUT/app" -czf "$OUT/forge-server.tgz" .

if [[ "${1:-}" == "--setup" ]]; then
  echo ">> first-time setup on $HOST"
  ssh "$HOST" 'bash -s' <<'EOF'
set -e
id forge >/dev/null 2>&1 || useradd --system --home /var/lib/forge-server --shell /usr/sbin/nologin forge
install -d -o forge -g forge -m 700 /var/lib/forge-server
install -d -m 755 /opt/forge-server
cat > /etc/systemd/system/forge-server.service <<'UNIT'
[Unit]
Description=Forge guild + encrypted backup API (nidavellir)
After=network-online.target
Wants=network-online.target

[Service]
User=forge
Group=forge
WorkingDirectory=/opt/forge-server
ExecStart=/usr/bin/dotnet /opt/forge-server/Forge.Server.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=Forge__DataDir=/var/lib/forge-server
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1
Restart=always
RestartSec=5
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
PrivateTmp=true
ReadWritePaths=/var/lib/forge-server

[Install]
WantedBy=multi-user.target
UNIT
systemctl daemon-reload
systemctl enable forge-server >/dev/null
EOF
fi

echo ">> deploy to $HOST"
scp -q "$OUT/forge-server.tgz" "$HOST:/tmp/forge-server.tgz"
ssh "$HOST" 'set -e; rm -rf /opt/forge-server.new; mkdir /opt/forge-server.new; tar -xzf /tmp/forge-server.tgz -C /opt/forge-server.new;
  rm -rf /opt/forge-server.old; [ -d /opt/forge-server ] && mv /opt/forge-server /opt/forge-server.old; mv /opt/forge-server.new /opt/forge-server;
  rm /tmp/forge-server.tgz; systemctl restart forge-server; sleep 3; systemctl is-active forge-server; curl -fsS http://127.0.0.1:5080/health; echo'

if [[ "${1:-}" == "--setup" ]]; then
  echo ">> tailnet-only HTTPS (443 -> 127.0.0.1:5080)"
  ssh "$HOST" 'tailscale serve --bg 5080 >/dev/null && tailscale serve status'
fi
echo ">> done"
