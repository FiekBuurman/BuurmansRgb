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
  `LightingController` niet tot de aanvraag-timeout vastzit); bij een animatie kan dat
  zichtbaar haperen tijdens een rescan
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
  seconden "OpenRGB: verbinden…" tot de volgende poging), bij "niet bereikbaar" een knop om de
  service `OpenRGB` te starten (via UAC) in plaats van alleen de tip, en de versie van OpenRGB
  en het SDK-protocol in de tip, handig bij "geen goed antwoord"
- Bij het starten van de engine in de stand "Automatisch" staan de gesimuleerde lampen even
  in de lijst tot OpenRGB verbonden is (ongeveer een seconde). Eventueel pas na de eerste
  verbindpoging beslissen
- Een andere keuze voor de gesimuleerde lampen ook naar een tweede open instellingen-app
  sturen (een event naast `DevicesChanged`); nu ziet alleen de lijst de wijziging, niet de keuze
- In de instellingen-app een korte uitleg tonen als er geen headset is gevonden, met een
  link naar de lijst van OpenRGB; nu staat dat alleen in `handleiding.md`

## Zones en zonelengtes

- Per-LED-kleuren van een apparaat dat langer niet meer verschijnt opruimen; nu blijven ze
  in `ledColors` en `ledColorZoneLengths` staan, net als de eigen verlichting onder `devices` en
  de lengtes onder `zoneLengths`
- In de instellingen-app melden als OpenRGB een nieuwe lengte niet overneemt (de zone heeft
  na het herladen nog de oude lengte), of als een bewaarde lengte niet past bij het bereik dat
  de zone nu meldt (bijv. na een update van OpenRGB); nu staat dat alleen in het logbestand
- Vergeet OpenRGB een zonelengte terwijl de engine verbonden blijft (bijv. bij opnieuw zoeken),
  dan meldt het apparaat 0 LED's en komt de bewaarde lengte pas terug als het apparaat opnieuw
  verschijnt. Eventueel ook dan de lengte terugvragen, als blijkt dat OpenRGB dat doet
- Een zone waarvan het apparaat de naam twee keer heeft, krijgt nu geen bewaarde lengte (er is
  niet te zeggen welke van de twee het was). Eventueel naam plus volgnummer binnen die naam

## Welk apparaat is wat (knipperen)

- Ook één zone laten knipperen (bijv. met een knop naast de zonenaam), zodat je bij een
  moederbord met meer ARGB-headers ziet welke header welke fan of strip is; nu knippert het hele
  apparaat
- Het knipperen loopt op de framerate van de render-loop: bij een lage `framesPerSecond`
  (bijv. 1) zie je het wit-uit nauwelijks. Eventueel een eigen tempo voor het knipperen
- Een flits stopt meteen als de apparatenlijst verandert (bijv. als OpenRGB opnieuw zoekt);
  het apparaat krijgt dan wel zijn eigen kleuren terug

## Effecten

- Alle apparaten synchroon laten lopen. Een begin: na een wijziging aan de apparatenlijst
  lopen nieuwe apparaten pas weer gelijk met de rest als ze dezelfde snelheid hebben én die
  nooit veranderd is; `EffectClock` zou de klok van "Hele huis" kunnen delen met elk apparaat
  zonder eigen snelheid
- Golf en Regenboog per zone laten lopen in plaats van over het hele apparaat (bijv. elke
  fan-ring een eigen golf)
- Regenboog en Golf op een toetsenbord over de plek van de toetsen laten lopen (kolom of rij
  uit de matrix-map) in plaats van over de LED-volgorde; nu loopt de regenboog rij voor rij
  als OpenRGB de LED's zo nummert, maar springt hij bij andere toetsenborden
- Een effect dat de videokaart (of alles) laat kleuren naar de temperatuur of belasting van de
  GPU, bijvoorbeeld via LibreHardwareMonitor, NVML of ADLX
- Kleur en helderheid per zone (bouwplan: "per apparaat, per zone en globaal"); de
  `IEngineApi` kent nu alleen apparaat en globaal

## Instellingen-app en ontwerp

- De pagina Buren echt maken zodra het ontwerp er is (§ 5.4), met de buren-kaart op Lampen en
  "Samen aan de slag" op Scènes uit de schermen van het ontwerp; die zijn nu weggelaten, omdat er
  nog niets achter zit
- Een kleurvlak of -wiel naast de schuiven Rood, Groen en Blauw, voor wie liever op tint en
  verzadiging kiest
- Een gekozen kleur bij Kleur die nog niet op de lamp staat, gaat verloren als je een andere lamp
  kiest of de pagina wisselt; eventueel vragen of hem bewaren per lamp
