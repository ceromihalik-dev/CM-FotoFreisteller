$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root "CMFotoFreisteller\CMFotoFreisteller.csproj"
$Publish = Join-Path $Root "artifacts\portable\CM FotoFreisteller"
$Zip = Join-Path $Root "artifacts\CM-FotoFreisteller-0.2.4-win-x64-portable.zip"

if (Test-Path (Join-Path $Root "artifacts\portable")) {
    Remove-Item (Join-Path $Root "artifacts\portable") -Recurse -Force
}
New-Item $Publish -ItemType Directory -Force | Out-Null

Write-Host "Publish self-contained win-x64..." -ForegroundColor Cyan
dotnet publish $Project -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false -o $Publish
if ($LASTEXITCODE -ne 0) { throw "Publish fehlgeschlagen." }

Copy-Item (Join-Path $Root "Windows-Integration-installieren.ps1") $Publish
Copy-Item (Join-Path $Root "Windows-Integration-entfernen.ps1") $Publish

@"
CM FotoFreisteller 0.2.4

Start:
CMFotoFreisteller.exe

Dieser Build enthält die .NET-Laufzeit.
Die Bildengine wird beim ersten Bearbeitungslauf weiterhin automatisch eingerichtet.
Die vollständig gebündelte Offline-Engine folgt als nächste Distributionsstufe.
"@ | Set-Content (Join-Path $Publish "LIESMICH.txt") -Encoding UTF8

if (Test-Path $Zip) { Remove-Item $Zip -Force }
Compress-Archive -Path "$Publish\*" -DestinationPath $Zip -CompressionLevel Optimal

Write-Host ""
Write-Host "Portable Paket erstellt:" -ForegroundColor Green
Write-Host $Zip
