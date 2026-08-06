#!/bin/sh
# Faz 2B.9.1 gorev md.7 - KASITLI OLARAK `set -e` KULLANILMAZ: `dotnet test` normal bir test
# basarisizliginda non-zero exit doner, `-e` altinda bu script'i SESSIZCE/erken sonlandirir ve
# guvenli ozet/TRX yolu/skip kontrolu HICBIR ZAMAN calismaz. Bunun yerine HER `dotnet test`
# cagrisi acik bir if/then ile kontrol edilir (bkz. run_dotnet_test). `set -u` (tanimsiz degisken
# kullanimini hata sayma) KORUNUR - bu, "-e"nin neden oldugu sessiz erken-cikis SORUNUYLA AYNI
# SINIFTA DEGILDIR.
set -u

PROFILE="fast"
VALIDATE_ONLY=0
LIST_ONLY=0

usage() {
    cat <<'EOF'
Usage:
  ./scripts/test-ebelge.sh [fast|integration|nightly|release] [--validate] [--list]

Profiller ve TestLevel/dependency kapsamlari scripts/ebelge-test-profiles.json'dan okunur (bkz.
docs/e-belge-test-stratejisi.md) - burada TEKRAR TANIMLANMAZ.

  --validate   Manifest + filtre + metadata sozlesmesini dogrular, dis/agir test CALISTIRMAZ.
  --list       Filtreyle eslesen testleri listeler, CALISTIRMAZ.
EOF
}

for arg in "$@"; do
    case "$arg" in
        --validate) VALIDATE_ONLY=1 ;;
        --list) LIST_ONLY=1 ;;
        -h|--help) usage; exit 0 ;;
        *) PROFILE="$arg" ;;
    esac
done

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
PROJECT_ROOT="$(dirname -- "$SCRIPT_DIR")"
if ! cd "$PROJECT_ROOT"; then
    echo "HATA: proje kok dizinine gecilemedi: $PROJECT_ROOT" >&2
    exit 1
fi

MANIFEST_FILE="$SCRIPT_DIR/ebelge-test-profiles.json"

if [ ! -f "$MANIFEST_FILE" ]; then
    echo "HATA: profil manifesti bulunamadi: $MANIFEST_FILE - test CALISTIRILMAYACAK." >&2
    exit 1
fi

# --- Minimal JSON okuyucu -----------------------------------------------------------------
# Genel amacli bir JSON parser DEGILDIR - yalniz BU depodaki, BİLİNEN, sabit-bicimli (2 boslukla
# girintili, her array elemani kendi satirinda) scripts/ebelge-test-profiles.json'u okumak icindir.
# PowerShell tarafi native `ConvertFrom-Json` kullanir; iki taraf ARASINDAKI esdegerlik bu turda
# dogrudan cikti karsilastirmasiyla dogrulanmistir (bkz. docs/e-belge-test-stratejisi.md).

json_extract_block() {
    # STDIN'siz, $MANIFEST_FILE icinde $1 arama dizesini ("\"fast\": {" gibi) bulup, esleşen
    # kapanis suslu parantezine kadar olan bloğu basar (ic ice derinlik SAYILARAK).
    key_pattern="$1"
    awk -v k="$key_pattern" '
        index($0, k) { infound=1 }
        infound {
            print
            n = gsub(/\{/, "{"); depth += n
            n = gsub(/\}/, "}"); depth -= n
            if (infound && depth == 0) { exit }
        }
    ' "$MANIFEST_FILE"
}

json_extract_array() {
    # STDIN = arama kapsami (blok metni veya tum dosya), $1 = array anahtari.
    key="$1"
    awk -v k="\"$key\":" '
        index($0, k) { infound=1 }
        infound {
            print
            if (index($0, "]")) { exit }
        }
    ' | grep -oE '"[^"]*"' | sed -e 's/^"//' -e 's/"$//' | grep -v -x "$key"
}

json_extract_string_scalar() {
    key="$1"
    grep -oE "\"$key\": *\"[^\"]*\"" | head -n1 | sed -E "s/\"$key\": *\"//; s/\"\$//"
}

json_extract_bool_scalar() {
    key="$1"
    grep -oE "\"$key\": *(true|false)" | head -n1 | sed -E 's/.*: *//'
}

