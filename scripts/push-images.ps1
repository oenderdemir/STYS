param(
    [string]$VpsHost = "185.229.12.39",
    [string]$VpsUser = "root",
    [string]$SshKeyPath = "id_ed25519",
    [string]$RemoteDir = "/root/stys",
    [string]$ComposeFilePath = "docker-compose.yml",
    [Parameter(Mandatory = $true)][string]$AppVersion,
    [Parameter(Mandatory = $true)][string]$GitSha,
    [Parameter(Mandatory = $true)][string]$ShortGitSha,
    [Parameter(Mandatory = $true)][string]$BuildTime
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

function Get-ComposeConfig {
    $configJson = & docker compose config --format json
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose config failed with exit code $LASTEXITCODE"
    }

    return ($configJson | Out-String | ConvertFrom-Json)
}

function Split-ImageReference {
    param(
        [Parameter(Mandatory = $true)][string]$ImageReference
    )

    $lastSlashIndex = $ImageReference.LastIndexOf('/')
    $lastColonIndex = $ImageReference.LastIndexOf(':')

    if ($lastColonIndex -gt $lastSlashIndex) {
        return [pscustomobject]@{
            Repository = $ImageReference.Substring(0, $lastColonIndex)
            Tag = $ImageReference.Substring($lastColonIndex + 1)
        }
    }

    return [pscustomobject]@{
        Repository = $ImageReference
        Tag = "latest"
    }
}

function Get-BackendIntegrityEnvContent {
    param(
        [Parameter(Mandatory = $true)][string]$ImageReference,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    $containerId = $null
    $tempDir = Join-Path $WorkingDirectory ("integrity-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

    try {
        $containerId = & docker create $ImageReference
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($containerId)) {
            throw "docker create failed for image: $ImageReference"
        }

        $containerId = $containerId.Trim()
        Invoke-NativeCommand docker @('cp', "${containerId}:/app", $tempDir)

        $appDir = Join-Path $tempDir "app"
        if (-not (Test-Path -LiteralPath $appDir)) {
            throw "Integrity hash source directory was not found in the backend image: $appDir"
        }

        $dllFiles = Get-ChildItem -LiteralPath $appDir -Filter *.dll -File | Sort-Object Name
        if ($dllFiles.Count -eq 0) {
            throw "No DLL files were found under $appDir."
        }

        $lines = foreach ($dllFile in $dllFiles) {
            $fileBytes = [System.IO.File]::ReadAllBytes($dllFile.FullName)
            $sha256 = [System.Security.Cryptography.SHA256]::Create()
            try {
                $hashBytes = $sha256.ComputeHash($fileBytes)
            }
            finally {
                $sha256.Dispose()
            }
            $hashBase64 = [Convert]::ToBase64String($hashBytes)
            "Licensing__IntegrityHashes__{0}={1}" -f $dllFile.Name, $hashBase64
        }

        return ($lines -join "`n") + "`n"
    }
    finally {
        if (-not [string]::IsNullOrWhiteSpace($containerId)) {
            & docker rm $containerId | Out-Null
        }

        if (Test-Path -LiteralPath $tempDir) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force
        }
    }
}

if (-not (Test-Path -LiteralPath $ComposeFilePath)) {
    throw "Compose dosyasi bulunamadi: $ComposeFilePath"
}

$envFilePath = Join-Path $projectRoot ".env"
if (-not (Test-Path -LiteralPath $envFilePath)) {
    throw ".env dosyasi bulunamadi: $envFilePath"
}

# Full image tag: AppVersion + ShortGitSha (e.g. v1.0.11-d5ba8148)
$FullTag = "$AppVersion-$ShortGitSha"
$env:STYS_IMAGE_TAG = $FullTag

$composeConfig = Get-ComposeConfig
$backendImageReference = $composeConfig.services.backend.image
$frontendImageReference = $composeConfig.services.frontend.image
$backendImageInfo = Split-ImageReference $backendImageReference
$frontendImageInfo = Split-ImageReference $frontendImageReference

if ($backendImageInfo.Tag -ne $frontendImageInfo.Tag) {
    throw "Backend ve frontend image tag'leri farkli: $($backendImageInfo.Tag) / $($frontendImageInfo.Tag)"
}

$artifactDir = Join-Path $projectRoot "artifacts\deploy\$AppVersion"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$backendTar = Join-Path $artifactDir "backend.tar"
$frontendTar = Join-Path $artifactDir "frontend.tar"
$imageEnvFile = Join-Path $artifactDir "stys-image.env"
$integrityEnvFile = Join-Path $artifactDir "stys-integrity.env"
$resolvedSshKeyPath = $SshKeyPath
if (-not [System.IO.Path]::IsPathRooted($resolvedSshKeyPath)) {
    $candidateKeyPath = Join-Path $projectRoot $resolvedSshKeyPath
    if (Test-Path -LiteralPath $candidateKeyPath) {
        $resolvedSshKeyPath = $candidateKeyPath
    }
}

