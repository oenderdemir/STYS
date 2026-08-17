[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputDirectory,
    [switch]$IncludeSymbols
)

$ErrorActionPreference = 'Stop'

# Only win-x64 is publishable in this phase; the backend rejects anything else at publish time.
if ($RuntimeIdentifier -ne 'win-x64') {
    throw "Bu asamada yalnizca win-x64 destekleniyor. Verilen: $RuntimeIdentifier"
}

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Surum semantik versiyon olmali (or. 1.2.3 veya 1.2.3-beta.1). Verilen: $Version"
}

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")
$projectPath = Join-Path $repoRoot "agent\STYS.Agent\STYS.Agent.csproj"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Agent projesi bulunamadi: $projectPath"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\agent-releases"
}

$publishDir = Join-Path $repoRoot "artifacts\agent-release-publish\$RuntimeIdentifier"
$zipPath = Join-Path $OutputDirectory "stys-agent-$Version-$RuntimeIdentifier.zip"

Write-Host "[Release build] $Version / $RuntimeIdentifier"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

# Matches the deployment model already used for the unified installer artifacts:
# self-contained single file, so the target machine needs no shared runtime.
#
# IncludeSourceRevisionInInformationalVersion=false is load bearing: inside a git repository the SDK
# otherwise appends "+<commit-sha>" to AssemblyInformationalVersion, which is what AgentVersionInfo
# reports at runtime. The updater's health probe compares that against the release version, so an
# appended SHA made a healthy upgrade look like the wrong build and rolled it back.
# AssemblyVersion/FileVersion stay numeric-only because Windows requires it; prerelease labels
# survive on Version/InformationalVersion.
$numericVersion = ($Version -split '-')[0] + '.0'

Write-Host "[Release build] dotnet publish calisiyor..."
& dotnet publish $projectPath `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:Version=$Version `
    /p:InformationalVersion=$Version `
    /p:IncludeSourceRevisionInInformationalVersion=false `
    /p:AssemblyVersion=$numericVersion `
    /p:FileVersion=$numericVersion `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish basarisiz oldu (exit $LASTEXITCODE)."
}

# bootstrap.json is written per machine by the installer and must never travel in a release.
# Symbols are excluded by default: remote agents download this package over the network.
$excluded = @('bootstrap.json')
if (-not $IncludeSymbols) {
    $excluded += '*.pdb'
}

foreach ($pattern in $excluded) {
    Get-ChildItem -LiteralPath $publishDir -Filter $pattern -Recurse -File -ErrorAction SilentlyContinue |
        ForEach-Object {
            Write-Host "[Release build] paket disi birakildi: $($_.Name)"
            Remove-Item -LiteralPath $_.FullName -Force
        }
}

$agentExe = Join-Path $publishDir "STYS.Agent.exe"
if (-not (Test-Path -LiteralPath $agentExe)) {
    throw "Publish ciktisinda STYS.Agent.exe bulunamadi: $agentExe"
}

# The binary reports this value to the updater's health probe, so a mismatch here would surface much
# later as a rolled-back upgrade. Fail at build time instead.
$productVersion = (Get-Item -LiteralPath $agentExe).VersionInfo.ProductVersion
if ($productVersion -ne $Version) {
    throw "Publish edilen product version istenen surumle eslesmiyor. Istenen: '$Version', uretilen: '$productVersion'. IncludeSourceRevisionInInformationalVersion ayarini kontrol edin."
}

Write-Host "[Release build] product version dogrulandi: $productVersion"

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

# The updater extracts this archive straight into the install directory, so its entries must sit at
# the archive root with no wrapper folder.
Write-Host "[Release build] ZIP olusturuluyor..."
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

$zipItem = Get-Item -LiteralPath $zipPath
$sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash

Write-Host ""
Write-Host "[Release build] tamamlandi."
Write-Host "  Paket   : $($zipItem.FullName)"
Write-Host "  Boyut   : $($zipItem.Length) bayt"
Write-Host "  SHA-256 : $sha256"
Write-Host ""
Write-Host "  Bu paketi STYS arayuzunden Agent Yonetimi > Agent Surumleri altinda yayinlayin."
Write-Host "  SHA-256 ve imza sunucu tarafinda uretilir; bu deger yalnizca dogrulama icindir."
