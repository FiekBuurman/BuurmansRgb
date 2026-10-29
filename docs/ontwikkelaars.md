# Voor ontwikkelaars

Bouwen, starten en testen, de installer, en hoe BuurmansRgb van binnen werkt. Hoe je de app
gebruikt, staat in de [handleiding](handleiding.md); het plan en de stand per taak in
[bouwplan.md](bouwplan.md).

## Bouwen, starten en testen

Nodig: Windows 10/11 en de .NET 10 SDK. Voor echte hardware ook OpenRGB met de SDK-server aan
(zie "OpenRGB koppelen" in de [handleiding](handleiding.md#openrgb-koppelen)); zonder OpenRGB
werkt alles met gesimuleerde apparaten.

Open `BuurmansRgb.slnx` in Visual Studio 2026, of vanaf de commandline:

```bash
dotnet build BuurmansRgb.slnx -warnaserror
dotnet test --solution BuurmansRgb.slnx
```

Engine en instellingen-app bouwen allebei naar `artifacts/app/<Debug|Release>/`, zodat de
engine de instellingen-app naast zich vindt. Bouw dus eerst de hele solution; alleen de engine
bouwen neemt de instellingen-app niet mee. Daarna:

```bash
dotnet run --project src/BuurmansRgb.Engine   # engine met tray-icoon
dotnet run --project src/BuurmansRgb.UI       # instellingen-app los (of via het tray-menu)
```

Zonder OpenRGB staan er twaalf gesimuleerde apparaten klaar, zie "Instellingen" in de
[handleiding](handleiding.md#instellingen).

## Projecten

| Project | Wat erin zit |
| --- | --- |
| `BuurmansRgb.Core` | Modellen (`RgbColor`, `RgbDevice`, `RgbZone`, `LedMatrix`), de plekken van de LED's (`LedPlaces`, `WideKeys`), `IDeviceProvider`, `IEffect`, `IDirectedEffect`, `ServerStatus`; geen hardware, UI of IPC |
| `BuurmansRgb.Effects` | De effecten, tijdgebaseerd, schrijven in een buffer die de engine hergebruikt |
| `BuurmansRgb.Contracts` | `IEngineApi`, de DTO's en de opzet van pipe en StreamJsonRpc, en wat engine en app allebei gebruiken (`ProgramNames`, `ProcessImageName`, `ShellLauncher`, `SingleInstance`, `FileLog`) |
| `BuurmansRgb.Providers.Simulated` | De gesimuleerde apparaten |
| `BuurmansRgb.Providers.OpenRgb` | De koppeling met de SDK-server via OpenRGB.NET |
| `BuurmansRgb.Engine` | Tray, render-loop, IPC-server, opslag, scènes, pauzeren (WinExe met WinForms) |
| `BuurmansRgb.UI` | De instellingen-app (WPF, CommunityToolkit.Mvvm) |

Pakketversies staan centraal in `Directory.Packages.props`; `nuget.config` gebruikt alleen
nuget.org. `MessagePack` is vastgepind op een versie zonder bekend lek, omdat StreamJsonRpc
een oudere meebrengt. UI-teksten staan in `src/BuurmansRgb.UI/Resources/Strings.resx` en
`src/BuurmansRgb.Engine/Resources/Strings.resx`; de namen van de gesimuleerde apparaten in
`src/BuurmansRgb.Providers.Simulated/Resources/Strings.resx`.

## Tests

Tests gebruiken xUnit v3 op Microsoft.Testing.Platform (ingesteld in `global.json`) en draaien
zonder hardware. Per project een testproject in `tests/`. Helpers die elk testproject gebruikt
(`Eventually`, `TestColors`, `TestCancellation`, `TestTimeouts`) staan in `tests/Shared`;
`tests/Directory.Build.props` bouwt ze mee. De OpenRGB-server in het geheugen
(`FakeOpenRgbConnector`, `InMemoryOpenRgb`) staat in het hulpproject
`tests/BuurmansRgb.Providers.OpenRgb.Testing`, zonder eigen tests; daarnaast is er een kleine
SDK-server op een loopback-socket (`OpenRgbTestServer`, in `BuurmansRgb.Providers.OpenRgb.Tests`).
De viewtests van de instellingen-app draaien in een echt venster buiten beeld (`OffscreenWindow`,
op `OffscreenWindow.Offset`); `PageViews.Press` drukt er een toets zoals het toetsenbord dat doet,
en `LiveRegionCapture` vangt wat een schermlezer zou voorlezen en zet `LiveRegion` daarna terug.

| Onderdeel | Belangrijkste testklassen |
| --- | --- |
| Effecten en render-loop | Een klasse per effect in `BuurmansRgb.Effects.Tests` (`EffectSamples` volgt één LED door de tijd, bijv. hoe één LED van een muis pulseert; bij Fonkelen, Komeet, Scanner en Vuur ook met een lijst van kleuren: Komeet elke ronde de volgende en de staart van de vorige ronde in zijn eigen kleur, Scanner elke keer heen of terug de volgende, Fonkelen elke fonkeling één kleur uit de lijst die hij houdt, Vuur van de eerste naar de laatste kleur, en één kleur in een lijst geeft precies het beeld van vóór de lijsten, vastgelegd als LED-kleuren), `EffectMathTests` (de vaste toevalsbron van Fonkelen en Vuur: elke uitkomst even vaak, dezelfde invoer hetzelfde getal, een eigen reeks per seed), `EffectCatalogTests` (ook dat precies de effecten die `EffectIds.UsesDeviceColor` noemt, er in een andere kleur van het apparaat anders uitzien; `EffectIdsTests` voor een eigen en een onbekend effect) en `LightingControllerEffectTests` (elk effect zijn eigen lijst, zonder lijst de kleur van het apparaat of het warme vuur; de seed per apparaat, een FNV-1a-hash van zijn id: twee even lange apparaten fonkelen en branden bij "Hele huis" anders, en na een herstart weer hetzelfde), `FireEffectTests` (ook geen strepen om de drie LED's, en tussen twee stappen in de tijd niet vlakker dan op een stap), `AnimationLoopTests`, `LightingControllerAnimationTests` (een frame zonder verbonden app alloceert 0 bytes, met Regenboog, Fonkelen, Komeet, Scanner, Vuur en Kleurenloop, de laatste vijf ook met vier en met twaalf kleuren), `LightingControllerSpeedTests`, `EffectClockTests` |
| Instellingen | `JsonSettingsStoreTests` en `EngineSettingsTests` (ook `effectColors`: bewaren per effect, zonder lijst de standaard, na "Standaardkleuren" weer uit het bestand, een kleur die niet te lezen is, een lijst die te lang, leeg of van een ander effect is; 12 kleuren in `colorLoop` en `effectColors` gelezen, 13 de standaard), `LightingSettingsSaverTests` (ook `HoldSaves`, `Adopt` en wachten op een wijziging met de hand), `EngineHostTests`, `JsonSettingsStoreChangeTests` (een wijziging met de hand tegenover de eigen opslag, half geschreven en kapot, een bestand dat open is of weg, `SaveIfUnchanged`), `SettingsReloaderTests` (de rust na de laatste wijziging, geen pollen; met `FakeSettingsFileEvents` en een nepklok), `EngineHostReloadTests` (opties, verlichting en zonelengtes toepassen en wat de app hoort; ook twee met de echte `FileSystemWatcher` in een tijdelijke map: de eigen opslag daarna komt niet terug, en een kapot bestand verandert niets tot het weer klopt), `SettingsFileWatcherTests` (de echte watcher: met de hand, via een tijdelijk bestand en rename, een ander bestand in de map), `EffectOptionsViewModelTests` (vloeiendheid en pauzeren bij vergrendelen in de app, en wat de apparaten tijdens die pauze doen), `SwitchOptionViewModelTests` (de schakelaars voor gemiste tijden inhalen en de sneltoetsen), `EnumOptionTests` (de Nederlandse naam van een keuze in een lijst, of de Engelse zonder vertaling), `customColors` in `JsonSettingsStoreTests` (bewaren en teruglezen, zonder eigen kleuren niet in het bestand, een kleur die niet te lezen is of twee keer staat valt weg, van 14 de eerste 12), `EngineHostTests` (bij het starten de bewaarde eigen kleuren, een wijziging bewaard), `EngineHostReloadTests` (met de hand gewijzigd: toegepast, en de app hoort het) en `SettingsViewTests` (de echte pagina zonder de kaart "Opslaan": of opslaan lukte, staat in de statusregel) |
| OpenRGB | `OpenRgbDeviceMapperTests`, `OpenRgbDeviceProviderTests`, `OpenRgbNetConnectionTests`, `OpenRgbProviderStatusTests`, `OpenRgbProviderServerTests`, `OpenRgbServerWatcherTests`, `OpenRgbPacketsTests` (de kop van een pakket, de naam van de client en pakket 140 om opnieuw te zoeken), `OpenRgbProviderCpuTests` (sluit de server de verbinding, dan laat de provider hem los) |
| Gesimuleerde apparaten | `SimulatedDeviceProviderTests`, `SimulatedDevicesSwitchTests`, `SwitchableDeviceProviderTests`; de regel uitleg op Apparaten in `MainViewModelLampTests` (alleen gesimuleerd, gemengd, alleen echt en geen apparaten bij "Automatisch", en "Altijd" en "Nooit"; weg met een echt apparaat dat erbij komt of met "Altijd", terug als het weer alleen gesimuleerde zijn), `LampsViewModelTests` (`HasOnlySimulatedLamps`), `DeviceItemTests` (`IsSimulated`) en `LampsPageViewTests` (op de echte `LampsPage`: de regel zichtbaar en in de UI Automation-boom, Tab van "+ Erbij knutselen" naar "Naar Instellingen", dat naar Instellingen gaat, weg met een echt apparaat, "Altijd" of "Nooit"; en de tooltip van "+ Erbij knutselen" over OpenRGB en Verversen) |
| Verversen | `OpenRgbServerWatcherTests` (de watcher stuurt pakket 140 en krijgt `DeviceListUpdated` terug), `OpenRgbProviderRescanTests` (met `FakeOpenRgbConnector` en een nepklok: een apparaat dat terugkomt staat in de lijst en krijgt zijn kleuren, geen update binnen de 5 s of een mislukte verzending geeft een nieuwe verbinding terwijl de apparaten in de lijst blijven, een verbinding die sluit, zonder verbinding meteen verbinden, OpenRGB niet bereikbaar, een verbinding die nog onderweg was krijgt de vraag zelf, opnieuw verbinden dat mislukt terwijl de oude verbinding werkt telt als bereikt, een tweede vraag die meeloopt), `OpenRgbProviderServerTests` (hetzelfde over de socket met `OpenRgbTestServer`: `DevicesAfterRescan` voor een gewijzigde lijst, `AnswersRescan` uit voor een OpenRGB van vóór 1.0), `SwitchableDeviceProviderTests` en `SimulatedDeviceProviderTests` (de gesimuleerde zoeken opnieuw, uitgezet telt als klaar), `LightingControllerRescanTests` en `LightingControllerOpenRgbRescanTests` (de koptelefoon uit en weer aan met `InMemoryOpenRgb`: na het verversen in de lijst met zijn eigen kleur; OpenRGB niet bereikbaar geeft `false`), `EngineIpcServerTests` en `EngineRpcTests` (`RescanDevicesAsync` over de pipe, de app hoort `DevicesChanged`), `DeviceRescanViewModelTests`, `MainViewModelRescanTests` (met `FakeEngineApi.RescanGate`; de melding "niet bereikbaar" gaat weg als OpenRGB weer verbonden is) en `RescanViewTests` (de knop houdt de focus, "Zoeken…", de melding als live region) |
| Richting | `LedPlacesTests` (de plekken van een rij, een fan-ring en een matrix-map met brede toetsen en LED's die de map mist; een strip op 0° telt als zijn index, op 180° andersom en op 90° in het midden; een toetsenbord en een ring schuin; elke zone zijn deel van het apparaat), `WideKeysTests`, `DirectedEffectTests` (Regenboog, Golf en Kleurenloop over plekken van i / n geven precies het beeld per index, andersom gespiegeld), `LightingControllerDirectionTests` (zonder richting op elk gesimuleerd apparaat bit voor bit het beeld van vóór de richting, ook na het weer uitzetten; met richting de plekken van `LedPlaces.Along`, een strip op 180° gespiegeld, de andere effecten ongemoeid, één apparaat apart, "Zones gelijk" over de plekken van de langste zone, buiten 0–359 geweigerd, 0 bytes per frame), `EffectCatalogTests` en `EffectDirectionTests` (welke effecten een richting kennen), `EngineApiTests` en `EngineRpcTests` (`SetDirectionAsync` en `Direction` in de verlichting), `JsonSettingsStoreTests` en `JsonProfileStoreTests` (`direction` bewaren en lezen, zonder niet in het bestand, met de hand buiten één slag, een breuk of een woord), `MainViewModelDirectionTests` en `DirectionViewTests` (de schuif alleen bij Regenboog, Golf en een verspreide Kleurenloop, naast de snelheid, met naam, uitleg en "Volgorde" of de graden voor een schermlezer; Home of een klik op het schuifje kiest 0°; "Volgorde van de LED's" met de focus daarna op de schuif) |
| Eigen effecten | `StepEffectTests` (blijven staan, overlopen, na de laatste stap de eerste, een patroon uitgerekt over meer of minder LED's, een stap zonder tijd overgeslagen, één stap staat stil, 0 bytes per frame), `CustomEffectsTests` (de `id`, de naam, de eerste vrije naam, de grenzen van stappen, kleuren en tijden, over JSON en terug), `LightingControllerCustomEffectTests` (de stappen op het apparaat, helderheid en snelheid, een effect dat er niet is wordt Statisch, ook in een scène, nieuwe stappen meteen zonder `LightingChanged`, dezelfde stappen houden hun `StepEffect`, verwijderen geeft Statisch als wijziging, 0 bytes per frame over alle gesimuleerde apparaten), `CustomEffectLibraryTests` (nieuw met een `id`, vervangen, op alfabet, dezelfde naam, de naam van een ingebouwd effect (ook in andere hoofdletters) of een onbekende `id` geweigerd, een ingebouwde naam uit het bestand wordt "Regenboog (eigen)", hooguit 32, een bestand dat even niet te lezen is en elke 5 s opnieuw geprobeerd wordt, het bestand een seconde na de laatste wijziging en bij afsluiten, ook na een schrijffout en een seconde daarna opnieuw, alleen de eerste fout met de hele stacktrace in het logboek, een stilgelegde timer na afsluiten met rust gelaten), `EffectNamesTests` (de ingebouwde namen van de engine gelijk aan die van de app, ook in elke vertaling), `JsonCustomEffectStoreTests` (bewaren en lezen, soepel lezen met de hand, kapotte JSON opzij), `EngineHostTests` (de effecten vóór de verlichting gelezen, een bestand dat bij het starten even niet te lezen is houdt het effect), `EngineApiTests` en `EngineRpcTests` (bewaren, kiezen en verwijderen over de pipe), `CustomEffectEditorViewModelTests` (een naam die niet bewaard is maakt plaats voor de echte met een melding die beide noemt, ook zonder Enter, en die weggaat als de engine het effect een andere naam geeft, Escape zet de naam terug zonder melding), `MainViewModelCustomEffectTests` (de effectkeuze met de eigen effecten, maken, mengen kleurt de gekozen stap, hernoemen, een lege naam of die van een ingebouwd effect met een melding geweigerd, verwijderen, Annuleren in de vraag houdt het effect, LED-kleuren overnemen, een lijst van de engine die een wijziging inhaalt, een wijziging die de engine weigert toont zijn lijst, verbinden terwijl de engine `effects.json` niet kan lezen) en `CustomEffectViewTests` (het paneel op de echte pagina: namen voor een schermlezer, Delete op een stap, de focus na − en na Verwijderen of Annuleren in de vraag, Enter in de naam, een naam die telt als het vak de focus verliest, een naam die het effect verschuift, een lege of bezette naam met een melding die een schermlezer hoort en de echte naam terug als het vak de focus verliest, Escape in de naam; "LED-kleuren van dit apparaat overnemen" bij "Hele huis" grijs en vaag, de stijl van elke uitgeschakelde link) |
| Uitleg bij het effect | `EffectOptionTests` (elke id in `EffectIds` heeft een zin, zodat een nieuw effect zonder zin faalt; een onbekend effect en een eigen effect zonder stappen geen; een eigen effect telt zijn stappen, "1 stap" enkelvoud, en een andere `Steps` geeft een nieuwe `Description`), `MainViewModelFollowLightingTests` (een ander effect van buiten de app, zoals een scène uit het tray-menu, geeft zijn zin), `MainViewModelCustomEffectTests` (een stap erbij in de editor, of een stap minder uit een andere instellingen-app, telt mee) en `EffectDescriptionViewTests` (op de echte `ColorPage`: de zin onder de keuze en als `HelpText` ervan, één keer in de UI Automation-boom, verandert mee; in het kleinste venster (960 px breed) en bij 1000 px staan de schuiven en "+ Eigen effect maken" voor elk effect op dezelfde plek en houdt de kaart zijn hoogte) |
| Vragen voor het verwijderen | `ConfirmDialogViewTests` (de dialoog zelf als echt venster, met het venster van de test als eigenaar; zelf buiten beeld gezet (`WindowStartupLocation.Manual`), want WPF zet een dialoog die midden op een eigenaar buiten beeld hoort op het bureaublad, en elke test controleert dat hij daar blijft; alleen de test van het centreren toont hem even op het scherm, midden op een eigenaar op het scherm: "Annuleren" en "Verwijderen" uit `Strings.resx`, ook als naam voor UI Automation, de focus begint op Annuleren, Tab blijft in de dialoog, de vraag als naam van het venster en de uitleg als `HelpText`, zonder uitleg alleen de vraag, een lange uitleg loopt om binnen de breedte van de dialoog, het icoon met woordmerk van de titelbalk, Enter doet de knop met de focus, Escape, het kruisje en Annuleren zeggen nee; het venster is precies de inhoud plus de rand, zonder lege ruimte van de Windows-rand die `WindowChrome` weghaalt, en staat midden op de eigenaar; `ForScene` en `ForCustomEffect` vragen met de naam, alleen het eigen effect met de uitleg over Statisch), `ProfilesViewModelTests` en `CustomEffectEditorViewModelTests` (met een nep-bevestiging: ja verwijdert, nee niet, met de naam in de vraag; komt er tijdens de vraag een ander effect, dan verwijdert ja toch het effect waar het om ging), `MainViewModelCustomEffectTests` en `CustomEffectViewTests` (Annuleren: niets naar de engine, het paneel blijft en de focus op Verwijderen) |
| Zones en lengtes | `RgbZoneTests`, `LedMatrixTests`, `LightingControllerZoneTests`, `LedColorStoreTests`, `ZoneLengthKeeperTests`, `ZoneLengthKeeperOpenRgbTests`, `ZoneLengthViewModelTests` (ook "Kies 0 tot 60 LED's." met het bereik van de zone bij een getypte lengte die geen getal is of die de zone niet kan hebben, niet bij een leeg vak, en alleen opnieuw gemeld als de melding verandert) en `LedPreviewPanelViewTests` (die melding onder het vak, als live region) |
| Zones gelijk | `RgbZoneTests` (`PartsOf`), `DeviceOutputTests` (het beeld van de langste zone uitgerekt over elke zone, even lange zones, "Overige LED's", een zone zonder LED's, één zone, helderheid en een flits over het beeld), `LightingControllerZonesAlikeTests` (twee zones van één LED in een regenboog één kleur, en bij Fonkelen, Komeet, Scanner en Vuur samen één kleur die beweegt, per LED niet, onbekend apparaat, geen `LightingChanged`, een scène laat het staan, een apparaat dat later komt, een zone die langer wordt of zijn eerste LED's krijgt, een zone knipperen, 0 bytes per frame), `JsonSettingsStoreTests`, `EngineHostTests` en `EngineHostReloadTests` (`zonesAlike` lezen, bewaren, met de hand en niet terugschrijven), `EngineApiTests`, `EngineRpcTests` en `DeviceInfoTests` (`SetZonesAlikeAsync` en `DeviceInfo.ZonesAlike`), `DevicePreviewViewModelTests` en `MainViewModelTests` (de schakelaar alleen bij twee of meer zones met LED's, sturen en volgen, een kleur die je mengt blijft bij het omzetten, zoals bij elke nieuwe lijst apparaten: zie "Pagina's") |
| Knipperen en plek | `LightingControllerFlashTests` (ook een apparaat zonder LED's, dat de flits van een ander stopt), `LightingControllerZoneFlashTests` (alleen de LED's van één zone, een zone zonder LED's of die er niet meer is, een nieuwe lengte tijdens het knipperen, 0 bytes per frame), `RgbZoneTests` (`RangesOf`), `DevicePreviewViewModelTests` en `MainViewModelTests` (de knop per zone, eerst een lengte die nog wacht, een knop annuleert de flits die bij de apparaatkeuze wacht), `LedPreviewPanelViewTests` (de knoppen van het apparaat en van elke zone heten allemaal "Knipperen", met elk een eigen naam voor een schermlezer), `DeviceInfoTests`, `DeviceLocationLabelsTests`, `DeviceItemTests` (de plek op de kaart; de tooltip van de kaart met de hele naam, die een smalle kaart afkapt, en de hele locatie) |
| LED's tellen | `LightingControllerZoneCountTests` (met `ManualClock`, een klok waarvan de timers niet afgaan: LED 1 groen en de rest van de zone uit, één LED per tik in het tempo, voorbij de LED's die de zone nu heeft, stoppen bij de laatste, pauze, doorgaan, −/+ binnen het bereik, ander tempo, een andere zone, pauze van de effecten, een nieuwe lijst met de zone langer of een andere zone op die plek, 0 bytes per frame met `CountChanged`, een `Sequence` die bij elke wijziging groeit), `ZoneCounterTests` (de grootste lengte erheen en de oude terug bij stoppen, de eigenaar die gaat, een andere zone, pauze, `StopAll` en `Dispose`; "Dit is de laatste" houdt de lengte, ook een lengte die de zone niet kan hebben; een lengte van elders of een apparaat dat weg is stopt zonder terugzetten; de grootste lengte wordt niet bewaard en een apparaat dat terugkomt krijgt zijn oude lengte; een lijst met de oude lengte vóór de grootste laat hem doortellen; een zone met 0 LED's bewaart niets), `ZoneLengthKeeperTests` (`ResizeForAWhile` en `GiveLengthBack`: niet onthouden, 0 niet bewaren, de laatst gevraagde lengte wint), `ZoneLengthKeeperOpenRgbTests` (tellen en stoppen tegen de nep-OpenRGB; het afsluiten wacht op de teruggezette lengte, ook als de app de telling al stopte), `OpenRgbDeviceProviderTests` (`WaitUntilIdle` bij het afsluiten), `EngineIpcServerTests` en `EngineRpcTests` (over de pipe, de app die weggaat, Verversen), `ZoneCountViewModelTests`, `PreviewViewModelTests` (een eigen telling na "Dit is de laatste" of een `None` van de engine, een stap na een stop in de app, een stap van vóór de start bevestigt de eigen telling niet, een pauze of geweigerde start vóór de bevestiging maakt hem niet meer eigen), `MainViewModelZoneCountTests` (met `FakeEngineApi.RaiseZoneCount`: een oudere stand valt weg, alleen een eigen telling stopt vanzelf, een geweigerde start is geen eigen telling, de stand bij het verbinden) en `ZoneCountViewTests` (namen voor een schermlezer, de status als live region, de focus naar "Tellen" na "Stoppen") |
| Aan en uit | `LightingControllerOnTests`, `LampsViewModelTests`, `MainViewModelLampTests` (ook een kleur op een apparaat dat uit staat zet het aan, na de kleur in de wachtrij, en bij de loop of LED's die meteen meekleuren één keer; bij de kleurenloop in `MainViewModelColorLoopTests`) |
| Scènes | `ProfileLibraryTests`, `EngineHostTests` en `MainViewModelSceneTests` (ook welke scène aan staat, ook na een herstart), `ProfileAutomationTests` (meer tijden: volgorde, dubbele, hooguit 8), `LoadTimeTests` (een getypte tijd op één plek voor de app en de engine: `23:00`, `7:30`, `21.30`, en zonder iets ertussen `2130`, `0930` en `930`; `2400`, `960` en `9u` niet), `JsonProfileStoreTests` (ook `loadTimes` naast het oude `loadAt` en `loadOn`, `effectColors` en een scène van vóór de kleuren per effect, een tijd met de hand zoals de app hem leest, en waarden die niet te lezen zijn), `DailyTimeTests` (ook op gekozen dagen), `ProfileTimerTests` (ook meer tijden per scène, elk op eigen dagen, en gemiste tijden inhalen na het ontwaken en bij het starten, met de echte `ResumeRestorer` op een nepklok; bij het starten terwijl een spel draait ook in `EngineHostTests`, met de echte `ProgramWatcher`), `ProfileTriggersTests` (ook een tijd terwijl Windows vergrendeld is, met `FakeAwayState`, en terwijl een programma op de voorgrond staat), `ProgramWatcherTests`, `ProgramWatcherForegroundTests` (alleen op de voorgrond: met `FakeForegroundWindow` en `FakeRunningPrograms`, wisselen tussen twee voorgrond-scènes, samen met "zolang het draait", vergrendeld, gesloten en opnieuw vooraan, het vinkje aan en uit), `SystemForegroundWindowTests` (welke vensters meetellen, ook dat een schermbeveiliging niet telt, het eigen proces en het bureaublad, ook twee keer na elkaar, en naar de gedeelde hook luisteren alleen zolang er gevolgd wordt), `ForegroundHookTests` (één echte `SetWinEventHook` zolang iemand luistert, elk venster naar elke luisteraar, ook als er een gooit of tussendoor stopt, en met een verzonnen `SetWinEventHook` die eerst weigert: wie luisterde krijgt de vensters van een latere hook; ook in `SystemForegroundWindowTests` en `SystemScreenSaverEventsTests`), `ProcessListTests`, `ProfilesViewModelSceneTests`, `SceneCardTests` (ook de warme bolletjes van Vuur, de kleuren van een effect met een lijst, en de eerste vijf van twaalf kleuren), `ProfileAutomationViewModelTests` (ook tijden toevoegen en weghalen, `2130` en `930` als tijd, het vinkje voor de voorgrond, en uit "Kies uit wat nu draait" de exe-naam in het veld; de lijst wacht op het lezen en toont de nieuwste, en dezelfde programma's laten de lijst staan, zonder `PropertyChanged`), `MainViewModelProfileTests` (ook `930` na "Toepassen" als 09:30 in het veld), `WindowedProgramsTests` (de lijst van "Kies uit wat nu draait" uit een verzonnen lijst van processen met verzonnen beschrijvingen: de beschrijving uit het exe-bestand met de exe-naam erachter, anders (ook als de beschrijving alleen de exe-naam herhaalt) de venstertitel, anders de exe-naam; een beschrijving die niet te lezen is (andere gebruiker, beveiligd, beëindigd, bestand weg) valt terug; op alfabet van de leesbare naam, elke exe één keer, een lange titel afgekapt (zonder een emoji doormidden te knippen), onderdelen van Windows zoals `TextInputHost.exe` en `ApplicationFrameHost.exe` weg zonder hun bestand te lezen, en elk van `ProgramNames.ShellParts`; alleen het eigen testproces wordt echt gelezen, voor `SystemProcesses.Description`), `ProcessImageNameTests` (de exe-naam en het pad van het eigen proces; het idle-proces heeft geen naam), `RunningProgramsViewTests` (de echte pagina met verzonnen programma's: elk item heet "Outlook (olk.exe)" voor een schermlezer, de lijst is al gelezen zodra de scène opengaat, en een keuze zet alleen de exe-naam in het veld) en `SystemClockEventsTests` (ontwaken meldt een verandering van de klok) |
| Pauzeren en slaapstand | `LightingControllerPauseTests`, `LightingControllerPausedLightsTests` (wat de apparaten bij elke stand en elke reden krijgen, en na het hervatten; de handmatige pauze stuurt niets), `LightingControllerRestoreTests`, `AutoPauseTests` (ook `Away` met het pauzeren uit, het scherm uit met `FakeDisplayEvents` en de schermbeveiliging met `FakeScreenSaverEvents`, samen met vergrendelen en slaap, en reeksen van gebeurtenissen met `pausedLights` op "off", ook zwart vóór het slapen en na het ontwaken met `ResumeRestorer`), `ResumeRestorerTests`, `SystemSessionEventsTests`, `SystemDisplayEventsTests` (het lezen van `POWERBROADCAST_SETTING`, een `WM_POWERBROADCAST` via `SendMessage` op de eigen thread, en de echte registratie zonder waarschuwing), `WindowsProgramsTests` (welke exe-namen een schermbeveiliging of het vergrendelscherm zijn), `SystemScreenSaverEventsTests` (elk venster ervan melden en het einde één keer, een ander venster vooraan beëindigt hem, met de echte `ForegroundHook` en samen met `AutoPause` opnieuw pauzeren na ontgrendelen; met verzonnen vensters, een verzonnen `SPI_GETSCREENSAVERRUNNING` en `FakeTimeProvider`: "Voorbeeld" telt niet en komt in het logboek, wat Windows bevestigt telt, ook een seconde later, maar niet na een ander venster, en er wordt maar één keer opnieuw gevraagd; het vergrendelscherm beëindigt hem niet, zodat de pauze met `AutoPause` zonder gat doorloopt tot het ontgrendelen; geen test start een echte schermbeveiliging, en de echte vlag wordt alleen gelezen), `EffectsPauseViewModelTests`, `MainViewModelPauseTests` (de melding volgt de pauze van de engine, "Hervatten" na de wijzigingen die nog wachten), `ProgramWatcherTests` (niet in de proceslijst kijken zolang Windows vergrendeld is, slaapt of op geen scherm staat, ook via een extern bureaublad en een slaap die met ontgrendelen eindigt, met `pauseOnLockAndSleep` aan en uit; met `FakeSessionEvents`, `FakePowerEvents` en `FakeRunningPrograms`; andere tests van `ProgramWatcher` krijgen `FakeAwayState`, waarbij niemand weg is) |
| Sneltoetsen | `TrayPresenterTests` (een toets doet wat "Alles aan" of "Alles uit" doet, aan en uit zetten, een bezette toets; met `FakeGlobalHotkeys`), `AllOnOffHotkeysTests`, `WindowsGlobalHotkeysTests` (de echte `RegisterHotKey` op Ctrl+Alt+Shift+F24, die op geen toetsenbord zit: twee keer registreren faalt en wordt gelogd; een `WM_HOTKEY` via `SendMessage` op de eigen thread draait de actie van de toets met dat vaste id en na `Unregister` niets meer, en `IsRegistered` volgt dat; is de toets bezet door een andere testrun of is er geen bureaublad, dan slaat de test zich over), `EngineHostTests` en `JsonSettingsStoreTests` (`hotkeys`), `EngineApiTests`, `SwitchOptionViewModelTests` |
| Tray en opstarten | `TrayPresenterTests` (ook dat het tray-menu en de app gelijk lopen: een klik in het menu bereikt de app via `OptionsChanged`, en wat de app zet, toont het menu als het opengaat), `SettingsAppLauncherTests` (de instellingen-app ontbreekt of is geen programma), `SingleInstanceTests`, `EngineOptionsTests`, `AutoStartExtensionsTests` (nooit tegen de echte Run-sleutel), `RegistryAutoStartTests` (ook een verplaatste engine; met een eigen sleutel onder `HKCU\Software\BuurmansRgb.Tests`), `AutoStartSwitchTests` ("Starten met Windows" van tray en app samen: `Changed` alleen bij een andere waarde, een register dat niet te schrijven of te lezen is), `EngineApiTests` en `EngineRpcTests` (`GetAutoStartAsync` en `SetAutoStartAsync` met zijn antwoord), `MainViewModelAutoStartTests` en `SettingsViewTests` (de schakelaar bij Instellingen, die het tray-menu volgt en bij een weigering terugspringt met de melding); alles met `FakeAutoStart` |
| Herstarten | `ServiceRestarterTests` (de volgorde OpenRGB stoppen, PawnIO stoppen, PawnIO starten, OpenRGB starten; PawnIO dat niet stopt, niet bestaat of onder een andere naam als driver staat; OpenRGB dat niet stopt of niet start), `ServicesRestartReportTests` (de exitcode heen en terug, ook met de oorzaak van de eerste fout, een crash leest als mislukt, elke waarde van de enums past in zijn bits), `ElevatedServicesRestartTests` (`runas` met `--restart-services`, een geweigerde UAC, een stap die faalt of te lang duurt, "Afsluiten" tijdens de UAC-vraag of het wachten, de oorzaak uit de exitcode in het log, zonder OpenRGB en PawnIO geen UAC), `ServiceControlTests` (starten van een service die nog stopt, stoppen van een service die nog start, "draait al" alleen als hij echt draait, time-outs en foutcodes), `EngineRestarterTests` (de nieuwe engine krijgt `--restarted` met de uitkomst en `--open-settings` als de app open was, een nieuwe engine die niet start, de echte `SettingsAppWindow` via een eigen mutex), `EngineLauncherTests` (de engine-exe, ook hernoemd, en de exe naast de engine als hij via `dotnet BuurmansRgb.Engine.dll` draait), `TrayPresenterTests` (de tooltip en het grijze menu-item tijdens het herstarten, één herstart tegelijk, de melding na afloop en de app weer open), `SingleInstanceTests` (`AcquireWhenFree`, `SignalExitIfRunning`), `PreviousEngineTests` (een oude engine die laat afsluit en niet meer naar het exit-signaal luistert: de nieuwe wacht nog eens en laat geen exit-signaal achter; blijft hij, dan stopt de nieuwe, met de waarschuwingen in een eigen `engine.restart.log`) en `WindowsServiceManagerTests` (alleen opzoeken). Alles met fakes (`FakeServiceManager`): geen test stopt of start een echte service of driver, of vraagt UAC |
| IPC | `EngineApiTests` (ook `SetEffectColorsAsync`: een kopie per effect, voor één apparaat of alle, een effect zonder kleuren of een lijst van 0 of 13 kleuren geweigerd, en een kleurenloop van 12 kleuren bewaard; en `ClearEffectColorsAsync`: weer de standaard, de andere lijsten blijven, bij "Hele huis" op elk apparaat), `EngineIpcServerTests`, `EngineRpcTests` (over een echte named pipe, ook `EffectColors` in de verlichting en `ClearEffectColorsAsync`), `EffectColorsTests` (welke effecten een lijst hebben, de standaard, `With` en `Without`, gelijkheid per lijst), `LightingTableTests` (de verlichting van "Hele huis" en per apparaat: een apparaat gelijk aan "Hele huis" volgt dat weer, een snelheid, kleurenloop of lijst kleuren die niet past wordt geweigerd), `EngineApiTests` en `EngineRpcTests` voor `GetCustomColorsAsync` en `SetCustomColorsAsync` (een kleur twee keer wordt één, 13 kleuren geweigerd, `OptionsChanged` alleen bij een andere lijst), `CustomColorsTests` (`Normalize`) |
| Logbestand, versie en handleiding | `FileLogTests` (regels, herhalingen tellen, een nieuw bestand na 1 MB, lezen terwijl het open is, `PathOf`), `AppInfoViewModelTests` ("Logbestand openen" en "Handleiding" met een nep-`open`, ook als Windows ze niet kan openen; geen link zonder adres; het versienummer zonder de commit), `SettingsViewTests` (de knop, en onderaan de versie, zonder link zolang er geen adres is en met een adres een link die het opent); `PageViews.ConnectAsync` geeft elke paginatest een nep-`open`, zodat geen test een bestand of browser opent |
| Voorbeeld | `PreviewFramesTests` en `MainViewModelFramesTests` (hooguit 20 beelden per seconde, geen beelden als de app ze niet wil, op Kleur alleen het gekozen apparaat), `DevicePreviewViewModelTests`, `PreviewViewModelTests`, `LedLayoutTests`, `RingLayoutTests`, `WideKeysTests`, `LedPreviewViewTests` (UI Automation), `FocusKeeperTests` en `SliderDragTests` (met een echt venster buiten beeld) |
| Per-LED-editor | `LedEditorViewModelTests`, `MainViewModelLedEditorTests` (ook mengen kleurt de gekozen LED's meteen, en een slepende schuif terwijl de engine bezig is geeft weinig aanroepen met de laatste kleur, met `FakeEngineApi.SetGate`, ook als de engine tussendoor `LightingChanged` meldt; ook alleen met toetsen een reeks kiezen en kleuren, twee snelle Shift+→ voordat de engine de kleuren gaf, alles kiezen en Escape; een ander effect dan Per LED kiezen heft de selectie op, ook bij Kleurenloop, een effect van de engine niet), `DevicePreviewKeyboardTests` (de actieve LED: vorige en volgende ook over zones heen, Home en End, de grenzen, ↑ en ↓ naar de rij die de view vond of de zone ernaast, Shift voor een reeks vanaf de LED waar je vandaan kwam, Ctrl+A, Escape alleen met een selectie op dat apparaat, een klik maakt een LED actief, bij een kortere lengte blijft hij op een LED die de zone toont), `PreviewViewModelTests` (de actieve LED blijft op zijn plek in zijn zone als de apparaten opnieuw komen, ook als een zone ervoor langer werd), `LedPreviewViewKeyboardTests` (welke toets wat doet, de rij erboven of eronder in een rij die omloopt en op een toetsenbord met een brede spatiebalk, Tab-stop en focus naar de zone met de actieve LED, een echte klik op een ander apparaat neemt de focus alleen mee als die al in de LED's stond, de tooltip terug naar de actieve LED als de muis weggaat, de focusring, UI Automation, ook dat het item van de actieve LED de focus meldt; met een echt venster buiten beeld) |
| Pagina's | `NavigationViewModelTests`, `ColorPickerViewModelTests` (ook `AppliesAtOnce` en `IsUnused`, en een hex-code die geen kleur is: niets tijdens het typen, bij het verlaten van het veld de code van de kleur terug met een melding die weggaat met een kleur die klopt, een andere kleur of `Show`; een leeg veld zonder melding), `HexCodeViewTests` (op de echte `ColorPage`: de melding onder de hex-code pas als het veld de focus verliest, als live region; de hex-code is een veld met de rand van invoer en de focusring, waarin een hele code past, ook in het kleinste venster), `MainViewModelDeviceColorTests` (per ingebouwd effect en een eigen effect of de kiezer de kleur nergens kwijt kan en of "Op het apparaat zetten" er is: alleen bij Regenboog geen knop met een regel die het effect noemt, ook bij "Hele huis" en na een scène; mengen stuurt dan niets, + bewaart de kleur wel; een scène met Statisch daarna toont de kleur van het apparaat, een scène die Regenboog laat staan houdt de gemengde kleur), `MainViewModelTests` en de andere `MainViewModel*Tests`, waaronder `MainViewModelFollowLightingTests` (Kleur volgt een scène uit het tray-menu; een kleur die je voor het apparaat mengt blijft staan, behalve als de scène de kleurenloop aanzet; in `MainViewModelTests` ook bij elke nieuwe lijst apparaten zolang hetzelfde apparaat gekozen blijft, en is dat weg, dan de kleur van "Hele huis") en `MainViewModelColorLoopTests` (een loopkleur verandert meteen mee met de schuiven, hex en vaste kleuren, ook slepend terwijl de engine bezig is en tussendoor `LightingChanged` meldt), `ColorLoopViewTests` (op de echte `ColorPage` gaat het vinkje "Kleuren over de LED's verdelen (…)" over meer regels in plaats van afgekapt te worden, ook naast de navigatiebalk in het kleinste venster van 960 px breed; `CheckBoxStyleTests`: het vakje staat naast de eerste regel en bij één regel in het midden), `MainViewModelEffectColorsTests` (de kleuren van Fonkelen, Komeet, Scanner en Vuur: de vakjes bij die effecten, zonder lijst de kleur van het apparaat of het warme vuur, verven, + en −, wisselen tussen effecten houdt elk zijn lijst, twee lijsten die wachten vervangen elkaar niet, gekozen LED's gaan voor, "Standaardkleuren" alleen met een eigen lijst en daarna weer de kleur van het apparaat of het warme vuur), `ColorLoopEditorViewModelTests` (de vakjes blijven dezelfde bij verven en kiezen) en `ColorListEditorViewModelTests` (de gedeelde editor van de kleurenloop en de effecten, binnen zijn minimum en maximum), `PresetTileTests` (de dikke rand van het gekozen vakje, met een echt venster buiten beeld), `ColorListEditorViewTests` (de vakjes, + en − in een echt venster buiten beeld, 12 kleuren in twee rijen van zes, en de kaart "Kleuren van het effect" met "Standaardkleuren" op de echte `ColorPage`; XAML van de app zoekt zijn stijlen al tijdens het laden, dus `AppTheme` zet het thema in de resources van een `Application`, alleen zolang zo'n test loopt, in een collectie die niet parallel loopt), `CustomColorsViewModelTests` (+ met de kleur van de kiezer, niet dubbel, hooguit 12, weghalen, `Show` zonder terugsturen) en `ToolboxViewTests` (op de echte `ColorPage`: de vakjes van 32 px op één rij, focus, namen voor een schermlezer, + en een eigen kleur met de focus naar het nieuwe vakje, de focus terug op dezelfde kleur als de engine andere kleuren stuurt, Delete met de focus naar het volgende vakje of +, "Weghalen" in het contextmenu, bij Regenboog geen knop maar de regel onder de hex-code, en de knop "Op alle apparaten zetten" bij "Hele huis" en "Op het apparaat zetten" bij één apparaat) en `TileCornerTests` (de kleinere hoeken van de vakjes van 32 px en van +, de vakjes van 44 px en de knop "+ nieuw" houden die van het ontwerp) |
| Contrast en velden | `TokenContrastTests` (de contrasten van de kleuren in `Tokens.xaml` met de formule van WCAG 2 (`Wcag`): tekst en subtekst op elk vlak, de primaire knop en de links minstens 4,5:1, en de rand van invoer op `surface`, `bg` en `preview-bg` minstens 3:1; een nieuw paar gaat in `TextPairs` of `NonTextPairs`; de rand van een veld in rust, onder de muis en met de focus steeds lichter), `InputBorderStyleTests` (een tekstvak, een keuzelijst en een vinkje van het thema hebben `InputBorderBrush` in rust en `InputHoverBorderBrush` onder de muis, een tekstvak met de focus de ring van 2 px in tekstkleur) |
| Contrastthema | `ContrastThemeTests` (op de resources van `Theme.xaml`, met `FakeHighContrast`: met een contrastthema aan is elk token precies de brush van `SystemColors` die bij zijn rol hoort (`Assert.Same`: verschillende systeemkleuren kunnen gelijk zijn, zoals `HighlightText` en `Window`), een token voor een eigen rol heeft zonder thema de kleur van het ontwerp die het onderdeel eerst had, en wat alleen in een contrastthema te zien is, is zonder thema doorzichtig, uit blijven de kleuren van het ontwerp, aan en weer uit gaat erheen en terug, een ander contrastthema vervangt de kleuren in plaats van ze op te stapelen, de LED-kleuren, `LedOffBrush` en `StripBgBrush` veranderen niet, elke sleutel van `SystemBrushes` is een token, na `Dispose` volgt hij niet meer, en geen XAML van de app gebruikt een van die tokens als `StaticResource`, in geen van de vormen: `{StaticResource X}`, `{StaticResource ResourceKey=X}` of het element `<StaticResource ResourceKey="X" />`), `ContrastThemeViewTests` (de echte `SettingsPage`: een pagina die al open is, krijgt de systeemkleuren als het thema aangaat en de kleuren van het ontwerp terug als het uitgaat: subtekst, kaart, rand en de schakelaar met zijn bedekking, ring en knop), `ContrastThemeStyleTests` (de stijlen van `Controls.xaml` met een contrastthema aan: een gewone knop, een ronde knop en een kaartknop hebben `ButtonFace` en `ButtonText`, de primaire knop, een aangevinkt vakje en een dag die aan staat `Highlight` met `HighlightText`; onder de muis heeft de primaire knop `HighlightText` met tekst en ring in `Highlight`, een link `Highlight` met een streep eronder, de streepjesrand van "+ nieuw" en de schuifbalk `Highlight` in plaats van `WindowText`, en het kruisje van de titelbalk `Highlight` met `HighlightText` in plaats van rood) |
| Schermlezer | `AccessibleNamesViewTests` (de UI Automation-boom van de echte pagina's Apparaten, Kleur met Statisch, Kleurenloop, Komeet, een eigen effect en Tellen, Scènes met een scène en "aanpassen" open, en Instellingen, in een echt venster buiten beeld: geen naam met `BuurmansRgb.` of `{ `; de kaarten, vaste kleuren, tempo's en tijden bij hun naam, de sierstrips en de verborgen zinnen onder de effectkeuze zonder items, de bolletjes van een scènekaart één afbeelding; een kaart op Apparaten zonder plek heeft geen hulptekst, en de hele naam als tooltip op de inhoud in plaats van op de knop), en de `ToString` van de view models in `LampsViewModelTests`, `ColorPickerViewModelTests`, `ZoneCountViewModelTests`, `SceneCardTests` en `ProfileAutomationViewModelTests` |

## Met de gesimuleerde apparaten bekijken

Start de engine en de instellingen-app zonder OpenRGB en loop de pagina's langs met muis en
toetsenbord. Een paar dingen om te proberen:

- Kies "Regenboog": het voorbeeld en de strip bij "Hele huis" lopen mee.
- Kies "Fonkelen", "Komeet", "Scanner" en "Vuur" bij "Hele huis" en kijk naar de ledstrip, de
  ventilator en de "Gesimuleerde headset" (twee oorschelpen van één LED): de komeet loopt de
  ring rond, de scanner gaat heen en terug en remt bij de uiteinden af, en bij de headset
  gaan de komeet en de scanner om de beurt naar de ene en de andere oorschelp. Bij Fonkelen en
  Vuur heeft elk apparaat zijn eigen toeval: geen twee apparaten fonkelen of flakkeren gelijk, ook
  niet het begin van de strip en de ventilator. Zet bij Instellingen "Zuinig" en bij Kleur de
  snelheid op 300 %: de punten springen dan verder, maar zijn nog te volgen.
- Kies bij Kleur "Gesimuleerde ARGB-controller" en klik bij "ARGB 2" een paar keer op + of
  sleep de schuif. Sluit de engine af (tray, "Afsluiten") en start hem opnieuw: "ARGB 2" heeft
  nog steeds die lengte.
- Klik bij "Gesimuleerde ARGB-controller" naast "ARGB 1" op "Knipperen": in het voorbeeld
  knippert alleen die zone. "ARGB 2" heeft pas een knop als het LED's heeft.
- Klik bij "Gesimuleerde ARGB-controller" naast "ARGB 1" op "Tellen": de zone springt naar 60
  LED's en in het voorbeeld gaat elke seconde één LED meer aan, de nieuwste groen. Kies Pauze,
  ga met − terug naar LED 20 en kies "Dit is de laatste": ARGB 1 houdt 20 LED's. Tel nog eens en
  kies "Stoppen", kies een ander apparaat of ga naar Scènes: ARGB 1 krijgt zijn lengte terug. Sluit
  je de app tijdens het tellen, dan zet de engine de lengte ook terug. "ARGB 2" (0 LED's) telt ook.
- Kies bij Kleur "Gesimuleerd toetsenbord" en "Regenboog", en sleep "Richting" naar 45°: in het
  voorbeeld loopt de regenboog schuin over de toetsen, van linksboven naar rechtsonder, en het
  pijltje wijst die kant op. Kies de ventilator en zet 90°: de kleuren lopen van boven naar
  beneden over de ring. Bij de ledstrip draait 180° hem om en geeft 90° de hele strip één kleur.
  "Volgorde van de LED's" zet het beeld terug zoals het was. Bij Statisch, Komeet, Scanner,
  Fonkelen, Vuur en Per LED is de schuif weg, en bij de Kleurenloop als "Kleuren over de LED's
  verdelen" uit staat. Bewaar het als scène, kies een andere en weer deze: de richting komt mee.
- Kies "Regenboog" en zet bij "Gesimuleerde headset" (twee oorschelpen van één LED) "Zones
  gelijk" aan: in het voorbeeld hebben beide LED's nu dezelfde kleur, en bij de ventilator met
  twee ringen lopen de ringen gelijk op. Bij Komeet en Scanner pulseren de oorschelpen dan
  tegelijk, en Vuur flakkert als een kaars. Herstart de engine: de schakelaar staat nog aan.
  Kies "Per LED": elke LED houdt zijn eigen kleur.
- Kies "Gesimuleerde ventilator met twee ringen" en klik op "Outer ring"; kies "Gesimuleerd
  toetsenbord", wijs een toets aan voor zijn naam en klik op het brede deel van de spatiebalk.
- Doe de per-LED-editor alleen met het toetsenbord: ga bij "Gesimuleerd toetsenbord" met Tab
  naar de toetsen (de actieve krijgt een ring en een tooltip), loop met de pijltjes, ↑ en ↓,
  Home en End, kies met Spatie, breid uit met Shift+→, kies alles met Ctrl+A en hef op met
  Escape. Kies een reeks, ga met Shift+Tab terug naar de schuif Rood en druk een paar keer →:
  de reeks kleurt bij elke stap mee. Bij de ventilator met twee ringen gaat → van de binnenste
  ring door naar de buitenste, en de focus gaat mee. Klik bij "Hele huis" met de focus in de
  toetsen op een LED van een ander apparaat: de ring gaat mee. Zet daarna Verteller aan
  (Ctrl+Win+Enter): bij elke stap hoort hij de naam van de LED te zeggen ("Escape (LED 1)") en
  of hij geselecteerd is.
- Kies bij Kleur "Hele huis" en **+ Eigen effect maken** onder het effect: alle apparaten knipperen
  rustig tussen hun kleur en zwart, en onder het kleurvoorbeeld staat het paneel "Eigen effect".
  Klik op de tweede stap en kies "Echt blauw", voeg met + een stap toe en zet "Blijven staan" op
  0,3 s en "Overlopen" op 0: in het voorbeeld onderaan loopt het meteen mee. Kies daarna
  "Gesimuleerd toetsenbord", kies Per LED en kleur een paar toetsen, kies je effect weer, klik een
  stap en "LED-kleuren van dit apparaat overnemen": het vakje wordt een verloop, en de ventilator
  en de strip (bij "Hele huis") tonen het patroon uitgerekt. Hernoem het (Enter), bewaar het als
  scène, en verwijder het met Verwijderen: de app vraagt het eerst (kies eerst Annuleren: het
  effect blijft, met de focus op Verwijderen), en daarna staan de apparaten op Statisch en toont
  de scène bij het laden ook Statisch. Doe het ook alleen met het toetsenbord: Tab door de
  stappen, Delete op een stap, de pijltjes op de schuiven, en in de vraag Escape of Tab en Enter.
  Kijk in `%AppData%\BuurmansRgb\effects.json`.
- Zet twee scènes in elkaar en kies er een in het tray-menu; geef een scène een tijd een minuut
  verder en met "+ Nog een tijd" een tweede een minuut daarna (zet er tussendoor een kleur), of
  `notepad.exe`, en start en sluit Kladblok. Een tijd weghalen gaat met "−"; de kaart zegt dan
  bijv. "Elke dag om 21:00 en nog 1", of weer alleen de ene tijd. Verwijder een scène met de
  moersleutel en "Verwijderen": de vraag is donker zoals de app, met "Annuleren" en
  "Verwijderen", ook op een Engelse Windows; met Verteller aan hoor je de vraag.
- Zet op Apparaten een apparaat uit, kies het bij Kleur en kies "Op het apparaat zetten": het gaat aan
  en zijn schakelaar gaat mee; de andere apparaten blijven zoals ze waren.
- Kies bij Kleur het effect "Kleurenloop", klik op de tweede kleur (dikke witte rand) en sleep
  de schuif Blauw: het vakje en het voorbeeld lopen mee en de knop "Op alle apparaten zetten" is weg.
  Klik op de derde kleur: de tweede houdt zijn nieuwe kleur, ook na het opnieuw starten van de
  engine. Hetzelfde met een paar gekozen LED's in het voorbeeld. Voeg met + kleuren toe tot
  twaalf: de vakjes staan in twee rijen van zes, + staat rechts naast de eerste rij en wordt
  grijs. Maak het venster smaller: de rijen blijven zes breed, en waar + en − er niet meer naast
  passen, staan ze eronder.
- Kies bij "Hele huis" het effect "Komeet": onder het kleurvoorbeeld staat één vakje in de kleur
  van het apparaat. Voeg met + twee kleuren toe en geef ze een andere kleur: de ventilator krijgt elke
  ronde de volgende kleur. Kies "Vuur" (de warme kleuren staan klaar), maak er donkerblauw, blauw,
  lichtblauw en wit van, en kies weer "Komeet": die heeft zijn eigen kleuren nog. Bewaar het als
  scène: de kaart toont de kleuren. Kies ook "Scanner" en "Fonkelen" met een paar kleuren.
- Kies bij Kleur het effect "Regenboog", bij "Hele huis" en bij één apparaat: onder de hex-code
  staat dat Regenboog geen kleur van het apparaat gebruikt, en de knop is weg. Meng
  een kleur (er staat geen "Nog niet op het apparaat") en zet hem met + bij de eigen kleuren. Kies
  Statisch: de knop is terug en de regel weg.
- Kies bij Kleur "Echt blauw" in de gereedschapskist en klik + onder "Eigen kleuren": het vakje
  komt erbij en + wordt grijs. Meng een andere kleur en zet die er ook bij. Sluit de app en open
  hem weer: de eigen kleuren staan er nog (`customColors` in `settings.json`). Ga met Tab naar
  een eigen kleur en druk Delete: hij is weg en de focus staat op het volgende vakje of op +.
  Rechts klikken en "Weghalen" doet hetzelfde. Met een tweede instellingen-app open zie je het
  daar meteen.
- Klik bij Apparaten op "Verversen": de knop zegt even "Zoeken…" en de lijst blijft staan. Zonder
  OpenRGB verschijnt onder de kop de melding dat OpenRGB niet bereikbaar is; met Tab naar de knop
  en Spatie gaat hetzelfde, en de focus blijft op de knop.
- Vink in het tray-menu "Effecten pauzeren" aan: de melding met "Hervatten" verschijnt.
- Start zonder OpenRGB: onder "Apparaten in huis" staat dat dit nep-apparaten zijn, met "Naar
  Instellingen" ernaast. Ga met Tab van "+ Erbij knutselen" naar die link en druk Spatie: je bent
  bij Instellingen. Zet daar "Altijd": terug bij Apparaten is de regel weg; met "Automatisch" is
  hij er weer. Wijs "+ Erbij knutselen" aan: de tooltip zegt dat OpenRGB de apparaten toevoegt.
- Zet bij Instellingen "Nooit": de gesimuleerde apparaten verdwijnen meteen.
- Druk Ctrl+Alt+Shift+U en Ctrl+Alt+Shift+A terwijl een ander venster de focus heeft: de
  gesimuleerde apparaten gaan uit en aan. Zet bij Instellingen "Sneltoetsen voor alles aan en uit"
  uit: de toetsen doen niets meer en staan niet meer naast de items in het tray-menu.
- Ga naar Instellingen en scrol naar onderen: daar staat het versienummer, zonder "Handleiding"
  (die link verschijnt pas als `AppInfoViewModel.GuideAddress` een adres heeft). "Logbestand
  openen" opent `engine.log` in Kladblok, ook als je de engine eerst afsluit. "Starten met
  Windows" volgt het tray-menu: vink het daar aan of uit en kijk naar de schakelaar. Let op: die
  schakelaar (en het tray-item) schrijft echt in
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, ook van een engine met `APPDATA` naar een
  testmap; zet hem daarna terug zoals hij stond.
- Open `%AppData%\BuurmansRgb\settings.json` in Kladblok of VS Code terwijl de engine en de app
  draaien, zet `framesPerSecond` op 10 en `color` op `"#FF0000"` en sla op: binnen een seconde
  staan "Vloeiendheid" bij Instellingen en de kleur bij Kleur mee. Sla daarna een komma te veel
  op: er verandert niets en `engine.log` noemt het; haal de komma weg en sla weer op.

## Hoe de engine werkt

- **Render-loop:** alleen als minstens één apparaat een bewegend effect toont, draait een
  `PeriodicTimer` op `framesPerSecond`; ook tijdens het knipperen. Een frame stuurt alleen de
  apparaten waarvan de kleuren veranderden, en alloceert niets zolang de instellingen-app dicht
  is. Gaat een frame mis, dan probeert het volgende het opnieuw.
- **Effecten:** `EffectCatalog` maakt per apparaat een effect met de kleur, de per-LED-kleuren,
  de kleurenloop, `EffectColors` (de eigen kleuren van Fonkelen, Komeet, Scanner en Vuur; zonder
  lijst de kleur van het apparaat of `DefaultFire`) en een seed (`SeedOf`, FNV-1a over het id), zodat
  Fonkelen en Vuur per apparaat anders lopen en na een herstart hetzelfde.
  Welke effecten de kleur van het apparaat tonen, staat in `EffectIds.UsesDeviceColor` in
  `Contracts`, net als `IsAnimated`: niet Regenboog, de Kleurenloop, Vuur en een eigen effect.
  `EffectCatalogTests` rendert elk effect in twee kleuren van het apparaat en faalt als een nieuw
  effect er niet goed in staat. De app leidt er alleen uit af wat de kiezer met de kleur doet
  (`MainViewModel.SetPickerMode`): heeft het effect geen lijst, loop of stappen die de
  kleur krijgen en gebruikt het de kleur van het apparaat niet, dan is `ColorPicker.IsUnused`
  waar, zonder "Op het apparaat zetten" en met de regel `DeviceColorUnused` met de naam van het
  effect. Zo krijgt een later effect zonder kleur die regel vanzelf.
- **Richting:** zonder `Direction` (null) rendert een effect over de buffer van het apparaat zoals
  altijd, met `i / n` per LED. Met een richting rekent `DeviceOutput` bij de eerste `Show` met een
  andere hoek (en voor elke nieuwe apparatenlijst, die nieuwe outputs maakt) per LED zijn plek uit
  (`LedPlaces.Along`: per zone de plekken van `LedPlaces.Of`, geprojecteerd op `x·cos θ + y·sin θ`,
  per zone geschaald naar zijn deel van `0` tot `1` in de volgorde van de zones) en geeft die aan
  een `IDirectedEffect` (Regenboog, Golf, Kleurenloop) in plaats van de index; een frame
  alloceert zo niets. Met "Zones gelijk" krijgt de langste zone de plekken van zichzelf alleen.
  De instellingen-app rekent `LedPlaces.Of` niet zelf uit, maar tekent met dezelfde bouwstenen:
  een fan-ring met `LedPlaces.IsRing` en `LedPlaces.OnRing`, toetsen met `LedPlaces.Place` en
  `WideKeys.Widen`. Een brede toets beslaat in het voorbeeld al zijn vakjes; alleen de engine
  neemt het midden ervan als plek. Een zone zonder vorm tekent het voorbeeld in regels
  (`LedPlaces.Wrap`), terwijl de engine er één lijn van links naar rechts van maakt.
- **Eigen effecten:** `CustomEffectLibrary` houdt de eigen effecten (`CustomEffect` in
  `Contracts`: een `id` als `custom:1a2b3c4d`, een naam en 1–32 `CustomEffectStep`s) in het
  geheugen en schrijft `effects.json` (`JsonCustomEffectStore`, naast `profiles.json`) een seconde
  na de laatste wijziging, zoals `LightingSettingsSaver`, en bij `Dispose` (na de host); een
  bestand dat niet te schrijven is, probeert dezelfde timer een seconde later opnieuw (en een
  volgende wijziging of het afsluiten ook).
  De verlichting en de apps krijgen een wijziging meteen. `EngineHost` leest het bij het starten,
  vóór `Apply` van de verlichting. Elke nieuwe lijst gaat naar
  `LightingController.SetCustomEffects`, die per effect één `StepEffect` maakt (in `Effects`, voor
  alle apparaten samen: het houdt niets bij tussen frames; een effect met dezelfde stappen, zoals
  na een nieuwe naam of een wijziging van een ander effect, houdt het zijne), de apparaten die
  het tonen meteen opnieuw rendert en een verlichting met een effect dat weg is op Statisch zet
  (als wijziging, zodat het bewaard wordt). `Apply` en `Replace` doen dat ook voor een scène of
  `settings.json` met zo'n effect; `SetLighting` weigert een `custom:`-id die er niet is. Dat
  alles pas na de eerste `SetCustomEffects`: is `effects.json` bij het starten niet te lezen
  (`IOException`), dan krijgt de controller niets, houdt de verlichting de `custom:`-id en toont
  het apparaat zijn kleur tot de library het bestand wel leest: een tweede timer van dezelfde
  `TimeProvider` probeert het elke `LoadRetryDelay` (5 s) tot het lukt en stopt bij `Dispose`, en
  een vraag (`Effects`, `Save`, `Delete`) leest het ook. De eerste lezing die lukt geeft
  `Changed`, dus `CustomEffectsChanged` naar de apps. Een app die bij het verbinden een
  geweigerde `GetCustomEffectsAsync` krijgt, verbindt zonder eigen effecten
  (`MainViewModel.GetCustomEffectsOrNoneAsync`) en haalt ze bij die `CustomEffectsChanged` op.
  `LightingSettings` laat een `custom:`-id staan (`EffectCatalog.IsValidId`), zodat een effect
  dat even niet te lezen is de verlichting niet meteen kwijtraakt. `StepEffect` zoekt per frame
  zijn stap in een array van begintijden (hooguit 32) en rekt een patroon uit met dezelfde
  formule als "Zones gelijk"; de snelheid komt, zoals bij elk effect, van `EffectClock`.
  `IEngineApi`: `GetCustomEffectsAsync`, `SaveCustomEffectAsync` (een lege `Id` is een nieuw
  effect), `DeleteCustomEffectAsync` en `CustomEffectsChanged`; `GetEffectsAsync` geeft alleen de
  effecten van de engine. In de app maakt `MainViewModel` de effectkeuze uit beide, en
  `CustomEffectEditorViewModel` is het paneel; elke wijziging gaat als het hele effect, in de
  wachtrij met de `id` als sleutel, zodat een slepende schuif weinig aanroepen geeft. Weigert de
  engine een wijziging, een nieuw effect of verwijderen (een naam die een andere app net nam, een
  effect dat weg is), dan haalt de app de lijst opnieuw op en toont die, ook als hij een lijst
  na `CustomEffectsChanged` om een wachtende wijziging liet liggen.
  De zin onder de effectkeuze is `EffectOption.Description`: voor een ingebouwd effect
  `EffectDescription_<id>` uit de `Strings.resx` van de app, voor een eigen effect
  `CustomEffectDescription` met het aantal stappen, dat `ShowEffects` op de `EffectOption` bijwerkt.
  Een nieuw ingebouwd effect heeft dus `Effect_<id>` en `EffectDescription_<id>` nodig, anders
  faalt `EffectOptionTests`.
  Een eigen effect mag niet heten als een ingebouwd effect, want dan staat die naam twee keer in
  de effectkeuze. De namen zijn UI-teksten van de app (`Effect_<id>` in zijn `Strings.resx`); de
  engine toont geen effectnamen, maar heeft dezelfde in zijn eigen `Strings.resx`, zodat hij
  zonder de app kan weigeren (`EffectNames.IsBuiltIn`, net als een dubbele naam in
  `CustomEffectLibrary.Save`) en ook `effects.json` kan nakijken. `EffectNamesTests` leest de
  `Strings*.resx` van de app (gekoppeld in het testproject) en houdt beide gelijk, ook voor elke
  vertaling: een vertaling van de app vraagt er een van de engine. Staat in `effects.json` een
  effect dat al zo heet (van vóór deze controle), dan gooit de library het niet weg; hij noemt
  het "Regenboog (eigen)" (of met een nummer als die naam ook bezet is), en het bestand krijgt de
  nieuwe naam bij de volgende wijziging. De app kijkt vóór hij iets stuurt
  (`MainViewModel.IsCustomEffectNameTaken`, met de namen van `GetEffectsAsync`) en zet de melding
  (`NameError`) onder het vak, een live region. Het vak schrijft de naam alleen bij Enter en als
  het de focus verliest (`UpdateSourceTrigger=Explicit`, `ColorPage.xaml.cs`): dan bewaart
  `CustomEffectEditorViewModel.EndNameEdit` een goede naam, of zet de echte naam terug met een
  melding die de geweigerde en de echte naam noemt (`CustomEffectNameTakenKept`,
  `CustomEffectNameEmptyKept`), zodat een schermlezer één melding hoort. Die melding gaat weg bij
  een goede naam, een ander effect of een nieuwe naam van de engine; dezelfde melding voor een
  volgende naam leest een schermlezer opnieuw voor. Escape zet met
  `CustomEffectEditorViewModel.CancelNameEdit` de echte naam terug, zonder melding.
  `effects.json` met de hand aanpassen terwijl de engine draait, pikt hij niet op, net als
  `profiles.json` (zie `docs/ideas.md`).
- **Beelden voor het voorbeeld:** een render zegt `PreviewFrames` (één per verbonden app) alleen
  dat er iets nieuws is; een timer op de thread pool kopieert dan het nieuwste beeld en stuurt
  het, hooguit 20 keer per seconde, zodat de animatie nooit op de app wacht. De app zegt met
  `IEngineApi.SetFramesWantedAsync` dat ze geen beelden wil zolang ze geminimaliseerd is of een
  pagina zonder voorbeeld toont (Scènes, Instellingen); dan kopieert de engine niets. Met
  `SetFrameDevicesAsync` vraagt Kleur met één apparaat alleen de beelden van dat apparaat;
  Apparaten en "Hele huis" krijgen ze allemaal. Een beeld met maar een deel van de apparaten laat
  de strip op Apparaten ongemoeid.
- **Logging:** elke `Trace`-melding (`Trace.TraceError`, `TraceWarning`, `TraceInformation`) gaat
  naar `FileLog` in `BuurmansRgb.Contracts`, die de engine (`engine.log`) en de instellingen-app
  (`ui.log`) bij het starten aanzetten; ook een onafgevangen exception komt erin. Zo blijft het
  één regel code om iets te loggen, zonder extra pakketten in de engine. DebugView ziet de
  meldingen ook nog.
- **IPC:** named pipe `BuurmansRgb.Engine.<sessie-id>` (alleen voor de eigen Windows-gebruiker)
  met StreamJsonRpc; de interface is `IEngineApi` in `BuurmansRgb.Contracts`. De engine meldt
  wijzigingen met events (`LightingChanged`, `DevicesChanged`, `PauseChanged`,
  `OpenRgbStatusChanged`, `SettingsSaved`, `FrameRendered`, `ProfilesChanged`, `OptionsChanged`;
  de app leest bij die laatste twee de scènes of de opties opnieuw). App en engine moeten van
  dezelfde versie zijn: de berichten hebben geen versienummer en veranderen soms van vorm (zo
  kreeg `ProfileAutomation` een lijst `Times` in plaats van één tijd met zijn dagen). Daarom
  stopt de installer bij een update allebei (`PrepareToInstall`). Na het opstarten en na het
  sluiten van de laatste instellingen-app geeft de engine zijn ongebruikte geheugen terug aan
  Windows (`MemoryTrimmer`), en ook nadat het tray-menu dicht ging.
- **Verversen:** `IEngineApi.RescanDevicesAsync` laat elke `IRescannableDeviceProvider` tegelijk
  opnieuw zoeken (`LightingController.RescanDevicesAsync`) en neemt daarna hun lijsten over, zodat
  `DevicesChanged` vóór het antwoord over de pipe gaat. OpenRGB.NET kent geen rescan, dus
  `OpenRgbServerWatcher` schrijft `REQUEST_RESCAN_DEVICES` (140, zonder data en zonder antwoord)
  zelf op zijn verbinding; `OpenRgbDeviceProvider` wacht tot `RescanTimeout` (5 s) op de eerste
  `DeviceListUpdated` en zijn herlaadactie. Komt die niet, of is er geen verbinding, dan maakt hij
  een nieuwe verbinding die de oude pas vervangt als de apparaten geladen zijn. Een tweede vraag
  terwijl er een loopt, krijgt het antwoord van die eerste. De gesimuleerde provider meldt zijn
  lijst opnieuw; `SwitchableDeviceProvider` geeft het door zolang hij aan staat.
- **Pauzeren:** via `SystemEvents.SessionSwitch` en `SystemEvents.PowerModeChanged` op de thread
  van het tray-icoon (`AutoPause`, `SystemSessionEvents`, `SystemPowerEvents`). Bij het starten
  vraagt de engine één keer of Windows al vergrendeld is (`WTSQuerySessionInformation`). Het
  scherm volgt hij met `RegisterPowerSettingNotification` voor `GUID_CONSOLE_DISPLAY_STATE` op
  een verborgen message-only venster op dezelfde thread (`SystemDisplayEvents` achter
  `IDisplayEvents`; `UnregisterPowerSettingNotification` bij afsluiten). Windows stuurt de
  toestand meteen na het registreren, via de berichtenwachtrij; alleen 0 (uit) telt, 2 (gedimd)
  niet. Dat geeft de eigen reden `PauseReasons.DisplayOff`, onder dezelfde `pauseOnLockAndSleep`.
  Ontgrendelen of verbinden beëindigt die reden ook, voor het geval Windows het aangaan van het
  scherm niet meldt. `Away` doet bij alleen een scherm dat uit is niet mee: dan kan er nog
  iemand achter de pc zitten.
- **Schermbeveiliging:** `SystemScreenSaverEvents` (achter `IScreenSaverEvents`) luistert naar
  dezelfde voorgrond-hook als de scènes (`ForegroundHook`, hieronder), zonder pollen. Komt een
  venster van een programma met de extensie `.scr` vooraan, dan vraagt hij
  `SystemParametersInfo(SPI_GETSCREENSAVERRUNNING)`; alleen als Windows zegt dat er een
  schermbeveiliging loopt, telt het. Die vlag zet Windows alleen voor een schermbeveiliging die
  het zelf start, dus "Voorbeeld" in de instellingen van de schermbeveiliging telt niet (het
  kleine voorbeeld is een kindvenster en komt nooit vooraan). Staat er een `.scr` vooraan terwijl
  de vlag uit is, dan schrijft hij dat met de exe-naam in het logboek en vraagt hij het één keer
  opnieuw, een seconde later met een eenmalige timer (`TimeProvider`, geen pollen): staat dat
  venster dan nog vooraan en zegt Windows nu ja, dan telt het alsnog, voor het geval Windows de
  vlag pas na het venster zet. Elk ander venster dat daarna vooraan komt (het programma van
  daarvoor) beëindigt hem, zonder de vlag te vragen: die kan nog even aan staan terwijl de
  schermbeveiliging sluit. Behalve het vergrendelscherm (`LockApp.exe`, zoals bij
  `SystemForegroundWindow.Counts`): bij een wachtwoord na hervatten komt dat vooraan vóórdat
  Windows het vergrendelen meldt, en dan zouden gedimde of zwarte apparaten even fel worden;
  ontgrendelen beëindigt de reden toch. Hij meldt elk venster van een lopende
  schermbeveiliging, ook als hij al liep, zodat een volgende schermbeveiliging na een pauze die
  `AutoPause` zelf beëindigde weer pauzeert. Dat geeft de eigen reden `PauseReasons.ScreenSaver`,
  onder `pauseOnLockAndSleep` en met `pausedLights`, zoals `DisplayOff`; ontgrendelen of
  verbinden beëindigt ook die reden, voor het geval er na de schermbeveiliging geen ander venster
  vooraan komt. `Away` doet niet mee: de schermbeveiliging zegt alleen dat niemand de pc een tijd
  aanraakte, en verdwijnt bij de eerste muisbeweging; vergrendelt Windows erbij, dan maakt
  `Locked` de gebruiker weg. Bij het starten kijkt hij één keer naar het venster dat vooraan
  staat, alleen als `ForegroundHook` dan de hook heeft. Weigert Windows die, dan krijgt hij pas
  na een latere geslaagde `Listen` (van een scène op de voorgrond) de volgende wisselingen, niet
  het venster dat op dat moment vooraan staat; een schermbeveiliging die dan al loopt, pauzeert
  niet, pas de volgende. Voor de scènes op de voorgrond telt een schermbeveiliging niet als
  wegwisselen, net als het vergrendelscherm.
- **Apparaten tijdens de pauze:** `LightingController.PausedLights` (`PausedLightsMode` in
  `Contracts`, `pausedLights` in `settings.json` via `PausedLightsModeJsonConverter`). Zolang de
  pauze een andere reden heeft dan `Manual`, schaalt `DeviceOutput.Send` wat naar de apparaten
  gaat: 1 bij `Keep`, `DimmedBrightness` (0,2) bij `Dim`, 0 bij `Off`. De buffers houden de
  echte kleuren, dus het voorbeeld en het eerste `RenderLocked` na de pauze geven die terug.
  Verandert de schaal door een reden die begint of eindigt, of door een andere stand, dan stuurt
  `SendAllLocked` de buffers opnieuw; dimmen kost dus geen frames. `Manual` stuurt nooit iets, ook
  niet het zwart. `AutoPause` zet de slaapreden synchroon in `Suspending`, dus het zwart gaat de
  deur uit voordat de handler van `PowerModeChanged` terugkeert.
- **Programma's zien:** zonder adminrechten meldt Windows niet dat een programma start. Daarom
  kijkt de engine elke 2 seconden in de proceslijst (`EnumProcesses`, de naam één keer per
  nieuw proces), alleen zolang een scène een programma heeft (zonder vinkje voor de voorgrond,
  zie hieronder). Dat kost minder dan 0,1 ms per ronde, waar `Process.GetProcessesByName` of
  een Toolhelp-snapshot 5 tot 7 ms kosten en WMI `System.Management` in de engine haalt.
  Programma's die als administrator draaien, ziet hij ook. Zolang Windows vergrendeld is of
  slaapt staat de timer stil, ook als `pauseOnLockAndSleep` uit staat: `ProgramWatcher` volgt
  het verplichte `IAwayState` (`Away`, `AwayChanged`) van `AutoPause`, dat vergrendelen en
  slaapstand bijhoudt los van `AutoPause.Enabled`. Daarna kijkt hij meteen, zodat een
  programma dat intussen startte of stopte dan telt. Komt intussen een scène op tijd en
  toont een programma zijn scène, dan kijkt `HoldUntilProgramsStop` eerst één keer, zodat een
  spel dat op het vergrendelscherm stopte de tijd niet tot na het ontgrendelen tegenhoudt.
- **Programma op de voorgrond:** met `ProfileAutomation.ForegroundOnly` (`foregroundOnly` in
  `profiles.json`) telt een programma alleen zolang een venster ervan op de voorgrond staat.
  `SystemForegroundWindow` (achter `IForegroundWindow`) luistert daarvoor naar `ForegroundHook`,
  de ene `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT |
  WINEVENT_SKIPOWNPROCESS)` van de engine op de thread van het tray-icoon, die al een message
  loop heeft: Windows post elk nieuw voorgrondvenster daarheen, één keer hoeveel er ook luisteren,
  zonder DLL in andere programma's en zonder pollen. De hook is er zolang iemand luistert; omdat
  de schermbeveiliging (hierboven) altijd luistert, is dat zolang de engine draait. Weigert
  Windows de hook bij het starten, dan blijft de schermbeveiliging luisteren en krijgt hij de
  vensters zodra een latere `Listen` (van een scène met de keuze) de hook wel krijgt; de scènes
  zelf stoppen dan met luisteren en proberen het bij de volgende `Follow(true)` opnieuw. De naam
  komt van `GetWindowThreadProcessId` en `ProcessImageName` (`QueryFullProcessImageName`,
  dezelfde als in `ProcessList`; in `BuurmansRgb.Contracts`, omdat ook de instellingen-app er het
  pad van een exe mee leest voor "Kies uit wat nu draait"), en `ProgramNames.Comparer` vergelijkt hem.
  `ProgramWatcher` vraagt met `Follow` alleen om de vensters zolang minstens één scène de keuze aan heeft; dat
  komt van elke thread, dus `Follow` post naar een verborgen message-only venster, dat op de
  tray-thread gaat luisteren of ermee stopt (ook bij afsluiten). Zonder scène met de keuze leest
  hij dus geen namen. Na het luisteren meldt hij één keer wat er nu vooraan staat. Hij meldt elk
  venster dat telt, ook van het programma dat al vooraan stond: dat kan intussen gesloten en
  opnieuw gestart zijn, met alleen de taakbalk of het startmenu ertussen. `ProgramWatcher` houdt
  zelf bij wat nieuw is. Vensters die niet tellen (`SystemForegroundWindow.Counts`): de taakbalk
  (`Shell_TrayWnd`, `Shell_SecondaryTrayWnd`), het overloopvak van het systeemvak
  (`TopLevelWindowForOverflowXamlIsland` op Windows 11, `NotifyIconOverflowWindow` op Windows
  10), Alt+Tab en Win+Tab (`XamlExplorerHostIslandWindow`, `MultitaskingViewFrame`,
  `TaskSwitcherWnd`, `ForegroundStaging`), `LockApp.exe`, het startmenu en zoeken, meldingen en
  snelle instellingen (`StartMenuExperienceHost.exe`, `SearchHost.exe`, `SearchApp.exe`,
  `ShellExperienceHost.exe`, `ShellHost.exe`; samen `ProgramNames.ShellParts`, die "Kies uit wat
  nu draait" ook weglaat), een schermbeveiliging (elke `.scr`), de engine
  zelf en een venster waarvan de naam niet te lezen is: dan blijft de stand zoals hij was. In
  `ProgramWatcher` is op de voorgrond komen een start en wegwisselen een stop, op dezelfde stapel
  als de programma's die tellen zolang ze draaien. Bij het wisselen tussen twee voorgrond-scènes
  laadt hij eerst de nieuwe en haalt hij daarna de oude eronder weg (die geeft zijn verlichting
  van daarvoor door), dus er gaat maar één wijziging naar de apparaten.
  Zolang er een voorgrond-scène aan staat, kijkt hij die naam ook in de proceslijst na, zodat
  een programma dat sluit zonder dat er een ander venster vooraan komt toch stopt; pas als hij
  het daar één keer zag, zodat een naam die nog niet te lezen is niet als gestopt telt. Tijdens
  `Away` onthoudt hij alleen wat Windows vooraan meldt; na terugkomen telt dat op de timer (niet
  onder de lock van `AutoPause`) één keer, behalve als de proceslijst het intussen zag stoppen.
- **Welke scène aan staat:** `ProfileLibrary.ActiveName`, gezet door `Load`, `Save` en `Show`
  (waarmee `ProgramWatcher` na een spel ook de scène van daarvoor terugzet) en gewist door elke
  andere `LightingChanged`; de app vraagt het met `IEngineApi.GetActiveProfileAsync`. Bij elke
  wissel (`ActiveChanged`) bewaart `EngineHost` de naam als `activeScene` in `settings.json`, en
  na het starten zet `RestoreActive` hem terug als die scène nog bestaat.
- **Scènes op tijd:** een scène heeft een lijst tijden (`ProfileAutomation.Times`, elk een
  `LoadTime` met eigen dagen, hooguit `MaxTimes` = 8; `Normalize` zet ze van vroeg naar laat en
  haalt dubbele weg). `ProfileTimer` zet één timer op de eerstvolgende van alle tijden van alle
  scènes; op hetzelfde moment wint de eerste op alfabet. Gaat die meer dan
  `LateLimit` (een minuut) te laat af of springt de klok erover (`IClockEvents`, ook bij het
  ontwaken), dan slaat hij die tijd over. Met `CatchUp` (`catchUpSceneTimes`, standaard uit)
  laadt hij de scène waarvan een tijd volgens `DailyTime.Previous` het laatst voorbij is (van
  alle tijden van alle scènes): bij het starten via
  `CatchUpAtStart`, dat `EngineHost` aanroept als alle triggers draaien (na de opgeslagen
  verlichting en `RestoreActive`, behalve als die scène al aan staat), en na het ontwaken op
  `IResumeEvents.Restored` van `ResumeRestorer` (na het eerste herstel, ~2 s). Dat event geeft
  het moment van de eerste `Suspending` sinds het vorige herstel mee; hij haalt alleen in als
  de laatste tijd daarna viel, dus een verzette klok of een klokevent na het herstel laat geen
  "gemiste" tijd achter. Een programma met een eigen scène houdt hem vast via
  `HoldUntilProgramsStop`, ook bij het starten.
- **Sneltoetsen:** `WindowsGlobalHotkeys` registreert Ctrl+Alt+Shift+A en Ctrl+Alt+Shift+U
  (`AllOnOffHotkeys`, met `MOD_NOREPEAT`) met `RegisterHotKey` op een verborgen message-only
  venster op de thread van het tray-icoon; `WM_HOTKEY` komt binnen via de message loop die er al
  is, dus er draait geen extra thread of timer. `TrayPresenter.ApplyHotkeys` registreert ze
  zodra de engine gestart is en roept bij een druk op een toets dezelfde `SetAllOn` aan als de
  menu-items. Zet de app ze uit (`IEngineApi.SetHotkeysAsync`, op een IPC-thread), dan meldt
  `AllOnOffHotkeys.EnabledChanged` dat; het tray-icoon post het naar zijn eigen thread, waar
  `UnregisterHotKey` de toetsen vrijgeeft, en `EngineHost` bewaart het als `hotkeys`. Een
  toets die al bezet is, geeft `false` (gelogd met de Windows-foutcode) en een melding bij het
  icoon; de andere toets werkt gewoon.
- **Starten met Windows:** `RegistryAutoStart` (achter `IAutoStart`) zet `BuurmansRgb` in de
  Run-sleutel. `AutoStartSwitch` deelt dat tussen het tray-menu (`TrayPresenter.SetAutoStart`)
  en de instellingen-app (`IEngineApi.GetAutoStartAsync` en `SetAutoStartAsync`, op een
  IPC-thread; een lock houdt lezen en schrijven bij elkaar). Een andere waarde meldt
  `AutoStartSwitch.Changed`, dat `EngineApi` als `OptionsChanged` doorgeeft, zodat de
  schakelaar in de app het tray-menu volgt; het menu leest het register elke keer dat het
  opengaat (`RefreshAutoStart`) en toont zo wat de app zette. Het staat niet in `settings.json`:
  het register is de waarheid, ook als iemand de waarde met de hand weghaalt. Lukt schrijven
  niet (`UnauthorizedAccessException`, `SecurityException`, `IOException`), dan logt
  `TrySetEnabled` de reden en geeft `SetAutoStartAsync` `false` (geen RPC-fout, zodat de app
  een eigen melding geeft in plaats van "De engine kon de wijziging niet verwerken."); de app zet
  de schakelaar terug op wat de engine heeft en toont dezelfde zin als het tray-icoon.
- **Logbestand, versie en handleiding in de app:** `AppInfoViewModel`, onderaan Instellingen.
  "Logbestand openen" opent `engine.log` zelf (`FileLog.PathOf(FileLog.EngineName)`, met
  `ShellLauncher.Open`) in plaats van het via de engine te vragen: zo werkt het ook als de
  engine niet draait of niet antwoordt, juist als je het log nodig hebt, en komt Kladblok
  vooraan, omdat de app de focus heeft (een programma dat de engine op de achtergrond start, mag
  dat van Windows niet altijd). Het versienummer is de `AssemblyInformationalVersion` van de app
  zonder de commit na de `+` (`installer\build.ps1` zet hem met `-p:Version`, anders is het
  1.0.0). De link "Handleiding" staat er al, maar is verborgen (`HasGuide`) zolang
  `AppInfoViewModel.GuideAddress` `null` is: de repo op GitHub is privé, dus een link daarheen
  geeft een gewone gebruiker een inlogscherm of een 404, en de installer levert de docs (nog)
  niet mee. Eén adres in `GuideAddress` (een webadres of het pad van een meegeleverde
  handleiding) zet de link aan; hij opent het met `ShellLauncher.Open`. `App` maakt
  `AppInfoViewModel.ForThisApp(ShellLauncher.Open)` en geeft hem aan `MainViewModel`; tests
  geven een nep-`open`, en zonder `appInfo` opent `MainViewModel` niets, zodat een test nooit
  echt Kladblok of de browser opent. `ShellLauncher` staat in `BuurmansRgb.Contracts`, want het
  item in het tray-menu gebruikt hem ook.
- **Herstarten:** het tray-item roept `TrayPresenter.RestartAsync` aan, dat `EngineRestarter`
  gebruikt. Eerst kijkt `ElevatedServicesRestart` als gewone gebruiker of de service `OpenRGB`
  (de naam uit `installer/BuurmansRgb.iss`) of PawnIO er is; zo niet, dan geen UAC. Anders start
  het dezelfde engine-exe met `--restart-services` via `ShellExecute` met `Verb = "runas"`, en
  wacht hooguit `ServiceRestarter.MaxDuration` plus 30 s. Die stap (`Program.RestartServices`,
  zonder instance, log of tray) laat `ServiceRestarter` OpenRGB stoppen, PawnIO stoppen, PawnIO
  starten en OpenRGB starten, met `WindowsServiceManager` (advapi32 via `LibraryImport`, want
  `System.ServiceProcess.ServiceController` is een apart NuGet-pakket) en time-outs van 30 s
  voor OpenRGB en 15 s voor PawnIO (een service die nog start, laat hij binnen die tijd eerst
  opstarten voordat hij hem stopt, en een die nog stopt eerst stoppen voordat hij hem start).
  PawnIO zoekt hij als service `PawnIO`, anders als eerste
  kerneldriver met "PawnIO" in de naam (de echte naam is nog niet op een pc met PawnIO
  nagekeken; de bronnen voor `PawnIO` staan in `openrgb-meeleveren.md`). De uitkomst
  (`ServicesRestartReport`) komt terug als exitcode: `0x42` in de hoogste byte, dan OpenRGB en PawnIO elk in 4 bits, en in de laagste 16 bits de oorzaak van de
  eerste fout (`ServicesRestartCause`: welke stap, de `ServiceControlFailure` en de Win32-code
  tot 2047). De engine logt die oorzaak ("The restart of the services went wrong first at
  stopping OpenRGB: TimedOut."), zodat "zie het logbestand" in de melding klopt; al het andere
  (een crash) leest als mislukt. Geen bestand, zodat het verhoogde proces niets schrijft waar de
  gebruiker kan schrijven. `ERROR_CANCELLED` (1223) bij het starten is een geweigerde UAC. Daarna vraagt
  `SettingsAppWindow` een open instellingen-app via haar mutex en exit-signaal
  (`SingleInstance.SignalExitIfRunning`, dat de mutex alleen opent en nooit aanmaakt; de app
  hoort het via `ListenForExit`, zoals `--exit` bij de engine) om te sluiten, en start
  `EngineLauncher` de engine met `--restarted <exitcode>` en zo nodig `--open-settings`. Lukt
  dat, dan sluit de oude af zoals bij "Afsluiten"; de nieuwe wacht in
  `PreviousEngine.AcquireAfterRestart` (`SingleInstance.AcquireWhenFree`, elke 200 ms, hooguit
  60 s) tot de mutex van de oude weg is. Is hij er dan nog, dan wacht de nieuwe nog eens 60 s;
  blijft de oude, dan stopt de nieuwe. Geen exit-signaal (`--exit`) ertussen: de oude heeft zijn
  message loop al verlaten en luistert dus niet meer (de `ListenForExit` in `Program.Main` is dan
  al gedisposed); het AutoReset-signaal zou blijven staan en een later gestarte engine meteen
  sluiten. De waarschuwingen geeft `AcquireAfterRestart` terug in plaats van ze te schrijven:
  `engine.log` is nog van de oude, en twee `FileLog`s die aan hetzelfde bestand toevoegen
  schrijven elk op hun eigen positie en overschrijven zo elkaars regels (en kunnen tegelijk
  roteren). Wordt de nieuwe primary, dan traceert hij ze na `FileLog.Start`; stopt hij, dan
  schrijft `PreviousEngine.WriteStopLog` ze naar `engine.restart.log` in dezelfde map, dat
  alleen zo'n engine schrijft. Anders start de nieuwe, meldt via
  `TrayPresenter.DescribeRestart` wat er herstart is en opent de app weer.
- **settings.json met de hand:** `SettingsFileWatcher` (achter `ISettingsFileEvents`) zet een
  `FileSystemWatcher` op het bestand; Windows meldt een wijziging, niets pollt. `SettingsReloader`
  wacht 400 ms na de laatste melding (een editor schrijft soms in stappen, of naar een ander
  bestand dat hij daarna hernoemt) en vraagt dan `ISettingsStore.LoadIfChanged`, ook één keer na
  het starten voor een wijziging tijdens het opstarten. De timer loopt alleen als er een melding
  wacht. Een exceptie bij het toepassen komt in het logbestand; de engine draait door.
  `JsonSettingsStore` onthoudt de bytes die hij het laatst las of schreef; staat precies dat
  in het bestand, dan was het zijn eigen opslag (die geeft meerdere meldingen, soms laat) en
  gebeurt er niets. Een bestand dat niet te lezen is (geen geldige JSON, half geschreven, open
  bij een ander programma), verandert niets en geeft een waarschuwing in het logbestand; de
  volgende melding leest opnieuw, en de volgende opslag zet een kapot bestand eerst opzij als
  `settings.json.broken`. Een bestand dat open is (`SettingsFileChange.ReadLater`) leest de
  reloader na de vertraging zelf nog eens, tot vijf keer. Een waarde die niet klopt, slaat
  `LenientJson` over zoals bij het starten. `EngineHost.ApplyChangedByHand` zet de opties via
  dezelfde properties als de IPC (dus `OptionsChanged` en de saver), de zonelengtes met
  `ZoneLengthKeeper.Change` en de verlichting met `LightingController.Replace`
  (`LightingChanged`), die alleen als hij anders is dan nu (`EngineSettings.SameLighting`), zodat
  de scène bij alleen een optie aan blijft.
  `activeScene` neemt hij niet over. Daarna roept hij `LightingSettingsSaver.Adopt` aan met de
  verlichting zoals het toepassen hem achterliet (die `Replace` teruggeeft, niet opnieuw gelezen):
  die, met de opties van de saver, geldt als de inhoud van het bestand (met `activeScene` uit het
  bestand), zodat de opslag die de wijzigingen plannen niets schrijft en het bestand de opmaak en
  onbekende sleutels van de gebruiker houdt. Alleen wat de engine anders heeft (een scène die
  uitging of niet overgenomen werd, of een wijziging uit de app op een andere thread tijdens het
  toepassen, ook een waarvan `LightingChanged` pas daarna komt) schrijft hij een seconde later
  terug. De reloader leest en past toe binnen
  `LightingSettingsSaver.HoldSaves`: een opslag die dan afloopt, wacht, zodat hij niet de stand
  van vóór het lezen over de wijziging heen schrijft. Een opslag met dezelfde inhoud als het
  bestand schrijft nooit. De saver
  slaat op met `ISettingsStore.SaveIfUnchanged`, een compare-and-swap: staan er andere bytes in
  het bestand dan de store kent (een wijziging met de hand die de reloader nog niet las), dan
  schrijft hij niets en probeert de saver het na zijn vertraging opnieuw; meestal heeft de
  reloader het dan toegepast en is er niets meer op te slaan. Na vijf keer schrijft hij toch (er
  is dan geen watcher die het leest), en bij afsluiten gaat de wijziging met de hand voor.
- **Scènes in het geheugen:** staat er een `profiles.json`, dan leest de engine het bij het
  starten één keer, voor de tijden en programma's. Een wijziging met de hand komt pas na een
  herstart binnen.
- **Gesimuleerde apparaten:** `SimulatedDevicesSwitch` volgt de apparatenlijst en beslist bij elke
  wijziging opnieuw, onder één lock.
- **Zonelengtes:** `ZoneLengthKeeper` vraagt een bewaarde lengte alleen als een apparaat
  verschijnt (of `settings.json` met de hand veranderde) en wacht tot het die meldt, zodat een
  weigerend apparaat geen lus van aanpassingen geeft. De laatst gevraagde lengte wint: ook een
  lengte die de zone al heeft, wacht hij af zolang een eerder gevraagde nog niet kwam, zodat die
  eerdere niet blijft hangen (`ResizeZone` en `ResizeForAWhile` delen `ResizeLocked`). Een zone
  waarvan de naam weg is of dubbel voorkomt, blijft met rust; een lengte buiten het bereik blijft
  bewaard maar wordt niet gevraagd.
- **Knipperen:** `IEngineApi.FlashDeviceAsync`, in de engine `LightingController.Flash` en
  `DeviceFlash`. Eén zone gaat met `FlashZoneAsync` en `LightingController.FlashZone`: de flits
  dekt dan alleen het LED-bereik van die zone (`RgbZone.RangesOf`, zoals de engine de
  zone nu kent; het voorbeeld in de app deelt de zones met dezelfde functie in), de rest van
  het apparaat rendert zijn effect gewoon. Er is steeds één `DeviceFlash`, van een apparaat of
  een zone; een zone die de engine niet (meer) kent, slaat hij over, zoals een onbekend
  apparaat. Een apparaat of zone zonder LED's knippert niet, maar stopt wel de flits die liep,
  zoals een apparaat zonder LED's al deed vóór er zones knipperden.
- **LED's tellen:** `IEngineApi.StartZoneCountAsync`, `SetZoneCountAsync` (een
  `ZoneCountChange`: LED, pauze of tempo), `StopZoneCountAsync` en het event `ZoneCountChanged`
  met een `ZoneCountInfo`. Het tekenen zit in de controller, naar het voorbeeld van
  `DeviceFlash`: `LightingController.StartCount`, `ChangeCount` en `StopCount` met één
  `ZoneCount`, die op de engine-tijd telt (LED = LED bij de laatste wijziging + verstreken tijd ×
  tempo) en in `DeviceOutput.Render` na de helderheid en vóór een flits over het bereik van de
  zone tekent (`RgbZone.RangesOf`, met de lengte die de zone nu heeft). De zone heet bij index én
  naam, zodat een nieuwe lijst met een andere zone op die plek de telling niet krijgt; een nieuwe
  lijst met de zone hangt hem aan de nieuwe `DeviceOutput`. Zolang hij loopt, draait de
  render-loop; bij de laatste LED of een pauze stopt die weer (bij een statisch effect).
  `CountChanged` gaat af bij een stap, zonder allocaties; `EngineApi` leest dan pas `Count`, met
  een `Sequence` die onder de lock van de controller bij elke wijziging ophoogt. De render-thread
  en een IPC-thread kunnen hun stand in een andere volgorde versturen dan de wijzigingen, dus
  `PreviewViewModel.ShowZoneCount` laat een oudere stand vallen (`ForgetZoneCount` begint weer
  bij 0 voor een nieuwe engine). De lengtes doet `ZoneCounter`, boven `ZoneLengthKeeper`:
  `ResizeForAWhile` geeft de zone zijn grootste lengte zonder die te onthouden (en zonder hem als
  lengte van elders te zien), en onthoudt eerst de huidige lengte als er nog geen was, zodat een
  apparaat dat terugkomt na een crash zijn oude lengte terugkrijgt. Een huidige lengte 0
  onthoudt hij niet (zoals 0 van elders): die komt dan niet in `settings.json`, en de teller zet
  hem toch terug, uit de lengte die hij bij de start zag; alleen na een crash blijft zo'n zone op
  zijn grootste lengte. Het terugzetten gaat met `GiveLengthBack`, dat niets onthoudt. De teller
  zet die onthouden lengte terug bij `Stop`, als de eigenaar (de `EngineApi` van de app die
  begon) wordt weggegooid, bij een andere zone, bij elke `PauseChanged` met een reden, vóór
  `RescanDevicesAsync` en bij `Dispose` van de engine. Bij het afsluiten wacht `Program` na de
  teller altijd hooguit 2 seconden tot OpenRGB zijn werk kreeg
  (`Program.WaitForResizesOnExit`, met `OpenRgbDeviceProvider.WaitUntilIdle`), voordat de
  provider sluit: ook als de telling al op een ander pad stopte, zoals de app die tegelijk sluit
  (zijn `EngineApi` wordt dan eerst weggegooid). Zonder werk in de wachtrij keert hij meteen
  terug; een OpenRGB dat langer nodig heeft, houdt de zone op zijn grootste lengte.
  `ResizeZoneAsync` van de getelde zone ("Dit is de laatste") gaat via `ZoneCounter.ResizeZone`:
  dat stopt de telling zonder terugzetten en geeft de lengte aan de keeper. Een lijst waarin het
  apparaat weg is, of de zone ná het bereiken van de grootste lengte een andere lengte heeft,
  stopt de telling zonder terugzetten. In de app houdt `PreviewViewModel` één
  `ZoneCountViewModel` per telling vast terwijl het voorbeeld opnieuw gemaakt wordt; de zone toont
  hem in plaats van de lengte. Verdwijnt de getelde zone uit beeld (een ander apparaat), of gaat
  de app van Kleur weg, dan vraagt de app zelf om te stoppen, maar alleen voor een telling die
  hij zelf startte (`PreviewViewModel` onthoudt de zone bij `StartZoneCount`): de telling van een
  tweede app loopt door. Die zone vergeet hij weer bij "Dit is de laatste", bij een pauze (die
  stopt elke telling, en weigert een start die daarna aankomt), als de start-aanroep mislukt
  (`ForgetZoneCountStart`, zolang de engine hem niet bevestigde), en als de engine na
  de eerste stand met de eigen telling een stand stuurt die de zone niet meer telt. Alleen een
  stand met een `Sequence` boven die van de nieuwste stand bij de start telt daarvoor mee; een
  stap of `None` van vóór de start bevestigt of wist hem dus niet. Na een stop in de app negeert hij
  standen van die zone tot de engine de telling beëindigt, zodat een stap die nog onderweg was de
  teller niet even terugbrengt. Bij het verbinden haalt de app de stand op met `GetZoneCountAsync`
  (naast de pauze en de apparaten), zodat hij ook een gepauzeerde telling van een andere app
  toont, die geen events meer geeft.
- **Zones gelijk:** `LightingController.ZonesAlike` (een set apparaat-id's, in `settings.json`
  als `zonesAlike`) en `SetZonesAlike`, via `IEngineApi.SetZonesAlikeAsync`. Het hoort niet bij
  de verlichting: `LightingChanged` gaat niet af, een scène laat het staan, en de app hoort het
  als `DevicesChanged` met `DeviceInfo.ZonesAlike`. `DeviceOutput` deelt de LED's bij het maken
  één keer in met `RgbZone.PartsOf`: de zones en de LED's na de laatste zone, zonder de delen
  zonder LED's, de langste vooraan. Staat het aan, dan rendert het effect alleen in de langste en
  kopieert `Stretch` dat beeld naar de andere (elke LED neemt de LED op dezelfde plek, gemeten in
  het midden van de LED), zonder allocaties per frame. Bij `per-led` geeft de controller het niet
  door, zodat elke LED zijn eigen kleur houdt. De flits komt daarna, dus knipperen per zone
  werkt zoals eerst. Het voorbeeld telt de delen met dezelfde `RgbZone.PartsOf` en toont de
  schakelaar alleen bij twee of meer delen met LED's (`DevicePreviewViewModel.ShowsZonesAlike`).

## Ontwerp, lettertypes en icoon

- **Ontwerp:** `design/` bevat het ontwerppakket (`DESIGN.md`, het icoon en drie schermen als
  referentie). Kleuren, maten en lettertypes staan in `src/BuurmansRgb.UI/Themes/Tokens.xaml`,
  de componenten en de donkere stijl van de gewone controls in `Themes/Controls.xaml`, wat de
  pagina's delen in `Themes/Theme.xaml`, en de pagina's in `Views/Pages`. De gloed van een
  LED-bolletje is een radiale gradiënt (`LedBulb`) in plaats van een `DropShadowEffect`, zodat
  niet elk bolletje elk frame apart gerenderd wordt.
- **Contrast:** `TokenContrastTests` rekent de kleuren van `Tokens.xaml` na met de formule van
  WCAG 2: tekst minstens 4,5:1, wat geen tekst is maar een veld of grafiek herkenbaar maakt
  (zoals de rand van invoer, `InputBorderColor`) minstens 3:1. Komt er een kleur of een
  combinatie bij, zet het paar (voorgrond en ondergrond, als `Color`- of brush-sleutel) dan in
  `TextPairs` of `NonTextPairs`; een kleurwijziging die te weinig contrast geeft, wordt zo rood.
- **Contrastthema:** `ContrastTheme` (in `Views`) volgt de contrastthema's van Windows. Staat er
  een aan, dan zet hij een eigen `ResourceDictionary` achter de samengevoegde woordenboeken van de
  app, met per token uit `ContrastTheme.SystemBrushes` een kleur van `SystemColors` (tekst
  `WindowText`, vlakken `Window`, randen `WindowText`, hover en selectie `Highlight` met
  `HighlightText`, links `HotTrack`, het spoor van een schuif `GrayText`; knoppen en kaarten om op
  te klikken `ButtonFace` en `ButtonText`, in WPF `SystemColors.Control` en `ControlText`, en de
  primaire knop, een aangevinkt vakje en een dag die aan staat `Highlight` met `HighlightText`,
  zoals de richtlijnen van Microsoft voor contrastthema's zeggen; elke hover `Highlight`, een
  link onder de muis ook met een streep eronder (`LinkHoverUnderlineBrush`, zonder thema
  doorzichtig), en de primaire knop draait dan zijn kleuren om met een ring in `Highlight`); gaat
  het thema uit, dan haalt hij dat woordenboek weg en gelden de kleuren van het ontwerp weer.
  `App` start hem vóór het venster met `SystemHighContrast`, dat `SystemParameters.HighContrast`
  leest en `SystemParameters.StaticPropertyChanged` volgt; tests geven een `FakeHighContrast` via
  `IHighContrast`, zodat geen test de instelling van Windows leest of verandert. Wil je het zelf
  zien zonder je eigen instelling om te zetten, zet dan een contrastthema aan in Windows Sandbox of
  een virtuele machine. Een token dat hij vervangt, staat in de XAML altijd als
  `DynamicResource`: een `StaticResource` houdt de kleur van bij het laden, en
  `ContrastThemeTests` wordt rood als er een in `src/BuurmansRgb.UI` staat. Waar een token twee
  rollen had (bijv. `border` als rand en als achtergrond van een gekozen item), kreeg de tweede
  rol een eigen token (`ListSelectedBrush`, `CaptionHoverBrush`, `NavSelectedBrush`, ...) met
  dezelfde kleur, zodat het ontwerp gelijk blijft. Wat inhoud is, blijft `StaticResource` en
  verandert niet: de LED-kleuren, `LedOffBrush` van de bolletjes, `StripBgBrush`, de gloed en
  de kleuren van het voorbeeld (die tekent `LedPreviewView` zelf, elk beeld opnieuw), het
  woordmerk en de schuiven R, G en B.
- **Schermlezer:** UI Automation noemt een item van een `ItemsControl` naar de `ToString` van
  zijn view model, ook als de knop erin een eigen `AutomationProperties.Name` heeft. Elk view
  model in een lijst heeft daarom een `ToString` met de naam die de UI ervoor toont
  (`ColorSwatch`, `LampItem`, `ColorPreset`, ...), ook een record. Gelijke records worden voor UI
  Automation één item. Een rij bolletjes die alleen sier is, is een `DecorativeItemsControl`:
  zonder `AutomationProperties.Name` slaat UI Automation hem over, met een naam is hij één
  afbeelding (de bolletjes van een scènekaart). `AccessibleNamesViewTests` bewaakt dat per pagina.
  Een tooltip als tekst op een knop zonder `AutomationProperties.HelpText` leest WPF voor als
  hulptekst, zodat een tooltip met de naam die naam twee keer laat zeggen; de kaart op Apparaten
  heeft zijn tooltip daarom op de inhoud van de knop.
- **Lettertypes:** Bricolage Grotesque (Regular, SemiBold, ExtraBold) en IBM Plex Mono Medium
  zitten als resource in `BuurmansRgb.UI.dll` (`src/BuurmansRgb.UI/Fonts`), per gewicht een
  eigen bestand omdat WPF van een variabel lettertype alleen het standaardgewicht toont. Ze
  vallen onder de SIL Open Font License; de licenties gaan als `Fonts\OFL-*.txt` mee en staan
  in `installer/THIRD-PARTY-NOTICES.txt`. Ontbreekt een letter, dan valt WPF terug op Segoe UI
  of Cascadia Mono/Consolas.
- **Icoon:** `design/icon/buurmansrgb.ico` is het icoon van beide exe's, het venster, het
  tray-icoon en de installer (`AppIconPath` in `Directory.Build.props`). In de titelbalk staat
  de kleine donkere versie als vector (`AppIconSmallDark`).

## Metingen

Gemeten op de ontwikkel-pc; "één kern" is het percentage van één CPU-kern.

| Wat | Situatie | CPU | Geheugen |
| --- | --- | --- | --- |
| Engine | Statisch, app dicht | ~0,07 % van één kern | 21–24 MB working set, 12–13 MB private |
| Engine | Regenboog op 30 fps, app dicht | ~0,35 % van één kern (~0,02 % van de pc) | 22–27 MB working set, 12,5–13 MB private |
| Engine | Allocaties (`dotnet-counters`), app dicht | Regenboog ~101 KB, statisch ~89 KB per minuut: geen meetbaar verschil door 1.800 frames per minuut | |
| Engine | Alles uit, met regenboog als effect | 0,00–0,12 % van één kern, geen render-timer | 19–28 MB working set, 12–13 MB private |
| Engine | Vergrendeld, slaapstand, scherm uit of schermbeveiliging | 0 frames, geen timer | |
| Engine | Een scène met een programma | ~0,05 % van één kern extra (~100 miljoen CPU-cycli per minuut) | ~0,3 MB voor de procesnamen |
| Engine | Tien scènes met een tijd en eigen kleuren | | ~51 KB op de heap |
| Engine | Regenboog, "Gemiste tijden inhalen" aan met twee scènes met een tijd (#51) | 0,49–0,75 % van één kern, main zonder deze uitbreiding 0,65–0,73 %: geen verschil buiten de ruis | 27–30 MB working set, 11–12 MB private (main 25–29 en 11–12 MB) |
| Engine | Regenboog, sneltoetsen aan (#52) | 0,36–0,39 % van één kern, zonder sneltoetsen (#51) 0,26–0,39 %: geen verschil buiten de ruis; een toets kost alleen iets als je hem indrukt | 26–28 MB working set, 11–11,7 MB private (zonder: 24–27 en 11–12 MB) |
| Engine | Regenboog, `pauseOnLockAndSleep` uit, een scène met een programma, niet vergrendeld (#54) | 0,31–0,60 % van één kern, zonder deze uitbreiding (#53) 0,49–0,52 %: geen verschil buiten de ruis. Vergrendeld scheelt het de ~0,05 % van het kijken in de proceslijst | 27–31 MB working set, 11–12,6 MB private (zonder: 26–31 en 11–13 MB) |
| Engine | Regenboog, met het volgen van het scherm (#55); Windows vergrendeld, het scherm niet apart gemeten; het verschil is het extra venster | Met `pauseOnLockAndSleep` uit 0,16–0,57 % van één kern, zonder deze uitbreiding (#54) 0,21–0,36 %: geen verschil buiten de ruis. Met het pauzeren aan (gepauzeerd) 0,10 tegen 0,08 % | 23–28 MB working set, 11–12 MB private (zonder: 23–28 en 11–11,6 MB) |
| Engine | Regenboog, `pausedLights` "keep", "off" en "dim" (#56); Windows vergrendeld, dus gepauzeerd; zonder pauze stuurt de engine niets anders dan eerst | "keep" 0,08–0,13 %, "off" 0,05–0,10 %, "dim" 0,05–0,10 % van één kern, zonder deze uitbreiding (#55) 0,03–0,16 %: geen verschil buiten de ruis. Dimmen of uit kost alleen één keer sturen bij het begin en eind van de pauze | 23–28 MB working set, 11–12,4 MB private (zonder: 23–28 en 11–12 MB) |
| Engine | Regenboog, `pauseOnLockAndSleep` uit, "Gemiste tijden inhalen" aan, "Nacht" om 23:00 en "Ochtend" met drie tijden (`loadAt` en twee `loadTimes`) (#57); zonder deze uitbreiding (#56) leest de engine alleen `loadAt` | 0,18–0,78 % van één kern, zonder deze uitbreiding 0,34–0,55 %: geen verschil buiten de ruis; de timer rekent alleen bij het plannen een paar tijden meer uit | 27–28,4 MB working set, 11–12,4 MB private (zonder: 27–28,2 en 11–12,3 MB) |
| Engine | Regenboog, `pauseOnLockAndSleep` uit, één scène met een programma (#58): zonder vinkje (pollen) en met `foregroundOnly` (de hook, geen pollen; zonder deze uitbreiding pollen); Windows vergrendeld, dus geen pollen en geen wisselingen van de voorgrond | Zonder vinkje 0,29–0,60 % van één kern, zonder deze uitbreiding (#57) 0,26–0,42 %; met `foregroundOnly` 0,29–0,36 % tegen 0,31–0,44 %: geen verschil buiten de ruis. Een wisseling van de voorgrond kost alleen iets als je wisselt | Zonder vinkje 25–27 MB working set, 11–13 MB private (zonder deze uitbreiding 25–28 en 11–12,7 MB); met `foregroundOnly` 25,5–26,7 en 10,9–12,3 MB (zonder: 25,2–26,4 en 11,1–12,8 MB) |
| Engine | Regenboog, `pauseOnLockAndSleep` uit, Windows vergrendeld (#59): zonder app, en met een client die elke 2 s het moederbord laat knipperen (`FlashDeviceAsync`) of zijn eerste zone (`FlashZoneAsync`), zonder beelden | Zonder app 0,23–0,47 % van één kern, zonder deze uitbreiding (#58) 0,13–0,34 %; het apparaat knipperen 0,86 % tegen 0,73–1,04 %; een zone knipperen 0,94–1,01 %: geen verschil buiten de ruis. Knipperen kost alleen iets zolang het duurt (frames op 30 fps) | Zonder app 25–26,5 MB working set, 10,9–12 MB private (zonder: 25–26,3 en 11–12,7 MB); met knipperen 37–48 MB working set, 12,7–18,3 MB private, voor apparaat en zone gelijk aan #58 (37–44,5 en 12,7–18,4 MB) |
| Engine | Regenboog, `pauseOnLockAndSleep` uit, Windows vergrendeld (#61): met de `FileSystemWatcher` op `settings.json` en niemand die het bestand wijzigt, en met elke 5 s een wijziging met de hand (`framesPerSecond` 29 en 30 om en om) | Zonder wijzigingen 0,39–0,49 % van één kern, zonder deze uitbreiding (#60) 0,36–0,44 %: geen verschil buiten de ruis, er pollt niets. Met een wijziging elke 5 s 0,88–1,07 %: lezen, toepassen en een seconde later de eigen opslag | Zonder wijzigingen 25–28 MB working set, 11,1–11,8 MB private (zonder deze uitbreiding 25–27,6 en 11–11,7 MB); met een wijziging elke 5 s 27–33 en 11,3–14 MB |
| Engine | Regenboog, `pauseOnLockAndSleep` uit, sneltoetsen aan, Windows vergrendeld, na de refactor (#63): één lezer voor de keuzewoorden in `settings.json`, één basisklasse voor de verborgen vensters, de sneltoetsen alleen in `WindowsGlobalHotkeys` bijgehouden; drie keer 60 s om en om met `kleur-kiezen` | 0,13–0,34 % van één kern, zonder de refactor (`kleur-kiezen`) 0,16–0,39 %: geen verschil buiten de ruis | 25,5–29,2 MB working set, 11,1–15 MB private (zonder: 25,7–28,7 en 11,3–12,8 MB); de eerste ronde van elk hoger, daarna gelijk (25,5–27 en 11,1–11,9 MB) |
| Engine | Regenboog, Windows vergrendeld, met de schermbeveiliging (#64): de voorgrond-hook staat nu altijd, ook zonder scène met `foregroundOnly`; vergrendeld komen er geen wisselingen van de voorgrond. Drie keer 60 s om en om met `pauseOnLockAndSleep` uit, en één keer met het pauzeren aan (gepauzeerd); zonder deze uitbreiding is main (#63) | Pauzeren uit 0,10–0,31 % van één kern, zonder deze uitbreiding 0,16–0,31 %: geen verschil buiten de ruis. Gepauzeerd 0,05 tegen 0,05 %. Wat een wisseling kost, staat in de rij hieronder | Pauzeren uit 25,7–28,9 MB working set, 10,9–11,8 MB private (zonder: 25,6–29 en 11,1–12,5 MB); gepauzeerd 24–25,8 en 11,2–12,1 MB (zonder: 24,1–25,9 en 11,3–12,8 MB) |
| Engine | Het werk per wisseling van de voorgrond met de schermbeveiliging (#64), los gemeten: de handlers van een Release-build twee keer 7 × 10.000 keer aangeroepen met het venster dat vooraan stond en met het bureaublad (`explorer.exe`), zonder echte wisseling en zonder hook in Windows, met een meetprogramma buiten de repo; main (#63) op dezelfde manier. Niet gemeten: wat Windows kost om de gebeurtenis naar de tray-thread te posten | Zonder scène met `foregroundOnly` 20–29 µs per wisseling (`SystemScreenSaverEvents.Tell`; bijna alles is de exe-naam via `ProcessImageName`, 20–28 µs, en `SPI_GETSCREENSAVERRUNNING` 0,6–1,3 µs alleen bij een `.scr`), main 0 µs (geen hook). Met zo'n scène 48–62 µs, main 22–23 µs: elke luisteraar leest de exe-naam zelf. Bij één wisseling per seconde ~0,002–0,003 % van één kern extra, met zo'n scène ~0,003–0,004 % | 80 bytes per wisseling, ~4,8 KB per minuut bij één per seconde; met zo'n scène 200–224 bytes, main 120–144 bytes |
| Engine | Regenboog op 30 fps, `pauseOnLockAndSleep` uit, Windows vergrendeld, met "Zones gelijk" (#65): aan bij de vijf gesimuleerde lampen met twee of meer zones met LED's, de lampen met de schakelaar (`simulated-argb` heeft standaard alleen in ARGB 1 LED's), en uit; drie keer 60 s om en om, zonder deze uitbreiding is de schermbeveiliging (#64) | Aan 0,36–0,52 % van één kern, uit 0,36–0,47 %, zonder deze uitbreiding 0,34–0,65 %: geen verschil buiten de ruis. Een frame alloceert ook met "Zones gelijk" niets (`LightingControllerZonesAlikeTests`) | Aan 26,3–28,6 MB working set, 11–12,6 MB private; uit 26,4–27,9 en 11,1–12,4 MB; zonder: 26,5–29,5 en 11,1–12,4 MB |
| Engine | Fonkelen, Komeet, Scanner en Vuur (#66) op 30 fps tegen Regenboog op de basis ("Zones gelijk", #65), `pauseOnLockAndSleep` uit, Windows vergrendeld. Na de review (eigen toeval per apparaat, minder fonkelingen, vuur zonder strepen) per effect zes keer 120 s om en om, Vuur vier keer; daarvoor drie keer 60 s. Daarnaast de tijd van één `Render` over 104 LED's (het gesimuleerde toetsenbord) in een Release-meetprogramma buiten de repo, 200.000 frames na het opwarmen, voor en na de review om en om, en voor Vuur ook voor en na de tweede review | Na de review: Regenboog op de basis 0,20–0,39 % van één kern (gemiddeld 0,31), met #66 0,21–0,44 % (0,35); Fonkelen 0,22–0,35 % (0,31), Komeet 0,17–0,43 % (0,23), Scanner 0,20–0,36 % (0,28), Vuur 0,23–0,46 % (0,34; gemeten vóór de tweede review, die 0,5 µs per frame meer kost, ~0,002 % van één kern). De spreiding binnen één effect is groter dan het verschil tussen de effecten: geen effect kost aantoonbaar meer dan Regenboog, maar een klein verschil valt met deze meting niet uit te sluiten. De eerste meting van 60 s gaf Komeet 0,34–0,55 % en Fonkelen 0,29–0,36 % tegen Regenboog 0,13–0,31 %; met de langere runs kwam dat niet terug (de CPU-tijd van Windows telt in stappen van 15,6 ms, ~0,013 % per 120 s). Twee runs zijn weggelaten: in één met Komeet liep de engine op naar 0,87 % en 64 MB working set, en in één met Scanner stopte hij na 55 s vanzelf ("Engine stopped" in het log, zonder fout). De oorzaak staat niet in het log, maar waarschijnlijk was het een `--exit` van een andere `BuurmansRgb.Engine.exe` in dezelfde Windows-sessie: de mutex en de events `Local\BuurmansRgb.Engine.*` hangen aan de sessie, dus een `--exit` of een start elders bereikt ook een engine met een eigen `APPDATA` en `USERPROFILE` (zie de meettips hieronder). Per frame van 104 LED's: Regenboog 1,3–1,7 µs, Golf 1,6–2,0, Fonkelen 1,9–2,2 (was 2,9–3,0: er branden minder LED's), Komeet 1,0–1,4, Scanner 0,8–1,3 en Vuur 7,1–7,2 µs (was 6,0 vóór de review en 6,6 vóór de tweede: de ruis wordt tussen twee plaatsen en tussen twee stappen in de tijd weer opgerekt); bij 30 fps is ook Vuur ~0,02 % van één kern. Een frame alloceert met elk effect niets (`LightingControllerAnimationTests`) | Na de review: Regenboog op de basis 25,6–27,9 MB working set, 10,9–12,4 MB private, met #66 25,8–29,2 en 10,9–12,7 MB; Fonkelen 25,7–27,9 en 10,9–12,1, Komeet 25,8–28,4 en 10,9–12,4, Scanner 25,8–27,9 en 11–12,7, Vuur 25,8–27,9 en 11–12,3 MB |
| Engine | Kleuren per effect (#69) op 30 fps tegen de basis (#68), `pauseOnLockAndSleep` uit, Windows vergrendeld: Komeet en Vuur zonder eigen kleuren op de basis, met vier eigen kleuren, en Komeet in de lampkleur met deze uitbreiding; drie keer 120 s om en om. Daarnaast de tijd van één `Render` over 104 LED's in een Release-meetprogramma buiten de repo, 200.000 frames na het opwarmen | Komeet: basis 0,16–0,36 % van één kern (gemiddeld 0,24), vier kleuren 0,17–0,31 % (0,26), lampkleur 0,13–0,31 % (0,25); Vuur: basis 0,30–0,39 % (0,35), vier kleuren 0,22–0,31 % (0,27): geen verschil buiten de ruis. Per frame van 104 LED's: Komeet 0,97–1,00 µs op de basis, 0,94–0,98 µs met één of vier kleuren (de eerste versie zocht de kleur per LED op en kostte 2,1 µs; nu zoekt hij de drie kleuren die een frame kan hebben één keer op), Scanner 0,80–0,87 tegen 0,80–0,81, Fonkelen 1,88 tegen 1,80 (één kleur) en 1,93 (vier: een kans per fonkeling meer), Vuur 7,18–7,40 tegen 7,19–7,27 µs. Een frame alloceert ook met vier kleuren niets (`LightingControllerAnimationTests`) | Komeet: basis 24–26 MB working set, 10,9–11,7 MB private, vier kleuren 23,9–25,7 en 11,1–11,8 MB; Vuur: basis 23,9–25,7 en 11–12,1 MB, vier kleuren 23,9–25,8 en 11–11,7 MB |
| Engine | 12 kleuren (#74) op 30 fps in Release, `pauseOnLockAndSleep` uit, sessie losgekoppeld: Kleurenloop met 3, 8 en 12 kleuren en Komeet met 4 en 12, twee keer 60 s om en om (na 15 s opwarmen). Het werk per frame hangt niet van het aantal kleuren af (de loop zoekt per LED twee buren op, de komeet per frame drie kleuren), en een frame met 12 kleuren alloceert niets (`LightingControllerAnimationTests`) | Kleurenloop 3 kleuren 0,42–0,49 % van één kern, 8 kleuren 0,26–0,39 %, 12 kleuren 0,18–0,42 %; Komeet 4 kleuren 0,34–0,39 %, 12 kleuren 0,47–0,49 %: binnen de ruis van eerdere metingen | 26,2–29,2 MB working set en 10,9–12,6 MB private bij elke meting; geen verschil tussen 3, 8 en 12 kleuren |
| Engine | Eigen kleuren in de gereedschapskist (#76) op 30 fps in Release, Regenboog, `pauseOnLockAndSleep` uit, sessie losgekoppeld: met 12 `customColors` tegen de basis zonder (#75); vijf keer 60 s om en om (na 15 s opwarmen). De engine bewaart de kleuren alleen; de render-loop raakt ze niet | 0,16–0,60 % van één kern (gemiddeld 0,31; één uitschieter van 0,60, de andere 0,16–0,29), basis 0,16–0,39 % (0,27): geen verschil buiten de ruis | 26,4–29,1 MB working set, 11,0–12,0 MB private (basis: 26,1–29,2 en 11,0–12,9 MB) |
| Engine | Verversen (#77) op 30 fps in Release, Regenboog, `pauseOnLockAndSleep` uit, zonder OpenRGB: zonder te verversen tegen de basis (#76), en met een client die elke 5 s `RescanDevicesAsync` aanroept (zonder beelden); twee keer 60 s om en om (na 15 s opwarmen). Zonder verversen draait er niets extra: de watcher schrijft alleen bij een klik | Zonder verversen 0,16–0,39 % van één kern, basis 0,21–0,23 %: geen verschil buiten de ruis. Elke 5 s verversen 0,91–1,01 %: elke keer een poging om OpenRGB te bereiken (~1 s, de verbindingstime-out, omdat Windows een geweigerde verbinding op 127.0.0.1 nog even opnieuw probeert) en een nieuwe apparatenlijst; gemiddeld 1,9 s per verversing doordat hij soms achter de gewone poging van elke 5 s wacht | Zonder verversen 26,6–27,8 MB working set, 11,1–11,6 MB private (basis: 26,7–27,7 en 11,1–11,7 MB); met verversen en een verbonden client 38–41 en 13,2–14,7 MB, zoals bij knipperen met een client (#59) |
| Engine | LED's tellen (#78) op 30 fps in Release, Regenboog, `pauseOnLockAndSleep` uit, zonder OpenRGB: zonder te tellen tegen de basis (#77), een verbonden client die niets doet, en een client die ARGB 1 van de gesimuleerde ARGB-controller telt (1 en 5 LED's per seconde, bij de laatste LED weer vanaf 1; zonder beelden); twee keer drie rondes van 60 s om en om, in twee volgordes (na 15 s opwarmen): zonder tellen, de basis en 1 per seconde zes keer, de client zonder tellen en 5 per seconde drie keer. Zonder tellen loopt er niets extra: alleen een `null`-controle per frame. Een frame alloceert ook tijdens het tellen niets (`LightingControllerZoneCountTests`) | Zonder tellen 0,26–0,70 % van één kern, basis 0,31–0,81 %: geen verschil buiten de ruis (in de eerste volgorde was de basis steeds lager, in de omgekeerde steeds hoger). Een verbonden client zonder tellen 0,60–1,04 %; tellen 1 per seconde 1,06–1,77 %, 5 per seconde 1,46–2,26 %. Tellen kost dus zo'n 0,5–0,9 % van één kern zolang het loopt; waar dat precies zit (de zone die naar 60 LED's gaat met een nieuwe apparatenlijst, een event per LED over de pipe, of het eerste werk aan nieuwe code) is niet apart gemeten | Zonder tellen 26,6–29,1 MB working set, 11,2–13,1 MB private (basis: 26,5–30,2 en 11,1–14 MB); client zonder tellen 37–40 en 12,2–13,9 MB; tellen 39–47 en 12,9–19,2 MB, zoals bij knipperen met een client (#59) |
| Engine | LED's tellen (#78, na de review) met Statisch, `framesPerSecond` 30, in Release, `pauseOnLockAndSleep` uit, zonder OpenRGB: zonder client, een verbonden client die niets doet, en een client die ARGB 1 van de gesimuleerde ARGB-controller telt met 1 LED per seconde (bij de laatste LED weer vanaf 1; zonder beelden); drie rondes van 60 s om en om (na 15 s opwarmen). Bij een statisch effect draait er zonder tellen geen render-loop; tijdens het tellen loopt hij op 30 fps, ook al verandert er maar één keer per seconde iets (zie `docs/ideas.md`) | Zonder client 0,03–0,08 % van één kern; een verbonden client zonder tellen 0,34–0,49 %; tellen 1 per seconde 1,27–1,61 %. Tellen kost dus zo'n 1 % van één kern zolang het loopt, iets meer dan met Regenboog (0,5–0,9 %), omdat de render-loop er dan helemaal bij komt | Zonder client 25,3–27,0 MB working set, 11,0–12,5 MB private; client zonder tellen 35,5–36,8 en 12,1–13,9 MB; tellen 38,7–45,6 en 13,3–18,7 MB, zoals met Regenboog |
| Engine | Richting (#79) op 30 fps in Release, Regenboog, `pauseOnLockAndSleep` uit, zonder OpenRGB: zonder richting tegen de basis (#78), en met `direction` 45 bij "Hele huis" (alle gesimuleerde apparaten over hun plekken); drie rondes van 60 s om en om, de volgorde per ronde omgedraaid (na 15 s opwarmen). Daarnaast de tijd van één `Render` over 104 LED's (het gesimuleerde toetsenbord) in een Release-meetprogramma buiten de repo, 200.000 frames na het opwarmen, per index en over de plekken van 45°. De plekken rekent de engine alleen uit als de hoek of de apparatenlijst verandert, en een frame alloceert ook met een richting niets (`LightingControllerDirectionTests`) | Zonder richting 0,29–0,47 % van één kern, basis 0,21–0,47 %; met 45° 0,13–0,55 %: geen verschil buiten de ruis. Per frame van 104 LED's: Regenboog per index 1,1–1,2 µs, over de plekken 1,0–1,5 µs; Golf 1,6–1,8 tegen 1,5–1,6 µs; Kleurenloop 1,7–1,9 tegen 2,2 µs (de plekken springen over de toetsen, de index loopt op); bij 30 fps is het verschil ~0,002 % van één kern. De plekken van het toetsenbord uitrekenen kost ~17 µs per nieuwe hoek | Zonder richting 26,7–29,0 MB working set, 11,2–12,0 MB private (basis: 26,6–28,9 en 11,1–11,9 MB); met 45° 27,0–28,5 en 11,1–11,7 MB. De plekken zelf zijn een `double` per LED per apparaat (~1 KB voor het toetsenbord) |
| Engine | Eigen effecten (#80) op 30 fps in Release, `pauseOnLockAndSleep` uit, zonder OpenRGB: Regenboog op de basis (richting, #79), Regenboog met één eigen effect in `effects.json`, en dat eigen effect bij "Hele huis" (alle gesimuleerde apparaten): een patroon van 104 kleuren, een kleur en een kleur zonder blijven staan, met 1–2 s overlopen, dus bijna altijd een overgang; drie rondes van 60 s om en om, de volgorde per ronde omgedraaid (na 15 s opwarmen). Daarnaast de tijd van één `Render` over 104 LED's in een Release-meetprogramma buiten de repo, 200.000 frames na het opwarmen. Een eigen effect maakt de engine alleen als de lijst van effecten of de verlichting verandert, en een frame alloceert niets (`StepEffectTests`, `LightingControllerCustomEffectTests`) | Regenboog op de basis 0,26–0,68 % van één kern, met deze uitbreiding 0,29–0,75 %; het eigen effect 0,26–0,49 %: geen verschil buiten de ruis (in de eerste ronde lag alles hoger, ook de basis). Per frame van 104 LED's: Regenboog 1,1–1,5 µs; een stap van één kleur 0,08–0,14 µs, ook tijdens het overlopen (één kleur voor alle LED's); een patroon van 104 kleuren 0,64–0,99 µs als het blijft staan en 1,4–1,8 µs tijdens het overlopen; een patroon van 24 kleuren uitgerekt over 104 LED's tijdens het overlopen naar een patroon van 104 2,0–2,4 µs; bij 30 fps hooguit ~0,007 % van één kern | Regenboog op de basis 26,6–29,9 MB working set, 11,1–12,0 MB private; met deze uitbreiding 26,7–29,1 en 11,0–12,0 MB; het eigen effect 27,1–29,5 en 11,1–12,0 MB. Een stap houdt zijn kleuren één keer in het geheugen (3 bytes per kleur), voor alle apparaten samen |
| Engine | Slepen in het paneel Eigen effect (#80, na review) in Release, zonder OpenRGB en zonder beelden voor de app: een client bewaart één eigen effect (4 stappen: twee patronen van 104 kleuren en twee kleuren) 30 keer per seconde, 30 s lang, één aanroep tegelijk, met zes eigen effecten in de lijst, en haalt bij elke `CustomEffectsChanged` de lijst op zoals de app; het effect staat op alle gesimuleerde apparaten. Basis: `effects.json` bij elke wijziging; nieuw: een seconde na de laatste wijziging en bij afsluiten. Drie rondes, de volgorde per ronde omgedraaid; gemeten over 25 s van het slepen | Basis 8,8–14,3 % van één kern, nieuw 6,1–7,2 %; een aanroep gemiddeld 1,5–1,9 ms, nieuw 0,4–0,6 ms; `effects.json` 900 keer geschreven, nieuw 1 keer, met de laatste waarde. Wat overblijft is vooral het ophalen van de hele lijst bij elke wijziging (zie `docs/ideas.md`) | Basis 56–65 MB working set, 25–35 MB private; nieuw 54–61 MB en 24–30 MB |
| Engine | Herstarten (#81) op 30 fps in Release, Regenboog, `pauseOnLockAndSleep` uit, zonder OpenRGB, zonder te herstarten: tegen de basis (eigen effecten, #80); drie rondes van 60 s om en om, de volgorde per ronde omgedraaid (na 15 s opwarmen). Zolang je niet herstart, doet de uitbreiding niets: één menu-item en een `null`-controle bij het starten. Herstarten zelf is niet gemeten (dat stopt en start services, wat alleen op de eigen pc mag) | Basis 0,31–0,44 % van één kern, nieuw 0,13–0,57 %: geen verschil buiten de ruis | Basis 26,8–29,7 MB working set, 11,1–12,2 MB private; nieuw 26,8–29,6 en 11,2–12,0 MB |
| Engine | Geen eigen effect onder de naam van een ingebouwd effect (#84) op 30 fps in Release, Regenboog, `pauseOnLockAndSleep` uit, zonder OpenRGB, zonder `effects.json`: tegen de basis (`schermlezer-namen`); vier rondes van 60 s om en om, de volgorde per ronde omgedraaid (na 15 s opwarmen). De engine kijkt de namen alleen na bij het lezen van `effects.json` en bij het bewaren van een eigen effect; de render-loop raakt het niet | Basis 0,18–0,62 % van één kern (gemiddeld 0,47), nieuw 0,26–0,44 % (0,31): geen verschil buiten de ruis | Basis 26,9–30,3 MB working set, 11,0–11,9 MB private; nieuw 27,0–29,8 en 11,2–12,2 MB |
| Engine | `ProcessImageName` gedeeld met de instellingen-app (#91, "Kies uit wat nu draait"): in `BuurmansRgb.Contracts`, `TryRead` via het gedeelde `TryReadPath`, `OpenProcess` via `Open`, en de onderdelen van Windows in `ProgramNames.ShellParts`. Los gemeten zoals bij #64: de handlers van een Release-build 7 × 10.000 keer aangeroepen met het bureaublad (`explorer.exe`; de sessie was losgekoppeld, dus er stond geen ander venster vooraan) en één ronde van `ProcessList.Find` 7 × 1.000 keer, met een meetprogramma buiten de repo, twee keer om en om met de basis (`verwijderen-bevestigen`). Daarnaast Regenboog op 30 fps in Release, `pauseOnLockAndSleep` uit, zonder OpenRGB, met één scène met `vlc.exe` en `foregroundOnly`; drie rondes van 60 s om en om, de volgorde per ronde omgedraaid (na 15 s opwarmen) | Per aanroep (mediaan): `ProcessImageName.TryRead` 19,9–20,2 µs, basis 19,9–20,3 µs; `SystemScreenSaverEvents.Tell` 20,5 tegen 20,4 µs; `SystemForegroundWindow.Tell` 23,5–24,0 tegen 23,7 µs; beide luisteraars via `ForegroundHook.Tell` 44,9–46,1 tegen 45,0–45,2 µs; `ProcessList.Find` 48,8–54,0 tegen 49,9–55,2 µs: geen verschil buiten de ruis. De engine 0,26–0,39 % van één kern, basis 0,34–0,57 %: geen verschil buiten de ruis | Per aanroep gelijk aan de basis: 80 bytes voor `TryRead` en `SystemScreenSaverEvents.Tell` (alleen de exe-naam; het pad blijft in de buffer die hij hergebruikt), 120 voor `SystemForegroundWindow.Tell`, 200 voor beide luisteraars en 176 voor `ProcessList.Find`. De engine 27,0–29,9 MB working set, 11,2–11,7 MB private (basis: 27,3–30,1 en 11,2–12,0 MB) |
| Engine | "Starten met Windows" gedeeld met de instellingen-app (#92, `AutoStartSwitch` en `GetAutoStartAsync`/`SetAutoStartAsync`): Regenboog op 30 fps in Release, `pauseOnLockAndSleep` uit, zonder OpenRGB en zonder scènes, app dicht; drie rondes van 60 s om en om met de basis (`programma-kiezen`), de volgorde per ronde omgedraaid (na 15 s opwarmen). De switch doet alleen iets bij een klik of als een app de opties leest, dus niets per frame | 0,26–0,36 % van één kern, basis 0,26–0,39 %: geen verschil buiten de ruis | 27,2–29,9 MB working set, 11,2–12,2 MB private (basis: 27,0–29,7 en 11,1–13,0 MB) |
| Engine | Refactor na de review van 28-10 (#?): `AutoStartSwitch.IsAccessError` ook voor de `catch` van `AutoStartExtensions`, en een ongebruikte `using` weg uit `TrayApplicationContext`; Regenboog op 30 fps in Release, `pauseOnLockAndSleep` uit, zonder OpenRGB en zonder scènes, app dicht; vijf rondes van 60 s om en om met de basis (`instellingen-aanvullen`, #92), in een reeks van drie en een van twee, de volgorde per ronde omgedraaid (na 15 s opwarmen). Er verandert niets per frame: de foutafhandeling van "Starten met Windows" draait alleen bij het starten en bij een klik | 0,26–0,34 % van één kern, met één uitschieter van 0,78, basis 0,31–0,44 %: geen verschil buiten de ruis | 26,8–29,6 MB working set, 11,0–12,0 MB private (basis: 26,9–29,8 en 11,1–12,8 MB) |
| App (Release, zichtbaar) | Op Lampen, regenboog | ~5,0 % van één kern (was 10,2 % vóór de begrenzing van het voorbeeld) | 166 MB working set, 83 MB private |
| App (Release, zichtbaar) | Op Kleur met "Hele huis", regenboog | ~3,2 % van één kern (was 3,8 %) | 179 MB working set, 88 MB private |
| App (Release, zichtbaar) | Op Scènes, of geminimaliseerd, regenboog | 0,0–0,2 % (was 1,7 en 0,6 %): geen beelden | 182–191 MB working set, 89–97 MB private |
| App (Debug, Windows vergrendeld) | Lampen of Kleur, regenboog | ~2,1–2,4 % van één kern | 113–133 MB working set, 39–43 MB private |
| App (Release, Windows vergrendeld) | Contrastthema (#?): de kleuren van de templates als `DynamicResource`, zonder contrastthema; op Apparaten met het voorbeeld, regenboog op 30 fps, `pauseOnLockAndSleep` uit, zonder OpenRGB, venster 1240 × 820; drie rondes van 60 s om en om met de basis (`contrast-invoer`), na 15 s opwarmen. Vergrendeld tekent WPF niets, dus het tekenen zelf zit er niet in | 0,96–1,61 % van één kern, basis 0,96–1,90 %: geen verschil buiten de ruis. De LED's en de gloed lopen niet via `DynamicResource` | 116,9–117,6 MB working set, 39,9–42,1 MB private (basis 116,7–117,7 en 39,8–42,1 MB) |

De engine is gemeten in Release met de gesimuleerde lampen en zonder OpenRGB (dan probeert hij
elke 5 seconden te verbinden; dat is het grootste deel van de CPU bij een statische kleur).
Wil je je eigen `settings.json` niet aanraken, start de engine dan met `USERPROFILE` en `APPDATA`
naar een lege map (bijv. via `ProcessStartInfo.Environment`): de engine schrijft dan zijn
instellingen en log daar, en een Release-build met `-p:AppOutputRoot=<map>\` komt buiten de repo.
Zo'n eigen map scheidt alleen de bestanden: de mutex en de events `Local\BuurmansRgb.Engine.*`
hangen aan de Windows-sessie. Een `--exit` van een andere `BuurmansRgb.Engine.exe` in dezelfde
sessie stopt dus ook de engine die je meet, en een start elders opent bij hem de instellingen-app;
start of stop tijdens een meting geen andere engine.
Meet allocaties met de app dicht, bijvoorbeeld met
`dotnet tool install -g dotnet-counters` en daarna
`dotnet-counters collect -p <pid van de engine> --counters System.Runtime --refresh-interval 60 --duration 00:05:00 --format csv`.
Vergelijk `dotnet.gc.heap.total_allocated` met een animatie en met een statische kleur: de teller
loopt in stappen van ongeveer 8 KB, dus een minuut per meetpunt zegt meer dan 5 seconden. De
app is gemeten met 12 gesimuleerde lampen, 30 s per pagina, in een venster van 1240 × 820; met
Windows vergrendeld tekent WPF niets, dus die oudere meting mist het tekenen zelf (ook van de
gloed). Met een zichtbaar venster zit de app boven het budget van 150 MB working set; minder
beelden scheelt CPU, maar geen geheugen.

Waar het geheugen van de app zit (Release, zichtbaar venster, regenboog, gemeten met
`dotnet-counters`):

| Wat | Uitkomst |
| --- | --- |
| .NET-heap | ~8 MB levend, 23 MB vastgelegd; de rest van de 85–98 MB private is native: WPF met DirectX, de geladen DLL's en gecompileerde code |
| Tekenen zonder videokaart (`RenderMode.SoftwareOnly`, als proef) | 130–143 MB working set en 55–60 MB private (35–40 MB minder), maar op Lampen ~10,5 % van één kern tegen ~5–7 % |
| ReadyToRun bij het publiceren | Geen verschil (85 tegen 85 MB private) |
| Alleen de gekozen lamp op Kleur (`SetFrameDevicesAsync`) | Geen verschil in geheugen; allocaties van de app 860 → 805 KB/s met het toetsenbord gekozen |
| Allocaties op Lampen | ~2,2 MB/s: elke `LedBulb` maakte bij elke kleur een nieuwe gloed (`RadialGradientBrush`). Sinds #45 komen de gloeden uit een cache (kleur afgerond op 5 bits per kanaal): 1,64 MB/s, CPU en geheugen gelijk. Een masker per maat (`PushOpacityMask`) gaf 1,8 MB/s maar twee keer zoveel CPU |

De CPU van de app schommelt tussen metingen van 30 s flink (dezelfde build: 0,6 en 2,7 % op Kleur),
dus vergelijk allocaties of meet meer keren.

## Installer bouwen en testen

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build.ps1 -Version 1.2.3
```

`installer\build.ps1` bouwt `artifacts\installer\out\BuurmansRgb-setup.exe` (Inno Setup 7.1,
ongeveer 77 MB): engine en instellingen-app self-contained, de MSI van OpenRGB 1.1 (installeert
de service `OpenRGB`) en de setup van PawnIO 2.2.0. Het script controleert de SHA-256 van beide
downloads en haalt de Inno Setup-compiler van NuGet; met `-Iscc <pad>` gebruik je een eigen
`ISCC.exe`. Zonder `-Version` is de versie 1.0.0. Alles komt in `artifacts\installer`, dat git
negeert. Hoe het werkt en waarom: [openrgb-meeleveren.md](openrgb-meeleveren.md).

- **Na installeren** start de installer de engine als de gewone gebruiker met `--first-run`.
  Die zet "Starten met Windows" aan, alleen als dat nog uit staat.
- **Engine stoppen** in een script: `BuurmansRgb.Engine.exe --exit` (een tweede start geeft het
  signaal `Local\BuurmansRgb.Engine.Exit` en stopt meteen; de engine zelf is binnen een paar
  tienden van een seconde weg, wacht daarop met `Wait-Process BuurmansRgb.Engine`). De installer
  gebruikt nog `taskkill /F`, omdat een engine van 1.0.0 `--exit` niet kent.
- **Verwijderen** stopt de engine, haalt de autostart weg en vraagt dan of OpenRGB en PawnIO
  ook weg moeten (alleen als deze installer ze heeft geïnstalleerd), en of je instellingen weg
  moeten. Stil verwijderen (`/VERYSILENT /SUPPRESSMSGBOXES`) beantwoordt alles met "Nee".
- **Testen in Windows Sandbox**, zonder iets op je eigen pc te installeren:
  1. Zet eenmalig Windows Sandbox aan (Windows-onderdelen → "Windows Sandbox", vraagt een
     herstart).
  2. Draai `installer\build.ps1` en dubbelklik daarna op
     `artifacts\installer\BuurmansRgb-test.wsb`.
  3. De Sandbox installeert stil, controleert de service, poort 6742, de engine, de autostart
     en de verbinding, verwijdert weer en controleert opnieuw. De uitkomst staat in
     `artifacts\installer\sandbox-results\result.json`, met `setup.log`, `uninstall.log` en
     `transcript.txt` erbij. In de Sandbox is er geen RGB-hardware, dus 0 apparaten via
     OpenRGB is normaal.

## Een release maken

`.github/workflows/release.yml` bouwt en test de solution op een Windows-runner van GitHub, bouwt
de installer met `installer\build.ps1` en zet `BuurmansRgb-setup.exe` met zijn SHA-256 als
download bij een GitHub Release, met een korte installatie-uitleg en de wijzigingen sinds de
vorige release. Starten kan op twee manieren:

- een tag pushen (de versie is de tag zonder `v`):

  ```bash
  git tag v1.1.0
  git push origin v1.1.0
  ```

- of op GitHub bij **Actions → Release → Run workflow** een versie invullen (bijv. `1.1.0`); de
  workflow maakt de tag `v1.1.0` dan zelf, op de commit waarop hij draait.

Een versie moet de vorm `1.2.3` hebben. Faalt de build, een test of het downloaden van OpenRGB
of PawnIO, dan komt er geen release. De installer is niet ondertekend, dus SmartScreen
waarschuwt nog (zie `ideas.md`). Is de repository privé, dan kunnen alleen mensen met toegang
de release zien en downloaden.

## Zelf testen op echte hardware

Vergrendelen, slaapstand en echte apparaten zijn niet na te bootsen in de tests; deze stappen
doe je zelf. Noteer wat werkt in [hardware.md](hardware.md).

### Voor elk apparaattype

1. Sluit de software van de fabrikant af, start OpenRGB als administrator met de SDK-server
   aan, daarna de engine en de instellingen-app.
2. Vergelijk per apparaat het aantal LED's en de zones met OpenRGB.
3. Kies een statische kleur, verander de helderheid en kies "Regenboog". Kies daarna
   "Fonkelen", "Komeet", "Scanner" en "Vuur": lopen ze op het apparaat zelf even vloeiend als
   in het voorbeeld, en gaat de komeet bij een fan netjes rond (OpenRGB zegt niet waar LED 1
   zit, dus hij kan de andere kant op draaien)?
4. Zet de pc in slaapstand, maak hem wakker en wacht 10 seconden: de kleuren horen terug te
   zijn.
5. Noteer in `docs/hardware.md` wat werkt, en bij een videokaart of hij een "Direct"-modus
   heeft.

### OpenRGB-status en gesimuleerde apparaten

1. Start de engine en de app met de OpenRGB-service uit: "niet bereikbaar" met de tip, en de
   gesimuleerde apparaten staan er. Start de service: binnen 5 seconden staat er "verbonden" met
   het aantal apparaten, verschijnen de echte en verdwijnen de gesimuleerde. Stop de service: de
   gesimuleerde komen terug. Kijk of de tooltip van het tray-icoon hetzelfde zegt.
2. Kies "Altijd" en "Nooit" en kijk of de apparaten meteen meeveranderen; herstart de engine en
   kijk of de keuze bewaard is.
3. Heeft een apparaat geen "Direct"-modus, kijk dan of het de waarschuwing krijgt, en noteer
   of het de kleur toch volgt.

### Verversen

1. Geef een koptelefoon (of een ander USB-apparaat) een eigen kleur. Zet hem uit, wacht tot hij
   uit de lijst bij Apparaten verdwijnt (of niet), en zet hem weer aan. Komt hij niet vanzelf
   terug, klik dan op "Verversen": binnen enkele seconden staat hij er weer en krijgt hij zijn
   eigen kleur.
2. Kijk in `engine.log` of er "OpenRGB sent no new device list" staat. Dan nam OpenRGB pakket 140
   niet aan van de watcher (die alleen `SetClientName` stuurt en geen protocolversie afspreekt),
   en verbond de engine opnieuw; noteer de versie van OpenRGB in `docs/hardware.md`.
3. Klik op "Verversen" terwijl de OpenRGB-service gestopt is: de melding "OpenRGB is niet
   bereikbaar" verschijnt. Start de service weer: zodra de engine verbonden is, verdwijnt de
   melding.
4. Kijk bij een echte rescan of de lijst bij Apparaten even leeg wordt of maar een deel toont.
   De engine antwoordt na de eerste nieuwe lijst van OpenRGB, en OpenRGB vult zijn lijst mogelijk
   stapsgewijs, dus de knop kan al "Verversen" zeggen voordat alle apparaten terug zijn. Noteer
   wat je ziet in `docs/hardware.md`.

### Fans, case-LED en ARGB-strips

1. Stel bij een ARGB-header of -kanaal het aantal LED's in met + of de schuif en controleer
   dat de hele strip (en niet meer) oplicht, en pas na het loslaten verandert. Herstart OpenRGB
   (of de pc) en kijk of de strip na het verbinden weer de goede lengte heeft, zonder te
   blijven knipperen of herladen.
2. Geef de zones van een controller met meer kanalen elk eigen kleuren (per LED), herstart
   OpenRGB en kijk of elke zone zijn eigen kleuren terugkrijgt.
3. Klik bij een fan met meer ringen op de naam van een ring en geef alleen die ring een kleur.
4. Geef LED 1 van een fan-ring een kleur en kijk waar die op de echte fan zit en welke kant
   LED 2 op gaat.
5. Pas met OpenRGB verbonden bij `zoneLengths` in `%AppData%\BuurmansRgb\settings.json` de
   lengte van een ARGB-kanaal aan en sla op: binnen een seconde heeft de strip de nieuwe lengte
   (ook in de app), zonder te blijven knipperen of herladen. Zet de oude lengte terug en sla
   weer op.
6. Noteer welke controller het is, hoeveel LED's er per kanaal zitten, of het type klopt, waar
   LED 1 van een ring zit en of OpenRGB de lengte na een herstart vergat.

### LED's tellen

1. Kies bij Kleur het moederbord en klik naast een ARGB-header met een fan of strip op
   "Tellen": de header hoort naar zijn grootste aantal LED's te gaan (OpenRGB herlaadt dan even)
   en elke seconde één LED meer aan te zetten, de nieuwste groen, de rest van het moederbord
   met zijn eigen effect. Kijk of het tellen na dat herladen doorgaat en niet opnieuw begint.
2. Kies Pauze zodra de groene LED niet meer aangaat, ga met − terug tot de laatste LED van de
   fan of strip groen is en kies "Dit is de laatste": de hele fan of strip, en niet meer, hoort
   te branden. Herstart OpenRGB en kijk of de lengte blijft.
3. Tel nog eens en kies "Stoppen", sluit de app, vergrendel Windows en kies Verversen tijdens het
   tellen: elke keer hoort de header zijn oude lengte terug te krijgen. Probeer ook 5 per seconde.
4. Vul met deze functie de onbekende aantallen LED's van de fans in `docs/hardware.md` in.

### settings.json met de hand

1. Open `%AppData%\BuurmansRgb\settings.json` in Kladblok terwijl de engine en de app met
   OpenRGB verbonden draaien. Zet `color` op `"#FF0000"` en `framesPerSecond` op 10 en sla op:
   binnen een seconde zijn de echte apparaten rood en staat "Vloeiendheid" op 10. Het bestand
   blijft zoals je het schreef.
2. Doe hetzelfde in VS Code (dat slaat anders op dan Kladblok), met bijv. `"brightness": 0.3`
   en een effect, en sla twee keer kort na elkaar op: de apparaten volgen de laatste keer, en VS
   Code meldt niet dat het bestand van buitenaf veranderde.
3. Wijzig in de app een kleur en sla binnen een seconde in de editor een andere op: de kleur
   uit het bestand blijft staan, op de apparaten en in de app.

### Toetsenbord, muis en headset

1. Klik in het voorbeeld op bijv. Esc, W, A, S, D en de spatiebalk, geef ze een kleur en
   controleer dat precies die toetsen oplichten. Kijk of de tooltip de goede naam geeft en of
   de brede toetsen niet over een andere toets heen lopen.
2. Loop met Tab en de pijltjes door de toetsen van het echte toetsenbord in het voorbeeld en
   kijk of ↑ en ↓ naar de toets erboven en eronder gaan, ook bij de spatiebalk, Enter en het
   numerieke blok. Kies een toets met Spatie en een reeks met Shift+→, geef ze een kleur en
   controleer dat precies die toetsen oplichten.
3. Geef bij de muis één zone een eigen kleur en controleer dat alleen die zone verandert.
4. Kies "Regenboog" en zet bij de muis (bijv. de Logitech G502 met "Primary Zone" en "Logo
   Zone" van één LED) "Zones gelijk" aan: beide LED's hebben op de muis zelf steeds dezelfde
   kleur, ook na een herstart van de engine. Zet hem uit: ze lopen weer uit elkaar. Klik bij een
   van de zones op "Knipperen": alleen die zone knippert. Doe hetzelfde bij een fan met meer
   ringen of een videokaart met een lichtbalk en een logo, en kijk of het uitrekken er goed
   uitziet.
5. Kies "Regenboog" bij het toetsenbord en sleep "Richting" naar 0°, 90° en 45°: de kleuren lopen
   op het toetsenbord zelf van links naar rechts, van boven naar beneden en schuin, net als in het
   voorbeeld, ook over de spatiebalk en het numerieke blok. Doe hetzelfde bij een fan: noteer in
   `docs/hardware.md` waar LED 1 echt zit en welke kant de fan op telt (het voorbeeld neemt
   bovenaan en met de klok mee aan), want daarvan hangt af welke hoek "van boven naar beneden" is.
6. Staat de headset niet in OpenRGB, noteer dan het model.

### Knipperen en plek

1. Open elk apparaat en kijk of precies dat apparaat knippert en daarna zijn eigen kleur
   terugkrijgt, ook bij een bewegend effect.
2. Pauzeer de effecten in het tray-menu en open een apparaat: er hoort niets te knipperen.
3. Kijk of de RAM-sticks (en eventueel twee dezelfde videokaarten) een verschillend adres of
   een verschillende bus tonen, en zoek met "Knipperen" bij het apparaat uit welke welke is. Noteer wat
   OpenRGB als locatie geeft (tooltip).
4. Zet bij een ARGB-header met 0 LED's het aantal LED's en controleer met "Knipperen" bij het apparaat dat
   de hele fan of strip oplicht.
5. Klik bij het moederbord (of een hub met meer kanalen) naast elke zone op "Knipperen": alleen
   de fan of strip op die header hoort te knipperen, de rest van het apparaat houdt zijn kleur
   of effect, en daarna krijgt de zone zijn eigen kleur terug. Noteer welke header welke fan of
   strip is. Kijk ook bij een zone van één LED (een 12V-header): die knippert als geheel.
6. Verander de lengte van een header terwijl hij knippert: het knipperen stopt en het apparaat
   krijgt zijn eigen kleuren terug; met "Knipperen" daarna knippert de nieuwe lengte. Klik ook
   meteen na + of − op "Knipperen": de app stuurt dan eerst de nieuwe lengte. Kijk of de nieuwe
   lengte knippert, of dat het knipperen kort is doordat OpenRGB zijn apparaten herlaadt.

### Aan en uit

1. Zet een apparaat uit en weer aan, en alles uit en aan; kijk of de apparaten echt zwart worden en
   daarna hun eigen kleur en effect terugkrijgen.
2. Zet alles uit, herstart de engine en kijk of ze uit blijven. Kijk of een apparaat zonder
   "Direct"-modus ook uit gaat.
3. Sluit de instellingen-app, klik in een ander programma (bijv. de browser of een spel in
   venstermodus) en druk Ctrl+Alt+Shift+U en daarna Ctrl+Alt+Shift+A: alle apparaten gaan uit en
   weer aan, zoals met "Alles uit" en "Alles aan" in het tray-menu. Kijk of de toetsen in het
   menu naast die items staan. Probeer het ook in een spel op volledig scherm.
4. Zet bij Instellingen "Sneltoetsen voor alles aan en uit" uit: de toetsen doen niets meer en
   het programma met de focus krijgt ze weer. Herstart de engine: ze blijven uit. Zet ze weer
   aan zonder herstart: ze werken meteen.
5. Gebruikt een andere app (bijv. een overlay of macro-tool) een van de toetsen, start dan de
   engine: het icoon meldt welke toets niet werkt en `engine.log` noemt hem; de andere toets
   werkt. Noteer welke app het was.
6. Zet een apparaat uit, kies het bij Kleur en zet er een andere kleur op: het gaat aan, meteen in
   de nieuwe kleur, zonder eerst even de oude te tonen. Doe hetzelfde met één LED in het
   voorbeeld.

### Kleurenloop en kleuren van een effect

1. Kies bij Kleur een apparaat met de Kleurenloop, klik op een kleur van de loop en sleep een
   schuif heen en weer: het echte apparaat loopt vloeiend mee, zonder achter te blijven of na het
   loslaten nog na te ijlen, en eindigt in de kleur waar je losliet. Doe hetzelfde met een reeks
   LED's van een ARGB-strip of fan. Zet het apparaat eerst uit: het gaat aan zodra je schuift, en
   knippert of flikkert daarbij niet.
2. Geef een fan Komeet met drie kleuren en een strip Scanner met twee: de komeet wisselt van
   kleur waar de ronde begint, de scanner aan het eind, zonder flits. Zet Vuur op blauw
   (donkerblauw, blauw, lichtblauw, wit) en op één kleur, en kijk of de donkerste kleur op de
   echte LED's nog zichtbaar gloeit en niet uit lijkt. Fonkelen met een paar kleuren op een
   toetsenbord: elke fonkeling houdt zijn kleur.
3. Geef een strip of fan een kleurenloop van twaalf kleuren, verdeeld en daarna alle LED's
   tegelijk: hij loopt vloeiend door alle twaalf, ook op een korte zone van minder LED's dan
   kleuren. Doe hetzelfde met Komeet en Vuur van twaalf kleuren.

### Eigen effecten

1. Maak een zwaailicht (rood en blauw, 0,3 s blijven staan, 0 s overlopen) en zet het op het
   moederbord, de GPU en een fan: de wissels zijn scherp en lopen op de apparaten gelijk, zonder
   flikkering of een tussenkleur.
2. Maak een effect met lang overlopen (bijv. oranje en donkerblauw, elk 1 s blijven staan en 5 s
   overlopen): het overlopen is op de echte LED's vloeiend, ook bij lage helderheid, zonder
   zichtbare stapjes.
3. Kleur bij Per LED het toetsenbord in een verloop, neem het over in een stap en zet het effect
   op "Hele huis": op het toetsenbord staat het patroon zoals je het kleurde, op een fan en een
   ARGB-strip uitgerekt over hun LED's.
4. Sleep de schuif "Overlopen" heen en weer terwijl het effect op een echt apparaat staat: het
   apparaat loopt mee zonder te haperen, en de engine blijft onder 1 % van één kern.

### Herstarten

Op een pc met OpenRGB als service en PawnIO (zoals de installer ze zet):

1. Kijk welke naam de driver van PawnIO heeft: `sc query type= driver | findstr /i pawn` in een
   opdrachtprompt. Zet de naam in `docs/hardware.md` en bij "Naam van de driver" in
   `docs/openrgb-meeleveren.md`; is het niet `PawnIO`, pas dan
   `ServiceRestarter.PawnIoService` en deze tekst aan.
2. Kies "Herstarten" in het tray-menu en keur de UAC-vraag goed. De tooltip zegt
   "buurmansRgb – herstarten…", het icoon verdwijnt even en komt terug, en de melding zegt
   "buurmansRgb is herstart.", "OpenRGB is herstart." en "PawnIO is herstart.". Controleer met
   `netstat -ano | findstr 6742` dat OpenRGB weer luistert op `127.0.0.1:6742`, en dat de
   apparaten bij Apparaten terugkomen en hun kleur krijgen.
3. Doe het nog een keer met de instellingen-app open: die gaat dicht en weer open en verbindt
   met de nieuwe engine.
4. Kies "Herstarten" en weiger de UAC-vraag: alleen buurmansRgb herstart, met de melding dat
   OpenRGB en PawnIO niet herstart zijn; OpenRGB blijft verbonden.
5. Start FanControl of LibreHardwareMonitor (die PawnIO gebruiken) en kies "Herstarten": PawnIO
   wordt overgeslagen met een melding, OpenRGB herstart wel. Noteer in `docs/hardware.md` wat er
   gebeurde, ook of PawnIO daarna nog werkt als je dat programma sluit.

Zonder OpenRGB en PawnIO (zoals op een ontwikkel-pc) vraagt "Herstarten" geen UAC en herstart
het alleen buurmansRgb, met "OpenRGB staat niet als service op deze pc." en "PawnIO is niet
gevonden.".

### Scènes

1. Maak scènes "Werk", "Nacht" en "Gamen" en wissel via het tray-menu en met een klik op hun
   kaart; kijk of alle apparaten meegaan.
2. Geef "Nacht" een tijd een paar minuten verder en wacht. Zet daarna een tijd, laat de pc
   erover heen slapen en kijk dat de scène na het ontwaken niet alsnog komt ("Gemiste tijden
   inhalen" uit, zoals standaard).
3. Geef "Gamen" de naam van je spel, start het spel en sluit het weer: de verlichting van
   daarvoor hoort terug te komen. Verander tijdens het spel iets en kijk dat dat blijft.
4. Start een spel dat als administrator draait of anti-cheat heeft, en kijk of de engine het
   ziet.
5. Sluit de engine af (tray, "Afsluiten") terwijl het spel draait: de verlichting van daarvoor
   hoort terug te komen. Start de engine weer: de scène van het spel komt terug, en na het
   sluiten van het spel weer de verlichting van daarvoor.
6. Zet bij Instellingen "Gemiste tijden inhalen" aan, geef "Nacht" een tijd een paar minuten
   verder en laat de pc erover heen slapen: zo'n 2 seconden na het ontwaken hoort "Nacht" aan te
   gaan, ook op apparaten die na de slaap hun eigen effect tonen. Slaap daarna kort zonder dat er
   een tijd voorbij komt: de verlichting blijft zoals hij was, ook als je vlak daarvoor de klok
   van Windows over een tijd heen zette. Sluit de engine af, laat een tijd voorbijgaan en start
   hem weer: de scène van die tijd gaat aan; met het spel van stap 3 open alleen die van het
   spel, en de scène van de tijd pas als je het spel sluit.
7. Geef "Werk" twee tijden een paar minuten na elkaar, de tweede alleen op vandaag, en zet
   tussendoor een andere kleur: bij beide tijden hoort "Werk" aan te gaan. Een `profiles.json`
   van vóór de meer tijden per scène (met alleen `loadAt` en `loadOn`) werkt zonder iets te
   doen.
8. Geef "Film" een programma dat je open laat staan (bijv. `vlc.exe` of `notepad.exe`) met het
   vinkje "Alleen als het op de voorgrond staat", en "Gamen" je spel zonder vinkje. Klik op het
   programma en wissel met Alt+Tab weg en terug: de scène hoort meteen aan te gaan en de
   verlichting van daarvoor meteen terug te komen. Kijk dat de taakbalk, het Alt+Tab-venster
   zelf, het startmenu (Windows-toets, dan Esc), het overloopvak van het systeemvak (het pijltje
   naast de klok) en het tray-menu van BuurmansRgb de scène laten staan. Geef een tweede
   programma ook het vinkje en wissel tussen de twee: de apparaten horen rechtstreeks van de ene
   scène naar de andere te gaan, zonder flits van de verlichting van daarvoor. Start het spel
   terwijl "Film" vooraan staat, wissel naar het spel en weer terug. Vergrendel met Win+L
   terwijl "Film" vooraan staat: de scène blijft; ontgrendel en wissel weg. Sluit het programma
   terwijl het vooraan staat, wacht een paar seconden en open het weer via het startmenu of de
   taakbalk: de scène hoort weer aan te gaan. Een spel in volledig scherm (exclusive fullscreen)
   en een programma dat als administrator draait, horen ook te tellen.
9. Open een paar programma's (Outlook, een spel, Kladblok, de Instellingen van Windows) en klap
   bij een scène "Kies uit wat nu draait" open: elk programma staat er met een naam die je
   herkent en de exe-naam erachter ("Microsoft Outlook (olk.exe)"), op alfabet, zonder
   `TextInputHost.exe` en `ApplicationFrameHost.exe`, en de lijst klapt zonder haperen open, ook
   de eerste keer niet leeg. Markeer met de pijltjes een item terwijl hij open is: draait er
   niets nieuws, dan blijft dat gemarkeerd en meldt Verteller geen nieuwe lijst. Kies er een: in
   het veld komt alleen de exe-naam. Een programma dat als administrator draait
   hoort er ook bij te staan, met zijn naam of anders zijn venstertitel. Met Verteller (Ctrl+
   Windows-toets+Enter) hoort elk item als "Microsoft Outlook (olk.exe)" voorgelezen te worden.

### Pauzeren

1. Kies "Regenboog" en vergrendel Windows (Windows-toets+L): de apparaten blijven op één frame
   staan. Ontgrendel: de regenboog loopt weer. Kijk in Taakbeheer (Details, kolom CPU-tijd)
   dat de CPU-tijd van `BuurmansRgb.Engine.exe` tijdens het vergrendelen niet oploopt.
2. Pauzeer in het tray-menu, vergrendel en ontgrendel: de effecten blijven gepauzeerd en het
   vinkje blijft staan. Hervat in de app: het vinkje verdwijnt.
3. Zet de pc in slaapstand en maak hem wakker: op het vergrendelscherm horen de kleuren binnen
   10 seconden terug te zijn (stilstaand), na het ontgrendelen loopt de animatie weer.
4. Wissel van gebruiker en kom terug, of verbind met Extern bureaublad en verbreek: de animatie
   stopt en loopt daarna weer.
5. Zet bij Instellingen "Pauzeren bij vergrendelen, slaapstand en scherm uit" uit en vergrendel:
   de regenboog loopt door.
6. Laat die schakelaar uit en geef een scène het programma `timeout.exe`. Typ in een terminal
   `timeout /t 30`: de scène gaat aan. Vergrendel meteen en wacht tot de 30 seconden om zijn: de
   scène blijft staan. Ontgrendel: meteen daarna komt de verlichting van daarvoor terug.
7. Zet de schakelaar weer aan, kies "Regenboog" en zet bij Windows Instellingen, Systeem,
   Energiebeheer de time-out van het scherm op 1 minuut, zonder vergrendelen na het uitgaan
   (bij Accounts, Aanmeldingsopties: "Nooit" voor opnieuw aanmelden). Wacht zonder muis of
   toetsenbord tot het scherm uitgaat: de apparaten blijven op één frame staan en de CPU-tijd
   loopt niet op. Beweeg de muis: de regenboog loopt meteen weer, zonder ontgrendelen. Het
   dimmen vlak voor het uitgaan (op een laptop) pauzeert niet.
8. Vergrendel met Windows-toets+L en wacht tot het scherm op het vergrendelscherm uitgaat.
   Beweeg de muis: de regenboog blijft stilstaan tot je ontgrendelt. Laat daarna het scherm
   uitgaan en de pc in slaapstand gaan (of zet hem erin), en maak hem wakker: na het
   ontgrendelen loopt de regenboog weer.
9. Geef een scène het programma `timeout.exe` (zoals bij 6) en typ `timeout /t 120`; laat het
   scherm uitgaan zonder vergrendelen. Na de 120 seconden komt de verlichting van daarvoor
   terug terwijl het scherm nog uit is: bij alleen scherm uit kijkt de engine wel naar
   programma's.
10. Kies bij Instellingen "Apparaten tijdens deze pauze" "Uit" en "Regenboog", en vergrendel: alle
    apparaten gaan uit. Ontgrendel: de regenboog loopt weer op alle apparaten. Doe hetzelfde met
    "Dimmen (20 %)": de apparaten houden het laatste beeld op ongeveer een vijfde van de helderheid.
11. Laat "Uit" staan, zet de pc in slaapstand en kijk of apparaten die stroom houden (bijv. van een
    voeding met standby-stroom of een USB-controller) uitgaan vóórdat de pc slaapt. Maak hem
    wakker: op het vergrendelscherm blijven de apparaten uit, ook na de 10 seconden van het
    herstellen; na het ontgrendelen komen de kleuren en de regenboog terug, ook op apparaten die
    na de slaap hun eigen effect lieten zien.
12. Laat het scherm uitgaan zonder vergrendelen (zoals bij 7): de apparaten gaan uit; beweeg de
    muis: de regenboog komt meteen terug.
13. Pauzeer in het tray-menu en vergrendel: de apparaten blijven zoals ze waren. Ontgrendel en
    hervat: de regenboog loopt weer.
14. Wissel met "Uit" van gebruiker naar iemand met een eigen engine: de apparaten krijgen na het
    wisselen de kleuren van de tweede gebruiker en niet het zwart van de eerste.
15. Kies "Regenboog" en "Laatste beeld laten staan", en zet bij Windows een schermbeveiliging aan
    (Instellingen, Persoonlijke instellingen, Vergrendelingsscherm, Schermbeveiliging), bijv.
    "Linten", met een wachttijd van 1 minuut en zonder het vinkje voor het aanmeldingsscherm bij
    hervatten. Zet de time-out van het scherm langer dan dat. Wacht zonder muis of toetsenbord:
    zodra de schermbeveiliging begint, staan de apparaten op één frame stil en loopt de CPU-tijd
    niet op; in de instellingen-app (als die open was) staat de melding van de schermbeveiliging.
    Beweeg de muis: de regenboog loopt meteen weer, zonder ontgrendelen. Klik in hetzelfde
    venster op "Voorbeeld": de regenboog loopt door, ook na een seconde, en in het logboek van de
    engine staat de exe-naam (bijv. `Ribbons.scr`) met dat Windows geen schermbeveiliging meldt.
    Staat daar bij de echte schermbeveiliging dat Windows het "a second later" toch zegt, noteer
    dat dan. Zet daarna de schermbeveiliging terug zoals hij was.
16. Doe hetzelfde met het vinkje voor het aanmeldingsscherm bij hervatten aan: na de muisbeweging
    staat het vergrendelscherm en blijft de regenboog stil tot je ontgrendelt. Met "Dimmen" of
    "Uit" bij "Apparaten tijdens deze pauze" dimmen de apparaten of gaan ze uit zodra de
    schermbeveiliging begint, en blijven ze gedimd of uit als het vergrendelscherm komt, zonder
    tussendoor even fel te worden; pas na het ontgrendelen loopt de regenboog weer.
17. Geef een scène het programma van een venster dat je open laat (bijv. `notepad.exe`) met het
    vinkje "Alleen op de voorgrond", zet Kladblok vooraan en wacht op de schermbeveiliging: de
    scène blijft staan (de schermbeveiliging telt niet als wegwisselen), ook na de muisbeweging.
    Met de schakelaar "Pauzeren bij vergrendelen, slaapstand en scherm uit" uit loopt de
    regenboog tijdens de schermbeveiliging door.

## Mappen en documenten

- `src/` – de projecten; `tests/` – de tests, zonder hardware
- `installer/` – `build.ps1`, `BuurmansRgb.iss`, de Sandbox-test en `THIRD-PARTY-NOTICES.txt`
- `design/` – het ontwerppakket van de instellingen-app (`DESIGN.md`, icoon, referentieschermen)
- `docs/images/` – de schermafbeeldingen in `README.md`, gemaakt met de gesimuleerde apparaten
- [installatie.md](installatie.md) – installeren, bijwerken en verwijderen in het kort
- [handleiding.md](handleiding.md) – de instellingen-app, het tray-icoon, de
  instellingenbestanden en per apparaattype
- [bouwplan.md](bouwplan.md) – architectuur, fasering, taken en uitbreidingen
- [openrgb-meeleveren.md](openrgb-meeleveren.md) – waarom de installer OpenRGB en PawnIO zo
  meelevert
- [hardware.md](hardware.md) – jouw hardware en wat werkt
- [ideas.md](ideas.md) – ideeën voor later
- [prompts.md](prompts.md) – kant-en-klare opdrachten voor agents
- `AGENTS.md` – werkafspraken voor agents
