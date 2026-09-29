$ErrorActionPreference = "Stop"
$exe = Join-Path $PSScriptRoot "CMFotoFreisteller.exe"
if (!(Test-Path $exe)) { throw "CMFotoFreisteller.exe wurde neben diesem Skript nicht gefunden." }

$menu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcut = Join-Path $menu "CM FotoFreisteller.lnk"
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($shortcut)
$sc.TargetPath = $exe
$sc.WorkingDirectory = $PSScriptRoot
$sc.Description = "CM FotoFreisteller"
$sc.Save()

# Folder context menu, current user, no admin rights.
$key = "HKCU:\Software\Classes\Directory\shell\CMFotoFreisteller"
New-Item $key -Force | Out-Null
Set-ItemProperty $key -Name "(default)" -Value "Mit CM FotoFreisteller öffnen"
New-Item "$key\command" -Force | Out-Null
Set-ItemProperty "$key\command" -Name "(default)" -Value ('"' + $exe + '" "%1"')

Write-Host "Startmenü und Explorer-Integration wurden eingerichtet." -ForegroundColor Green