Write-Host "Image build basliyor (tag: $FullTag)..."

Invoke-NativeCommand docker @(
    'build',
    '--build-arg', "APP_VERSION=$AppVersion",
    '--build-arg', "IMAGE_TAG=$backendImageReference",
    '--build-arg', "GIT_SHA=$ShortGitSha",
    '--build-arg', "BUILD_TIME=$BuildTime",
    '-t', $backendImageReference,
    '-f', 'backend/Dockerfile',
    '.'
)

Invoke-NativeCommand docker @(
    'build',
    '--build-arg', "APP_VERSION=$AppVersion",
    '--build-arg', "IMAGE_TAG=$frontendImageReference",
    '--build-arg', "GIT_SHA=$ShortGitSha",
    '--build-arg', "BUILD_TIME=$BuildTime",
    '-t', $frontendImageReference,
    '-f', 'frontend/Dockerfile',
    '.'
)

Write-Host "Image archive olusturuluyor..."
Invoke-NativeCommand docker @('save', '-o', $backendTar, $backendImageReference)
Invoke-NativeCommand docker @('save', '-o', $frontendTar, $frontendImageReference)

Write-Host "Assembly integrity env dosyasi olusturuluyor..."
$integrityEnvContent = Get-BackendIntegrityEnvContent -ImageReference $backendImageReference -WorkingDirectory $artifactDir
[System.IO.File]::WriteAllText($integrityEnvFile, $integrityEnvContent, (New-Object System.Text.UTF8Encoding($false)))

$imageEnvContent = @(
    "export STYS_BACKEND_IMAGE=$($backendImageInfo.Repository)"
    "export STYS_FRONTEND_IMAGE=$($frontendImageInfo.Repository)"
    "export STYS_IMAGE_TAG=$($backendImageInfo.Tag)"
) -join "`n"
[System.IO.File]::WriteAllText($imageEnvFile, $imageEnvContent + "`n", (New-Object System.Text.UTF8Encoding($false)))

Write-Host "VPS'ye kopyalanacak dosyalar hazir:"
Write-Host " - $backendTar"
Write-Host " - $frontendTar"
Write-Host " - $imageEnvFile"
Write-Host " - $integrityEnvFile"

Write-Host "Compose dosyasi ve image archive'lari VPS'ye kopyalaniyor..."
$remoteTarget = "$VpsUser@$VpsHost"
Invoke-NativeCommand ssh @('-i', $resolvedSshKeyPath, $remoteTarget, "mkdir -p '$RemoteDir/images' '$RemoteDir/scripts'")
Invoke-NativeCommand scp @('-i', $resolvedSshKeyPath, $ComposeFilePath, "${remoteTarget}:$RemoteDir/docker-compose.yml")
Invoke-NativeCommand scp @('-i', $resolvedSshKeyPath, $backendTar, $frontendTar, $imageEnvFile, "${remoteTarget}:$RemoteDir/images/")
Invoke-NativeCommand scp @('-i', $resolvedSshKeyPath, $integrityEnvFile, "${remoteTarget}:$RemoteDir/scripts/stys-integrity.env")

$remoteEnvUpdate = @'
set -eu
cd '__REMOTE_DIR__'
if [ -f .env ]; then
    tmp=$(mktemp)
    awk -v tag='__TAG__' 'BEGIN { found = 0 } /^STYS_IMAGE_TAG=/ { print "STYS_IMAGE_TAG=" tag; found = 1; next } { print } END { if (!found) print "STYS_IMAGE_TAG=" tag }' .env > "$tmp"
    mv "$tmp" .env
else
    printf 'STYS_IMAGE_TAG=%s\n' '__TAG__' > .env
fi
'@
$remoteEnvUpdate = $remoteEnvUpdate.Replace('__REMOTE_DIR__', $RemoteDir).Replace('__TAG__', $FullTag)
Invoke-NativeCommand ssh @('-i', $resolvedSshKeyPath, $remoteTarget, $remoteEnvUpdate)

Write-Host ""
Write-Host "Kopyalama tamamlandi:"
Write-Host " - ${remoteTarget}:$RemoteDir/docker-compose.yml"
Write-Host " - ${remoteTarget}:$RemoteDir/.env (STYS_IMAGE_TAG=$FullTag olarak guncellendi)"
Write-Host " - ${remoteTarget}:$RemoteDir/images/backend.tar"
Write-Host " - ${remoteTarget}:$RemoteDir/images/frontend.tar"
Write-Host " - ${remoteTarget}:$RemoteDir/images/stys-image.env"
Write-Host " - ${remoteTarget}:$RemoteDir/scripts/stys-integrity.env"
