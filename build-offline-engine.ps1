$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Runtime = Join-Path $Root "Runtime"
$Models = Join-Path $Runtime ".u2net"

if (Test-Path $Runtime) { Remove-Item $Runtime -Recurse -Force }
New-Item $Runtime -ItemType Directory -Force | Out-Null

Write-Host "Ermittle lokales Python..." -ForegroundColor Cyan
$py = py -3 -c "import sys; print(sys.executable)"
if (!$py) { throw "Python 3 wurde nicht gefunden." }
$py = $py.Trim()

Write-Host "Kopiere Python-Laufzeit..." -ForegroundColor Cyan
$prefix = & $py -c "import sys; print(sys.prefix)"
Copy-Item (Join-Path $prefix "*") $Runtime -Recurse -Force

$runtimePy = Join-Path $Runtime "python.exe"
if (!(Test-Path $runtimePy)) { throw "python.exe wurde im Runtime-Ordner nicht gefunden." }

Write-Host "Installiere Engine-Pakete in die gebündelte Runtime..." -ForegroundColor Cyan
& $runtimePy -m pip install --upgrade pip
& $runtimePy -m pip install rembg onnxruntime pillow numpy
if ($LASTEXITCODE -ne 0) { throw "Paketinstallation fehlgeschlagen." }

New-Item $Models -ItemType Directory -Force | Out-Null
$env:U2NET_HOME = $Models

Write-Host "Lade benötigte Modelle vor..." -ForegroundColor Cyan
& $runtimePy -c "from rembg import new_session; new_session('u2net'); new_session('u2netp'); print('Modelle OK')"
if ($LASTEXITCODE -ne 0) { throw "Modelldownload fehlgeschlagen." }

Write-Host ""
Write-Host "Offline-Engine erstellt:" -ForegroundColor Green
Write-Host $Runtime
