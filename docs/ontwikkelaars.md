# Voor ontwikkelaars

Bouwen, starten en testen, de installer, en hoe BuurmansRgb van binnen werkt. Hoe je de app
gebruikt, staat in de [handleiding](handleiding.md); het plan en de stand per taak in
[bouwplan.md](bouwplan.md).

## Bouwen, starten en testen

Nodig: Windows 10/11 en de .NET 10 SDK. Voor echte hardware ook OpenRGB met de SDK-server aan
(zie "OpenRGB koppelen" in de [handleiding](handleiding.md#openrgb-koppelen)); zonder OpenRGB werkt alles met gesimuleerde lampen.

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

Zonder OpenRGB staan er twaalf gesimuleerde lampen klaar, zie "Instellingen" in de
[handleiding](handleiding.md#instellingen).

## Projecten

| Project | Wat erin zit |
| --- | --- |
| `BuurmansRgb.Core` | Modellen (`RgbColor`, `RgbDevice`, `RgbZone`, `LedMatrix`), `IDeviceProvider`, `IEffect`, `ServerStatus`; geen hardware, UI of IPC |
| `BuurmansRgb.Effects` | De effecten, tijdgebaseerd, schrijven in een buffer die de engine hergebruikt |
| `BuurmansRgb.Contracts` | `IEngineApi`, de DTO's en de opzet van pipe en StreamJsonRpc |
| `BuurmansRgb.Providers.Simulated` | De gesimuleerde lampen |
| `BuurmansRgb.Providers.OpenRgb` | De koppeling met de SDK-server via OpenRGB.NET |
| `BuurmansRgb.Engine` | Tray, render-loop, IPC-server, opslag, scènes, pauzeren (WinExe met WinForms) |
| `BuurmansRgb.UI` | De instellingen-app (WPF, CommunityToolkit.Mvvm) |

Pakketversies staan centraal in `Directory.Packages.props`; `nuget.config` gebruikt alleen
nuget.org. `MessagePack` is vastgepind op een versie zonder bekend lek, omdat StreamJsonRpc
een oudere meebrengt. UI-teksten staan in `src/BuurmansRgb.UI/Resources/Strings.resx` en
`src/BuurmansRgb.Engine/Resources/Strings.resx`.

## Tests

Tests gebruiken xUnit v3 op Microsoft.Testing.Platform (ingesteld in `global.json`) en draaien
zonder hardware. Per project een testproject in `tests/`. Helpers die elk testproject gebruikt
(`Eventually`, `TestColors`, `TestCancellation`, `TestTimeouts`) staan in `tests/Shared`;
`tests/Directory.Build.props` bouwt ze mee. De OpenRGB-server in het geheugen
(`FakeOpenRgbConnector`, `InMemoryOpenRgb`) staat in het hulpproject
`tests/BuurmansRgb.Providers.OpenRgb.Testing`, zonder eigen tests; daarnaast is er een kleine
SDK-server op een loopback-socket (`OpenRgbTestServer`).

| Onderdeel | Belangrijkste testklassen |
| --- | --- |
| Effecten en render-loop | Een klasse per effect in `BuurmansRgb.Effects.Tests`, `AnimationLoopTests`, `LightingControllerAnimationTests` (een frame zonder verbonden app alloceert 0 bytes), `LightingControllerSpeedTests`, `EffectClockTests` |
| Instellingen | `JsonSettingsStoreTests`, `EngineSettingsTests`, `LightingSettingsSaverTests`, `EngineHostTests`, `EffectOptionsViewModelTests` (vloeiendheid en pauzeren bij vergrendelen in de app), `SceneTimesViewModelTests` (gemiste tijden inhalen) |
| OpenRGB | `OpenRgbDeviceMapperTests`, `OpenRgbDeviceProviderTests`, `OpenRgbNetConnectionTests`, `OpenRgbProviderStatusTests`, `OpenRgbProviderServerTests`, `OpenRgbServerWatcherTests` |
| Gesimuleerde lampen | `SimulatedDeviceProviderTests`, `SimulatedDevicesSwitchTests`, `SwitchableDeviceProviderTests` |
| Zones en lengtes | `RgbZoneTests`, `LedMatrixTests`, `LightingControllerZoneTests`, `LedColorStoreTests`, `ZoneLengthKeeperTests`, `ZoneLengthKeeperOpenRgbTests`, `ZoneLengthViewModelTests` |
| Knipperen en plek | `LightingControllerFlashTests`, `LightingControllerZoneFlashTests` (alleen de LED's van één zone, een zone zonder LED's of die er niet meer is, een nieuwe lengte tijdens het knipperen, 0 bytes per frame), `RgbZoneTests` (`RangesOf`), `DevicePreviewViewModelTests` en `MainViewModelTests` (de knop per zone, eerst een lengte die nog wacht, een knop annuleert de flits die bij de lampkeuze wacht), `DeviceInfoTests`, `DeviceLocationLabelsTests` |
| Aan en uit | `LightingControllerOnTests`, `LampsViewModelTests`, `MainViewModelLampTests` (ook een kleur op een lamp die uit staat zet hem aan, na de kleur in de wachtrij; bij de kleurenloop in `MainViewModelColorLoopTests`) |
| Scènes | `ProfileLibraryTests`, `EngineHostTests` en `MainViewModelSceneTests` (ook welke scène aan staat, ook na een herstart), `ProfileAutomationTests` (meer tijden: volgorde, dubbele, hooguit 8), `JsonProfileStoreTests` (ook `loadTimes` naast het oude `loadAt` en `loadOn`, en waarden die niet te lezen zijn), `DailyTimeTests` (ook op gekozen dagen), `ProfileTimerTests` (ook meer tijden per scène, elk op eigen dagen, en gemiste tijden inhalen na het ontwaken en bij het starten, met de echte `ResumeRestorer` op een nepklok; bij het starten terwijl een spel draait ook in `EngineHostTests`, met de echte `ProgramWatcher`), `ProfileTriggersTests` (ook een tijd terwijl Windows vergrendeld is, met `FakeAwayState`, en terwijl een programma op de voorgrond staat), `ProgramWatcherTests`, `ProgramWatcherForegroundTests` (alleen op de voorgrond: met `FakeForegroundWindow` en `FakeRunningPrograms`, wisselen tussen twee voorgrond-scènes, samen met "zolang het draait", vergrendeld, gesloten en opnieuw vooraan, het vinkje aan en uit), `SystemForegroundWindowTests` (welke vensters meetellen, het eigen proces en het bureaublad, ook twee keer na elkaar, en de echte `SetWinEventHook` alleen zolang er gevolgd wordt), `ProcessListTests`, `ProfilesViewModelSceneTests`, `SceneCardTests`, `ProfileAutomationViewModelTests` (ook tijden toevoegen en weghalen, en het vinkje voor de voorgrond) en `WindowedProgramsTests` (een programma kiezen) |
| Pauzeren en slaapstand | `LightingControllerPauseTests`, `LightingControllerPausedLightsTests` (wat de lampen bij elke stand en elke reden krijgen, en na het hervatten; de handmatige pauze stuurt niets), `LightingControllerRestoreTests`, `AutoPauseTests` (ook `Away` met het pauzeren uit, het scherm uit met `FakeDisplayEvents`, samen met vergrendelen en slaap, en reeksen van gebeurtenissen met `pausedLights` op "off", ook zwart vóór het slapen en na het ontwaken met `ResumeRestorer`), `ResumeRestorerTests`, `SystemSessionEventsTests`, `SystemDisplayEventsTests` (het lezen van `POWERBROADCAST_SETTING`, een `WM_POWERBROADCAST` via `SendMessage` op de eigen thread, en de echte registratie zonder waarschuwing), `EffectsPauseViewModelTests`, `ProgramWatcherTests` (niet in de proceslijst kijken zolang Windows vergrendeld is, slaapt of op geen scherm staat, ook via een extern bureaublad en een slaap die met ontgrendelen eindigt, met `pauseOnLockAndSleep` aan en uit; met `FakeSessionEvents`, `FakePowerEvents` en `FakeRunningPrograms`; andere tests van `ProgramWatcher` krijgen `FakeAwayState`, waarbij niemand weg is) |
| Sneltoetsen | `TrayPresenterTests` (een toets doet wat "Alles aan" of "Alles uit" doet, aan en uit zetten, een bezette toets; met `FakeGlobalHotkeys`), `AllOnOffHotkeysTests`, `WindowsGlobalHotkeysTests` (de echte `RegisterHotKey` op Ctrl+Alt+Shift+F24, die op geen toetsenbord zit: twee keer registreren faalt en wordt gelogd; een `WM_HOTKEY` via `SendMessage` op de eigen thread draait de actie van de toets met dat vaste id en na `Unregister` niets meer; is de toets bezet door een andere testrun of is er geen bureaublad, dan slaat de test zich over), `EngineHostTests` en `JsonSettingsStoreTests` (`hotkeys`), `EngineApiTests`, `HotkeysViewModelTests` |
| Tray en opstarten | `TrayPresenterTests`, `SingleInstanceTests`, `EngineOptionsTests`, `AutoStartExtensionsTests` (nooit tegen de echte Run-sleutel), `RegistryAutoStartTests` (ook een verplaatste engine) |
| IPC | `EngineApiTests`, `EngineIpcServerTests`, `EngineRpcTests` (over een echte named pipe) |
| Logbestand | `FileLogTests` (regels, herhalingen tellen, een nieuw bestand na 1 MB, lezen terwijl het open is) |
| Voorbeeld | `PreviewFramesTests` en `MainViewModelFramesTests` (hooguit 20 beelden per seconde, geen beelden als de app ze niet wil, op Kleur alleen de gekozen lamp), `DevicePreviewViewModelTests`, `PreviewViewModelTests`, `LedLayoutTests`, `RingLayoutTests`, `WideKeysTests`, `LedPreviewViewTests` (UI Automation), `FocusKeeperTests` en `SliderDragTests` (met een echt venster buiten beeld) |
| Pagina's | `NavigationViewModelTests`, `ColorPickerViewModelTests`, `MainViewModelTests` en de andere `MainViewModel*Tests`, waaronder `MainViewModelFollowLightingTests` (Kleur volgt een scène uit het tray-menu) |

## Met de gesimuleerde lampen bekijken

Start de engine en de instellingen-app zonder OpenRGB en loop de pagina's langs met muis en
toetsenbord. Een paar dingen om te proberen:

- Kies "Regenboog": het voorbeeld en de strip bij "Hele huis" lopen mee.
- Kies bij Kleur "Simulated ARGB controller" en klik bij "ARGB 2" een paar keer op + of sleep
  de schuif. Sluit de engine af (tray, "Afsluiten") en start hem opnieuw: "ARGB 2" heeft nog
  steeds die lengte.
- Klik bij "Simulated ARGB controller" naast "ARGB 1" op "Knipperen": in het voorbeeld knippert
  alleen die zone. "ARGB 2" heeft pas een knop als het LED's heeft.
- Kies "Gesimuleerde ventilator met twee ringen" en klik op "Outer ring"; kies "Gesimuleerd
  toetsenbord", wijs een toets aan voor zijn naam en klik op het brede deel van de spatiebalk.
- Zet twee scènes in elkaar en kies er een in het tray-menu; geef een scène een tijd een minuut
  verder en met "+ Nog een tijd" een tweede een minuut daarna (zet er tussendoor een kleur), of
  `notepad.exe`, en start en sluit Kladblok. Een tijd weghalen gaat met "−"; de kaart zegt dan bijv.
  "Elke dag om 21:00 en nog 1", of weer alleen de ene tijd.
- Zet op Lampen een lamp uit, kies hem bij Kleur en kies "Op de strip zetten": hij gaat aan
  en zijn schakelaar gaat mee; de andere lampen blijven zoals ze waren.
- Vink in het tray-menu "Effecten pauzeren" aan: de melding met "Hervatten" verschijnt.
- Zet bij Instellingen "Nooit": de gesimuleerde lampen verdwijnen meteen.
- Druk Ctrl+Alt+Shift+U en Ctrl+Alt+Shift+A terwijl een ander venster de focus heeft: de
  gesimuleerde lampen gaan uit en aan. Zet bij Instellingen "Sneltoetsen voor alles aan en uit"
  uit: de toetsen doen niets meer en staan niet meer naast de items in het tray-menu.

## Hoe de engine werkt

- **Render-loop:** alleen als minstens één lamp een bewegend effect toont, draait een
  `PeriodicTimer` op `framesPerSecond`; ook tijdens het knipperen. Een frame stuurt alleen de
  lampen waarvan de kleuren veranderden, en alloceert niets zolang de instellingen-app dicht
  is. Gaat een frame mis, dan probeert het volgende het opnieuw.
- **Beelden voor het voorbeeld:** een render zegt `PreviewFrames` (één per verbonden app) alleen
  dat er iets nieuws is; een timer op de thread pool kopieert dan het nieuwste beeld en stuurt
  het, hooguit 20 keer per seconde, zodat de animatie nooit op de app wacht. De app zegt met
  `IEngineApi.SetFramesWantedAsync` dat ze geen beelden wil zolang ze geminimaliseerd is of een
  pagina zonder voorbeeld toont (Scènes, Instellingen); dan kopieert de engine niets. Met
  `SetFrameDevicesAsync` vraagt Kleur met één lamp alleen de beelden van die lamp; Lampen en
  "Hele huis" krijgen ze allemaal. Een beeld met maar een deel van de lampen laat de strip op
  Lampen ongemoeid.
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
- **Lampen tijdens de pauze:** `LightingController.PausedLights` (`PausedLightsMode` in
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
  `SystemForegroundWindow` (achter `IForegroundWindow`) zet daarvoor
  `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)`
  op de thread van het tray-icoon, die al een message loop heeft: Windows post elk nieuw
  voorgrondvenster daarheen, zonder DLL in andere programma's en zonder pollen. De naam komt van
  `GetWindowThreadProcessId` en `ProcessImageName` (`QueryFullProcessImageName`, dezelfde als in
  `ProcessList`), en `ProgramNames.Comparer` vergelijkt hem. `ProgramWatcher` vraagt met
  `Follow` alleen om de hook zolang minstens één scène de keuze aan heeft; dat komt van elke
  thread, dus `Follow` post naar een verborgen message-only venster, dat de hook op de
  tray-thread zet of met `UnhookWinEvent` weghaalt (ook bij afsluiten). Na het zetten meldt hij
  één keer wat er nu vooraan staat. Hij meldt elk venster dat telt, ook van het programma dat al
  vooraan stond: dat kan intussen gesloten en opnieuw gestart zijn, met alleen de taakbalk of het
  startmenu ertussen. `ProgramWatcher` houdt zelf bij wat nieuw is. Vensters die niet tellen
  (`SystemForegroundWindow.Counts`): de taakbalk (`Shell_TrayWnd`, `Shell_SecondaryTrayWnd`),
  het overloopvak van het systeemvak (`TopLevelWindowForOverflowXamlIsland` op Windows 11,
  `NotifyIconOverflowWindow` op Windows 10), Alt+Tab en Win+Tab
  (`XamlExplorerHostIslandWindow`, `MultitaskingViewFrame`, `TaskSwitcherWnd`,
  `ForegroundStaging`), `LockApp.exe`, het startmenu en zoeken, meldingen en snelle instellingen
  (`StartMenuExperienceHost.exe`, `SearchHost.exe`, `SearchApp.exe`, `ShellExperienceHost.exe`,
  `ShellHost.exe`), de engine zelf en een venster waarvan de naam niet te lezen is: dan blijft
  de stand zoals hij was. In `ProgramWatcher` is op de voorgrond komen een start en wegwisselen
  een stop, op dezelfde stapel als de programma's die tellen zolang ze draaien. Bij het wisselen
  tussen twee voorgrond-scènes laadt hij eerst de nieuwe en haalt hij daarna de oude eronder weg
  (die geeft zijn verlichting van daarvoor door), dus er gaat maar één wijziging naar de lampen.
  Zolang er een voorgrond-scène aan staat, kijkt hij die naam ook in de proceslijst na, zodat
  een programma dat sluit zonder dat er een ander venster vooraan komt toch stopt; pas als hij
  het daar één keer zag, zodat een naam die nog niet te lezen is niet als gestopt telt. Tijdens
  `Away` onthoudt hij alleen wat Windows vooraan meldt; na terugkomen telt dat op de timer (niet
  onder de lock van `AutoPause`) één keer, behalve als de proceslijst het intussen zag stoppen.
- **Welke scène aan staat:** `ProfileLibrary.ActiveName`, gezet door `Load`, `Save` en `Show` (waarmee
  `ProgramWatcher` na een spel ook de scène van daarvoor terugzet) en gewist door elke andere
  `LightingChanged`; de app vraagt het met `IEngineApi.GetActiveProfileAsync`. Bij elke wissel
  (`ActiveChanged`) bewaart `EngineHost` de naam als `activeScene` in `settings.json`, en na het
  starten zet `RestoreActive` hem terug als die scène nog bestaat.
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
- **Scènes in het geheugen:** staat er een `profiles.json`, dan leest de engine het bij het
  starten één keer, voor de tijden en programma's.
- **Gesimuleerde lampen:** `SimulatedDevicesSwitch` volgt de apparatenlijst en beslist bij elke
  wijziging opnieuw, onder één lock.
- **Zonelengtes:** `ZoneLengthKeeper` vraagt een bewaarde lengte alleen als een apparaat
  verschijnt en wacht tot het die meldt, zodat een weigerend apparaat geen lus van aanpassingen
  geeft. Een zone waarvan de naam weg is of dubbel voorkomt, blijft met rust; een lengte buiten
  het bereik blijft bewaard maar wordt niet gevraagd.
- **Knipperen:** `IEngineApi.FlashDeviceAsync`, in de engine `LightingController.Flash` en
  `DeviceFlash`. Eén zone gaat met `FlashZoneAsync` en `LightingController.FlashZone`: de flits
  dekt dan alleen het LED-bereik van die zone (`RgbZone.RangesOf`, zoals de engine de
  zone nu kent; het voorbeeld in de app deelt de zones met dezelfde functie in), de rest van het apparaat rendert zijn effect gewoon. Er is steeds één
  `DeviceFlash`, van een apparaat of een zone; een zone die de engine niet (meer) kent of
  zonder LED's slaat hij over, zoals een onbekend apparaat.

## Ontwerp, lettertypes en icoon

- **Ontwerp:** `design/` bevat het ontwerppakket (`DESIGN.md`, het icoon en drie schermen als
  referentie). Kleuren, maten en lettertypes staan in `src/BuurmansRgb.UI/Themes/Tokens.xaml`,
  de componenten en de donkere stijl van de gewone controls in `Themes/Controls.xaml`, wat de
  pagina's delen in `Themes/Theme.xaml`, en de pagina's in `Views/Pages`. De gloed van een
  LED-bolletje is een radiale gradiënt (`LedBulb`) in plaats van een `DropShadowEffect`, zodat
  niet elk bolletje elk frame apart gerenderd wordt.
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
| Engine | Vergrendeld, slaapstand of scherm uit | 0 frames, geen timer | |
| Engine | Een scène met een programma | ~0,05 % van één kern extra (~100 miljoen CPU-cycli per minuut) | ~0,3 MB voor de procesnamen |
| Engine | Tien scènes met een tijd en eigen kleuren | | ~51 KB op de heap |
| Engine | Regenboog, "Gemiste tijden inhalen" aan met twee scènes met een tijd (#51) | 0,49–0,75 % van één kern, main zonder deze uitbreiding 0,65–0,73 %: geen verschil buiten de ruis | 27–30 MB working set, 11–12 MB private (main 25–29 en 11–12 MB) |
| Engine | Regenboog, sneltoetsen aan (#52) | 0,36–0,39 % van één kern, zonder sneltoetsen (#51) 0,26–0,39 %: geen verschil buiten de ruis; een toets kost alleen iets als je hem indrukt | 26–28 MB working set, 11–11,7 MB private (zonder: 24–27 en 11–12 MB) |
| Engine | Regenboog, `pauseOnLockAndSleep` uit, een scène met een programma, niet vergrendeld (#54) | 0,31–0,60 % van één kern, zonder deze uitbreiding (#53) 0,49–0,52 %: geen verschil buiten de ruis. Vergrendeld scheelt het de ~0,05 % van het kijken in de proceslijst | 27–31 MB working set, 11–12,6 MB private (zonder: 26–31 en 11–13 MB) |
| Engine | Regenboog, met het volgen van het scherm (#55); Windows vergrendeld, het scherm niet apart gemeten; het verschil is het extra venster | Met `pauseOnLockAndSleep` uit 0,16–0,57 % van één kern, zonder deze uitbreiding (#54) 0,21–0,36 %: geen verschil buiten de ruis. Met het pauzeren aan (gepauzeerd) 0,10 tegen 0,08 % | 23–28 MB working set, 11–12 MB private (zonder: 23–28 en 11–11,6 MB) |
| Engine | Regenboog, `pausedLights` "keep", "off" en "dim" (#56); Windows vergrendeld, dus gepauzeerd; zonder pauze stuurt de engine niets anders dan eerst | "keep" 0,08–0,13 %, "off" 0,05–0,10 %, "dim" 0,05–0,10 % van één kern, zonder deze uitbreiding (#55) 0,03–0,16 %: geen verschil buiten de ruis. Dimmen of uit kost alleen één keer sturen bij het begin en eind van de pauze | 23–28 MB working set, 11–12,4 MB private (zonder: 23–28 en 11–12 MB) |
| Engine | Regenboog, `pauseOnLockAndSleep` uit, "Gemiste tijden inhalen" aan, "Nacht" om 23:00 en "Ochtend" met drie tijden (`loadAt` en twee `loadTimes`) (#57); zonder deze uitbreiding (#56) leest de engine alleen `loadAt` | 0,18–0,78 % van één kern, zonder deze uitbreiding 0,34–0,55 %: geen verschil buiten de ruis; de timer rekent alleen bij het plannen een paar tijden meer uit | 27–28,4 MB working set, 11–12,4 MB private (zonder: 27–28,2 en 11–12,3 MB) |
| Engine | Regenboog, `pauseOnLockAndSleep` uit, één scène met een programma (#58): zonder vinkje (pollen) en met `foregroundOnly` (de hook, geen pollen; zonder deze uitbreiding pollen); Windows vergrendeld, dus geen pollen en geen wisselingen van de voorgrond | Zonder vinkje 0,29–0,60 % van één kern, zonder deze uitbreiding (#57) 0,26–0,42 %; met `foregroundOnly` 0,29–0,36 % tegen 0,31–0,44 %: geen verschil buiten de ruis. Een wisseling van de voorgrond kost alleen iets als je wisselt | Zonder vinkje 25–27 MB working set, 11–13 MB private (zonder deze uitbreiding 25–28 en 11–12,7 MB); met `foregroundOnly` 25,5–26,7 en 10,9–12,3 MB (zonder: 25,2–26,4 en 11,1–12,8 MB) |
| Engine | Regenboog, `pauseOnLockAndSleep` uit, Windows vergrendeld (#??): zonder app, en met een client die elke 2 s het moederbord laat knipperen (`FlashDeviceAsync`) of zijn eerste zone (`FlashZoneAsync`), zonder beelden | Zonder app 0,23–0,47 % van één kern, zonder deze uitbreiding (#58) 0,13–0,34 %; het apparaat knipperen 0,86 % tegen 0,73–1,04 %; een zone knipperen 0,94–1,01 %: geen verschil buiten de ruis. Knipperen kost alleen iets zolang het duurt (frames op 30 fps) | Zonder app 25–26,5 MB working set, 10,9–12 MB private (zonder: 25–26,3 en 11–12,7 MB); met knipperen 37–48 MB working set, 12,7–18,3 MB private, voor apparaat en zone gelijk aan #58 (37–44,5 en 12,7–18,4 MB) |
| App (Release, zichtbaar) | Op Lampen, regenboog | ~5,0 % van één kern (was 10,2 % vóór de begrenzing van het voorbeeld) | 166 MB working set, 83 MB private |
| App (Release, zichtbaar) | Op Kleur met "Hele huis", regenboog | ~3,2 % van één kern (was 3,8 %) | 179 MB working set, 88 MB private |
| App (Release, zichtbaar) | Op Scènes, of geminimaliseerd, regenboog | 0,0–0,2 % (was 1,7 en 0,6 %): geen beelden | 182–191 MB working set, 89–97 MB private |
| App (Debug, Windows vergrendeld) | Lampen of Kleur, regenboog | ~2,1–2,4 % van één kern | 113–133 MB working set, 39–43 MB private |

De engine is gemeten in Release met de gesimuleerde lampen en zonder OpenRGB (dan probeert hij
elke 5 seconden te verbinden; dat is het grootste deel van de CPU bij een statische kleur).
Meet allocaties met de app dicht, bijvoorbeeld met
`dotnet tool install -g dotnet-counters` en daarna
`dotnet-counters collect -p <pid van de engine> --counters System.Runtime --refresh-interval 60 --duration 00:05:00 --format csv`.
Vergelijk `dotnet.gc.heap.total_allocated` met een animatie en met een statische kleur: de teller
loopt in stappen van ongeveer 8 KB, dus een minuut per meetpunt zegt meer dan 5 seconden. De app is gemeten met 12 gesimuleerde lampen, 30 s per pagina,
in een venster van 1240 × 820; met Windows vergrendeld tekent WPF niets, dus die oudere meting
mist het tekenen zelf (ook van de gloed). Met een zichtbaar venster zit de app boven het budget
van 150 MB working set; minder beelden scheelt CPU, maar geen geheugen.

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
3. Kies een statische kleur, verander de helderheid en kies "Regenboog".
4. Zet de pc in slaapstand, maak hem wakker en wacht 10 seconden: de kleuren horen terug te
   zijn.
5. Noteer in `docs/hardware.md` wat werkt, en bij een videokaart of hij een "Direct"-modus
   heeft.

### OpenRGB-status en gesimuleerde lampen

1. Start de engine en de app met de OpenRGB-service uit: "niet bereikbaar" met de tip, en de
   gesimuleerde lampen staan er. Start de service: binnen 5 seconden staat er "verbonden" met
   het aantal lampen, verschijnen de echte en verdwijnen de gesimuleerde. Stop de service: de
   gesimuleerde komen terug. Kijk of de tooltip van het tray-icoon hetzelfde zegt.
2. Kies "Altijd" en "Nooit" en kijk of de lampen meteen meeveranderen; herstart de engine en
   kijk of de keuze bewaard is.
3. Heeft een apparaat geen "Direct"-modus, kijk dan of het de waarschuwing krijgt, en noteer
   of het de kleur toch volgt.

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
5. Noteer welke controller het is, hoeveel LED's er per kanaal zitten, of het type klopt, waar
   LED 1 van een ring zit en of OpenRGB de lengte na een herstart vergat.

### Toetsenbord, muis en headset

1. Klik in het voorbeeld op bijv. Esc, W, A, S, D en de spatiebalk, geef ze een kleur en
   controleer dat precies die toetsen oplichten. Kijk of de tooltip de goede naam geeft en of
   de brede toetsen niet over een andere toets heen lopen.
2. Geef bij de muis één zone een eigen kleur en controleer dat alleen die zone verandert.
3. Staat de headset niet in OpenRGB, noteer dan het model.

### Knipperen en plek

1. Open elke lamp en kijk of precies dat apparaat knippert en daarna zijn eigen kleur
   terugkrijgt, ook bij een bewegend effect.
2. Pauzeer de effecten in het tray-menu en open een lamp: er hoort niets te knipperen.
3. Kijk of de RAM-sticks (en eventueel twee dezelfde videokaarten) een verschillend adres of
   een verschillende bus tonen, en zoek met "Laat knipperen" uit welke welke is. Noteer wat
   OpenRGB als locatie geeft (tooltip).
4. Zet bij een ARGB-header met 0 LED's het aantal LED's en controleer met "Laat knipperen" dat
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

1. Zet een lamp uit en weer aan, en alles uit en aan; kijk of de lampen echt zwart worden en
   daarna hun eigen kleur en effect terugkrijgen.
2. Zet alles uit, herstart de engine en kijk of ze uit blijven. Kijk of een apparaat zonder
   "Direct"-modus ook uit gaat.
3. Sluit de instellingen-app, klik in een ander programma (bijv. de browser of een spel in
   venstermodus) en druk Ctrl+Alt+Shift+U en daarna Ctrl+Alt+Shift+A: alle lampen gaan uit en
   weer aan, zoals met "Alles uit" en "Alles aan" in het tray-menu. Kijk of de toetsen in het
   menu naast die items staan. Probeer het ook in een spel op volledig scherm.
4. Zet bij Instellingen "Sneltoetsen voor alles aan en uit" uit: de toetsen doen niets meer en
   het programma met de focus krijgt ze weer. Herstart de engine: ze blijven uit. Zet ze weer
   aan zonder herstart: ze werken meteen.
5. Gebruikt een andere app (bijv. een overlay of macro-tool) een van de toetsen, start dan de
   engine: het icoon meldt welke toets niet werkt en `engine.log` noemt hem; de andere toets
   werkt. Noteer welke app het was.
6. Zet een lamp uit, kies hem bij Kleur en zet er een andere kleur op: hij gaat aan, meteen in
   de nieuwe kleur, zonder eerst even de oude te tonen. Doe hetzelfde met één LED in het
   voorbeeld.

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
   van vóór deze uitbreiding (met alleen `loadAt` en `loadOn`) werkt zonder iets te doen.
8. Geef "Film" een programma dat je open laat staan (bijv. `vlc.exe` of `notepad.exe`) met het
   vinkje "Alleen als het op de voorgrond staat", en "Gamen" je spel zonder vinkje. Klik op het
   programma en wissel met Alt+Tab weg en terug: de scène hoort meteen aan te gaan en de
   verlichting van daarvoor meteen terug te komen. Kijk dat de taakbalk, het Alt+Tab-venster
   zelf, het startmenu (Windows-toets, dan Esc), het overloopvak van het systeemvak (het pijltje
   naast de klok) en het tray-menu van BuurmansRgb de scène laten staan. Geef een tweede programma ook het vinkje en wissel tussen de twee: de lampen horen
   rechtstreeks van de ene scène naar de andere te gaan, zonder flits van de verlichting van
   daarvoor. Start het spel terwijl "Film" vooraan staat, wissel naar het spel en weer terug.
   Vergrendel met Win+L terwijl "Film" vooraan staat: de scène blijft; ontgrendel en wissel weg.
   Sluit het programma terwijl het vooraan staat, wacht een paar seconden en open het weer via
   het startmenu of de taakbalk: de scène hoort weer aan te gaan. Een spel in volledig scherm (exclusive
   fullscreen) en een programma dat als administrator draait, horen ook te tellen.

### Pauzeren

1. Kies "Regenboog" en vergrendel Windows (Windows-toets+L): de lampen blijven op één frame
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
   toetsenbord tot het scherm uitgaat: de lampen blijven op één frame staan en de CPU-tijd
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
10. Kies bij Instellingen "Lampen tijdens deze pauze" "Uit" en "Regenboog", en vergrendel: alle
    lampen gaan uit. Ontgrendel: de regenboog loopt weer op alle lampen. Doe hetzelfde met
    "Dimmen (20 %)": de lampen houden het laatste beeld op ongeveer een vijfde van de helderheid.
11. Laat "Uit" staan, zet de pc in slaapstand en kijk of lampen die stroom houden (bijv. van een
    voeding met standby-stroom of een USB-controller) uitgaan vóórdat de pc slaapt. Maak hem
    wakker: op het vergrendelscherm blijven de lampen uit, ook na de 10 seconden van het
    herstellen; na het ontgrendelen komen de kleuren en de regenboog terug, ook op apparaten die
    na de slaap hun eigen effect lieten zien.
12. Laat het scherm uitgaan zonder vergrendelen (zoals bij 7): de lampen gaan uit; beweeg de
    muis: de regenboog komt meteen terug.
13. Pauzeer in het tray-menu en vergrendel: de lampen blijven zoals ze waren. Ontgrendel en
    hervat: de regenboog loopt weer.
14. Wissel met "Uit" van gebruiker naar iemand met een eigen engine: de lampen krijgen na het
    wisselen de kleuren van de tweede gebruiker en niet het zwart van de eerste.

## Mappen en documenten

- `src/` – de projecten; `tests/` – de tests, zonder hardware
- `installer/` – `build.ps1`, `BuurmansRgb.iss`, de Sandbox-test en `THIRD-PARTY-NOTICES.txt`
- `design/` – het ontwerppakket van de instellingen-app (`DESIGN.md`, icoon, referentieschermen)
- `docs/images/` – de schermafbeeldingen in `README.md`, gemaakt met de gesimuleerde lampen
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
