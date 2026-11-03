# Installeren – TL;DR

## Downloaden (aanbevolen)

Download `BuurmansRgb-setup.exe` bij de
[nieuwste release](https://github.com/FiekBuurman/BuurmansRgb/releases/latest) en ga verder bij
stap 2 hieronder. Wil je controleren of het bestand goed is aangekomen, vergelijk dan de SHA-256
(in PowerShell: `Get-FileHash .\BuurmansRgb-setup.exe`) met die in de release.

## Met de installer die je zelf bouwt

1. Bouw de installer, of pak een `BuurmansRgb-setup.exe` die je eerder bouwde:

   ```powershell
   git clone https://github.com/FiekBuurman/BuurmansRgb.git
   cd BuurmansRgb
   .\installer\build.ps1 -Version 1.2.0
   ```

   Nodig: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), Git
   en internet (het script downloadt OpenRGB, PawnIO en Inno Setup en controleert ze, en
   `dotnet publish` haalt de eerste keer de runtime van .NET voor win-x64 van nuget.org, die de
   installer meelevert, en `microsoft.netcore.app.crossgen2.win-x64`, dat de instellingen-app
   vooraf compileert). Met
   `-Version` geef je de versie mee; zonder is het 1.0.0. De installer staat daarna in
   `artifacts\installer\out\BuurmansRgb-setup.exe`.

   Weigert PowerShell het script ("running scripts is disabled"), start het dan zo:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\installer\build.ps1 -Version 1.2.0
   ```

2. Sluit andere RGB-software af (SignalRGB, iCUE, Synapse, Armoury Crate, Mystic Light,
   RGB Fusion, G Hub, ...); zie [Andere RGB-software afsluiten](#andere-rgb-software-afsluiten).
3. Draai `BuurmansRgb-setup.exe` en geef één keer toestemming (UAC). SmartScreen kan
   waarschuwen omdat de installer niet ondertekend is: **Meer info → Toch uitvoeren**.

De installer zet in één keer neer:

- **BuurmansRgb** in `C:\Program Files\BuurmansRgb`, met alles erin (geen losse .NET nodig);
- de driver **PawnIO**, voor RAM en oudere moederborden (overgeslagen als die er al is);
- **OpenRGB 1.1** als Windows-service, met de SDK-server op `127.0.0.1:6742` (overgeslagen
  als de service `OpenRGB` er al is).

Na afloop start BuurmansRgb met een tray-icoon rechtsonder bij de klok, en staat
**Starten met Windows** aan. Dubbelklik op het icoon opent de instellingen. Vraagt de
installer om een herstart, dan heeft PawnIO of OpenRGB die nodig.

Zodra OpenRGB echte apparaten levert, verdwijnen de gesimuleerde apparaten vanzelf; ze komen ook
niet terug als OpenRGB later wegvalt, zolang deze pc ooit een echt apparaat had (zie "Instellingen"
in de [handleiding](handleiding.md#instellingen)). Wil je ze nooit zien, kies dan in de
instellingen-app bij **Instellingen → Gesimuleerde apparaten** voor "Nooit".

## Wat er altijd draait

BuurmansRgb zelf is een gewone app: geen service, geen driver, geen beheerdersrechten. Om bij de
apparaten te komen zet de installer er twee dingen naast, ook als je BuurmansRgb afsluit of
**Starten met Windows** uit staat: de service OpenRGB, die altijd draait, en de driver PawnIO, die
er altijd staat en waarschijnlijk vanaf het opstarten geladen is, maar dat is nog niet bevestigd
(zie "Starttype" hieronder). De bronnen staan als [n] onder [Bronnen](#bronnen).

### De service OpenRGB

- De MSI van OpenRGB 1.1 (`ADDLOCAL=OpenRGBApplication,OpenRGBRegisterService`) maakt de service
  `OpenRGB` ("OpenRGB SDK Server") aan: **starttype automatisch**, account **LocalSystem**, een
  eigen proces en **geen argumenten** (uit de MSI gelezen, zie
  [openrgb-meeleveren.md](openrgb-meeleveren.md)). Hij start dus bij elke start van Windows mee,
  al vóór iemand zich aanmeldt.
- LocalSystem is nodig omdat OpenRGB via de driver PawnIO bij moederbord en RAM komt, en die
  driver alleen voor beheerders open staat [7].
- Staat er al een service `OpenRGB`, bijvoorbeeld van een eigen OpenRGB-installatie, dan slaat
  de installer OpenRGB over. Versie, account en instellingen zijn dan die van die installatie.
- Tijdelijk uit (in PowerShell als administrator): `Stop-Service OpenRGB`; niet meer
  automatisch starten: `Set-Service OpenRGB -StartupType Manual`. BuurmansRgb toont dan
  "Geen verbinding met je apparaten" tot de service weer draait.

### De SDK-server op 127.0.0.1:6742

- De service is de SDK-server van OpenRGB; BuurmansRgb verbindt ermee op `127.0.0.1:6742`.
  6742 is de standaardpoort [30].
- **Het adres staat niet vast in de installer.** De service krijgt geen `--server-host` of
  `--server-port` mee, dus hij volgt de standaard van OpenRGB. Voor 1.1 is dat volgens de
  help-tekst van `OpenRGB.exe` `127.0.0.1` en 6742 (zie
  [openrgb-meeleveren.md](openrgb-meeleveren.md)); de SDK-documentatie en de releasenotes
  noemen geen standaard [30][12]. Een andere versie kan anders doen: de releasekandidaten van
  1.0 luisterden op alle adressen (`0.0.0.0`), pas de securityhotfix 1.0rc3.1 van 23 augustus
  2026 maakte alleen localhost de standaard [31][12]. Het adres expliciet meegeven staat in
  [ideas.md](ideas.md).
- Controleren: `netstat -ano | findstr :6742` moet `127.0.0.1:6742 ... LISTENING` geven. Staat
  er `0.0.0.0:6742`, dan is de server vanaf het netwerk te bereiken; werk die OpenRGB dan bij.
  Dat geldt vooral voor een eigen service `OpenRGB` die de installer liet staan.
- **Geen wachtwoord.** De SDK-server kent geen authenticatie: wie de poort bereikt, mag alles
  [31]. Een verbinding vanaf de pc zelf telt als lokale client en mag daardoor ook profielen
  opslaan en instellingen wijzigen [30]. Elk proces op deze pc, van elke gebruiker en zonder
  beheerdersrechten, kan dus de service aansturen, en die draait als LocalSystem. Wie al code
  op je pc kan draaien, kan zo je verlichting en de OpenRGB-instellingen veranderen.
- **Bekende lekken (2026).** Onderzoekers van openSUSE publiceerden op 25 augustus 2026 drie
  lekken in de SDK-server [31]: CVE-2026-59682 (een bestand overschrijven door een profiel op
  te slaan), CVE-2026-59683 (op Linux root worden via het wisselen van modus en het opslaan van
  een profiel) en CVE-2026-18794 (laten vastlopen en geheugenfouten). Ze onderzochten Linux;
  Windows is volgens hen waarschijnlijk ook kwetsbaar, maar niet onderzocht. De hotfix 1.0rc3.1
  controleert profielnamen en begrenst de grootte van een pakket [12][31]. BuurmansRgb levert
  OpenRGB 1.1 (16 oktober 2026), van na die hotfix. Of 1.1 op Windows helemaal dicht is, is
  **onbekend**. De onderzoekers raden aan de server ook na de hotfix niet op een netwerk open te
  zetten [31].

### De driver PawnIO

- **Wat:** een kerneldriver van namazso die kleine programma's in de scripttaal Pawn uitvoert,
  de modules [1]. OpenRGB gebruikt ondertekende modules voor de SMBus van Intel (i801) en AMD
  (PIIX4) om moederbord en RAM aan te sturen [8][9]. De modules en `PawnIOLib.dll` zitten bij
  OpenRGB; de driver zelf niet. Die installeert BuurmansRgb met PawnIO 2.2.0
  (`PawnIO_setup.exe -install -silent`), en slaat hem over als PawnIO er al is. Zonder PawnIO
  werken RAM en oudere moederborden niet: OpenRGB 1.0 en 1.1 hebben hem daarvoor nodig [12].
  Of een videokaart hem ook nodig heeft, is mogelijk maar niet onderzocht.
- **Licentie:** de driver is GPL-2.0 [2], de modules LGPL-2.1 [3]; de setup is freeware (zie
  [openrgb-meeleveren.md](openrgb-meeleveren.md)).
- **Ondertekend:** de "Official edition" is digitaal ondertekend [1]; sinds 2.1.0 (12 februari
  2026) met een nieuwe handtekening [5][6]. Welk soort handtekening (Microsoft-attestatie of
  WHQL) is **onbekend**.
- **Waarom niet WinRing0**, de driver die OpenRGB vroeger gebruikte: met WinRing0 kan een gewoon
  proces elk geheugen lezen en schrijven [8]. Dat is CVE-2020-14979 (WinRing0 1.2.0, CVSS 7,8
  hoog): een lokale gebruiker kan zo SYSTEM worden [13][14]. Die gegevens komen uit secundaire
  bronnen; NVD zelf is niet geraadpleegd. Defender meldt WinRing0 sinds ongeveer maart 2025 als
  `HackTool:Win32/Winring0` [10] en zet hem in quarantaine als `VulnerableDriver:WinNT/Winring0`
  [14]. PawnIO laat alleen ondertekende modules met vaste bewerkingen toe [8]. OpenRGB stapte in
  juni 2025 over [9], net als FanControl [10] en, volgens een ander project, LibreHardwareMonitor
  [11].
- **Wie erbij mag:** standaard alleen beheerders, dus hier de service OpenRGB. Sinds 2.2.0 kun
  je PawnIO ook voor gewone gebruikers openzetten, maar de maker raadt dat af: toegang tot PawnIO
  staat gelijk aan beheerdersrechten [7][17]. De installer laat dat op de standaard.
- **Starttype: onbekend.** De installer zet het niet zelf, dat doet de setup van PawnIO, en geen
  bron noemt het. PawnIO staat als PnP-apparaat onder `ROOT\PAWNIO` [17][7], wat erop wijst dat
  hij vanaf het opstarten geladen is, maar dat is niet bevestigd. Op een echte pc controleren
  met `sc qc PawnIO` (de naam `PawnIO` is zelf ook nog een vermoeden; anders
  `sc query type= driver | findstr /i pawn`, zie "Herstarten" in
  [ontwikkelaars.md](ontwikkelaars.md)).
- **Bekende problemen**, alle drie gesloten in februari-maart 2026: een blauw scherm
  (`SYSTEM_THREAD_EXCEPTION_NOT_HANDLED`) na het installeren van 2.1.0, en PawnIO dat niet te
  stoppen of te verwijderen was [18].

## Andere RGB-software afsluiten

Twee programma's die dezelfde LED's aansturen, vechten om de kleur: het flikkert of springt
terug. Sluit ze af en zet hun autostart uit (**Taakbeheer → Opstartapps**), en zet een service
op **Handmatig** of **Uitgeschakeld** (`services.msc`). SignalRGB noemt in zijn eigen lijst nog
meer programma's die botsen, zoals NZXT CAM, SteelSeries GG en Windows Dynamic Lighting [46].

De meeste namen hieronder komen van sites met procesinformatie, niet van de makers.
Controleer ze op je eigen pc (**Taakbeheer → Details**, `sc query`).

| Software | Processen en services | Bron |
| --- | --- | --- |
| SignalRGB | service `SignalRgbService` ("SignalRGB" of "VortxEngine" in `services.msc`); proces van de app **onbekend** | officieel [35] |
| Corsair iCUE | `Corsair.Service.exe` (service `CorsairService`), `CueLLAccessService.exe` (service `CorsairLLAService`) | niet officieel [36][37] |
| Razer Synapse 3 | `RazerCentralService.exe`, `Razer Synapse Service.exe`, `Razer Synapse Service Process.exe`; Chroma: `RzSDKService.exe` en `RzSDKServer.exe`. Synapse 4: **onbekend** | deels officieel [38][39][40][41] |
| ASUS Armoury Crate | `LightingService.exe` (Aura, meestal in `C:\Program Files (x86)\LightingService`), `ROGLiveService.exe`; servicenaam **onbekend** | niet officieel [27][42] |
| MSI Mystic Light / MSI Center | `LEDKeeper2.exe`, `Mystic_Light_Service.exe`, `mysticlight2_service.exe`, `LightKeeperService.exe`; servicenaam **onbekend** | niet officieel [43][44] |
| Gigabyte RGB Fusion / Control Center | **onbekend**; Gigabyte meldt zelf dat RGB Fusion 2.0 en GCC samen botsen | officieel, zonder namen [45] |
| Een tweede OpenRGB | een OpenRGB met zijn eigen SDK-server (**Start Server**, `--server`) of een eigen service | zie hieronder |

**Een tweede OpenRGB op poort 6742:** er kan maar één server op 6742 luisteren. Start een andere
OpenRGB zijn server eerder, dan krijgt de service de poort niet, en BuurmansRgb verbindt met die
andere. Kijk met `netstat -ano | findstr :6742` welk proces (PID, laatste kolom) luistert, en
vergelijk dat met de PID van de service in `sc queryex OpenRGB`. Zijn ze verschillend, sluit dan
die andere OpenRGB of haal zijn autostart weg. De OpenRGB-app openen naast de service mag wel:
die verbindt als client met de service (zie [openrgb-meeleveren.md](openrgb-meeleveren.md)).

## Games met anti-cheat

BuurmansRgb zelf laadt geen driver en heeft geen beheerdersrechten. Anti-cheat kijkt vooral naar
drivers, dus het gaat om PawnIO, en misschien om OpenRGB. Wat er bekend is, en wat niet:

| Anti-cheat | Wat bekend is |
| --- | --- |
| FACEIT | Blokkeerde PawnIO om de handtekening, volgens de maker zelf (19 september 2025); volgens gebruikers omdat hetzelfde certificaat ook voor cheats gebruikt was [6][20][5]. Opgelost met de nieuwe handtekening van 2.1.0 [6][5]; BuurmansRgb levert 2.2.0, maar slaat PawnIO over als die er al is, dus een oudere versie (vóór 2.1.0, bijv. van FanControl) blijft staan. Kijk bij **Instellingen → Apps → PawnIO** welke versie je hebt. Op 16 maart 2026 meldde een gebruiker toch nog een blokkade, mogelijk met een oude versie [21]. Een officiële uitspraak van FACEIT over PawnIO is niet gevonden. |
| Riot Vanguard (Valorant, League of Legends) | Blokkeert bij het opstarten drivers die ondertekend maar kwetsbaar zijn, met de melding "Vanguard has blocked something from loading" [23][24]. Bevestigd voor InpOut32 van FanControl en LibreHardwareMonitor (2023) [25]. PawnIO met Vanguard: **onbekend**, ook een vraag erover bleef onbeantwoord [20]. OpenRGB met Vanguard: **onbekend**. |
| Easy Anti-Cheat | PawnIO, WinRing0 en OpenRGB: **onbekend**, geen bron gevonden. Volgens een niet-officiële bron markeert EAC soms `LightingService.exe` van ASUS [27]. |
| BattlEye | PawnIO, WinRing0 en OpenRGB: **onbekend**. BattlEye meldt een blokkade als "Blocked loading of file" [28]. |
| ESEA | **Onbekend**, niets gevonden. |
| EA Javelin | **Onbekend**: ernaar gevraagd, geen antwoord [20]. |

Eén forumbericht (februari 2026) beweert dat sommige anti-cheats bannen als PawnIO aanwezig is,
zonder namen of bewijs [29]; een bevestigde ban is in deze bronnen niet gevonden.

Blokkeert een game iets, kijk dan in de melding welk bestand het is:

- Gaat het om PawnIO, dan helpt het stoppen van alleen de service OpenRGB waarschijnlijk niet,
  want de driver blijft geladen (starttype onbekend, zie hierboven). Probeer eerst de minder
  ingrijpende stap: stop OpenRGB en daarna PawnIO, in een PowerShell als administrator
  `Stop-Service OpenRGB` en `sc.exe stop PawnIO` (de naam van de driver is nog een vermoeden, zie
  "Herstarten" in [ontwikkelaars.md](ontwikkelaars.md); dezelfde volgorde als **Herstarten** in
  het tray-menu). Of de anti-cheat daarna nog klaagt, is **onbekend**: misschien kijkt hij ook
  naar drivers die er staan maar niet geladen zijn. Na het spelen start **Herstarten** in het
  tray-menu ze weer. Helpt het niet, verwijder dan PawnIO via
  **Instellingen → Apps → PawnIO**. RAM en oudere moederborden werken dan niet meer in
  BuurmansRgb [12], mogelijk ook een videokaart (niet onderzocht); andere apparaten (USB) wel.
  Andere programma's zoals FanControl kunnen PawnIO ook gebruiken.
- Gaat het om OpenRGB, stop dan de service tijdens het spelen (`Stop-Service OpenRGB` als
  administrator) en start hem daarna weer, of kies **Herstarten** in het tray-menu.

## Bijwerken

Maak eerst een reservekopie van `%AppData%\BuurmansRgb` (je instellingen en scènes:
`settings.json` en `profiles.json`). Download dan de setup van de
[nieuwste release](https://github.com/FiekBuurman/BuurmansRgb/releases/latest) en draai die over
de oude heen. Of haal de nieuwe code op en bouw en draai de nieuwe installer zelf:

```powershell
git pull
.\installer\build.ps1 -Version 1.2.0
```

Daarna `artifacts\installer\out\BuurmansRgb-setup.exe` draaien. De installer stopt de
draaiende engine en instellingen-app zelf en laat je instellingen staan. Een OpenRGB-service
en PawnIO die er al staan, laat hij met rust. **Starten met Windows** blijft zoals jij het
zette: had je het uitgezet, dan zet de update het niet weer aan. Alleen als je het nog nooit
zelf aan- of uitzette, zet de installer het aan.

## Verwijderen

Via **Instellingen → Apps → BuurmansRgb → Verwijderen**. Dat stopt de engine en haalt
BuurmansRgb en de autostart weg. Daarna vraagt het of OpenRGB en PawnIO ook weg moeten (alleen
als deze installer ze heeft geïnstalleerd), en of je instellingen weg moeten. Houd je je
instellingen en had je **Starten met Windows** ooit zelf aan- of uitgezet, dan zet een nieuwe
installatie het niet aan; zet het dan zelf aan in het tray-menu.

Het verwijderen geldt voor jou, de gebruiker die het startte, ook als een andere beheerder de
UAC-vraag met zijn wachtwoord beantwoordt:

- De engine krijgt eerst het verzoek om af te sluiten, zoals **Afsluiten** in het tray-menu, zodat
  je laatste wijziging bewaard is. Is hij na 10 seconden nog niet weg, dan stopt het verwijderen
  hem alsnog. De instellingen-app wordt gesloten.
- Alleen jouw engine, instellingen-app, autostart en (als je "Ja" zegt) instellingen in
  `%AppData%\BuurmansRgb`. Die van andere gebruikers op de pc blijven staan, ook van de beheerder
  die de UAC-vraag beantwoordde.

Beperkingen:

- Beantwoordt een **andere beheerder** de UAC-vraag, dan bereikt het verzoek om af te sluiten je
  engine niet (Windows laat het signaal van jouw engine alleen voor jou open) en stopt het
  verwijderen hem meteen. Een wijziging van de laatste seconde kan dan verloren gaan. Wil je dat
  niet, sluit de engine dan eerst zelf af via **Afsluiten** in het tray-menu.
- Beantwoordt een **andere beheerder** de UAC-vraag en staat je `AppData`-map niet in je
  profielmap (`C:\Users\<naam>`), bijvoorbeeld omdat hij naar een andere schijf is omgeleid, dan
  laat het verwijderen je instellingen staan. Haal ze dan weg zoals hieronder.
- Draait een andere gebruiker op de pc BuurmansRgb nog (na wisselen van gebruiker), dan sluit het
  verwijderen die engine niet. De programmabestanden die de engine gebruikt, blijven dan staan in
  `C:\Program Files\BuurmansRgb` (het verwijderen meldt "Enkele elementen konden niet verwijderd
  worden"), en de autostart van die gebruiker start BuurmansRgb bij zijn volgende aanmelding
  gewoon weer. Meld die gebruiker daarom eerst af (**Taakbeheer → Gebruikers**, rechtermuisknop
  op de gebruiker → **Afmelden**), of laat hem de engine afsluiten via **Afsluiten** in het
  tray-menu, en verwijder daarna. Bleven er al bestanden staan, meld die gebruiker dan alsnog
  af en haal de map `C:\Program Files\BuurmansRgb` zelf weg (Verkenner vraagt om
  beheerdersrechten); staat BuurmansRgb nog bij **Apps**, dan kun je ook opnieuw verwijderen.
- Kan het verwijderen niet vaststellen wie het startte (in het verwijderlog, `Uninstall Log
  <datum> #001.txt` in `%TEMP%` van wie de UAC-vraag beantwoordde, staat dan geen regel
  `Uninstall started by`), dan laat het de autostart en instellingen van iedereen staan en stopt
  het alleen de apps van wie de UAC-vraag beantwoordde (de engine ook dan eerst met het verzoek
  om af te sluiten, en na 3 seconden alsnog). Haal ze dan weg zoals hieronder.

