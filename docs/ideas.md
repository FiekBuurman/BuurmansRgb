# Ideeën voor later

- Profielen per moment (gamen, werken, nacht)
- Alle apparaten synchroon laten lopen
- Eigen HID-providers voor apparaten die OpenRGB niet kent
- Zorgen dat `BuurmansRgb.UI.exe` naast de engine in de build- en publish-map komt
  (taak 2), zodat "Instellingen openen" ook vanuit `dotnet run` werkt
- Engine netjes laten afsluiten vanaf de commandline (bijv. `--exit` via het
  activatiesignaal), zodat scripts hem niet hoeven te killen en er geen spook-icoon blijft
- Autostart-pad bijwerken als de engine verplaatst is (nu toont het menu dan "uit")
- `InvariantGlobalization=true` staat andere UI-culturen niet toe; bij vertalen van de
  engine-teksten opnieuw afwegen
- Wijzigingen in `settings.json` live oppikken (FileSystemWatcher) in plaats van bij herstart
- Logging naar bestand (`Microsoft.Extensions.Logging`) voor fouten bij opstarten
- Geheugen van de engine bewaken: het budget in `docs/bouwplan.md` is working set
  (< 50 MB) en taak 1 zit daar met ~48 MB vlak onder. Bij taak 2 (StreamJsonRpc) en
  taak 3 (OpenRGB.NET) meten met `dotnet-counters`, en eventueel na opstarten
  `GC.Collect` plus `EmptyWorkingSet` proberen
