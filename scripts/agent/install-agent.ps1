[CmdletBinding()]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\..\artifacts\agent\win-x64"),
    [string]$InstallDir = (Join-Path $env:ProgramFiles "STYS\Agent"),
    [string]$ServiceName = "STYS Agent",
    [string]$ServiceDisplayName = "STYS Agent",
    [string]$ServiceAccount = "NT AUTHORITY\LocalService",
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

function Grant-DirectoryAccess {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Identity,
        [Parameter(Mandatory = $true)][string]$Rights
    )
    & icacls $Path /grant "$Identity:($Rights)" /T /C | Out-Null
}

$publishRoot = (Resolve-Path -LiteralPath $PublishDir).Path
Ensure-Directory -Path $InstallDir
Ensure-Directory -Path $SharedDataDir
Ensure-Directory -Path $UpdaterPrivateDataDir
Ensure-Directory -Path $LogDir
$releaseTrustDir = Split-Path -Path $ReleasePublicKeyPath -Parent
Ensure-Directory -Path $releaseTrustDir
if (-not (Test-Path -LiteralPath $ReleasePublicKeyPath)) {
    throw "ReleasePublicKeyPath not found: $ReleasePublicKeyPath"
}

Copy-Item -Path (Join-Path $publishRoot '*') -Destination $InstallDir -Recurse -Force

Grant-DirectoryAccess -Path $InstallDir -Identity $ServiceAccount -Rights 'RX'
Grant-DirectoryAccess -Path $SharedDataDir -Identity $ServiceAccount -Rights 'M'
Grant-DirectoryAccess -Path $SharedDataDir -Identity 'SYSTEM' -Rights 'M'
Grant-DirectoryAccess -Path $UpdaterPrivateDataDir -Identity 'SYSTEM' -Rights 'M'
Grant-DirectoryAccess -Path $LogDir -Identity $ServiceAccount -Rights 'M'

& icacls $UpdaterPrivateDataDir /inheritance:r /grant:r "SYSTEM:(OI)(CI)(F)" "BUILTIN\Administrators:(OI)(CI)(F)" /C | Out-Null
& icacls $releaseTrustDir /inheritance:r /grant:r "SYSTEM:(OI)(CI)(F)" "BUILTIN\Administrators:(OI)(CI)(F)" "$ServiceAccount:(OI)(CI)(RX)" /C | Out-Null
& icacls $ReleasePublicKeyPath /inheritance:r /grant:r "SYSTEM:F" "BUILTIN\Administrators:F" "$ServiceAccount:R" /C | Out-Null

$serviceExe = Join-Path $InstallDir "STYS.Agent.exe"
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
sc.exe description $ServiceName "STYS Agent local management service." | Out-Null

$serviceKeyPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
if (Test-Path -LiteralPath $serviceKeyPath) {
    New-ItemProperty -Path $serviceKeyPath -Name "Environment" -PropertyType MultiString -Force -Value @(
        "STYS_AGENT_SHARED_DATA_DIR=$SharedDataDir"
        "STYS_AGENT_DATA_DIR=$SharedDataDir"
        "STYS_AGENT_UPDATER_PRIVATE_DATA_DIR=$UpdaterPrivateDataDir"
        "STYS_AGENT_LOG_DIR=$LogDir"
        "STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH=$ReleasePublicKeyPath"
        "STYS_AGENT_LOCAL_UI_PORT=$LocalUiPort"
    ) | Out-Null
}

Write-Host "STYS Agent service installed."
Write-Host "InstallDir: $InstallDir"
Write-Host "SharedDataDir: $SharedDataDir"
Write-Host "UpdaterPrivateDataDir: $UpdaterPrivateDataDir"
Write-Host "LogDir: $LogDir"
Write-Host "ReleasePublicKeyPath: $ReleasePublicKeyPath"
Write-Host "LocalUiPort: $LocalUiPort"
