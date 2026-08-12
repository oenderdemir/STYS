#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="${1:-$SCRIPT_DIR/../../artifacts/agent/linux-x64}"
INSTALL_DIR="${2:-/opt/stys-agent}"
DATA_DIR="${3:-/var/lib/stys-agent}"
LOG_DIR="${4:-/var/log/stys-agent}"
SERVICE_NAME="stys-agent"
SERVICE_USER="stys-agent"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

mkdir -p "$INSTALL_DIR" "$DATA_DIR" "$LOG_DIR"

if ! id -u "$SERVICE_USER" >/dev/null 2>&1; then
    useradd --system --home-dir "$DATA_DIR" --shell /usr/sbin/nologin "$SERVICE_USER"
fi

cp -a "$PUBLISH_DIR"/. "$INSTALL_DIR"/
chown -R root:root "$INSTALL_DIR"
chmod -R u=rwX,go=rX "$INSTALL_DIR"
chown -R "$SERVICE_USER:$SERVICE_USER" "$DATA_DIR" "$LOG_DIR"
chmod -R u+rwX,go-rwx "$DATA_DIR" "$LOG_DIR"

cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=STYS Agent
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$SERVICE_USER
Group=$SERVICE_USER
WorkingDirectory=$INSTALL_DIR
ExecStart=$INSTALL_DIR/STYS.Agent
Restart=on-failure
RestartSec=5
Environment=STYS_AGENT_DATA_DIR=$DATA_DIR
Environment=STYS_AGENT_LOG_DIR=$LOG_DIR
Environment=STYS_AGENT_LOCAL_UI_PORT=5180
Environment=ASPNETCORE_URLS=http://127.0.0.1:$LocalUiPort
UMask=0077
NoNewPrivileges=true

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable "$SERVICE_NAME"
systemctl restart "$SERVICE_NAME"
