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

- Een zone na een nieuwe lengte vanzelf even laten knipperen, zodat je meteen ziet of de lengte
  klopt (de laatste LED knippert mee of niet); nu moet je daarna zelf op "Knipperen" klikken.
  Ook: de wachtende zone-flits opnieuw starten zodra de apparatenlijst binnen is; nu stuurt
  "Knipperen" eerst een wachtende lengte, en de reload van OpenRGB daarna stopt de flits weer
- De LED's die geen zone hebben ("Overige LED's" in het voorbeeld) hebben geen knop
  "Knipperen", omdat de engine ze niet als zone kent; eventueel een flits voor een LED-bereik
  in plaats van een zone-index
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

- Een kleurvlak of -wiel naast de schuiven Rood, Groen en Blauw, voor wie liever op tint en
  verzadiging kiest
- Een gekozen kleur bij Kleur die nog niet op de lamp staat, gaat verloren als je een andere lamp
  kiest of de pagina wisselt; eventueel vragen of hem bewaren per lamp
- Een kleur op een lamp die uit staat, zet hem nu aan; de helderheid of een effect kiezen doet
  dat (nog) niet. Misschien moet ook een helderheid boven 0 % de lamp aanzetten, zoals bij een
  dimmer
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

- Met ↑ en ↓ in het voorbeeld naar de LED die in de zone erboven of eronder recht boven of
  onder de actieve staat; nu gaan ze vanaf de bovenste of onderste rij naar de laatste LED van
  de zone ervoor of de eerste van de zone erna, en in een ring meteen naar de zone ernaast
- Met het toetsenbord ook van de ene lamp naar de andere in het voorbeeld gaan (bijv. Page Up en
  Page Down); nu gaat dat met Tab, dat één keer per lamp stopt
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

- Bij "Alleen als het op de voorgrond staat" ook Store-apps met een eigen Store-venster
  herkennen: dat venster (`ApplicationFrameWindow`) hoort bij `ApplicationFrameHost.exe`, en de
  echte app staat in het kindvenster `Windows.UI.Core.CoreWindow`, waarvan het proces te lezen is
  (bijv. met `EnumChildWindows`). Nu telt zo'n app als `ApplicationFrameHost.exe`
- Bij "Alleen als het op de voorgrond staat" de instellingen-app niet laten tellen als
  wegwisselen, zodat je een voorgrond-scène kunt bijstellen terwijl hij brandt; nu komt bij het
  openen van de app de verlichting van daarvoor terug
- Bij "Alleen als het op de voorgrond staat" een korte wachttijd (bijv. een halve seconde)
  voordat de scène wisselt, zodat snel heen en weer alt-tabben de lampen niet laat flitsen; nu
  wisselen ze bij elk venster dat vooraan komt
- "Gemiste tijden inhalen" (#51) laat bij het starten de tijd altijd winnen, ook van een kleur
  of scène die je na de laatste tijd zelf koos, omdat de engine niet weet wat er gebeurde terwijl
  hij niet draaide. Met een tijdstempel van de laatste wijziging van de verlichting in
  `settings.json` kan hij alleen inhalen als de tijd daarna viel, zoals na het ontwaken
- Op de kaart van een scène met meer tijden ze allemaal kunnen zien, bijv. in een tooltip; nu
  noemt hij de eerste en "en nog 1"
- Een nieuwe tijd bij "Vanzelf aanzetten" laten beginnen op de dagen die nog geen tijd hebben
  (na 07:00 op werkdagen dus za en zo); nu begint hij op elke dag
- Ook inhalen als de klok van Windows over een tijd heen springt (nu alleen na het ontwaken en
  bij het starten; `IClockEvents` zegt niet of de klok verzet werd of de pc ontwaakte)

## Tray-menu

- De sneltoetsen van "Alles aan" en "Alles uit" (sinds #52, vast Ctrl+Alt+Shift+A en U)
  instelbaar maken, bijv. met een veld bij Instellingen dat de volgende toetscombinatie opvangt.
  Dan kan ook een toets die met een andere app (of met AltGr+Shift op een internationaal
  toetsenbord) botst, vervangen worden in plaats van uitgezet
- Een bezette sneltoets ook bij de schakelaar in de instellingen-app tonen; nu meldt alleen het
  tray-icoon het (en het logbestand)
- Sneltoetsen voor meer items uit het tray-menu, zoals "Effecten pauzeren" of een scène

## Pauzeren, slaapstand en wisselen van gebruiker

- De helderheid van "Dimmen" bij "Lampen tijdens deze pauze" instelbaar maken; nu vast 20 %
- Met "Dimmen" of "Uit" ook dimmen of uitgaan als de pauze uit het tray-menu al liep toen
  Windows vergrendelde of ging slapen; nu laat die pauze de lampen zoals ze zijn. Daarvoor moet
  de engine onthouden wat de lampen bij het begin van die pauze lieten zien
- Het voorbeeld in de instellingen-app laten zien dat de lampen gedimd of uit zijn; nu toont het
  tijdens zo'n pauze de echte kleuren
- "Lampen tijdens deze pauze" per reden kunnen kiezen, bijv. alleen bij de slaapstand uit en bij
  vergrendelen het laatste beeld
- Een eigen schakelaar voor pauzeren als het scherm uitgaat, los van vergrendelen en slaapstand;
  nu valt het onder dezelfde `pauseOnLockAndSleep`
- De reden van een pauze ook in de tooltip van het tray-icoon; nu staat daar alleen "effecten
  gepauzeerd"
- Na van gebruiker wisselen kan de engine van de eerste gebruiker nog kleuren sturen (een scène
  op tijd, herstellen na het ontwaken) terwijl de engine van de tweede gebruiker dezelfde lampen
  aanstuurt; eventueel niets meer sturen zolang de eigen sessie op geen scherm staat. Met
  "Dimmen" of "Uit" bij "Lampen tijdens deze pauze" stuurt hij bij het wisselen ook nog het
  gedimde beeld of zwart
- Herstellen na het ontwaken ook bij Modern Standby (S0) controleren: of `PowerModeChanged`
  dan `Resume` meldt, verschilt per pc. Eventueel ook herstellen (de apparaten opnieuw laden)
  bij het aangaan van het scherm (`IDisplayEvents`) of bij ontgrendelen; nu stuurt de engine
  dan alleen de kleuren opnieuw
- De wachttijden na het ontwaken (nu vast 2 en 10 seconden) instelbaar maken, of herstellen
  zodra OpenRGB de apparaten weer meldt

## Engine en prestaties

- Het geheugen van de instellingen-app onder het budget van 150 MB krijgen. Uitgezocht in #43
  (zie "Metingen" in `ontwikkelaars.md`): de .NET-heap is ~8 MB, de rest is WPF met DirectX.
  Tekenen zonder videokaart (`RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly`) brengt
  het naar 130–143 MB, maar kost op Lampen CPU; eventueel als keuze bij Instellingen, of alleen
  zonder bewegend effect
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
- `activeScene` met de hand in `settings.json` zetten om een scène aan te zetten; nu neemt de
  engine dat veld bij een wijziging met de hand niet over
- Een wijziging met de hand die samenvalt met een opslag van de engine (iets in de app
  gewijzigd binnen dezelfde seconde) gaat nu verloren; eventueel vóór het opslaan kijken of het
  bestand intussen door iemand anders veranderde

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
