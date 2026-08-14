#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_ROOT="${1:-$SCRIPT_DIR}"
AGENT_INSTALL_DIR="${2:-/opt/stys-agent}"
UPDATER_INSTALL_DIR="${3:-/opt/stys-agent-updater}"
SHARED_DATA_DIR="${4:-/var/lib/stys-agent}"
UPDATER_PRIVATE_DATA_DIR="${5:-/var/lib/stys-agent-updater}"
LOG_DIR="${6:-/var/log/stys-agent}"
RELEASE_PUBLIC_KEY_PATH="${7:-/etc/stys-agent/trust/release-public-key.pem}"
LOCAL_UI_PORT="${8:-5180}"

log() { printf '[%s] %s\n' "$1" "$2"; }
fail() { printf '[FAILED] %s\n' "$1" >&2; exit 1; }

if ! [[ "$LOCAL_UI_PORT" =~ ^[0-9]+$ ]] || (( LOCAL_UI_PORT < 1 || LOCAL_UI_PORT > 65535 )); then
    fail "Local UI port 1..65535 aralığında olmalı."
fi

PACKAGE_ROOT="$(cd "$PACKAGE_ROOT" && pwd)"
BOOTSTRAP_PATH="$PACKAGE_ROOT/config/bootstrap.json"
PACKAGE_TRUST_KEY="$PACKAGE_ROOT/trust/release-public-key.pem"
AGENT_PUBLISH_DIR="$PACKAGE_ROOT/agent"
UPDATER_PUBLISH_DIR="$PACKAGE_ROOT/updater"
INSTALL_AGENT_SCRIPT="$PACKAGE_ROOT/scripts/agent/install-agent.sh"
INSTALL_UPDATER_SCRIPT="$PACKAGE_ROOT/scripts/agent/install-agent-updater.sh"

for required in "$BOOTSTRAP_PATH" "$PACKAGE_TRUST_KEY" "$AGENT_PUBLISH_DIR" "$UPDATER_PUBLISH_DIR" "$INSTALL_AGENT_SCRIPT" "$INSTALL_UPDATER_SCRIPT"; do
    [[ -e "$required" ]] || fail "Gerekli paket bileşeni bulunamadı: $required"
done

mkdir -p "$SHARED_DATA_DIR" "$UPDATER_PRIVATE_DATA_DIR" "$LOG_DIR" "$(dirname "$RELEASE_PUBLIC_KEY_PATH")"

cp "$BOOTSTRAP_PATH" "$SHARED_DATA_DIR/bootstrap.json"
cp "$PACKAGE_TRUST_KEY" "$RELEASE_PUBLIC_KEY_PATH"

chmod 0644 "$RELEASE_PUBLIC_KEY_PATH"

log OK "Trust anchor"
log OK "Bootstrap configuration"

bash "$INSTALL_AGENT_SCRIPT" "$AGENT_PUBLISH_DIR" "$AGENT_INSTALL_DIR" "$SHARED_DATA_DIR" "$UPDATER_PRIVATE_DATA_DIR" "$LOG_DIR" "$LOCAL_UI_PORT" "$RELEASE_PUBLIC_KEY_PATH"
log OK "Agent"

bash "$INSTALL_UPDATER_SCRIPT" "$UPDATER_PUBLISH_DIR" "$UPDATER_INSTALL_DIR" "$AGENT_INSTALL_DIR" "$SHARED_DATA_DIR" "$UPDATER_PRIVATE_DATA_DIR" "$LOG_DIR" "$LOCAL_UI_PORT" "$RELEASE_PUBLIC_KEY_PATH"
log OK "Updater"

bootstrap_stys_base_url="$(BOOTSTRAP_PATH="$BOOTSTRAP_PATH" python3 - <<'PY'
import json, os
path = os.environ["BOOTSTRAP_PATH"]
with open(path, encoding="utf-8") as f:
    data = json.load(f)
print(data.get("StysBaseUrl", "https://localhost:7160"))
PY
)"

bootstrap_display_name="$(BOOTSTRAP_PATH="$BOOTSTRAP_PATH" python3 - <<'PY'
import json, os
path = os.environ["BOOTSTRAP_PATH"]
with open(path, encoding="utf-8") as f:
    data = json.load(f)
print(data.get("AgentDisplayName", "STYS Agent"))
PY
)"

bootstrap_http_timeout="$(BOOTSTRAP_PATH="$BOOTSTRAP_PATH" python3 - <<'PY'
import json, os
path = os.environ["BOOTSTRAP_PATH"]
with open(path, encoding="utf-8") as f:
    data = json.load(f)
print(int(data.get("HttpTimeoutSeconds", 30)))
PY
)"

bootstrap_local_ui_port="$(BOOTSTRAP_PATH="$BOOTSTRAP_PATH" python3 - <<'PY'
import json, os
path = os.environ["BOOTSTRAP_PATH"]
with open(path, encoding="utf-8") as f:
    data = json.load(f)
print(int(data.get("LocalUiPort", 5180)))
PY
)"

wait_for_ui() {
    local deadline now uri
    deadline=$((SECONDS + 60))
    uri="http://127.0.0.1:${bootstrap_local_ui_port}/api/bootstrap/dashboard"
    while (( SECONDS < deadline )); do
        if curl -fsS --max-time 5 "$uri" >/dev/null 2>&1; then
            return 0
        fi
        sleep 2
    done
    fail "Yerel Agent UI zamanında hazır olmadı: $uri"
}

wait_for_ui
log OK "Local UI"

read -r -s -p 'Enrollment Code: ' enrollment_code < /dev/tty
printf '\n'

enroll_payload="$(BOOTSTRAP_STYS_BASE_URL="$bootstrap_stys_base_url" \
BOOTSTRAP_AGENT_DISPLAY_NAME="$bootstrap_display_name" \
BOOTSTRAP_HTTP_TIMEOUT="$bootstrap_http_timeout" \
BOOTSTRAP_LOCAL_UI_PORT="$bootstrap_local_ui_port" \
ENROLLMENT_CODE="$enrollment_code" \
python3 - <<'PY'
import json, os
payload = {
    "StysBaseUrl": os.environ["BOOTSTRAP_STYS_BASE_URL"],
    "AgentDisplayName": os.environ["BOOTSTRAP_AGENT_DISPLAY_NAME"],
    "EnrollmentCode": os.environ["ENROLLMENT_CODE"],
    "HttpTimeoutSeconds": int(os.environ["BOOTSTRAP_HTTP_TIMEOUT"]),
    "LocalUiPort": int(os.environ["BOOTSTRAP_LOCAL_UI_PORT"]),
    "Capabilities": []
}
print(json.dumps(payload))
PY
)"

curl -fsS -X POST "http://127.0.0.1:${bootstrap_local_ui_port}/api/bootstrap/enroll" \
    -H 'Content-Type: application/json' \
    -d "$enroll_payload" >/dev/null

unset enrollment_code enroll_payload

log OK "Enrollment"
printf 'STYS Agent unified installation completed.\n'
printf 'AgentInstallDir: %s\n' "$AGENT_INSTALL_DIR"
printf 'UpdaterInstallDir: %s\n' "$UPDATER_INSTALL_DIR"
printf 'SharedDataDir: %s\n' "$SHARED_DATA_DIR"
printf 'LogDir: %s\n' "$LOG_DIR"
printf 'TrustAnchor: %s\n' "$RELEASE_PUBLIC_KEY_PATH"
printf 'LocalUiPort: %s\n' "$bootstrap_local_ui_port"
