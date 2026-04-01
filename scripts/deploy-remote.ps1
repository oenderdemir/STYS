param(
    [switch]$WithLogin,
    [string]$RegistryServer = "",
    [string]$Username = "",
    [string]$Password = "",
    [switch]$SkipDatabaseBootstrap
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($WithLogin) {
    if ([string]::IsNullOrWhiteSpace($RegistryServer)) {
        throw "WithLogin kullaniliyorsa RegistryServer vermen gerekiyor."
    }

    if ([string]::IsNullOrWhiteSpace($Username) -or [string]::IsNullOrWhiteSpace($Password)) {
        throw "WithLogin kullaniliyorsa Username ve Password vermen gerekiyor."
    }

    Write-Host "Registry login yapiliyor: $RegistryServer"
    $Password | docker login $RegistryServer --username $Username --password-stdin
}

if (-not $SkipDatabaseBootstrap) {
    $mssqlContainerState = docker compose ps --format json mssql | ConvertFrom-Json -ErrorAction SilentlyContinue
    $mssqlContainerItems = @($mssqlContainerState)

    if (-not $mssqlContainerState -or $mssqlContainerItems.Count -eq 0) {
        Write-Host "mssql container bulunamadi. Ilk kurulum icin mssql ayaga kaldiriliyor..."
        docker compose up -d mssql
    }
    elseif ($mssqlContainerItems[0].State -ne "running") {
        Write-Host "mssql container calismiyor. Tekrar baslatiliyor..."
        docker compose up -d mssql
    }
    else {
        Write-Host "mssql zaten calisiyor. Dokunulmuyor."
    }
}

Write-Host "Remote image'lar cekiliyor: backend, frontend"
docker compose pull backend frontend

Write-Host "Container'lar yeniden olusturuluyor: backend, frontend"
docker compose up -d --no-deps backend frontend

Write-Host ""
Write-Host "Deploy tamamlandi."
Write-Host "Kontrol icin:"
Write-Host " - docker compose ps"
Write-Host " - docker compose logs --tail 200 backend"
Write-Host " - docker compose logs --tail 200 frontend"
