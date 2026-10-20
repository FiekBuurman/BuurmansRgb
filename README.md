# BuurmansRgb

Lichtgewicht RGB-aansturing voor Windows: fans, strips, moederbord, GPU, toetsenbord,
muis en headset vanuit één app. Draait rustig op de achtergrond met een tray-icoon.

**Installeren:** zie [docs/installatie.md](docs/installatie.md) (TL;DR).

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
`tests/Shared`; `tests/Directory.Build.props` bouwt ze in elk testproject mee. De OpenRGB-server in
het geheugen (`FakeOpenRgbConnector`, en voor tests buiten de provider zelf `InMemoryOpenRgb`)
staat in het hulpproject `tests/BuurmansRgb.Providers.OpenRgb.Testing`, zonder eigen tests. Na de
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

- **Tray-menu:** "Instellingen openen", "Profiel laden", "Effecten pauzeren", "Starten met
  Windows" en "Afsluiten". Dubbelklik op het icoon opent ook de instellingen. Zie "Profielen"
  voor "Profiel laden". Zie "Pauzeren" voor "Effecten pauzeren".
- **Instellingen** staan in `%AppData%\BuurmansRgb\settings.json`. Bij de eerste start
  schrijft de engine de standaardwaarden weg. Wijzigingen uit de instellingen-app slaat
  de engine een seconde na de laatste wijziging op, en bij afsluiten. Je kunt het bestand
  ook met de hand aanpassen en de engine opnieuw starten:

  ```json
  {
    "color": "#FF8800",
    "brightness": 0.8,
    "effect": "rainbow",
    "speed": 1.5,
    "colorLoop": { "colors": ["#FFFF00", "#00FF00", "#0000FF"], "spread": true },
    "simulatedDevices": "automatic",
    "framesPerSecond": 30,
    "pauseOnLockAndSleep": true,
    "devices": {
      "simulated-fan": { "color": "#0000FF", "brightness": 0.5, "effect": "per-led" }
    },
    "ledColors": {
      "simulated-fan": ["#FF0000", null, "#0000FF", "..."],
      "simulated-argb": ["#FF0000", "...", "#0000FF"]
    },
    "ledColorZoneLengths": {
      "simulated-argb": [20, 15]
    },
    "zoneLengths": {
      "simulated-argb": { "ARGB 2": 15 }
    }
  }
  ```

  `color` is `#RRGGBB`, `brightness` loopt van 0 tot 1, `effect` is een van de effecten
  hieronder. `speed` is de snelheid van een bewegend effect: 1 is normaal, van 0,1 (tien keer
  zo langzaam) tot 3 (drie keer zo snel); ook per apparaat onder `devices`. `colorLoop` zijn de
  2–8 kleuren van de Kleurenloop, met `spread` op `false` voor alle LED's tegelijk; zonder
  `colorLoop` (of met te weinig of te veel kleuren) is het geel, groen, blauw.
  `simulatedDevices` zegt wanneer de gesimuleerde apparaten er zijn: `"automatic"`
  (standaard: alleen zolang OpenRGB geen echte apparaten levert), `"always"` of `"never"`
  (zie "OpenRGB-status en gesimuleerde apparaten"). Een woord dat de engine niet kent, of een
  andere waarde (een getal, `null`, ...), leest als `"automatic"`; de rest van het bestand
  blijft gewoon gelden. Oudere bestanden hebben hier `true` of `false`: `true` schreef de engine
  zelf als standaard weg en leest dus als `"automatic"`, `false` blijft `"never"`. Kies het
  liever in de instellingen-app: dan wisselt de engine meteen, zonder herstart.
  `framesPerSecond` (1–60, standaard 30) is de framerate van de bewegende effecten.
  `pauseOnLockAndSleep` (standaard `true`) pauzeert de bewegende effecten zolang Windows
  vergrendeld is of slaapt; `false` laat ze doorlopen (zie "Pauzeren"). Onder
  `devices` staan alleen apparaten met een eigen kleur of effect; de rest volgt de algemene
  waarden. Onder `ledColors` staat per apparaat een kleur per LED voor het effect `per-led`;
  `null` (of een LED voorbij het eind van de lijst) volgt de kleur van het apparaat. Onder
  `ledColorZoneLengths` staat per apparaat het aantal LED's van elke zone (in de volgorde van de
  zones) waarvoor die kleuren gekozen zijn, zodat ze een zone met een andere lengte volgen (zie
  "Fans, case-LED en ARGB-strips"); oudere bestanden hebben dat niet. Onder `zoneLengths` staat per
  apparaat de lengte die je een instelbare zone gaf, op de naam van de zone. Een waarde in
  `zoneLengths` of `ledColorZoneLengths` die geen getal is, slaat de engine over (bij
  `ledColorZoneLengths` de hele lijst van dat apparaat); de rest van het bestand blijft gelden. Is
  het bestand geen geldige JSON of klopt er iets anders niet (bijv. een kleur als `"purple"`), dan
  zet de engine het opzij als `settings.json.broken` en draait hij op de standaardwaarden; die
  schrijft hij bij de volgende wijziging weg. Verbeter de fout in `settings.json.broken` en zet
  het terug als `settings.json` terwijl de engine niet draait. Kan de engine het bestand niet
  lezen (bijv. omdat een ander programma het open heeft), of is de map `%AppData%\BuurmansRgb`
  niet beschrijfbaar, dan laat hij het ongemoeid en draait hij op de standaardwaarden.
- **Eén instantie:** start je de engine nog een keer, dan stopt die meteen en vraagt de
  draaiende engine om de instellingen-app (`BuurmansRgb.UI.exe` naast de engine) te
  openen. Staat die er niet of start hij niet (bijv. geblokkeerd door SmartScreen), dan
  meldt het tray-icoon dat.
