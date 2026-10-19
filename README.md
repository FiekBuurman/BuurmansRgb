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
een loopback-socket, dus zonder OpenRGB), `Providers.Simulated` (de gesimuleerde
apparaten) en `UI` (viewmodels met een nep-engine).

Helpers die elk testproject gebruikt (`Eventually`, `TestColors`, `TestCancellation`, `TestTimeouts`) staan in
`tests/Shared`; `tests/Directory.Build.props` bouwt ze in elk testproject mee. Na de
code-review kregen ook deze delen eigen tests: de invoercontrole van de verlichting
(`LightingControllerValidationTests`, `LightingTableTests`), `EffectCatalogTests`,
`DeviceOutputTests`, `OpenRgbPacketsTests`, `OpenRgbServerWatcherTests` (pakketten met data en
een kapotte header), `ColorBrushesTests`, `ReentrancyGuardTests`, `DeviceErrorsTests` en
`RgbColorJsonConverterTests`.

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
  OpenRGB. Zie "OpenRGB koppelen" hieronder. Gesimuleerd zijn er een strip (30 LED's), een
  fan (12), een toetsenbord (104, met de plek van elke toets), een moederbord (7, in drie
  zones), twee RAM-sticks (8), een videokaart (13, in twee zones), een fan met twee ringen
  (20), een ARGB-controller (20, in twee kanalen), verlichting in de behuizing (10), een muis
  (12, in vier zones) en een headset (2, in twee zones).
- **Na slaapstand:** 2 en 10 seconden na het ontwaken zet de engine de apparaten opnieuw
  klaar en stuurt hij alle kleuren opnieuw, ook als er niets veranderde. Zie "Moederbord en
  RAM" hieronder.
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
  kleuren of "Per LED" draait er geen timer. Gaat een frame mis, bijvoorbeeld omdat een
  apparaat even faalt, dan probeert het volgende frame het gewoon opnieuw.
- **Effecten pauzeren** in het tray-menu stopt ook de animatie; hervatten stuurt alles
  opnieuw en start de animatie weer.
- **Testen zonder hardware:** `dotnet test --project tests/BuurmansRgb.Effects.Tests` test
  elk effect op vaste tijdstippen; de render-loop zelf zit in
  `tests/BuurmansRgb.Engine.Tests`
  (`AnimationLoopTests` en `LightingControllerAnimationTests`, met een nepklok en een test die
  controleert dat een frame zonder verbonden instellingen-app 0 bytes alloceert).
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
- ARGB-headers op het moederbord hebben in OpenRGB vaak 0 LED's tot je de zonegrootte instelt.
  Dat kan in OpenRGB, of in de instellingen-app (zie "Fans, case-LED en ARGB-strips").
- Meldingen over verbinden, protocolversie en fouten gaan naar de debug-uitvoer (bekijken
  met DebugView), zolang er nog geen logbestand is.

## Moederbord en RAM

Moederbord en RAM zijn de eerste echte apparaattypes (taak 5). OpenRGB bereikt ze meestal
via SMBus, dus OpenRGB moet de eerste keer met adminrechten draaien (zie "OpenRGB koppelen").

- **In de lijst** staat per apparaat een icoon en het type: "Moederbord" of "RAM", met het
  aantal LED's en, bij meer dan één zone, het aantal zones. De andere types hebben ook een
  eigen icoon en naam (zie hun secties); alleen wat OpenRGB niet als een van deze types kent,
  zoals een muismat, krijgt een algemeen icoon zonder type.
- **Zones:** het voorbeeld toont de LED's per zone, met de naam en het aantal LED's, zoals
  OpenRGB ze geeft (bijv. "Aura Mainboard" en "Aura Addressable 1"). Een header zonder LED's
  staat er ook, met een vak om in te stellen hoeveel LED's erop zitten (zie "Fans, case-LED en
  ARGB-strips"). Een apparaat met één zone toont geen zonenaam.
- **RAM:** OpenRGB toont elke RAM-stick als eigen apparaat, vaak met dezelfde naam. Die
  krijgen een nummer in de volgorde van OpenRGB ("ENE DRAM 1", "ENE DRAM 2"). Het id in
  `settings.json` verandert daardoor niet.
- **Na slaapstand** gaan veel moederborden en RAM-sticks terug naar hun eigen effect. De
  engine luistert naar `PowerModeChanged` en herstelt 2 en 10 seconden na het ontwaken: hij
  laadt de OpenRGB-apparaten opnieuw (dat zet ze weer in "Direct" of "Custom") en stuurt alle
  kleuren opnieuw. Zijn de effecten gepauzeerd, dan gebeurt dat pas bij hervatten.
- **Testen zonder hardware:** `dotnet test --solution BuurmansRgb.slnx`. Wat dit deel test:
  de vertaling van OpenRGB-moederborden en -RAM met nepdata (`OpenRgbDeviceMapperTests`,
  `OpenRgbDeviceProviderTests`), herstellen na slaapstand (`LightingControllerRestoreTests`,
  `ResumeRestorerTests`, met een nepklok) en de lijst en zones in de instellingen-app
  (`DeviceItemTests`, `DeviceIconsTests`, `DevicePreviewViewModelTests`). Start daarnaast de
  engine en de instellingen-app: het gesimuleerde moederbord en de RAM-sticks staan in de lijst.
- **Testen met je eigen hardware:** volg "Een apparaattype testen met je eigen hardware" voor
  elk moederbord en elke RAM-stick.

## Videokaart (GPU)

De videokaart is het tweede echte apparaattype (taak 6) en werkt net als moederbord en RAM.
Ook de LED's van een videokaart bereikt OpenRGB meestal via SMBus (I2C), dus ook hier moet
OpenRGB de eerste keer met adminrechten draaien.

- **In de lijst** staat een videokaart met een eigen icoon en het type "Videokaart", met het
  aantal LED's en, bij meer dan één zone, het aantal zones. De gesimuleerde videokaart heeft
  een lichtbalk van 12 LED's en een logo van 1 LED.
- **Zones:** het voorbeeld toont de zones zoals OpenRGB ze geeft. Sommige kaarten hebben een
  eigen ARGB-header met 0 LED's; die staat er net als bij het moederbord bij, en ook daar stel
  je het aantal LED's in.
- **Twee dezelfde kaarten** krijgen een nummer in de volgorde van OpenRGB, net als RAM-sticks.
- **Zonder "Direct"-modus:** veel kaarten kennen in OpenRGB alleen hun eigen effecten
  (bijv. "Static" en "Breathing"). Zo'n kaart staat wel in de lijst, maar volgt de kleur
  misschien niet; de debug-uitvoer meldt dat bij het verbinden.
- **Na slaapstand** herstelt de engine ook de videokaart, zoals beschreven bij "Moederbord en
  RAM".
- **Testen zonder hardware:** `dotnet test --solution BuurmansRgb.slnx`. Voor dit deel:
  `OpenRgbDeviceMapperTests` en `OpenRgbDeviceProviderTests` (videokaarten met nepdata van
  OpenRGB, ook zonder "Direct"-modus en na herstellen), `SimulatedDeviceProviderTests`,
  `DeviceItemTests`, `DeviceIconsTests` en `DevicePreviewViewModelTests` (de zones, die voor
  elk apparaattype hetzelfde werken). Start daarnaast de engine en de instellingen-app: de
  gesimuleerde videokaart staat onderaan in de lijst.
- **Testen met je eigen hardware:** volg "Een apparaattype testen met je eigen hardware" voor
  de videokaart, en noteer in `docs/hardware.md` ook of de kaart in OpenRGB een
  "Direct"-modus heeft.

## Fans, case-LED en ARGB-strips

Fans, strips en de verlichting in de behuizing zijn het derde echte apparaattype (taak 7). Ze
hangen meestal aan een ARGB-header van het moederbord of aan een controller (bijv. een
Corsair Commander of een Lian Li-hub), die OpenRGB via USB bereikt.

- **In de lijst** heeft elk type een eigen icoon en naam: "Ventilator", "LED-strip" (ook voor
  controllers met ARGB-kanalen, zo noemt OpenRGB ze) en "Behuizing". OpenRGB.NET 3.1.1 kent het
  type "case" van nieuwere OpenRGB-versies niet bij naam; BuurmansRgb herkent het aan zijn
  nummer.
- **Lengte instellen:** bij een zone die OpenRGB in lengte laat instellen (een ARGB-header of
  -kanaal) staat onder de LED's "Aantal LED's (0–120):" met het bereik van die zone. Typ het
  aantal LED's van wat erop zit en kies "Toepassen" (of Enter). OpenRGB geeft de zone de
  nieuwe lengte; daarna laadt BuurmansRgb de apparaten opnieuw en stuurt het de kleuren weer.
  Bij een apparaat met één zone, zoals de gesimuleerde strip (1–60 LED's), staat het vak er
  ook, zonder zonenaam. Per-LED-kleuren van de zones erna schuiven mee, ook als je de lengte
  in OpenRGB zelf verandert. De gesimuleerde apparaten onthouden een nieuwe lengte tot de
  engine stopt.
- **Let op bij per-LED-kleuren:** `settings.json` bewaart de kleuren per LED, maar niet de
  zonelengtes waar ze bij horen. Heeft een zone bij de start van de engine een andere lengte
  dan toen je de kleuren koos, dan staan de kleuren van de zones erna zoveel LED's verschoven.
  Dat gebeurt bij de gesimuleerde apparaten na elke herstart van de engine (ze beginnen weer
  met hun eerste lengte), en bij OpenRGB als het een lengte vergeet of als je die verandert
  terwijl de engine niet draait. Kies de kleuren van die zones dan opnieuw.
- **Een ring of zone kiezen:** klik in het voorbeeld op de naam van een zone, bijv. "Outer
  ring" van een fan of een kanaal van een controller. Alle LED's van die zone zijn dan
  geselecteerd in de per-LED-editor, en de kleur die je kiest geldt alleen voor die zone.
- **Na slaapstand** herstelt de engine ook fans en strips, zoals beschreven bij "Moederbord en
  RAM". Een ingestelde lengte blijft na het ontwaken gelijk: OpenRGB houdt hem vast zolang het
  draait. Of OpenRGB hem ook na een herstart nog weet, test je met je eigen hardware (stap 1
  hieronder).
- **Testen zonder hardware:** `dotnet test --solution BuurmansRgb.slnx`. Voor dit deel:
  `RgbZoneTests`, `RgbDeviceTests` en `SimulatedDeviceProviderTests` (bereik en lengte van zones),
  `OpenRgbDeviceMapperTests`, `OpenRgbDeviceProviderTests` en `OpenRgbNetConnectionTests`
  (het type behuizing, lengte instellen bij OpenRGB met nepdata en een kleine SDK-server, ook
  na herstellen), `LightingControllerZoneTests` en `LedColorStoreTests` (lengte instellen via de
  engine, per-LED-kleuren die meeschuiven), `SwitchableDeviceProviderTests`, `EngineApiTests`,
  `EngineRpcTests` en in de instellingen-app `DeviceItemTests`, `DeviceIconsTests`,
  `DevicePreviewViewModelTests`, `LedEditorViewModelTests` en `MainViewModelLedEditorTests`.
  Start daarnaast de engine en de instellingen-app: kies "Simulated ARGB controller", zet
  "ARGB 2" op bijv. 15 en kies "Toepassen", of klik bij "Simulated dual-ring fan" op "Outer
  ring" en kies een kleur.
- **Testen met je eigen hardware:** volg "Een apparaattype testen met je eigen hardware" voor
  elke fan, strip en controller, en daarnaast:
  1. Stel bij een ARGB-header of -kanaal het aantal LED's in en controleer dat de hele strip
     (en niet meer) oplicht; herstart OpenRGB en kijk of de lengte bewaard blijft.
  2. Klik bij een fan met meer ringen op de naam van een ring en geef alleen die ring een
     kleur.
  3. Noteer in `docs/hardware.md` welke controller het is, hoeveel LED's er per kanaal zitten
     en of het type klopt (fan, strip of behuizing).

## Toetsenbord, muis en headset

Toetsenbord, muis en headset zijn het laatste apparaattype (taak 8). OpenRGB bereikt ze via
USB, niet via SMBus.

- **In de lijst** heeft elk type een eigen icoon en naam: "Toetsenbord", "Muis" en "Headset".
- **Toetsenbord als matrix:** bij veel toetsenborden geeft OpenRGB per zone een matrix-map:
  rijen en kolommen met in elk vakje de LED van de toets die daar zit. Het voorbeeld tekent
  dan elke LED als toets op die plek, en maakt de toetsen kleiner als het venster te smal is.
  LED's die wel in de zone zitten maar niet in de map (bijv. een logo), staan in een rij onder
  de toetsen. Een zone zonder map, zoals een lichtbalk, blijft een rij bolletjes. Klik op een
  toets om hem in de per-LED-editor een eigen kleur te geven. Shift+klik selecteert een reeks
  in de LED-volgorde van OpenRGB; die loopt vaak rij voor rij, maar verschilt per toetsenbord.
  Regenboog en Golf lopen ook over die LED-volgorde, niet over de plek van de toetsen.
- **Muis:** het voorbeeld toont de zones zoals OpenRGB ze geeft (bijv. logo, scrollwiel en
  zijkanten). Klik op de naam van een zone om alleen die zone een kleur te geven.
- **Headset:** OpenRGB ondersteunt maar een deel van de headsets; kijk in de lijst "Supported
  Devices" op [openrgb.org](https://openrgb.org). Een headset die OpenRGB niet kent, staat niet
  in OpenRGB en dus ook niet in BuurmansRgb. BuurmansRgb heeft geen eigen driver voor headsets
  en krijgt er ook geen; noteer zo'n headset in `docs/hardware.md` als "niet ondersteund door
  OpenRGB". Een headsetstandaard heet in OpenRGB geen headset en staat zonder type in de lijst.
- **Na slaapstand** herstelt de engine ook toetsenbord, muis en headset, zoals beschreven bij
  "Moederbord en RAM". Sluit je een USB-apparaat opnieuw aan, dan ziet OpenRGB het mogelijk pas
  nadat het opnieuw naar apparaten heeft gezocht.
- **Testen zonder hardware:** `dotnet test --solution BuurmansRgb.slnx`. Voor dit deel:
  `LedMatrixTests` en `SimulatedDeviceProviderTests` (de matrix en het gesimuleerde
  toetsenbord, de muis en de headset), `OpenRgbDeviceMapperTests`, `OpenRgbNetConnectionTests`
  en `OpenRgbDeviceProviderTests` (matrix-maps van OpenRGB met nepdata en een kleine
  SDK-server, rijen en kolommen, en herstellen na slaapstand), `EngineRpcTests` en
  `EngineIpcServerTests` (de matrix gaat mee naar de instellingen-app), en `DeviceItemTests`,
  `DeviceIconsTests`, `LedLayoutTests` (plek van de toetsen, klikken en meekrimpen in een smal
  venster) en `DevicePreviewViewModelTests` in de instellingen-app.
  Start daarnaast de engine en de instellingen-app: kies "Simulated keyboard" en klik op een
  toets, of kies bij "Simulated mouse" de zone "Scroll wheel".
- **Testen met je eigen hardware:** volg "Een apparaattype testen met je eigen hardware" voor
  het toetsenbord, de muis en de headset, en daarnaast:
  1. Vergelijk het voorbeeld van het toetsenbord met het echte toetsenbord: klik op bijv. Esc,
     W, A, S, D en de spatiebalk, geef ze een kleur en controleer dat precies die toetsen
     oplichten.
  2. Geef bij de muis één zone een eigen kleur en controleer dat alleen die zone verandert.
  3. Staat de headset niet in OpenRGB, noteer dan het model in `docs/hardware.md`.
  4. Noteer in `docs/hardware.md` per apparaat het model en wat werkt.

## Een apparaattype testen met je eigen hardware

Deze stappen gelden voor elk echt apparaattype (moederbord, RAM, videokaart, fans, ...). Wat per
type extra is, staat in de sectie van dat type.

1. Sluit de software van de fabrikant af (bijv. Armoury Crate, MSI Center, RGB Fusion of
   iCUE), start OpenRGB als administrator met de SDK-server aan, daarna de engine en de
   instellingen-app.
2. Vergelijk per apparaat het aantal LED's en de zones met OpenRGB.
3. Kies een statische kleur, verander de helderheid en kies "Regenboog".
4. Zet de pc in slaapstand, maak hem wakker en wacht 10 seconden: de kleuren horen terug te
   zijn.
5. Noteer in `docs/hardware.md` wat werkt.

## Instellingen-app starten

De instellingen-app (`BuurmansRgb.UI`, WPF) staat na het bouwen naast
`BuurmansRgb.Engine.exe` in `artifacts/app/<Debug|Release>/`. Start hem via het tray-menu
"Instellingen openen", of los:

```bash
dotnet run --project src/BuurmansRgb.UI
```

- **Links** de apparatenlijst, met bovenaan "Alle apparaten" en per apparaat een icoon en
  het type. **Rechts** de kleur (hex-code, vaste kleuren, schuiven voor tint en
  verzadiging), de helderheid (0–100%), het effect en een voorbeeld met een bolletje per
  LED, per zone. Een toetsenbord met een matrix-map toont een toets per LED, op zijn plek.
- Een wijziging bij "Alle apparaten" geldt voor alle apparaten, maar alleen voor wat je
  wijzigt: zet je de helderheid, dan houdt een apparaat met een eigen kleur die kleur, en
  andersom. Een wijziging bij één apparaat geldt alleen voor dat apparaat. Is een apparaat
  weer gelijk aan "Alle apparaten", dan volgt het die weer en verdwijnt het uit `devices`.
- Het voorbeeld toont wat de engine echt rendert, ook als de effecten gepauzeerd zijn. Bij
  een bewegend effect loopt het mee.
- **Per-LED-editor:** klik in het voorbeeld op een LED om hem te selecteren, Shift+klik
  selecteert een reeks vanaf de vorige klik, en een klik op de naam van een zone selecteert
  die hele zone (bijv. één ring van een fan). Zolang er LED's geselecteerd zijn, staat boven
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
- `tests/` – unit-tests, zonder hardware; `tests/Shared` – helpers voor elk testproject

- `docs/bouwplan.md` – architectuur, fasering en taken met acceptatiecriteria
- `docs/prompts.md` – kant-en-klare opdrachten voor agents
- `docs/hardware.md` – jouw hardware en wat werkt
- `docs/ideas.md` – ideeën voor later