# --- Kok seviye alanlar ------------------------------------------------------------------
KNOWN_TEST_LEVELS=$(json_extract_array "knownTestLevels" < "$MANIFEST_FILE")
KNOWN_DEPENDENCIES=$(json_extract_array "knownDependencies" < "$MANIFEST_FILE")
TEST_PROJECT=$(json_extract_string_scalar "testProject" < "$MANIFEST_FILE")

if [ -z "$KNOWN_TEST_LEVELS" ] || [ -z "$KNOWN_DEPENDENCIES" ] || [ -z "$TEST_PROJECT" ]; then
    echo "HATA: profil manifesti gecersiz/bozuk gorunuyor (knownTestLevels/knownDependencies/testProject eksik): $MANIFEST_FILE" >&2
    exit 1
fi

# Profil adlari - 4 boslukla girintili "\"ad\": {" satirlari (bu depronun KENDI, sabit
# bicimlendirilmis manifest dosyasina OZEL bir varsayimdir).
PROFILE_NAMES=$(awk '/^    "[A-Za-z]+": \{$/ { gsub(/^    "/, ""); gsub(/": \{$/, ""); print }' "$MANIFEST_FILE")

if ! printf '%s\n' "$PROFILE_NAMES" | grep -qx "$PROFILE"; then
    echo "HATA: bilinmeyen profil '$PROFILE'. Bilinen profiller: $(printf '%s' "$PROFILE_NAMES" | tr '\n' ' ')" >&2
    exit 1
fi

PROFILE_BLOCK=$(json_extract_block "\"$PROFILE\": {")
if [ -z "$PROFILE_BLOCK" ]; then
    echo "HATA: '$PROFILE' profil blogu manifestte bulunamadi/parse edilemedi." >&2
    exit 1
fi

TEST_LEVELS=$(printf '%s\n' "$PROFILE_BLOCK" | json_extract_array "testLevels")
REQUIRED_DEPS=$(printf '%s\n' "$PROFILE_BLOCK" | json_extract_array "requiredDependencies")
ALL_DOMAIN_TESTS=$(printf '%s\n' "$PROFILE_BLOCK" | json_extract_bool_scalar "allDomainTests")
REQUIRE_CRITICAL=$(printf '%s\n' "$PROFILE_BLOCK" | json_extract_bool_scalar "requireCriticalInvariants")
FAIL_ON_SKIPPED=$(printf '%s\n' "$PROFILE_BLOCK" | json_extract_bool_scalar "failOnSkippedTests")

for lvl in $TEST_LEVELS; do
    if ! printf '%s\n' "$KNOWN_TEST_LEVELS" | grep -qx "$lvl"; then
        echo "HATA: manifestte bilinmeyen TestLevel '$lvl' (profil: $PROFILE) - test CALISTIRILMAYACAK." >&2
        exit 1
    fi
done

for dep in $REQUIRED_DEPS; do
    if ! printf '%s\n' "$KNOWN_DEPENDENCIES" | grep -qx "$dep"; then
        echo "HATA: manifestte bilinmeyen Dependency '$dep' (profil: $PROFILE) - test CALISTIRILMAYACAK." >&2
        exit 1
    fi
done

if [ "$ALL_DOMAIN_TESTS" = "true" ]; then
    FILTER="Domain=EBelge"
else
    FILTER=""
    for lvl in $TEST_LEVELS; do
        if [ -z "$FILTER" ]; then
            FILTER="(Domain=EBelge&TestLevel=$lvl)"
        else
            FILTER="$FILTER|(Domain=EBelge&TestLevel=$lvl)"
        fi
    done
fi

TEST_PROJECT_DIR=$(dirname "$TEST_PROJECT")
TRX_DIR="$TEST_PROJECT_DIR/TestResults"

echo ""
echo "=== e-Belge test profili: $PROFILE ==="
if [ "$ALL_DOMAIN_TESTS" = "true" ]; then
    echo "TestLevel kapsami: TUMU (Domain=EBelge)"
else
    echo "TestLevel kapsami: $(printf '%s' "$TEST_LEVELS" | tr '\n' ' ')"
fi
echo "Filtre: $FILTER"
if [ -n "$REQUIRED_DEPS" ]; then
    echo "Gerekli dependency: $(printf '%s' "$REQUIRED_DEPS" | tr '\n' ' ')"
else
    echo "Gerekli dependency: (yok)"
fi
echo "Sifir-skip politikasi: $FAIL_ON_SKIPPED"
echo ""