- Een kleur kiezen voor een lamp die uit staat, zet hem niet aan; misschien moet "Op de strip
  zetten" de lamp dan ook aanzetten, zoals veel lamp-apps doen
- De navigatiebalk uitklapbaar maken (~240 px met het label naast het icoon), het alternatief uit
  § 6 van het ontwerp
- De Snap Layouts van Windows 11 (het menu bij het aanwijzen van de maximaliseerknop) ook bij de
  eigen titelbalk: dat vraagt `WM_NCHITTEST` met `HTMAXBUTTON` voor die knop
- Letterafstand van de koppen (−0,02em in het ontwerp): WPF kent geen letterafstand voor een
  `TextBlock`; eventueel met `Glyphs` of een eigen tekstelement voor de grote koppen
- De optische maten van Bricolage Grotesque gebruiken (de statische `12pt`- en `96pt`-bestanden):
  nu draaien alle maten op de standaardversie, terwijl een browser bij 30 px een grotere optische
  maat kiest

### Voorbeeld en per-LED-editor

- De per-LED-editor met het toetsenbord bedienen (pijltjes, Shift+pijltje, Enter); nu kan
  het alleen met de muis in het voorbeeld (en met een schermlezer)
- In de per-LED-editor ongedaan maken, kleuren kopiëren naar een ander apparaat en een
  verloop met meer dan twee kleuren
- In de per-LED-editor een rechthoek van toetsen selecteren (slepen over de matrix) in plaats
  van een reeks in LED-volgorde
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
- Het voorbeeld niet helemaal opnieuw opbouwen als de engine de apparaten opnieuw meldt, maar
  alleen de apparaten en zones die veranderden; nu houdt `FocusKeeper` de focus vast, maar een
  sleep over een schuif breekt af als er midden in het slepen een nieuwe apparatenlijst komt
- Een LED in het voorbeeld via UI Automation in beeld laten schuiven (`ScrollItem`) en hem
  `IsOffscreen` laten melden als hij buiten de zichtbare rand valt; nu volgen de LED's daarin de
  hele zone

## Scènes

- Scènes die in een tweede instellingen-app of op een andere manier veranderen, komen pas na het
  opnieuw openen in de lijst; een event `ProfilesChanged` naast `DevicesChanged` zou helpen
- Tijden alleen op bepaalde dagen (bijv. werkdagen 09:00 "Werk", weekend niet), of meer tijden
  per scène
- Na het ontwaken of bij het starten de scène aanzetten die volgens de tijden nu zou gelden (het
  laatste tijdstip dat voorbij is), als keuze naast het huidige "overslaan"
- Een programma alleen laten tellen zolang het op de voorgrond staat
  (`SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`), wisselen als je alt-tabt; dat werkt ook zonder
  pollen
- Bij "Aanzetten als dit programma start" een knop om een .exe te kiezen of een lijst met wat nu
  draait, zodat je de naam niet hoeft te weten

## Tray-menu

