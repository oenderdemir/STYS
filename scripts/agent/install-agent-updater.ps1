[CmdletBinding()]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\..\artifacts\agent-updater\win-x64"),
    [string]$InstallDir = (Join-Path $env:ProgramFiles "STYS\Agent Updater"),
    [string]$ServiceName = "STYS Agent Updater",
    [string]$ServiceDisplayName = "STYS Agent Updater",
    [string]$ServiceAccount = "LocalSystem",
    [string]$DataDir = (Join-Path $env:ProgramData "STYS\Agent\Updater"),
    [string]$LogDir = (Join-Path $env:ProgramData "STYS\Agent\Updater\logs"),
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

if ($LocalUiPort -lt 1 -or $LocalUiPort -gt 65535) {
    throw "LocalUiPort must be between 1 and 65535."
}

$publishRoot = (Resolve-Path -LiteralPath $PublishDir).Path
Ensure-Directory -Path $InstallDir
Ensure-Directory -Path $DataDir
Ensure-Directory -Path $LogDir

Copy-Item -Path (Join-Path $publishRoot '*') -Destination $InstallDir -Recurse -Force

Grant-DirectoryAccess -Path $InstallDir -Identity 'SYSTEM' -Rights 'F'
Grant-DirectoryAccess -Path $DataDir -Identity 'SYSTEM' -Rights 'F'
Grant-DirectoryAccess -Path $LogDir -Identity 'SYSTEM' -Rights 'F'

$serviceExe = Join-Path $InstallDir "STYS.Agent.Updater.exe"
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
        "STYS_AGENT_INSTALL_DIR=$InstallDir"
        "STYS_AGENT_DATA_DIR=$DataDir"
        "STYS_AGENT_LOG_DIR=$LogDir"
        "STYS_AGENT_LOCAL_UI_PORT=$LocalUiPort"
    ) | Out-Null
}

Write-Host "STYS Agent Updater service installed."
Write-Host "InstallDir: $InstallDir"
Write-Host "DataDir: $DataDir"
Write-Host "LogDir: $LogDir"
Write-Host "LocalUiPort: $LocalUiPort"