**Andere gebruikers** op de pc houden hun eigen instellingen en autostart. Zijn de
programmabestanden weg, dan kan dat geen kwaad: de autostart wijst naar een programma dat er niet
meer is en doet niets. Bleven ze staan omdat die gebruiker BuurmansRgb nog open had (zie
hierboven), dan start zijn autostart het bij zijn volgende aanmelding weer. Wil een andere gebruiker
ze toch weg, dan doet hij dat zelf, ingelogd als zichzelf:

1. Verwijder de map `%AppData%\BuurmansRgb` (plak `%AppData%` in de adresbalk van Verkenner).
2. Haal de autostart weg in **Taakbeheer → Opstartapps** (BuurmansRgb, rechtermuisknop →
   Uitschakelen), of in PowerShell:

   ```powershell
   Remove-ItemProperty HKCU:\Software\Microsoft\Windows\CurrentVersion\Run -Name BuurmansRgb
   ```

## Stil installeren en verwijderen

De installer is gemaakt met Inno Setup en kent de gewone parameters daarvan [32][34]. Draai het
in een **gewone PowerShell** (niet als administrator) en keur de UAC-vraag goed. De engine start
na afloop dan als jij, zonder beheerdersrechten (zie "De engine start na afloop" hieronder).

### Installeren

