[CmdletBinding()]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\..\artifacts\agent-updater\win-x64"),
    [string]$UpdaterInstallDir = (Join-Path $env:ProgramFiles "STYS\Agent Updater"),
    [string]$AgentInstallDir = (Join-Path $env:ProgramFiles "STYS\Agent"),
    [string]$ServiceName = "STYS Agent Updater",
    [string]$ServiceDisplayName = "STYS Agent Updater",
    [string]$ServiceAccount = "LocalSystem",
    [string]$SharedDataDir = (Join-Path $env:ProgramData "STYS\Agent"),
    [string]$UpdaterPrivateDataDir = (Join-Path $env:ProgramData "STYS\AgentUpdater\private"),
    [string]$LogDir = (Join-Path $env:ProgramData "STYS\AgentUpdater\logs"),
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

function Grant-DirectoryAccess {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Identity,
        [Parameter(Mandatory = $true)][string]$Rights
    )
    # ${} delimits the name: "$Identity:(...)" makes the parser read $Identity: as a scope-qualified
    # variable reference, which is a hard parse error before the script can run at all.
    & icacls $Path /grant "${Identity}:($Rights)" /T /C | Out-Null
}

function Start-And-Wait-ServiceRunning {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    Start-Service -Name $Name -ErrorAction Stop
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $service = Get-Service -Name $Name -ErrorAction Stop
        if ($service.Status -eq 'Running') {
            Write-Host "[OK] $Name service running"
            return
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "$Name service Running durumuna geçmedi."
}

if ($LocalUiPort -lt 1 -or $LocalUiPort -gt 65535) {
    throw "LocalUiPort must be between 1 and 65535."
}

$publishRoot = (Resolve-Path -LiteralPath $PublishDir).Path
Ensure-Directory -Path $UpdaterInstallDir
Ensure-Directory -Path $SharedDataDir
Ensure-Directory -Path $UpdaterPrivateDataDir
Ensure-Directory -Path $LogDir
$releaseTrustDir = Split-Path -Path $ReleasePublicKeyPath -Parent
Ensure-Directory -Path $releaseTrustDir
if (-not (Test-Path -LiteralPath $ReleasePublicKeyPath)) {
    throw "ReleasePublicKeyPath not found: $ReleasePublicKeyPath"
}

Copy-Item -Path (Join-Path $publishRoot '*') -Destination $UpdaterInstallDir -Recurse -Force

Grant-DirectoryAccess -Path $UpdaterInstallDir -Identity 'SYSTEM' -Rights 'F'
Grant-DirectoryAccess -Path $SharedDataDir -Identity 'SYSTEM' -Rights 'M'
Grant-DirectoryAccess -Path $UpdaterPrivateDataDir -Identity 'SYSTEM' -Rights 'F'
Grant-DirectoryAccess -Path $LogDir -Identity 'SYSTEM' -Rights 'F'

& icacls $UpdaterPrivateDataDir /inheritance:r /grant:r "SYSTEM:(OI)(CI)(F)" "BUILTIN\Administrators:(OI)(CI)(F)" /C | Out-Null
& icacls $releaseTrustDir /inheritance:r /grant:r "SYSTEM:(OI)(CI)(F)" "BUILTIN\Administrators:(OI)(CI)(F)" "${ServiceAccount}:(OI)(CI)(R)" /C | Out-Null
# "$ServiceAccount:R" parses cleanly as scope "ServiceAccount" / variable "R" and expands to an
# empty string, so icacls silently received no ACE and the service account never got read access
# to the trust anchor. ${} is required here for correctness, not just to satisfy the parser.
& icacls $ReleasePublicKeyPath /inheritance:r /grant:r "SYSTEM:F" "BUILTIN\Administrators:F" "${ServiceAccount}:R" /C | Out-Null

$serviceExe = Join-Path $UpdaterInstallDir "STYS.Agent.Updater.exe"
$quotedServiceExe = '"' + $serviceExe + '"'

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $ServiceName binPath= $quotedServiceExe start= auto obj= $ServiceAccount DisplayName= $ServiceDisplayName | Out-Null
sc.exe config $ServiceName start= delayed-auto | Out-Null
sc.exe failure $ServiceName reset= 60 actions= restart/5000/restart/5000/restart/5000 | Out-Null
sc.exe failureflag $ServiceName 1 | Out-Null
sc.exe description $ServiceName "STYS Agent privileged updater service." | Out-Null

$serviceKeyPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
if (Test-Path -LiteralPath $serviceKeyPath) {
    New-ItemProperty -Path $serviceKeyPath -Name "Environment" -PropertyType MultiString -Force -Value @(
        "STYS_AGENT_UPDATER_INSTALL_DIR=$UpdaterInstallDir"
        "STYS_AGENT_INSTALL_DIR=$AgentInstallDir"
        "STYS_AGENT_SHARED_DATA_DIR=$SharedDataDir"
        "STYS_AGENT_DATA_DIR=$SharedDataDir"
        "STYS_AGENT_UPDATER_PRIVATE_DATA_DIR=$UpdaterPrivateDataDir"
        "STYS_AGENT_LOG_DIR=$LogDir"
        "STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH=$ReleasePublicKeyPath"
        "STYS_AGENT_LOCAL_UI_PORT=$LocalUiPort"
    ) | Out-Null
}

Start-And-Wait-ServiceRunning -Name $ServiceName -TimeoutSeconds 30

Write-Host "STYS Agent Updater service installed."
Write-Host "UpdaterInstallDir: $UpdaterInstallDir"
Write-Host "AgentInstallDir: $AgentInstallDir"
Write-Host "SharedDataDir: $SharedDataDir"
Write-Host "UpdaterPrivateDataDir: $UpdaterPrivateDataDir"
Write-Host "LogDir: $LogDir"
Write-Host "ReleasePublicKeyPath: $ReleasePublicKeyPath"
Write-Host "LocalUiPort: $LocalUiPort"
