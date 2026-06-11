#!/bin/sh
set -eu

VPS_HOST="185.229.12.39"
VPS_USER="root"
SSH_KEY_PATH="id_ed25519"
REMOTE_DIR="/root/stys"
COMPOSE_FILE_PATH="docker-compose.yml"
TAG="latest"

usage() {
    cat <<'EOF'
Usage:
  ./scripts/deploy-all.sh [--host HOST] [--user USER] [--key PATH] [--remote-dir DIR] [--compose-file PATH] [--tag TAG]

This runs push-images first, then deploy-remote.
EOF
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --host)
            VPS_HOST="${2:-}"
            shift
            ;;
        --user)
            VPS_USER="${2:-}"
            shift
            ;;
        --key)
            SSH_KEY_PATH="${2:-}"
            shift
            ;;
        --remote-dir)
            REMOTE_DIR="${2:-}"
            shift
            ;;
        --compose-file)
            COMPOSE_FILE_PATH="${2:-}"
            shift
            ;;
        --tag)
            TAG="${2:-}"
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Bilinmeyen arguman: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
    shift
done

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

"$SCRIPT_DIR/push-images.sh" \
    --host "$VPS_HOST" \
    --user "$VPS_USER" \
    --key "$SSH_KEY_PATH" \
    --remote-dir "$REMOTE_DIR" \
    --compose-file "$COMPOSE_FILE_PATH" \
    --tag "$TAG"

"$SCRIPT_DIR/deploy-remote.sh" \
    --host "$VPS_HOST" \
    --user "$VPS_USER" \
    --key "$SSH_KEY_PATH" \
    --remote-dir "$REMOTE_DIR" \
    --tag "$TAG"
