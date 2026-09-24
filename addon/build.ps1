# Publish the self-contained sforge.exe, stage it into the extension, and zip it.
# Usage:  pwsh -File addon/build.ps1
$ErrorActionPreference = "Stop"

$addonDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $addonDir
$cliProj  = Join-Path $repoRoot "src\ShadowForge.Cli\ShadowForge.Cli.csproj"
$pkgDir   = Join-Path $addonDir "io_shadowforge"
$binDir   = Join-Path $pkgDir "bin"
$distDir  = Join-Path $addonDir "dist"
$pubTmp   = Join-Path $distDir "publish"

New-Item -ItemType Directory -Force -Path $binDir  | Out-Null
New-Item -ItemType Directory -Force -Path $pubTmp  | Out-Null

Write-Host "Publishing sforge.exe (bundle profile)..."
dotnet publish $cliProj -p:PublishProfile=bundle -o $pubTmp
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$exe = Join-Path $pubTmp "sforge.exe"
if (-not (Test-Path $exe)) { throw "sforge.exe not found in $pubTmp" }
Copy-Item $exe (Join-Path $binDir "sforge.exe") -Force
Write-Host "Staged sforge.exe -> $binDir"

$zipPath = Join-Path $distDir "io_shadowforge.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
$staging = Join-Path $distDir "stage"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Copy-Item $pkgDir (Join-Path $staging "io_shadowforge") -Recurse
Get-ChildItem $staging -Recurse -Directory -Filter "__pycache__" |
    Remove-Item -Recurse -Force
Compress-Archive -Path (Join-Path $staging "io_shadowforge") -DestinationPath $zipPath -Force
Remove-Item $staging -Recurse -Force
Write-Host "Wrote extension zip -> $zipPath"
