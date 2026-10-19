# Ideeën voor later

- Profielen per moment (gamen, werken, nacht)
- Alle apparaten synchroon laten lopen
- Eigen HID-providers voor apparaten die OpenRGB niet kent
- `BuurmansRgb.UI.exe` ook in de publish-map van de engine zetten; nu kopieert alleen
  `dotnet build` hem naast de engine
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
  Vergelijk daarom private bytes. Trimmen direct na opstarten geeft dezelfde schijnwinst;
  echte ruimte voor taak 3 (OpenRGB.NET) zit in minder geladen assemblies
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
