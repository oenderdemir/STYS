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

function Read-DotEnvFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path
    )

    $values = [ordered]@{}
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf('=')
        if ($separatorIndex -lt 1) {
            continue
        }

        $name = $trimmed.Substring(0, $separatorIndex).Trim()
        $value = $trimmed.Substring($separatorIndex + 1)
        $values[$name] = $value
    }

    return $values
}

if (-not (Test-Path -LiteralPath $ComposeFilePath)) {
    throw "Compose dosyasi bulunamadi: $ComposeFilePath"
}

$envFilePath = Join-Path $projectRoot ".env"
if (-not (Test-Path -LiteralPath $envFilePath)) {
    throw ".env dosyasi bulunamadi: $envFilePath"
}

$envExampleFilePath = Join-Path $projectRoot ".env.example"
if (-not (Test-Path -LiteralPath $envExampleFilePath)) {
    throw ".env.example dosyasi bulunamadi: $envExampleFilePath"
}

$env:STYS_IMAGE_TAG = $Tag
$composeConfig = Get-ComposeConfig
$backendImageReference = $composeConfig.services.backend.image
$frontendImageReference = $composeConfig.services.frontend.image
$backendImageInfo = Split-ImageReference $backendImageReference
$frontendImageInfo = Split-ImageReference $frontendImageReference

if ($backendImageInfo.Tag -ne $frontendImageInfo.Tag) {
    throw "Backend ve frontend image tag'leri farkli: $($backendImageInfo.Tag) / $($frontendImageInfo.Tag)"
}

$Tag = $backendImageInfo.Tag
$artifactDir = Join-Path $projectRoot "artifacts\deploy\$Tag"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$backendTar = Join-Path $artifactDir "backend.tar"
$frontendTar = Join-Path $artifactDir "frontend.tar"
$imageEnvFile = Join-Path $artifactDir "stys-image.env"
$mergedEnvFile = Join-Path $artifactDir ".env"
$resolvedSshKeyPath = $SshKeyPath
if (-not [System.IO.Path]::IsPathRooted($resolvedSshKeyPath)) {
    $candidateKeyPath = Join-Path $projectRoot $resolvedSshKeyPath
    if (Test-Path -LiteralPath $candidateKeyPath) {
        $resolvedSshKeyPath = $candidateKeyPath
    }
}

Write-Host "Image build basliyor..."
Invoke-NativeCommand docker @('compose', 'build', 'backend', 'frontend')

Write-Host "Image archive olusturuluyor..."
Invoke-NativeCommand docker @('save', '-o', $backendTar, $backendImageReference)
Invoke-NativeCommand docker @('save', '-o', $frontendTar, $frontendImageReference)

$mergedEnvValues = Read-DotEnvFile -Path $envExampleFilePath
$localEnvValues = Read-DotEnvFile -Path $envFilePath
foreach ($entry in $localEnvValues.GetEnumerator()) {
    $mergedEnvValues[$entry.Key] = $entry.Value
}

$mergedEnvContent = New-Object System.Collections.Generic.List[string]
foreach ($line in [System.IO.File]::ReadAllLines($envExampleFilePath)) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
        $mergedEnvContent.Add($line)
        continue
    }

    $separatorIndex = $trimmed.IndexOf('=')
    if ($separatorIndex -lt 1) {
        $mergedEnvContent.Add($line)
        continue
    }

    $name = $trimmed.Substring(0, $separatorIndex).Trim()
    if ($mergedEnvValues.Contains($name)) {
        $mergedEnvContent.Add("$name=$($mergedEnvValues[$name])")
        continue
    }

    $mergedEnvContent.Add($line)
}

[System.IO.File]::WriteAllText($mergedEnvFile, ($mergedEnvContent -join "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))

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
Write-Host " - $mergedEnvFile"

Write-Host "Compose dosyasi ve image archive'lari VPS'ye kopyalaniyor..."
$remoteTarget = "$VpsUser@$VpsHost"
Invoke-NativeCommand ssh @('-i', $resolvedSshKeyPath, $remoteTarget, "mkdir -p '$RemoteDir/images'")
Invoke-NativeCommand scp @('-i', $resolvedSshKeyPath, $ComposeFilePath, "${remoteTarget}:$RemoteDir/docker-compose.yml")
Invoke-NativeCommand scp @('-i', $resolvedSshKeyPath, $mergedEnvFile, "${remoteTarget}:$RemoteDir/.env")
Invoke-NativeCommand scp @('-i', $resolvedSshKeyPath, $backendTar, $frontendTar, $imageEnvFile, "${remoteTarget}:$RemoteDir/images/")

Write-Host ""
Write-Host "Kopyalama tamamlandi:"
Write-Host " - ${remoteTarget}:$RemoteDir/docker-compose.yml"
Write-Host " - ${remoteTarget}:$RemoteDir/.env"
Write-Host " - ${remoteTarget}:$RemoteDir/images/backend.tar"
Write-Host " - ${remoteTarget}:$RemoteDir/images/frontend.tar"
Write-Host " - ${remoteTarget}:$RemoteDir/images/stys-image.env"
