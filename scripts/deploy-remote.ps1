param(
    [string]$VpsHost = "185.229.12.39",
    [string]$VpsUser = "root",
    [string]$SshKeyPath = "id_ed25519",
    [string]$RemoteDir = "/root/stys",
    [string]$Tag = "latest",
    [switch]$IncludeObservability
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }
}

$resolvedSshKeyPath = $SshKeyPath
if (-not [System.IO.Path]::IsPathRooted($resolvedSshKeyPath)) {
    $candidateKeyPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent) $resolvedSshKeyPath
    if (Test-Path -LiteralPath $candidateKeyPath) {
        $resolvedSshKeyPath = $candidateKeyPath
    }
}

$remoteTarget = "$VpsUser@$VpsHost"

$composeArgs = "--env-file .env -f docker-compose.yml"
if ($IncludeObservability) {
    $composeArgs += " -f docker-compose.observability.yml"
}

if ($IncludeObservability) {
    $obsChecks = @(
        "test -f docker-compose.observability.yml || (echo 'HATA: docker-compose.observability.yml bulunamadi.' && exit 1)",
        "test -f observability/alloy/config.alloy || (echo 'HATA: observability/alloy/config.alloy bulunamadi.' && exit 1)",
        "test -f observability/loki/loki-config.yml || (echo 'HATA: observability/loki/loki-config.yml bulunamadi.' && exit 1)",
        "test -d observability/grafana/provisioning || (echo 'HATA: observability/grafana/provisioning klasoru bulunamadi.' && exit 1)"
    )
    $observabilityFileCheck = ($obsChecks -join " &&`n") + " &&`n"
} else {
    $observabilityFileCheck = ""
}

$remoteCommand = @"
cd '$RemoteDir' &&
test -f .env || (echo 'HATA: .env dosyasi bulunamadi. $RemoteDir/.env dosyasini olusturun. Ornek icin .env.example dosyasina bakin.' && exit 1) &&
test -f docker-compose.yml || (echo 'HATA: docker-compose.yml bulunamadi.' && exit 1) &&
test -f images/backend.tar || (echo 'HATA: images/backend.tar bulunamadi. Once scripts/deploy-all.ps1 ile image artefactlarini VPSye kopyalayin.' && exit 1) &&
test -f images/frontend.tar || (echo 'HATA: images/frontend.tar bulunamadi. Once scripts/deploy-all.ps1 ile image artefactlarini VPSye kopyalayin.' && exit 1) &&
test -f images/schematron-validator.tar || (echo 'HATA: images/schematron-validator.tar bulunamadi. Schematron validator image artefacti VPSye kopyalanmamis.' && exit 1) &&
test -f images/stys-image.env || (echo 'HATA: images/stys-image.env bulunamadi. Image repository/tag env dosyasi eksik.' && exit 1) &&
test -f scripts/stys-integrity.env || (echo 'HATA: scripts/stys-integrity.env bulunamadi. Integrity hash env dosyasi eksik.' && exit 1) &&
$($observabilityFileCheck)set -a &&
. ./images/stys-image.env &&
set +a &&
docker load -i images/backend.tar &&
docker load -i images/frontend.tar &&
docker load -i images/schematron-validator.tar &&
docker compose $composeArgs up -d --no-build
"@

Write-Host "VPS deploy basliyor: $remoteTarget"
if ($IncludeObservability) {
    Write-Host "Observability stack dahil edildi (Grafana/Loki/Alloy)." -ForegroundColor Cyan
}
Invoke-NativeCommand ssh @('-i', $resolvedSshKeyPath, $remoteTarget, $remoteCommand)

Write-Host ""
Write-Host "Deploy tamamlandi."
Write-Host "Kontrol icin:"
Write-Host " - ssh -i $resolvedSshKeyPath $remoteTarget 'cd $RemoteDir && docker compose $composeArgs ps'"
Write-Host " - ssh -i $resolvedSshKeyPath $remoteTarget 'cd $RemoteDir && docker compose $composeArgs logs --tail 200 backend'"
Write-Host " - ssh -i $resolvedSshKeyPath $remoteTarget 'cd $RemoteDir && docker compose $composeArgs logs --tail 200 frontend'"
if ($IncludeObservability) {
    Write-Host " - ssh -i $resolvedSshKeyPath $remoteTarget 'cd $RemoteDir && docker compose $composeArgs logs --tail 200 alloy'"
    Write-Host " - ssh -i $resolvedSshKeyPath $remoteTarget 'cd $RemoteDir && docker compose $composeArgs logs --tail 200 loki'"
    Write-Host " - ssh -i $resolvedSshKeyPath $remoteTarget 'cd $RemoteDir && docker compose $composeArgs logs --tail 200 grafana'"
}
