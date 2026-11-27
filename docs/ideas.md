# Ideeën voor later

Nog niet gebouwd, per onderwerp. Wat inmiddels gebouwd is, staat in `docs/handleiding.md` en in de
uitbreidingen in `docs/bouwplan.md`.

## Apparaten en OpenRGB

- Eigen HID-providers voor apparaten die OpenRGB niet kent
- Adres en poort van de OpenRGB-server instelbaar maken (nu vast `127.0.0.1:6742`), en
  OpenRGB helemaal uit kunnen zetten (`SwitchableDeviceProvider.For` weigert OpenRGB nu nog,
  omdat het herstellen na slaapstand anders verloren gaat; daarvoor is eerst een wrapper
  nodig die `IRestorableDeviceProvider` doorgeeft)
- Twee fouten in OpenRGB.NET 3.1.1 melden bij het project: `DeviceListUpdated` gaat nooit af
  (de client geeft de lege event-waarde bij het aanmaken door aan zijn verbinding) en na een
  nette sluiting door de server leest de read-loop op volle CPU nul bytes tot de volgende
  send faalt. Zodra dat is opgelost kan `OpenRgbServerWatcher` (de tweede verbinding) weg
- OpenRGB.NET 3.1.1 spreekt hooguit SDK-protocol 4. Met OpenRGB 1.1 verbindt het (zie
  `docs/openrgb-meeleveren.md`); met echte apparaten nog controleren of de apparaatindexen en
  `UpdateLeds` werken
- OpenRGB.NET bijwerken zodra het de nieuwere apparaattypes kent (storage, case, microphone,
  accessory, keypad); dan kunnen de vaste nummers 15 (case), 16 (microphone) en 18 (keypad) in
  `OpenRgbDeviceMapper` weg. Daarna eigen types voor lampen (11), "accessory" (17) en de nieuwe
  types van OpenRGB 1.0 (laptop 19, monitor 20). "Storage" (14) is alleen van "unknown" van
  oudere OpenRGB-versies te onderscheiden via de protocolversie (OpenRGB.NET 3.1.1 was op
  20-10-2026 nog de nieuwste versie)
- Apparaten zonder "Direct"- of "Custom"-modus toch een kleur geven via de modus "Static"
  met `UpdateMode` en de kleur van die modus
- Kleuren naar OpenRGB via een eigen wachtrij per verbinding sturen. Nu slaat `SetLeds` een
  frame over zolang een herlaadactie op de server wacht (zodat de lock van
  `LightingController` niet vastzit zolang OpenRGB zoekt, tot `ReloadTimeout`, 30 s); bij een
  animatie kan dat zichtbaar haperen tijdens een rescan
- Alleen de nieuwe apparaten aansturen als de lijst verandert; nu krijgen alle apparaten de
  kleur opnieuw als OpenRGB (opnieuw) verbindt
- De segmenten van een zone (SDK-protocol 4) tonen en apart kunnen kiezen, bijv. als een
  ARGB-kanaal uit een fan en een strip achter elkaar bestaat
- Bij twee dezelfde videokaarten controleren wat OpenRGB op Windows als locatie geeft: als bus
  en adres gelijk zijn, valt er alleen op volgorde te kiezen. Nieuwere SDK-versies of een
  PCI-locatie zouden dan helpen
- Apparaten en zones zelf een naam kunnen geven (bijv. "Voorste fans", "Strip achter"), bewaard
  per id in `settings.json`
