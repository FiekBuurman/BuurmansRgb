# Installeren – TL;DR

## Downloaden (aanbevolen)

Download `BuurmansRgb-setup.exe` bij de
[nieuwste release](https://github.com/FiekBuurman/BuurmansRgb/releases/latest) en ga verder bij
stap 2 hieronder. Wil je controleren of het bestand goed is aangekomen, vergelijk dan de SHA-256
(in PowerShell: `Get-FileHash .\BuurmansRgb-setup.exe`) met die in de release.

## Met de installer die je zelf bouwt

1. Bouw de installer, of pak een `BuurmansRgb-setup.exe` die je eerder bouwde:

   ```powershell
   git clone https://github.com/FiekBuurman/BuurmansRgb.git
   cd BuurmansRgb
   .\installer\build.ps1 -Version 1.0.0
   ```

   Nodig: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), Git
   en internet (het script downloadt OpenRGB, PawnIO en Inno Setup en controleert ze). Met
   `-Version` geef je de versie mee; zonder is het 1.0.0. De installer staat daarna in
   `artifacts\installer\out\BuurmansRgb-setup.exe`.

   Weigert PowerShell het script ("running scripts is disabled"), start het dan zo:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\installer\build.ps1 -Version 1.0.0
   ```

2. Sluit fabrikantsoftware af (iCUE, Armoury Crate, Mystic Light, Synapse, G Hub, ...).
3. Draai `BuurmansRgb-setup.exe` en geef één keer toestemming (UAC). SmartScreen kan
   waarschuwen omdat de installer niet ondertekend is: **Meer info → Toch uitvoeren**.

De installer zet in één keer neer:

- **BuurmansRgb** in `C:\Program Files\BuurmansRgb`, met alles erin (geen losse .NET nodig);
- de driver **PawnIO**, voor moederbord, RAM en videokaart (overgeslagen als die er al is);
- **OpenRGB 1.1** als Windows-service, met de SDK-server op `127.0.0.1:6742` (overgeslagen
  als de service `OpenRGB` er al is).

Na afloop start BuurmansRgb met een tray-icoon rechtsonder bij de klok, en staat
**Starten met Windows** aan. Dubbelklik op het icoon opent de instellingen. Vraagt de
installer om een herstart, dan heeft PawnIO of OpenRGB die nodig.

Zodra OpenRGB echte lampen levert, verdwijnen de gesimuleerde lampen vanzelf. Wil je ze nooit
zien, kies dan in de instellingen-app bij **Instellingen → Gesimuleerde lampen** voor "Nooit".

## Bijwerken

Maak eerst een reservekopie van `%AppData%\BuurmansRgb` (je instellingen en scènes:
`settings.json` en `profiles.json`). Download dan de setup van de
[nieuwste release](https://github.com/FiekBuurman/BuurmansRgb/releases/latest) en draai die over
de oude heen. Of haal de nieuwe code op en bouw en draai de nieuwe installer zelf:

```powershell
git pull
.\installer\build.ps1 -Version 1.1.0
```

Daarna `artifacts\installer\out\BuurmansRgb-setup.exe` draaien. De installer stopt de
draaiende engine en instellingen-app zelf en laat je instellingen staan. Een OpenRGB-service
en PawnIO die er al staan, laat hij met rust.

## Verwijderen

Via **Instellingen → Apps → BuurmansRgb → Verwijderen**. Dat stopt de engine en haalt
BuurmansRgb en de autostart weg. Daarna vraagt het of OpenRGB en PawnIO ook weg moeten (alleen
als deze installer ze heeft geïnstalleerd), en of je instellingen weg moeten.

## Zonder installer (voor ontwikkelaars)

Zonder adminrechten, met OpenRGB zelf geïnstalleerd en de SDK-server aan (tabblad
**SDK Server** → **Start Server**, OpenRGB als administrator voor moederbord, RAM en GPU):

```powershell
$dest = "$env:LOCALAPPDATA\Programs\BuurmansRgb"
dotnet publish src/BuurmansRgb.Engine -c Release -o $dest
dotnet publish src/BuurmansRgb.UI -c Release -o $dest
& "$dest\BuurmansRgb.Engine.exe"
```

Engine en instellingen-app moeten in dezelfde map staan; daarom twee keer `publish` naar
`$dest`. Zo heb je wel de .NET 10 Desktop Runtime nodig (die zit bij de SDK).

Meer uitleg over de instellingen-app, de instellingenbestanden en per apparaattype staat in
de [handleiding](handleiding.md); hoe de installer werkt in
[openrgb-meeleveren.md](openrgb-meeleveren.md).
