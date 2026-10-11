param([string]$ReleaseTag = 'v1.3.0')
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $root 'tools'
if ((Test-Path -LiteralPath (Join-Path $destination 'exiftool/ExifTool.exe')) -and
    (Test-Path -LiteralPath (Join-Path $destination 'ffmpeg/ffmpeg.exe')) -and
    (Test-Path -LiteralPath (Join-Path $destination 'ffmpeg/ffprobe.exe'))) {
    Write-Output 'Tool executables are already present. No files changed.'
    exit 0
}
if (Test-Path -LiteralPath $destination) { throw 'Partial tools folder exists. Preserve or rename it before bootstrapping.' }
if ($ReleaseTag -notmatch '^v\d+\.\d+\.\d+$') { throw 'Release tag format must be vMAJOR.MINOR.PATCH.' }
$stage = Join-Path $root ('.build/bootstrap-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$url = 'https://github.com/AbulH88/MetaRemover/releases/download/' + $ReleaseTag
$zip = Join-Path $stage 'MetaRemover-Windows-x64.zip'
$manifest = Join-Path $stage 'SHA256SUMS.txt'
Invoke-WebRequest -UseBasicParsing -TimeoutSec 120 -Uri "$url/MetaRemover-Windows-x64.zip" -OutFile $zip
Invoke-WebRequest -UseBasicParsing -TimeoutSec 120 -Uri "$url/SHA256SUMS.txt" -OutFile $manifest
$line = Get-Content -LiteralPath $manifest | Where-Object { $_ -match '^([A-Fa-f0-9]{64})\s+\*?MetaRemover-Windows-x64\.zip$' } | Select-Object -First 1
if (-not $line) { throw 'Portable archive checksum is missing from the release.' }
$expected = ($line -split '\s+')[0]
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $expected) { throw 'Portable archive checksum mismatch.' }
$expanded = Join-Path $stage 'expanded'
Expand-Archive -LiteralPath $zip -DestinationPath $expanded
$source = Join-Path $expanded 'tools'
if (-not (Test-Path -LiteralPath (Join-Path $source 'exiftool/ExifTool.exe')) -or
    -not (Test-Path -LiteralPath (Join-Path $source 'ffmpeg/ffprobe.exe'))) { throw 'Release does not contain expected tools.' }
Copy-Item -LiteralPath $source -Destination $destination -Recurse
Write-Output "Tools and bundled license/source materials copied from $ReleaseTag."
