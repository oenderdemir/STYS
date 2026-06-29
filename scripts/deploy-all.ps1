param(
    [string]$VpsHost = "185.229.12.39",
    [string]$VpsUser = "root",
    [string]$SshKeyPath = "id_ed25519",
    [string]$RemoteDir = "/root/stys",
    [string]$ComposeFilePath = "docker-compose.yml",
    [Parameter(Mandatory = $true)][string]$Tag,
    [switch]$AllowDirtyWorkingTree,
    [switch]$IncludeObservability
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDirectory
Set-Location $projectRoot

# --- 1. Working tree kontrolu ---
if (-not $AllowDirtyWorkingTree) {
    $dirtyFiles = git status --porcelain
    if (-not [string]::IsNullOrWhiteSpace($dirtyFiles)) {
        Write-Host ""
        Write-Host "HATA: Working tree temiz degil. Once degisiklikleri commit/stash yapin." -ForegroundColor Red
        Write-Host $dirtyFiles
        Write-Host ""
        Write-Host "Dirty working tree ile deploy etmek icin: -AllowDirtyWorkingTree" -ForegroundColor Yellow
        exit 1
    }
}

# --- 2. Local tag kontrolu ---
Write-Host "Local tag kontrol ediliyor: $Tag"
$localTag = git tag -l $Tag
if ([string]::IsNullOrWhiteSpace($localTag)) {
    Write-Host "Local tag bulunamadi, HEAD'e olusturuluyor: $Tag"
    git tag $Tag
    if ($LASTEXITCODE -ne 0) {
        Write-Host "HATA: Local tag olusturulamadi: $Tag" -ForegroundColor Red
        exit 1
    }
    Write-Host "Local tag olusturuldu: $Tag" -ForegroundColor Green
}

# --- 3. Local tag commit SHA ---
# rev-list annotated tag'i de dogru cozer (^{} olmaksizin calisir)
$GitSha = (git rev-list -n 1 $Tag 2>&1).Trim()
if ($LASTEXITCODE -ne 0 -or $GitSha.Length -lt 40) {
    Write-Host "HATA: Tag icin commit SHA alinamadi: $Tag" -ForegroundColor Red
    exit 1
}
$ShortGitSha = $GitSha.Substring(0, 8)

# --- 4. Remote tag kontrolu ---
Write-Host "Origin tag kontrol ediliyor: $Tag"
$remoteLines = git ls-remote --tags origin "refs/tags/$Tag" "refs/tags/$Tag^{}" 2>&1
$remoteTagExists = ($LASTEXITCODE -eq 0) -and (-not [string]::IsNullOrWhiteSpace($remoteLines))

if (-not $remoteTagExists) {
    Write-Host "Origin uzerinde tag bulunamadi. Pushlanıyor: $Tag"
    git push origin $Tag
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "HATA: Tag origin'e pushlanamadi: $Tag" -ForegroundColor Red
        Write-Host "Deploy durduruldu."
        exit 1
    }
    Write-Host "Tag origin'e pushlandı: $Tag" -ForegroundColor Green

    # Push sonrasi remote SHA dogrula
    $remoteLines = git ls-remote --tags origin "refs/tags/$Tag" "refs/tags/$Tag^{}" 2>&1
} else {
    Write-Host "Tag origin uzerinde mevcut: $Tag"
}

# --- 5. Remote SHA coz ve local ile karsilastir ---
# Annotated tag icin refs/tags/$Tag^{} satirini tercih et
$remoteSha = $null
foreach ($line in ($remoteLines -split "`n")) {
    $line = $line.Trim()
    if ($line -match '^([0-9a-f]{40})\s+refs/tags/.+\^\{\}$') {
        $remoteSha = $Matches[1]
        break
    }
}
if (-not $remoteSha) {
    foreach ($line in ($remoteLines -split "`n")) {
        $line = $line.Trim()
        if ($line -match '^([0-9a-f]{40})\s+refs/tags/') {
            $remoteSha = $Matches[1]
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($remoteSha)) {
    Write-Host "HATA: Origin'deki tag icin SHA cozulemedi: $Tag" -ForegroundColor Red
    exit 1
}

if ($remoteSha -ne $GitSha) {
    Write-Host ""
    Write-Host "HATA: Origin tag ile local tag farklı commit'e isaret ediyor." -ForegroundColor Red
    Write-Host "  Local : $GitSha"
    Write-Host "  Origin: $remoteSha"
    Write-Host "Deploy durduruldu. Tag force push yapılmayacak." -ForegroundColor Yellow
    exit 1
}

# --- 6. Deploy bilgileri ---
$BuildTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

Write-Host ""
Write-Host "=== Deploy Ozeti ===" -ForegroundColor Cyan
Write-Host "Tag:            $Tag"
Write-Host "Git SHA:        $GitSha"
Write-Host "Short SHA:      $ShortGitSha"
Write-Host "Build Time:     $BuildTime"
Write-Host "Backend image:  stys-backend:$Tag-$ShortGitSha"
Write-Host "Frontend image: stys-frontend:$Tag-$ShortGitSha"
Write-Host "====================" -ForegroundColor Cyan
Write-Host ""

# --- 7. Build ve push ---
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

# --- 8. Remote deploy ---
$remoteDeployParams = @{
    VpsHost    = $VpsHost
    VpsUser    = $VpsUser
    SshKeyPath = $SshKeyPath
    RemoteDir  = $RemoteDir
    Tag        = "$Tag-$ShortGitSha"
}
if ($IncludeObservability) { $remoteDeployParams['IncludeObservability'] = $true }
& (Join-Path $scriptDirectory "deploy-remote.ps1") @remoteDeployParams
