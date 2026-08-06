param(
    [Parameter(Position = 0)]
    [string]$ProfileName = "fast",
    [string]$TestProject,
    [switch]$ListTestsOnly,
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDirectory
Set-Location $projectRoot

# --- Faz 2B.9.1: tek merkezi profil manifesti - profil/TestLevel/dependency tanimlari BURADA
# TEKRARLANMAZ, hem bu script hem test-ebelge.sh AYNI dosyayi okur. ---
$ManifestYolu = Join-Path $scriptDirectory "ebelge-test-profiles.json"

if (-not (Test-Path $ManifestYolu)) {
    Write-Host "HATA: profil manifesti bulunamadi: $ManifestYolu - test CALISTIRILMAYACAK." -ForegroundColor Red
    exit 1
}

try {
    $Manifest = Get-Content $ManifestYolu -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
} catch {
    Write-Host "HATA: profil manifesti gecerli JSON degil: $ManifestYolu" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

$KnownTestLevels = @($Manifest.knownTestLevels)
$KnownDependencies = @($Manifest.knownDependencies)

$ProfileNames = @($Manifest.profiles.PSObject.Properties.Name)
if ($ProfileNames -notcontains $ProfileName) {
    Write-Host "HATA: bilinmeyen profil '$ProfileName'. Bilinen profiller: $($ProfileNames -join ', ')" -ForegroundColor Red
    exit 1
}

$ProfileDef = $Manifest.profiles.$ProfileName
$Seviyeler = @($ProfileDef.testLevels)
foreach ($seviye in $Seviyeler) {
    if ($KnownTestLevels -notcontains $seviye) {
        Write-Host "HATA: manifestte bilinmeyen TestLevel '$seviye' (profil: $ProfileName) - test CALISTIRILMAYACAK." -ForegroundColor Red
        exit 1
    }
}

$RequiredDeps = @($ProfileDef.requiredDependencies)
foreach ($dep in $RequiredDeps) {
    if ($KnownDependencies -notcontains $dep) {
        Write-Host "HATA: manifestte bilinmeyen Dependency '$dep' (profil: $ProfileName) - test CALISTIRILMAYACAK." -ForegroundColor Red
        exit 1
    }
}

$AllDomainTests = [bool]$ProfileDef.allDomainTests
if ($AllDomainTests) {
    $Filtre = "Domain=EBelge"
} else {
    $Parcalar = $Seviyeler | ForEach-Object { "(Domain=EBelge&TestLevel=$_)" }
    $Filtre = [string]::Join("|", $Parcalar)
}

$RequireCriticalInvariants = [bool]$ProfileDef.requireCriticalInvariants
$FailOnSkipped = [bool]$ProfileDef.failOnSkippedTests
$TestProjectPath = if ($TestProject) { $TestProject } else { [string]$Manifest.testProject }
$TrxDizini = Join-Path (Split-Path -Parent $TestProjectPath) "TestResults"

Write-Host ""
Write-Host "=== e-Belge test profili: $ProfileName ===" -ForegroundColor Cyan
Write-Host "TestLevel kapsami: $(if ($AllDomainTests) { 'TUMU (Domain=EBelge)' } else { $Seviyeler -join ', ' })"
Write-Host "Filtre: $Filtre"
Write-Host "Gerekli dependency: $(if ($RequiredDeps.Count -gt 0) { $RequiredDeps -join ', ' } else { '(yok)' })"
Write-Host "Sifir-skip politikasi: $FailOnSkipped"
Write-Host ""

function Get-TrxCounters {
    param([string]$TrxPath)
    if (-not (Test-Path $TrxPath)) {
        return $null
    }
    [xml]$xml = Get-Content $TrxPath
    $counters = $xml.TestRun.ResultSummary.Counters
    return [PSCustomObject]@{
        Total   = [int]$counters.total
        Passed  = [int]$counters.passed
        Failed  = [int]$counters.failed
        Skipped = [int]$counters.notExecuted
    }
}

function Invoke-EBelgeDotnetTest {
    param([string]$FilterExpr, [string]$TrxLabel)

    $trxName = "ebelge-$TrxLabel-$(Get-Date -Format 'yyyyMMdd-HHmmss-fff').trx"
    # ONEMLI: dotnet test'in kendi konsol ciktisi burada `Out-Host`'a yonlendirilir - aksi halde
    # PowerShell fonksiyon icindeki YAKALANMAMIS harici komut ciktisini fonksiyonun DONUS
    # DEGERINE (pipeline output) KARISTIRIR ve asagidaki PSCustomObject'i BOZAR.
    dotnet test $TestProjectPath --filter $FilterExpr --nologo --logger "trx;LogFileName=$trxName" | Out-Host
    $exitCode = $LASTEXITCODE
    $trxPath = Join-Path $TrxDizini $trxName

    return [PSCustomObject]@{
        ExitCode = $exitCode
        Counters = Get-TrxCounters -TrxPath $trxPath
        TrxPath  = $trxPath
    }
}

# Faz 2B.9.1 gorev md.5 - dependency preflight'lari, e-Belge entegrasyon testlerinin ZATEN
# kullandigi baglanti/sidecar-fixture yolunu KENDI kisa omurlu ornekleriyle dogrulayan GERCEK
# dotnet test calistirmalaridir (bkz. EBelgeSqlSidecarPreflightTests) - ham SqlClient/TCP kontrolu
# script tarafinda ICAT EDILMEZ, baglanti dizesi/secret HICBIR ZAMAN loglanmaz.
function Test-EBelgeDependency {
    param([string]$DependencyName)

    switch ($DependencyName) {
        "SqlServer" {
            if ([string]::IsNullOrWhiteSpace($env:STYS_INTEGRATION_TEST_CONNECTION_STRING)) {
                Write-Host "HATA: STYS_INTEGRATION_TEST_CONNECTION_STRING tanimli degil - SqlServer dependency saglanamiyor." -ForegroundColor Red
                return $false
            }
            Write-Host "SqlServer erisilebilirlik on-kontrolu calistiriliyor..."
            $r = Invoke-EBelgeDotnetTest -FilterExpr "Purpose=SqlPreflight" -TrxLabel "preflight-sql"
            if ($null -eq $r.Counters -or $r.Counters.Passed -ne 1 -or $r.Counters.Failed -ne 0 -or $r.Counters.Skipped -ne 0) {
                $c = $r.Counters
                Write-Host "HATA: SqlServer on-kontrolu basarisiz (Passed=$($c.Passed) Failed=$($c.Failed) Skipped=$($c.Skipped))." -ForegroundColor Red
                return $false
            }
            Write-Host "SqlServer erisilebilir." -ForegroundColor Green
            return $true
        }
        "JavaSidecar" {
            Write-Host "JavaSidecar calisirlik on-kontrolu calistiriliyor (kisa omurlu, ayri bir prob - ana kosumla ES ZAMANLI DEGIL)..."
            $r = Invoke-EBelgeDotnetTest -FilterExpr "Purpose=JavaSidecarPreflight" -TrxLabel "preflight-java"
            if ($null -eq $r.Counters -or $r.Counters.Passed -ne 1 -or $r.Counters.Failed -ne 0) {
                Write-Host "HATA: JavaSidecar on-kontrolu basarisiz." -ForegroundColor Red
                return $false
            }
            Write-Host "JavaSidecar calisir durumda." -ForegroundColor Green
            return $true
        }
        "Cryptography" {
            # Gercek RSA test sertifikasi bellekte uretilir (EBelgeTestSertifikaSaglayici) - harici
            # bir servis/dosya BAGIMLILIGI YOKTUR, bu yuzden ayrica bir preflight GEREKMEZ.
            return $true
        }
        default {
            Write-Host "HATA: bilinmeyen dependency '$DependencyName'." -ForegroundColor Red
            return $false
        }
    }
}

if ($ListTestsOnly) {
    dotnet test $TestProjectPath --list-tests --filter $Filtre --nologo
    exit $LASTEXITCODE
}

if ($ValidateOnly) {
    Write-Host "=== -ValidateOnly: manifest + filtre + metadata sozlesmesi dogrulanacak, dis/agir testler CALISTIRILMAYACAK ===" -ForegroundColor Cyan
    $r = Invoke-EBelgeDotnetTest -FilterExpr "FullyQualifiedName~EBelgeTestMetadataContractTests" -TrxLabel "validate-metadata"
    if ($r.ExitCode -ne 0 -or $null -eq $r.Counters -or $r.Counters.Failed -ne 0) {
        Write-Host "HATA: metadata sozlesme dogrulamasi basarisiz." -ForegroundColor Red
        exit 1
    }
    Write-Host "Metadata sozlesmesi GECERLI ($($r.Counters.Passed)/$($r.Counters.Total))." -ForegroundColor Green

    dotnet test $TestProjectPath --list-tests --filter $Filtre --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "HATA: '$ProfileName' filtresiyle test discovery basarisiz oldu." -ForegroundColor Red
        exit 1
    }
    Write-Host "'$ProfileName' filtresiyle test discovery GECERLI." -ForegroundColor Green
    Write-Host "'$ProfileName' profili GECERLI (yalniz dogrulama - hicbir dis test calistirilmadi)." -ForegroundColor Green
    exit 0
}

foreach ($dep in $RequiredDeps) {
    if (-not (Test-EBelgeDependency $dep)) {
        Write-Host ""
        Write-Host "HATA: '$ProfileName' profili icin gerekli dependency ('$dep') saglanamadi - profil DURDURULDU (yesile ZORLANMADI)." -ForegroundColor Red
        exit 1
    }
}

if ($RequireCriticalInvariants) {
    Write-Host "Kritik invariant manifest dogrulamasi calistiriliyor..."
    $r = Invoke-EBelgeDotnetTest -FilterExpr "FullyQualifiedName~EBelgeTestMetadataContractTests" -TrxLabel "preflight-criticalinvariants"
    if ($r.ExitCode -ne 0 -or $null -eq $r.Counters -or $r.Counters.Failed -ne 0) {
        Write-Host "HATA: kritik invariant manifest dogrulamasi basarisiz - '$ProfileName' DURDURULDU." -ForegroundColor Red
        exit 1
    }
    Write-Host "Kritik invariant manifesti dogrulandi ($($r.Counters.Passed)/$($r.Counters.Total))." -ForegroundColor Green
}

Write-Host ""
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$mainResult = Invoke-EBelgeDotnetTest -FilterExpr $Filtre -TrxLabel $ProfileName
$sw.Stop()

$counters = $mainResult.Counters
$finalExit = $mainResult.ExitCode

Write-Host ""
Write-Host "=== $ProfileName profili tamamlandi: $([math]::Round($sw.Elapsed.TotalSeconds, 1)) sn ===" -ForegroundColor Cyan
if ($null -ne $counters) {
    Write-Host "Passed: $($counters.Passed)  Failed: $($counters.Failed)  Skipped: $($counters.Skipped)  Total: $($counters.Total)"
} else {
    Write-Host "UYARI: TRX sonucu okunamadi ($($mainResult.TrxPath))." -ForegroundColor Yellow
}
Write-Host "TRX sonucu: $($mainResult.TrxPath)"

if ($FailOnSkipped -and $null -ne $counters -and $counters.Skipped -gt 0) {
    Write-Host "HATA: '$ProfileName' profilinde $($counters.Skipped) test ATLANDI - bu profilde sifir-skip politikasi ZORUNLUDUR (dotnet test'in KENDI exit code'u bunu YANSITMAZ)." -ForegroundColor Red
    $finalExit = 1
}

if ($finalExit -ne 0) {
    Write-Host "HATA: '$ProfileName' profili basarisiz oldu (exit code $finalExit)." -ForegroundColor Red
} else {
    Write-Host "'$ProfileName' profili basariyla tamamlandi." -ForegroundColor Green
}

exit $finalExit
