# Ideeën voor later

- Profielen per moment (gamen, werken, nacht)
- Alle apparaten synchroon laten lopen
- Eigen HID-providers voor apparaten die OpenRGB niet kent
- `BuurmansRgb.UI.exe` ook in de publish-map van de engine zetten; nu staan ze alleen na
  `dotnet build` samen in `artifacts/app/`
- Engine netjes laten afsluiten vanaf de commandline (bijv. `--exit` via het
  activatiesignaal), zodat scripts hem niet hoeven te killen en er geen spook-icoon blijft
- Autostart-pad bijwerken als de engine verplaatst is (nu toont het menu dan "uit")
- `InvariantGlobalization=true` staat andere UI-culturen niet toe; bij vertalen van de
  engine-teksten opnieuw afwegen
- Wijzigingen in `settings.json` live oppikken (FileSystemWatcher) in plaats van bij herstart
- Logging naar bestand (`Microsoft.Extensions.Logging`) voor fouten bij opstarten; nu gaan
  fouten van de IPC-server en de UI alleen naar `Trace`
- Geheugen van de engine bewaken: het budget in `docs/bouwplan.md` is working set
  (< 50 MB). Zonder UI zit de engine na taak 2 op ~49 MB working set (taak 1: ~48 MB), maar
  slechts ~11 MB private bytes: de rest zijn gedeelde pagina's van .NET en WinForms. Met een
  UI verbonden ~13 MB private, na het sluiten ~12 MB private. `MemoryTrimmer` brengt de
  working set daarna naar ~9 MB, maar dat zegt weinig: Windows zet de pagina's alleen op de
  standby-lijst en ze komen terug zodra de engine ze weer gebruikt (tray-menu, timer).
  Vergelijk daarom private bytes. Sinds taak 3 trimt de engine ook één keer na het opstarten
  (zie hieronder); echte winst zit in minder geladen assemblies
- Instellingen-app als enkele instantie: nu opent "Instellingen openen" bij een open
  venster een tweede venster in plaats van het bestaande naar voren te halen
- Kleur en helderheid per zone (bouwplan: "per apparaat, per zone en globaal"); de
  `IEngineApi` kent nu alleen apparaat en globaal
- In de instellingen-app tonen dat de effecten gepauzeerd zijn (nu laat het voorbeeld de
  kleur zien die na hervatten terugkomt)
- Een echte kleurkiezer (vlak of wiel voor verzadiging en helderheid) in plaats van twee
  schuiven; "helderheid" van de kleur zelf zit nu alleen in de hex-code
- De melding "De engine kon de wijziging niet verwerken." laten verdwijnen na de
  volgende geslaagde wijziging
- Frames naar de UI begrenzen (bijv. max. 30 per seconde) zodra taak 4 animeert: nu maakt
  `EngineApi` bij elke render per verbonden UI een kopie en een `LightingFrame`, buiten de
  lock van `LightingController`
- Gesimuleerde apparaten aan- en uitzetten vanuit de instellingen-app in plaats van alleen
  met `simulatedDevices` in `settings.json` en een herstart. De engine kan het al tijdens het
  draaien (`SwitchableDeviceProvider.IsEnabled`); er mist een `IEngineApi`-aanroep en een
  vinkje in de UI
- Adres en poort van de OpenRGB-server instelbaar maken (nu vast `127.0.0.1:6742`), en
  OpenRGB helemaal uit kunnen zetten
- Twee fouten in OpenRGB.NET 3.1.1 melden bij het project: `DeviceListUpdated` gaat nooit af
  (de client geeft de lege event-waarde bij het aanmaken door aan zijn verbinding) en na een
  nette sluiting door de server leest de read-loop op volle CPU nul bytes tot de volgende
  send faalt. Zodra dat is opgelost kan `OpenRgbServerWatcher` (de tweede verbinding) weg
- Apparaten zonder "Direct"- of "Custom"-modus toch een kleur geven via de modus "Static"
  met `UpdateMode` en de kleur van die modus
- Kleuren naar OpenRGB via een eigen wachtrij per verbinding sturen. Nu slaat `SetLeds` een
  frame over zolang een herlaadactie op de server wacht (zodat de lock van
  `LightingController` niet tot de aanvraag-timeout vastzit); bij animaties uit taak 4 kan
  dat zichtbaar haperen tijdens een rescan
