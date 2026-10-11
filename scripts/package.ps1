param([string]$Output = 'dist')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$localSdk = Join-Path $root '.build/dotnet/dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet).Source }
$publish = Join-Path $root "$Output/MetaRemover"
& $sdk publish 'src/MetaRemover.App' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath 'README.md','LICENSE','THIRD-PARTY-NOTICES.md','CHANGELOG.md' -Destination $publish
$publicDocs = Join-Path $publish 'docs'
New-Item -ItemType Directory -Path $publicDocs -Force | Out-Null
Copy-Item -LiteralPath 'docs/verification.md','docs/camera-catalog-sources.md' -Destination $publicDocs
Copy-Item -LiteralPath 'docs/images' -Destination $publicDocs -Recurse -Force
$licenses = Join-Path $publish 'licenses/dotnet'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path (Split-Path $sdk -Parent) 'LICENSE.txt'),(Join-Path (Split-Path $sdk -Parent) 'ThirdPartyNotices.txt') -Destination $licenses
& (Join-Path $publish 'tools/ffmpeg/ffmpeg.exe') -version 2>&1 | Set-Content -LiteralPath (Join-Path $publish 'ffmpeg-version.txt') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Bundled FFmpeg does not run' }
$sourceStage = Join-Path $root ('.build/app-source-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sourceStage -Force | Out-Null
foreach ($component in @('src','tests','scripts','docs')) {
    Get-ChildItem -LiteralPath (Join-Path $root $component) -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|superpowers)[\\/]|[\\/]progress\.md$' } |
        ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($root, $_.FullName)
            $destination = Join-Path $sourceStage $relative
            New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $destination
        }
}
Copy-Item -LiteralPath 'LICENSE','README.md','THIRD-PARTY-NOTICES.md','CHANGELOG.md' -Destination $sourceStage
Compress-Archive -Path "$sourceStage/*" -DestinationPath (Join-Path $publish 'MetaRemover-source.zip') -Force
$zip = Join-Path $root "$Output/MetaRemover-Windows-x64.zip"
Compress-Archive -Path "$publish/*" -DestinationPath $zip -Force
Get-Item -LiteralPath $zip | Select-Object FullName,Length
