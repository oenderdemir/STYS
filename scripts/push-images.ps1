param(
    [string]$VpsHost = "185.229.12.39",
    [string]$VpsUser = "root",
    [string]$SshKeyPath = "id_ed25519",
    [string]$RemoteDir = "/root/stys",
    [string]$ComposeFilePath = "docker-compose.yml",
    [string]$Tag = "latest"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDirectory
Set-Location $projectRoot

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

if (-not (Test-Path -LiteralPath $ComposeFilePath)) {
    throw "Compose dosyasi bulunamadi: $ComposeFilePath"
}

$artifactDir = Join-Path $projectRoot "artifacts\deploy\$Tag"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$backendImage = "stys/backend"
$frontendImage = "stys/frontend"
$backendTar = Join-Path $artifactDir "backend.tar"
$frontendTar = Join-Path $artifactDir "frontend.tar"

$env:STYS_IMAGE_TAG = $Tag

Write-Host "Image build basliyor..."
Invoke-NativeCommand docker @('compose', 'build', 'backend', 'frontend')

Write-Host "Image archive olusturuluyor..."
Invoke-NativeCommand docker @('save', '-o', $backendTar, "$backendImage`:$Tag")
Invoke-NativeCommand docker @('save', '-o', $frontendTar, "$frontendImage`:$Tag")

Write-Host "VPS'ye kopyalanacak dosyalar hazir:"
Write-Host " - $backendTar"
Write-Host " - $frontendTar"

Write-Host "Compose dosyasi ve image archive'lari VPS'ye kopyalaniyor..."
$remoteTarget = "$VpsUser@$VpsHost"
Invoke-NativeCommand ssh @('-i', $SshKeyPath, $remoteTarget, "mkdir -p '$RemoteDir/images'")
Invoke-NativeCommand scp @('-i', $SshKeyPath, $ComposeFilePath, "${remoteTarget}:$RemoteDir/docker-compose.yml")
Invoke-NativeCommand scp @('-i', $SshKeyPath, $backendTar, $frontendTar, "${remoteTarget}:$RemoteDir/images/")

Write-Host ""
Write-Host "Kopyalama tamamlandi:"
Write-Host " - ${remoteTarget}:$RemoteDir/docker-compose.yml"
Write-Host " - ${remoteTarget}:$RemoteDir/images/backend.tar"
Write-Host " - ${remoteTarget}:$RemoteDir/images/frontend.tar"
