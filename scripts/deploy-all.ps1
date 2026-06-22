param(
    [string]$VpsHost = "185.229.12.39",
    [string]$VpsUser = "root",
    [string]$SshKeyPath = "id_ed25519",
    [string]$RemoteDir = "/root/stys",
    [string]$ComposeFilePath = "docker-compose.yml",
    [Parameter(Mandatory = $true)][string]$Tag
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Tag)) {
    throw "-Tag parametresi zorunludur. Ornek: -Tag v1.0.11"
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDirectory
Set-Location $projectRoot

Write-Host "Tag dogrulaniyor: $Tag"
$null = git rev-parse $Tag 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tag bulunamadi, mevcut HEAD'e olusturuluyor: $Tag"
    git tag $Tag
    if ($LASTEXITCODE -ne 0) {
        throw "Git tag olusturulamadi: $Tag"
    }
    Write-Host "Tag olusturuldu: $Tag"
}

$GitSha = (git rev-list -n 1 $Tag 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $GitSha.Length -lt 8) {
    throw "Tag icin commit SHA alinamadi: $Tag"
}
$ShortGitSha = $GitSha.Substring(0, 8)
$BuildTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

Write-Host "Tag:       $Tag"
Write-Host "GitSha:    $GitSha"
Write-Host "ShortSha:  $ShortGitSha"
Write-Host "BuildTime: $BuildTime"
Write-Host ""

& (Join-Path $scriptDirectory "push-images.ps1") `
    -VpsHost $VpsHost `
    -VpsUser $VpsUser `
    -SshKeyPath $SshKeyPath `
    -RemoteDir $RemoteDir `
    -ComposeFilePath $ComposeFilePath `
    -AppVersion $Tag `
    -GitSha $GitSha `
    -ShortGitSha $ShortGitSha `
    -BuildTime $BuildTime

& (Join-Path $scriptDirectory "deploy-remote.ps1") `
    -VpsHost $VpsHost `
    -VpsUser $VpsUser `
    -SshKeyPath $SshKeyPath `
    -RemoteDir $RemoteDir `
    -Tag "$Tag-$ShortGitSha"
