# Installeren – TL;DR

## Met de installer (aanbevolen)

1. Bouw de installer, of pak een `BuurmansRgb-setup.exe` die je eerder bouwde:

   ```powershell
   git clone https://github.com/FiekBuurman/BuurmansRgb.git
   cd BuurmansRgb
   .\installer\build.ps1
   ```

   Nodig: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) en Git.
   De installer staat daarna in `artifacts\installer\out\`.
2. Sluit fabrikantsoftware af (iCUE, Armoury Crate, Mystic Light, Synapse, G Hub, ...).
3. Draai `BuurmansRgb-setup.exe` en geef één keer toestemming (UAC). SmartScreen kan
   waarschuwen omdat de installer niet ondertekend is: **Meer info → Toch uitvoeren**.

De installer zet in één keer neer:

- **BuurmansRgb** in `C:\Program Files\BuurmansRgb`, met alles erin (geen losse .NET nodig);
- de driver **PawnIO**, voor moederbord, RAM en videokaart;
- **OpenRGB 1.1** als Windows-service, met de SDK-server op `127.0.0.1:6742`.

Na afloop start BuurmansRgb met een tray-icoon rechtsonder bij de klok, en staat
**Starten met Windows** aan. Dubbelklik op het icoon opent de instellingen. Vraagt de
installer om een herstart, dan heeft PawnIO die nodig.

Echte apparaten werken? Zet dan `"simulatedDevices": false` in
`%AppData%\BuurmansRgb\settings.json` en start de engine opnieuw (tray → **Afsluiten**,
daarna BuurmansRgb uit het Startmenu).

## Bijwerken

`git pull`, `.\installer\build.ps1` en de nieuwe `BuurmansRgb-setup.exe` draaien. De installer
stopt de draaiende engine zelf. Een OpenRGB-service die er al staat, laat hij met rust.

## Verwijderen

Via **Instellingen → Apps → BuurmansRgb → Verwijderen**. Dat haalt BuurmansRgb en de autostart
weg, en vraagt of OpenRGB, PawnIO en je instellingen ook weg moeten.

## Zonder installer (voor ontwikkelaars)

Zonder adminrechten, met OpenRGB zelf geïnstalleerd en de SDK-server aan (tabblad
**SDK Server** → **Start Server**, OpenRGB als administrator voor moederbord, RAM en GPU):

```powershell
$dest = "$env:LOCALAPPDATA\Programs\BuurmansRgb"
dotnet publish src/BuurmansRgb.Engine -c Release -o $dest
dotnet publish src/BuurmansRgb.UI -c Release -o $dest
& "$dest\BuurmansRgb.Engine.exe"
```

Meer uitleg over effecten, de per-LED-editor en per apparaattype staat in de
[README](../README.md); hoe de installer werkt in [openrgb-meeleveren.md](openrgb-meeleveren.md).
