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
pipe), `Effects` (elk effect op vaste tijdstippen), `Engine` (instellingen, verlichting,
render-loop, IPC-server), `Providers.OpenRgb`
(de koppeling met OpenRGB, met een nep-server in het geheugen en een kleine SDK-server op
een loopback-socket, dus zonder OpenRGB) en `UI` (viewmodels met een nep-engine).

## Engine starten

De engine draait zonder venster, alleen met een tray-icoon (rechtsonder bij de klok).
Engine en instellingen-app bouwen allebei naar `artifacts/app/<Debug|Release>/`, zodat de
tray de instellingen-app naast de engine vindt. Bouw dus eerst de solution; alleen de
engine bouwen neemt de instellingen-app niet mee:

```bash
dotnet build BuurmansRgb.slnx
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
    "effect": "rainbow",
    "simulatedDevices": true,
    "framesPerSecond": 30,
    "devices": {
      "simulated-fan": { "color": "#0000FF", "brightness": 0.5, "effect": "per-led" }
    },
    "ledColors": {
      "simulated-fan": ["#FF0000", null, "#0000FF", "..."]
    }
  }
  ```

  `color` is `#RRGGBB`, `brightness` loopt van 0 tot 1, `effect` is een van de effecten
  hieronder. `simulatedDevices` op `false` verbergt de gesimuleerde apparaten (na een herstart
  van de engine), handig zodra de echte apparaten via OpenRGB werken. `framesPerSecond`
  (1–60, standaard 30) is de framerate van de bewegende effecten. Onder `devices` staan
  alleen apparaten met een eigen kleur of effect; de rest volgt de algemene waarden. Onder
  `ledColors` staat per apparaat een kleur per LED voor het effect `per-led`; `null` (of een
  LED voorbij het eind van de lijst) volgt de kleur van het apparaat. Een ongeldig of
  onleesbaar bestand laat de engine ongemoeid en draait dan op de standaardwaarden. Dat
  geldt ook als de map `%AppData%\BuurmansRgb` niet beschrijfbaar is.
- **Eén instantie:** start je de engine nog een keer, dan stopt die meteen en vraagt de
  draaiende engine om de instellingen-app (`BuurmansRgb.UI.exe` naast de engine) te
  openen. Staat die er niet of start hij niet (bijv. geblokkeerd door SmartScreen), dan
  meldt het tray-icoon dat.
- **Starten met Windows** zet `BuurmansRgb` in
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; nogmaals klikken haalt hem weg.
- **Apparaten:** de gesimuleerde apparaten en, als OpenRGB draait, de echte apparaten via
  OpenRGB. Zie "OpenRGB koppelen" hieronder.
- **IPC:** de engine luistert op named pipe `BuurmansRgb.Engine.<sessie-id>` (alleen voor
  de eigen Windows-gebruiker) met StreamJsonRpc. Het sessienummer zorgt dat twee ingelogde
  gebruikers elk hun eigen engine hebben, net als de mutex. De interface is `IEngineApi` in
  `BuurmansRgb.Contracts`. Na het opstarten en na het sluiten van de laatste
  instellingen-app geeft de engine zijn ongebruikte geheugen terug aan Windows.

## Effecten

De effecten staan in `BuurmansRgb.Effects` en zijn tijdgebaseerd: ze schrijven per frame
een kleur per LED in een buffer die de engine hergebruikt. De helderheid past de engine
daarna toe, dus de schuif werkt op elk effect.

| Effect | In `settings.json` | Wat het doet |
| --- | --- | --- |
| Statisch | `static` | Eén kleur op alle LED's |
| Ademen | `breathing` | De kleur fadet in 4 seconden in en uit |
| Regenboog | `rainbow` | Alle tinten verdeeld over de LED's, schuift met 0,2 rondes per seconde |
| Golf | `wave` | Een band van de kleur die over de LED's loopt, 0,5 golf per seconde |
| Per LED | `per-led` | Een opgeslagen kleur per LED (zie de per-LED-editor hieronder) |

- **Render-loop:** alleen als minstens één apparaat een bewegend effect toont, draait een
  `PeriodicTimer` op `framesPerSecond`. Een frame stuurt alleen de apparaten waarvan de
  kleuren veranderden. Met de instellingen-app dicht alloceert een frame niets; met de app
  open maakt de engine per frame een kopie van de kleuren voor het voorbeeld, dus dan wel.
  Meet allocaties (bijv. met `dotnet-counters`) daarom met de app dicht. Bij statische
  kleuren of "Per LED" draait er geen timer.
- **Effecten pauzeren** in het tray-menu stopt ook de animatie; hervatten stuurt alles
  opnieuw en start de animatie weer.
- **Testen zonder hardware:** `dotnet test --project tests/BuurmansRgb.Effects.Tests` test
  elk effect op vaste tijdstippen; de render-loop zelf zit in
  `tests/BuurmansRgb.Engine.Tests` (`LightingControllerAnimationTests`, met een nepklok en
  een test die controleert dat een frame zonder verbonden instellingen-app 0 bytes alloceert).
