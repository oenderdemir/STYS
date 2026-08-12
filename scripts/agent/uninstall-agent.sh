#!/usr/bin/env bash
set -euo pipefail

PURGE="${1:-false}"
SERVICE_NAME="stys-agent"
INSTALL_DIR="/opt/stys-agent"
DATA_DIR="/var/lib/stys-agent"
LOG_DIR="/var/log/stys-agent"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

systemctl stop "$SERVICE_NAME" >/dev/null 2>&1 || true
systemctl disable "$SERVICE_NAME" >/dev/null 2>&1 || true
rm -f "$SERVICE_FILE"
systemctl daemon-reload

rm -rf "$INSTALL_DIR"

if [[ "$PURGE" == "--purge" ]]; then
    rm -rf "$DATA_DIR" "$LOG_DIR"
fi
