# OpenRGB meeleveren – uitkomst van de spike

> **Stand:** optie B is gekozen en gebouwd in taak 9 (PR #16). De installer staat in
> [`installer/`](../installer/): `build.ps1` bouwt hem, `BuurmansRgb.iss` is het Inno
> Setup-script en `sandbox/test-install.ps1` test hem in Windows Sandbox. Bouwen en testen staat
> in [ontwikkelaars.md](ontwikkelaars.md) onder "Installer bouwen en testen", installeren in
> [installatie.md](installatie.md). Hieronder het oorspronkelijke onderzoek; afwijkingen van het
> voorstel staan bij "Zo is het gebouwd".

Doel: één installatie, zodat je OpenRGB niet meer apart hoeft te installeren en in te stellen.
De spike is uitgevoerd op 20 oktober 2026 met **OpenRGB 1.1** (16 oktober 2026). Er is niets
systeemwijd geïnstalleerd: de zip is lokaal gestart, de MSI alleen gelezen.

## Conclusie

**Optie B werkt.** De officiële MSI van OpenRGB kan stil installeren, met de achtergrondservice
aan. Daarmee wordt BuurmansRgb één `setup.exe` die BuurmansRgb, OpenRGB (als service) en de
driver PawnIO in één keer neerzet, met één UAC-vraag. De engine hoeft daarvoor niet te
veranderen: de service luistert op `127.0.0.1:6742`, waar de engine nu al mee verbindt.

## Wat de spike heeft vastgesteld

### OpenRGB 1.1

- **Downloads:** `OpenRGB_1.1_Windows_64_8e16de2.msi` (22,3 MB) en `.zip` (21,0 MB) op
  [Codeberg](https://codeberg.org/OpenRGB/OpenRGB/releases). Binaries en MSI zijn **niet
  ondertekend**.
- **CLI** (uit de help-tekst in `OpenRGB.exe`): `--server`, `--server-host` (standaard
  `127.0.0.1`), `--server-port` (standaard 6742), `--gui`, `--startminimized`, `--nodetect`,
  `--noautoconnect`, `--localconfig`, `--config <pad>`, `--profile <naam>`, `--loglevel`.
- **`--server` zonder `--gui` draait zonder venster**: geen venster, ongeveer 23 MB geheugen.
- **De service zit in `OpenRGB.exe` zelf** (`StartServiceCtrlDispatcher`). Er zijn
  instellingen voor een profiel bij het starten en stoppen van de service.
- **`--localconfig` is niet waterdicht:** OpenRGB maakte toch `%AppData%\OpenRGB` aan, met een
  log en `OpenRGB.json`.

### De MSI (alleen gelezen, niet geïnstalleerd)

| Tabel | Inhoud |
| --- | --- |
| Feature `OpenRGBApplication` | De programmabestanden, standaard aan (Level 1) |
| Feature `OpenRGBRegisterService` | "Register OpenRGB as a system service", **standaard uit** (Level 3 tegen `INSTALLLEVEL` 2) |
| ServiceInstall | Service `OpenRGB`, "OpenRGB SDK Server", eigen proces, **start automatisch**, account `[SERVICEACCOUNT]` (leeg = LocalSystem), geen argumenten |
| ServiceControl | Events 163: (her)starten bij installeren, stoppen en verwijderen bij deïnstalleren |
| Shortcut | Startmenu-snelkoppeling naar de OpenRGB-GUI |

Stil installeren met de service:

```powershell
msiexec /i OpenRGB_1.1_Windows_64_8e16de2.msi /qn /norestart ADDLOCAL=OpenRGBApplication,OpenRGBRegisterService
```

### Verbinding vanuit BuurmansRgb

- OpenRGB 1.1 met `--server --server-port 6743 --nodetect` gestart; **OpenRGB.NET 3.1.1
  verbindt** en onderhandelt **protocol 4**, het maximum van de client. Zonder detectie kwamen
  er 0 apparaten binnen, zoals verwacht.
- Protocol 6 (vaste apparaat-ID's van OpenRGB) gebruiken we dus niet. Dat hoeft ook niet: de
  engine maakt al zelf vaste ID's uit locatie en serienummer.

### PawnIO

- PawnIO 2.2.0, `PawnIO_setup.exe` (3,3 MB), van [GitHub](https://github.com/namazso/PawnIO.Setup/releases)
  of winget `namazso.PawnIO`.
- **Stil:** `PawnIO_setup.exe -install -silent`. Exitcode 0 = gelukt, 3010 = gelukt maar
  herstart nodig. Machinebreed, vraagt zelf om adminrechten.
- Licentie: "Proprietary (Freeware)". Meeleveren voor eigen gebruik is geen probleem. Wil je
  de installer ooit delen, download hem dan tijdens de installatie en controleer de SHA-256
  (2.2.0: `1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032`).
- De OpenRGB-zip bevat de PawnIO-modules (`Smbus*.bin`) en `PawnIOLib.dll`, maar **niet de
  driver**. Zonder PawnIO werken moederbord, RAM en (vaak) GPU niet.
- **Naam van de driver (voor "Herstarten"):** vermoedelijk `PawnIO`, maar dat is een vermoeden
  tot het op een echte pc met PawnIO is bevestigd (`sc query type= driver | findstr /i pawn`,
  zie "Herstarten" in `ontwikkelaars.md`). Daarom zoekt `ServiceRestarter` eerst de service
  `PawnIO` en anders de eerste kerneldriver met "PawnIO" in de naam. De bronnen:
  - LibreHardwareMonitor herkent PawnIO aan de Uninstall-sleutel
    `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO` en opent het apparaat
    `\\?\GLOBALROOT\Device\PawnIO`
    ([PawnIo.cs](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LibreHardwareMonitorLib/PawnIo/PawnIo.cs)).
  - FanControl spreekt van de "PawnIO service", zonder de precieze naam
    ([issue 3683](https://github.com/Rem0o/FanControl.Releases/issues/3683)).
  - winget kent het pakket `namazso.PawnIO` (2.2.0); de repository
    [namazso/PawnIO.Setup](https://github.com/namazso/PawnIO.Setup) bevat alleen releases, dus
    de naam staat niet in een bron van de setup.

## Voorstel voor de installer

Inno Setup 6, één `BuurmansRgb-setup.exe`, `PrivilegesRequired=admin`:

1. **BuurmansRgb:** engine en instellingen-app `self-contained` publiceren naar
   `{autopf}\BuurmansRgb`, dan is ook geen .NET-runtime nodig.
2. **PawnIO:** `PawnIO_setup.exe -install -silent`; 0 en 3010 tellen als gelukt (bij 3010 aan
   het eind om een herstart vragen).
3. **OpenRGB:** bestaat de service `OpenRGB` al, dan overslaan. Anders de `msiexec`-regel
   hierboven.
4. **Autostart:** de engine draait als gewone gebruiker, dus niet als service. Laat de
   installer de engine aan het eind starten met bijv. `--first-run`, zodat de engine zelf
   "Starten met Windows" aanzet in de `HKCU`-sleutel van de ingelogde gebruiker. Een elevated
   installer schrijft anders mogelijk in de `HKCU` van een ander (admin)account.
5. **Verwijderen:** engine stoppen, bestanden weg, en vragen of OpenRGB
   (`msiexec /x {ProductCode}`) en PawnIO ook weg moeten.

In de engine verandert alleen: `--first-run` afhandelen en gesimuleerde apparaten standaard
uit zodra OpenRGB apparaten levert.

## Zo is het gebouwd

Het voorstel hierboven is gevolgd, met deze verschillen en aanvullingen:

- **Inno Setup 7.1** in plaats van 6; `build.ps1` haalt de compiler van NuGet.
- **Downloaden bij het bouwen, niet bij het installeren:** `build.ps1` haalt de MSI van
  OpenRGB en de setup van PawnIO op, controleert hun SHA-256 en zet ze in de installer. Voor
  eigen gebruik is dat genoeg; zie de licentie van PawnIO hierboven als je de installer wilt
  delen.
- **Sinds #35 staat de installer als download bij GitHub Releases.** Zolang de repository privé
  is, is dat delen met wie toegang heeft; wordt hij openbaar, kijk dan eerst opnieuw naar de
  licentie van PawnIO en naar ondertekenen.
- **PawnIO** wordt ook overgeslagen als het al geïnstalleerd is. De installer onthoudt in
  `HKLM\Software\BuurmansRgb` of hij OpenRGB en PawnIO zelf installeerde; alleen dan vraagt
  verwijderen of ze ook weg moeten.
- **Gesimuleerde apparaten** staan standaard op "Automatisch": ze verdwijnen zodra OpenRGB
  echte apparaten levert (PR #21).
- **Licentie OpenRGB:** GPL-2.0, als los programma meegeleverd. De licentie en de link naar de
  broncode staan in `installer/THIRD-PARTY-NOTICES.txt`, dat naast BuurmansRgb wordt
  geïnstalleerd.
- **De OpenRGB-GUI naast de service:** de GUI start als client en verbindt met de service.
  Twee servers op 6742 kan niet, dus de installer maakt geen eigen autostart voor de GUI aan.

## Open punten en risico's

- **Getest: de echte service-installatie.** Op 20-10-2026 heeft de gebruiker de installer op
  de eigen pc gedraaid: de service `OpenRGB` draait, BuurmansRgb verbindt en verwijderen werkt
  (de criteria van taak 9 in `docs/bouwplan.md`). De Windows Sandbox-test (`installer/sandbox`)
  controleert hetzelfde zonder iets op je eigen pc te installeren.
- **Waar de service zijn config bewaart:** waarschijnlijk in het profiel van LocalSystem. Dat
  is niet bevestigd. Profielen of zonegroottes die je in de OpenRGB-GUI instelt, komen
  terecht bij de service, omdat de GUI als client met de service verbindt. Dat moet nog
  getest worden.
- **Niet ondertekend:** SmartScreen en Defender waarschuwen bij de installer en bij OpenRGB
  ("Meer info → Toch uitvoeren"). Voor eigen gebruik prima; delen vraagt een
  code-signing-certificaat (zie `docs/ideas.md`).
- **Bijwerken van OpenRGB:** een nieuwere MSI over de oude heen zou moeten werken (zelfde
  `UpgradeCode`), maar dat is niet getest. De installer slaat OpenRGB nu over als de service
  al bestaat, dus een nieuwere OpenRGB komt er alleen bij een nieuwe installatie in.
