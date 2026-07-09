param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src/Jellyfin.Plugin.StreamingSources/Jellyfin.Plugin.StreamingSources.csproj"
$output = Join-Path $root "artifacts"
$packageRoot = Join-Path $output "StreamingSources"
$zip = Join-Path $output "Jellyfin.Plugin.StreamingSources-$Version.zip"

dotnet build $project -c $Configuration

Remove-Item -LiteralPath $packageRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

$buildOutput = Join-Path $root "src/Jellyfin.Plugin.StreamingSources/bin/$Configuration/net9.0"
Copy-Item (Join-Path $buildOutput "Jellyfin.Plugin.StreamingSources.dll") $packageRoot -Force
Copy-Item (Join-Path $buildOutput "Jellyfin.Plugin.StreamingSources.pdb") $packageRoot -Force
Copy-Item (Join-Path $buildOutput "Jellyfin.Plugin.StreamingSources.deps.json") $packageRoot -Force
Copy-Item (Join-Path $buildOutput "Jellyfin.Plugin.StreamingSources.xml") $packageRoot -Force

Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zip -Force

$md5 = (Get-FileHash -Algorithm MD5 -LiteralPath $zip).Hash.ToLowerInvariant()
$sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant()

[pscustomobject]@{
    Version = $Version
    Package = $zip
    JellyfinChecksumMd5 = $md5
    Sha256 = $sha256
} | ConvertTo-Json
