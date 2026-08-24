param(
  [switch]$SelfContained,
  [switch]$Run,
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "PowerSave.csproj"

if (-not (Test-Path $project)) { throw "PowerSave.csproj not found at $project" }

# Verify SDK
try { dotnet --list-sdks | Out-Null } catch { throw ".NET SDK not found. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0" }

$selfContainedFlag = if ($SelfContained) { "true" } else { "false" }
$outDir = if ($SelfContained) { Join-Path $PSScriptRoot "publish selfcontained" } else { Join-Path $PSScriptRoot "publish" }

Write-Host "Building PowerSave Studio ($Configuration, selfContained=$selfContainedFlag) -> $outDir" -ForegroundColor Cyan

$argsList = @(
  "publish", $project,
  "-c", $Configuration,
  "-r", $Runtime,
  "--self-contained", $selfContainedFlag,
  "-p:PublishSingleFile=true",
  "-p:IncludeNativeLibrariesForSelfExtract=true",
  "-p:DebugType=none",
  "-p:GenerateDocumentationFile=false",
  "-o", $outDir
)
if ($SelfContained) { $argsList += "-p:EnableCompressionInSingleFile=true" }
& dotnet @argsList
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit $LASTEXITCODE" }

$exe = Join-Path $outDir "PowerSave.exe"
if (Test-Path $exe) {
  $size = (Get-Item $exe).Length
  $mb = [math]::Round($size / 1MB, 2)
  Write-Host "OK: $exe ($mb MB)" -ForegroundColor Green
} else { Write-Warning "Build finished but $exe not found (framework-dependent may be .dll only?)" }

if ($Run) {
  Write-Host "Launching..." -ForegroundColor Yellow
  Start-Process $exe
}
