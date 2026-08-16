[CmdletBinding()]
param(
    [string]$PackageRoot = $PSScriptRoot,
    [string]$AgentInstallDir = (Join-Path $env:ProgramFiles "STYS\Agent"),
    [string]$UpdaterInstallDir = (Join-Path $env:ProgramFiles "STYS\Agent Updater"),
    [string]$SharedDataDir = (Join-Path $env:ProgramData "STYS\Agent"),
    [string]$UpdaterPrivateDataDir = (Join-Path $env:ProgramData "STYS\AgentUpdater\private"),
    [string]$LogDir = (Join-Path $env:ProgramData "STYS\Agent\logs"),
    [string]$ReleasePublicKeyPath = (Join-Path $env:ProgramData "STYS\AgentTrust\release-public-key.pem"),
    [int]$LocalUiPort = 5180
)

$ErrorActionPreference = 'Stop'

function Ensure-Directory {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-Host "[$Name] başlıyor..."
    & $Action
    Write-Host "[$Name] tamamlandı."
}

function Wait-ForLocalUi {
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $uri = "http://127.0.0.1:$Port/api/bootstrap/dashboard"

    while ((Get-Date) -lt $deadline) {
        try {
            Invoke-WebRequest -Uri $uri -TimeoutSec 5 | Out-Null
            return
        } catch {
            Start-Sleep -Seconds 2
        }
    }

    throw "Yerel Agent UI zamanında hazır olmadı: $uri"
}

function Read-SecureEnrollmentCode {
    $secure = Read-Host -AsSecureString -Prompt "Enrollment Code"
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        if ($bstr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
}

function Invoke-Enrollment {
    param(
        [Parameter(Mandatory = $true)][object]$Bootstrap,
        [Parameter(Mandatory = $true)][string]$EnrollmentCode
    )

    $body = @{
        StysBaseUrl = $Bootstrap.StysBaseUrl
        AgentDisplayName = $Bootstrap.AgentDisplayName
        EnrollmentCode = $EnrollmentCode
        HttpTimeoutSeconds = $Bootstrap.HttpTimeoutSeconds
        LocalUiPort = $Bootstrap.LocalUiPort
        Capabilities = @()
    }

    $json = $body | ConvertTo-Json -Depth 6
    Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$($Bootstrap.LocalUiPort)/api/bootstrap/enroll" -ContentType 'application/json' -Body $json | Out-Null
}

$PackageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
$BootstrapPath = Join-Path $PackageRoot "config\bootstrap.json"
$PackageTrustKey = Join-Path $PackageRoot "trust\release-public-key.pem"
$AgentPublishDir = Join-Path $PackageRoot "agent"
$UpdaterPublishDir = Join-Path $PackageRoot "updater"
$InstallAgentScript = Join-Path $PackageRoot "scripts\install-agent.ps1"
$InstallUpdaterScript = Join-Path $PackageRoot "scripts\install-agent-updater.ps1"

if (-not (Test-Path -LiteralPath $InstallAgentScript)) {
    $InstallAgentScript = Join-Path $PackageRoot "scripts\agent\install-agent.ps1"
}

if (-not (Test-Path -LiteralPath $InstallUpdaterScript)) {
    $InstallUpdaterScript = Join-Path $PackageRoot "scripts\agent\install-agent-updater.ps1"
}

foreach ($required in @($BootstrapPath, $PackageTrustKey, $AgentPublishDir, $UpdaterPublishDir, $InstallAgentScript, $InstallUpdaterScript)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Gerekli paket bileşeni bulunamadı: $required"
    }
}

$bootstrap = Get-Content -LiteralPath $BootstrapPath -Raw | ConvertFrom-Json
Ensure-Directory -Path $SharedDataDir
Ensure-Directory -Path $UpdaterPrivateDataDir
Ensure-Directory -Path $LogDir
Ensure-Directory -Path (Split-Path -Path $ReleasePublicKeyPath -Parent)

Copy-Item -LiteralPath $BootstrapPath -Destination (Join-Path $SharedDataDir "bootstrap.json") -Force
Copy-Item -LiteralPath $PackageTrustKey -Destination $ReleasePublicKeyPath -Force

Invoke-Step -Name "Agent kurulumu" -Action {
    & $InstallAgentScript `
        -PublishDir $AgentPublishDir `
        -InstallDir $AgentInstallDir `
        -SharedDataDir $SharedDataDir `
        -UpdaterPrivateDataDir $UpdaterPrivateDataDir `
        -LogDir $LogDir `
        -ReleasePublicKeyPath $ReleasePublicKeyPath `
        -LocalUiPort $LocalUiPort
}

Invoke-Step -Name "Updater kurulumu" -Action {
    & $InstallUpdaterScript `
        -PublishDir $UpdaterPublishDir `
        -UpdaterInstallDir $UpdaterInstallDir `
        -AgentInstallDir $AgentInstallDir `
        -SharedDataDir $SharedDataDir `
        -UpdaterPrivateDataDir $UpdaterPrivateDataDir `
        -LogDir $LogDir `
        -ReleasePublicKeyPath $ReleasePublicKeyPath `
        -LocalUiPort $LocalUiPort
}

Invoke-Step -Name "Yerel UI bekleme" -Action {
    Wait-ForLocalUi -Port $bootstrap.LocalUiPort -TimeoutSeconds 60
}

$enrollmentCode = Read-SecureEnrollmentCode
try {
    Invoke-Step -Name "Enrollment" -Action {
        Invoke-Enrollment -Bootstrap $bootstrap -EnrollmentCode $enrollmentCode
    }
} finally {
    if ($enrollmentCode) {
        $enrollmentCode = $null
    }
}

Write-Host ""
Write-Host "STYS Agent unified installation completed."
Write-Host "AgentInstallDir: $AgentInstallDir"
Write-Host "UpdaterInstallDir: $UpdaterInstallDir"
Write-Host "SharedDataDir: $SharedDataDir"
Write-Host "LogDir: $LogDir"
Write-Host "TrustAnchor: $ReleasePublicKeyPath"
Write-Host "LocalUiPort: $LocalUiPort"