- Geheugen na taak 3: zonder trim ~53 MB working set en ~12 MB private bytes (taak 2: ~49,6
  en ~11,0); het verschil zit vooral in Winsock en `System.Net.Sockets`. Daarom roept de
  engine `MemoryTrimmer.Trim` één keer aan als de lampen na het opstarten aan zijn. Gemeten
  (Release, statische kleur, geen OpenRGB-server, dus elke 5 s een verbindpoging): ~20 MB
  working set na 3 s, ~22 MB na 60 s, ~11 MB private bytes. Opent iemand het tray-menu of de
  instellingen-app, dan komen pagina's terug; na het sluiten van de UI trimt de engine weer.
  Het tray-menu zelf trimt niet; dat zou een volgende stap kunnen zijn
- In het tray-icoon of de instellingen-app tonen of OpenRGB verbonden is, en waarom niet
- OpenRGB.NET 3.1.1 spreekt hooguit SDK-protocol 4. Bij een nieuwere OpenRGB (1.0 en later)
  controleren of verbinden, de apparaatindexen en `UpdateLeds` nog werken
- Alleen de nieuwe apparaten aansturen als de lijst verandert; nu krijgen alle apparaten de
  kleur opnieuw als OpenRGB (opnieuw) verbindt
- Snelheid van de effecten instelbaar maken (periode van Ademen, rondes per seconde van
  Regenboog, golven per seconde van Golf), in `settings.json` en in de instellingen-app; nu
  vaste standaardwaarden in `EffectCatalog`. Ook `framesPerSecond` staat nog alleen in
  `settings.json`
- Frames naar de UI begrenzen wordt na taak 4 belangrijker: met de regenboog op "Alle
  apparaten" zit de instellingen-app (Debug) op ~147 MB working set, vlak onder het budget van
  150 MB. Een lagere preview-framerate of alleen de zichtbare apparaten versturen helpt
- De preview-frames loskoppelen van de render-loop: nu draait `EngineApi.OnRendered` op de
  timer-thread en maakt per frame een kopie van alle buffers en een `LightingFrame`, dus met de
  instellingen-app open alloceert elk frame en vertraagt UI-werk de animatie. Bijvoorbeeld de
  laatste uitvoer markeren en die op een eigen thread of met een lagere framerate versturen
- De animatie ook pauzeren bij vergrendelen of slaapstand (`SystemEvents.SessionSwitch`,
  `PowerModeChanged`), zoals het prestatiebudget noemt; nu stopt hij alleen via "Effecten
  pauzeren"
- De per-LED-editor met het toetsenbord bedienen (pijltjes, Shift+pijltje, Enter); nu kan
  het alleen met de muis in het voorbeeld
- In de per-LED-editor ongedaan maken, kleuren kopiëren naar een ander apparaat en een
  verloop met meer dan twee kleuren
- Per-LED-kleuren van een apparaat dat langer niet meer verschijnt opruimen; nu blijven ze
  in `ledColors` staan, net als de eigen kleur onder `devices`
- `dotnet-counters` gebruiken in een vaste meetstap (bijv. een script in `docs/`), zodat de
  0-allocaties-check ook op het draaiende proces gebeurt en niet alleen in een unit-test
- Golf en Regenboog per zone laten lopen in plaats van over het hele apparaat (bijv. elke
  fan-ring een eigen golf)
- Herstellen na het ontwaken ook bij Modern Standby (S0) controleren: of `PowerModeChanged`
  dan `Resume` meldt, verschilt per pc. Eventueel ook herstellen bij het aangaan van het scherm
  of bij ontgrendelen (`SessionSwitch`), als apparaten dan ook terug naar hun eigen effect gaan
- De wachttijden na het ontwaken (nu vast 2 en 10 seconden) instelbaar maken, of herstellen
  zodra OpenRGB de apparaten weer meldt
- Bij RAM-sticks met dezelfde naam de OpenRGB-locatie (SMBus-adres) tonen, zodat je ziet welke
  stick in welk slot zit; nu krijgen ze alleen een nummer in de volgorde van OpenRGB
- In de per-LED-editor een hele zone in één keer selecteren door op de zonenaam te klikken
- In de instellingen-app tonen dat een apparaat in OpenRGB geen "Direct"- of "Custom"-modus
  heeft (veel videokaarten), zodat je ziet waarom de kleur niet aankomt; nu staat dat alleen in
  de debug-uitvoer
- Een effect dat de videokaart (of alles) laat kleuren naar de temperatuur of belasting van de
  GPU, bijvoorbeeld via LibreHardwareMonitor, NVML of ADLX
- Bij twee videokaarten van hetzelfde model de OpenRGB-locatie tonen, net als bij RAM-sticks;
  nu krijgen ze alleen een nummer in de volgorde van OpenRGB
