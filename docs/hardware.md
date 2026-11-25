# Hardware

Vul hier je onderdelen in. Agents gebruiken dit voor taak 5–8; de teststappen staan in
`ontwikkelaars.md` onder "Zelf testen op echte hardware".

De pc thuis, stand 24-11-2026: AMD Ryzen 7 9850X3D, Windows 11 Pro 26H2 (build 26300), schaal
125 %. Getest met BuurmansRgb 1.5.90 (`main` ff9cbca), OpenRGB 1.1.0 als service (SDK-protocol 6)
en PawnIO 2.2.0.0, met Logitech G HUB en GIGABYTE Control Center afgesloten. "Werkt" betekent
hieronder: de gebruiker heeft het op het apparaat zelf gezien.

| Onderdeel | Merk en model | Ondersteund door OpenRGB? | Werkt in BuurmansRgb? | Opmerkingen |
| --- | --- | --- | --- | --- |
| Moederbord | Gigabyte B850 AORUS ELITE WIFI7, BIOS F10 (5-2-2026) | Ja: "B850 AORUS ELITE WIFI7", IT5711 (`048D:5711`, firmware 1.0.29.6), modus Direct | Ja: statisch, helderheid, Regenboog, Fonkelen, Komeet, Scanner, Vuur, Kleurenloop, per LED, knipperen per zone, slaapstand | Zones: `ARGB_V2_1`, `ARGB_V2_2`, `ARGB_V2_3` (Linear, 0–256 LED's, eerst 0 tot je de lengte zet), `Chipset Accent` (1 LED, een lampje op het bord) en `LED_C` (1 LED, niets zichtbaar). Welke header wat is: zie "Gemeten" |
| CPU-koeler | Lian Li Galahad II Lite 360R (waterkoeling) | Via `ARGB_V2_2` van het moederbord, niet als eigen apparaat | Ja | Drie radiatorfans **in serie** met **10 LED's per fan** (LED 1–10, 11–20, 21–30); de pomp hangt **parallel** en toont dezelfde eerste LED's (Lian Li: 16 LED's). Zone op **30** |
| RAM | 2× Kingston FURY Beast 16 GB DDR5-6000 CL30 (`KF560C30-16`) | Niet gevonden door OpenRGB | – | Geen RGB-variant |
| GPU | PowerColor Radeon RX 9070 XT Hellhound 16GB OC | Nee, niet gevonden | Nee | Vaste ijsblauwe LED met een "Dark"-stand, geen RGB. Geen "Direct"-vraag van toepassing |
| Fans / fan-controller | Vier 120 mm ARGB PWM-fans van de Lian Li Vector V100R (drie aan de zijkant met omgekeerde luchtstroom, één achter) | Via `ARGB_V2_1` van het moederbord | Ja | Alle vier **parallel** met de strip op `ARGB_V2_1`, **10 LED's per fan**. LED 1 zit van voren gezien **linksonder**, LED 2 ligt **met de klok mee** (het voorbeeld neemt LED 1 bovenaan aan). Komeet draait netjes rond |
| Case-LED / ARGB-strips | Lian Li Vector V100R midi tower: ARGB-strip langs de voedingskap en de bodem | Via `ARGB_V2_1` van het moederbord | Ja | 26 LED's (het langste deel van `ARGB_V2_1`, zone op **26**). Lengte via `zoneLengths` in settings.json wijzigen werkt binnen een seconde, rustig |
| Toetsenbord | Logitech G213 (`046D:C336`) | Ja: "Logitech G213", modus Direct, serienummer `0A8430503134` | Ja: statisch, helderheid, effecten, per LED, knipperen, slaapstand | **Geen losse toetsen**: één zone `Keyboard` van 5 LED's (Left Area, Middle Area, Right Area, Arrow and Homekeys, Numpad), geen matrix. De volgorde klopt. Toetsen-tests (WASD, letters op de toetsen) zijn op dit toetsenbord niet te doen |
| Muis | Logitech G502 HERO (`046D:C08B`) | Ja: "G502 HERO Gaming Mouse" (via ontvanger, "Wireless Lightspeed Device"), modus Direct, **geen serienummer** | Ja: statisch, helderheid, effecten, Zones gelijk (ook na een herstart van de engine), knipperen per zone, slaapstand | `Primary Zone` = de streep, `Logo Zone` = het logo, elk 1 LED. Het logo ziet er met dezelfde kleur roziger uit dan de streep (paars #8800FF) |
| Headset | Logitech G733 (`046D:0AB5`) | Ja, maar niet altijd: "G733 Gaming Headset" (via ontvanger), modus Direct, geen serienummer | Ja als OpenRGB hem ziet: statische kleur, aan/uit, knipperen | `Logo Zone` en `Primary Zone`, elk 1 LED. OpenRGB zag hem niet bij het opstarten van de service (headset wel aan), wel na een herstart van de service, en na "Herstarten" terwijl G HUB en Control Center draaiden weer niet. Uit- en weer aanzetten van de headset: blijft in de lijst, krijgt vanzelf zijn kleur terug |

### Gemeten (24-11-2026)

Met Knipperen per zone (in een donkere kast) en een LED 1/LED 2-proef per header:

| Header | Lengte | Wat erop zit |
| --- | --- | --- |
| `ARGB_V2_1` | 26 | De strip (26 LED's) en de vier casefans (10 LED's per fan), allemaal parallel: ze krijgen hetzelfde signaal |
| `ARGB_V2_2` | 30 | De drie koelerfans in serie (3 × 10, geteld met Tellen) en de pomp parallel |
| `ARGB_V2_3` | 10 (uit oude instellingen) | Niets: knipperen is nergens te zien |
| `Chipset Accent` | 1 | Een lampje op het moederbord |
| `LED_C` | 1 | Niets zichtbaar |

- Tellen bij `ARGB_V2_2` gaf 30; na "Dit is de laatste" brandde de hele pomp en niets meer.
- OpenRGB hield de lengtes vast na een herstart van de service.
- Knipperen duurt 1,5 s; met één klik was het in de kast makkelijk te missen.

### Verversen en OpenRGB

- Na "Verversen" (een echte rescan) is de lijst bij Apparaten ~1,5 s leeg en komen de apparaten
  stapsgewijs terug (1 → 3 → 4 binnen 3,3 s). De knop zegt dan al weer "Verversen".
- `engine.log` gaf geen "OpenRGB sent no new device list". OpenRGB 1.1.0, SDK-protocol 6.
- Met GIGABYTE Control Center en G HUB open liep een "Verversen" vast ("Reloading the OpenRGB
  devices failed: The operation has timed out.") en verdween de headset uit OpenRGB.
  - De time-out lag bij BuurmansRgb: na de melding van OpenRGB dat zijn lijst veranderde,
    wachtte de engine 5 s op die lijst en verbrak dan de verbinding ("Lost the connection",
    opnieuw na 5 s), ook al stond die nog open. Het log toont alleen die time-out, niet de
    oorzaak. Vermoedelijk zocht OpenRGB langer dan 5 s; het kan ook zijn dat OpenRGB geen
    antwoord gaf op `REQUEST_CONTROLLER_DATA` voor een index die aan het begin van de zoekronde,
    na het leegmaken van zijn lijst, niet meer bestond, zodat de leesactie bleef hangen tot de
    time-out. Vanaf #? wacht de engine tot 30 s zolang de verbinding openstaat, maakt hij daarna
    meteen een nieuwe, en noemt de app de draaiende RGB-software als de lijst niet komt. Komt de
    lijst bij een volgende test steeds pas na ~30 s, dan past de tweede verklaring beter.
  - Dat de **G733** wegviel, ligt bij OpenRGB en G HUB: OpenRGB zag de headset niet bij het
    zoeken terwijl G HUB draaide, ook niet na "Herstarten" (OpenRGB herstart) met G HUB open. Na
    een herstart van de service zonder G HUB zag OpenRGB hem wel. BuurmansRgb krijgt de lijst
    van OpenRGB en kan een apparaat dat daar ontbreekt niet tonen. Zie "Een apparaat verdwijnt
    terwijl fabrikantsoftware draait" in [installatie.md](installatie.md).

### Herstarten

- Naam van de driver: `PawnIO` ("PawnIO Service"), `KERNEL_DRIVER`, starttype `DEMAND_START`,
  bestand in `DriverStore\FileRepository\pawnio.inf_amd64_…\PawnIO.sys`.
- "Herstarten" in het tray-menu terwijl FanControl draaide (dat gebruikt PawnIO): OpenRGB werd
  herstart, PawnIO niet ("PawnIO NotStopped", Win32-fout 1052), met een melding over PawnIO; de
  engine en de apparaten kwamen terug.

Overige USB-apparaten, geen RGB in BuurmansRgb:

- **CH340 (COM3, `1A86:7523`):** een USB-naar-serieel-chip, geen ARGB-hub; er hangt
  vermoedelijk een microcontrollerbordje aan.
- **ESP32-S3 (COM6, `303A:1001`):** mogelijk de ATOM S3 Lite met WLED.

Handig om bij "Opmerkingen" te noteren:

- of het apparaat in OpenRGB een "Direct"-modus heeft, en of het de kleur zonder die modus
  toch volgt;
- de locatie die OpenRGB geeft (tooltip van de apparaatkaart), vooral bij RAM-sticks en twee
  dezelfde videokaarten;
- bij een controller of ARGB-header: hoeveel LED's er per kanaal zitten, of het type klopt
  (fan, strip of behuizing), waar LED 1 van een fan-ring zit en of OpenRGB de lengte na een
  herstart vergat;
- bij een moederbord of hub: welke header welke fan of strip is ("Knipperen" naast de zone), en
  of "Zones gelijk" er op de echte LED's goed uitziet;
- een headset die OpenRGB niet kent: "niet ondersteund door OpenRGB".

## Fabrikantsoftware die uit moet

Op de pc thuis staan vier RGB-programma's. Regelen er twee hetzelfde apparaat, dan flikkert
het of springen kleuren terug; laat per apparaat één programma de verlichting regelen.

- **GIGABYTE Control Center** 25.07.21.01: moederbord, en daarmee de koeler, de casefans en de
  strip van de behuizing aan de ARGB-headers. Proces `GCC.exe`
  (`C:\Program Files\GIGABYTE\Control Center\`), draait verhoogd, **geen service**; geen
  `RGBFusion.exe`. BuurmansRgb 1.5.90 herkent het niet; versies na 1.5.90 herkennen het wel
  (#143).
- **Logitech G HUB** 2026.7.1011216: toetsenbord, muis en headset. Processen `lghub_agent.exe` en
  `lghub_system_tray.exe` (autostart `LGHUB` in `HKCU\…\Run`), service `LGHUBUpdaterService`
  (`lghub_updater.exe`); daarnaast de driver-service `logi_lamparray_service`. Nam tijdens de
  test een deel van de G213 over. BuurmansRgb 1.5.90 herkent het niet; versies na 1.5.90
  herkennen het wel (#143).
- **SignalRGB** 2.5.78: kan alle apparaten aansturen. Op 24-11-2026 geen proces en geen
  Uninstall-regel meer, alleen de map `%LOCALAPPDATA%\VortxEngine`.
- **OpenRGB** 1.1.0 staat ook los geïnstalleerd en is hier de service `OpenRGB`; laat er één
  OpenRGB tegelijk draaien.
- Windows **Dynamisch licht** staat uit.