- **Zelf bekijken:** zet `"effect": "rainbow"` in `settings.json`, start de engine en open
  de instellingen-app: het voorbeeld loopt mee met de gesimuleerde apparaten.

## OpenRGB koppelen

De echte hardware loopt via de SDK-server van [OpenRGB](https://openrgb.org). BuurmansRgb
zelf heeft geen adminrechten nodig; OpenRGB eenmalig wel, voor moederbord, RAM en GPU.

1. Sluit fabrikantsoftware af die dezelfde LED's aanstuurt (iCUE, Armoury Crate, ...).
2. Start OpenRGB en zet de SDK-server aan: tabblad **SDK Server** → **Start Server**, of start
   OpenRGB met `--server`. BuurmansRgb verbindt met `127.0.0.1:6742`, de standaardpoort.
3. Start de engine (de volgorde maakt niet uit). Binnen enkele seconden staan de apparaten
   van OpenRGB in de instellingen-app, onder de gesimuleerde. Een open venster werkt de
   lijst vanzelf bij.

- Bij het verbinden zet BuurmansRgb elk apparaat in de modus "Direct" of "Custom" (OpenRGB
  kiest welke) en stuurt het de opgeslagen kleur. Een apparaat zonder zo'n modus staat wel
  in de lijst, maar volgt de kleur misschien niet.
- **OpenRGB stopt of is er nog niet:** de engine probeert elke 5 seconden opnieuw te
  verbinden en stuurt daarna de kleuren opnieuw. De apparaten verdwijnen zolang uit de
  lijst; hun eigen kleur blijft bewaard.
- In de clientlijst van OpenRGB staat BuurmansRgb twee keer: `BuurmansRgb` stuurt de
  kleuren, `BuurmansRgb watcher` merkt op wanneer OpenRGB stopt of opnieuw zoekt naar
  apparaten (OpenRGB.NET 3.1.1 meldt dat zelf niet goed).
- Een OpenRGB-apparaat heet in `settings.json` bijvoorbeeld
  `openrgb-corsair-vengeance-pro-rgb-1a2b3c4d`: de naam plus een code uit de locatie en het
  serienummer, zodat het na een herstart van OpenRGB of de pc hetzelfde apparaat blijft.
- ARGB-headers op het moederbord hebben in OpenRGB vaak 0 LED's tot je de zonegrootte daar
  instelt (dat komt in taak 7 in BuurmansRgb zelf).
- Meldingen over verbinden, protocolversie en fouten gaan naar de debug-uitvoer (bekijken
  met DebugView), zolang er nog geen logbestand is.

## Instellingen-app starten

De instellingen-app (`BuurmansRgb.UI`, WPF) staat na het bouwen naast
`BuurmansRgb.Engine.exe` in `artifacts/app/<Debug|Release>/`. Start hem via het tray-menu
"Instellingen openen", of los:

```bash
dotnet run --project src/BuurmansRgb.UI
```

- **Links** de apparatenlijst, met bovenaan "Alle apparaten". **Rechts** de kleur (hex-code,
  vaste kleuren, schuiven voor tint en verzadiging), de helderheid (0–100%), het effect en
  een voorbeeld met een bolletje per LED.
- Een wijziging bij "Alle apparaten" geldt voor alle apparaten, maar alleen voor wat je
  wijzigt: zet je de helderheid, dan houdt een apparaat met een eigen kleur die kleur, en
  andersom. Een wijziging bij één apparaat geldt alleen voor dat apparaat. Is een apparaat
  weer gelijk aan "Alle apparaten", dan volgt het die weer en verdwijnt het uit `devices`.
- Het voorbeeld toont wat de engine echt rendert, ook als de effecten gepauzeerd zijn. Bij
  een bewegend effect loopt het mee.
- **Per-LED-editor:** klik in het voorbeeld op een LED om hem te selecteren, Shift+klik
  selecteert een reeks vanaf de vorige klik. Zolang er LED's geselecteerd zijn, staat boven
  de kleur welke dat zijn en kleurt de kleurkiezer alleen die LED's. "Verloop vullen" vult
  de reeks met een verloop van de kleur van de eerste naar die van de laatste LED (geef
  dus eerst de eerste en de laatste LED een kleur). "Selectie opheffen", of een ander
  apparaat in de lijst kiezen, geeft de kleurkiezer weer aan het apparaat. Het apparaat
  krijgt bij de eerste wijziging het effect "Per LED". LED's die je niet kleurt, volgen de
  kleur van het apparaat: kies je zonder selectie een kleur, dan krijgen alleen die LED's de
  nieuwe kleur. De kleuren staan onder `ledColors` in
  `settings.json` en blijven bewaard na een herstart, ook als je tussendoor een ander effect
  kiest.
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
