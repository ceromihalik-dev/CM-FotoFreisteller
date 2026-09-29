$shortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\CM FotoFreisteller.lnk"
if (Test-Path $shortcut) { Remove-Item $shortcut -Force }
$key = "HKCU:\Software\Classes\Directory\shell\CMFotoFreisteller"
if (Test-Path $key) { Remove-Item $key -Recurse -Force }
Write-Host "Windows-Integration wurde entfernt." -ForegroundColor Green
