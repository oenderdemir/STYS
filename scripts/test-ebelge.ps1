param(
    [Parameter(Position = 0)]
    [ValidateSet("fast", "integration", "nightly", "release")]
    [string]$Profile = "fast",
    [string]$TestProject = "tests/STYS.Tests/STYS.Tests.csproj",
    [switch]$ListTestsOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDirectory
Set-Location $projectRoot

# --- Faz 2B.9 profil manifesti (merkezi, tek yerde tanimli - script disina kopyalanmaz) ---
# Her profil hangi TestLevel degerlerini kapsar. "release" ozel deger "*" ile TUM Domain=EBelge
# testlerini (TestLevel'dan bagimsiz) kapsar.
$ProfilManifesti = @{
    fast        = @("Unit", "Contract")
    integration = @("Unit", "Contract", "SqlIntegration", "CryptoIntegration")
    nightly     = @("Unit", "Contract", "SqlIntegration", "SidecarIntegration", "CryptoIntegration", "WorkerEndToEnd")
    release     = @("*")
}

# release disindaki profillerde ReleaseGate testleri dahil edilmez - ReleaseGate az sayida,
# TUM zinciri kanitlayan temsili testtir; yalnizca release profilinde (Domain=EBelge tumu icinde) calisir.

$seviyeler = $ProfilManifesti[$Profile]

if ($seviyeler -contains "*") {
    $filtre = "Domain=EBelge"
} else {
    $parcalar = $seviyeler | ForEach-Object { "(Domain=EBelge&TestLevel=$_)" }
    $filtre = [string]::Join("|", $parcalar)
}

Write-Host ""
Write-Host "=== e-Belge test profili: $Profile ===" -ForegroundColor Cyan
Write-Host "TestLevel kapsami: $($seviyeler -join ', ')"
Write-Host "Filtre: $filtre"
Write-Host ""

if ($ListTestsOnly) {
    dotnet test $TestProject --list-tests --filter $filtre --nologo
    exit $LASTEXITCODE
}

if ($Profile -eq "nightly" -or $Profile -eq "release" -or $Profile -eq "integration") {
    if ([string]::IsNullOrWhiteSpace($env:STYS_INTEGRATION_TEST_CONNECTION_STRING)) {
        Write-Host "UYARI: STYS_INTEGRATION_TEST_CONNECTION_STRING tanimli degil - SQL Server/worker" -ForegroundColor Yellow
        Write-Host "gerektiren testler mevcut acik skip politikasiyla ATLANACAK (yesile zorlanmaz)." -ForegroundColor Yellow
        Write-Host ""
    }
}

$trxDosyaAdi = "ebelge-$Profile-$(Get-Date -Format 'yyyyMMdd-HHmmss').trx"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

dotnet test $TestProject --filter $filtre --nologo --logger "trx;LogFileName=$trxDosyaAdi"
$exitCode = $LASTEXITCODE

$sw.Stop()
Write-Host ""
Write-Host "=== $Profile profili tamamlandi: $([math]::Round($sw.Elapsed.TotalSeconds, 1)) sn ===" -ForegroundColor Cyan
Write-Host "TRX sonucu: tests/STYS.Tests/TestResults/$trxDosyaAdi"

if ($exitCode -ne 0) {
    Write-Host "HATA: '$Profile' profili basarisiz oldu (exit code $exitCode)." -ForegroundColor Red
} else {
    Write-Host "'$Profile' profili basariyla tamamlandi." -ForegroundColor Green
}

exit $exitCode
