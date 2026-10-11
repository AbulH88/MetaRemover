param([string]$PackageDirectory = 'dist/MetaRemover')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$package = [IO.Path]::GetFullPath((Join-Path $root $PackageDirectory))
$exe = Join-Path $package 'MetaRemover.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Missing application executable' }
& (Join-Path $package 'tools/exiftool/ExifTool.exe') -ver
if ($LASTEXITCODE -ne 0) { throw 'Bundled ExifTool failed' }
& (Join-Path $package 'tools/ffmpeg/ffprobe.exe') -version
if ($LASTEXITCODE -ne 0) { throw 'Bundled FFprobe failed' }
$working = Join-Path $root ('.build/Packaged smoke ' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $working -Force | Out-Null
$task = Start-Process -FilePath $exe -ArgumentList @('--smoke-test', ('"' + $working + '"')) -WindowStyle Hidden -PassThru -Wait
if ($task.ExitCode -ne 0) { if (Test-Path -LiteralPath (Join-Path $working 'smoke-result.json')) { Get-Content -LiteralPath (Join-Path $working 'smoke-result.json') }; throw "Packaged app smoke test failed (exit $($task.ExitCode))" }
Get-Content -LiteralPath (Join-Path $working 'smoke-result.json')