# --- TRX Counters okuma ------------------------------------------------------------------
get_trx_counters() {
    trx="$1"
    if [ ! -f "$trx" ]; then
        return 1
    fi
    line=$(grep -o '<Counters[^/]*/>' "$trx" | head -n1)
    total=$(printf '%s' "$line" | grep -oE 'total="[0-9]+"' | grep -oE '[0-9]+')
    passed=$(printf '%s' "$line" | grep -oE 'passed="[0-9]+"' | grep -oE '[0-9]+')
    failed=$(printf '%s' "$line" | grep -oE 'failed="[0-9]+"' | grep -oE '[0-9]+')
    skipped=$(printf '%s' "$line" | grep -oE 'notExecuted="[0-9]+"' | grep -oE '[0-9]+')
    printf '%s %s %s %s\n' "${total:-0}" "${passed:-0}" "${failed:-0}" "${skipped:-0}"
}

# run_dotnet_test <filter> <trx-label> - RUN_EXIT_CODE / RUN_TRX_PATH / RUN_TOTAL / RUN_PASSED /
# RUN_FAILED / RUN_SKIPPED / RUN_COUNTERS_OK global degiskenlerini SETLER.
run_dotnet_test() {
    filter="$1"
    label="$2"
    trx_name="ebelge-$label-$(date +%Y%m%d-%H%M%S)-$$.trx"
    RUN_TRX_PATH="$TRX_DIR/$trx_name"

    if dotnet test "$TEST_PROJECT" --filter "$filter" --nologo --logger "trx;LogFileName=$trx_name"; then
        RUN_EXIT_CODE=0
    else
        RUN_EXIT_CODE=$?
    fi

    counters=$(get_trx_counters "$RUN_TRX_PATH")
    if [ -z "$counters" ]; then
        RUN_TOTAL=0
        RUN_PASSED=0
        RUN_FAILED=0
        RUN_SKIPPED=0
        RUN_COUNTERS_OK=0
    else
        RUN_TOTAL=$(printf '%s' "$counters" | cut -d' ' -f1)
        RUN_PASSED=$(printf '%s' "$counters" | cut -d' ' -f2)
        RUN_FAILED=$(printf '%s' "$counters" | cut -d' ' -f3)
        RUN_SKIPPED=$(printf '%s' "$counters" | cut -d' ' -f4)
        RUN_COUNTERS_OK=1
    fi
}

# Faz 2B.9.1 gorev md.5 - preflight'lar e-Belge entegrasyon testlerinin ZATEN kullandigi
# baglanti/sidecar-fixture yolunu KENDI kisa omurlu ornekleriyle dogrulayan GERCEK dotnet test
# calistirmalaridir (bkz. EBelgeSqlSidecarPreflightTests). Baglanti dizesi/secret HICBIR ZAMAN
# loglanmaz.
check_dependency() {
    dep="$1"
    case "$dep" in
        SqlServer)
            if [ -z "${STYS_INTEGRATION_TEST_CONNECTION_STRING:-}" ]; then
                echo "HATA: STYS_INTEGRATION_TEST_CONNECTION_STRING tanimli degil - SqlServer dependency saglanamiyor." >&2
                return 1
            fi
            echo "SqlServer erisilebilirlik on-kontrolu calistiriliyor..."
            run_dotnet_test "Purpose=SqlPreflight" "preflight-sql"
            if [ "$RUN_COUNTERS_OK" != "1" ] || [ "$RUN_PASSED" != "1" ] || [ "$RUN_FAILED" != "0" ] || [ "$RUN_SKIPPED" != "0" ]; then
                echo "HATA: SqlServer on-kontrolu basarisiz (Passed=$RUN_PASSED Failed=$RUN_FAILED Skipped=$RUN_SKIPPED)." >&2
                return 1
            fi
            echo "SqlServer erisilebilir."
            return 0
            ;;
        JavaSidecar)
            echo "JavaSidecar calisirlik on-kontrolu calistiriliyor (kisa omurlu, ayri bir prob - ana kosumla ES ZAMANLI DEGIL)..."
            run_dotnet_test "Purpose=JavaSidecarPreflight" "preflight-java"
            if [ "$RUN_COUNTERS_OK" != "1" ] || [ "$RUN_PASSED" != "1" ] || [ "$RUN_FAILED" != "0" ]; then
                echo "HATA: JavaSidecar on-kontrolu basarisiz." >&2
                return 1
            fi
            echo "JavaSidecar calisir durumda."
            return 0
            ;;
        Cryptography)
            # Gercek RSA test sertifikasi bellekte uretilir - harici bir servis/dosya BAGIMLILIGI
            # YOKTUR, ayrica bir preflight GEREKMEZ.
            return 0
            ;;
        *)
            echo "HATA: bilinmeyen dependency '$dep'." >&2
            return 1
            ;;
    esac
}

