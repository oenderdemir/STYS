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
  ./scripts/push-images.sh [--host HOST] [--user USER] [--key PATH] [--remote-dir DIR] [--compose-file PATH] [--tag TAG]
EOF
}

if docker compose version >/dev/null 2>&1; then
    compose() {
        docker compose "$@"
    }
elif command -v docker-compose >/dev/null 2>&1; then
    compose() {
        docker-compose "$@"
    }
else
    echo "Ne docker compose plugin'i ne de docker-compose binary'si bulundu." >&2
    exit 1
fi

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
PROJECT_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_ROOT"

if [ ! -f "$COMPOSE_FILE_PATH" ]; then
    echo "Compose dosyasi bulunamadi: $COMPOSE_FILE_PATH" >&2
    exit 1
fi

ARTIFACT_DIR="$PROJECT_ROOT/artifacts/deploy/$TAG"
mkdir -p "$ARTIFACT_DIR"

BACKEND_TAR="$ARTIFACT_DIR/backend.tar"
FRONTEND_TAR="$ARTIFACT_DIR/frontend.tar"

STYS_IMAGE_TAG="$TAG" compose build backend frontend
docker save -o "$BACKEND_TAR" "stys/backend:$TAG"
docker save -o "$FRONTEND_TAR" "stys/frontend:$TAG"

REMOTE_TARGET="$VPS_USER@$VPS_HOST"
ssh -i "$SSH_KEY_PATH" "$REMOTE_TARGET" "mkdir -p '$REMOTE_DIR/images'"
scp -i "$SSH_KEY_PATH" "$COMPOSE_FILE_PATH" "$REMOTE_TARGET:$REMOTE_DIR/docker-compose.yml"
scp -i "$SSH_KEY_PATH" "$BACKEND_TAR" "$FRONTEND_TAR" "$REMOTE_TARGET:$REMOTE_DIR/images/"

printf '\nKopyalama tamamlandi:\n'
printf ' - %s:%s/docker-compose.yml\n' "$REMOTE_TARGET" "$REMOTE_DIR"
printf ' - %s:%s/images/backend.tar\n' "$REMOTE_TARGET" "$REMOTE_DIR"
printf ' - %s:%s/images/frontend.tar\n' "$REMOTE_TARGET" "$REMOTE_DIR"
