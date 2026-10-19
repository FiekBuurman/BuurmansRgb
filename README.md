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
alleen nuget.org.

## Engine starten

De engine draait zonder venster, alleen met een tray-icoon (rechtsonder bij de klok):

```bash
dotnet run --project src/BuurmansRgb.Engine
```

- **Tray-menu:** "Instellingen openen", "Effecten pauzeren", "Starten met Windows" en
  "Afsluiten". Dubbelklik op het icoon opent ook de instellingen.
- **Instellingen** staan in `%AppData%\BuurmansRgb\settings.json`. Bij de eerste start
  schrijft de engine de standaardwaarden weg. Pas het bestand aan en start de engine
  opnieuw om een andere statische kleur te zien:

  ```json
  {
    "color": "#FF8800",
    "brightness": 0.8
  }
  ```

  `color` is `#RRGGBB`, `brightness` loopt van 0 tot 1. Een ongeldig of onleesbaar bestand
  laat de engine ongemoeid en draait dan op de standaardwaarden. Dat geldt ook als de map
  `%AppData%\BuurmansRgb` niet beschrijfbaar is.
- **Eén instantie:** start je de engine nog een keer, dan stopt die meteen en vraagt de
  draaiende engine om de instellingen-app (`BuurmansRgb.UI.exe` naast de engine) te
  openen. Staat die er niet, dan meldt het tray-icoon dat.
- **Starten met Windows** zet `BuurmansRgb` in
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; nogmaals klikken haalt hem weg.
- In deze fase stuurt de engine alleen de gesimuleerde apparaten aan; je ziet de kleur
  dus nog nergens. De tests in `tests/BuurmansRgb.Engine.Tests` controleren dat de
  opgeslagen kleur op alle gesimuleerde LED's komt.

## Mappen

- `src/` – de projecten, zie de solution-indeling in `docs/bouwplan.md`
- `tests/` – unit-tests, zonder hardware

- `docs/bouwplan.md` – architectuur, fasering en taken met acceptatiecriteria
- `docs/prompts.md` – kant-en-klare opdrachten voor agents
- `docs/hardware.md` – jouw hardware en wat werkt
- `docs/ideas.md` – ideeën voor later