if [ "$LIST_ONLY" = "1" ]; then
    dotnet test "$TEST_PROJECT" --list-tests --filter "$FILTER" --nologo
    exit $?
fi

if [ "$VALIDATE_ONLY" = "1" ]; then
    echo "=== --validate: manifest + filtre + metadata sozlesmesi dogrulanacak, dis/agir testler CALISTIRILMAYACAK ==="
    run_dotnet_test "FullyQualifiedName~EBelgeTestMetadataContractTests" "validate-metadata"
    if [ "$RUN_EXIT_CODE" != "0" ] || [ "$RUN_COUNTERS_OK" != "1" ] || [ "$RUN_FAILED" != "0" ]; then
        echo "HATA: metadata sozlesme dogrulamasi basarisiz." >&2
        exit 1
    fi
    echo "Metadata sozlesmesi GECERLI ($RUN_PASSED/$RUN_TOTAL)."

    if ! dotnet test "$TEST_PROJECT" --list-tests --filter "$FILTER" --nologo >/dev/null; then
        echo "HATA: '$PROFILE' filtresiyle test discovery basarisiz oldu." >&2
        exit 1
    fi
    echo "'$PROFILE' filtresiyle test discovery GECERLI."
    echo "'$PROFILE' profili GECERLI (yalniz dogrulama - hicbir dis test calistirilmadi)."
    exit 0
fi

for dep in $REQUIRED_DEPS; do
    if ! check_dependency "$dep"; then
        echo "" >&2
        echo "HATA: '$PROFILE' profili icin gerekli dependency ('$dep') saglanamadi - profil DURDURULDU (yesile ZORLANMADI)." >&2
        exit 1
    fi
done

if [ "$REQUIRE_CRITICAL" = "true" ]; then
    echo "Kritik invariant manifest dogrulamasi calistiriliyor..."
    run_dotnet_test "FullyQualifiedName~EBelgeTestMetadataContractTests" "preflight-criticalinvariants"
    if [ "$RUN_EXIT_CODE" != "0" ] || [ "$RUN_COUNTERS_OK" != "1" ] || [ "$RUN_FAILED" != "0" ]; then
        echo "HATA: kritik invariant manifest dogrulamasi basarisiz - '$PROFILE' DURDURULDU." >&2
        exit 1
    fi
    echo "Kritik invariant manifesti dogrulandi ($RUN_PASSED/$RUN_TOTAL)."
fi

echo ""
START_TS=$(date +%s)
run_dotnet_test "$FILTER" "$PROFILE"
END_TS=$(date +%s)
ELAPSED=$((END_TS - START_TS))

FINAL_EXIT="$RUN_EXIT_CODE"

echo ""
echo "=== $PROFILE profili tamamlandi: ${ELAPSED} sn ==="
if [ "$RUN_COUNTERS_OK" = "1" ]; then
    echo "Passed: $RUN_PASSED  Failed: $RUN_FAILED  Skipped: $RUN_SKIPPED  Total: $RUN_TOTAL"
else
    echo "UYARI: TRX sonucu okunamadi ($RUN_TRX_PATH)." >&2
fi
echo "TRX sonucu: $RUN_TRX_PATH"

if [ "$FAIL_ON_SKIPPED" = "true" ] && [ "$RUN_COUNTERS_OK" = "1" ] && [ "$RUN_SKIPPED" -gt 0 ]; then
    echo "HATA: '$PROFILE' profilinde $RUN_SKIPPED test ATLANDI - bu profilde sifir-skip politikasi ZORUNLUDUR (dotnet test'in KENDI exit code'u bunu YANSITMAZ)." >&2
    FINAL_EXIT=1
fi

if [ "$FINAL_EXIT" != "0" ]; then
    echo "HATA: '$PROFILE' profili basarisiz oldu (exit code $FINAL_EXIT)." >&2
else
    echo "'$PROFILE' profili basariyla tamamlandi."
fi

exit "$FINAL_EXIT"
