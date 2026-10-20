# Installeren – TL;DR

## Nodig

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) en Git
- [OpenRGB](https://openrgb.org) voor echte hardware

## Installeren

In PowerShell, zonder adminrechten:

```powershell
git clone https://github.com/FiekBuurman/BuurmansRgb.git
cd BuurmansRgb
$dest = "$env:LOCALAPPDATA\Programs\BuurmansRgb"
dotnet publish src/BuurmansRgb.Engine -c Release -o $dest
dotnet publish src/BuurmansRgb.UI -c Release -o $dest
& "$dest\BuurmansRgb.Engine.exe"
```

Er verschijnt een tray-icoon rechtsonder bij de klok. Rechtsklik erop en kies
**Starten met Windows**. Dubbelklik opent de instellingen.

## OpenRGB koppelen

1. Sluit fabrikantsoftware af (iCUE, Armoury Crate, Mystic Light, Synapse, G Hub, ...).
2. Start OpenRGB **als administrator** (nodig voor moederbord, RAM en GPU).
3. Tabblad **SDK Server** → **Start Server** (poort 6742). Tip: zet in de instellingen van
   OpenRGB "Start at login", "Start minimized" en "Start server" aan.
4. Binnen een paar seconden staan je apparaten in de instellingen-app.

Echte apparaten werken? Zet dan `"simulatedDevices": false` in
`%AppData%\BuurmansRgb\settings.json` en start de engine opnieuw (tray → **Afsluiten**,
daarna opnieuw starten).

## Bijwerken

Tray → **Afsluiten**, dan in de repo:

```powershell
git pull
$dest = "$env:LOCALAPPDATA\Programs\BuurmansRgb"
dotnet publish src/BuurmansRgb.Engine -c Release -o $dest
dotnet publish src/BuurmansRgb.UI -c Release -o $dest
& "$dest\BuurmansRgb.Engine.exe"
```

## Verwijderen

Tray → **Starten met Windows** uitzetten → **Afsluiten**, dan:

```powershell
Remove-Item "$env:LOCALAPPDATA\Programs\BuurmansRgb", "$env:APPDATA\BuurmansRgb" -Recurse
```

Meer uitleg over effecten, de per-LED-editor en per apparaattype staat in de
[README](../README.md).
