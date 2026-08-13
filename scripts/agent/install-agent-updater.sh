#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="${1:-$SCRIPT_DIR/../../artifacts/agent-updater/linux-x64}"
UPDATER_INSTALL_DIR="${2:-/opt/stys-agent-updater}"
AGENT_INSTALL_DIR="${3:-/opt/stys-agent}"
SHARED_DATA_DIR="${4:-/var/lib/stys-agent}"
UPDATER_PRIVATE_DATA_DIR="${5:-/var/lib/stys-agent-updater}"
LOG_DIR="${6:-/var/log/stys-agent-updater}"
LOCAL_UI_PORT="${7:-5180}"
RELEASE_PUBLIC_KEY_PATH="${8:-/etc/stys-agent/trust/release-public-key.pem}"
SERVICE_NAME="stys-agent-updater"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

if ! [[ "$LOCAL_UI_PORT" =~ ^[0-9]+$ ]] || (( LOCAL_UI_PORT < 1 || LOCAL_UI_PORT > 65535 )); then
    echo "LOCAL_UI_PORT must be an integer between 1 and 65535." >&2
    exit 1
fi

mkdir -p "$UPDATER_INSTALL_DIR" "$AGENT_INSTALL_DIR" "$SHARED_DATA_DIR" "$UPDATER_PRIVATE_DATA_DIR" "$LOG_DIR"
if [[ ! -f "$RELEASE_PUBLIC_KEY_PATH" ]]; then
    echo "Release public key not found: $RELEASE_PUBLIC_KEY_PATH" >&2
    exit 1
fi

TRUST_DIR="$(dirname "$RELEASE_PUBLIC_KEY_PATH")"
mkdir -p "$TRUST_DIR"

cp -a "$PUBLISH_DIR"/. "$UPDATER_INSTALL_DIR"/
chown -R root:root "$UPDATER_INSTALL_DIR"
chmod -R u=rwX,go=rX "$UPDATER_INSTALL_DIR"
chown -R root:root "$UPDATER_PRIVATE_DATA_DIR" "$LOG_DIR"
chown -R stys-agent:stys-agent "$SHARED_DATA_DIR"
chmod -R u+rwX,go-rwx "$SHARED_DATA_DIR" "$LOG_DIR"
chmod -R u+rwX,go-rwx "$UPDATER_PRIVATE_DATA_DIR"
chown -R root:root "$TRUST_DIR"
chmod 0755 "$TRUST_DIR"
chmod 0644 "$RELEASE_PUBLIC_KEY_PATH"

cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=STYS Agent Updater
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=root
Group=root
WorkingDirectory=$UPDATER_INSTALL_DIR
ExecStart=$UPDATER_INSTALL_DIR/STYS.Agent.Updater
Restart=on-failure
RestartSec=5
Environment=STYS_AGENT_UPDATER_INSTALL_DIR=$UPDATER_INSTALL_DIR
Environment=STYS_AGENT_INSTALL_DIR=$AGENT_INSTALL_DIR
Environment=STYS_AGENT_SHARED_DATA_DIR=$SHARED_DATA_DIR
Environment=STYS_AGENT_DATA_DIR=$SHARED_DATA_DIR
Environment=STYS_AGENT_UPDATER_PRIVATE_DATA_DIR=$UPDATER_PRIVATE_DATA_DIR
Environment=STYS_AGENT_LOG_DIR=$LOG_DIR
Environment=STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH=$RELEASE_PUBLIC_KEY_PATH
Environment=STYS_AGENT_LOCAL_UI_PORT=$LOCAL_UI_PORT
Environment=ASPNETCORE_URLS=http://127.0.0.1:$LOCAL_UI_PORT
UMask=0077
NoNewPrivileges=true

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable "$SERVICE_NAME"
systemctl restart "$SERVICE_NAME"
