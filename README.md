# BuurmansRgb

Lichtgewicht RGB-aansturing voor Windows: fans, strips, moederbord, GPU, toetsenbord,
muis en headset vanuit één app. Draait rustig op de achtergrond met een tray-icoon.

## Status

Nog in opbouw. Zie `docs/bouwplan.md` voor het plan en de voortgang.

## Vereisten

- Windows 10/11
- .NET 10 SDK
- [OpenRGB](https://openrgb.org) met de SDK-server aan (vanaf taak 3)

## Bouwen en testen

Open `BuurmansRgb.slnx` in Visual Studio 2026, of vanaf de commandline:

```bash
dotnet build BuurmansRgb.slnx -warnaserror
dotnet test --solution BuurmansRgb.slnx
```

Tests gebruiken xUnit v3 op Microsoft.Testing.Platform (ingesteld in `global.json`).
Pakketversies staan centraal in `Directory.Packages.props`; `nuget.config` gebruikt
alleen nuget.org. Daar staat ook `MessagePack` vastgepind op een versie zonder bekend
lek, omdat StreamJsonRpc een oudere versie meebrengt.

Per project een testproject in `tests/`: `Core`, `Contracts` (IPC over een echte named
pipe), `Effects`, `Engine` (instellingen, verlichting, IPC-server) en `UI` (viewmodels
met een nep-engine).

## Engine starten

De engine draait zonder venster, alleen met een tray-icoon (rechtsonder bij de klok):

```bash
dotnet run --project src/BuurmansRgb.Engine
```

- **Tray-menu:** "Instellingen openen", "Effecten pauzeren", "Starten met Windows" en
  "Afsluiten". Dubbelklik op het icoon opent ook de instellingen.
- **Instellingen** staan in `%AppData%\BuurmansRgb\settings.json`. Bij de eerste start
  schrijft de engine de standaardwaarden weg. Wijzigingen uit de instellingen-app slaat
  de engine een seconde na de laatste wijziging op, en bij afsluiten. Je kunt het bestand
  ook met de hand aanpassen en de engine opnieuw starten:

  ```json
  {
    "color": "#FF8800",
    "brightness": 0.8,
    "effect": "static",
    "devices": {
      "simulated-fan": { "color": "#0000FF", "brightness": 0.5 }
    }
  }
  ```

  `color` is `#RRGGBB`, `brightness` loopt van 0 tot 1, `effect` is voorlopig alleen
  `static`. Onder `devices` staan alleen apparaten met een eigen kleur; de rest volgt de
  algemene waarden. Een ongeldig of onleesbaar bestand laat de engine ongemoeid en draait
  dan op de standaardwaarden. Dat geldt ook als de map `%AppData%\BuurmansRgb` niet
  beschrijfbaar is.
- **Eén instantie:** start je de engine nog een keer, dan stopt die meteen en vraagt de
  draaiende engine om de instellingen-app (`BuurmansRgb.UI.exe` naast de engine) te
  openen. Staat die er niet of start hij niet (bijv. geblokkeerd door SmartScreen), dan
  meldt het tray-icoon dat.
- **Starten met Windows** zet `BuurmansRgb` in
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; nogmaals klikken haalt hem weg.
- In deze fase stuurt de engine alleen de gesimuleerde apparaten aan; je ziet de kleur
  in het voorbeeld van de instellingen-app.
- **IPC:** de engine luistert op named pipe `BuurmansRgb.Engine` (alleen voor de eigen
  Windows-gebruiker) met StreamJsonRpc; de interface is `IEngineApi` in
  `BuurmansRgb.Contracts`. Na het sluiten van de laatste instellingen-app geeft de engine
  zijn ongebruikte geheugen terug aan Windows.

## Instellingen-app starten

De instellingen-app (`BuurmansRgb.UI`, WPF) komt bij het bouwen van de engine naast
`BuurmansRgb.Engine.exe` te staan. Start hem via het tray-menu "Instellingen openen", of
los:

```bash
dotnet run --project src/BuurmansRgb.UI
```

- **Links** de apparatenlijst, met bovenaan "Alle apparaten". **Rechts** de kleur (hex-code,
  vaste kleuren, schuiven voor tint en verzadiging), de helderheid (0–100%), het effect en
  een voorbeeld met een bolletje per LED.
- Een wijziging bij "Alle apparaten" geldt voor alle apparaten en wist hun eigen kleuren;
  een wijziging bij één apparaat geldt alleen voor dat apparaat.
- Het voorbeeld toont wat de engine echt rendert, ook als de effecten gepauzeerd zijn.
- Draait de engine niet, dan meldt het venster dat en kun je "Opnieuw proberen". Stopt de
  engine terwijl het venster open is, dan meldt het dat de verbinding is verbroken.
- Venster sluiten = proces stopt; de engine blijft draaien.

## Mappen

- `src/` – de projecten, zie de solution-indeling in `docs/bouwplan.md`
- `tests/` – unit-tests, zonder hardware

- `docs/bouwplan.md` – architectuur, fasering en taken met acceptatiecriteria
- `docs/prompts.md` – kant-en-klare opdrachten voor agents
- `docs/hardware.md` – jouw hardware en wat werkt
- `docs/ideas.md` – ideeën voor later