```powershell
$setup = Start-Process .\BuurmansRgb-setup.exe -Wait -PassThru -ArgumentList `
    '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/RESTARTEXITCODE=3010',
    "/LOG=`"$env:TEMP\BuurmansRgb-setup.log`""
$setup.ExitCode
```

`Start-Process -Wait` is nodig omdat PowerShell anders niet op de setup wacht. De aanhalingstekens
om het pad van `/LOG` (met een backtick ervoor) zijn nodig als `%TEMP%` een spatie bevat:
`Start-Process` plakt de argumenten met spaties aan elkaar, en Setup leest dan alleen het stuk tot
de spatie.

| Parameter | Wat het doet |
| --- | --- |
| `/SILENT` | Geen wizard, wel een venster met de voortgang. |
| `/VERYSILENT` | Ook geen voortgang. **Let op:** is een herstart nodig, dan herstart de pc zonder te vragen, tenzij je `/NORESTART` meegeeft. |
| `/NORESTART` | Nooit herstarten. Geef dit altijd mee bij `/VERYSILENT` en herstart later zelf; bij `/SILENT` vraagt Setup anders "Nu herstarten?". |
| `/RESTARTEXITCODE=n` | De exitcode als een herstart nodig is (standaard 0, dus niet te zien); bijv. `3010`. Alleen nuttig samen met `/NORESTART`: zonder herstart `/VERYSILENT` de pc meteen en zie je de code niet. De help noemt het "typically used along with /NORESTART" [32]. |
| `/SUPPRESSMSGBOXES` | Elk berichtvenster krijgt het standaardantwoord. Werkt alleen samen met `/SILENT` of `/VERYSILENT`. |
| `/LOG="pad"` | Het log naar een vaste plek (overschrijft; kan het bestand niet gemaakt worden, dan stopt Setup). `/LOG` zonder pad geeft een unieke naam in `%TEMP%`. |
| `/DIR="pad"` | Een andere installatiemap dan `C:\Program Files\BuurmansRgb`. |
| `/SP-`, `/NOCANCEL` | Geen vraag vooraf; Annuleren niet mogelijk. |

