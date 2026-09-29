$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Runtime = Join-Path $Root "Runtime"

if (!(Test-Path (Join-Path $Runtime "python.exe"))) {
    Write-Host "Offline-Engine fehlt - wird jetzt erstellt..." -ForegroundColor Yellow
    & (Join-Path $Root "build-offline-engine.ps1")
}

$Project = Join-Path $Root "CMFotoFreisteller\CMFotoFreisteller.csproj"
$Publish = Join-Path $Root "artifacts\full-portable\CM FotoFreisteller"
$Zip = Join-Path $Root "artifacts\CM-FotoFreisteller-0.2.5-win-x64-full-portable.zip"

$PortableRoot = Split-Path $Publish -Parent
if (Test-Path $PortableRoot) {
    Write-Host "Bereinige alten Portable-Build..." -ForegroundColor Cyan
    # Remove-Item fails on deep Python/ONNX paths. Use cmd/rmdir which handles
    # this tree more robustly; if needed use the Win32 long-path prefix.
    $longPortableRoot = "\\?\$([System.IO.Path]::GetFullPath($PortableRoot))"
    cmd.exe /d /c "rmdir /s /q `"$longPortableRoot`""
    if (Test-Path $PortableRoot) {
        # Fallback: robocopy an empty directory over the target, then remove.
        $empty = Join-Path $Root ".empty-delete"
        New-Item $empty -ItemType Directory -Force | Out-Null
        robocopy $empty $PortableRoot /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        cmd.exe /d /c "rmdir /s /q `"$longPortableRoot`""
        Remove-Item $empty -Force -ErrorAction SilentlyContinue
    }
}
New-Item $Publish -ItemType Directory -Force | Out-Null

dotnet publish $Project -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false -o $Publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fehlgeschlagen." }

$RuntimeDest = Join-Path $Publish "Runtime"
New-Item $RuntimeDest -ItemType Directory -Force | Out-Null

Write-Host "Kopiere Offline-Engine..." -ForegroundColor Cyan
# robocopy is more robust than Copy-Item for very deep Python/onnxruntime paths.
# Exclude caches and compiled Python bytecode; they are not required at runtime.
robocopy $Runtime $RuntimeDest /E /NFL /NDL /NJH /NJS /NP /R:2 /W:1 /XD "__pycache__" /XF "*.pyc" | Out-Null
$rc = $LASTEXITCODE
if ($rc -gt 7) { throw "Kopieren der Offline-Engine fehlgeschlagen (robocopy ExitCode $rc)." }

Copy-Item (Join-Path $Root "Windows-Integration-installieren.ps1") $Publish
Copy-Item (Join-Path $Root "Windows-Integration-entfernen.ps1") $Publish

@"
CM FotoFreisteller 0.2.5 - Full Portable

Start:
CMFotoFreisteller.exe

Enthalten:
- self-contained .NET
- lokale Python-Laufzeit
- rembg
- ONNX Runtime
- Pillow / NumPy
- u2net / u2netp Modelle

Für die Bildverarbeitung ist keine separate Python-Installation vorgesehen.
"@ | Set-Content (Join-Path $Publish "LIESMICH.txt") -Encoding UTF8

if (Test-Path $Zip) { Remove-Item $Zip -Force }

Write-Host "Erzeuge ZIP-Paket..." -ForegroundColor Cyan
# Compress-Archive/.NET ZipArchive can fail on deep Python package paths.
# Use tar.exe (bsdtar shipped with modern Windows) from inside the publish folder;
# relative archive paths avoid the long absolute source path problem.
$ZipFull = [System.IO.Path]::GetFullPath($Zip)
Push-Location $Publish
try {
    & tar.exe -a -c -f $ZipFull *
    if ($LASTEXITCODE -ne 0) { throw "ZIP-Erstellung mit tar.exe fehlgeschlagen (ExitCode $LASTEXITCODE)." }
}
finally {
    Pop-Location
}

Write-Host "Full-Portable-Paket:" -ForegroundColor Green
Write-Host $ZipFull
