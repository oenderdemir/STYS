#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
PROJECT_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_ROOT"

WITH_LOGIN="false"
REGISTRY_SERVER=""
USERNAME=""
PASSWORD=""
SKIP_DATABASE_BOOTSTRAP="false"

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
        --with-login)
            WITH_LOGIN="true"
            ;;
        --registry-server)
            REGISTRY_SERVER="${2:-}"
            shift
            ;;
        --username)
            USERNAME="${2:-}"
            shift
            ;;
        --password)
            PASSWORD="${2:-}"
            shift
            ;;
        --skip-database-bootstrap)
            SKIP_DATABASE_BOOTSTRAP="true"
            ;;
        *)
            echo "Bilinmeyen arguman: $1" >&2
            exit 1
            ;;
    esac
    shift
done

if [ "$WITH_LOGIN" = "true" ]; then
    if [ -z "$REGISTRY_SERVER" ]; then
        echo "with-login kullaniliyorsa --registry-server vermen gerekiyor." >&2
        exit 1
    fi

    if [ -z "$USERNAME" ] || [ -z "$PASSWORD" ]; then
        echo "with-login kullaniliyorsa --username ve --password vermen gerekiyor." >&2
        exit 1
    fi

    echo "Registry login yapiliyor: $REGISTRY_SERVER"
    printf '%s' "$PASSWORD" | docker login "$REGISTRY_SERVER" --username "$USERNAME" --password-stdin
fi

if [ "$SKIP_DATABASE_BOOTSTRAP" != "true" ]; then
    MSSQL_STATE=""
    MSSQL_CONTAINER_ID="$(compose ps -q mssql 2>/dev/null || true)"

    if [ -n "$MSSQL_CONTAINER_ID" ]; then
        MSSQL_STATE="$(docker inspect -f '{{.State.Status}}' "$MSSQL_CONTAINER_ID" 2>/dev/null || true)"
    fi

    if [ -z "$MSSQL_STATE" ]; then
        echo "mssql container bulunamadi. Ilk kurulum icin mssql ayaga kaldiriliyor..."
        compose up -d mssql
    elif [ "$MSSQL_STATE" != "running" ]; then
        echo "mssql container calismiyor. Tekrar baslatiliyor..."
        compose up -d mssql
    else
        echo "mssql zaten calisiyor. Dokunulmuyor."
    fi
fi

echo "Remote image'lar cekiliyor: backend, frontend"
compose pull backend frontend

echo "Container'lar yeniden olusturuluyor: backend, frontend"
compose up -d --no-deps backend frontend

echo
echo "Deploy tamamlandi."
echo "Kontrol icin:"
echo " - compose ps"
echo " - compose logs --tail 200 backend"
echo " - compose logs --tail 200 frontend"
