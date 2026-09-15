# PowerSave Studio — one-shot setup.exe builder (run on Windows)
# ---------------------------------------------------------------
# Publishes the app (self-contained single file, no .NET runtime needed)
# and compiles it into installer\output\PowerSave-Setup.exe with Inno Setup.
#
# Usage:
#   .\installer\build-installer.ps1                # build setup.exe
#   .\installer\build-installer.ps1 -FrameworkDependent   # smaller payload, needs .NET 8 Desktop Runtime
#
# Requirements:
#   - .NET 8 SDK            (https://dotnet.microsoft.com/download/dotnet/8.0)
#   - Inno Setup 6.3+       (https://jrsoftware.org/isinfo.php)  <- default install path is auto-detected

param(
  [switch]$FrameworkDependent,
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "PowerSave.csproj"
$publishDir = Join-Path $PSScriptRoot "publish"
$outputDir = Join-Path $PSScriptRoot "output"

if (-not (Test-Path $project)) { throw "PowerSave.csproj not found at $project" }

# ---- locate Inno Setup --------------------------------------------------
$iscc = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) {
  throw "Inno Setup 6 not found. Install it from https://jrsoftware.org/isdl.php (or add ISCC.exe to PATH and set -IsccPath)."
}

# ---- publish ------------------------------------------------------------
$selfContained = if ($FrameworkDependent) { "false" } else { "true" }
Write-Host "Publishing PowerSave Studio (selfContained=$selfContained) -> $publishDir" -ForegroundColor Cyan

dotnet publish $project `
  -c $Configuration -r win-x64 `
  --self-contained $selfContained `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=none `
  -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit $LASTEXITCODE" }

$exe = Join-Path $publishDir "PowerSave.exe"
if (-not (Test-Path $exe)) { throw "Publish finished but PowerSave.exe was not found." }
$mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Published PowerSave.exe ($mb MB)" -ForegroundColor Green

# ---- compile installer --------------------------------------------------
Write-Host "Compiling Inno Setup installer -> $outputDir" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$payloadRel = "publish"
& $iscc (Join-Path $PSScriptRoot "PowerSave.iss") "/DPayloadDir=$payloadRel"
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit $LASTEXITCODE" }

$setup = Join-Path $outputDir "PowerSave-Setup.exe"
if (Test-Path $setup) {
  $smb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
  Write-Host "OK: $setup ($smb MB)" -ForegroundColor Green
  if ($smb -lt 0.5) { Write-Warning "setup.exe suspiciously small — did the publish output land in installer\$payloadRel ?" }
} else {
  throw "ISCC finished but PowerSave-Setup.exe was not found in $outputDir"
}
