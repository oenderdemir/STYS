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

$remoteTarget = "$VpsUser@$VpsHost"
$remoteCommand = @"
cd '$RemoteDir' &&
. images/stys-image.env &&
docker load -i images/backend.tar &&
docker load -i images/frontend.tar &&
docker compose up -d
"@

Write-Host "VPS deploy basliyor: $remoteTarget"
Invoke-NativeCommand ssh @('-i', $SshKeyPath, $remoteTarget, $remoteCommand)

Write-Host ""
Write-Host "Deploy tamamlandi."
Write-Host "Kontrol icin:"
Write-Host " - ssh -i $SshKeyPath $remoteTarget 'cd $RemoteDir && docker compose ps'"
Write-Host " - ssh -i $SshKeyPath $remoteTarget 'cd $RemoteDir && docker compose logs --tail 200 backend'"
Write-Host " - ssh -i $SshKeyPath $remoteTarget 'cd $RemoteDir && docker compose logs --tail 200 frontend'"
