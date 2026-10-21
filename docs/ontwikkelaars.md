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
| Knipperen en plek | `LightingControllerFlashTests`, `DeviceInfoTests`, `DeviceLocationLabelsTests` |
| Aan en uit | `LightingControllerOnTests`, `LampsViewModelTests`, `MainViewModelLampTests` |
| Scènes | `ProfileLibraryTests`, `EngineHostTests` en `MainViewModelSceneTests` (ook welke scène aan staat, ook na een herstart), `JsonProfileStoreTests`, `DailyTimeTests` (ook op gekozen dagen), `ProfileTimerTests` (ook gemiste tijden inhalen na het ontwaken en bij het starten, met de echte `ResumeRestorer` op een nepklok; bij het starten terwijl een spel draait ook in `EngineHostTests`, met de echte `ProgramWatcher`), `ProfileTriggersTests`, `ProgramWatcherTests`, `ProcessListTests`, `ProfilesViewModelSceneTests`, `SceneCardTests`, `ProfileAutomationViewModelTests` en `WindowedProgramsTests` (een programma kiezen) |
| Pauzeren en slaapstand | `LightingControllerPauseTests`, `LightingControllerRestoreTests`, `AutoPauseTests`, `ResumeRestorerTests`, `SystemSessionEventsTests`, `EffectsPauseViewModelTests` |
| Sneltoetsen | `TrayPresenterTests` (een toets doet wat "Alles aan" of "Alles uit" doet, aan en uit zetten, een bezette toets; met `FakeGlobalHotkeys`), `AllOnOffHotkeysTests`, `WindowsGlobalHotkeysTests` (de echte `RegisterHotKey` op Ctrl+Alt+Shift+F24, die op geen toetsenbord zit: twee keer registreren faalt en wordt gelogd), `EngineHostTests` en `JsonSettingsStoreTests` (`hotkeys`), `EngineApiTests`, `HotkeysViewModelTests` |
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
- Kies "Gesimuleerde ventilator met twee ringen" en klik op "Outer ring"; kies "Gesimuleerd
  toetsenbord", wijs een toets aan voor zijn naam en klik op het brede deel van de spatiebalk.
- Zet twee scènes in elkaar en kies er een in het tray-menu; geef een scène een tijd een minuut
  verder, of `notepad.exe`, en start en sluit Kladblok.
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
  de app leest bij die laatste twee de scènes of de opties opnieuw). Na het opstarten en na het
  sluiten van de laatste instellingen-app geeft de engine zijn ongebruikte geheugen terug aan Windows
  (`MemoryTrimmer`), en ook nadat het tray-menu dicht ging.
- **Pauzeren:** via `SystemEvents.SessionSwitch` en `SystemEvents.PowerModeChanged` op de thread
  van het tray-icoon (`AutoPause`, `SystemSessionEvents`, `SystemPowerEvents`). Bij het starten
  vraagt de engine één keer of Windows al vergrendeld is (`WTSQuerySessionInformation`).
- **Programma's zien:** zonder adminrechten meldt Windows niet dat een programma start. Daarom
  kijkt de engine elke 2 seconden in de proceslijst (`EnumProcesses`, de naam één keer per
  nieuw proces), alleen zolang een scène een programma heeft. Dat kost minder dan 0,1 ms per
  ronde, waar `Process.GetProcessesByName` of een Toolhelp-snapshot 5 tot 7 ms kosten en WMI
  `System.Management` in de engine haalt. Programma's die als administrator draaien, ziet hij
  ook. Tijdens een pauze om vergrendelen of slaapstand (`PauseReasons.Locked` of `Asleep`) staat
  de timer stil; als die pauze eindigt, kijkt hij meteen.
- **Welke scène aan staat:** `ProfileLibrary.ActiveName`, gezet door `Load`, `Save` en `Show` (waarmee
  `ProgramWatcher` na een spel ook de scène van daarvoor terugzet) en gewist door elke andere
  `LightingChanged`; de app vraagt het met `IEngineApi.GetActiveProfileAsync`. Bij elke wissel
  (`ActiveChanged`) bewaart `EngineHost` de naam als `activeScene` in `settings.json`, en na het
  starten zet `RestoreActive` hem terug als die scène nog bestaat.
- **Scènes op tijd:** `ProfileTimer` zet één timer op de eerstvolgende tijd. Gaat die meer dan
  `LateLimit` (een minuut) te laat af of springt de klok erover (`IClockEvents`, ook bij het
  ontwaken), dan slaat hij die tijd over. Met `CatchUp` (`catchUpSceneTimes`, standaard uit)
  laadt hij de scène waarvan `DailyTime.Previous` het laatst voorbij is: bij het starten via
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
  `DeviceFlash`.

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
| Engine | Vergrendeld of slaapstand | 0 frames, geen timer | |
| Engine | Een scène met een programma | ~0,05 % van één kern extra (~100 miljoen CPU-cycli per minuut) | ~0,3 MB voor de procesnamen |
| Engine | Tien scènes met een tijd en eigen kleuren | | ~51 KB op de heap |
| Engine | Regenboog, "Gemiste tijden inhalen" aan met twee scènes met een tijd (#51) | 0,49–0,75 % van één kern, main zonder deze uitbreiding 0,65–0,73 %: geen verschil buiten de ruis | 27–30 MB working set, 11–12 MB private (main 25–29 en 11–12 MB) |
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
5. Zet bij Instellingen "Pauzeren bij vergrendelen en slaapstand" uit en vergrendel: de
   regenboog loopt door.

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
