param([string]$Compiler)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $Compiler) {
    $local = Join-Path $root '.build/InnoSetup/ISCC.exe'
    $Compiler = if (Test-Path -LiteralPath $local) { $local } else { (Get-Command ISCC.exe -ErrorAction Stop).Source }
}
if (-not (Test-Path -LiteralPath (Join-Path $root 'dist/MetaRemover/MetaRemover.exe'))) {
    throw 'Run scripts/package.ps1 first to publish the application.'
}
& $Compiler (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
Get-Item -LiteralPath (Join-Path $root 'dist/MetaRemover-Setup-x64.exe') | Select-Object FullName,Length
