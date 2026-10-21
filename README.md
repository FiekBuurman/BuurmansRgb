# BuurmansRgb

Lichtgewicht RGB-aansturing voor Windows: fans, strips, moederbord, RAM, videokaart,
toetsenbord, muis en headset vanuit één app. Een kleine engine draait op de achtergrond met een
tray-icoon en stuurt de lampen via de SDK-server van [OpenRGB](https://openrgb.org). De
instellingen-app open je alleen als je iets wilt veranderen; sluiten = proces weg.

Het plan, de architectuur en de stand per taak staan in [docs/bouwplan.md](docs/bouwplan.md);
ideeën voor later in [docs/ideas.md](docs/ideas.md).

## Installeren

Zie [docs/installatie.md](docs/installatie.md): één `BuurmansRgb-setup.exe` zet BuurmansRgb,
OpenRGB (als service) en de driver PawnIO in één keer neer. Hoe je de installer bouwt en test,
staat hieronder bij "Installer bouwen en testen".

## Bouwen, starten en testen

Nodig: Windows 10/11 en de .NET 10 SDK. Voor echte hardware ook OpenRGB met de SDK-server aan
(zie "OpenRGB koppelen"); zonder OpenRGB werkt alles met gesimuleerde lampen.

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

Zonder OpenRGB staan er twaalf gesimuleerde lampen klaar, zie "Gesimuleerde lampen".

## De instellingen-app

De app volgt het ontwerp "Werkplaats bij nacht" uit [design/DESIGN.md](design/DESIGN.md):
altijd donker, zodat de kleuren van de lampen gloeien, met een eigen titelbalk en links een
navigatiebalk met **Lampen**, **Kleur**, **Scènes**, **Buren** en onderaan **Instellingen**.

- **Venster:** donkere titelbalk met het kleine icoon en het woordmerk. Slepen, dubbelklikken
  om te maximaliseren, Aero Snap en Alt+Spatie werken zoals altijd. Minimaal 960 × 640.
- **Toetsenbord:** met Tab kom je in de navigatiebalk, met de pijltjes kies je een pagina. Elke
  knop, kaart en schuif krijgt bij toetsenbordfocus een ring van 2 px.
- **Onderaan** staat wat er net gebeurde (bijv. "Scène 'Nacht' staat aan.") en rechts
  "Opgeslagen ✓" met de tijd zodra de engine je wijziging heeft weggeschreven, een seconde na
  de laatste wijziging. "Opslaan mislukt" betekent: de lampen werken, maar na een herstart is
  de wijziging weg.
- **Engine weg:** draait de engine niet, dan meldt het venster dat, met "Opnieuw proberen".
  Stopt de engine terwijl het venster open is, dan staat er dat de verbinding is verbroken.

### Lampen

- Bovenaan het woordmerk en de OpenRGB-status in één regel, met een tip achter de ⓘ (bij
  aanwijzen of met Tab). Een schermlezer leest een nieuwe status voor.
- De kaart **Hele huis** zegt hoeveel lampen aan zijn ("11 van 12 aan"), heeft een grote
  schakelaar voor alles en een strip van 14 bolletjes die laat zien wat de lampen nu echt doen:
  elk bolletje is een LED uit alle lampen achter elkaar, dus bij een regenboog loopt hij mee.
- **Lampen in huis:** elke lamp (elk apparaat) als kaart, de echte onder "Echte lampen
  (OpenRGB)" en de gesimuleerde onder "Gesimuleerde lampen", twee of drie naast elkaar. Op de
  kaart: een bolletje in de kleur van zijn felste LED, de naam, het type met de helderheid of
  "uit", zo nodig de plek (bijv. "Adres 0x52", zie "Welk apparaat is wat?") en
  "⚠ Volgt de kleur misschien niet", en een schakelaar in de kleur van de lamp.
- Een klik op een kaart opent de lamp bij Kleur en laat hem op de hardware knipperen.
  "+ Erbij knutselen" gaat naar Instellingen.

### Kleur

Bovenaan kies je bij "Welke lamp?" een lamp of "Hele huis", met de schakelaar ernaast.

- **Kleur mengen:** links een groot voorbeeld met een strip in de gemengde kleur, de hex-code
  (je kunt er ook een typen) en de R · G · B-waarden; rechts de schuiven Rood, Groen en Blauw
  (0–255, ook met de pijltjes), "Uit de gereedschapskist" met zes vaste kleuren (Klus-wit,
  Rood, Oranje, Groen, Blauw, Paars) en de knop **Op de strip zetten**. Schuiven, hex-code en
  vaste kleuren laten de kleur meteen in het voorbeeld zien ("Nog niet op de lamp"); pas de
  knop zet hem op de lamp.
- **Licht en beweging:** de helderheid (0–100 %), het effect en de snelheid. De helderheid
  werkt op elk effect.
- **Hele huis of één lamp:** een wijziging bij "Hele huis" geldt voor alle lampen, maar alleen
  voor wat je wijzigt: zet je de helderheid, dan houdt een lamp met een eigen kleur die kleur.
  Een wijziging bij één lamp geldt alleen voor die lamp. Is een lamp weer gelijk aan "Hele
  huis", dan volgt hij die weer.
- Zijn de effecten gepauzeerd, dan staat bovenaan een melding met "Hervatten" (zie
  "Pauzeren").

#### Effecten

| Effect | In `settings.json` | Wat het doet |
| --- | --- | --- |
| Statisch | `static` | Eén kleur op alle LED's |
| Ademen | `breathing` | De kleur fadet in 4 seconden in en uit |
| Regenboog | `rainbow` | Alle tinten verdeeld over de LED's, schuift met 0,2 rondes per seconde |
| Golf | `wave` | Een band van de kleur die over de LED's loopt, 0,5 golf per seconde |
| Kleurenloop | `color-loop` | Je eigen 2–8 kleuren die in elkaar overlopen, 0,2 loop per seconde; over de LED's verdeeld of alle LED's tegelijk |
| Per LED | `per-led` | Een eigen kleur per LED (zie "Per-LED-editor") |

- **Snelheid** (10–300 %, standaard 100 %) werkt alleen bij een bewegend effect; bij Statisch
  en Per LED staat de schuif grijs. Een nieuwe snelheid gaat verder waar de animatie was, en
  lampen die samen een nieuwe snelheid krijgen, blijven gelijk lopen.
- **Kleurenloop:** kies het effect, dan verschijnen onder het kleurvoorbeeld de kleuren van de
  loop. Klik op een kleur, meng een nieuwe en kies "In de loop zetten"; + voegt een kleur toe
  na de gekozen, − haalt de gekozen weg. Zonder vinkje bij "Kleuren over de LED's verdelen"
  krijgen alle LED's tegelijk dezelfde kleur. Een lamp onthoudt zijn loop, ook als je
  tussendoor een ander effect kiest. Standaard: geel, groen, blauw.

#### Voorbeeld

Onder "Licht en beweging" staat per lamp een kaart met het icoon van zijn type, de naam en
**Laat knipperen**, en per zone een bolletje per LED. Het toont wat de engine echt rendert,
ook als de effecten gepauzeerd zijn.

- **Zones** staan er met naam en aantal LED's zoals OpenRGB ze geeft; een lamp met één zone
  toont geen zonenaam.
- **Fans** (in OpenRGB het type "cooler", dus ook een AIO-pomp) tekent het voorbeeld per zone
  als ring, met LED 1 bovenaan en dan met de klok mee. OpenRGB zegt niet waar LED 1 echt zit;
  de wijzerplaat is dus een keuze. Een zone waarvan je de lengte instelt, of een zone van meer
  dan 48 LED's, blijft een rij: dat is meestal een kanaal van een hub met een strip of meer
  fans erop.
- **Toetsenborden** met een matrix-map van OpenRGB tonen elke LED als toets op zijn plek, en
  maken de toetsen kleiner in een smal venster. LED's buiten de map (bijv. een logo) staan in
  een rij eronder. Met toetsnamen tekent het voorbeeld Tab, Caps Lock, Shift, Ctrl, Backspace,
  Enter, de spatiebalk en de 0 van het numerieke blok breder over de lege vakjes ernaast.
- **Namen:** wijs een LED aan voor zijn naam en nummer, bijv. "Escape (LED 1)", of "LED 5"
  zonder naam. Een schermlezer noemt de LED's zo ook, en kan ze selecteren zoals een klik.
- **Lengte van een zone:** bij een zone die OpenRGB in lengte laat instellen (een ARGB-header
  of -kanaal) staat "Aantal LED's (0–120):" met het bereik, en −, +, een schuif, een vak en
  "Toepassen". Met −, + en de schuif groeit het voorbeeld meteen; de zone zelf krijgt de lengte
  pas als je 0,6 seconde niets verandert of de schuif loslaat, want OpenRGB laadt bij elke
  nieuwe lengte zijn apparaten opnieuw. Typen en "Toepassen" (of Enter) gaat meteen. Een zone
  met 0 LED's zegt "Nog geen LED's ingesteld", met uitleg. De lengte wordt bewaard (zie
  `zoneLengths` bij "settings.json").

