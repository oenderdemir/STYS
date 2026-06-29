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
    $observabilityFileCheck = "test -f docker-compose.observability.yml || (echo 'HATA: docker-compose.observability.yml bulunamadi.' && exit 1) &&`n"
} else {
    $observabilityFileCheck = ""
}

$remoteCommand = @"
cd '$RemoteDir' &&
test -f .env || (echo 'HATA: .env dosyasi bulunamadi. Once .env dosyasini olusturun.' && exit 1) &&
test -f docker-compose.yml || (echo 'HATA: docker-compose.yml bulunamadi.' && exit 1) &&
$($observabilityFileCheck)set -a &&
. ./images/stys-image.env &&
set +a &&
docker load -i images/backend.tar &&
docker load -i images/frontend.tar &&
docker compose $composeArgs up -d
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
