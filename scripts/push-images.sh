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

PYTHON_BIN=""
if command -v python3 >/dev/null 2>&1; then
    PYTHON_BIN="python3"
elif command -v python >/dev/null 2>&1; then
    PYTHON_BIN="python"
else
    echo "JSON ayristirma icin python3 veya python bulunamadi." >&2
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

if [ ! -f ".env" ]; then
    echo ".env dosyasi bulunamadi: $PROJECT_ROOT/.env" >&2
    exit 1
fi

export STYS_IMAGE_TAG="$TAG"

read_compose_image() {
    service_name="$1"
    compose config --format json | "$PYTHON_BIN" -c 'import json,sys; data=json.load(sys.stdin); print(data["services"][sys.argv[1]]["image"])' "$service_name"
}

split_image_reference() {
    image_ref="$1"
    repo="${image_ref%:*}"
    tag="${image_ref##*:}"

    case "$image_ref" in
        *:*)
            printf '%s\n%s\n' "$repo" "$tag"
            ;;
        *)
            printf '%s\n%s\n' "$image_ref" "latest"
            ;;
    esac
}

BACKEND_IMAGE_REF="$(read_compose_image backend)"
FRONTEND_IMAGE_REF="$(read_compose_image frontend)"
BACKEND_IMAGE_INFO="$(split_image_reference "$BACKEND_IMAGE_REF")"
FRONTEND_IMAGE_INFO="$(split_image_reference "$FRONTEND_IMAGE_REF")"
BACKEND_IMAGE_REPO="$(printf '%s\n' "$BACKEND_IMAGE_INFO" | sed -n '1p')"
BACKEND_IMAGE_TAG="$(printf '%s\n' "$BACKEND_IMAGE_INFO" | sed -n '2p')"
FRONTEND_IMAGE_REPO="$(printf '%s\n' "$FRONTEND_IMAGE_INFO" | sed -n '1p')"
FRONTEND_IMAGE_TAG="$(printf '%s\n' "$FRONTEND_IMAGE_INFO" | sed -n '2p')"

if [ "$BACKEND_IMAGE_TAG" != "$FRONTEND_IMAGE_TAG" ]; then
    echo "Backend ve frontend image tag'leri farkli: $BACKEND_IMAGE_TAG / $FRONTEND_IMAGE_TAG" >&2
    exit 1
fi

TAG="$BACKEND_IMAGE_TAG"
ARTIFACT_DIR="$PROJECT_ROOT/artifacts/deploy/$TAG"
mkdir -p "$ARTIFACT_DIR"

BACKEND_TAR="$ARTIFACT_DIR/backend.tar"
FRONTEND_TAR="$ARTIFACT_DIR/frontend.tar"
IMAGE_ENV_FILE="$ARTIFACT_DIR/stys-image.env"

STYS_IMAGE_TAG="$TAG" compose build backend frontend
docker save -o "$BACKEND_TAR" "$BACKEND_IMAGE_REF"
docker save -o "$FRONTEND_TAR" "$FRONTEND_IMAGE_REF"

cat > "$IMAGE_ENV_FILE" <<EOF
export STYS_BACKEND_IMAGE=$BACKEND_IMAGE_REPO
export STYS_FRONTEND_IMAGE=$FRONTEND_IMAGE_REPO
export STYS_IMAGE_TAG=$TAG
EOF

REMOTE_TARGET="$VPS_USER@$VPS_HOST"
ssh -i "$SSH_KEY_PATH" "$REMOTE_TARGET" "mkdir -p '$REMOTE_DIR/images'"
scp -i "$SSH_KEY_PATH" "$COMPOSE_FILE_PATH" "$REMOTE_TARGET:$REMOTE_DIR/docker-compose.yml"
scp -i "$SSH_KEY_PATH" "$BACKEND_TAR" "$FRONTEND_TAR" "$IMAGE_ENV_FILE" "$REMOTE_TARGET:$REMOTE_DIR/images/"
ssh -i "$SSH_KEY_PATH" "$REMOTE_TARGET" "
set -eu
cd '$REMOTE_DIR'
if [ -f .env ]; then
    tmp=\$(mktemp)
    awk -v tag='$TAG' 'BEGIN { found = 0 } /^STYS_IMAGE_TAG=/ { print \"STYS_IMAGE_TAG=\" tag; found = 1; next } { print } END { if (!found) print \"STYS_IMAGE_TAG=\" tag }' .env > \"\$tmp\"
    mv \"\$tmp\" .env
else
    printf 'STYS_IMAGE_TAG=%s\\n' '$TAG' > .env
fi
"

printf '\nKopyalama tamamlandi:\n'
printf ' - %s:%s/docker-compose.yml\n' "$REMOTE_TARGET" "$REMOTE_DIR"
printf ' - %s:%s/.env (STYS_IMAGE_TAG guncellendi)\n' "$REMOTE_TARGET" "$REMOTE_DIR"
printf ' - %s:%s/images/backend.tar\n' "$REMOTE_TARGET" "$REMOTE_DIR"
printf ' - %s:%s/images/frontend.tar\n' "$REMOTE_TARGET" "$REMOTE_DIR"
printf ' - %s:%s/images/stys-image.env\n' "$REMOTE_TARGET" "$REMOTE_DIR"