- OpenRGB-status: een eigen stand "verbinding verbroken" (nu staat er na het wegvallen 5
  seconden "Verbinden met je apparaten…" tot de volgende poging, en pas daarna "Geen verbinding
  met je apparaten" met de melding in de statusregel), en de versie van OpenRGB en het
  SDK-protocol in de tooltip, handig bij "geen goed antwoord"
- Herstarten vanuit de app (sinds `een-melding`): de app hoort alleen dat de engine het verzoek
  aannam, niet wat ervan kwam. Mislukt de herstart, dan komt de app vers terug (de engine sluit
  hem vóór het starten van de nieuwe engine en `TrayPresenter.RestartAsync` opent hem daarna
  opnieuw) en meldt het tray-icoon het; maar valt het verzoek stil, omdat de herstart vanuit het
  tray-menu al loopt (`TrayPresenter.RestartAsync` geeft dan `false` terug zonder iets te doen),
  of omdat de herstart al vóór het sluiten van de app vastliep (de `catch` in
  `TrayPresenter.RestartAsync` vóór `CloseIfOpen`), dan blijft onderaan "buurmansRgb herstart…"
  staan tot de volgende wijziging of het opnieuw verbinden. Een gebeurtenis van de engine (herstart geweigerd of mislukt) zou dat opruimen en de
  knop weer vrijgeven
- Herstarten: alleen OpenRGB en PawnIO herstarten zonder buurmansRgb zelf (dan blijft de
  instellingen-app open), en in de melding noemen welk programma PawnIO vasthoudt als de driver
  niet stopt. De stap met adminrechten logt nu niets; de engine logt de uitkomst en alleen de
  eerste fout (via de exitcode), niet elke fout met zijn volledige tekst
- Herstarten: de PID van de oude engine meegeven aan de nieuwe (bijv. `--restarted <code>
  <pid>`), zodat die een oude engine die na de wachttijd nog hangt kan beëindigen in plaats van
  zelf te stoppen
- De laatst bekende apparaten grijs blijven tonen als OpenRGB weg is, met hun naam, type en plek
  en zonder schakelaar, in plaats van alleen de lege staat "Hier komen je apparaten zodra de
  verbinding terug is" (sinds `openrgb-weg`, de tekst sinds `een-melding`): dan zie je welke
  apparaten het waren en komt de lijst zonder sprong terug.
  Daarvoor moet de engine de laatste lijst van OpenRGB onthouden (ook over een herstart heen, in
  `settings.json` of een eigen bestand) en als "weg" aan de app geven, en moeten Kleur en de
  scènes zo'n apparaat overslaan
- In de instellingen-app een korte uitleg tonen als er geen headset is gevonden, met een
  link naar de lijst van OpenRGB; nu staat dat alleen in `handleiding.md`
- Vanzelf verversen als Windows een USB- of HID-apparaat ziet verschijnen (`WM_DEVICECHANGE` met
  `DBT_DEVICEARRIVAL`, op het verborgen venster van de engine), met een wachttijd van een paar
  seconden zodat een apparaat dat net aangaat klaar is en meer meldingen na elkaar één keer
  zoeken worden; nu moet je zelf op "Verversen" bij Apparaten klikken
- Neemt OpenRGB het verzoek om opnieuw te zoeken (pakket 140) alleen aan van een client die
  protocolversie 5 of hoger heeft afgesproken, laat de watcher dan eerst zelf
  `REQUEST_PROTOCOL_VERSION` sturen; nu valt "Verversen" dan na 5 seconden terug op opnieuw
  verbinden, wat ook werkt maar trager is
- Na Verversen zeggen welk apparaat er niet meer is ("De G733 Gaming Headset is bij het zoeken
  niet gevonden"), door de lijst van voor en na te vergelijken, met de verwijzing naar andere
  RGB-software die draait; nu verdwijnt zo'n apparaat stil uit de lijst (thuistest
  24-11-2026, F2)
- In `engine.log` zetten hoe lang OpenRGB over een zoekronde deed (van de vraag tot de laatste
  `DeviceListUpdated`), om op echte hardware te zien of `ReloadTimeout` (30 s) past bij
  OpenRGB met fabrikantsoftware open
- Komt er na `LoadTimeout` (5 s) een nieuwe `DeviceListUpdated` binnen terwijl het herladen nog
  op de lijst wacht, die leesactie dan opgeven en meteen opnieuw verbinden in plaats van tot
  `ReloadTimeout` (30 s) te wachten. Hangt de lees omdat OpenRGB niet antwoordt op
  `REQUEST_CONTROLLER_DATA` voor een index die bij het begin van de zoekronde verdween, dan
  geeft een nieuwe verbinding de lijst sneller (thuistest 24-11-2026, F2)
- "Zoeken…" laten staan zolang de engine nog op de lijst van OpenRGB wacht (tot 30 s), met een
  melding van de engine dat OpenRGB nog zoekt; nu stopt de knop na 15 s met "Het zoeken duurt
  lang" en komt het antwoord van de engine later
- Nederlandse namen voor de toetsen van het gesimuleerde toetsenbord ("Linker Shift", "Pijl
  omhoog", "Num 7"), uit `Strings.resx` van `Providers.Simulated`; de zones heten sinds
  `gewone-taal` Nederlands, de toetsen nog Engels, zoals OpenRGB ze bij echte toetsenborden geeft
- "Hoe komt een apparaat erbij?" op Apparaten de handleiding laten openen bij de uitleg over
  apparaten, zodra `AppInfoViewModel.GuideAddress` een adres heeft; nu gaat de link naar
  Instellingen, waar bij OpenRGB de uitleg staat (`gewone-taal`). Tot dan de link op Instellingen
  direct naar het blok van OpenRGB laten scrollen: nu opent de pagina bovenaan, en sinds het
  slaapblok en "Na een herstart" is de uitleg het derde blok (refactorreview 4-11-2026)
- Een apparaat buiten "Hele huis" houden, bijv. een toetsenbord dat zijn eigen kleur houdt terwijl
  de kast een effect krijgt: een schakelaar per apparaat ("Volgt Hele huis niet"), bewaard als een
  set id's in `settings.json` zoals `zonesAlike`, die `LightingController` bij een kleur, effect of
  helderheid van "Hele huis" overslaat; nu krijgt elk apparaat dat aan staat mee wat je bij "Hele
  huis" kiest, en moet je het daarna apart terugzetten (testpanel 1-11-2026, Mo)
- Een kleur vóór het aanmelden: de engine draait pas in de sessie van de gebruiker, dus tot het
  aanmelden staan de apparaten zoals de firmware of OpenRGB ze zet. De OpenRGB-service kan bij het
  starten een profiel laden (`--profile` of "Load profile on startup" in zijn instellingen); de
  engine zou de kleur van "Hele huis" (of een scène "Aanmelden") als OpenRGB-profiel kunnen bewaren
  (`SaveProfile` van de SDK) en de installer die keuze in de service kunnen zetten, zodat de pc
  meteen na het aanzetten de goede kleur heeft en de engine hem na het aanmelden overneemt
  (testpanel 1-11-2026, Mo en Kees)
- Andere RGB-software (`conflict-melding`): een knop bij Instellingen "Meldingen over andere
  RGB-software weer tonen" die `dismissedOtherRgbSoftware` leegmaakt; nu kan dat alleen met de
  hand in `settings.json`
- Andere RGB-software: NZXT CAM, SteelSeries GG en ASRock Polychrome in `OtherRgbSoftwareList`,
  en de namen van Synapse 4, Armoury Crate zelf en het oude RGB Fusion op een echte installatie
  nagaan (`docs/hardware.md`); SignalRGB noemt ze als botsend
- Andere RGB-software, G HUB: nagaan of het venster van G HUB een eigen proces heeft (`lghub.exe`
  is een gok zonder bron, daarom niet in de lijst; de agent draait toch altijd mee), en waar
  Gigabyte Control Center zijn autostart regelt (Opstartapps, Taakplanner of een eigen
  instelling), zodat `installatie.md` het precies kan zeggen
- Andere RGB-software: een programma dat maar een deel van de apparaten regelt (G HUB alleen de
  muis en headset) zou geen melding hoeven; dat kan alleen als de app weet welke apparaten van
  welk programma zijn, bv. op de fabrikant van het apparaat in OpenRGB (Logitech bij G HUB)
- Andere RGB-software: de uitleg onder "Uitleg" voorlezen zodra hij opengaat (een live region,
  zoals de melding zelf); nu zegt een schermlezer alleen dat de knop ingedrukt is, en staat de
  uitleg direct na de melding in de leesvolgorde (`conflict-melding`)
- "Uitleg" ook bij de andere meldingen van de statusregel, zoals de verbinding weg (wat je doet
  als Opnieuw verbinden niet helpt); `StatusMessage.ShowExplanation` kan dat al
- Andere RGB-software in de tooltip van het tray-icoon ("SignalRGB draait ook"), voor wie de app
  niet opent; nu zegt alleen de statusregel het
- Andere RGB-software: alleen melden als het echt botst, bijv. als OpenRGB een apparaat heeft dat
  het andere programma ook aanstuurt (iCUE en een Corsair-apparaat); nu meldt de engine elk
  programma uit de lijst, ook als het een ander apparaat regelt

## Zones en zonelengtes

- Per-LED-kleuren van een apparaat dat langer niet meer verschijnt opruimen; nu blijven ze
  in `ledColors` en `ledColorZoneLengths` staan, net als de eigen verlichting onder `devices`, de
  lengtes onder `zoneLengths` en het id in `zonesAlike`
- In de instellingen-app melden als OpenRGB een nieuwe lengte niet overneemt (de zone heeft
  na het herladen nog de oude lengte; dat staat nu nergens, ook niet in het logbestand), of als
  een bewaarde lengte niet past bij het bereik dat de zone nu meldt (bijv. na een update van
  OpenRGB; dat staat nu alleen in het logbestand)
- Vergeet OpenRGB een zonelengte terwijl de engine verbonden blijft (bijv. bij opnieuw zoeken),
  dan meldt het apparaat 0 LED's en komt de bewaarde lengte pas terug als het apparaat opnieuw
  verschijnt. Eventueel ook dan de lengte terugvragen, als blijkt dat OpenRGB dat doet
- Een zone waarvan het apparaat de naam twee keer heeft, krijgt nu geen bewaarde lengte (er is
  niet te zeggen welke van de twee het was). Eventueel naam plus volgnummer binnen die naam

## Welk apparaat is wat (knipperen)

- Een zone na een nieuwe lengte vanzelf even laten knipperen, zodat je meteen ziet of de lengte
  klopt (de laatste LED knippert mee of niet); nu moet je daarna zelf op "Knipperen" klikken
- De LED's die geen zone hebben ("Overige LED's" in het voorbeeld) hebben geen knop
  "Knipperen", omdat de engine ze niet als zone kent; eventueel een flits voor een LED-bereik
  in plaats van een zone-index
- Het knipperen loopt op de framerate van de render-loop: bij een lage `framesPerSecond`
  (bijv. 1) zie je het wit-uit nauwelijks. Eventueel een eigen tempo voor het knipperen
- Een flits stopt meteen als de apparatenlijst verandert (bijv. als OpenRGB opnieuw zoekt of een
  nieuwe zonelengte overneemt); het apparaat krijgt dan wel zijn eigen kleuren terug. Zo stuurt
  "Knipperen" eerst een wachtende lengte, en stopt de reload van OpenRGB daarna de flits weer.
  Eventueel een wachtende flits opnieuw starten zodra de nieuwe apparatenlijst binnen is
- "Tellen" ook bij een zone met een vaste lengte, om op een fan-ring te zien waar LED 1 zit en
  welke kant hij op telt (zie het idee over draaien en spiegelen bij "Voorbeeld en
  per-LED-editor"); nu kan het alleen bij een zone waarvan je de lengte instelt
- Sneller tellen bij een lange header (120 LED's duurt bij 1 per seconde twee minuten): bijv.
  halveren ("brandt LED 60 nog?") of een vak om meteen naar een LED te springen
- Een telling hoort bij de app die hem begon en stopt als die weggaat, ook als een tweede open
  instellingen-app hem nog toont en bedient. Eventueel elke app die hem toont laten meetellen,
  zodat de telling pas stopt als de laatste weggaat

## Effecten

- Alle apparaten synchroon laten lopen. Een begin: na een wijziging aan de apparatenlijst
  lopen nieuwe apparaten pas weer gelijk met de rest als ze dezelfde snelheid hebben én die
  nooit veranderd is; `EffectClock` zou de klok van "Hele huis" kunnen delen met elk apparaat
  zonder eigen snelheid
- Golf, Regenboog, Komeet en Scanner per zone laten lopen in plaats van over het hele apparaat
  (bijv. elke fan-ring een eigen golf of komeet). "Zones gelijk" komt in de buurt: elke zone
  krijgt het beeld van de langste, uitgerekt. Nog niet: het effect echt over de eigen lengte
  van elke zone renderen (nu neemt een kortere zone LED's uit het lange beeld, dus tot een
  halve LED verschoven en bij een veel kortere zone grover), of elke zone een eigen fase of
  richting geven
- "Zones gelijk" kan meer: kiezen welke zone het beeld geeft (nu de langste, bij gelijke lengte
  de eerste), een zone buiten de rest houden (bijv. het logo naast de toetsen van een
  toetsenbord, dat nu de kleur van de middelste toets krijgt), en bij Per LED de kleuren van de
  langste zone op de andere zetten (nu houdt elke LED zijn eigen kleur). De keuze hoort nu niet
  bij een scène; eventueel per scène kunnen bewaren
- In de app laten zien dat "Zones gelijk" bij Per LED niets doet, bijv. de schakelaar grijs met
  een uitleg; nu staat dat alleen in de tooltip. Bij Statisch en Ademen maakt hij ook geen
  verschil, maar daar is hij niet mis
- Faalt een apparaat tijdens het renderen na het omzetten van "Zones gelijk" (de setter van
  `LightingController.ZonesAlike` of `SetZonesAlike` → `RenderLocked` → `Provider.SetLeds`
  gooit), dan gaat `ZonesAlikeChanged` niet af: de keuze staat in het geheugen, maar wordt niet
  bewaard en de app hoort het niet. `PausedLights` meldt zijn wijziging met try/finally,
  `SetLighting` doet het net als `ZonesAlike` niet. Gelijktrekken als eigen fix, met een test
- Een effect over de hele kast, met apparaten op een plek in de kast: elk apparaat (en elke
  zone) een plek en grootte in een tekening van de kast, zodat een regenboog of golf in één
  richting van de voorste fans via het moederbord naar de achterste loopt. De richting is nu per
  apparaat (`LedPlaces.Along`), en de zones van een apparaat delen het effect zoals in de
  volgorde, omdat niemand zegt waar ze ten opzichte van elkaar zitten
- Richting ook voor Komeet en Scanner: een punt dat over de plekken loopt in plaats van over de
  volgorde. Nu kennen alleen Regenboog, Golf en een verspreide Kleurenloop een richting
- Een effect dat de videokaart (of alles) laat kleuren naar de temperatuur of belasting van de
  GPU, bijvoorbeeld via LibreHardwareMonitor, NVML of ADLX
- Kleur en helderheid per zone (bouwplan: "per apparaat, per zone en globaal"); de
  `IEngineApi` kent nu alleen apparaat en globaal
- Instellingen per effect naast de snelheid, bijv. de lengte van de staart bij Komeet en
  Scanner of hoeveel LED's er fonkelen. Nu liggen die vast als constanten in de effecten
- Komeet: kiezen tussen rondgaan (nu, goed voor een fan-ring) en aan het eind weglopen en bij
  de eerste LED weer binnenkomen (beter voor een strip, waar de staart nu nog even aan het eind
  zit als de kop alweer bij het begin is), of meer kometen tegelijk
- Fonkelen met een zwakke achtergrond van de kleur in plaats van zwart, of elke fonkeling in
  een willekeurige tint uit de hele kleurencirkel (regenboog-fonkelen); een willekeurige kleur
  uit een eigen lijst kan al
- Vuur dat over een strip omhoog trekt zoals Fire2012 van FastLED. Dat vraagt een richting per
  strip en hitte die van frame tot frame blijft; nu komt elk frame vers uit de tijd, zodat er
  geen buffer per apparaat nodig is
- Ademen en Golf ook met een lijst van kleuren, zoals Komeet en Scanner: elke ademhaling of elke
  golf de volgende kleur. Nu hebben ze één kleur, de kleur van het apparaat
- Bij Komeet en Scanner de kleur laten overlopen in plaats van wisselen (nu wisselt de komeet
  waar de ronde begint en de scanner aan het eind), of de staart in een verloop van de ene
  kleur naar de volgende
- Kant-en-klare kleuren om te kiezen bij de kleuren van een effect, zoals "Blauw vuur", "Gif"
  of "Kerst", in plaats van elke kleur zelf mengen
- De volgorde van de kleuren van een effect of de kleurenloop veranderen door een vakje te
  slepen; nu kan dat alleen door de kleuren opnieuw te mengen (bij Vuur telt de volgorde: van
  donker naar licht)
- De kleuren van de kleurenloop en die van de andere effecten op één manier bewaren (nu
  `colorLoop` met `spread` en daarnaast `effectColors`); kan pas als een oudere versie het
  bestand niet meer hoeft te lezen
- Een scènekaart toont vijf bolletjes, dus van een kleurenloop of effect met meer kleuren (tot
  twaalf) alleen de eerste vijf; eventueel de bolletjes over de hele lijst verdelen of een
  verloop tonen
- "Standaardkleuren" bij "Hele huis" ook te gebruiken als alleen een apparaat eigen kleuren voor
  het effect heeft; nu telt voor de knop alleen wat "Hele huis" zelf heeft, en moet je eerst
  dat apparaat kiezen
- Audio-reactief licht en muziek-sync: een effect dat meegaat met het geluid dat de pc afspeelt,
  bijv. de helderheid of een balk per LED op het volume of de bas, en een flits op de beat. Het
  geluid komt zonder stuurprogramma via WASAPI loopback (`AudioClient` op het standaardapparaat),
  met een kleine FFT per frame; de opname loopt alleen zolang het effect op een apparaat staat,
  zodat de engine zonder het effect niet zwaarder wordt (testpanel 1-11-2026, Jayden en Noor)
- Schermsync: de apparaten de kleuren van het scherm laten volgen, zoals Ambilight (de strip
  achter de monitor de rand van het beeld, de kast de gemiddelde kleur). Het scherm samplen met
  Desktop Duplication (`IDXGIOutputDuplication`), een paar keer per seconde en sterk verkleind, en
  per apparaat kiezen welke rand of welk deel het volgt; ook hier alleen samplen zolang het effect
  op een apparaat staat. Beschermde video (DRM) geeft een zwart beeld (testpanel 1-11-2026, Noor)
- Toetsen die oplichten bij indrukken (en weer uitdoven, of een kring die van de toets af loopt):
  de engine hoort de toets via Raw Input (`WM_INPUT` op het verborgen venster), liever dan via een
  `WH_KEYBOARD_LL`-hook (die laat elke toetsaanslag door de engine lopen en kan door anti-cheat als
  verdacht gezien worden), en zoekt de LED op via de toetsnaam die OpenRGB geeft ("Key: W", zie
  `OpenRgbDeviceMapper.ToLedNames`); het effect houdt per toets een tijd bij, zoals het idee voor
  Vuur hitte per LED bijhoudt. Alleen registreren zolang het effect op een toetsenbord staat;
  alleen welke toets, nooit wat je typt, en niets ervan in het logbestand (testpanel 1-11-2026,
  Jayden)
- Eigen effecten, na de eerste versie (stappen met een kleur of patroon, blijven staan en
  overlopen):
  - `effects.json` met de hand live oppikken, zoals `settings.json` met `SettingsFileWatcher`;
    nu leest de engine het alleen bij het starten, zoals `profiles.json`
  - Een stap die een bestaand effect een tijd laat lopen (bijv. 5 s Regenboog, dan 2 s rood), of
    een effect dat er een ander overheen legt; lagen en scripts zitten er bewust niet in
  - Per stap kiezen of een patroon wordt uitgerekt (nu) of herhaald, of over de plekken van de
    LED's loopt in plaats van over de volgorde, met de richting
  - Andere manieren van overlopen: zacht beginnen en eindigen (`SmoothStep`), via zwart, of een
    veeg van de ene naar de andere kant in plaats van alle LED's tegelijk
  - Een effect dupliceren (exporteren en importeren kan sinds `scene-delen`)
  - De stappen slepen om ze te ordenen (nu met ← en →), en de tijden ook als getal typen
  - Verwijderen ongedaan maken, bijv. met "Ongedaan maken" in de statusregel, ook voor scènes; nu
    vraagt Verwijderen het eerst, maar een verwijderd effect of een verwijderde scène komt niet terug
  - In het voorbeeld de gekozen stap laten zien in plaats van het lopende effect, zodat je een
    lange stap niet hoeft af te wachten
  - Op de scènekaart de naam van het eigen effect en de kleuren van zijn stappen als bolletjes;
    nu staat er "Eigen effect" met de kleur van het apparaat (hoort bij het idee hieronder om de
    regel onder de naam van een scènekaart gelijk te trekken met die van een apparaatkaart)
  - Een scène die een kopie van het effect bewaart in plaats van een verwijzing, zodat hij er
    hetzelfde uit blijft zien als je het effect later aanpast of verwijdert
  - Een eigen pagina met alle eigen effecten naast elkaar, met een klein voorbeeld per effect
  - De namen van de ingebouwde effecten op één plek: nu staan ze in de `Strings.resx` van de app
    én van de engine, die een eigen effect onder zo'n naam weigert (`EffectNamesTests` houdt ze
    gelijk, ook voor elke vertaling van de app). Bij een vertaling kan de engine ze bijv. met
    `GetEffectsAsync` meegeven. Let dan op een taalwissel: de engine weigert alleen de namen in
    de taal van dat moment, dus na een wissel kan een eigen effect heten als een ingebouwd effect
    in de nieuwe taal en hernoemt hij het bij het lezen van `effects.json`, met "(eigen)" in de
    taal van dat moment
  - Melden dat de engine een eigen effect uit `effects.json` een nieuwe naam gaf ("Regenboog" werd
    "Regenboog (eigen)"); nu staat dat alleen in het logboek
  - Bij een wijziging niet de hele lijst laten ophalen: `CustomEffectsChanged` met het gewijzigde
    effect, of zonder melding aan de verbinding die het zelf bewaarde. Nu haalt elke app bij elke
    stap van een slepende schuif alle eigen effecten op; dat is het grootste deel van de CPU van
    de engine tijdens het slepen (zie "Metingen" in `docs/ontwikkelaars.md`)
- De laag boven het effect (`toetsenbord-laag`) de zones laten volgen zoals de per-LED-kleuren
  (`LedColorStore.Layout`): nu hoort een laagkleur bij een LED-nummer, dus wordt een zone vóór
  de laag langer of korter, dan schuift de laag mee naar andere LED's
- Een laag op volle helderheid boven een gedimd effect (een vinkje "Laag niet dimmen"), voor wie
  's avonds het toetsenbord laag zet maar WASD fel wil; nu dimt de laag mee met het apparaat
- Een laag met een eigen beweging (ademen of knipperen op de gekozen toetsen), of een laag per
  scène die alleen meegaat bij een spel (bouwt op de gamemodus onder Scènes)
- Op een apparaatkaart laten zien dat er een laag is (bijv. "Laag op 4 LED's" in de regel met het
  aantal LED's) en "Hele laag weghalen" zonder eerst alle LED's te kiezen
- Laag weghalen met Ctrl+A haalt ook de laagkleuren voorbij het eind van het apparaat weg. Nu
  houdt een geïmporteerde scène met een laag van een apparaat met meer LED's onzichtbare kleuren
  vast, en Ctrl+A bereikt alleen de LED's die het apparaat nu heeft, zodat een export version 2
  blijft
- Wat nog per effect buiten `EffectTraits` staat (refactorplan 26-11-2026): de bolletjes van
  Regenboog, Kleurenloop en Vuur op een scènekaart (`SceneLook.DotsOf`) en dat Per LED geen ene
  kleur heeft (`SceneLook.OneColorOf`) zijn eigen tekenwerk van de app; en `MainViewModel` biedt de
  snelheid ook voor een eigen effect (`IsAnimated(id) || IsCustom`). Pas in de tabel zetten als een
  nieuw effect hier echt een tweede plek nodig heeft

## Instellingen-app en ontwerp

- De kopjes "Echte apparaten (OpenRGB)" en "Gesimuleerde apparaten" op Apparaten ook een kop voor
  de schermlezer maken (niveau 3, de apparaatkaarten dan niveau 4), met een eigen
  `GroupItemAutomationPeer` die het kopje als tekst toont: nu toont UI Automation het alleen als
  de naam van de groep, en zijn de kaarten niveau 3 zoals in het voorbeeld op Kleur
  (`schermlezer-structuur`)
- Een kleurvlak of -wiel naast de schuiven Rood, Groen en Blauw, voor wie liever op tint en
  verzadiging kiest
- Een kiezer voor kleurtemperatuur: wit kiezen als warm tot koud (bijv. 2700–6500 K) met één
  schuif in kelvin in plaats van Rood, Groen en Blauw, voor wie het licht als bureaulamp gebruikt;
  de omzetting van kelvin naar RGB is een vaste benadering (Tanner Helland). Dat het 's avonds
  vanzelf warmer wordt, zoals Nachtlicht van Windows, is dan een scène die op tijd aangaat (zie
  "Scènes") (testpanel 1-11-2026, Noor)
- Eigen kleuren onder Kleuren een naam geven en ze door slepen (of met Ctrl+← en →)
  in een andere volgorde zetten; nu krijgen ze hun hex-code als naam en komt een nieuwe achteraan
- Een hex-code van drie tekens (`#F80`) lezen als `#FF8800`, zoals in CSS; nu krijgt die bij het
  verlaten van het veld de melding dat het geen kleurcode is (sinds "invoer-uitleg")
- Escape in het veld van de hex-code de code van de kleur laten terugzetten, zoals Escape in het
  naamveld van een eigen effect de naam terugzet; nu blijft een getypte code staan tot je het
  veld verlaat. Bij de refactor na de review bleef het staan, omdat het de manieren van fouten
  tonen (hex-code, naam, zonelengte) gelijktrekt en dus gedrag verandert
- "+" van de eigen kleuren achter het laatste vakje zetten in plaats van ervoor, met een paneel
  dat de vakjes en + samen laat omlopen; nu staat + vooraan, zodat de vakjes binnen de kolom
  omlopen
- Ongedaan maken voor een kleur die je meteen meekleurde (van het apparaat, de kleurenloop, de
  kleuren van een effect of LED's), nu er geen knop meer tussen mengen en het apparaat zit; ook
  "de kleur van daarnet" na een scène die door een nieuwe kleur niet meer aan staat
- "Na een herstart" bij Instellingen ook de scènes, eigen kleuren en eigen effecten noemen, met een
  verwijzing naar "Instellingen bewaren…" voor een andere pc; nu noemt het blok alleen de
  verlichting, de helderheid, de slaapstand en "Starten met Windows" (`wat-blijft-bewaard`)
- Eén gedeeld template voor een rij met een schakelaar op Instellingen: de vier schakelaars daar
  (en "Zones gelijk" in het voorbeeld op Kleur) volgen hetzelfde patroon, nu elk met de hand in de XAML. Meer dan
  verplaatsen, omdat de rijen een `x:Name` hebben waar `FocusFallback` naar wijst en de boom voor
  UI Automation gelijk moet blijven (refactorreview 4-11-2026)
- Zes oudere plekken kiezen de tekst voor één of meer nog met de hand en kunnen ook
  `UiText.ForCount` gebruiken: `DeviceItem.cs:41`, `EffectOption.cs:34`,
  `OpenRgbStatusViewModel.cs:35`, `StatusLineViewModel.cs:71`, `ZoneCountPaceOption.cs:16` en
  `ZoneCountViewModel.cs:105` (refactorreview 18-11-2026)
- Een kleur op een apparaat dat uit staat, zet het nu aan; de helderheid of een effect kiezen doet
  dat (nog) niet. Misschien moet ook een helderheid boven 0 % het apparaat aanzetten, zoals bij een
  dimmer
- De zin over wat een effect doet ook als tooltip op elke effecttegel van Kleur, zodat je hem met
  de muis leest vóór je kiest; nu staat hij alleen onder de tegels, bij het gekozen effect, en
  hoort een schermlezer hem als hulptekst van elke tegel (`kleur-volgorde`)
- De navigatiebalk uitklapbaar maken (~240 px met het label naast het icoon), het alternatief uit
  § 6 van het ontwerp
- De Snap Layouts van Windows 11 (het menu bij het aanwijzen van de maximaliseerknop) ook bij de
  eigen titelbalk: dat vraagt `WM_NCHITTEST` met `HTMAXBUTTON` voor die knop
- Letterafstand van de koppen (−0,02em in het ontwerp): WPF kent geen letterafstand voor een
  `TextBlock`; eventueel met `Glyphs` of een eigen tekstelement voor de grote koppen
- De optische maten van Bricolage Grotesque gebruiken (de statische `12pt`- en `96pt`-bestanden):
  nu draaien alle maten op de standaardversie, terwijl een browser bij 30 px een grotere optische
  maat kiest
- Eén gedeelde stijl voor een melding onder een veld (een `DockPanel` met `WarningMark` en een
  `TextBlock` als live region met `LiveRegion.AnnouncesUpdates`); nu staat dat blok zeven keer in
  de XAML, onder andere bij de hex-code in `ColorMixer.xaml`, de naam van een eigen effect in
  `CustomEffectEditor.xaml`, bij de lengte van
  een zone in `LedPreviewPanel.xaml` en bij "Vanzelf aanzetten" in `ScenesPage.xaml` (die met
  `HasError` en `BoolToVisibility`, de andere drie met `NullToCollapsed`). Geen control met een
  eigen `Text`-property: die slikt dezelfde melding opnieuw in, zodat er geen nieuwe aankondiging
  komt, terwijl `CustomEffectViewTests` juist controleert dat een schermlezer hem dan weer hoort.
  De `TextBlock` met `NotifyOnTargetUpdated=True` blijft dus bij de aanroeper, bijv. met een
  `ContentControl`-stijl rond de tekst. `xaml-stijlen-opruimen` liet dit liggen: het plan noemde
  het optioneel, en de `ContentControl`-aanpak raakt zeven aanroepers en hun live-region-tests, wat
  een eigen taak waard is (refactorplan 26-11-2026)
- De statusregel onderaan leegmaken bij het wisselen van pagina (of na een tijdje); nu blijft de
  laatste melding staan, bijv. "Vanzelf aanzetten van 'Avond' opgeslagen." van Scènes nog op
  Kleur, zodat hij bij die pagina lijkt te horen (gebruikerstest 28-10, B3). Geldt ook voor de
  meldingen van Instellingen, zoals "Starten met Windows kon niet worden aangepast."
- Het grote lege vlak links onder het kleurvoorbeeld in kaart 4 bij Statisch (en Ademen en Golf)
  vullen of kleiner maken, bijv. met de kleuren en eigen kleuren onder het voorbeeld in plaats van
  onder de schuiven (zie `docs/images/kleur.png`). Het restant van gebruikerstest 28-10, B7: de
  volgorde van het effect is gebouwd (`kleur-volgorde`), het lege vlak bleef
- Een dichte kaart bij "Per apparaat" laten zien dat er in die kaart iets loopt dat een ander
  venster van de app startte, zoals een telling: dichtklappen stopt alleen een telling van dit
  venster, en die van een ander venster telt dan onzichtbaar door (`kleur-lagen`)
- De kaart van één gekozen apparaat op Kleur dezelfde vorm geven als de kaarten die dichtklappen
  (de kaartkleur, en de LED's van een zone op een eigen donker vlak zoals in de mock van § 9);
  nu houdt hij de donkere achtergrond van het voorbeeld van eerst (`kleur-lagen`)
- De inhoud van een dichte kaart bij "Per apparaat" pas opbouwen als hij voor het eerst opengaat;
  nu bouwt de app de zones, LED's en lengtes van elke kaart al bij het tonen van de pagina (ze
  krijgen dicht alleen geen kleuren), wat met veel apparaten geheugen en tijd bij het openen van
  Kleur kost (`kleur-lagen`)
- Op de effecttegels van Kleur in lagen (§ 9.3 van `design/DESIGN.md`) een klein stilstaand
  plaatje van het effect (bijv. een strip in de kleuren van Regenboog of Vuur), zodat je ziet wat
  een effect doet vóór je het kiest; geen animatie, voor minder beweging en geen werk per beeld
- Een eigen effect dat je importeert of plakt terwijl kaart 4 "Jouw effect" zonder eigen effecten
  toont, meteen kiezen; nu blijft de kaart staan met de keuzelijst zonder keuze, en verandert het
  licht pas als je het effect daar kiest (`kleur-volgorde`)
- Onthouden welk eigen effect de tegel "Eigen effect" kiest, ook na het opnieuw openen van de app
  (bijv. het eigen effect van de verlichting van "Hele huis", of in `settings.json`); nu kiest de
  tegel na het opstarten het eerste eigen effect op alfabet tot je er een gebruikt (`kleur-volgorde`)
- Een bewaker in `LinkStyleTests` of een eigen test die elke `RadioButton` van de app langsloopt
  (Tellen, de effecttegels) op een naam, een plek in de groep ("3 van 11") en de focusring, zoals
  `PlainWordsTests` dat voor knoppen doet (`kleur-volgorde`)
- In de subregel onder de titel van Kleur ook de scène noemen die aan staat ("Hele huis · 9
  apparaten · scène Gamen"), zodat je ziet dat een wijziging hem uitzet (`kleur-lagen-schets`)
- Na een sprong naar een apparaat met veel LED's (een toetsenbord) een link "Naar de apparaten"
  onder de kaart, of de chiprij laten meeschuiven bovenaan, zodat je zonder lang terugscrollen naar
  een volgend apparaat springt; § 9 van `design/DESIGN.md` laat de chiprij nu op zijn plek
  (`kleur-lagen-schets`)
- De grote schakelaar bij "Hele huis" op Apparaten bij een gemengde stand (bijv. 1 van 12 aan)
  eerst alles aan laten zetten; nu staat hij aan zolang er één apparaat aan is, dus een klik zet
  alles uit, en wie alles aan wil, moet twee keer klikken (gebruikerstest 28-10)
- Een bolletje per apparaat in de strip bij "Hele huis" (tot een maximum) in plaats van de vaste
  veertien (`LampsViewModel.StripLength`), of minder nadruk op de strip: een gebruiker gaat ze
  tellen en vergelijkt ze met "12 van 12 aan" (gebruikerstest 28-10)
- Op de schuif Snelheid 100 % als middenmarkering, of een streepje bij 100 %: nu staat de normale
  snelheid op een derde van de schuif (10–300 %), dus zie je niet dat 100 % normaal is en je tot
  300 % kunt (gebruikerstest 28-10)
- Gedempte kleuren een eigen naam geven in `ColorNames`, zoals "grijsblauw" (`#708090`),
  "grijsrood" of "oudroze" (`#BC8F8F`) en "kaki" (`#BDB76B`): nu krijgt een middelhelle kleur met
  weinig tint (met een verzadiging net boven de 20 %, de grens van grijs) de volle tintnaam,
  "blauw", "rood" of "geel". Een derde
  stap op verzadiging tussen grijs en de tint houdt het voorspelbaar, maar kaki heeft al 43 %,
  dus de grens vraagt een reeks kleuren om te kiezen (review 29-10)
- Eén grens voor "zwart": een LED krijgt de ring in plaats van een gloed als elk kanaal onder 8
  ligt (`LedRing.IsDark`), maar heet "zwart" voor een schermlezer zodra het felste kanaal onder 24
  ligt (`ColorNames.BlackBelow`), dus `#101010` heet zwart en gloeit toch. Ze hebben een ander
  doel (geen licht dat je ziet, tegenover een naam die klopt met wat je ziet), en gelijktrekken
  verandert het gedrag van een van beide; eerst kiezen welke grens het moet zijn (audit 29-10)
- De handleiding met de installer meeleveren, bijv. als HTML die `installer\build.ps1` uit
  `docs/handleiding.md` maakt, en die lokaal openen met "Handleiding" onderaan Instellingen: zo
  opent hij voor iedereen, ook zonder internet, en gaat hij over de versie die geïnstalleerd is,
  niet over functies die die nog niet heeft. Nu is de link verborgen
  (`AppInfoViewModel.GuideAddress` is `null`), want de repo op GitHub is privé en geeft een
  gewone gebruiker een inlogscherm of een 404; het pad van de meegeleverde handleiding in
  `GuideAddress` zet hem weer aan (gebruikerstest 28-10)
- "Snel aan de slag" (bovenaan `docs/handleiding.md`) ook bij de installer of de eerste start
  laten zien, bijv. als één pagina die de app opent; met de handleiding zelf als hij meekomt (zie
  hierboven). Daarbij een plek voor hulpvragen die een gewone gebruiker kan bereiken: de issues
  op GitHub zijn er niet voor zolang de repo privé is, dus "Hulp nodig?" verwijst nu naar wie
  BuurmansRgb installeerde, met het logbestand (snel-aan-de-slag 4-11)
- Bij Instellingen ook de versie van de engine tonen als die een andere is dan die van de app
  (bijv. na een update waarbij de oude engine bleef draaien); nu toont hij alleen die van de app
- De rand van de open lijst van een keuzelijst (`border-strong`, 1,8:1) en de randen van knoppen
  en kaarten (`border`, 1,2:1) nalopen op 3:1. Knoppen en kaarten zijn aan hun tekst te
  herkennen, dus WCAG vraagt het daar niet; de velden kregen in "contrast-invoer" wel een eigen
  rand (`InputBorderColor`)
- De primaire knop, een schuif, een vinkje, een keuzelijst, "+ nieuw" en een schakelaar die
  niets doen, net als de gewone knoppen ("knop-reden") in een eigen grijs tonen in plaats van
  alleen doorzichtiger (`Opacity` 0,4–0,6): de tekst van de primaire knop is dan ~3,3:1, en in
  een contrastthema geeft doorzichtig minder contrast dan het thema belooft, waar `GrayText` (zoals
  bij de gewone knoppen nu) zegt dat hij uit staat. Een `Opacity` is geen brush, dus
  `ContrastTheme.SystemBrushes` kan het niet vervangen
- "Wijzigingen worden automatisch opgeslagen." onderaan alleen tonen tot de eerste wijziging is
  opgeslagen, en daarna alleen "Opgeslagen ✓" met de tijd; het panel (voorstel B) noemde de zin
  als een van de dingen die op een fout lijken, en hij staat nu op elke pagina tot de eerste
  wijziging. Hij bleef bij `een-melding` als subtekst staan omdat hij bij het hele venster hoort,
  niet bij een pagina, en de statusregel anders altijd zou staan
- Onder "Details" van de statusregel ook de technische OpenRGB-status tonen (nu alleen in de
  tooltip rechtsboven, achter de ⓘ en bij Instellingen), zodat hij op Kleur ook zonder tooltip te lezen is
- De statusregel ook bovenaan Scènes en Instellingen; sinds `een-melding` staat hij alleen op
  Apparaten en Kleur, zoals het ontwerp (§ 4) hem noemt, dus op Scènes zie je een pauze of de
  verbinding die weg is alleen aan het tray-icoon, terwijl een scène die je daar aanzet dan niet
  op de apparaten komt
- De knoppen van een melding (actie, Uitleg, Niet meer melden) en de uitgeklapte uitleg met de
  link staan in `StatusLine.xaml` twee keer: voor de melding in de regel en in de lijst onder
  Details. Eén gedeelde `DataTemplate` of `UserControl` voor beide, zodat een wijziging niet op
  twee plekken hoeft; bleef bij de review van `conflict-melding` staan, omdat de knoppen in de
  regel namen hebben die de code-behind gebruikt (`ActionButton`, `DetailsToggle`)
- De balk onderaan (`SaveStatus` en wat er net gebeurde, in `MainWindow.xaml`) heette in de
  handleiding ook "statusregel"; sinds `een-melding` is de kaart bovenaan de Statusregel en zegt
  de handleiding voor de balk "onderaan". Eventueel de balk onderaan een eigen naam geven, ook in
  `docs/ideas.md` ("de statusregel onderaan leegmaken")
- Ook bij andere grijze knoppen zeggen waarom, zoals "Toepassen" bij een lengte die al staat en
  de schakelaar "Hele huis" zonder apparaten; nu zegt alleen de tooltip het, en een knop die uit
  staat krijgt geen Tab-focus, dus een toetsenbordgebruiker ziet die tooltip niet
- Met een licht contrastthema (bijv. "Woestijn") een lichte versie van het kleine icoon in de
  titelbalk (`AppIconSmallDark`); nu staat daar het donkere icoon op een licht vlak
- De focusring van de actieve LED in het voorbeeld (`LedPreviewView`) leest de tekstkleur pas bij
  het volgende beeld; bij een statisch effect krijgt hij de kleur van een contrastthema dat net
  aanging pas als de focus of de LED's veranderen
- De focus doorgeven als "Hervatten" boven het voorbeeld verdwijnt zonder klik: eindigt de pauze
  via het tray-menu terwijl de focus op die knop staat, dan valt hij naar het venster, want
  `PreviewActionsHint` heeft alleen `OnResumeClick`; de statusregel doet het wel
  (`StatusLine.OnButtonIsVisibleChanged`). Een gedeelde attached behavior in `Views/` (zoals
  `LiveRegion.AnnouncesUpdates`) voor "knop verbergt zich met de focus erin: focus naar de
  volgende" zou beide bedienen (refactorreview 2-11-2026)
- Een gewone knop die je indrukt in een contrastthema `Highlight` met `HighlightText` geven, zoals
  Windows; nu wordt hij `Window` (`SurfaceRaisedBrush`), wat op `ButtonFace` weinig of niets
  verandert
- De primaire knop onder de muis en ingedrukt (`Face.Opacity` 0,9 en 0,8) alleen zonder
  contrastthema doorzichtiger maken; in een contrastthema mengt dat `HighlightText` en
  `Highlight` met het vlak erachter, terwijl de omgedraaide kleuren en de ring de hover al tonen.
  Een `Opacity` is geen brush, dus `ContrastTheme.SystemBrushes` kan het nu niet vervangen
- Het tekenen van de app met en zonder contrastthema meten met een zichtbaar venster (niet
  vergrendeld); de meting bij "contrastthema" telde het opbouwen van een pagina en een wissel,
  niet het tekenen zelf
- Het tekenen van de ring van een LED die uit staat of zwart is meten met een zichtbaar venster
  (niet vergrendeld): de meting bij "led-uit-zichtbaar" telde `OnRender` en de layout, niet het
  tekenen van de lijnen door WPF zelf
- Met een grote tekstgrootte van Windows ook nalopen wat `TextScaleViewTests` niet toont: een open
  keuzelijst, een tooltip en "Kies uit wat nu draait" met programma's. Nu controleert de test de
  pagina's zelf, met de panelen die komen en gaan (ook Tellen bij een zone en de melding onder de
  hex-code)
- Een tooltip met de hele naam op de keuze van een apparaat op Kleur als die op "…" eindigt
- De tekst van een knop laten omlopen als de knop smaller is dan zijn tekst (de primaire knop, een
  gewone knop); bij de pagina's van nu gebeurt dat met elke tekstgrootte niet, omdat de kolommen
  op Kleur onder elkaar gaan, maar een smallere plek zou de tekst afkappen
- De breedste regel op Instellingen (`MaxWidth` 760) en de kolommen van Apparaten en Scènes
  (`MinColumnWidth` 280 en 220) in em laten meegroeien met de tekstgrootte, zodat een regel bij
  200 % niet half zo veel woorden heeft
- Het tekenen van de app bij een grote tekstgrootte meten met een zichtbaar venster (niet
  vergrendeld); bij "tekstgrootte" is alleen het opbouwen van een pagina en een wissel gemeten
- De app laten meekleuren met de actieve scène of de kleur van "Hele huis": de accentkleur (de
  primaire knop, het actieve item in de navigatie, de focusring) uit die kleur, zoals Windows zijn
  accentkleur uit de achtergrond kan halen. Daarvoor een tint die op het vlak minstens 4,5:1 haalt
  (dus een donkere of lichte variant van de kleur), geen wissel in een contrastthema
  (`ContrastTheme.SystemBrushes` blijft de baas) en geen animatie bij het omkleuren (minder
  beweging); eerst een keuze in `design/DESIGN.md`, dat de accentkleuren nu vast kiest (testpanel
  1-11-2026, Sanne)
- De stepper van § 4 (`design/DESIGN.md`) als eigen control: nu zet `LedPreviewPanel` de −, de
  schuif, de +, het veld en Toepassen met de hand in een `Grid`, en hebben − en + bij Tellen
  (vorige en volgende LED) gewone knoppen in plaats van knoppen die herhalen als je ze vasthoudt

### Voorbeeld en per-LED-editor

- `AutomationProperties.HelpText="{Binding LedCountHelpText}"` op de schuif en het veld van het
  aantal LED's (`LedPreviewPanel`) schrijft een bindingswaarschuwing zodra de tekst `null` is
  ("Value produced by BindingExpression is not valid"); `TargetNullValue` met een lege tekst
  haalt die weg. Gezien met de luisteraar van `DeviceChoiceViewTests`
- Met ↑ en ↓ in het voorbeeld naar de LED die in de zone erboven of eronder recht boven of
  onder de actieve staat; nu gaan ze vanaf de bovenste of onderste rij naar de laatste LED van
  de zone ervoor of de eerste van de zone erna, en in een ring meteen naar de zone ernaast
- Met het toetsenbord ook van het ene apparaat naar het andere in het voorbeeld gaan (bijv. Page Up en
  Page Down); nu gaat dat met Tab, dat één keer per apparaat stopt. Kleur in lagen (§ 9 van
  `design/DESIGN.md`) springt met de chiprij naar een apparaat en geeft een dichte kaart maar twee
  Tab-stops, maar Page Up en Page Down zitten er niet in
- Met ← en → over de LED's heen gaan die een zone niet meer toont terwijl je een kortere lengte
  instelt; de actieve LED springt nu terug naar de laatste getoonde LED als de lengte korter
  wordt, maar → kan er in de 600 ms voordat de lengte naar de engine gaat nog op komen (zonder
  ring)
- `ZonePreviewViewModel` alleen een smalle callback geven voor de actieve LED en de toetsen
  (bijv. een interface met `Activate` en `PressKey`) in plaats van het hele
  `DevicePreviewViewModel`
- In de per-LED-editor ongedaan maken, kleuren kopiëren naar een ander apparaat en een
  verloop met meer dan twee kleuren
- In de per-LED-editor een rechthoek van toetsen selecteren (slepen over de matrix) in plaats
  van een reeks in LED-volgorde
- De letters op de toetsen (`ctrl-klik-selectie`) volgen de Amerikaanse namen van OpenRGB; op een
  AZERTY- of QWERTZ-toetsenbord kan het voorbeeld `Q` zeggen waar `A` op de toets staat. De
  indeling van Windows gebruiken (`MapVirtualKeyEx` en `ToUnicodeEx` met de scancode van de toets)
  of een keuze "Indeling van het toetsenbord" per apparaat
- Op een toets van één vakje vallen `Shift`, `Caps`, `Enter`, `PrtSc` en `Home` nu weg (bij 200 %
  in een klein venster bijna alle woorden). Een teken dat wel past, zoals ⇧, ⇪ en ⏎, of een tweede,
  kortere tekst als het woord niet past (`Sh`), zou meer toetsen een tekst geven; wel nagaan of
  Bricolage Grotesque die tekens heeft of dat WPF ze uit een ander lettertype haalt
- De letters op de toetsen in een eigen laag (een `DrawingVisual` onder de view) die alleen opnieuw
  tekent als een toets van zwart naar wit wisselt of andersom. Nu neemt elk beeld de 92 bevroren
  tekeningen opnieuw op: gemeten ~8 µs en 13,5 KB (van WPF) per beeld extra, bij 20 beelden per
  seconde ~0,2 ms CPU en ~270 KB per seconde, alleen zolang het toetsenbord open in beeld is
- Per fan-ring kunnen kiezen waar LED 1 zit en welke kant hij op telt (draaien en spiegelen),
  bewaard per zone. OpenRGB zegt dat niet; het voorbeeld zet LED 1 nu bovenaan en telt met de
  klok mee
- Een kanaal met meer fans achter elkaar (bijv. een Lian Li-hub met vier fans van 16 LED's in
  één zone) als zoveel ringen tekenen in plaats van een rij, bijv. met een instelling "fans in
  deze zone"; nu blijft een aanpasbare of lange zone van een fan een rij
- Een zone die geen fan-apparaat is toch als ring kunnen tekenen (een ARGB-header met een fan
  erop, een zone "Fan" van een videokaart): de naam is daar niet betrouwbaar genoeg om het
  vanzelf te doen. Past bij het idee om apparaten en zones zelf een naam te geven
- De ringen van een fan met binnen- en buitenring in elkaar tekenen (concentrisch) in plaats
  van onder elkaar; nu is elke zone een eigen element met een eigen kop
- Hoge toetsen (Enter en + van het numerieke blok, de ISO-Enter) over twee rijen tekenen; nu
  worden brede toetsen alleen breder, niet hoger. Alt, Windows, Fn en Menu (net iets breder
  dan een letter) blijven één vakje, omdat ze anders lege vakjes van de spatiebalk afsnoepen
- Het voorbeeld niet helemaal opnieuw opbouwen als de engine de apparaten opnieuw meldt (ook na
  een nieuwe zonelengte of het omzetten van "Zones gelijk", die de app als `DevicesChanged`
  hoort), maar alleen de apparaten en zones die veranderden; nu houdt `FocusKeeper` de focus
  vast, maar een sleep over een schuif breekt af als er midden in het slepen een nieuwe
  apparatenlijst komt
- Een LED in het voorbeeld via UI Automation in beeld laten schuiven (`ScrollItem`) en hem
  `IsOffscreen` laten melden als hij buiten de zichtbare rand valt; nu volgen de LED's daarin de
  hele zone
- De kop van Apparaten "Apparaten" laten zeggen in plaats van een tweede woordmerk onder dat van
  de titelbalk (gebruikerstest 28-10, B4), zoals Kleur, Scènes en Instellingen een eigen kop
  hebben; dat vraagt eerst een keuze in het ontwerp, want § 5.1 zet het woordmerk bovenaan de
  pagina Lampen en § 6 ook in de titelbalk
- In het voorbeeld meteen laten zien welke LED een klik kiest, bijv. een dunne ring om de LED die
  `LedLayout.NearestLed` onder de muis vindt; nu kiest een klik naast een LED de dichtstbijzijnde
  ("led-klikdoel"), maar dat zie je pas aan de tooltip, na zijn vertraging
- Een grotere weergave van het voorbeeld in de per-LED-editor (bijv. een knop "Groter", met LED's
  van 24 px of meer en de afstand ertussen), voor een touchscreen of wie minder nauwkeurig wijst;
  de klik tot 12 px van een LED ("led-klikdoel") haalt WCAG 2.5.8 al, maar maakt de LED niet groter
- "Laten lopen" bij animaties uit bewaren, of een eigen keuze "Voorbeeld laten bewegen" in
  Instellingen naast die van Windows; nu geldt de knop tot de app sluit ("minder-beweging")
- Bij Tellen met animaties uit de getelde LED een ring geven in plaats van per stap een nieuw
  beeld; nu gaat er per stap één LED meer aan, zonder iets wat aan en uit gaat ("minder-beweging")
- Een stilstaand voorbeeld van Ademen of Golf op een herkenbaar moment laten zien (bijv. op zijn
  helderst); nu is het beeld het moment waarop de wijziging kwam, dat bij Ademen ook bijna donker
  kan zijn ("minder-beweging")
- De duur van een flits (`DeviceFlash.Duration`, 1,5 s) in `BuurmansRgb.Contracts` zetten, zodat
  de markering van Knipperen in de app (`PreviewViewModel.FlashTime`) hem niet hoeft over te nemen
- Het CPU-gebruik van de app met een stilstaand voorbeeld meten (zichtbaar venster, regenboog):
  vergrendeld tekent WPF niets en de instelling van Windows hoort niet omgezet te worden, dus dat
  vraagt een Windows Sandbox of een schakelaar om de instelling na te bootsen ("minder-beweging")
- Een knop "Vorige instellingen terugzetten" bij Instellingen die de nieuwste kopie uit
  `backups` terugzet, zodat je een terugzetting met één klik ongedaan maakt; nu kies je die zip
  zelf met "Instellingen terugzetten…" ("instellingen-beheer")
- Bij terugzetten ook de scène die in de zip aan stond (`activeScene`) weer als aan tonen; nu
  telt een teruggezette verlichting als een gewone wijziging, en staat er daarna geen scène aan
  ("instellingen-beheer")
- Bij "Licht op de achtergrond" ook het geheugen en de CPU van de instellingen-app zelf tonen, of
  een klein verloop van de laatste minuut; nu alleen de engine, en alleen het getal van nu
  ("instellingen-beheer")
- Bewaarde instellingen van een andere pc met andere apparaten: de verlichting per apparaat en
  de zonelengtes horen bij apparaat-id's die daar misschien niet zijn. Ze blijven bewaard tot
  zo'n apparaat komt, maar eventueel vragen welk apparaat welk is ("instellingen-beheer")
- De volgorde "gekozen LED's → kleurenloop → lijst van het effect → eigen effect → apparaat" staat
  vier keer in `MainViewModel` (`ColorCard`, `OnColorPicked`, `ShowPickerColor`,
  `SetPickerMode`; bij `OnColorPicked` zit de eerste stap nu in `LedEditCoordinator.TryPaint`).
  Eén enum van "wat de kiezer kleurt" kan dat samenbrengen. Een herontwerp met gedragsrisico,
  want `ShowsCustomCard` en Per LED wijken van die volgorde af, dus niet in een refactor
  (refactorreview 23-11-2026). Samen daarmee: de bediening van de kleur op Kleur (de kleurkiezer,
  de kleurenloop, de kleuren van het effect, kaart 4 en die vier plekken) uit `MainViewModel`
  naar een eigen `LightingControlsViewModel` (V1 stap 7 van het refactorplan). Bleef liggen bij
  `mainviewmodel-effecten-en-leds`: zonder die enum moet zo'n klasse dezelfde volgorde opnieuw
  vier keer kennen, en hij raakt bijna elke `MainViewModel*Tests` (refactorplan 26-11-2026)
- Eén `ZoneRef(DeviceId, ZoneIndex)` voor de records van het voorbeeld die met een apparaat en een
  zone beginnen (`ZoneResize`, `ZoneFlash`, `ZoneCountStart`, `ZoneCountChangeRequest`,
  `ZoneCountStop`), zodat `ZoneCountTracker` en `PreviewActionsCoordinator` er één waarde aan
  doorgeven. Bleef liggen bij `voorbeeld-zonetelling-apart`: het raakt elke aanmaak van die
  records in de view models en tientallen asserts in de `PreviewViewModel*Tests` en
  `ZoneCountViewModelTests`, voor alleen kortere code (refactorplan 26-11-2026)
- Het tekenen van `LedPreviewView` (`OnRender` met de LED's, de toetsletters, de selectie- en de
  focusring, nu ~630 regels in de view) naar een eigen klasse, zoals `LedPreviewKeyboard` en
  `LedArrangementCache` al los staan. Bleef liggen bij `ledvoorbeeld-opsplitsen`: de taak noemde
  alleen het toetsenbord, de schikking, de pennen per thread en `ColorContrast`, en het tekenen
  leest veel afhankelijkheidseigenschappen van de view, die zo'n klasse allemaal mee moet krijgen
  (refactorplan 26-11-2026)
- Het verlaten van het naamvak van een eigen effect (`OnCustomEffectNameLostFocus`, dat
  `EndNameEdit` aanroept en het vak de naam opnieuw laat tonen) en van het hexveld
  (`OnHexLostFocus`) staan nog in code-behind, sinds `kleurpagina-opsplitsen` in
  `CustomEffectEditor.xaml.cs` en `ColorMixer.xaml.cs`. `CommitOnEnter` zou er een
  `LostFocusCommand` bij kunnen krijgen, zodat die bestanden alleen nog de focus na een klik regelen.
  Bleef liggen bij `dialogen-als-service`: de taak noemde alleen Enter en Escape, en het
  naamvak geeft de tekst van het vak mee, wat een eigen soort commando vraagt
  (refactorplan 26-11-2026)
- `ColorPicker.IsUnused` en het pad "gemengd maar niet op het apparaat" (`HasColorNotOnDevice`,
  `keepMixedColor` in `ShowLighting`) zijn vanuit de app niet meer te bereiken, sinds kaart 4 bij
  een effect zonder kleur en bij een lege "Jouw effect" geen mengpaneel meer toont. Besluiten of
  het weg kan; dat raakt o.a. `MainViewModelDeviceColorTests.Rainbow_MixedColorGoesOnlyToTheOwnColors`
  (refactorreview 23-11-2026)
- De handleiding (`docs/handleiding.md`, "Als laag boven het effect") en `design/DESIGN.md`
  (9.10, "Wat de keuze doet") zeggen dat de keuze naar "Als laag boven het effect" springt als je
  een LED klikt die al een laagkleur heeft. In de code gebeurt dat alleen bij de eerste LED van
  een bewerking (`LedEditorViewModel.Start`), niet bij een volgende LED op hetzelfde apparaat
  (een gewone klik, Shift+klik of Ctrl+klik) zolang die bewerking loopt. Een productkeuze: het
  gedrag of de docs gelijktrekken
  (refactorreview 23-11-2026)

## Scènes

- Bij "Alleen als het op de voorgrond staat" ook Store-apps met een eigen Store-venster
  herkennen: dat venster (`ApplicationFrameWindow`) hoort bij `ApplicationFrameHost.exe`, en de
  echte app staat in het kindvenster `Windows.UI.Core.CoreWindow`, waarvan het proces te lezen is
  (bijv. met `EnumChildWindows`). Nu telt zo'n app als `ApplicationFrameHost.exe`
- Dezelfde Store-apps ook in "Kies uit wat nu draait" tonen, via het proces van hun
  `Windows.UI.Core.CoreWindow`: hun eigen proces heeft geen hoofdvenster, en
  `ApplicationFrameHost.exe` staat er sinds de herkenbare namen niet meer bij (gebruikerstest
  28-10, B14)
- In "Kies uit wat nu draait" de naam in de taal van Windows tonen ("Instellingen" en
  "Verkenner" in plaats van "Settings" en "Windows Explorer"): `FileVersionInfo` leest de
  taalneutrale versie-informatie; de vertaalde staat in het `.mui`-bestand of is via de shell
  te lezen (`PKEY_FileDescription`)
- Bij "Alleen als het op de voorgrond staat" de instellingen-app niet laten tellen als
  wegwisselen, zodat je een voorgrond-scène kunt bijstellen terwijl hij brandt; nu komt bij het
  openen van de app de verlichting van daarvoor terug
- Bij "Alleen als het op de voorgrond staat" een korte wachttijd (bijv. een halve seconde)
  voordat de scène wisselt, zodat snel heen en weer alt-tabben de apparaten niet laat flitsen; nu
  wisselen ze bij elk venster dat vooraan komt
- Bij "Alleen als het op de voorgrond staat" ook het zoeken van oudere Windows 10-versies
  (`SearchUI.exe`, vóór 2004) laten voorbijgaan, zoals `SearchApp.exe` en `SearchHost.exe` in
  `ProgramNames.ShellParts`; "Kies uit wat nu draait" laat hem al weg, maar op zo'n pc niet
  nagekeken of het zoekvenster daar vooraan komt
- "Gemiste tijden inhalen" (#51) laat bij het starten de tijd altijd winnen, ook van een kleur
  of scène die je na de laatste tijd zelf koos, omdat de engine niet weet wat er gebeurde terwijl
  hij niet draaide. Met een tijdstempel van de laatste wijziging van de verlichting in
  `settings.json` kan hij alleen inhalen als de tijd daarna viel, zoals na het ontwaken
- Op de kaart van een scène met meer tijden ze allemaal kunnen zien, bijv. in een tooltip; nu
  noemt hij de eerste en "en nog 1"
- De regel onder de naam van een scènekaart gelijktrekken met die van een apparaatkaart: een
  scène zegt "Statisch · 80%" (en bij een eigen effect alleen "Eigen effect"), een apparaat
  "LED-strip · Warm wit · 100%". Verandert zichtbare tekst, dus niet in een refactor
  (refactorreview 4-11-2026). Hoort bij het idee hierboven om de naam van het eigen effect op de
  scènekaart te zetten
- Een nieuwe tijd bij "Vanzelf aanzetten" laten beginnen op de dagen die nog geen tijd hebben
  (na 07:00 op werkdagen dus za en zo); nu begint hij op elke dag
- Ook inhalen als de klok van Windows over een tijd heen springt (nu alleen na het ontwaken en
  bij het starten; `IClockEvents` zegt niet of de klok verzet werd of de pc ontwaakte)
- "Overschrijven" in de naamkaart van "+ Nieuwe scène" en "Bewaar als scène" op Kleur eerst laten
  vragen, als je een naam typt die al bestaat, zoals "Bijwerken met wat nu brandt" nu doet; nu
  overschrijft hij meteen en komt de oude verlichting van de scène niet terug
  (`bewaar-als-scene`, `scene-bewerken`)
- "Bewaar als scène" op Kleur met een keuze "alleen dit apparaat" als je rechtsboven één
  apparaat koos; nu bewaart een nieuwe scène altijd alle apparaten en kies je de apparaten daarna
  op Scènes bij "Apparaten die meedoen" (`bewaar-als-scene`, `scene-bewerken`)
- Op de kaart van een scène van een paar apparaten zeggen voor hoeveel ("Lezen · 2 apparaten"),
  ook voor een schermlezer; nu zie je dat alleen in het blok "… aanpassen", en tonen de bolletjes
  de verlichting van het eerste apparaat (`scene-bewerken`)
- Als er een nieuw apparaat bij komt, zeggen dat het bij scènes met een keuze van apparaten niet
  meedoet, met een link naar Scènes; nu merk je dat pas als zo'n scène aangaat (`scene-bewerken`)
- "Apparaten die meedoen" ook in het tray-menu tonen, bijv. "Lezen (2 apparaten)", zodat je ziet
  dat die scène niet alles verandert (`scene-bewerken`)
- Na "Bewaar als scène" een link "Naar Scènes" in de melding, om meteen een tijd of programma
  bij de nieuwe scène te zetten; nu staat de scène gekozen klaar als je zelf naar Scènes gaat
  (`bewaar-als-scene`)
- Een tijd of programma bij "Vanzelf aanzetten" dat je wel typte maar niet toepaste, zichtbaar
  maken (bijv. "Nog niet toegepast") of vanzelf
  opslaan, zoals de rest van de app; nu staat 22:00 na het wisselen van pagina nog in het veld,
  terwijl de scène om de oude tijd aangaat (gebruikerstest 28-10, B13)
- Een gamemodus: één schakelaar (tray-menu of sneltoets) die tijdens een spel de effecten stilzet,
  op het toetsenbord alleen de speeltoetsen (WASD, de cijfers) laat branden en de rest dimt, en
  intussen geen scène laat wisselen op een tijd of een ander programma; het liefst vanzelf aan als
  een spel op volledig scherm vooraan komt (een venster zonder rand zo groot als de monitor, via
  `ForegroundHook`) en weer uit als het sluit. Bouwt op "Aanzetten als een van deze programma's
  start" en "Alleen op de voorgrond", die nu alleen een scène kiezen; de speeltoetsen kunnen al een
  vaste kleur boven het effect krijgen met de laag van `toetsenbord-laag` (testpanel 1-11-2026, Jayden)
- Een scène vanzelf aanzetten bij zonsondergang (en zonsopkomst) naast een vaste tijd: de tijden
  zijn zonder internet uit te rekenen uit een plaats (de NOAA-formule, met de breedte- en
  lengtegraad die je bij Instellingen invult of uit de locatieservice van Windows), elke dag
  opnieuw gepland zoals de tijden van "Vanzelf aanzetten" nu (testpanel 1-11-2026, Noor)
- Een scène vanzelf aanzetten zodra OBS live gaat (en terug als de stream stopt): OBS 28 en nieuwer
  heeft obs-websocket ingebouwd (localhost, poort 4455, met een wachtwoord), dat
  `StreamStateChanged` meldt; "Aanzetten als een van deze programma's start" ziet alleen dat OBS
  draait, niet dat de stream loopt. Hoort bij de brug onder "Integraties", of een eigen luisteraar die alleen
  verbindt zolang een scène het vraagt (testpanel 1-11-2026, Noor)
- De startscènes terug kunnen halen, bijv. met "Startscènes terugzetten" bij Instellingen, voor wie
  ze verwijderde en ze toch weer wil; een scène met dezelfde naam laten staan. Nu komen ze nooit
  terug (`startScenesCreated`) (`startscenes`)
- `--scene "Gamen"` op een nieuwe pc vóór de eerste start van de engine: de opdracht kijkt in
  `profiles.json` voordat de engine de startscènes schrijft, dus dan is het exitcode 2. Na de
  installer (die de engine met `--first-run` start) speelt het niet; eventueel de startscènes al
  meetellen als er nog geen `profiles.json` en geen `startScenesCreated` is (`startscenes`)
- Bij de eerste keer openen van de instellingen-app kiezen welke startscènes je wilt, in plaats
  van alle vijf; met "Minder beweging" van Windows aan eventueel zonder de bewegende
  (`startscenes`)
- Bij "Uit om" en "Aan om" op Apparaten alle tijden van "Alles uit" kunnen zien en bewerken; nu
  is de regel grijs zodra de scène meer dan één tijd heeft, en zegt hij hoeveel er nog bij
  Scènes staan (`uit-om`)
- "Aan om" laten kiezen wat er aangaat: nu komt na "Alles uit" de verlichting van daarvoor terug
  en anders gaat alles aan zoals "Alles aan"; een keuze voor een scène ("Rustig wit om 07:00")
  kan ook al met een tijd bij die scène (`uit-om`)
- De verlichting van vóór "Alles uit" bij elke keer "Alles uit" bewaren, ook als "Aan om" nog
  uit staat; nu bewaart buurmansRgb hem alleen als "Aan om" al een tijd heeft, dus zet je "Aan
  om" aan terwijl "Alles uit" al aan staat, dan komt 's ochtends Rustig wit terug (de
  verlichting van het moment dat je Aan om aanzette). Kost een schrijfactie van `profiles.json`
  per "Alles uit" en een intern profiel zonder tijden (`uit-om`)
- Bij "Alles uit" ook de naam van de scène van daarvoor bewaren voor "Aan om", zodat die scène
  's ochtends weer als aan telt (haar kaart bij Scènes actief); nu bewaart buurmansRgb alleen de
  verlichting, dus komen de kleuren terug maar staat er geen scène aan (`uit-om`)
- "Vanavond niet": één keer "Uit om" overslaan zonder de schakelaar uit te zetten, bijv. vanuit
  het tray-menu (`uit-om`)
- Een oudere versie van buurmansRgb laat `role` en `internal` in `profiles.json` bij het opslaan
  weg, dus na teruggaan naar een oudere versie en weer terug moet je "Uit om" en "Aan om" opnieuw
  aanzetten; eventueel bij het terugzetten van een backup (#115) melden (`uit-om`)
- Bij "Vanzelf aanzetten" melden als een programma al bij een andere scène staat, bijv. "cs2.exe
  zet al 'Shooters' aan"; nu wint stil de eerste scène op alfabet, ook met meer programma's per
  scène (`scene-programmas`)
- Start een tweede programma van dezelfde scène terwijl het eerste draait (CS2 naast Valorant),
  dan gaat de scène opnieuw aan, en verdwijnt wat je tijdens het eerste spel zelf veranderde;
  overwegen de scène dan te laten staan zolang hij onaangeraakt of aangepast van dezelfde scène
  is (`scene-programmas`)
- De programma's van een scène als lijst met een knop "weghalen" per naam, als het tekstveld met
  komma's in een gebruikerstest lastig blijkt (bijv. voor een schermlezer, die het veld als één
  regel voorleest); ook nodig voor een exe-naam met een komma erin, die nu niet kan
  (`scene-programmas`)
- Een oudere versie van buurmansRgb slaat `programs` in `profiles.json` over en laat het bij de
  volgende wijziging weg; eventueel `program` met de eerste naam erbij blijven schrijven, zodat
  die versie ten minste het eerste spel nog herkent (`scene-programmas`)
- Scènes delen, na de eerste versie (`scene-delen`):
  - Bij een naam die er al is zelf kiezen, in de naamkaart van "+ Nieuwe scène": een andere naam
    of (na een vraag) overschrijven. Nu krijgt de nieuwe altijd " (2)"
  - Vóór het toevoegen laten zien wat er komt: de bolletjes van de scène en welke apparaten
    passen, met een knop "Toevoegen"
  - Een apparaat dat niet paste zelf aan een apparaat van hier koppelen, bijv. een "K70" aan je
    eigen toetsenbord van een ander merk; nu valt het weg
  - De tijden en programma's op verzoek meenemen (een vinkje bij Exporteren), voor wie twee eigen
    pc's gelijk wil hebben
  - Meer scènes en effecten in één bestand, en een `.brgb.json` openen met een dubbelklik (een
    bestandskoppeling in de installer)
  - Een eigen melding als exporteren niet kan omdat een ander de scène zou weigeren (meer dan 64
    apparaten, groter dan 2 MB), met de reden; nu gooit de engine en zegt de app alleen
    "Exporteren is niet gelukt.". Of de verlichting van apparaten die er al lang niet meer zijn
    niet meenemen, zodat zo'n scène wel past
- De teksten met "en nog" gelijktrekken: de kaart van een scène zegt "Bij valorant.exe en nog 2",
  Hele huis "uit om 23:00 (elke dag) en nog 1 tijd". Verandert zichtbare tekst, dus niet in een
  refactor (refactorreview 18-11-2026)
- Overgang naar een scène (`scene-overgang`), uitbreidingen:
  - Per scène een eigen overgang (bijv. "Nacht" langzaam in 5 s, "Gamen" direct), naast de
    algemene van Instellingen; ook langer dan 2 s voor een wekker-achtig opkomend licht
  - Ook één apparaat aan of uit met de overgang, en een scène van een programma (voorgrond)
    misschien juist direct, zodat alt-tabben niet traag voelt; nu gaan alleen scènes en alles aan
    of uit zacht
  - Een andere curve dan recht: zacht beginnen en eindigen (`SmoothStep`), of in HSV of een
    kleurruimte die het oog gelijkmatiger vindt, zodat rood naar blauw niet even dof paars wordt
  - Een overgang na het hervatten van een pauze of na het ontwaken (nu direct, zodat het
    terugkomen niet wacht), als iemand dat mist
- Een melding voor een scène die niet te lezen is: lukt het lezen van een scènekaart of van de
  scène die aan staat niet (`SceneCoordinator`), dan blijft de kaart donker en komt de fout alleen
  in het log (`EngineSession.OnReadFailed`). Via `OnCallFailed` zou de app "De engine kon de
  wijziging niet verwerken." tonen, wat niet klopt voor een lezing; een eigen zin, of de kaart
  "Niet te lezen" laten zeggen, maakt het zichtbaar. Een scène die net in het tray-menu verwijderd
  is, moet dan stil blijven (refactorplan 26-11-2026)

## Tray-menu

- De sneltoetsen van "Alles aan" en "Alles uit" (sinds #52, vast Ctrl+Alt+Shift+A en U)
  instelbaar maken, bijv. met een veld bij Instellingen dat de volgende toetscombinatie opvangt.
  Dan kan ook een toets die met een andere app (of met AltGr+Shift op een internationaal
  toetsenbord) botst, vervangen worden in plaats van uitgezet
- Een bezette sneltoets ook bij de schakelaar in de instellingen-app tonen; nu meldt alleen het
  tray-icoon het (en het logbestand)
- Sneltoetsen voor meer items uit het tray-menu, zoals "Effecten pauzeren" of een scène

## Pauzeren, slaapstand en wisselen van gebruiker

- De helderheid van "Dimmen" bij "Als de pc slaapt of vergrendeld is" instelbaar maken; nu vast 20 %
- Met "Dimmen" of "Uitzetten" ook dimmen of uitgaan als de pauze uit het tray-menu al liep toen
  Windows vergrendelde of ging slapen; nu laat die pauze de apparaten zoals ze zijn. Daarvoor moet
  de engine onthouden wat de apparaten bij het begin van die pauze lieten zien
- Het voorbeeld in de instellingen-app laten zien dat de apparaten gedimd of uit zijn; nu toont het
  tijdens zo'n pauze de echte kleuren
- De keuze bij "Als de pc slaapt of vergrendeld is" per reden kunnen maken, bijv. alleen bij de
  slaapstand uit en bij vergrendelen het laatste beeld
- Een eigen schakelaar voor pauzeren als het scherm uitgaat of de schermbeveiliging loopt, los
  van vergrendelen en slaapstand; nu valt het onder dezelfde `pauseOnLockAndSleep`. De naam van
  die schakelaar ("Pauzeren bij vergrendelen, slaapstand en scherm uit") noemt de
  schermbeveiliging niet; alleen de uitleg eronder doet dat
- Tijdens de schermbeveiliging een eigen scène of een rustig effect laten zien in plaats van
  pauzeren, bijv. langzaam ademen in de kleur van de scène
- Ook een "schermbeveiliging" herkennen die een gewoon programma met een volledig scherm is; nu
  telt alleen een `.scr`-venster vooraan dat Windows bevestigt met `SPI_GETSCREENSAVERRUNNING`
- De exe-naam van het venster vooraan één keer lezen in `ForegroundHook` en aan elke luisteraar
  meegeven; nu leest de schermbeveiliging hem en een scène met "Alleen op de voorgrond" nog een
  keer, ~20–28 µs per keer (gemeten, zie de prestaties in `docs/ontwikkelaars.md`)
- De reden van een pauze ook in de tooltip van het tray-icoon; nu staat daar alleen "effecten
  gepauzeerd"
- Na van gebruiker wisselen kan de engine van de eerste gebruiker nog kleuren sturen (een scène
  op tijd, herstellen na het ontwaken) terwijl de engine van de tweede gebruiker dezelfde apparaten
  aanstuurt; eventueel niets meer sturen zolang de eigen sessie op geen scherm staat. Met
  "Dimmen" of "Uitzetten" bij "Als de pc slaapt of vergrendeld is" stuurt hij bij het wisselen
  ook nog het gedimde beeld of zwart
- Herstellen na het ontwaken ook bij Modern Standby (S0) controleren: of `PowerModeChanged`
  dan `Resume` meldt, verschilt per pc. Eventueel ook herstellen (de apparaten opnieuw laden)
  bij het aangaan van het scherm (`IDisplayEvents`) of bij ontgrendelen; nu stuurt de engine
  dan alleen de kleuren opnieuw
- Een bestaande installatie na de update één keer laten weten dat "Uitzetten" nu bovenaan
  Instellingen te kiezen is; nu houdt zo'n pc stil zijn "Laten branden" (`slaapstand-vooraan`)
- "Uitzetten" en "Dimmen" ook laten werken met het pauzeren uit, zodat je kunt kiezen voor
  doorlopende effecten tijdens het werken en toch donkere apparaten als de pc slaapt; nu hangt de
  keuze aan `pauseOnLockAndSleep` en is hij grijs als die uit staat
- De wachttijden na het ontwaken (nu vast 2 en 10 seconden) instelbaar maken, of herstellen
  zodra OpenRGB de apparaten weer meldt
- Ook naar het scherm van de console kijken terwijl je via Extern bureaublad werkt
  (`GUID_CONSOLE_DISPLAY_STATE` naast `GUID_SESSION_DISPLAY_STATUS`): is dat uit, dan zit er
  niemand bij de pc en kunnen de apparaten dimmen of uit, zonder de effecten te pauzeren voor
  wie op afstand kijkt. Nu volgt de engine alleen het scherm van zijn eigen sessie
- De sessiewissel (`ConsoleConnect`, `RemoteConnect`) verder benutten: na het wisselen de
  apparaten opnieuw laden zoals na het ontwaken, want een pc die intussen bij de console stond
  kan van een andere engine kleuren hebben gekregen; nu stuurt de engine dan alleen zijn kleuren
  opnieuw
- De laatste pauzes met reden en tijd in de instellingen-app laten zien (uit `engine.log`),
  zodat je niet het logbestand hoeft te openen om te zien waarom de effecten stilstonden

## Engine en prestaties

- Het geheugen van de instellingen-app onder het budget van 150 MB krijgen. Uitgezocht in #43
  (zie "Metingen" in `ontwikkelaars.md`): de .NET-heap is ~8 MB, de rest is WPF met DirectX.
  Tekenen zonder videokaart (`RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly`) brengt
  het naar 130–143 MB, maar kost op Apparaten CPU; eventueel als keuze bij Instellingen, of alleen
  zonder bewegend effect. Sinds #100 draait de app zonder tiered compilation (8–17 MB minder),
  maar dat is alleen gemeten over Extern bureaublad, waar WPF zonder videokaart tekent
  (122–133 MB); het kost ~0,4–1,1 procentpunt van één kern meer CPU, geen harde eis voor de
  app. Openstaand punt: de app met een zichtbaar venster op het eigen scherm opnieuw
  meten (Apparaten en Kleur met "Hele huis", zie "Metingen"), om te zien of hij nu onder de
  150 MB blijft; vóór #100 was dat 166–191 MB. Wat er daarna nog te halen is, met wat de proeven
  in #100 opleverden:
  - De kleinste gen0 van de GC (`GCgen0size`, bijv. 4 MB): 4–8 MB op Apparaten, 0–6 MB op Kleur,
    maar alleen als omgevingsvariabele (`DOTNET_GCgen0size`); `System.GC.Gen0Size` in
    `runtimeconfig.json` deed niets. Dan zou de engine de app met die variabele moeten starten
  - De voorbeeldkaarten van "Hele huis" op Kleur pas maken als ze in beeld komen, of de delen
    per zone die verborgen zijn (Tellen en de lengte) pas als ze zichtbaar worden: vergrendeld
    hooguit 3–7 en 0–3 MB, en de focus (`FocusKeeper`, `FocusFallback`), Tab door de kaarten en
    UI Automation moeten dan blijven werken
  - Wat het tekenen op de videokaart kost (het stuurprogramma, de buffers van DirectX, de gloed
    van `LedBulb`) is nog nooit apart gemeten; dat kan alleen met een venster op het eigen scherm
- De gedeelde DLL's van engine en app (Contracts, Core, StreamJsonRpc, MessagePack en wat daarbij
  hoort) ook met ReadyToRun publiceren: de app is dan ~0,1 s eerder bruikbaar (1.047–1.096 tegen
  941–986 ms, zie "Metingen" in `ontwikkelaars.md`). Ze staan in dezelfde map als de engine, dus
  eerst de engine meten met en zonder (working set, private bytes, CPU; statisch en regenboog), en
  alleen doen als die niet slechter wordt; dan `ReadyToRunAssemblies` in `BuurmansRgb.UI.csproj`
  aanvullen of het target `KeepSharedAssembliesIL` weghalen
- Geheugen van de engine: het budget in `docs/bouwplan.md` is working set, maar die telt
  gedeelde pagina's van .NET en WinForms mee; vergelijk daarom private bytes (~11–13 MB).
  `MemoryTrimmer` maakt de working set klein, maar de pagina's komen terug zodra de engine ze
  gebruikt (tray-menu, timer). Echte winst zit in minder geladen assemblies (bijv. Winsock en
  `System.Net.Sockets` sinds de koppeling met OpenRGB)
- `InvariantGlobalization=true` staat andere UI-culturen niet toe; bij vertalen van de
  engine-teksten opnieuw afwegen
- Wijzigingen in `profiles.json` met de hand live oppikken, zoals `settings.json` nu
  (`SettingsFileWatcher` en `SettingsReloader`); daarvoor moet `ProfileLibrary` zijn lijst opnieuw
  lezen en moeten de triggers (tijden, programma's) opnieuw plannen. Nu pas na een herstart
- Eén IPC-aanroep die meer stukken LED's tegelijk zet (bijv. `SetLedRunsAsync` en
  `SetOverlayRunsAsync` met een lijst van eerste LED en kleuren), met één render en één
  `LightingChanged`. Sinds `ctrl-klik-selectie` stuurt de app per aaneengesloten stuk gekozen
  LED's een eigen `SetLedColorsAsync` of `SetOverlayAsync`, en elk stuk geeft in de engine een
  render en een `LightingChanged`; met veel losse toetsen kunnen stukken na elkaar oplichten.
  Eerst op echte hardware kijken of dat te zien is (stap 7 bij "Losse LED's en letters op de
  toetsen" in `ontwikkelaars.md`)
- `activeScene` met de hand in `settings.json` zetten om een scène aan te zetten; nu neemt de
  engine dat veld bij een wijziging met de hand niet over
- Na een start met een `settings.json` die er wel was maar niet las (vergrendeld door een
  virusscanner of OneDrive, of geen JSON) werkt de engine met standaardwaarden. Een gewone
  wijziging van de gebruiker kan die dan alsnog over het bestand schrijven: na vijf wachtbeurten
  (`MaxWaitsForChangeByHand`) slaat `LightingSettingsSaver.SavePending` op met `Save`. De
  startscènes slaan daarom niets op (`ISettingsStore.LoadDidNotRead`), maar de saver kijkt daar
  niet naar. Beter: bij `LoadDidNotRead` niet schrijven, of eerst het bestand opnieuw lezen en de
  wijziging daarop toepassen (review startscènes, 12-11-2026)
- `ProgramWatcher` opsplitsen: de logica van de voorgrond (`foregroundOnly`, het volgen van
  `IForegroundWindow`, wisselen tussen voorgrond-scènes) naar een eigen klasse. Bij de refactor
  na de review bleef het staan; pas oppakken als het werk aan programma's en LED's groeit
- Eén beschrijving per engine-optie: een nieuwe optie raakt nu zo'n tien plekken (`EngineSettings`,
  de JSON-converter, `EngineHost` bij laden en opslaan, `IEngineApi` met get, set en
  `OptionsChanged`, `EngineApi`, de RPC, `EngineOptionsCoordinator` met lezen, tonen en sturen, het
  viewmodel en de XAML van Instellingen). Een gedeelde beschrijving met naam, standaard en
  geldige waarden zou de meeste daarvan uit één bron halen. `hadRealDevices` (`openrgb-weg`)
  raakte opnieuw negen plekken: `EngineSettings`, `EngineHost` tweemaal,
  `LightingSettingsSaver.AdoptLocked`, `IEngineApi`, `EngineApi`, `MainViewModel` driemaal en
  beide fakes in de tests. `autoStartChosen` (`update-autostart`) ging zonder IPC (tray en app
  lopen al via `AutoStartSwitch`) en raakte vier plekken: `EngineSettings`, `JsonSettingsStore`
  (een bestand dat niet leest), `EngineHost` bij starten, kiezen en een wijziging met de hand, en
  `AutoStartSwitch.Chosen`. `dismissedOtherRgbSoftware` (`conflict-melding`) raakte er acht:
  `EngineSettings` (veld, lezen en schrijven), `EngineHost` bij `ApplyOptions` en bij opslaan,
  `OtherRgbSoftwareCheck.Dismissed` met `DismissedChanged`, `IEngineApi` (event, lijst en
  wegklikken), `EngineApi`, `MainViewModel` (lezen bij verbinden, het event en wegklikken) en
  beide fakes in de tests
- Een onbekende enumwaarde via IPC gelijk behandelen: `SetSimulatedDevicesAsync` gooit bij een
  waarde die niet bestaat, `SetPausedLightsAsync` valt terug op "keep". Eén lijn kiezen (liefst
  terugvallen, zoals `settings.json` dat ook doet) en voor alle enum-opties gelijk testen
- De reden van stoppen in het log van de engine: "Afsluiten" in het tray-menu of een `--exit`
  van een andere start. Nu staat er alleen "Engine stopped."; bij de meting van de nieuwe effecten
  stopte een engine na 55 s zonder dat het log zei waarom (waarschijnlijk een `--exit` in dezelfde
  Windows-sessie, zie "Metingen" in `ontwikkelaars.md`)
- Tijdens het tellen van LED's de render-loop alleen op het tempo van de telling laten tikken
  (1, 2 of 5 keer per seconde) als verder alles statisch is: nu draait hij dan op 30 fps, terwijl
  er maar één keer per LED iets verandert (zie de meting "LED's tellen" met Statisch in
  `ontwikkelaars.md`). `AnimationLoop.Period` kan al veranderen (bij `framesPerSecond`); de
  controller moet dan bij elke wijziging nagaan of er naast de telling een bewegend effect of een
  flits loopt, en een stap mag niet later komen dan nu
- `tests/Shared/TestColors.cs` alleen meebouwen in testprojecten die `BuurmansRgb.Core` kennen,
  zodat `BuurmansRgb.Platform.Tests` geen verwijzing naar `Core` meer nodig heeft; nu heeft het
  die alleen voor `TestColors` (contracts-zonder-infrastructuur, refactorplan 26-11-2026)
- De regel "`onlyDevices` is nooit leeg en nooit bij "Alles uit"" als één invariant in `Profile`.
  Nu staat hij op drie plekken, elk met een eigen reactie: `ProfileLibrary.SetDevices` gooit,
  `SharedFileReader` weigert het hele bestand en `ProfileSettings` laat de lijst met een regel in
  het log weg (refactorreview 18-11-2026)
- De grenzen van de twee `SameAs` gelijktrekken: `LedOverlayStore.SameAs` vergelijkt alleen de
  LED's die het apparaat nu heeft, maar `LedColorStore.SameAs` ook de per-LED-kleuren voorbij
  het eind van het apparaat. Een verschil dat je niet ziet (bijv. bewaard voor een zone die nu
  korter is) geeft zo bij een scène toch een overgang, en 0,5 s render-loop voor niets. Allebei
  lopen ze via `LedColorLists.Same`, dus het is één getal (`device.LedCount`) en een test
  (`LedColorStoreTests.SameAs_DifferencePastTheEndOfTheDevice_IsFalse`) aanpassen
  (refactorreview 23-11-2026; refactorplan 26-11-2026 liet dit bewust liggen bij taak 11, want
  het verandert wanneer een scène een overgang geeft en past dus een test aan)

## Integraties

- Game-integratie via CS2 Game State Integration: de game stuurt bij elke verandering een
  HTTP-POST met JSON (gezondheid, ronde, bom, kills) naar een lokaal adres dat in een
  `gamestate_integration_*.cfg` in de map van de game staat; een luisteraar op `127.0.0.1` zet dat
  om in licht (weinig gezondheid rood, bom geplant knipperen, ronde gewonnen een flits), bijv. als
  een scène per toestand, met het `auth`-token uit de `.cfg` (CS2 stuurt dat in elke POST mee),
  zodat niet elk lokaal programma de kleuren kan zetten. Liefst als apart proces op de pipe van de
  engine (zoals de brug hieronder), zodat de engine geen HTTP-server krijgt en het niets kost
  zolang het uit staat (testpanel 1-11-2026, Jayden)
- Een brug naar Home Assistant of MQTT: een apart proces (bijv. `BuurmansRgb.Bridge`) dat zoals de
  instellingen-app op de named pipe van de engine verbindt (`IEngineApi`: alles aan en uit, kleur,
  helderheid, scènes) en dat als `light` aanbiedt via MQTT discovery van Home Assistant of een
  kleine REST-koppeling. De eisen van Kees: standaard uit; wat het zelf opent, luistert alleen op
  localhost (`127.0.0.1`); elk verzoek draagt een token uit de instellingen; en
  `docs/handleiding.md` zegt wat er naar buiten gaat en hoe je het aanzet. De engine zelf krijgt
  geen netwerkcode (testpanel 1-11-2026, Noor, Kees en Bram)
- Meer opdrachten naast `--scene`, `--all-on` en `--all-off` (`scene-commandline`): bijv.
  `--pause` en `--resume` voor "Effecten pauzeren", of `--toggle` voor één Stream Deck-knop die
  alles aan of uit zet. Gaan over hetzelfde kanaal (`CommandMailbox.Send`), met een eigen
  bericht in `EngineCommand`
- Een opdracht die in de eerste seconden na het starten van de engine komt (exitcode 3), laten
  wachten tot de opgeslagen verlichting staat in plaats van hem te weigeren, zoals een opdracht
  bij een engine die door die opdracht zelf start. Handig voor Home Assistant vlak na het
  aanmelden; let op dat de tweede start dan langer dan 10 s kan wachten
- Een Stream Deck-plugin die de scènes zelf opsomt (via de pipe van de engine, zoals de
  instellingen-app) en op de knop toont welke aan staat; nu maak je per scène met de hand een
  knop met `--scene`
- Een `--scene` voor een scène die niet bestaat ook bij het icoon melden; nu staat het alleen in
  `engine.log` en de exitcode, wat een Stream Deck-knop niet toont
- Een `.cmd`-bestand of uitleg om een opdracht via `ssh` in de sessie van de gebruiker te laten
  lopen zonder per scène een geplande taak (bijv. één taak die een scène uit een bestand leest);
  de signalen `Local\BuurmansRgb.Engine*` hangen aan de Windows-sessie, dus een opdracht die via
  `ssh` binnenkomt, vindt de engine niet en start een eigen. De tweede start zou in een sessie
  zonder bureaublad ook kunnen weigeren een engine te worden

## Installer en uitrol

- Opnieuw installeren na verwijderen met behoud van de instellingen: verwijderen haalt de
  autostart weg, maar `autoStartChosen: true` blijft in `settings.json`, dus de nieuwe installatie
  zet "Starten met Windows" niet aan, ook als je het aan had (`update-autostart`). Oplossing:
  de keuze zelf bewaren (aan of uit) en die bij `--first-run` terugzetten, of het verwijderen het
  veld laten weghalen als de instellingen blijven
- De Windows Sandbox-test ook een update laten doen: installeren, "Starten met Windows" uitzetten
  zoals het tray-item (Run-waarde weg en `autoStartChosen: true`), dezelfde setup nog een keer
  stil draaien en controleren dat het uit blijft; nu staat dat als handmatige test in
  `ontwikkelaars.md` (`update-autostart`)

- De installer bij een update de engine laten stoppen met `--exit` in plaats van `taskkill /F`,
  zodat er geen icoon in de tray blijft hangen, en net als het verwijderen alleen de apps van de
  gebruiker die installeert (`PrepareToInstall` stopt nu die van alle gebruikers). `--exit` kan pas
  als geen installatie meer een engine van 1.0.0 heeft: die kent `--exit` niet en opent dan de
  instellingen-app; het verwijderen doet het al, want de uninstaller hoort bij de geïnstalleerde
  engine (`verwijderen-netjes`)
- Verwijderen met het wachtwoord van een andere beheerder ook netjes laten stoppen: `--exit`
  bereikt de engine van de gebruiker dan niet, omdat het signaal alleen voor die gebruiker open
  staat, dus de engine wordt meteen gestopt. Bijv. de engine zijn signaal ook voor
  Administrators laten openen, of `--exit` via een taak met `/IT` als de gebruiker starten
  (`verwijderen-netjes`)
- Programmabestanden die een engine van een andere gebruiker nog open heeft, bij de volgende
  herstart laten weghalen (`uninsrestartdelete` op de `[Files]`-regel van de app). Nu blijven ze
  staan en start de autostart van die gebruiker BuurmansRgb weer; `installatie.md` zegt dat je die
  gebruiker eerst afmeldt. Niet zomaar aanzetten: met `/VERYSILENT` herstart de uninstaller de pc
  dan zonder te vragen (`/NORESTART` meegeven of `NeedRestart` beperken), en de autostart van die
  gebruiker moet ook weg of naar niets wijzen (`verwijderen-netjes`)
- Bij verwijderen een beheerder laten kiezen om de instellingen en autostart van álle gebruikers
  weg te halen (nu alleen van wie het verwijderen startte; de rest staat in `installatie.md`)
  (`verwijderen-netjes`)
- De Sandbox-test ook het verwijderen als gewone gebruiker met het wachtwoord van een andere
  beheerder laten doen; nu een handmatige test in `ontwikkelaars.md`, omdat de UAC-vraag met een
  wachtwoord niet te automatiseren is (`verwijderen-netjes`)
- De service `OpenRGB` expliciet aan `127.0.0.1` binden: nu krijgt hij geen argumenten en volgt
  hij de standaard van OpenRGB 1.1 (`--server-host 127.0.0.1` volgens de help-tekst), dus een
  andere versie of instelling kan op `0.0.0.0` luisteren. Bijv. na de MSI het `ImagePath` van de
  service aanvullen met `--server-host 127.0.0.1 --server-port 6742`; eerst testen of OpenRGB die
  argumenten als service leest, en of een reparatie of update van de MSI ze weer weghaalt
  (`installatie-uitleg`)
- Bij een service `OpenRGB` die er al stond (die slaat de installer over) controleren of hij op
  `0.0.0.0:6742` luistert of een versie van vóór de hotfix 1.0rc3.1 is, en dat melden; zie
  "De SDK-server" in `installatie.md` (`installatie-uitleg`)
- Een parameter voor stil verwijderen die OpenRGB en PawnIO ook weghaalt (bijv. `/REMOVEDEPS`,
  te lezen met `{param:...}` of `GetCmdTail`); nu beantwoordt `/SUPPRESSMSGBOXES` die vragen met
  "Nee" en staan de losse commando's in `installatie.md` (`installatie-uitleg`)
- PawnIO (en de service OpenRGB) bij een update of herinstallatie niet terugzetten als de
  gebruiker ze zelf weghaalde, bijv. voor een game met anti-cheat. Nu slaat `InstallPawnIo`
  PawnIO alleen over als `HKLM\...\Uninstall\PawnIO` bestaat, dus na verwijderen komt hij stil
  terug. Bijv. een parameter (`/NOPAWNIO`) of een vinkje in de wizard, en de keuze onthouden in
  de sleutel van de installer, zodat een volgende update hem ook overslaat
  (`installatie-uitleg`)
- De melding `PawnIoFailed` in `installer/BuurmansRgb.iss` ("Moederbord, RAM en videokaart
  werken dan misschien niet") gelijktrekken met de documentatie: zonder PawnIO werken RAM en
  oudere moederborden niet; een videokaart is niet onderzocht (`installatie-uitleg`)
- De Sandbox-test ook `sc qc PawnIO` laten loggen, zodat het starttype van PawnIO (nu "onbekend"
  in `installatie.md`) zonder echte pc bekend is (`installatie-uitleg`)
- De engine niet verhoogd starten bij een verhoogde stille installatie: `runasoriginaluser` werkt
  alleen als Setup zelf om UAC vroeg, dus vanuit een beheerdersvenster of als SYSTEM draaien de
  engine en de instellingen-app verhoogd (of als SYSTEM) tot de volgende aanmelding. Bijv. de
  engine dan niet starten (een `Check:` op de `[Run]`-regel die ziet dat Setup al verhoogd of als
  SYSTEM begon), of hem via een geplande taak als de aangemelde gebruiker starten
  (`installatie-uitleg`)
- Installer en OpenRGB ondertekenen (code-signing), zodat SmartScreen niet waarschuwt
- Na een release de Windows Sandbox-test automatisch draaien met de gedownloade setup, en de
  release pas zichtbaar maken (nu meteen) als die slaagt
- `BuurmansRgb.UI.exe` ook meenemen bij `dotnet publish` van alleen de engine; nu staan ze
  alleen na `dotnet build` van de solution (in `artifacts/app/`) of met `installer\build.ps1`
  samen
- Een check op updates: hooguit één keer per dag de GitHub Releases-API vragen om de nieuwste
  release (`GET /repos/<eigenaar>/BuurmansRgb/releases/latest`, de tag tegen de versie van de app)
  en bij een nieuwere bij Instellingen "Versie 1.4 is er" tonen met de link naar de release-pagina;
  niets downloaden of installeren, dat doet de gebruiker (of de beheerder) zelf. Een schakelaar om
  het uit te zetten, voor een pc die centraal beheerd wordt, en zonder internet geen melding.
  Zolang de repo privé is, geeft de API zonder token niets terug (zie het idee over de handleiding
  meeleveren) (testpanel 1-11-2026, Kees)
- De vraag bij verwijderen (`RemoveSettings` in `BuurmansRgb.iss`, "instellingen (kleuren en
  effecten)") ook de scènes en de kopieën in `backups` laten noemen: "Ja" haalt de hele map
  `%AppData%\BuurmansRgb` weg (refactorreview 3-11-2026)

## Tests en tooling

- Een script dat de schermafbeeldingen in `docs/images/` opnieuw maakt: engine en
  instellingen-app starten met vaste demo-instellingen en -scènes, de pagina's kiezen via UI
  Automation en het venster vastleggen met `PrintWindow`. De echte instellingen in
  `%AppData%\BuurmansRgb` blijven heel met `USERPROFILE` en `APPDATA` naar een lege map (zie de
  meettips in `ontwikkelaars.md`), maar de mutex, de events en de pipe hangen aan de
  Windows-sessie, dus de eigen engine en instellingen-app moeten zolang dicht. Met Windows
  vergrendeld lukt het niet altijd: op 23-10-2026 gaf `PrintWindow` om 07:45 een goed beeld, maar
  om 08:25 gaven `PrintWindow` en zelfs `RenderTargetBitmap` van een los rood vlak een leeg beeld;
  waarom, is niet uitgezocht
- Een meetscript in de repo voor CPU, geheugen en allocaties van de engine; nu draait elke meting
  met een eigen script buiten de repo (zie "Metingen" in `ontwikkelaars.md`). Het meet om en om
  tegen de basis, herhaalt met `dotnet-counters` de 0-allocaties-check op het draaiende proces
  (regenboog tegen statisch, niet alleen in een unit-test), logt voor en na elke run welke
  `BuurmansRgb*`-processen draaien en verklaart een run ongeldig als de engine al gestopt is
  vóór de eigen `--exit`; een eigen `APPDATA` scheidt alleen de bestanden, niet de mutex en de
  events van de sessie
- De andere pagina's (Apparaten, Scènes, Instellingen) en `MainWindow` offscreen laden met de
  resources van `App.xaml`, zoals `ColorListEditorViewTests` dat sinds #69 met `AppTheme` voor de
  pagina Kleur doet, zodat een fout in de XAML niet pas bij het starten opvalt. Bijv. voor
  `SettingsPage` controleren dat de schakelaars `IsOn` van hun viewmodel volgen, of bindingfouten
  opvangen via `PresentationTraceSources`: een hernoemde property breekt een binding nu stil,
  zonder dat een test of de build het merkt
- Uitzoeken wat er in een volledige testrun zo'n 270 ms CPU per seconde gebruikt in het
  testproces van `Providers.OpenRgb`, vlak voordat de CPU-test de verbinding laat vallen (los
  gedraaid is dat 0 ms); de test vergelijkt nu met die meting vooraf, dus hij hindert niet meer
- UI Automation (`InvokePattern.Invoke`) op een knop van de instellingen-app duurt op deze pc
  zo'n 2 seconden; uitzoeken of de app zelf dan bezig is (bijv. met het voorbeeld) of dat het aan
  UI Automation ligt
- Eén gedeelde schrijver voor `CustomEffectLibrary` en `LightingSettingsSaver`: beide schrijven
  hun bestand even na de laatste wijziging, loggen de eerste fout volledig en proberen het daarna
  opnieuw, elk met een eigen timer en eigen foutafhandeling
- Eén patroon voor de instellingen van Windows die de app volgt (contrastthema, tekstgrootte en
  animatie-effecten), bijv. een `ISystemSetting<T>`: de vorm is al gelijk, het gedeelde deel (de
  meldingen naar de dispatcher) zit in `SystemParameterMessages`, en de rest verschilt echt, dus
  het scheelt nu ~20–30 regels. Bij een vierde instelling opnieuw bekijken (audit 29-10)
- UI-teksten met een vast getal aan hun constante koppelen, via een placeholder of een test:
  "hooguit 12" in `CustomColorsAddToolTip` (`CustomColors.MaxColors`) en "tot 32 eigen effecten"
  in `CustomEffectNewToolTip` (`CustomEffects.MaxEffects`). Nu blijft de tekst stil staan als
  een grens verandert
- Een test die de teksten die tray-menu en instellingen-app delen gelijk houdt, zoals
  `EffectNamesTests` dat voor de namen van de effecten doet: `AutoStartLabel`, `AutoStartFailed`,
  `OpenLog` en `LogOpenFailed` van de app tegen `MenuAutoStart`, `AutoStartFailed`, `MenuOpenLog`
  en `LogOpenFailed` van de engine, en de `OpenRgb*`-teksten tegen `TrayOpenRgb*`. Nu staat dat
  alleen als opmerking in `Strings.resx`
- `FocusKeeperTests.ElementMadeAgain_GetsTheFocusBack` faalt af en toe: los gedraaid terwijl de
  UI-testset tegelijk draait 1 tot 2 van de 50 keer, ook in de testset zelf. Hij faalt altijd op
  de laatste controle (`after.IsKeyboardFocused`), meestal als eerste test in een nieuw proces
  (~400 ms). Bij de refactor na de review is de vermoede oorzaak geprobeerd, maar niet behouden
  (een uitgestelde render: de focus gaat weg vóór Unloaded en Loaded, en het vergeten op
  ApplicationIdle komt vóór de render): de sleutel pas vergeten na Unloaded, `s_lostKey`
  `[ThreadStatic]`, en `OffscreenWindow.WaitUntilIdle` eerst op één `CompositionTarget.Rendering`
  laten wachten. Daarna faalde hij nog 3 van de 50 keer, dus dat is het niet, of niet alleen.
  Volgende stap: in een falende run vastleggen welke focus- en laadgebeurtenissen er in welke
  volgorde komen
- `LedPreviewViewKeyboardTests.Render_WithTheFocus_DrawsARingAroundTheActiveLed` faalt als twee
  UI-testsets tegelijk draaien in zo'n 9 % van de runs (7 van de 80); heel af en toe ook
  `ShowActiveLed_RaisesFocusChangedForTheItemOfTheActiveLed`. Los van de focus-poging hierboven,
  want het gebeurde even vaak met als zonder die wijziging. Ook met één testset tegelijk: 2 van de
  20 runs, en 3 van de 40 nadat de caches van `ColorBrushes` en `LedBulb` per thread kwamen, dus
  die gedeelde caches waren het niet. In die 40 runs faalde ook twee keer
  `DirectionViewTests.Slider_HomeWithoutDirection_ChoosesZeroDegrees` op `slider.Focus()`.
  Bij de refactor na de audit van 29-10 faalde `Render_WithTheFocus_DrawsARingAroundTheActiveLed`
  weer 1 keer in 6 volledige testruns. Bij `ledvoorbeeld-opsplitsen`, met de pennen van de ringen
  per thread, faalde hij niet in 3 volledige testruns en 10 losse runs van de
  `LedPreview*Tests`
- `ProcessListTests.Find_SeesAProgramStartAndStop` faalde af en toe; de oorzaak is niet gevonden
  (0 van de 30 runs vóór en 0 van de 30 na de refactor na de audit van 29-10). Wel geeft
  `WaitUntilGone` het signaal van ping nu `TestTimeouts.Long`, en probeert `TempDirectory.Dispose`
  het wissen een tijd opnieuw. Faalt hij weer, leg dan vast op welke regel
- De rij "Scènes" in de testtabel van `docs/ontwikkelaars.md` is één enorme cel; herschrijven als
  een lijst per onderwerp (startscènes, tijden, Hele huis, programma's, apparaten van een scène),
  zodat je een test terugvindt (refactorreview 18-11-2026)
- Eén helper voor de 0-bytes-per-frame-tests van de engine: de opzet (`Rendered` abonneren, de
  klok een paar frames laten opwarmen, dan `Allocations.During` over een reeks
  `RenderAnimationFrame`s) staat nu los in 9 testklassen (`LightingControllerAnimationTests`,
  `...CustomEffectTests`, `...DirectionTests`, `...FlashTests`, `...OverlayTests`,
  `...SceneFadeTests`, `...ZoneCountTests`, `...ZoneFlashTests`, `...ZonesAlikeTests`)
  (refactorreview 23-11-2026)
- Een test die de XAML van de app doorloopt en faalt op een gewone `Button`, `RepeatButton` of
  `ToggleButton` met een vaste tekst als inhoud en een gebonden `AutomationProperties.Name`: die
  hoort een `LiteralNameButton`, `LiteralNameRepeatButton` of `LiteralNameToggleButton` te zijn,
  anders verliest een naam uit OpenRGB zijn eerste `_` voor de schermlezer. `UnderscoreNamesViewTests` vindt het nu alleen op de
  pagina's en in de toestanden die hij opent (`zone-namen-schermlezer`, thuistest 24-11-2026).
  Neem in de statusregel (`Views/StatusLine.xaml`) ook de knoppen `StatusLineAction` en
  `StatusLineDetailAction` (gewone `Button`s met een gebonden tekst als inhoud, dus de test moet
  ook een gebonden tekst meetellen) en de `DetailsToggle` (vaste tekst "Details", gebonden
  `DetailsAccessibleName`) mee: zolang `ActionAccessibleName` en `DetailsAccessibleName` alleen
  eigen teksten bevatten gaat het goed, maar bevat een van beide ooit een naam van OpenRGB of de
  gebruiker, dan verliest die zijn eerste `_`
  (refactorreview 25-11-2026)
- `LedColorStoreTests` bouwt zijn eigen `Board` en gebruikt `TestBoards` niet: die deelt het id
  `device` en `DeviceSource.Simulated` met de andere apparaten van de store (refactorplan 26-11-2026)
- `EngineRpcTests` (ca. 800 regels) per deel van `IEngineApi` splitsen, zoals de partials van
  `FakeEngineApi`: bij het delen van de nep-engine bleef de klasse heel, omdat de opzet (server en
  client over een pipe) per test hetzelfde is en splitsen alleen schuiven was (refactorplan 26-11-2026)
- `EngineHostTransferTests` ook op `EngineApiParts` zetten en daarna de korte constructor van
  `EngineApi` (de vijf vaste delen en `settingsTransfer`) weghalen, zodat er alleen
  `EngineApi(EngineServices)` is. Bij `engine-host-dunner` moest die testklasse byte voor byte
  gelijk blijven als bewijs dat bewaren en terugzetten niet veranderden; `SettingsAppSide` in
  `TrayPresenterTests` deelt zijn verlichting met de tray en past daarom niet in `EngineApiParts`
  (refactorplan 26-11-2026)
- `EngineApiTests` (ca. 1130 regels) per gedrag splitsen: `engine-host-dunner` raakte de klasse
  wel (de delen komen nu uit `EngineApiParts`), maar dat waren veldnamen en geen gedrag; splitsen
  was alleen schuiven en zou de diff van de refactor onleesbaar maken (refactorplan 26-11-2026)
