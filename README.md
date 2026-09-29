# CM FotoFreisteller

Moderne Windows-Anwendung zur automatischen Freistellung und Stapelbearbeitung von Fotos mit lokaler Offline-KI.

## Aktueller Stand

**V0.2.5 Buildfix 3** ist der aktuell bestätigte stabile Stand.

Bestätigt:
- Stapelverarbeitung von JPG/JPEG/PNG
- automatische Freistellung mit rembg/ONNX
- weißer, transparenter oder frei wählbarer Hintergrund
- visueller Farbpicker
- Ausgabegrößen von 256 × 256 bis 1600 × 1600 px sowie freie Größe
- Produktfoto-Modus mit Zentrierung und Rand
- Behandlung langer Kabel/Ausläufer
- Qualitätsmodi und Maskenbereinigung
- Bildliste und Vorher-/Nachher-Vorschau
- Entwickler- und Unterstützungsinformationen
- self-contained Windows-Build
- Full-Portable-Paket mit gebündelter Offline-Engine
- Betrieb ohne separate Python- oder .NET-Installation im Full-Portable-Paket

## Entwicklung

Voraussetzung für die Quellcode-Entwicklung ist das .NET 10 SDK.

```powershell
dotnet run --project .\CMFotoFreisteller\CMFotoFreisteller.csproj
```

### Offline-Engine bauen

```powershell
.\build-offline-engine.bat
```

### Full Portable bauen

```powershell
.\build-full-portable.bat
```

Generierte Laufzeiten, Modelle und Build-Artefakte werden nicht im Git-Repository versioniert.

## Entwickler

C. Mihalik

## Unterstützung

Die Anwendung enthält einen freiwilligen Unterstützungslink. Zahlungs- oder Zugangsdaten werden nicht in der Anwendung gespeichert.