#### Per-LED-editor

Klik in het voorbeeld op een LED om hem te kiezen; Shift+klik kiest een reeks vanaf de vorige
klik, en een klik op de naam van een zone kiest die hele zone (bijv. één ring van een fan).
Zolang er LED's gekozen zijn, staat boven het mengpaneel welke dat zijn, heet de knop "Op de
gekozen LED's zetten" en gaat de kleur alleen naar die LED's.

- **Verloop vullen** vult de reeks met een verloop van de kleur van de eerste naar die van de
  laatste LED (geef die dus eerst een kleur).
- **Selectie opheffen**, of een andere lamp kiezen, geeft het mengpaneel weer aan de lamp.
- De lamp krijgt bij de eerste wijziging het effect "Per LED". LED's die je niet kleurt,
  volgen de kleur van de lamp. De kleuren blijven bewaard, ook als je tussendoor een ander
  effect kiest of een zone van lengte verandert.

### Scènes

Een scène bewaart de verlichting van alle lampen onder een naam (in de code en in
`profiles.json` heet dat een profiel). De scènes staan als kaarten, drie of vier naast elkaar:
vijf bolletjes met de kleuren van de scène, de naam en wanneer hij vanzelf aangaat ("Elke dag
om 23:00", "Bij eldenring.exe"), of anders wat hij laat zien ("Regenboog · 80%", "Alles uit").

- **Een klik op een kaart** zet de scène aan: kleur, helderheid, aan/uit, effect, snelheid,
  kleurenloop en per-LED-kleuren van alle lampen worden die van de scène, en ook de opgeslagen
  verlichting.
- **"+ Nieuwe scène in elkaar zetten"** vraagt een naam en bewaart de verlichting zoals die nu
  is.
- **De moersleutel** op een kaart kiest de scène (lichtere rand) en toont eronder "Aanzetten",
  "Overschrijven met wat nu brandt", "Verwijderen" (vraagt eerst of het zeker is) en
  "Vanzelf aanzetten".
- **Vanzelf aanzetten:** vul bij "Elke dag aanzetten om" een tijd in (`23:00` of `23.00`)
  en/of bij "Aanzetten als dit programma start" de naam van een .exe (`eldenring.exe`;
  `eldenring` of een heel pad mag ook), en kies "Toepassen". Leeg en "Toepassen" zet het uit.
  Een snelkoppeling of script (`.lnk`, `.url`, `.bat`, `.cmd`) weigert de app: vul het doel
  in (Eigenschappen van de snelkoppeling).
- Zonelengtes horen niet bij een scène. Welke scène het laatst aanging, weet de app niet;
  daarom heeft geen kaart de rand van "actief".

Hoe het vanzelf aanzetten werkt:

- **Op tijd:** elke dag om die tijd zet de engine de scène aan, net alsof je op de kaart klikte.
  Het is de klok van Windows: bij zomer- en wintertijd blijft 23:00 gewoon 23:00; een tijd in
  het uur dat wegvalt komt na dat uur, een tijd in het dubbele uur alleen de eerste keer.
  Slaapt de pc, staat hij uit of zet je de klok erover heen, dan slaat de engine die keer over
  (ook als de timer meer dan een minuut te laat afgaat): na een nacht slaapstand komt "Nacht"
  's ochtends niet alsnog. Hebben twee scènes dezelfde tijd, dan wint de eerste op alfabet.
- **Bij een programma:** start het programma, dan staat de scène binnen 2 seconden aan. Stopt
  het, dan komt de verlichting van daarvoor terug, tenzij je die intussen veranderde. Bij twee
  programma's met een scène toont het laatst gestarte de zijne; stopt dat, dan komt die van
  het eerste terug. Een programma dat al draait als je het instelt, telt pas bij zijn volgende
  start; draait het al als de engine start, dan gaat de scène meteen aan. Sluit je de engine
  af terwijl het programma draait, dan zet hij eerst de verlichting van daarvoor terug en
  bewaart die.
- **Tijd tijdens een spel:** komt een tijd terwijl een programma zijn scène toont, dan blijft
  die staan en komt de scène van de tijd zodra het programma stopt; heb je tijdens het spel
  zelf iets veranderd, dan komt hij meteen.

### Buren

Nog niet ontworpen; de pagina zegt rustig dat het nog in de werkplaats is.

### Instellingen

- **OpenRGB:** de status met de tip uitgeschreven.

  | Wat je ziet | Wat er aan de hand is |
  | --- | --- |
  | OpenRGB: verbinden… | De engine is net gestart, of de verbinding viel weg en de volgende poging komt zo |
  | OpenRGB: verbonden, 5 lampen | Verbonden; OpenRGB gaf zoveel apparaten |
  | OpenRGB: verbonden, geen lampen gevonden | Verbonden, maar OpenRGB gaf geen apparaten |
  | OpenRGB: niet bereikbaar | Niets nam de verbinding aan op `127.0.0.1:6742`: OpenRGB draait niet of de SDK-server staat uit. Tip: herstart de pc of draai de installer opnieuw |
  | OpenRGB: geen goed antwoord | Er antwoordde wel iets, maar verbinden of de apparaten ophalen mislukte |

- **Gesimuleerde lampen:** bij "Gesimuleerde lampen tonen"
  kies je "Automatisch" (standaard: alleen zolang OpenRGB geen echte lampen levert), "Altijd"
  of "Nooit". De engine wisselt meteen en bewaart de keuze. Gesimuleerd zijn er een strip
  (30 LED's), een fan (12), een toetsenbord (104, met de plek en naam van elke toets), een
  moederbord (7, in drie zones, met een lege ARGB-header), twee RAM-sticks (8), een videokaart
  (13, in twee zones), een fan met twee ringen (20), een ARGB-controller (20, in twee
  kanalen), verlichting in de behuizing (10), een muis (12, in vier zones) en een headset (2,
  in twee zones).
- **Opslaan:** wijzigingen worden automatisch opgeslagen; hier staat of dat gelukt is.

### Aan en uit

Elke lamp, en met "Hele huis" alles tegelijk, kan aan en uit (de schakelaars op Lampen en
naast de lampkeuze bij Kleur). Een lamp die uit is, wordt zwart en houdt zijn kleur, effect
en helderheid voor als hij weer aan gaat. De grote schakelaar staat aan zolang er minstens
één lamp aan is. Met alles uit loopt er geen timer in de engine, ook niet als het effect een
regenboog is. "Laat knipperen" werkt ook bij een lamp die uit is; daarna is hij weer zwart.

### Ontwerp en bewuste afwijkingen

De app volgt `design/DESIGN.md`. Bewust anders, of (nog) niet gebouwd:

- **Hele huis:** de strip toont de echte kleuren van de lampen, niet de vaste regenboog van het
  ontwerp; zo zegt hij iets over het huis.
- **Knoppen** hebben ronde hoeken (12 px) in plaats van pilvormen.
- **Scènes = profielen:** wat in de app een scène heet, is in de code en in `profiles.json`
  een profiel. Een scène als "actief" markeren (de rand uit het ontwerp) kan nog niet, en elke
  kaart heeft een moersleutel om hem aan te passen.
- **Buren** is nog leeg, want nog niet ontworpen (§ 5.4). De buren-kaart op Lampen en
  "Samen aan de slag" op Scènes zijn weggelaten, omdat er nog niets achter zit.
- **Kleur** heeft, zoals het ontwerp, de schuiven R, G en B en zet de kleur pas met "Op de
  strip zetten" op de lamp; dat vervangt de schuiven Tint en Verzadiging van de oude app,
  die een kleur meteen toepasten. Extra ten opzichte van het ontwerp: "Licht en beweging"
  (helderheid, effect, snelheid) en het LED-voorbeeld met de per-LED-editor.
- **Lampkaarten** tonen het type met de helderheid in plaats van "Ruimte · helderheid": een
  ruimte kent BuurmansRgb niet.
- WPF kent geen letterafstand, dus de −0,02em van de koppen ontbreekt; de navigatiebalk is
  niet uitklapbaar.

## Tray-icoon

De engine draait zonder venster, met een tray-icoon rechtsonder bij de klok.

- **Menu:** "Instellingen openen", "Scène aanzetten", "Effecten pauzeren", "Starten met
  Windows" en "Afsluiten". Dubbelklik op het icoon opent ook de instellingen.
- **Scène aanzetten** toont de opgeslagen scènes; klik er een aan. Zonder scènes staat er
  "Nog geen scènes – zet ze in elkaar in de instellingen". De lijst is altijd actueel. Kan
  `profiles.json` niet gelezen worden, dan is het grijs en meldt het icoon dat; ook de eerste
  seconden na het starten is het grijs, tot de engine de opgeslagen verlichting heeft gezet.
- **Tooltip:** "buurmansRgb" (of "buurmansRgb – effecten gepauzeerd") en daaronder de
  OpenRGB-status, bijv. "OpenRGB: verbonden, 5 lampen".
- **Starten met Windows** zet `BuurmansRgb` in
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; nogmaals klikken haalt hem weg.
- **Eén instantie:** start je de engine nog een keer, dan stopt die meteen en vraagt de
  draaiende engine om de instellingen-app (`BuurmansRgb.UI.exe` naast de engine) te openen.
  Lukt dat niet (bijv. geblokkeerd door SmartScreen), dan meldt het icoon dat. Elke ingelogde
  Windows-gebruiker heeft zijn eigen engine.

### Pauzeren

De engine pauzeert de bewegende effecten om drie redenen, die elk op zichzelf aflopen:

| Reden | Begint | Eindigt | Wat de lampen doen |
| --- | --- | --- | --- |
| Tray-menu | "Effecten pauzeren" aanvinken | Uitvinken, of "Hervatten" in de instellingen-app | Blijven zoals ze zijn; de engine stuurt niets tot hervatten |
| Vergrendeld | Windows vergrendelen (Windows-toets+L), van gebruiker wisselen, of een verbroken extern bureaublad | Ontgrendelen, of de sessie is weer op een scherm | Het laatste frame blijft staan |
| Slaapstand | De pc gaat slapen of in sluimerstand | Wakker worden (of ontgrendelen) | Het laatste frame blijft staan |

- Bij elke reden stopt de animatie en kost hij geen CPU meer; de lampen gaan niet uit.
  Scènes op tijd of bij een programma en het verbinden met OpenRGB lopen gewoon door.
- De redenen staan los van elkaar: ontgrendelen heft een pauze uit het tray-menu niet op.
  Eindigt een reden, dan stuurt de engine alle kleuren nog een keer.
- Tijdens vergrendelen en slaapstand gaat een wijziging wel naar de lampen, bijv. een scène op
  tijd of het herstellen na het ontwaken. Alleen de pauze uit het tray-menu houdt alles tegen.
  Knipperen gebeurt bij geen enkele pauze.
- In de instellingen-app staat bovenaan Lampen en Kleur een melding met de reden, bij de pauze
  uit het tray-menu met de knop "Hervatten". Eindigt de pauze, dan staat er 5 seconden
  "Effecten lopen weer.". Het vinkje in het tray-menu hoort alleen bij de pauze uit het menu.
- Uitzetten: `"pauseOnLockAndSleep": false` in `settings.json` en de engine opnieuw starten.

## Instellingenbestanden

Beide bestanden staan in `%AppData%\BuurmansRgb\`. Bij de eerste start schrijft de engine de
standaardwaarden weg. Wijzigingen uit de app slaat de engine een seconde na de laatste
wijziging op, en bij afsluiten. Met de hand aanpassen kan ook, terwijl de engine niet draait.

### settings.json

```json
{
  "color": "#FF8800",
  "brightness": 0.8,
  "effect": "rainbow",
  "speed": 1.5,
  "colorLoop": { "colors": ["#FF0000", "#FF00FF", "#0000FF"], "spread": true },
  "on": true,
  "devices": {
    "simulated-fan": { "color": "#0000FF", "brightness": 0.5, "effect": "per-led", "speed": 1 },
    "simulated-headset": { "color": "#FF8800", "brightness": 0.8, "effect": "static", "on": false }
  },
  "ledColors": {
    "simulated-fan": ["#FF0000", null, "#0000FF"]
  },
  "ledColorZoneLengths": {
    "simulated-fan": [12]
  },
  "simulatedDevices": "automatic",
  "framesPerSecond": 30,
  "pauseOnLockAndSleep": true,
  "zoneLengths": {
    "simulated-argb": { "ARGB 2": 15 }
  }
}
```

| Veld | Betekenis |
| --- | --- |
| `color` | `#RRGGBB` |
| `brightness` | 0 tot 1 |
| `effect` | Een van de effecten uit de tabel bij "Effecten"; onbekend wordt `static` |
| `speed` | Snelheid van een bewegend effect: 1 is normaal, van 0,1 tot 3 |
| `colorLoop` | De 2–8 kleuren van de Kleurenloop; `spread` op `false` voor alle LED's tegelijk. Zonder (of met te weinig of te veel kleuren): geel, groen, blauw |
| `on` | `false` als de lamp (of alles) uit staat; zonder is hij aan |
| `devices` | Per lamp de velden hierboven, alleen voor lampen die afwijken van de algemene waarden |
| `ledColors` | Per lamp een kleur per LED voor het effect `per-led`; `null`, of een LED voorbij het eind van de lijst, volgt de kleur van de lamp |
| `ledColorZoneLengths` | Per lamp de lengte van elke zone (in volgorde) waarvoor die kleuren gekozen zijn, zodat ze een zone met een andere lengte volgen |
| `simulatedDevices` | `"automatic"` (standaard), `"always"` of `"never"`. Iets anders leest als `"automatic"`; oud `true` is `"automatic"`, oud `false` is `"never"` |
| `framesPerSecond` | 1–60, standaard 30: de framerate van bewegende effecten |
| `pauseOnLockAndSleep` | Standaard `true`; `false` laat de effecten doorlopen bij vergrendelen en slaapstand |
| `zoneLengths` | Per lamp de lengte die je een instelbare zone gaf, op de naam van de zone |

- Een lamp van OpenRGB heet bijvoorbeeld `openrgb-corsair-vengeance-pro-rgb-1a2b3c4d`: de naam
  plus een code uit de locatie en het serienummer, zodat hij na een herstart dezelfde blijft.
- Een waarde in `zoneLengths` of `ledColorZoneLengths` die geen getal is, slaat de engine over
  (bij `ledColorZoneLengths` de hele lijst van die lamp); de rest blijft gelden.
- Is het bestand geen geldige JSON of klopt er iets anders niet (bijv. een kleur als
  `"purple"`), dan zet de engine het opzij als `settings.json.broken` en draait hij op de
  standaardwaarden. Verbeter de fout en zet het terug als `settings.json` terwijl de engine
  niet draait. Kan de engine het bestand niet lezen (bijv. omdat een ander programma het open
  heeft), dan laat hij het ongemoeid.

### profiles.json

De scènes, met per scène een naam, de lichtvelden van `settings.json` (`color` t/m
`ledColorZoneLengths`) en, als hij vanzelf aangaat, `loadAt` (`"HH:mm"`) en/of `program`:

```json
{
  "profiles": [
    { "name": "Gamen", "color": "#FF0000", "effect": "rainbow", "program": "eldenring.exe" },
    { "name": "Nacht", "color": "#FF8800", "brightness": 0.1, "loadAt": "23:00" },
    { "name": "Werk", "color": "#FFFFFF", "devices": { "simulated-fan": { "color": "#0000FF" } } }
  ]
}
```

Zonder `loadAt` en `program` gaat een scène nooit vanzelf aan. Een tijd of programma dat niet
te lezen is (bijv. `"25:00"` of een getal), laat de engine weg; de rest blijft gelden. Is het
bestand geen geldige JSON, dan zet de engine het opzij als `profiles.json.broken` en begint
hij met een lege lijst.

## OpenRGB en hardware

### OpenRGB koppelen

De installer regelt dit (OpenRGB als service). Zonder installer:

1. Sluit fabrikantsoftware af die dezelfde LED's aanstuurt (iCUE, Armoury Crate, MSI Center,
   RGB Fusion, ...).
2. Start OpenRGB en zet de SDK-server aan: tabblad **SDK Server** → **Start Server**, of start
   OpenRGB met `--server`. BuurmansRgb verbindt met `127.0.0.1:6742`, de standaardpoort.
   Voor moederbord, RAM en videokaart moet OpenRGB de eerste keer als administrator draaien
   (SMBus); BuurmansRgb zelf heeft geen adminrechten nodig.
3. Start de engine (de volgorde maakt niet uit). Binnen enkele seconden staan de lampen van
   OpenRGB in de app en verdwijnen de gesimuleerde (bij "Automatisch").

- Bij het verbinden zet BuurmansRgb elk apparaat in de modus "Direct" of "Custom". Een apparaat
  zonder zo'n modus (vaak een videokaart) krijgt "⚠ Volgt de kleur misschien niet": het kan
  zijn eigen effect blijven tonen; stel het dan in OpenRGB zelf in.
- Stopt OpenRGB, dan probeert de engine elke 5 seconden opnieuw te verbinden en stuurt daarna
  de kleuren opnieuw. De eigen kleur van een lamp blijft bewaard.
- In de clientlijst van OpenRGB staat BuurmansRgb twee keer: `BuurmansRgb` stuurt de kleuren,
  `BuurmansRgb watcher` merkt op wanneer OpenRGB stopt of opnieuw zoekt.
- Details over verbinden, protocolversie en fouten gaan naar de debug-uitvoer (bekijken met
  DebugView); er is nog geen logbestand.

### Per apparaattype

- **Moederbord en RAM** bereikt OpenRGB via SMBus (eerste keer als administrator). OpenRGB
  toont elke RAM-stick als eigen apparaat, vaak met dezelfde naam; BuurmansRgb nummert ze in de
  volgorde van OpenRGB ("ENE DRAM 1", "ENE DRAM 2").
- **ARGB-headers** (op het moederbord, een videokaart of een controller) hebben in OpenRGB vaak
  0 LED's tot je de lengte instelt, in OpenRGB of in het voorbeeld bij Kleur.
- **Videokaarten** kennen in OpenRGB vaak alleen hun eigen effecten (geen "Direct"); zie de
  waarschuwing hierboven. Twee dezelfde kaarten krijgen een nummer, met hun bus eronder.
- **Fans, strips en behuizing** hangen meestal aan een ARGB-header of een controller (bijv.
  een Corsair Commander of een Lian Li-hub) die OpenRGB via USB bereikt. Controllers met
  ARGB-kanalen heten "LED-strip", zo noemt OpenRGB ze.
- **Toetsenbord, muis en headset** gaan via USB. Regenboog en Golf lopen over de LED-volgorde
  van OpenRGB, niet over de plek van de toetsen. OpenRGB ondersteunt maar een deel van de
  headsets (zie "Supported Devices" op [openrgb.org](https://openrgb.org)); een headset die
  OpenRGB niet kent, staat ook niet in BuurmansRgb, en er komt geen eigen driver.
- **Overige types:** muismat, headsetstandaard, speaker, gamepad, microfoon en keypad hebben
  een eigen naam en icoon. Lampen, virtuele apparaten, "accessory" en "storage" staan zonder
  type. OpenRGB.NET 3.1.1 kent "case", "microphone" en "keypad" niet bij naam; BuurmansRgb
  herkent ze aan hun nummer.
- **Na slaapstand** gaan veel apparaten terug naar hun eigen effect. Daarom zet de engine 2 en
  10 seconden na het ontwaken de OpenRGB-apparaten opnieuw klaar en stuurt hij alle kleuren
  opnieuw (bij een pauze uit het tray-menu pas bij hervatten). Een USB-apparaat dat je opnieuw
  aansluit, ziet OpenRGB mogelijk pas nadat het opnieuw naar apparaten heeft gezocht.
- **Zonelengtes:** een lengte die je in BuurmansRgb instelt, zet de engine terug als OpenRGB hem
  vergeten is (bijv. na een herstart van OpenRGB), op de naam van de zone. Neemt OpenRGB hem niet
  over, dan blijft het bij één poging tot het apparaat opnieuw verschijnt. Verander je de lengte
  in OpenRGB zelf, dan neemt BuurmansRgb die over, behalve 0.

### Welk apparaat is wat?

- **Knipperen:** open je een lamp (een klik op zijn kaart, of de lampkeuze bij Kleur), dan
  knippert hij op de hardware 1,5 seconde: drie keer fel wit en uit, ook bij een lage
  helderheid. Daarna krijgt hij zijn eigen verlichting terug. "Laat knipperen" in het
  voorbeeld doet het nog eens. Er knippert steeds één lamp; knipperen wordt niet opgeslagen,
  en bij een pauze knippert er niets.
- **Plek:** staan er twee of meer lampen van hetzelfde type en met dezelfde naam, zoals de
  RAM-sticks van een kit, dan staat op hun kaart het deel van hun OpenRGB-locatie dat verschilt:
  het adres ("Adres 0x52"), of anders de bus ("Op NVIDIA GPU 1"). De hele locatie staat in de
  tooltip van de kaart. Welke welke is in de pc, zie je met "Laat knipperen".
- **Echt of gesimuleerd:** elk onder een eigen kop, zie "Lampen".

## Zelf testen op echte hardware

Vergrendelen, slaapstand en echte apparaten zijn niet na te bootsen in de tests; deze stappen
doe je zelf. Noteer wat werkt in [docs/hardware.md](docs/hardware.md).

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

### Scènes

1. Maak scènes "Werk", "Nacht" en "Gamen" en wissel via het tray-menu en met een klik op hun
   kaart; kijk of alle apparaten meegaan.
2. Geef "Nacht" een tijd een paar minuten verder en wacht. Zet daarna een tijd, laat de pc
   erover heen slapen en kijk dat de scène na het ontwaken niet alsnog komt.
3. Geef "Gamen" de naam van je spel, start het spel en sluit het weer: de verlichting van
   daarvoor hoort terug te komen. Verander tijdens het spel iets en kijk dat dat blijft.
4. Start een spel dat als administrator draait of anti-cheat heeft, en kijk of de engine het
   ziet.
5. Sluit de engine af (tray, "Afsluiten") terwijl het spel draait: de verlichting van daarvoor
   hoort terug te komen. Start de engine weer: de scène van het spel komt terug, en na het
   sluiten van het spel weer de verlichting van daarvoor.

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
5. Zet `"pauseOnLockAndSleep": false`, herstart de engine en vergrendel: de regenboog loopt
   door.

## Installer bouwen en testen

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build.ps1 -Version 1.2.3
```

`installer\build.ps1` bouwt `artifacts\installer\out\BuurmansRgb-setup.exe` (Inno Setup 7.1,
ongeveer 77 MB): engine en instellingen-app self-contained, de MSI van OpenRGB 1.1 (installeert
de service `OpenRGB`) en de setup van PawnIO 2.2.0. Het script controleert de SHA-256 van beide
downloads en haalt de Inno Setup-compiler van NuGet; met `-Iscc <pad>` gebruik je een eigen
`ISCC.exe`. Zonder `-Version` is de versie 1.0.0. Alles komt in `artifacts\installer`, dat git
negeert. Hoe het werkt en waarom: [docs/openrgb-meeleveren.md](docs/openrgb-meeleveren.md).

- **Na installeren** start de installer de engine als de gewone gebruiker met `--first-run`.
  Die zet "Starten met Windows" aan, alleen als dat nog uit staat.
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

## Voor ontwikkelaars

### Projecten

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

### Tests

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
| Instellingen | `JsonSettingsStoreTests`, `EngineSettingsTests`, `LightingSettingsSaverTests`, `EngineHostTests` |
| OpenRGB | `OpenRgbDeviceMapperTests`, `OpenRgbDeviceProviderTests`, `OpenRgbNetConnectionTests`, `OpenRgbProviderStatusTests`, `OpenRgbProviderServerTests`, `OpenRgbServerWatcherTests` |
| Gesimuleerde lampen | `SimulatedDeviceProviderTests`, `SimulatedDevicesSwitchTests`, `SwitchableDeviceProviderTests` |
| Zones en lengtes | `RgbZoneTests`, `LedMatrixTests`, `LightingControllerZoneTests`, `LedColorStoreTests`, `ZoneLengthKeeperTests`, `ZoneLengthKeeperOpenRgbTests`, `ZoneLengthViewModelTests` |
| Knipperen en plek | `LightingControllerFlashTests`, `DeviceInfoTests`, `DeviceLocationLabelsTests` |
| Aan en uit | `LightingControllerOnTests`, `LampsViewModelTests`, `MainViewModelLampTests` |
| Scènes | `ProfileLibraryTests`, `JsonProfileStoreTests`, `DailyTimeTests`, `ProfileTimerTests`, `ProfileTriggersTests`, `ProgramWatcherTests`, `ProcessListTests`, `ProfilesViewModelSceneTests`, `SceneCardTests` |
| Pauzeren en slaapstand | `LightingControllerPauseTests`, `LightingControllerRestoreTests`, `AutoPauseTests`, `ResumeRestorerTests`, `SystemSessionEventsTests`, `EffectsPauseViewModelTests` |
| Tray en opstarten | `TrayPresenterTests`, `SingleInstanceTests`, `EngineOptionsTests`, `AutoStartExtensionsTests` (nooit tegen de echte Run-sleutel), `RegistryAutoStartTests` |
| IPC | `EngineApiTests`, `EngineIpcServerTests`, `EngineRpcTests` (over een echte named pipe) |
| Voorbeeld | `DevicePreviewViewModelTests`, `PreviewViewModelTests`, `LedLayoutTests`, `RingLayoutTests`, `WideKeysTests`, `LedPreviewViewTests` (UI Automation), `FocusKeeperTests` en `SliderDragTests` (met een echt venster buiten beeld) |
| Pagina's | `NavigationViewModelTests`, `ColorPickerViewModelTests`, `MainViewModelTests` en de andere `MainViewModel*Tests` |

### Met de gesimuleerde lampen bekijken

Start de engine en de instellingen-app zonder OpenRGB en loop de pagina's langs met muis en
toetsenbord. Een paar dingen om te proberen:

- Kies "Regenboog": het voorbeeld en de strip bij "Hele huis" lopen mee.
- Kies bij Kleur "Simulated ARGB controller" en klik bij "ARGB 2" een paar keer op + of sleep
  de schuif. Sluit de engine af (tray, "Afsluiten") en start hem opnieuw: "ARGB 2" heeft nog
  steeds die lengte.
- Kies "Simulated dual-ring fan" en klik op "Outer ring"; kies "Simulated keyboard", wijs een
  toets aan voor zijn naam en klik op het brede deel van de spatiebalk.
- Zet twee scènes in elkaar en kies er een in het tray-menu; geef een scène een tijd een minuut
  verder, of `notepad.exe`, en start en sluit Kladblok.
- Vink in het tray-menu "Effecten pauzeren" aan: de melding met "Hervatten" verschijnt.
- Zet bij Instellingen "Nooit": de gesimuleerde lampen verdwijnen meteen.

### Hoe de engine werkt

- **Render-loop:** alleen als minstens één lamp een bewegend effect toont, draait een
  `PeriodicTimer` op `framesPerSecond`; ook tijdens het knipperen. Een frame stuurt alleen de
  lampen waarvan de kleuren veranderden, en alloceert niets zolang de instellingen-app dicht
  is (met de app open maakt de engine per frame een kopie voor het voorbeeld). Gaat een frame
  mis, dan probeert het volgende het opnieuw.
- **IPC:** named pipe `BuurmansRgb.Engine.<sessie-id>` (alleen voor de eigen Windows-gebruiker)
  met StreamJsonRpc; de interface is `IEngineApi` in `BuurmansRgb.Contracts`. De engine meldt
  wijzigingen met events (`LightingChanged`, `DevicesChanged`, `PauseChanged`,
  `OpenRgbStatusChanged`, `SettingsSaved`, `FrameRendered`). Na het opstarten en na het sluiten
  van de laatste instellingen-app geeft de engine zijn ongebruikte geheugen terug aan Windows.
- **Pauzeren:** via `SystemEvents.SessionSwitch` en `SystemEvents.PowerModeChanged` op de thread
  van het tray-icoon (`AutoPause`, `SystemSessionEvents`, `SystemPowerEvents`). Bij het starten
  vraagt de engine één keer of Windows al vergrendeld is (`WTSQuerySessionInformation`).
- **Programma's zien:** zonder adminrechten meldt Windows niet dat een programma start. Daarom
  kijkt de engine elke 2 seconden in de proceslijst (`EnumProcesses`, de naam één keer per
  nieuw proces), alleen zolang een scène een programma heeft. Dat kost minder dan 0,1 ms per
  ronde, waar `Process.GetProcessesByName` of een Toolhelp-snapshot 5 tot 7 ms kosten en WMI
  `System.Management` in de engine haalt. Programma's die als administrator draaien, ziet hij
  ook.
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

### Ontwerp, lettertypes en icoon

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

### Metingen

Gemeten op de ontwikkel-pc; "één kern" is het percentage van één CPU-kern.

| Wat | Situatie | CPU | Geheugen |
| --- | --- | --- | --- |
| Engine | Statisch, app dicht | ~0,07 % van één kern | 21–24 MB working set, 12–13 MB private |
| Engine | Regenboog op 30 fps, app dicht | ~0,35 % van één kern (~0,02 % van de pc) | 22–27 MB working set, 12,5–13 MB private |
| Engine | Alles uit, met regenboog als effect | 0,00–0,12 % van één kern, geen render-timer | 19–28 MB working set, 12–13 MB private |
| Engine | Vergrendeld of slaapstand | 0 frames, geen timer | |
| Engine | Een scène met een programma | ~0,05 % van één kern extra (~100 miljoen CPU-cycli per minuut) | ~0,3 MB voor de procesnamen |
| Engine | Tien scènes met een tijd en eigen kleuren | | ~51 KB op de heap |
| App (Debug) | Op Lampen, regenboog | ~2,4 % van één kern | 113 MB working set, 39 MB private |
| App (Debug) | Op Kleur met "Hele huis", regenboog | ~2,1 % van één kern | 133 MB working set, 43 MB private |
| App (Debug) | Statisch | 0 % | 112–145 MB working set |

De engine is gemeten in Release met de gesimuleerde lampen en zonder OpenRGB (dan probeert hij
elke 5 seconden te verbinden; dat is het grootste deel van de CPU bij een statische kleur).
Meet allocaties met de app dicht. De metingen van de app zijn gedaan terwijl Windows vergrendeld
was; dan tekent WPF niets, dus het tekenen zelf (ook van de gloed) zit er niet in.

## Mappen en documenten

- `src/` – de projecten; `tests/` – de tests, zonder hardware
- `installer/` – `build.ps1`, `BuurmansRgb.iss`, de Sandbox-test en `THIRD-PARTY-NOTICES.txt`
- `design/` – het ontwerppakket van de instellingen-app (`DESIGN.md`, icoon, referentieschermen)
- [docs/bouwplan.md](docs/bouwplan.md) – architectuur, fasering, taken en uitbreidingen
- [docs/installatie.md](docs/installatie.md) – installeren, bijwerken en verwijderen in het kort
- [docs/openrgb-meeleveren.md](docs/openrgb-meeleveren.md) – waarom de installer OpenRGB en
  PawnIO zo meelevert
- [docs/hardware.md](docs/hardware.md) – jouw hardware en wat werkt
- [docs/ideas.md](docs/ideas.md) – ideeën voor later
- [docs/prompts.md](docs/prompts.md) – kant-en-klare opdrachten voor agents
- `AGENTS.md` – werkafspraken voor agents
