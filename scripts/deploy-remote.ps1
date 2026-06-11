param(
    [string]$VpsHost = "185.229.12.39",
    [string]$VpsUser = "root",
    [string]$SshKeyPath = "id_ed25519",
    [string]$RemoteDir = "/root/stys",
    [string]$Tag = "latest"
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
$remoteCommand = @"
cd '$RemoteDir' &&
set -a &&
. ./images/stys-image.env &&
set +a &&
docker load -i images/backend.tar &&
docker load -i images/frontend.tar &&
docker compose up -d
"@

Write-Host "VPS deploy basliyor: $remoteTarget"
Invoke-NativeCommand ssh @('-i', $resolvedSshKeyPath, $remoteTarget, $remoteCommand)

Write-Host ""
Write-Host "Deploy tamamlandi."
Write-Host "Kontrol icin:"
Write-Host " - ssh -i $resolvedSshKeyPath $remoteTarget 'cd $RemoteDir && docker compose ps'"
Write-Host " - ssh -i $resolvedSshKeyPath $remoteTarget 'cd $RemoteDir && docker compose logs --tail 200 backend'"
Write-Host " - ssh -i $resolvedSshKeyPath $remoteTarget 'cd $RemoteDir && docker compose logs --tail 200 frontend'"
