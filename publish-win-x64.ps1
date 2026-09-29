$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root "CMFotoFreisteller\CMFotoFreisteller.csproj"
$Out = Join-Path $Root "artifacts\win-x64"

Write-Host "CM FotoFreisteller - Publish win-x64" -ForegroundColor Cyan
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }

dotnet publish $Project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o $Out

if ($LASTEXITCODE -ne 0) { throw "Publish fehlgeschlagen." }

Write-Host ""
Write-Host "Fertig:" -ForegroundColor Green
Write-Host $Out
Get-ChildItem $Out | Select-Object Name,Length