Wat er bij deze installer bij komt kijken:

- **Herstart:** alleen als PawnIO of de MSI van OpenRGB meldt dat er een herstart nodig is
  (code 3010 of 1641). Dan herstart `/VERYSILENT` zonder `/NORESTART` de pc meteen.
- **Foutmeldingen:** mislukt PawnIO of OpenRGB, dan klikt `/SUPPRESSMSGBOXES` de melding weg en
  gaat Setup door; BuurmansRgb staat er dan wel, met alleen gesimuleerde apparaten of zonder
  RAM en oudere moederborden. **De exitcode van Setup blijft dan 0** (of de waarde van
  `/RESTARTEXITCODE`): de installer meldt de fout alleen in dat venster en in het log. Controleer
  na een stille installatie dus altijd het log: zoek naar `PawnIO setup returned` en
  `OpenRGB msiexec returned`. Het log van `msiexec` staat ernaast, met `.openrgb.txt` achter de
  naam van het setuplog. Ook zonder `/LOG` schrijft Setup een log in `%TEMP%` (`Setup Log <datum> #001.txt`).
- **De engine start na afloop**, ook stil, met `--first-run`, als het account dat de setup
  startte. Dat account krijgt dus de engine en **Starten met Windows**. Zonder beheerdersrechten
  draait hij alleen als Setup zelf om UAC vroeg: de installer start hem als "de gebruiker van vóór
  de UAC-vraag" (`runasoriginaluser` [47]). Start je Setup al verhoogd, vanuit een PowerShell als
  administrator of als SYSTEM (bijv. via software-uitrol), dan kan Setup niet terug naar een
  gewone gebruiker: de engine, en de instellingen-app die hij opent, draaien dan verhoogd of als
  SYSTEM, tot de volgende aanmelding of herstart (dan start de autostart hem gewoon). Als SYSTEM
  krijgt bovendien geen gebruiker **Starten met Windows**.