- Een sneltoets voor "Alles aan" en "Alles uit" (die staan sinds #41 in het tray-menu)

## Pauzeren, slaapstand en wisselen van gebruiker

- Bij vergrendelen of slaapstand de lampen uitzetten of dimmen, als keuze naast het huidige
  "laatste frame blijft staan"
- Ook pauzeren als alleen het scherm uitgaat zonder dat Windows vergrendelt
  (`RegisterPowerSettingNotification` met `GUID_CONSOLE_DISPLAY_STATE`); nu loopt de animatie dan
  door
- Het pollen van de proceslijst stilzetten bij vergrendelen ook als `pauseOnLockAndSleep` uit
  staat; nu volgt `ProgramWatcher` de pauze van de effecten
- Na van gebruiker wisselen kan de engine van de eerste gebruiker nog kleuren sturen (een scène
  op tijd, herstellen na het ontwaken) terwijl de engine van de tweede gebruiker dezelfde lampen
  aanstuurt; eventueel niets meer sturen zolang de eigen sessie op geen scherm staat
- Herstellen na het ontwaken ook bij Modern Standby (S0) controleren: of `PowerModeChanged`
  dan `Resume` meldt, verschilt per pc. Eventueel ook herstellen (de apparaten opnieuw laden)
  bij het aangaan van het scherm of bij ontgrendelen; nu stuurt de engine bij ontgrendelen
  alleen de kleuren opnieuw
- De wachttijden na het ontwaken (nu vast 2 en 10 seconden) instelbaar maken, of herstellen
  zodra OpenRGB de apparaten weer meldt

## Engine en prestaties

- Het geheugen van de instellingen-app onder het budget van 150 MB krijgen. Uitgezocht in #43
  (zie "Metingen" in `ontwikkelaars.md`): de .NET-heap is ~8 MB, de rest is WPF met DirectX.
  Tekenen zonder videokaart (`RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly`) brengt
  het naar 130–143 MB, maar kost op Lampen CPU; eventueel als keuze bij Instellingen, of alleen
  zonder bewegend effect
- De gloed van `LedBulb` hergebruiken in plaats van bij elke kleur een nieuwe
  `RadialGradientBrush` te maken (op Lampen met de regenboog ~2,2 MB/s aan allocaties), bijv.
  één bevroren gloed in wit met de kleur als `OpacityMask`, of een cache per kleur voor een
  statisch effect
- Geheugen van de engine: het budget in `docs/bouwplan.md` is working set, maar die telt
  gedeelde pagina's van .NET en WinForms mee; vergelijk daarom private bytes (~11–13 MB).
  `MemoryTrimmer` maakt de working set klein, maar de pagina's komen terug zodra de engine ze
  gebruikt (tray-menu, timer). Echte winst zit in minder geladen assemblies (bijv. Winsock en
  `System.Net.Sockets` sinds de koppeling met OpenRGB)
- `InvariantGlobalization=true` staat andere UI-culturen niet toe; bij vertalen van de
  engine-teksten opnieuw afwegen
- Wijzigingen in `settings.json` live oppikken (FileSystemWatcher) in plaats van bij herstart

## Installer en uitrol

- De installer de engine laten stoppen met `--exit` in plaats van `taskkill /F`, zodat er geen
  icoon in de tray blijft hangen. Dat kan pas als geen installatie meer een engine van 1.0.0
  heeft: die kent `--exit` niet en opent dan de instellingen-app
- Installer en OpenRGB ondertekenen (code-signing), zodat SmartScreen niet waarschuwt
- Na een release de Windows Sandbox-test automatisch draaien met de gedownloade setup, en de
  release pas zichtbaar maken (nu meteen) als die slaagt
- `BuurmansRgb.UI.exe` ook meenemen bij `dotnet publish` van alleen de engine; nu staan ze
  alleen na `dotnet build` van de solution (in `artifacts/app/`) of met `installer\build.ps1`
  samen

## Tests en tooling

- Een script dat de schermafbeeldingen in `docs/images/` opnieuw maakt: engine en
  instellingen-app starten met vaste demo-instellingen en -scènes, de pagina's kiezen via UI
  Automation en het venster vastleggen met `PrintWindow`. Het moet de echte instellingen in
  `%AppData%\BuurmansRgb` niet aanraken (bijv. een omgevingsvariabele of argument voor een
  andere map), en werkt alleen als Windows niet vergrendeld is
- Een script voor de `dotnet-counters`-meting (regenboog tegen statisch, zie "Metingen" in
  `ontwikkelaars.md`), zodat de 0-allocaties-check op het draaiende proces bij elke
  engine-wijziging herhaald kan worden en niet alleen in een unit-test gebeurt
- Een automatische test die de echte pagina's laadt (bijv. offscreen met de echte `App`-resources),
  zodat een fout in de XAML niet pas bij het starten opvalt; `RenderTargetBitmap` werkt niet zolang
  Windows vergrendeld is, dus een vergelijking van schermafbeeldingen kan alleen in een open sessie
- Uitzoeken wat er in een volledige testrun zo'n 270 ms CPU per seconde gebruikt in het
  testproces van `Providers.OpenRgb`, vlak voordat de CPU-test de verbinding laat vallen (los
  gedraaid is dat 0 ms); de test vergelijkt nu met die meting vooraf, dus hij hindert niet meer
- UI Automation (`InvokePattern.Invoke`) op een knop van de instellingen-app duurt op deze pc
  zo'n 2 seconden; uitzoeken of de app zelf dan bezig is (bijv. met het voorbeeld) of dat het aan
  UI Automation ligt
- `MainViewModel` verder opsplitsen: het LED-bewerken (`EditLedsAsync`, `SendLeds`,
  `ClearLedSelection`, `FillGradient`) naar een eigen `LedEditCoordinator`. Bij de refactor na
  de review bleef het staan: zo'n klasse heeft de verbinding, de wachtrij, het voorbeeld, de
  kleurkiezer en de effectkeuze nodig, en de bewerk-logica zelf zit al in `LedEditorViewModel`.
  Pas oppakken als het LED-bewerken groeit (bijv. per zone)
