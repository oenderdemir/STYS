[CmdletBinding()]
param(
    [string]$ServiceName = "STYS Agent Updater",
    [string]$InstallDir = (Join-Path $env:ProgramFiles "STYS\Agent Updater"),
    [string]$DataDir = (Join-Path $env:ProgramData "STYS\AgentUpdater\private"),
    [string]$LogDir = (Join-Path $env:ProgramData "STYS\AgentUpdater\logs"),
    [switch]$Purge
)

$ErrorActionPreference = 'Stop'

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
}

if (Test-Path -LiteralPath $InstallDir) {
    Remove-Item -LiteralPath $InstallDir -Recurse -Force
}

if ($Purge) {
    if (Test-Path -LiteralPath $DataDir) {
        Remove-Item -LiteralPath $DataDir -Recurse -Force
    }

    if (Test-Path -LiteralPath $LogDir) {
        Remove-Item -LiteralPath $LogDir -Recurse -Force
    }
}

Write-Host "STYS Agent Updater service removed."
if ($Purge) {
    Write-Host "Data and logs were purged."
} else {
    Write-Host "Data and logs were preserved."
}
