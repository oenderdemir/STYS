param(
    [string]$RegistryName,
    [string]$RegistryServer,
    [string]$RepositoryPrefix = "stys",
    [string]$Tag = "",
    [switch]$SkipLogin
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RegistryServer)) {
    if ([string]::IsNullOrWhiteSpace($RegistryName)) {
        throw "RegistryName veya RegistryServer vermen gerekiyor."
    }

    $RegistryServer = "$RegistryName.azurecr.io"
}

if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = Get-Date -Format "yyyyMMddHHmmss"
}

$backendImage = "$RegistryServer/$RepositoryPrefix/backend"
$frontendImage = "$RegistryServer/$RepositoryPrefix/frontend"

Write-Host "Registry server : $RegistryServer"
Write-Host "Backend image   : $backendImage`:$Tag"
Write-Host "Frontend image  : $frontendImage`:$Tag"

if (-not $SkipLogin) {
    if (-not [string]::IsNullOrWhiteSpace($RegistryName)) {
        Write-Host "Azure Container Registry login yapiliyor..."
        az acr login --name $RegistryName
    }
    else {
        Write-Host "RegistryName verilmedi. Mevcut docker login oturumu kullanilacak."
    }
}

$env:STYS_BACKEND_IMAGE = $backendImage
$env:STYS_FRONTEND_IMAGE = $frontendImage
$env:STYS_IMAGE_TAG = $Tag

Write-Host "Docker image build basliyor..."
docker compose build backend frontend

Write-Host "Docker image push basliyor..."
docker compose push backend frontend

Write-Host ""
Write-Host "Push tamamlandi:"
Write-Host " - $backendImage`:$Tag"
Write-Host " - $frontendImage`:$Tag"
