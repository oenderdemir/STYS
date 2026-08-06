#!/bin/sh
set -eu

PROFILE="${1:-fast}"
TEST_PROJECT="tests/STYS.Tests/STYS.Tests.csproj"

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
PROJECT_ROOT="$(dirname -- "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

usage() {
    cat <<'EOF'
Usage:
  ./scripts/test-ebelge.sh [fast|integration|nightly|release]

Profiller (bkz. docs/e-belge-test-stratejisi.md):
  fast        Unit + Contract (SQL/sidecar baslatmaz, PR geri bildirimi icin)
  integration Unit + Contract + SqlIntegration + CryptoIntegration
  nightly     Unit + Contract + SqlIntegration + SidecarIntegration + CryptoIntegration + WorkerEndToEnd
  release     Domain=EBelge altindaki TUM testler (ReleaseGate dahil)
EOF
}

case "$PROFILE" in
    fast)
        SEVIYELER="Unit,Contract"
        ;;
    integration)
        SEVIYELER="Unit,Contract,SqlIntegration,CryptoIntegration"
        ;;
    nightly)
        SEVIYELER="Unit,Contract,SqlIntegration,SidecarIntegration,CryptoIntegration,WorkerEndToEnd"
        ;;
    release)
        SEVIYELER="*"
        ;;
    -h|--help)
        usage
        exit 0
        ;;
    *)
        echo "HATA: bilinmeyen profil: $PROFILE" >&2
        usage
        exit 1
        ;;
esac

if [ "$SEVIYELER" = "*" ]; then
    FILTRE="Domain=EBelge"
else
    FILTRE=""
    OLD_IFS="$IFS"
    IFS=","
    for seviye in $SEVIYELER; do
        if [ -z "$FILTRE" ]; then
            FILTRE="(Domain=EBelge&TestLevel=$seviye)"
        else
            FILTRE="$FILTRE|(Domain=EBelge&TestLevel=$seviye)"
        fi
    done
    IFS="$OLD_IFS"
fi

echo ""
echo "=== e-Belge test profili: $PROFILE ==="
echo "TestLevel kapsami: $SEVIYELER"
echo "Filtre: $FILTRE"
echo ""

if [ "$PROFILE" = "nightly" ] || [ "$PROFILE" = "release" ] || [ "$PROFILE" = "integration" ]; then
    if [ -z "${STYS_INTEGRATION_TEST_CONNECTION_STRING:-}" ]; then
        echo "UYARI: STYS_INTEGRATION_TEST_CONNECTION_STRING tanimli degil - SQL Server/worker" >&2
        echo "gerektiren testler mevcut acik skip politikasiyla ATLANACAK (yesile zorlanmaz)." >&2
        echo "" >&2
    fi
fi

TRX_DOSYA_ADI="ebelge-$PROFILE-$(date +%Y%m%d-%H%M%S).trx"

dotnet test "$TEST_PROJECT" --filter "$FILTRE" --nologo --logger "trx;LogFileName=$TRX_DOSYA_ADI"
EXIT_CODE=$?

echo ""
echo "TRX sonucu: tests/STYS.Tests/TestResults/$TRX_DOSYA_ADI"
if [ "$EXIT_CODE" -ne 0 ]; then
    echo "HATA: '$PROFILE' profili basarisiz oldu (exit code $EXIT_CODE)." >&2
else
    echo "'$PROFILE' profili basariyla tamamlandi."
fi

exit "$EXIT_CODE"
