param(
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src\LoveBehaviorTranslator.Function"
$outDir = Join-Path $root "dist"
$publishDir = Join-Path $outDir "publish"
$zipPath = Join-Path $outDir "function.zip"

if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

dotnet restore (Join-Path $root "LoveBehaviorTranslator.sln")
dotnet publish $src -c $Configuration -o $publishDir

if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host "Built Lambda zip: $zipPath"