- **Starten met Windows** zet `BuurmansRgb` in
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; nogmaals klikken haalt hem weg.
- **Apparaten:** de echte apparaten via OpenRGB, als OpenRGB draait, en de gesimuleerde
  apparaten; die verdwijnen standaard zodra OpenRGB echte apparaten levert. Zie "OpenRGB
  koppelen" en "OpenRGB-status en gesimuleerde apparaten" hieronder. Gesimuleerd zijn er een
  strip (30 LED's), een fan (12), een toetsenbord (104, met de plek van elke toets), een
  moederbord (7, in drie zones), twee RAM-sticks (8), een videokaart (13, in twee zones), een
  fan met twee ringen (20), een ARGB-controller (20, in twee kanalen), verlichting in de
  behuizing (10), een muis (12, in vier zones) en een headset (2, in twee zones).
- **Tooltip van het tray-icoon:** de naam (of "effecten gepauzeerd", ook bij een pauze door
  vergrendelen of slaapstand) en daaronder hoe het met
  OpenRGB gaat, bijv. "OpenRGB: verbonden, 5 apparaten".
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
| Kleurenloop | `color-loop` | Je eigen 2–8 kleuren die in elkaar overlopen en na de laatste weer bij de eerste beginnen, 0,2 loop per seconde. Over de LED's verdeeld (zoals de regenboog) of alle LED's tegelijk |
| Per LED | `per-led` | Een opgeslagen kleur per LED (zie de per-LED-editor hieronder) |

- **Render-loop:** alleen als minstens één apparaat een bewegend effect toont, draait een
  `PeriodicTimer` op `framesPerSecond`. Een frame stuurt alleen de apparaten waarvan de
  kleuren veranderden. Met de instellingen-app dicht alloceert een frame niets; met de app
  open maakt de engine per frame een kopie van de kleuren voor het voorbeeld, dus dan wel.
  Meet allocaties (bijv. met `dotnet-counters`) daarom met de app dicht. Bij statische
  kleuren of "Per LED" draait er geen timer, behalve de 1,5 seconde dat een apparaat knippert.
  Gaat een frame mis, bijvoorbeeld omdat een apparaat even faalt, dan probeert het volgende
  frame het gewoon opnieuw.
- **Effecten pauzeren** in het tray-menu stopt ook de animatie; hervatten stuurt alles
  opnieuw en start de animatie weer. Ook bij vergrendelen en slaapstand stopt de animatie, zie
  "Pauzeren".
- **Testen zonder hardware:** `dotnet test --project tests/BuurmansRgb.Effects.Tests` test
  elk effect op vaste tijdstippen; de render-loop zelf zit in
  `tests/BuurmansRgb.Engine.Tests`
  (`AnimationLoopTests` en `LightingControllerAnimationTests`, met een nepklok en een test die
  controleert dat een frame zonder verbonden instellingen-app 0 bytes alloceert).
- **Zelf bekijken:** zet `"effect": "rainbow"` in `settings.json`, start de engine en open
  de instellingen-app: het voorbeeld loopt mee met de gesimuleerde apparaten.

## Pauzeren

De engine pauzeert de effecten om drie redenen, die elk op zichzelf aflopen
(`PauseReasons` in `BuurmansRgb.Contracts`):

| Reden | Begint | Eindigt | Wat de lampen doen |
| --- | --- | --- | --- |
| Tray-menu | "Effecten pauzeren" aanvinken | Uitvinken, of "Hervatten" in de instellingen-app | Blijven zoals ze zijn; de engine stuurt niets tot hervatten |
| Vergrendeld | Windows vergrendelen (Windows-toets+L), van gebruiker wisselen, of een extern bureaublad dat verbroken is zonder af te melden | Ontgrendelen, of de sessie is weer op een scherm | Het laatste frame blijft staan |
| Slaapstand | De pc gaat slapen of in sluimerstand | Wakker worden | Het laatste frame blijft staan |

- **Geen CPU voor effecten:** bij elke reden stopt de timer van de animatie; er draait dan geen
  timer meer, net als bij een statische kleur. Een bewegend effect staat stil op het laatste
  frame. De lampen gaan niet uit.
- **Los van elkaar:** ontgrendelen heft een pauze uit het tray-menu niet op, en hervatten in het
  menu laat de animatie niet lopen zolang Windows vergrendeld is. Pas als alle redenen weg zijn,
  loopt de animatie weer. Eindigt een reden en is er geen pauze uit het tray-menu (meer), dan stuurt
  de engine alle kleuren nog een keer: na van gebruiker wisselen kan de engine van de andere
  gebruiker de lampen veranderd hebben.
- **Bij vergrendelen en slaapstand** gaat een wijziging wel naar de lampen: een profiel dat op
  tijd laadt (bijv. "Nacht" om 23:00 terwijl de pc vergrendeld is) en het herstellen 2 en 10
  seconden na het ontwaken, ook als Windows dan nog op het vergrendelscherm staat. Alleen de
  pauze uit het tray-menu houdt alles tegen tot hervatten. Knipperen gebeurt bij geen enkele
  pauze.
- **Uitzetten:** zet `"pauseOnLockAndSleep": false` in `settings.json` en start de engine opnieuw;
  dan loopt de animatie ook door als Windows vergrendeld is of in slaapstand gaat.
- **In het tray-menu** staat het vinkje bij "Effecten pauzeren" alleen voor de pauze uit het
  menu; hervat je in de instellingen-app, dan verdwijnt het vinkje. De tooltip zegt "effecten
  gepauzeerd" bij elke reden.
- **In de instellingen-app** staat boven het voorbeeld een melding met de reden, bijv.
  "Effecten gepauzeerd via het tray-menu – het voorbeeld toont wat terugkomt na hervatten", met
  de knop "Hervatten". Bij vergrendelen en slaapstand staat er alleen de reden: die pauze eindigt
  met Windows. Een schermlezer leest een nieuwe melding voor. De engine meldt de stand via
  `IEngineApi.GetPauseAsync`, het event `PauseChanged` en `ResumeEffectsAsync`.
- **Hoe de engine het merkt:** via `SystemEvents.SessionSwitch` (vergrendelen, ontgrendelen,
  verbinden en verbreken van de sessie) en `SystemEvents.PowerModeChanged` (slapen en ontwaken),
  op de thread van het tray-icoon (`AutoPause`, `SystemSessionEvents`, `SystemPowerEvents`).
  Windows meldt alleen veranderingen; start de engine terwijl Windows vergrendeld is, dan pauzeert
  hij pas bij de volgende vergrendeling.
- **Gemeten** (Release, gesimuleerde apparaten, zonder OpenRGB, instellingen-app dicht): de hele
  engine met de regenboog op 30 fps ~0,31 % van één kern (~0,02 % van de pc), ~25 MB working
  set en ~12 MB private bytes; met een statische kleur ~0,08 % van één kern (vooral de
  verbindpogingen naar OpenRGB). De render-loop met dezelfde apparaten los gemeten: lopend 910
  frames per 30 s, vergrendeld of in slaapstand 0 frames en geen timer, en daarmee net zo stil
  als een statische kleur.
- **Testen zonder hardware:** `dotnet test --solution BuurmansRgb.slnx`. Voor dit deel:
  `LightingControllerPauseTests` (de redenen naast elkaar, wijzigingen en herstellen tijdens
  vergrendelen, knipperen), `AutoPauseTests` (met nep-events van Windows: vergrendelen,
  verbreken, slaapstand, de pauze uit het menu die blijft, wakker worden op het
  vergrendelscherm met `ResumeRestorer`, uitzetten), `EngineHostTests` en
  `JsonSettingsStoreTests` (`pauseOnLockAndSleep`), `TrayPresenterTests` (vinkje en tooltip),
  `EngineApiTests`, `EngineIpcServerTests` en `EngineRpcTests` (over de pipe), en in de
  instellingen-app `EffectsPauseViewModelTests` en `MainViewModelPauseTests`. Start daarnaast de
  engine en de instellingen-app, vink in het tray-menu "Effecten pauzeren" aan: de melding met
  "Hervatten" verschijnt; klik erop en het vinkje in het menu verdwijnt.
- **Testen op je eigen pc** (vergrendelen en slaapstand zijn niet na te bootsen in de tests):
  1. Kies "Regenboog", vergrendel Windows (Windows-toets+L): de lampen blijven op één frame
     staan. Ontgrendel: de regenboog loopt weer. Kijk in Taakbeheer (tabblad Details, kolom
     CPU-tijd) dat de CPU-tijd van `BuurmansRgb.Engine.exe` tijdens het vergrendelen niet oploopt.
  2. Pauzeer in het tray-menu, vergrendel en ontgrendel: de effecten blijven gepauzeerd en het
     vinkje blijft staan. Hervat in de instellingen-app: het vinkje verdwijnt.
  3. Zet de pc in slaapstand en maak hem wakker: op het vergrendelscherm horen de kleuren binnen
     10 seconden terug te zijn (stilstaand), na het ontgrendelen loopt de animatie weer.
  4. Wissel van gebruiker en kom terug, of verbind met Extern bureaublad en verbreek: de animatie
     stopt en loopt daarna weer.
  5. Zet `"pauseOnLockAndSleep": false`, herstart de engine en vergrendel: de regenboog loopt door.

## OpenRGB koppelen

De echte hardware loopt via de SDK-server van [OpenRGB](https://openrgb.org). BuurmansRgb
zelf heeft geen adminrechten nodig; OpenRGB eenmalig wel, voor moederbord, RAM en GPU.

1. Sluit fabrikantsoftware af die dezelfde LED's aanstuurt (iCUE, Armoury Crate, ...).
2. Start OpenRGB en zet de SDK-server aan: tabblad **SDK Server** → **Start Server**, of start
   OpenRGB met `--server`. BuurmansRgb verbindt met `127.0.0.1:6742`, de standaardpoort.
3. Start de engine (de volgorde maakt niet uit). Binnen enkele seconden staan de apparaten
   van OpenRGB in de instellingen-app, onder de kop "Echte apparaten (OpenRGB)", en
   verdwijnen de gesimuleerde (zie "OpenRGB-status en gesimuleerde apparaten"). Een open
   venster werkt de lijst vanzelf bij.

- Bij het verbinden zet BuurmansRgb elk apparaat in de modus "Direct" of "Custom" (OpenRGB
  kiest welke) en stuurt het de opgeslagen kleur. Een apparaat zonder zo'n modus staat wel
  in de lijst, maar volgt de kleur misschien niet; de instellingen-app waarschuwt daarvoor.
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
- Hoe het met OpenRGB gaat, staat in de instellingen-app en in de tooltip van het tray-icoon.
  De details over verbinden, protocolversie en fouten gaan naar de debug-uitvoer (bekijken
  met DebugView), zolang er nog geen logbestand is.

## OpenRGB-status en gesimuleerde apparaten

- **OpenRGB-status:** in de instellingen-app staat onder "Apparaten" in één regel hoe het met
  OpenRGB gaat; een tip staat achter de ⓘ ernaast (bij aanwijzen of met Tab) en een
  schermlezer leest een nieuwe status voor. Het tray-icoon zegt het in zijn tooltip. De
  engine houdt het bij met de
  verbindpogingen die hij toch al doet (elke 5 seconden zolang er geen verbinding is), dus
  zonder extra werk. De standen (`ServerStatus` in `BuurmansRgb.Core`, via
  `IEngineApi.GetOpenRgbStatusAsync` en het event `OpenRgbStatusChanged`):

  | Wat je ziet | Wat er aan de hand is |
  | --- | --- |
  | OpenRGB: verbinden… | De engine is net gestart, of de verbinding viel weg en de volgende poging komt zo |
  | OpenRGB: verbonden, 5 apparaten | Verbonden; OpenRGB gaf zoveel apparaten |
  | OpenRGB: verbonden, geen apparaten gevonden | Verbonden, maar OpenRGB gaf geen apparaten |
  | OpenRGB: niet bereikbaar | Niets nam de verbinding aan op `127.0.0.1:6742`: OpenRGB draait waarschijnlijk niet, of de SDK-server staat uit. Met een tip: herstart de pc of draai de installer opnieuw |
  | OpenRGB: geen goed antwoord | Er antwoordde wel iets, maar verbinden of de apparaten ophalen mislukte (bijv. een antwoord dat niet klopte of te laat kwam) |

- **Gesimuleerde apparaten:** onder de apparatenlijst kies je bij "Gesimuleerd:" uit
  "Automatisch" (standaard), "Altijd" of "Nooit". Automatisch toont ze alleen
  zolang er geen echt apparaat is: zodra OpenRGB echte apparaten levert, verdwijnen ze, en
  vallen die weg (OpenRGB stopt), dan komen ze terug. De engine wisselt meteen, zonder
  herstart, en bewaart de keuze in `settings.json` (`simulatedDevices`, zie "Engine
  starten"). In de engine doet `SimulatedDevicesSwitch` dit; die volgt de apparatenlijst en
  beslist bij elke wijziging van de lijst of de keuze opnieuw, onder één lock, dus ook als
  OpenRGB en de keuze tegelijk veranderen klopt de lijst daarna.
- **Apparaat zonder "Direct"- of "Custom"-modus:** zo'n apparaat (vaak een videokaart)
  krijgt in de lijst "⚠ Volgt de kleur misschien niet", en het voorbeeld legt uit dat het in
  OpenRGB geen modus heeft om kleuren per LED over te nemen: BuurmansRgb stuurt de kleuren
  wel, maar het apparaat kan zijn eigen effect blijven tonen; stel het dan in OpenRGB zelf in
  (`DeviceInfo.HasDirectMode`).
- **Testen zonder hardware:** `dotnet test --solution BuurmansRgb.slnx`. Voor dit deel:
  `SimulatedDevicesSwitchTests` (automatisch, altijd, nooit, en OpenRGB en de keuze die
  tegelijk veranderen), `JsonSettingsStoreTests` (`true`/`false` en de woorden),
  `LightingSettingsSaverTests` en `EngineHostTests` (de keuze bewaren),
  `OpenRgbProviderStatusTests` en `OpenRgbProviderServerTests` (de standen met nepdata en een
  kleine SDK-server), `OpenRgbNetConnectionTests` (niet bereikbaar tegenover geen antwoord),
  `OpenRgbDeviceMapperTests` en `OpenRgbDeviceProviderTests` (geen "Direct"-modus),
  `EngineApiTests`, `EngineIpcServerTests` en `EngineRpcTests` (over de pipe),
  `TrayPresenterTests` (tooltip) en in de instellingen-app
  `SimulatedDevicesViewModelTests`, `OpenRgbStatusViewModelTests`, `DeviceItemTests`,
  `DevicePreviewViewModelTests` en `MainViewModelTests`. Start daarnaast de engine en de
  instellingen-app zonder OpenRGB: er staat "OpenRGB: niet bereikbaar" met de tip achter de
  ⓘ, en met "Nooit" verdwijnen de gesimuleerde apparaten meteen. Ook in het kleinste venster
  (640 × 520) houdt de apparatenlijst ruimte voor drie apparaten; de lijst en de profielen
  delen de rest van de hoogte.
- **Testen met je eigen hardware:**
  1. Start de engine en de instellingen-app met de OpenRGB-service uit: "niet bereikbaar" met
     de tip, en de gesimuleerde apparaten staan er. Start de service: binnen 5 seconden staat er
     "verbonden" met het aantal apparaten, verschijnen de echte apparaten en verdwijnen de
     gesimuleerde. Stop de service weer: de gesimuleerde komen terug.
  2. Kies "Altijd" en "Nooit" en kijk of de lijst meteen meeverandert; herstart de engine en
     kijk of de keuze bewaard is.
  3. Kijk in de tooltip van het tray-icoon of hij hetzelfde zegt.
  4. Heeft een apparaat geen "Direct"-modus in OpenRGB (vaak een videokaart), kijk dan of het
     de waarschuwing krijgt, en noteer in `docs/hardware.md` of het de kleur toch volgt.

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
  `settings.json` verandert daardoor niet. Onder de naam staat het SMBus-adres dat OpenRGB
  meegeeft, bijv. "Adres 0x52". Zo zijn de sticks uit elkaar te houden; welke welke is in de pc,
  zie je samen met "Laat knipperen" (zie "Welk apparaat is wat?").
- **Na slaapstand** gaan veel moederborden en RAM-sticks terug naar hun eigen effect. De
  engine luistert naar `PowerModeChanged` en herstelt 2 en 10 seconden na het ontwaken: hij
  laadt de OpenRGB-apparaten opnieuw (dat zet ze weer in "Direct" of "Custom") en stuurt alle
  kleuren opnieuw. Zijn de effecten via het tray-menu gepauzeerd, dan gebeurt dat pas bij
  hervatten; is Windows na het ontwaken nog vergrendeld, dan gebeurt het gewoon (zie "Pauzeren").
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
- **Twee dezelfde kaarten** krijgen een nummer in de volgorde van OpenRGB, net als RAM-sticks,
  en net als daar staat onder de naam wat hun locatie in OpenRGB onderscheidt. Bij twee kaarten
  is dat meestal niet het adres maar de bus van elke kaart, bijv. "Op NVIDIA GPU 1".
- **Zonder "Direct"-modus:** veel kaarten kennen in OpenRGB alleen hun eigen effecten
  (bijv. "Static" en "Breathing"). Zo'n kaart staat wel in de lijst, maar volgt de kleur
  misschien niet; de instellingen-app waarschuwt daarvoor (zie "OpenRGB-status en
  gesimuleerde apparaten").
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
- **Fans als ring:** het voorbeeld tekent elke zone van een ventilator (in OpenRGB het type
  "cooler", dus ook een AIO-pomp) als een ring van bolletjes, met LED 1 bovenaan en dan met de
  klok mee. Een fan met meer ringen (bijv. binnen- en buitenring als eigen zones) krijgt een
  ring per zone. OpenRGB zegt niet waar de eerste LED van een fan zit of welke kant hij op
  telt, en heeft geen zonetype voor een ring; de wijzerplaat is dus een keuze, geen kaart van
  je fan. Alleen het apparaattype telt: een zone "Fan" op een videokaart of een ARGB-header met
  een fan erop blijft een rij, omdat de naam daar niet betrouwbaar zegt wat erop zit. Ook een
  zone waarvan je de lengte instelt blijft een rij: dat is een kanaal van een hub die OpenRGB
  ook "cooler" noemt (bijv. een Lian Li Uni Hub of Corsair Commander), met een strip of meer
  fans achter elkaar erop. Hetzelfde geldt voor een zone van meer dan 48 LED's, meer dan één
  fan-ring heeft. Klikken, Shift+klik en de zonenaam werken zoals in een rij; een klik naast of
  midden in de ring kiest niets.
- **Lengte instellen:** bij een zone die OpenRGB in lengte laat instellen (een ARGB-header of
  -kanaal) staat onder de LED's "Aantal LED's (0–120):" met het bereik van die zone, en daaronder
  − en +, een schuif, een vak en "Toepassen". Met − en +, de schuif of de pijltjes op de schuif
  groeit of krimpt het voorbeeld meteen; LED's die er nog bij komen zijn donker tot de zone ze
  heeft. De zone zelf krijgt de nieuwe lengte pas als je 0,6 seconde niets verandert of de schuif
  loslaat: OpenRGB laadt bij elke nieuwe lengte zijn apparaten opnieuw, dus tien keer op + of een
  sleep over de schuif is één aanpassing. Sluit je het venster of kies je een ander apparaat
  voordat het zover is, dan gaat de lengte meteen. Aan het eind van het bereik blijven − en +
  aan, maar veranderen ze niets meer. Een getal typen werkt zoals eerst: typ het en kies
  "Toepassen" (of Enter); dat gaat meteen. Heeft zo'n zone 0 LED's, dan staat er "Nog geen LED's
  ingesteld" met een korte uitleg erbij, en die verdwijnt zodra je er LED's bij zet. Daarna laadt
  BuurmansRgb de apparaten opnieuw, stuurt het de kleuren weer, en staat de focus van het
  toetsenbord weer op de knop of schuif waar hij stond. Bij een apparaat met één zone, zoals de
  gesimuleerde strip (1–60 LED's), staat het er ook, zonder zonenaam.
- **Lengtes bewaren:** een lengte die je in BuurmansRgb instelt, bewaart de engine in
  `settings.json` (`zoneLengths`, per apparaat op de naam van de zone). Bij het starten en elke
  keer dat een apparaat (weer) verschijnt, bijvoorbeeld als OpenRGB opnieuw verbonden is, krijgt
  een zone met een andere lengte de bewaarde lengte terug; ook de gesimuleerde apparaten houden zo
  hun lengte na een herstart. Op naam en niet op volgorde, omdat OpenRGB de zones in een andere
  volgorde kan geven; een zone waarvan de naam weg is, of die twee zones van het apparaat delen,
  blijft met rust. De engine vraagt een lengte alleen als het apparaat verschijnt, en wacht dan tot
  het apparaat die lengte meldt: neemt OpenRGB hem niet over, dan blijft het bij één poging (tot
  het apparaat opnieuw verschijnt) in plaats van een lus van aanpassingen. Een lengte buiten het
  bereik dat de zone nu meldt, blijft bewaard maar wordt niet gevraagd. Een apparaat dat er (nog)
  niet is, krijgt zijn lengte zodra het verschijnt. Verander je de lengte in OpenRGB zelf terwijl
  de engine draait, dan neemt BuurmansRgb die over als de nieuwe bewaarde lengte, behalve 0: dat
  lijkt eerder een vergeten lengte dan een keuze, en bij het volgende verschijnen van het apparaat
  komt de bewaarde lengte terug.
- **Per-LED-kleuren en zonelengtes:** `settings.json` (en `profiles.json`) bewaart bij de kleuren
  per LED ook de zonelengtes waarvoor je ze koos (`ledColorZoneLengths`). Heeft een zone nu een
  andere lengte, ook bij de eerste ontdekking na een start of als OpenRGB een lengte even
  vergeten is, dan houdt elke zone zijn eigen kleuren: een langere zone krijgt er LED's in de
  kleur van het apparaat bij, een kortere toont er minder, en de zones erna schuiven mee. De
  bewaarde kleuren zelf veranderen daarbij niet, dus krijgt een zone zijn lengte terug, dan zijn
  al zijn kleuren er weer. Pas als je kleuren van dat apparaat verandert, bewaart de engine ze
  voor de lengtes van dat moment; de kleuren van LED's die een zone dan mist, zijn weg (behalve
  aan het eind van de laatste zone). Kleuren uit een ouder bestand, zonder zonelengtes, horen bij
  de lengtes die het apparaat heeft als de engine het voor het eerst ziet.
- **Een ring of zone kiezen:** klik in het voorbeeld op de naam van een zone, bijv. "Outer
  ring" van een fan of een kanaal van een controller. Alle LED's van die zone zijn dan
  geselecteerd in de per-LED-editor, en de kleur die je kiest geldt alleen voor die zone.
- **Na slaapstand** herstelt de engine ook fans en strips, zoals beschreven bij "Moederbord en
  RAM". Een ingestelde lengte blijft na het ontwaken gelijk: OpenRGB houdt hem vast zolang het
  draait. Vergeet OpenRGB hem na een herstart, dan zet BuurmansRgb de bewaarde lengte terug zodra
  het weer verbonden is (zie "Lengtes bewaren"); test dat met je eigen hardware (stap 1 hieronder).
- **Testen zonder hardware:** `dotnet test --solution BuurmansRgb.slnx`. Voor dit deel:
  `RgbZoneTests`, `RgbDeviceTests` en `SimulatedDeviceProviderTests` (bereik en lengte van zones),
  `OpenRgbDeviceMapperTests`, `OpenRgbDeviceProviderTests` en `OpenRgbNetConnectionTests`
  (het type behuizing, lengte instellen bij OpenRGB met nepdata en een kleine SDK-server, ook
  na herstellen), `LightingControllerZoneTests` en `LedColorStoreTests` (lengte instellen via de
  engine, per-LED-kleuren die de zones volgen, ook bij de eerste ontdekking na een start),
  `ZoneLengthKeeperTests` (lengtes teruggeven: één keer per verschijnen, geen lus als het apparaat
  weigert, zones op naam, buiten het bereik, een apparaat dat later komt, een lengte uit OpenRGB zelf
  overnemen behalve 0),
  `ZoneLengthKeeperOpenRgbTests` (hetzelfde met de nep-OpenRGB-server: verbinden, een herstart
  die de lengte vergeet, opnieuw zoeken), `EngineHostTests` (lengtes en per-LED-kleuren na een
  herstart met de gesimuleerde apparaten), `JsonSettingsStoreTests` en `JsonProfileStoreTests`
  (`zoneLengths` en `ledColorZoneLengths`), `SwitchableDeviceProviderTests`, `EngineApiTests`,
  `EngineRpcTests` en in de instellingen-app `ZoneLengthViewModelTests` (−, +, schuif, slepen,
  typen en de pauze voor het versturen, met een nepklok), `PreviewViewModelTests` (een lengte die
  nog niet verstuurd is blijft staan als de engine de apparaten opnieuw meldt),
  `FocusKeeperTests` en `SliderDragTests` (de focus na het opnieuw opbouwen, en het begin en eind
  van een sleep over de schuif, met een echt venster buiten beeld),
  `DeviceItemTests`, `DeviceIconsTests`, `DevicePreviewViewModelTests` (welke zones een ring zijn,
  het voorbeeld dat meegroeit, de uitleg bij een lege zone), `RingLayoutTests`,
  `LedArrangementTests` en `LedPreviewViewTests` (de ring: plek van elke LED, klikken op en
  naast een LED), `LedEditorViewModelTests` en `MainViewModelLedEditorTests`.
  Start daarnaast de engine en de instellingen-app: kies "Simulated ARGB controller", klik bij
  "ARGB 2" een paar keer op + of sleep de schuif (het voorbeeld groeit meteen, de zone even later),
  of typ 15 en kies "Toepassen". Sluit de engine af (tray, "Afsluiten") en start hem opnieuw:
  "ARGB 2" heeft nog steeds die lengte. Of klik bij "Simulated dual-ring fan" op "Outer ring" en
  kies een kleur. "Simulated fan" en de twee ringen van "Simulated dual-ring fan" staan als ring
  in het voorbeeld; klik op een LED en Shift+klik op een LED verderop.
- **Testen met je eigen hardware:** volg "Een apparaattype testen met je eigen hardware" voor
  elke fan, strip en controller, en daarnaast:
  1. Stel bij een ARGB-header of -kanaal het aantal LED's in met + of de schuif en controleer
     dat de hele strip (en niet meer) oplicht; kijk ook of de echte strip pas na het loslaten
     verandert. Herstart OpenRGB (of de pc) en kijk of de strip binnen een paar seconden na het
     verbinden weer de goede lengte heeft, en of hij daarna niet blijft knipperen of herladen.
  2. Geef de zones van een controller met meer kanalen elk een eigen kleur (per LED), herstart
     OpenRGB en kijk of elke zone zijn eigen kleuren terugkrijgt.
  3. Klik bij een fan met meer ringen op de naam van een ring en geef alleen die ring een
     kleur.
  4. Geef LED 1 van een fan-ring een kleur en kijk waar die op de echte fan zit en welke kant
     LED 2 op gaat; het voorbeeld zet LED 1 bovenaan en telt met de klok mee.
  5. Noteer in `docs/hardware.md` welke controller het is, hoeveel LED's er per kanaal zitten,
     of het type klopt (fan, strip of behuizing), waar LED 1 van een ring zit en of OpenRGB de
     lengte na een herstart vergat.

## Toetsenbord, muis en headset

Toetsenbord, muis en headset zijn het laatste apparaattype (taak 8). OpenRGB bereikt ze via
USB, niet via SMBus.

- **In de lijst** heeft elk type een eigen icoon en naam: "Toetsenbord", "Muis" en "Headset".
  Ook de andere types van OpenRGB hebben er een: "Muismat", "Headsetstandaard", "Speaker",
  "Gamepad", "Microfoon" en "Keypad". Microfoon en keypad kent OpenRGB.NET 3.1.1 (de nieuwste
  versie) niet bij naam; BuurmansRgb herkent ze aan hun nummer, net als "case". Lampen,
  virtuele apparaten, "accessory" en "storage" staan zonder type in de lijst: "storage" heeft
  hetzelfde nummer als "unknown" van oudere OpenRGB-versies, dus dat valt niet uit elkaar te
  houden.
- **Toetsenbord als matrix:** bij veel toetsenborden geeft OpenRGB per zone een matrix-map:
  rijen en kolommen met in elk vakje de LED van de toets die daar zit. Het voorbeeld tekent
  dan elke LED als toets op die plek, en maakt de toetsen kleiner als het venster te smal is.
  LED's die wel in de zone zitten maar niet in de map (bijv. een logo), staan in een rij onder
  de toetsen. Een zone zonder map, zoals een lichtbalk, blijft een rij bolletjes. Klik op een
  toets om hem in de per-LED-editor een eigen kleur te geven. Shift+klik selecteert een reeks
  in de LED-volgorde van OpenRGB; die loopt vaak rij voor rij, maar verschilt per toetsenbord.
  Regenboog en Golf lopen ook over die LED-volgorde, niet over de plek van de toetsen.
- **Toetsnamen:** OpenRGB geeft elke LED een naam, bij een toetsenbord bijv. "Key: Escape".
  Wijs je in het voorbeeld een LED of toets aan, dan noemt een tooltip hem met zijn nummer,
  bijv. "Escape (LED 1)" (zonder "Key: "), of "LED 5" als hij geen naam heeft. Een naam die
  alleen een eigen nummer is, zoals "LED 3" of "Aura Addressable 1 LED 3", telt als geen naam:
  OpenRGB telt die per zone of apparaat, en naast het nummer in het apparaat verwart dat alleen.
  Een schermlezer noemt de LED's zo ook, en kan ze selecteren zoals een klik (en met "toevoegen
  aan de selectie" zoals Shift+klik). De namen gaan alleen mee naar de instellingen-app als
  OpenRGB echte namen geeft; het gesimuleerde toetsenbord heeft de namen van een gewoon
  Amerikaans toetsenbord.
- **Brede toetsen:** in de matrix-map heeft ook de spatiebalk maar één vakje, met lege vakjes
  ernaast. Met toetsnamen tekent het voorbeeld Tab, Caps Lock, linker Shift en Ctrl en de 0 van
  het numerieke blok breder naar rechts, Backspace, Enter, rechter Shift en Ctrl breder naar
  links, en de spatiebalk naar beide kanten, telkens over de lege vakjes tot de volgende toets
  in de rij. Lege vakjes aan het eind van een rij blijven leeg, en twee brede toetsen met lege
  vakjes ertussen delen die. Een klik op het bredere deel kiest die toets. Zonder namen, of
  zonder lege vakjes ernaast, blijft elke toets één vakje. Hoge toetsen (Enter en + van het
  numerieke blok) blijven één vakje hoog.
- **Muis:** het voorbeeld toont de zones zoals OpenRGB ze geeft (bijv. logo, scrollwiel en
  zijkanten). Klik op de naam van een zone om alleen die zone een kleur te geven.
- **Headset:** OpenRGB ondersteunt maar een deel van de headsets; kijk in de lijst "Supported
  Devices" op [openrgb.org](https://openrgb.org). Een headset die OpenRGB niet kent, staat niet
  in OpenRGB en dus ook niet in BuurmansRgb. BuurmansRgb heeft geen eigen driver voor headsets
  en krijgt er ook geen; noteer zo'n headset in `docs/hardware.md` als "niet ondersteund door
  OpenRGB". Een headsetstandaard heet in OpenRGB geen headset en staat als "Headsetstandaard"
  in de lijst.
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
  venster) en `DevicePreviewViewModelTests` in de instellingen-app. Voor de toetsnamen en
  brede toetsen: `RgbZoneTests` en `RgbDeviceTests` (namen in een zone, gelijkheid en JSON
  zonder namen), `OpenRgbNetConnectionTests` en `OpenRgbDeviceMapperTests` (namen per zone,
  zonder "Key: "), `SimulatedDeviceProviderTests` (elke toets de naam van zijn plek),
  `EngineRpcTests` (namen over de pipe), `WideKeysTests` en `LedPreviewViewTests`
  (de items voor UI Automation, selecteren zoals een klik, meldingen aan een schermlezer). Voor
  de types: `DeviceTypeTests` (vaste nummers over de pipe), `DeviceItemTests`,
  `DeviceIconsTests` en `OpenRgbDeviceMapperTests`.
  Start daarnaast de engine en de instellingen-app: kies "Simulated keyboard", wijs een toets
  aan voor zijn naam en klik op een toets (ook op het brede deel van de spatiebalk), of kies
  bij "Simulated mouse" de zone "Scroll wheel".
- **Testen met je eigen hardware:** volg "Een apparaattype testen met je eigen hardware" voor
  het toetsenbord, de muis en de headset, en daarnaast:
  1. Vergelijk het voorbeeld van het toetsenbord met het echte toetsenbord: klik op bijv. Esc,
     W, A, S, D en de spatiebalk, geef ze een kleur en controleer dat precies die toetsen
     oplichten. Kijk of de tooltip de goede naam geeft, en of de brede toetsen (spatiebalk,
     Enter, Shift, Backspace) breed staan en niet over een andere toets of blok heen lopen.
  2. Geef bij de muis één zone een eigen kleur en controleer dat alleen die zone verandert.
  3. Staat de headset niet in OpenRGB, noteer dan het model in `docs/hardware.md`.
  4. Noteer in `docs/hardware.md` per apparaat het model en wat werkt.

## Welk apparaat is wat?

Met een paar dezelfde fans, een ledstrip op een ARGB-header en RAM-sticks met dezelfde naam is
in de lijst niet altijd te zien welk apparaat of welke zone in de pc zit.

- **Knipperen:** kies je in de lijst een apparaat (niet "Alle apparaten"), dan knippert het op de
  echte hardware 1,5 seconde: drie keer fel wit en uit, ook als de helderheid laag staat. Daarna
  krijgt het zijn eigen verlichting terug. In het voorbeeld staat bij elk apparaat de knop "Laat
  knipperen" om het nog eens te doen, ook bij "Alle apparaten". Er knippert steeds één apparaat:
  kies je snel een volgend, dan stopt het vorige meteen. Knipperen verandert niets aan de
  verlichting en wordt niet opgeslagen. Bij het verbinden of als de lijst ververst, knippert er
  niets. Zijn de effecten gepauzeerd, dan knippert er ook niets; pauzeer je tijdens het
  knipperen of sluit je de engine af, dan krijgt het apparaat eerst zijn eigen kleuren terug.
  Een apparaat dat de engine niet (meer) kent, wordt overgeslagen. De engine draait alleen
  tijdens het knipperen een timer (`IEngineApi.FlashDeviceAsync`, in de engine
  `LightingController.Flash` en `DeviceFlash`).
- **Echt of gesimuleerd:** de lijst toont de echte apparaten onder de kop "Echte apparaten
  (OpenRGB)" en daaronder de gesimuleerde onder "Gesimuleerde apparaten". De provider geeft elk
  apparaat zijn bron mee (`DeviceInfo.Source`). Standaard staan de gesimuleerde er alleen
  zolang er geen echte zijn (zie "OpenRGB-status en gesimuleerde apparaten").
- **Plek:** staan er twee of meer apparaten van hetzelfde type en met dezelfde naam (op het
  volgnummer na) in de lijst, zoals de RAM-sticks van een kit, dan staat onder de naam het deel
  van hun OpenRGB-locatie dat verschilt: het adres ("Adres 0x52"), of als dat gelijk is de bus
  ("Op NVIDIA GPU 1"). Dat houdt ze uit elkaar; welke welke is in de pc, zie je samen met "Laat
  knipperen". De hele locatie staat in de tooltip van elk apparaat dat er een heeft
  (`DeviceInfo.Location`, in de app `DeviceLocationLabels`).
- **Lege ARGB-header:** een zone die in lengte instelbaar is maar 0 LED's heeft, zegt in het
  voorbeeld "Nog geen LED's ingesteld" en legt uit dat je het aantal LED's van de fan of strip
  instelt met + en −, de schuif, of door het te typen en "Toepassen" te kiezen. Zet je er LED's
  bij, dan verdwijnt de uitleg meteen.
- **Testen zonder hardware:** `dotnet test --solution BuurmansRgb.slnx`. Voor dit deel:
  `LightingControllerFlashTests` (knipperen met een nepklok: wit en uit, daarna de eigen kleur
  en geen timer meer, pauze en herstellen na slaapstand), `EngineIpcServerTests` en
  `EngineRpcTests` (knipperen, bron en locatie over de pipe), `OpenRgbDeviceMapperTests` en
  `OpenRgbDeviceProviderTests` (SMBus-adressen van RAM-sticks en twee videokaarten met nepdata),
  `SimulatedDeviceProviderTests`, `DeviceInfoTests`, `RgbDeviceTests` en in de instellingen-app
  `DeviceItemTests`, `DeviceLocationLabelsTests`, `DevicePreviewViewModelTests` en
  `MainViewModelTests`. Start daarnaast de engine en de instellingen-app: de gesimuleerde
  apparaten staan onder hun eigen kop, het voorbeeld van een gekozen apparaat knippert kort wit,
  en bij "Simulated ARGB controller" staat bij "ARGB 2" de uitleg voor een lege zone. De
  gesimuleerde apparaten hebben geen locatie.
- **Testen met je eigen hardware:**
  1. Kies elk apparaat in de lijst en kijk of precies dat apparaat in de pc knippert en daarna
     zijn eigen kleur terugkrijgt, ook bij een bewegend effect.
  2. Pauzeer de effecten in het tray-menu en kies een apparaat: er hoort niets te knipperen.
  3. Kijk of de RAM-sticks (en eventueel twee dezelfde videokaarten) een verschillend adres of
     een verschillende bus tonen, en zoek met "Laat knipperen" uit welke welke is. Noteer in
     `docs/hardware.md` wat OpenRGB als locatie geeft (tooltip).
  4. Zet bij een ARGB-header met 0 LED's het aantal LED's van je fan of strip en controleer met
     "Laat knipperen" dat de hele fan of strip oplicht.

## Profielen

Een profiel bewaart de verlichting van alle apparaten onder een naam (opslaan, laden en
verwijderen in de instellingen-app, zie "Instellingen-app starten"). Laden kan ook zonder de
instellingen-app: vanuit het tray-menu, elke dag op een tijd, of zolang een programma draait.

- **Vanuit het tray-menu:** "Profiel laden" toont de opgeslagen profielen; klik er een aan om
  het te laden. Zonder profielen staat er "Nog geen profielen – maak ze in Instellingen". De
  engine leest de lijst als het menu opengaat, dus ook een profiel dat je net in de
  instellingen-app opsloeg of verwijderde klopt. Kan `profiles.json` niet gelezen worden
  (bijvoorbeeld omdat een ander programma het open heeft), dan is "Profiel laden" grijs en meldt
  het tray-icoon dat; de engine draait gewoon door. Ook de eerste seconden na het starten, tot
  de engine de opgeslagen verlichting heeft gezet, is "Profiel laden" grijs: anders zou die
  verlichting een gekozen profiel meteen overschrijven.
- **Op tijd:** klap in de instellingen-app onder de profielen "Automatisch laden" open, kies een
  profiel en vul bij "Automatisch laden om" een tijd in (bijv. `23:00` of `23.00`) en kies
  "Toepassen" (of Enter). Elke dag om die tijd laadt de engine het profiel, net alsof je op
  "Laden" klikte: het wordt ook de opgeslagen verlichting. Leeg en "Toepassen" zet het uit. Er
  loopt één timer, alleen als minstens één profiel een tijd heeft, en die gaat precies op de
  volgende tijd af. Die tijd is de klok van Windows: bij de overgang naar zomer- of wintertijd
  blijft 23:00 gewoon 23:00. Een tijd in het uur dat bij zomertijd wegvalt (bijv. 02:30) komt
  als dat uur voorbij is (03:00); een tijd in het uur dat bij wintertijd twee keer komt, alleen
  de eerste keer. Slaapt de pc op dat moment, staat hij uit of zet je de klok erover heen, dan
  slaat de engine die keer over en wacht hij op de volgende dag: na een nacht slaapstand hoort
  het "nacht"-profiel 's ochtends niet alsnog te komen. Gaat de timer meer dan een minuut te laat
  af, dan telt dat ook als gemist. Na het ontwaken en na een andere tijd of tijdzone rekent de
  engine de volgende tijd opnieuw uit. Hebben twee profielen dezelfde tijd, dan laadt het eerste
  op alfabet. Komt de tijd terwijl een programma zijn profiel toont (zie hieronder), dan
  blijft dat profiel staan en komt het profiel van de tijd zodra het programma stopt: 23:00
  midden in een spel laat het spel met rust. Heb je tijdens het spel zelf iets veranderd, dan komt
  het profiel van de tijd meteen.
- **Bij een programma (spel):** vul bij "Laden als dit programma start" de naam van het
  .exe-bestand in (bijv. `eldenring.exe`; `eldenring` of een heel pad mag ook, de app maakt er
  `eldenring.exe` van). Een snelkoppeling (`.lnk`, `.url`) of script (`.bat`, `.cmd`) weigert de
  app: dat is niet het programma dat draait; het doel van de snelkoppeling staat bij
  Eigenschappen. Start het programma, dan laadt de engine het profiel binnen 2 seconden.
  Stopt het, dan komt de verlichting van vóór de start terug, tenzij die intussen veranderde (in
  de instellingen-app of vanuit het tray-menu): dan blijft die. Een tijd die intussen kwam, komt
  dan (zie "Op tijd"). Starten er twee
  programma's met een profiel, dan toont het laatste zijn profiel; stopt dat, dan komt het
  profiel van het eerste terug, en als beide gestopt zijn de verlichting van daarvoor. Draait
  het programma al als de engine start, dan laadt de engine het profiel meteen en gaat hij bij het
  stoppen terug naar de opgeslagen verlichting. Een programma dat al draait als je het instelt,
  telt pas bij zijn volgende start. Sluit je de engine af terwijl het programma draait (bijv. bij
  het afsluiten van Windows met Steam nog open), dan zet de engine eerst de verlichting van
  daarvoor terug en bewaart hij die, zodat die na een herstart niet kwijt is. Hebben twee
  profielen hetzelfde programma, dan laadt het eerste op alfabet.
- **Hoe de engine programma's ziet:** zonder adminrechten is er geen melding van Windows als
  een programma start (`Win32_ProcessStartTrace` vraagt admin). Daarom kijkt de engine elke 2
  seconden in de proceslijst, en alleen zolang minstens één profiel een programma heeft. Hij
  gebruikt `EnumProcesses` en zoekt de naam van een proces één keer op, als het nieuw is: zo'n
  ronde kost minder dan 0,1 ms CPU (gemeten met ~470 processen), waar `Process.GetProcessesByName`
  en een Toolhelp-snapshot 5 tot 7 ms per keer kosten, omdat ze ook alle threads van alle
  processen lezen. WMI (`__InstanceCreationEvent WITHIN n`) laat het werk alleen door de
  WMI-service doen, die ook de proceslijst afloopt, en haalt `System.Management` in de engine.
  Programma's die als administrator draaien, ziet de engine ook. Gemeten aan de hele engine
  (Release, gesimuleerde apparaten, zonder OpenRGB, in CPU-cycli per minuut): zonder regels en
  met een tijd net zoveel als vóór deze functies (~220 miljoen, vooral de verbindpogingen naar
  OpenRGB), met een programma ~100 miljoen meer, ongeveer 0,05 % van één kern. Het geheugen van
  de namen is ~0,3 MB.
- **Geheugen en starten:** staat er een `profiles.json`, dan leest de engine het bij het starten
  één keer, om de tijden en programma's te weten, en houdt hij alle profielen in het geheugen.
  Dat kost weinig: tien profielen met een tijd en een eigen kleur voor elk van de 246 gesimuleerde
  LED's (een bestand van 34 KB) nemen ~51 KB op de heap, ~5 KB per profiel; aan de hele engine
  gemeten 0,1 tot 0,35 MB meer private bytes, binnen de meetruis. Zonder bestand leest de engine
  niets tot je het tray-menu opent of de instellingen-app de profielen vraagt.
- **In `profiles.json`** staan bij een profiel de lichtvelden van `settings.json`, en als het
  vanzelf laadt `loadAt` (`"HH:mm"`) en/of `program`:

  ```json
  {
    "profiles": [
      { "name": "Gamen", "color": "#FF0000", "effect": "rainbow", "program": "eldenring.exe" },
      { "name": "Nacht", "color": "#FF8800", "brightness": 0.1, "loadAt": "23:00" },
      { "name": "Werk", "color": "#FFFFFF", "devices": { "simulated-fan": { "color": "#0000FF" } } }
    ]
  }
  ```

  Zonder `loadAt` en `program` laadt een profiel nooit vanzelf; oudere bestanden werken dus
  gewoon. Een tijd of programma dat niet te lezen is (bijv. `"25:00"` of een getal), laat de
  engine weg; de rest van het profiel en de andere profielen blijven gelden.
- **Testen zonder hardware:** `dotnet test --solution BuurmansRgb.slnx`. Voor dit deel:
  `TrayPresenterTests` (het submenu, een onleesbaar bestand en een profiel dat net verwijderd
  is), `ProfileLibraryTests` en `JsonProfileStoreTests` (opslaan, oude en kapotte waarden),
  `DailyTimeTests` (de volgende tijd, ook rond zomer- en wintertijd), `ProfileTimerTests` (met
  een nepklok waarvan de timers en de klok los kunnen bewegen: op tijd, elke dag, slaapstand,
  klok vooruit en terug, wintertijd, geen timer zonder tijden), `ProfileTriggersTests` (een tijd
  tijdens een spel), `ProgramWatcherTests` (met een nep-proceslijst: starten, stoppen, terug naar
  daarvoor, iets veranderd, twee programma's, de engine die stopt of start terwijl een programma
  draait), `ProcessListTests` (de echte proceslijst, met een eigen kopie van `ping.exe` die start
  en stopt), `ProgramNamesTests`, `EngineApiTests`, `EngineIpcServerTests` en `EngineRpcTests`
  (over de pipe), `EngineHostTests`, `SystemClockEventsTests` en in de instellingen-app
  `ProfileAutomationViewModelTests`, `ProfilesViewModelTests` en `MainViewModelProfileTests`. Start daarnaast de engine en de
  instellingen-app: sla twee profielen op, open het tray-menu en kies er een; zet bij een profiel
  een tijd een minuut verder en wacht; zet bij een profiel `notepad.exe`, start Kladblok en sluit
  het weer.
- **Testen met je eigen hardware:**
  1. Maak profielen "Werk", "Nacht" en "Gamen" en wissel via het tray-menu; kijk of alle
     apparaten meegaan.
  2. Geef "Nacht" een tijd een paar minuten verder en wacht. Zet daarna een tijd, laat de pc
     erover heen slapen en kijk dat het profiel na het ontwaken niet alsnog komt.
  3. Geef "Gamen" de naam van je spel, start het spel en sluit het weer: de verlichting van
     daarvoor hoort terug te komen. Verander tijdens het spel iets en kijk dat dat blijft.
  4. Start een spel dat als administrator draait of anti-cheat heeft, en kijk of de engine het
     ziet.
  5. Sluit de engine af (tray, "Afsluiten") terwijl het spel draait: de verlichting van daarvoor
     hoort terug te komen. Start de engine weer: het spelprofiel komt terug, en na het sluiten
     van het spel weer de verlichting van daarvoor.

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

## Installer (taak 9)

`installer\build.ps1` bouwt `artifacts\installer\out\BuurmansRgb-setup.exe` (Inno Setup 7.1,
ongeveer 77 MB): engine en instellingen-app self-contained, de MSI van OpenRGB 1.1 (installeert
de service `OpenRGB`) en de setup van PawnIO 2.2.0. Het script controleert de SHA-256 van beide
downloads en haalt de Inno Setup-compiler van NuGet. Alles komt in `artifacts\installer`, dat
git negeert. Met `-Iscc <pad>` gebruik je een eigen `ISCC.exe`, met `-Version 1.2.3` een andere
versie. Hoe het werkt en waarom: `docs/openrgb-meeleveren.md`.

- **Na installeren** start de installer de engine als de gewone gebruiker met `--first-run`.
  Die zet "Starten met Windows" aan, alleen als dat nog uit staat. Zonder `--first-run`
  verandert er niets.
- **Verwijderen** stopt de engine, haalt de autostart weg en vraagt dan of OpenRGB en PawnIO
  ook weg moeten (alleen als deze installer ze heeft geïnstalleerd), en of je instellingen weg
  moeten. Stil verwijderen (`/VERYSILENT /SUPPRESSMSGBOXES`) beantwoordt alles met "Nee".
- **Testen in Windows Sandbox**, zonder iets op je eigen pc te installeren:
  1. Zet eenmalig Windows Sandbox aan (Windows-onderdelen → "Windows Sandbox", vraagt een
     herstart).
  2. Draai `.\installer\build.ps1` en dubbelklik daarna op
     `artifacts\installer\BuurmansRgb-test.wsb`.
  3. De Sandbox installeert stil, controleert de service, poort 6742, de engine, de autostart
     en de verbinding, verwijdert weer en controleert opnieuw. De uitkomst staat in
     `artifacts\installer\sandbox-results\result.json`, met `setup.log`, `uninstall.log` en
     `transcript.txt` erbij. In de Sandbox is er geen RGB-hardware, dus 0 apparaten via OpenRGB
     is normaal.
- **Tests zonder installer:** `EngineOptionsTests` en `AutoStartExtensionsTests` testen
  `--first-run` met een nep-autostart, nooit tegen de echte Run-sleutel.

## Instellingen-app starten

De instellingen-app (`BuurmansRgb.UI`, WPF) staat na het bouwen naast
`BuurmansRgb.Engine.exe` in `artifacts/app/<Debug|Release>/`. Start hem via het tray-menu
"Instellingen openen", of los:

```bash
dotnet run --project src/BuurmansRgb.UI
```

- **Links** de apparatenlijst, met daarboven hoe het met OpenRGB gaat, bovenaan "Alle
  apparaten" en per apparaat een icoon en het type, de echte en de gesimuleerde apparaten elk
  onder een eigen kop. Onder de lijst kies je wanneer de gesimuleerde apparaten er zijn (zie
  "OpenRGB-status en gesimuleerde apparaten"). Een apparaat dat je kiest, knippert even op de
  hardware (zie "Welk apparaat is wat?"). **Rechts** de kleur (hex-code, vaste kleuren,
  schuiven voor tint en verzadiging), de helderheid (0–100%), het effect, de snelheid en een
  voorbeeld met een bolletje per LED, per zone. Een toetsenbord met een matrix-map toont een
  toets per LED, op zijn plek (brede toetsen breder), en een fan een ring per zone. Wijs een
  LED aan voor zijn naam en nummer (zie "Toetsenbord, muis en headset").
- **Kleurenloop:** kies het effect "Kleurenloop", dan verschijnen onder de kleurkiezer de
  kleuren van de loop. Klik op een kleur en kies hierboven een nieuwe; met + voeg je een kleur
  toe (na de geselecteerde, 2 tot 8 kleuren) en met − haal je de geselecteerde weg. Voorbeeld:
  geel, groen, blauw loopt van geel naar groen naar blauw en weer naar geel. Met "Kleuren over
  de LED's verdelen" uit krijgen alle LED's tegelijk dezelfde kleur. De Snelheid werkt ook hier.
  Een apparaat onthoudt zijn loop, ook als je tussendoor een ander effect kiest.
- **Snelheid** (10–300%, standaard 100%) werkt alleen bij een bewegend effect (Ademen,
  Regenboog, Golf, Kleurenloop); bij Statisch en Per LED staat de schuif grijs. Een nieuwe snelheid gaat
  verder waar de animatie was, zonder te verspringen, en apparaten die samen een nieuwe
  snelheid krijgen blijven gelijk lopen. Zie de tabel bij "Effecten" voor de normale snelheid.
- **Opslaan gaat vanzelf.** Rechtsonder staat "Opgeslagen ✓" met de tijd zodra de engine je
  wijziging heeft weggeschreven, een seconde na de laatste wijziging. Lukt dat niet, dan staat
  er "Opslaan mislukt": de lampen werken dan wel, maar na een herstart is de wijziging weg.
  Ook een zonelengte die je instelt, bewaart de engine zo (zie "Fans, case-LED en ARGB-strips").
- **Profielen** (links onder de apparaten): typ een naam en kies "Opslaan" om de verlichting
  van alle apparaten te bewaren (kleur, helderheid, effect, snelheid, kleurenloop en
  per-LED-kleuren). Kies later het profiel en "Laden" (of dubbelklik) om terug te gaan; de
  geladen verlichting wordt dan ook de gewone, opgeslagen verlichting. Een profiel kiezen
  vult zijn naam in, en de knop heet dan "Overschrijven". "Verwijderen" vraagt eerst of het
  zeker is. Profielen staan in `%AppData%\BuurmansRgb\profiles.json`, naast `settings.json`.
  Is dat bestand geen geldige JSON meer, dan zet de engine het opzij als
  `profiles.json.broken` en begint met een lege lijst. Zonelengtes horen niet bij een profiel (wel bij
  `settings.json`); per-LED-kleuren in een profiel weten wel voor welke zonelengtes ze gekozen zijn.
  De uitleg staat achter de ⓘ naast de kop. Onder de profielen klap je "Automatisch laden" open
  om het gekozen profiel op een tijd of bij een programma te laten laden (zie "Profielen").
  In het kleinste venster past alles zolang dat dichtgeklapt is; opengeklapt schuift de
  linkerkolom (met Tab komt elk veld vanzelf in beeld).
- Een wijziging bij "Alle apparaten" geldt voor alle apparaten, maar alleen voor wat je
  wijzigt: zet je de helderheid, dan houdt een apparaat met een eigen kleur die kleur, en
  andersom. Een wijziging bij één apparaat geldt alleen voor dat apparaat. Is een apparaat
  weer gelijk aan "Alle apparaten", dan volgt het die weer en verdwijnt het uit `devices`.
- Het voorbeeld toont wat de engine echt rendert, ook als de effecten gepauzeerd zijn. Bij
  een bewegend effect loopt het mee. Zijn de effecten gepauzeerd, dan staat boven het voorbeeld
  een melding met de reden, en bij een pauze uit het tray-menu de knop "Hervatten" (zie
  "Pauzeren").
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
- `docs/installatie.md` – installeren in het kort
- `docs/openrgb-meeleveren.md` – hoe OpenRGB en PawnIO mee kunnen in één installer