- **Met een ander account installeren** (bijv. een beheerder die het voor een gebruiker doet):
  die gebruiker heeft nog geen tray-icoon. Hij opent BuurmansRgb eerst zelf uit het menu Start,
  en zet daarna **Starten met Windows** aan in het tray-menu.
- **`/TASKS` en `/MERGETASKS`** doen niets: de installer heeft geen taken om aan of uit te
  vinken. **`/NOICONS`** ook niet: er is geen keuze voor een map in het menu Start, de
  snelkoppeling komt er altijd.
- **Draaiende apps** stopt de installer zelf; `/CLOSEAPPLICATIONS` en `/RESTARTAPPLICATIONS`
  zijn niet nodig.
- **Exitcodes** [33]: 0 gelukt; 1 starten mislukt; 2 geannuleerd vóór het installeren; 3 of 4
  fatale fout; 5 geannuleerd tijdens het installeren; 6 gestopt door een debugger; 7 en 8
  "Voorbereiden op installatie" kon niet verder (8: herstart nodig); of de waarde van
  `/RESTARTEXITCODE`.

### Verwijderen

```powershell
Start-Process 'C:\Program Files\BuurmansRgb\unins000.exe' -Wait -ArgumentList `
    '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$env:TEMP\BuurmansRgb-uninstall.log`""
