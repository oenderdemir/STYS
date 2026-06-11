#!/bin/sh
set -eu

VPS_HOST="185.229.12.39"
VPS_USER="root"
SSH_KEY_PATH="id_ed25519"
REMOTE_DIR="/root/stys"
TAG="latest"

usage() {
    cat <<'EOF'
Usage:
  ./scripts/deploy-remote.sh [--host HOST] [--user USER] [--key PATH] [--remote-dir DIR] [--tag TAG]

This script expects docker-compose.yml and image tar files to already exist on the VPS.
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

REMOTE_TARGET="$VPS_USER@$VPS_HOST"

ssh -i "$SSH_KEY_PATH" "$REMOTE_TARGET" "
cd '$REMOTE_DIR' &&
set -a &&
. ./images/stys-image.env &&
set +a &&
docker load -i images/backend.tar &&
docker load -i images/frontend.tar &&
docker compose up -d
"

printf '\nDeploy tamamlandi.\n'
printf 'Kontrol icin:\n'
printf ' - ssh -i %s %s \"cd %s && docker compose ps\"\n' "$SSH_KEY_PATH" "$REMOTE_TARGET" "$REMOTE_DIR"
printf ' - ssh -i %s %s \"cd %s && docker compose logs --tail 200 backend\"\n' "$SSH_KEY_PATH" "$REMOTE_TARGET" "$REMOTE_DIR"
printf ' - ssh -i %s %s \"cd %s && docker compose logs --tail 200 frontend\"\n' "$SSH_KEY_PATH" "$REMOTE_TARGET" "$REMOTE_DIR"
