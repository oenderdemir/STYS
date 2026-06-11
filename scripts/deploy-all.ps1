param(
    [string]$VpsHost = "185.229.12.39",
    [string]$VpsUser = "root",
    [string]$SshKeyPath = "id_ed25519",
    [string]$RemoteDir = "/root/stys",
    [string]$ComposeFilePath = "docker-compose.yml",
    [string]$Tag = "latest"
)

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $scriptDirectory "push-images.ps1") `
    -VpsHost $VpsHost `
    -VpsUser $VpsUser `
    -SshKeyPath $SshKeyPath `
    -RemoteDir $RemoteDir `
    -ComposeFilePath $ComposeFilePath `
    -Tag $Tag

& (Join-Path $scriptDirectory "deploy-remote.ps1") `
    -VpsHost $VpsHost `
    -VpsUser $VpsUser `
    -SshKeyPath $SshKeyPath `
    -RemoteDir $RemoteDir `
    -Tag $Tag