```

De uninstaller kent `/SILENT` (geen vraag vooraf en geen melding na afloop, wel de voortgang),
`/VERYSILENT` (ook geen voortgang; herstart zonder te vragen tenzij `/NORESTART`),
`/SUPPRESSMSGBOXES`, `/NORESTART` en `/LOG` zoals bij installeren [34]. Let op: ook `/SILENT`
vraagt niet meer of je het zeker weet. De exitcodes van de uninstaller staan niet in die bron.

Met `/SUPPRESSMSGBOXES` beantwoordt het verwijderen elke vraag met "Nee": OpenRGB, PawnIO en je
instellingen blijven staan. Wat het verwijderen verder doet, en voor welke gebruiker, staat bij
[Verwijderen](#verwijderen) hierboven en in [ontwikkelaars.md](ontwikkelaars.md) onder
"Installer bouwen en testen". Moeten OpenRGB en PawnIO toch weg, en gebruikt niets anders ze
(PawnIO bijv. FanControl), dan kan dat daarna apart (niet getest):

```powershell
$uninstall = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
$openRgb = Get-ChildItem $uninstall | Get-ItemProperty | Where-Object DisplayName -eq 'OpenRGB'
Start-Process msiexec.exe -Wait -ArgumentList '/x', $openRgb.PSChildName, '/qn', '/norestart'
$pawnIo = (Get-ItemProperty "$uninstall\PawnIO").UninstallString.Trim('"')
Start-Process $pawnIo -Wait -ArgumentList '-uninstall', '-silent'
```

## Bronnen

Gelezen op 3 november 2026. De nummers liggen vast, dus er ontbreken er een paar.
"Niet officieel" betekent een site met procesinformatie, een melding van een gebruiker of een
ander project, niet van de maker.

- [1] [pawnio.eu](https://pawnio.eu/)
- [2] [namazso/PawnIO (licentie)](https://github.com/namazso/PawnIO)
- [3] [namazso/PawnIO.Modules](https://github.com/namazso/PawnIO.Modules)
- [5] [FanControl, issue #3516](https://github.com/Rem0o/FanControl.Releases/issues/3516) (september 2025 tot februari 2026)
- [6] [PawnIO.Setup, issue #1](https://github.com/namazso/PawnIO.Setup/issues/1) (september 2025 tot mei 2026)
- [7] [PawnIO.Setup, releases](https://github.com/namazso/PawnIO.Setup/releases)
- [8] [OpenRGB, MR !2833](https://gitlab.com/CalcProgrammer1/OpenRGB/-/merge_requests/2833) (mei 2025)
- [9] [OpenRGB, MR !2885](https://gitlab.com/CalcProgrammer1/OpenRGB/-/merge_requests/2885) (juni 2025)
- [10] [FanControl, discussie #3017](https://github.com/Rem0o/FanControl.Releases/discussions/3017) (maart 2025 tot januari 2026)
- [11] [TrafficMonitor-PawnIO, releases](https://github.com/EmiliayC/TrafficMonitor-PawnIO/releases) (niet officieel)
- [12] [OpenRGB, releases op Codeberg](https://codeberg.org/OpenRGB/OpenRGB/releases)
- [13] [Gridinsoft over WinRing0x64.sys](https://blog.gridinsoft.com/winring0x64-sys-process/) (niet officieel)
- [14] [IT-Connect over OpenRGB 1.0](https://www.it-connect.tech/openrgb-1-0-the-open-source-tool-that-controls-rgb-devices-on-windows-linux-and-macos/) (september 2026)
- [17] [PawnIO.Setup, issue #12](https://github.com/namazso/PawnIO.Setup/issues/12) (juli tot augustus 2026)
- [18] [PawnIO.Setup, gesloten issues](https://github.com/namazso/PawnIO.Setup/issues?q=is%3Aissue+is%3Aclosed)
- [20] [FanControl, discussie #3537](https://github.com/Rem0o/FanControl.Releases/discussions/3537) (september 2025)
- [21] [FanControl, issue #3952](https://github.com/Rem0o/FanControl.Releases/issues/3952) (maart 2026)
- [23] [Riot, What is Vanguard](https://support.riotgames.com/valorant/performance/what-is-vanguard)
- [24] [Wikipedia, Riot Vanguard](https://en.wikipedia.org/wiki/Riot_Vanguard)
- [25] [FanControl, issue #1694](https://github.com/Rem0o/FanControl.Releases/issues/1694) (april 2023)
- [27] [Gridinsoft over LightingService.exe](https://blog.gridinsoft.com/lightingservice-exe/) (niet officieel)
- [28] [Ubisoft, BattlEye FAQ](https://www.ubisoft.com/pl-pl/game/rainbow-six/siege/news-updates/2999gdyIXLbeiZP78R7545/battleye-faq)
- [29] [Hardware Canucks, forum](https://hardwarecanucks.com/forum/threads/replacement-for-winring0.90203/post-1018517) (februari 2026, niet officieel)
- [30] [OpenRGB, SDK-documentatie](https://codeberg.org/OpenRGB/OpenRGB/raw/branch/master/Documentation/OpenRGBSDK.md)
- [31] [oss-security](https://www.openwall.com/lists/oss-security/2026/08/25/4) en [openSUSE security](https://security.opensuse.org/2026/08/25/openrgb-remote-vulnerabilities) (25 augustus 2026)
- [32] [Inno Setup, Setup Command Line Parameters](https://jrsoftware.org/ishelp/topic_setupcmdline.htm)
- [33] [Inno Setup, Setup Exit Codes](https://jrsoftware.org/ishelp/topic_setupexitcodes.htm)
- [34] [Inno Setup, Uninstaller Command Line Parameters](https://jrsoftware.org/ishelp/topic_uninstcmdline.htm)
- [35] [SignalRGB, Service Issues](https://docs.signalrgb.com/troubleshooting/software-conflicts/signalrgb-services/)
- [36] [file.net, Corsair.Service.exe](https://www.file.net/process/corsair.service.exe.html) (niet officieel)
- [37] [file.net, CueLLAccessService.exe](https://www.file.net/process/cuellaccessservice.exe.html) (niet officieel)
- [38] [Razer support, Synapse crashes](https://mysupport.razer.com/app/answers/detail/a_id/1917)
- [39] [file.net, Razer Synapse Service.exe](https://www.file.net/process/razer%20synapse%20service.exe.html) (niet officieel)
- [40] [file.net, RzSDKService.exe](https://www.file.net/process/rzsdkservice.exe.html) (niet officieel)
- [41] [Razer Insider, RzSDKServer.exe](https://insider.razer.com/razer-support-45/rzsdkserver-exe-crashes-every-5-minutes-18126) (niet officieel)
- [42] [file.net, ROGLiveService.exe](https://www.file.net/process/rogliveservice.exe.html) (niet officieel)
- [43] [file.net, LEDKeeper2.exe](https://www.file.net/process/ledkeeper2.exe.html) (niet officieel)
- [44] [FreeFixer, Mystic_Light_Service.exe](https://www.freefixer.com/library/file/Mystic_Light_Service.exe-321067/) (niet officieel)
- [45] [Gigabyte Control Center](https://www.gigabyte.com/WebPage/942/global/)
- [46] [SignalRGB, Conflicting Programs](https://docs.signalrgb.com/troubleshooting/software-conflicts/conflicting-programs/)
- [47] [Inno Setup, [Run] section](https://jrsoftware.org/ishelp/topic_runsection.htm) (de vlag `runasoriginaluser`)

## Zonder installer (voor ontwikkelaars)

Zonder adminrechten, met OpenRGB zelf geïnstalleerd en de SDK-server aan (tabblad
**SDK Server** → **Start Server**, OpenRGB als administrator voor moederbord, RAM en GPU):

```powershell
$dest = "$env:LOCALAPPDATA\Programs\BuurmansRgb"
dotnet publish src/BuurmansRgb.Engine -c Release -o $dest
dotnet publish src/BuurmansRgb.UI -c Release -o $dest
& "$dest\BuurmansRgb.Engine.exe"
```

Engine en instellingen-app moeten in dezelfde map staan; daarom twee keer `publish` naar
`$dest`. Zo heb je wel de .NET 10 Desktop Runtime nodig (die zit bij de SDK). De tweede
`publish` compileert de instellingen-app vooraf (ReadyToRun) en haalt daarvoor de eerste keer
vier pakketten van nuget.org: de compiler `microsoft.netcore.app.crossgen2.win-x64` en de
runtime waartegen hij compileert (`microsoft.netcore.app.runtime.win-x64`,
`microsoft.windowsdesktop.app.runtime.win-x64` en `microsoft.aspnetcore.app.runtime.win-x64`).
De app gebruikt daarna toch de geïnstalleerde .NET. Een gewone `dotnet build` haalt ze niet.

Meer uitleg over de instellingen-app, de instellingenbestanden en per apparaattype staat in
de [handleiding](handleiding.md); hoe de installer werkt in
[openrgb-meeleveren.md](openrgb-meeleveren.md).
