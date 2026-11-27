# buurmansRgb – ontwerpspecificatie (richting A, "Werkplaats bij nacht")

Dit document beschrijft het gekozen ontwerp voor de app **buurmansRgb**, een app om RGB-ledlampen in huis te bedienen, met een "klussen met de buren"-sfeer. Het ontwerp is gemaakt als telefoonmockup (390 × 844). Voor de Windows-versie geldt: **zelfde stijl, kleuren, typografie en componenten, maar de indeling aangepast aan een desktopvenster** (zie § 6).

Bestanden in dit pakket:

| Pad | Wat |
|---|---|
| `icon/buurmansrgb.ico` | Windows-icoon (16, 24, 32, 48, 64, 128, 256 px). Gebruik voor de .exe, het venster en de taakbalk. |
| `icon/buurmansrgb-icon.svg` | Volledig app-icoon (bron, lichte achtergrond). Gebruik vanaf 64 px. |
| `icon/buurmansrgb-icon-small.svg` | Vereenvoudigde versie voor ≤ 48 px (geen pleistergaatjes, geen gloed). |
| `icon/buurmansrgb-icon-small-dark.svg` | Vereenvoudigde versie op donker (#24242B), voor in de app zelf (titelbalk / navigatie). |
| `icon/buurmansrgb-icon-{64,256,512,1024}.png` | Gerenderde PNG's, bijv. voor MSIX-assets of een installer. |
| `screens/Main.dc.html` | Referentie: Lampen-scherm (home). |
| `screens/Kleur.dc.html` | Referentie: Kleur mengen (met werkende RGB-schuifjes). |
| `screens/Scenes.dc.html` | Referentie: Scènes. |
| `screens/KleurLagen.dc.html` | Ontwerp voor nacht 6: Kleur in lagen (§ 9), als desktopvenster in drie staten. Gewone HTML zonder template-runtime: opent zo in een browser. |

De `.dc.html`-bestanden zijn **ontwerpreferenties, geen draaibare code**: ze gebruiken een eigen template-runtime (`{{hole}}`-plaatshouders en `<sc-for>`-lussen). De data die in de plaatshouders komt staat onderaan elk bestand in het `<script type="text/x-dc">`-blok (`renderVals()`). Lees ze voor exacte maten, kleuren en teksten. De uitzondering is `KleurLagen.dc.html`: gewone HTML met een gewoon script onderaan, zonder template-runtime. Hij houdt de naam `.dc.html`, zodat alle ontwerpschermen in `screens/` bij elkaar horen.

---

## 1. Kleuren (design tokens)

| Token | Waarde | Gebruik |
|---|---|---|
| `bg` | `#16161A` | Achtergrond van het venster |
| `surface` | `#212128` | Kaarten, lijstitems |
| `surface-raised` | `#24242B` | Icoonachtergrond, knoppen met icoon |
| `nav-bg` | `#1A1A1F` | Navigatiebalk |
| `border` | `#2E2E36` | 1 px rand rond kaarten en knoppen, scheidingslijnen |
| `border-strong` | `#4A4A54` | Gestippelde rand van "+ nieuwe"-knoppen |
| `text` | `#F4F1EA` | Hoofdtekst (warm gebroken wit) |
| `text-muted` | `#A9A6A0` | Subtekst, labels, inactieve navigatie |
| `led-off` | `#3A3A42` | Uit-toestand van lampjes en schakelaars |
| `knob-off` | `#8C8A85` | Knop van schakelaar in uit-stand |
| `preview-bg` | `#0E0E11` | Achtergrond achter het ledstrip-voorbeeld |
| `strip-bg` | `#26262D` | De "printplaat" van de ledstrip |

**LED-kleuren** (worden op donker gebruikt, altijd met gloed – zie § 4):

| Naam | Waarde |
|---|---|
| Rood | `#FF5A5A` |
| Oranje | `#FF9A3D` |
| Geel | `#FFD84D` |
| Groen | `#3DDC84` |
| Cyaan | `#3DD6D0` |
| Blauw | `#5B93FF` |
| Paars | `#A97BFF` |
| Klus-wit | `#FFF4E5` |

**Buren-accent** (alles wat met de buurman te maken heeft):

| Token | Waarde |
|---|---|
| `buren-bg` | `#1C2433` |
| `buren-border` | `#2B3B57` |
| `buren-text-muted` | `#A9B8D4` |
| `buren-accent` | `#5B93FF` (knop "Samen", tekst op knop `#0E1420`) |
| `buren-icon` | `#A9C9FF` |

Er is geen lichte modus in richting A: de app is bewust altijd donker, zodat de ledkleuren gloeien.

## 2. Typografie

- **Koppen en tekst:** *Bricolage Grotesque* (Google Fonts, SIL Open Font License – mag meegeleverd worden). Gewichten 400, 600, 800. Fallback: `Segoe UI Variable`, `Segoe UI`, sans-serif.
- **Getallen en kleurcodes:** *IBM Plex Mono* 500 (OFL). Fallback: `Cascadia Mono`, `Consolas`, monospace.
- Lever beide fonts mee met de app; ga er niet van uit dat ze op de pc staan.

| Rol | Grootte | Gewicht | Extra |
|---|---|---|---|
| Schermtitel (Scènes) | 30 px | 800 | letter-spacing −0.02em |
| Grote waarde ("3 van 4 aan") | 26 px | 800 | −0.02em |
| Woordmerk / schermtitel klein | 22 px | 800 | −0.02em |
| Sectiekop ("Lampen in huis") | 18 px | 600 | |
| Kaarttitel | 16–17 px | 600–700 | |
| Brood/knoptekst | 15 px | 600 | |
| Subtekst | 13 px | 400 | kleur `text-muted` |
| Navigatielabel | 12 px | 400 (actief 600) | |
| Kleurcode (hex) | 30 px | Plex Mono 500 | |

### Woordmerk

`buurmans` in `text`, gevolgd door **R** in `#FF5A5A`, **g** in `#3DDC84`, **b** in `#5B93FF`. Bricolage Grotesque 800, 22 px, letter-spacing −0.02em. Links ervan het kleine donkere icoon (`buurmansrgb-icon-small-dark.svg`, 38 px), 10 px tussenruimte.

## 3. Maatvoering

- Basisraster 4 px. Veelgebruikte afstanden: 4, 6, 10, 12, 14, 16, 18, 20, 22 px.
- Paginamarge (telefoon): 20 px links/rechts. Op desktop: zie § 6.
- Afronding: grote kaart 22 px, lijstitem/scènekaart 18 px, preset-tegel 12 px, ledstrip-plaat 8 px, pillen en schakelaars volledig rond.
- Minimale klikgrootte 44 × 44 px (ook op desktop aanhouden; het voelt bij deze stijl goed aan).

## 4. Componenten

**Kaart** – `surface`, 1 px `border`, radius 18 (lijst) of 22 (groot), padding 14–18 px. Geen schaduw.

**LED-bolletje** – cirkel in de ledkleur met een gloed: `box-shadow: 0 0 16px 2px <kleur>` (lijstitem, 38 px), `0 0 10px 2px` (strip, 12 px), `0 0 14px 4px` (kleurvoorbeeld). Uit: kleur `led-off`, geen gloed. In WinUI/WPF: gebruik een `DropShadow`/`Effect` met blur in dezelfde kleur, of een radiale gradient.

**Ledstrip-voorbeeld** – rij van 12–14 gelijkverdeelde LED-bolletjes op `strip-bg` (hoogte 22 px, radius 8) of direct op de kaart.

**Schakelaar** – 52 × 32 (groot: 60 × 36), padding 4, knop 24 (28). Aan: spoor in de kleur van die lamp (of `text` voor "hele huis"), knop `bg`. Uit: spoor `led-off`, knop `knob-off`. Toegankelijk als toggle (aria-pressed / ToggleSwitch).

**Primaire knop** – pil, min-hoogte 52, achtergrond `text`, tekst `bg`, 17 px/800 ("Op de strip zetten").

**Knop** (gewone knop) – voor elke actie die niet de hoofdactie van een blok is ("Knipperen", "Tellen", "Toepassen", "Hervatten", "Details"). Vlak `surface`, 1 px `border`, radius 12, min. 44 × 44, padding 16 px opzij, tekst 15 px/600 in `text`. Onder de muis wordt de rand `border-strong`, ingedrukt het vlak `surface-raised`; met de toetsenbordfocus de ring van 2 px in `text`, 3 px buiten de knop. Een knop die aan/uit-staat aangeeft (Details, Uitleg, een dag) is ingedrukt een vlak in `text` met tekst in `bg`. Een knop die nu niets doet, houdt zijn vorm: tekst in `#93918C`, een vlak dat de kaart laat zien en de rand van een kaart, met een pijl als muisaanwijzer en zijn tooltip. Een ronde knop met één teken of icoon (−, +, ←, →) is 44 × 44 met radius 22. In een contrastthema: `ButtonFace` met `ButtonText`, onder de muis een rand in `Highlight`, uit `GrayText`. Een knop zegt in gewone woorden wat hij doet (§ 8).

**Link** – één stijl op elke pagina, voor een actie in de tekst ("Hoe komt een apparaat erbij?", "Verversen", "+ Eigen effect maken", de naam van een zone in het voorbeeld, "Alles selecteren"): tekst 14 px (in het voorbeeld 13 px, als subtekst) in `#7FB0FF`, **altijd onderstreept**, ook zonder muis, zodat een link niet alleen aan zijn kleur te herkennen is (WCAG 1.4.1). Geen vlak en geen rand; het klikvlak is minstens 44 px hoog. Onder de muis wordt hij lichter (`#A9C9FF`), met de toetsenbordfocus krijgt hij de ring van 2 px in `text` om zijn klikvlak (dus minstens 44 px hoog, niet strak om de tekst). Een link die nu niets doet, is grijs (`text-muted` op 60 %) zonder streep, zodat hij niet meer op een link lijkt. Een lange link loopt om naar een volgende regel. Een schermlezer noemt hem een link (Verteller zegt "link"), zoals hij eruitziet, en hij werkt met Enter en Spatie zoals een knop. In een contrastthema heeft hij `HotTrack`, onder de muis `Highlight`, de streep steeds in de kleur van de tekst; een link die nu niets doet heeft daar `GrayText`, zonder doorzichtigheid, zoals de tekst van een uitgeschakelde knop.

**Altijd zichtbaar** – knoppen en links staan er altijd, ook de kleinere acties naast een naam of in de kop van een blok. Niets verschijnt pas onder de muis of met de focus: wie met het toetsenbord, een schermlezer, een touchscreen of een vergrootglas werkt, moet zien wat kan. Kan een actie nu niet, dan is hij grijs met zijn tooltip, of houdt hij zijn plek vrij, zodat de rest niet verspringt.

**Buren-knop** – pil, min-hoogte 44, `buren-accent` met donkere tekst ("Samen").

**"+ nieuw"-knop** – pil, 2 px gestippelde rand `border-strong`, transparant ("+ Nieuwe scène in elkaar zetten").

**Preset-tegel** – 44 px hoog, radius 12, gevuld met de kleur, 1 px `border`; geselecteerd: 3 px rand in `text`.

**Stepper** – voor een getal dat je in kleine stappen instelt, zoals de lengte van een zone: van links naar rechts de ronde knop **−**, een schuif, de ronde knop **+**, een veld met het getal (IBM Plex Mono, net breed genoeg voor vier cijfers) en de gewone knop **Toepassen**, met het label erboven als subtekst. − en + herhalen zolang je ze ingedrukt houdt; de schuif en het veld gaan ook met de pijltjes en Enter. Elk onderdeel heeft een eigen naam voor een schermlezer met wat het instelt ("Aantal LED's van Board – ARGB 1 verlagen"). Een getal buiten het bereik geeft geen fout in een kleur, maar een zin onder het veld met ⚠ ("Kies 0 tot 120 LED's."), die een schermlezer voorleest, en Toepassen is dan grijs. Waar een schuif niet past, zoals de vorige en volgende LED bij Tellen, staan alleen − en + naast het getal.

**Kop van een zone** – in het voorbeeld heeft elke zone een kop: links de naam als link (die de zone kiest; bij een apparaat met één zone "Alles selecteren"), rechts op een vaste plek **Tellen** en **Knipperen** als gewone knoppen, Knipperen helemaal rechts, recht onder Knipperen van het apparaat. Heeft een zone Tellen maar niets te knipperen (nog geen LED's, of de enige zone van een apparaat), dan blijft de plek van Knipperen leeg, zodat Tellen nooit verspringt.

**Iconen** – lijniconen, 2 px streep, ronde uiteinden, 22 px. Geen emoji.

**Bevestiging** – een klein venster op het hoofdvenster, met dezelfde eigen titelbalk (icoon, woordmerk, kruisje), in `bg` met 1 px `border`: de vraag als sectiekop, de uitleg als subtekst, rechtsonder "Annuleren" (gewone knop, met de focus) en de primaire knop, 44 px hoog, die zegt wat er gebeurt ("Verwijderen", "Terugzetten"). Escape en het kruisje zijn Annuleren. Alleen voor wat niet vanzelf terugkomt: een scène of eigen effect verwijderen, instellingen terugzetten.

**Statusregel** – één kaart bovenaan een pagina (Lampen, Kleur) voor wat je nú moet weten, met hooguit één melding tegelijk. Een gewone kaart (`surface`, 1 px `border`, radius 18, padding 16 × 10), geen waarschuwingskleur: een pauze of een voorbeeld dat stilstaat is een keuze, geen fout. De melding is één zin in gewone taal (brood, 15 px, `text`); rechts ervan hooguit één actie als gewone knop ("Hervatten", "Opnieuw verbinden", "Laten lopen") en, zodra er meer meldingen gelden, de knop **Details**. Gelden er meer, dan staat de belangrijkste in de regel: *verbinding met de apparaten weg > andere RGB-software draait mee > effecten gepauzeerd > voorbeeld staat stil*. De verbinding gaat voor: zonder komt er toch niets op de apparaten. Andere RGB-software ("SignalRGB draait ook en kan je kleuren overschrijven.", één melding per programma, in de volgorde van de lijst van de engine) gaat voor de pauze, omdat het verklaart waarom de kleuren niet doen wat je kiest, terwijl een pauze een keuze is of vanzelf voorbijgaat. Deze melding heeft als enige twee knoppen: **Uitleg** en **Niet meer melden** (voorgoed voor dat programma), in die volgorde. Uitleg klapt onder de melding een korte uitleg open (subtekst 13 px in `text`, ook onder Details): sluit het programma af, zet zijn autostart uit en zet een service op Handmatig; met een tweede klik gaat hij weer dicht. Hij werkt als Details: een gewone knop die ingedrukt blijft zolang de uitleg open staat, zonder animatie. Zodra er een handleiding is, staat er onder de uitleg een link "Meer in de handleiding". Niet meer melden is een gewone knop; de eerste heet niet "Details", omdat die naam al bij het uitklappen van de andere meldingen hoort. Details klapt de andere uit onder de regel (subtekst 13 px in `text`, een lijn van 1 px `border` ertussen), elk met hun eigen knop, zonder animatie; de knop blijft ingedrukt (`text`-vlak, zoals een gekozen dag) zolang het open staat. Een melding die door een belangrijkere uit de regel valt, verdwijnt nooit stil: hij staat onder Details tot hij niet meer geldt, en krijgt de regel terug zodra de belangrijkere weg is. Niets te melden: geen kaart. De status van de apparaten rechtsboven op Lampen hoort er niet bij: dat is één regel subtekst in gewone taal ("5 apparaten verbonden", "Geen verbinding met je apparaten"), met de techniek (OpenRGB, SDK-server) in de tooltip en zonder verbinding de gewone knop "Herstarten". De knoppen van de Statusregel zijn een **Knop**, "Meer in de handleiding" een **Link** (zie boven); ze staan er altijd zolang hun melding geldt.

## 5. Schermen

### 5.1 Lampen (home) – `screens/Main.dc.html`
1. Kop: icoon + woordmerk links, ronde instellingenknop (moersleutel) rechts.
2. Kaart **Hele huis**: label "Hele huis", waarde "3 van 4 aan", grote schakelaar; daaronder een ledstrip in regenboogvolgorde (rood → paars).
3. Sectiekop "Lampen in huis" met link "+ Erbij knutselen".
4. Lijst met lampen: LED-bolletje, naam, subregel "Ruimte · helderheid", schakelaar. Voorbeelddata: Ledstrip woonkamer (oranje, 80%), Werkbank (klus-wit), Onder de trap (paars, 40%), Tuinhuisje (uit).
5. Buren-kaart: icoon met twee huisjes, "Buurman is online", "Zet jullie lampen in dezelfde kleur", knop "Samen".

### 5.2 Kleur mengen – `screens/Kleur.dc.html`
1. Terugknop, titel "Kleur mengen" (in de app heet de pagina nu "Kleur", besluit 8 in 9.9), subregel met de gekozen lamp.
2. Voorbeeldkaart (`preview-bg`): ledstrip van 12 bolletjes in de huidige kleur, daaronder "Huidige kleur" + hexcode (mono, 30 px) en rechts "R · G · B".
3. Drie schuifregelaars Rood / Groen / Blauw (0–255), elk met label links en waarde rechts (mono). Accentkleur van de schuif = zijn eigen kanaalkleur.
4. "Uit de gereedschapskist": 6 presets (Klus-wit, Rood, Oranje, Groen, Blauw, Paars).
5. Primaire knop "Op de strip zetten".
Gedrag: schuiven en presets werken de strip, hexcode en waarden direct bij (live voorbeeld); pas bij de knop gaat de kleur naar de lamp.
De pagina in lagen en in de volgorde van het effect, zoals nacht 6 hem bouwde: zie § 9.

### 5.3 Scènes – `screens/Scenes.dc.html`
1. Titel "Scènes", subregel "Eén tik, en het hele huis doet mee".
2. Buren-kaart "Samen aan de slag" met schakelaar en twee kleine strips naast elkaar ("Jouw huis" / "Buurman") die dezelfde kleuren tonen.
3. Raster van scènekaarten (2 kolommen): 5 bolletjes, naam, omschrijving. Klusmodus (actief, 2 px rand `text`), Filmavond, Feestje, Even bijkomen.
4. "+ Nieuwe scène in elkaar zetten".

### 5.4 Buren
Nog niet ontworpen; vierde navigatie-item. Zelfde stijl, met `buren-*`-kleuren.

## 6. Vertaling naar Windows (desktop)

- **Navigatie:** de onderbalk van de telefoon wordt een **navigatiebalk links** (rail, ~72–88 px breed, icoon boven label; of uitklapbaar ~240 px met icoon naast label). Achtergrond `nav-bg`, scheidingslijn `border`. Volgorde: Lampen, Kleur, Scènes, Buren; instellingen onderaan in de balk.
- **Venster:** donkere titelbalk in `bg` (eigen titelbalk met het kleine donkere icoon en woordmerk), minimale grootte ca. 960 × 640.
- **Inhoud:** marges 32 px, inhoud max. ~1200 px breed.
  - *Lampen:* "Hele huis"-kaart over de volle breedte; lampenlijst als raster van kaarten (2–3 kolommen afhankelijk van breedte); buren-kaart als smalle kolom rechts of onder de lijst.
  - *Kleur mengen:* twee kolommen – links groot voorbeeld (strip + hexcode), rechts de schuiven, presets en de knop. Bovenaan een keuzelijst welke lamp je bewerkt. De pagina in lagen staat in § 9.
  - *Scènes:* buren-kaart bovenaan over de volle breedte, scènekaarten in een raster van 3–4 kolommen.
- **Bediening met muis en toetsenbord:** hover-toestand op kaarten en knoppen (rand `border` → `#4A4A54`), duidelijke focusring (2 px `text`), schuiven bedienbaar met pijltjestoetsen. Hover laat alleen zien waar de muis is; een actie verschijnt er nooit pas door (zie "Altijd zichtbaar" in § 4).
- **Icoon:** `buurmansrgb.ico` voor de applicatie en het venster. Voor MSIX-verpakking kunnen de benodigde formaten uit `buurmansrgb-icon.svg` gegenereerd worden (en `-small.svg` voor de allerkleinste).

## 7. Toegankelijkheid

- Contrast tekst ≥ 4.5:1 (de tokens hierboven halen dat op `bg` en `surface`).
- Elke schakelaar heeft een toegankelijke naam ("<lampnaam> aan of uit"); icoonknoppen hebben een label.
- Kleur is nooit de enige drager van betekenis: aan/uit staat ook in tekst ("3 van 4 aan", "uit" in de subregel), en een link is onderstreept.
- De Statusregel is een live region: een schermlezer leest een melding voor als hij komt of verandert. De knoppen erin en Details staan in de Tab-volgorde met de focusring; Details zegt hoeveel meldingen eronder staan ("Details: nog 1 melding"). De knoppen van de melding over andere RGB-software noemen het programma ("Uitleg over SignalRGB", "SignalRGB niet meer melden"), en Uitleg zegt of de uitleg open staat (ingedrukt of niet, zoals Details); een knop die zijn melding wegneemt, geeft de focus door aan de volgende knop.
- Een getal dat vanzelf blijft veranderen, zoals wat de engine gebruikt bij Instellingen, is geen live region: het heeft een naam en een schermlezer leest het als je erop komt, niet bij elke meting.
- "Minder beweging" van Windows geldt voor wat op het scherm beweegt (het voorbeeld en de kleine strip op een dichte kaart), niet voor het licht in de kamer: de overgang naar een scène ("Overgang naar een scène" bij Instellingen, `sceneFade`) blijft zoals de gebruiker hem koos; wie hem niet wil, kiest daar "Direct".

## 8. Toon

Speels Nederlands met een knipoog naar klussen: "Erbij knutselen", "Uit de gereedschapskist", "In elkaar zetten", "Klusmodus". Kort, vriendelijk, nooit technisch jargon in de UI.

## 9. Kleur in lagen (ontwerp voor nacht 6) – `screens/KleurLagen.dc.html`

**Status: gebouwd (nacht 6).** De lagen (de titel "Kleur", "Per apparaat" met de links, de chiprij en de apparaatkaarten) in `kleur-lagen`, de volgorde van het effect (de effecttegels, kaart 4 en de volgorde van 9.1 en 9.2) in `kleur-volgorde`. Waar de app afwijkt van wat hieronder staat, zegt een regel *Gebouwd:*. Het testpanel van 1-11-2026 (voorstel E, "Kleur in lagen") koos met 10 van de 10 stemmen voor één pagina in lagen, niet voor een aparte eenvoudige stand: bovenaan wat iedereen nodig heeft, eronder per apparaat wat alleen sommigen nodig hebben, dicht tot je het openklapt. Twee ideeën uit `docs/ideas.md` komen hier samen: de kaarten van het voorbeeld open- en dichtklappen met een sprong naar één apparaat (`kleur-lagen`), en de pagina in de volgorde van het effect (`kleur-volgorde`). De mock toont drie staten: "Hele huis" met alle kaarten dicht, één apparaat open, en de tekstgrootte van Windows op 200 %.

### 9.1 Opbouw van boven naar beneden

| # | Onderdeel | Component | Nieuw of bestaand |
|---|---|---|---|
| 1 | Titel "Kleur" met subregel ("Hele huis · 9 apparaten"); rechts "Welk apparaat?" en de schakelaar | schermtitel klein, keuzelijst, **Schakelaar** | bestaand (de titel was "Kleur mengen", zie besluit 8 in 9.9) |
| 2 | Statusregel | **Statusregel** | bestaand |
| 3 | Kaart **Effect**: het effect kiezen, met de zin over wat het doet eronder en daaronder de links Importeren… en Code plakken | grote **Kaart**, **Effecttegel** (9.3), **Link** | nieuw: de tegels (zie besluit 1 in 9.9); de zin bestaat al |
| 4 | Kaart met **wat bij het effect hoort** (9.2) | grote **Kaart**, het mengpaneel van § 5.2, **Preset-tegel**, de lijst met kleuren | bestaand, op een nieuwe plek |
| 5 | Kaart **Helderheid en snelheid**: Helderheid, Snelheid en (bij Regenboog, Golf en een verspreide Kleurenloop) Richting naast elkaar | grote **Kaart**, schuiven, **Link** "Volgorde van de LED's" (alleen met een richting; zonder richting staat "Volgorde" naast de schuif) | bestaand (nu "Licht en beweging", zonder het effect) |
| 6 | **Bewaar als scène**, met de naamkaart eronder | **Knop**, de naamkaart van Scènes | bestaand |
| 7 | Sectiekop **Per apparaat** met de subtekst "Klap een apparaat open voor zones, LED's, lengtes, Knipperen en Tellen."; rechts de links **Alles openklappen** en **Alles dichtklappen** | sectiekop, subtekst, **Link** | nieuw (vervangt de kop "Voorbeeld"; de uitleg hoe je LED's kiest die eronder stond, staat nu bovenaan in elke open kaart, 9.3) |
| 8 | Tijdens een pauze de regel waarom Knipperen en Tellen grijs zijn, met **Hervatten** | **Knop** | bestaand, nu onder de kop van 7 |
| 9 | **Chiprij**: per apparaat een chip om naar zijn kaart te springen | **Apparaat-chip** (9.3) | nieuw |
| 10 | De **apparaatkaarten**, dicht of open | **Apparaatkaart** (9.3) | nieuw: de kop en het dichtklappen; de inhoud bestaat al |

Op een breed venster staan 3, 4 en 5 onder elkaar over de volle breedte, en heeft 4 twee kolommen: links het voorbeeld met strip, naam en hex-code, rechts de schuiven, "Kleuren" en "Eigen kleuren", zoals de bovenkant van de pagina nu. Wat kaart 4 toont als er LED's gekozen zijn, staat onder de tabel van 9.2.

*Gebouwd:* "Kleuren" en "Eigen kleuren" zijn in kaart 4 een kop van niveau 3 (ze liggen in een blok van niveau 2, zoals een apparaatkaart onder "Per apparaat"). Kaart 5 heeft drie kolommen (`CardGrid` met `MaxColumns="3"`, de plek van Richting blijft leeg zonder richting). **Bewaar als scène** (6) staat met de naamkaart in een kolom zo breed als een kolom van het mengpaneel, zodat de naamkaart niet over de hele breedte van de pagina loopt; op een smal venster of bij grote tekst over de volle breedte. In kaart 4 staat wat bij het effect hoort (de lijst, "Jouw effect", de zin bij Per LED) boven het mengpaneel, met 14 px ertussen.

### 9.2 In de volgorde van het effect

Je kiest eerst het effect; kaart 4 toont daarna alleen wat dat effect gebruikt. Zijn kop (kop niveau 2 voor een schermlezer) zegt wat erin staat:

| Effect | Kop van kaart 4 | Inhoud |
|---|---|---|
| Statisch, Ademen, Golf | Kleur | het mengpaneel (voorbeeld, hex-code, Rood, Groen, Blauw), "Kleuren", "Eigen kleuren" |
| Kleurenloop | Kleuren van de loop | de lijst van de loop met eronder het vinkje "Kleuren over de LED's verdelen (uit: alle LED's tegelijk dezelfde kleur)" (een `CheckBox`, zoals nu), en het mengpaneel voor de gekozen kleur daarin |
| Fonkelen, Komeet, Scanner, Vuur | Kleuren van het effect | de lijst van het effect met **Standaardkleuren** eronder, en het mengpaneel voor de gekozen kleur. Standaardkleuren haalt de eigen kleuren weg: Fonkelen, Komeet en Scanner volgen dan weer de kleur van het apparaat, Vuur brandt weer in zijn vier warme kleuren (de kleur van het apparaat doet bij Vuur nooit mee) |
| Eigen effect | Jouw effect | de keuzelijst van je eigen effecten met "+ Eigen effect maken"; naam, stappen, tijden en delen (Code kopiëren, Exporteren…); het mengpaneel voor de gekozen stap. Zonder eigen effecten alleen een zin en "+ Eigen effect maken" (9.3) |
| Per LED | Kleur voor de LED's | het mengpaneel (met gekozen LED's ook Verloop vullen en Selectie opheffen, zie onder de tabel), zonder gekozen LED's met de zin "Kies LED's in een apparaat hieronder; de kleur gaat naar die LED's." en de link **Naar de apparaten** (springt naar 7) |
| Regenboog | Geen kleur nodig | één zin: "Regenboog maakt zijn eigen kleuren. Kies hieronder de helderheid, de snelheid en de richting." Geen mengpaneel |

Bij Regenboog valt het mengpaneel dus weg, in plaats van een kleur te mengen die nergens heen gaat; de zin die dat nu onder de hex-code uitlegt, is dan niet meer nodig.

**LED's gekozen, bij elk effect.** LED's kiezen kan in elke open kaart, welk effect er ook loopt: de eerste kleur zet dat apparaat op Per LED (zoals nu, zie de Per-LED-editor in de handleiding), of met **Als laag boven het effect** een vaste kleur boven het effect dat blijft lopen (9.10). Zolang er LED's gekozen zijn, toont kaart 4 dus altijd het mengpaneel, zoals bij Per LED, welk effect er ook gekozen is: kop "Kleur voor de LED's", en bovenaan in de kaart, boven het voorbeeld, de regel van nu met welke LED's het zijn ("LED 3 t/m 7 van Gesimuleerde ledstrip") en de hulptekst eronder; rechts op die regel de gewone knoppen **Verloop vullen** en **Selectie opheffen**, in die volgorde (bij 200 % gaan ze onder de regel). Wat er bij het effect hoort (een lijst van de loop of van het effect, de tekst bij Regenboog) staat er dan niet: de kleur gaat naar de LED's, niet naar de lijst. Na **Selectie opheffen** (of Escape in de LED's, dichtklappen, een ander apparaat kiezen) toont kaart 4 weer wat bij het effect hoort: bij Per LED als de LED's al een kleur kregen (de effecttegels volgen dat vanaf de eerste kleur, zoals de keuzelijst nu), anders bij het effect van ervoor. Met de knop Selectie opheffen gaat de focus daarna naar de gekozen effecttegel, niet naar het venster; met Escape blijft hij in de LED's.

Wisselt het effect, dan blijft de focus op de effectkeuze en verandert alleen kaart 4; wat eronder staat, schuift zonder animatie naar zijn nieuwe plek.

*Gebouwd:* welke inhoud kaart 4 toont, rekent `MainViewModel.ColorCard` uit (`ColorCardContent`), in de volgorde gekozen LED's, "Jouw effect", de lijst van de loop of van het effect, Per LED, een effect zonder kleur (Regenboog) en anders Kleur; de kop is `ColorCardHeader`. Bij Regenboog is de zin onder de hex-code ("Regenboog gebruikt geen kleur van het apparaat …") weggehaald met het mengpaneel; de app houdt intern bij dat een gemengde kleur dan nergens heen gaat (`ColorPicker.IsUnused`), wat nog telt met gekozen LED's. De zin van Regenboog noemt het effect bij naam (`NoColorText`), en de richting alleen bij een effect met een richting (`IsDirectionVisible`; een later effect zonder kleur en zonder richting krijgt "Kies hieronder de helderheid en de snelheid."). **Naar de apparaten** (Per LED zonder gekozen LED's) zet "Per apparaat" bovenaan, zoals een chip een kaart, en geeft de focus aan de eerste Tab-stop daarin (Alles openklappen, of Knipperen van de ene kaart). Een klik op de tegel die gekozen bleef terwijl kaart 4 "Jouw effect" toont (Eigen effect zonder eigen effecten), haalt de kaart van dat effect terug zonder het effect opnieuw te sturen; een ander apparaat of een scène uit de app doet dat ook, ook met hetzelfde effect.

### 9.3 Nieuwe componenten

**Effecttegel** – een tegel per effect, in een rij die omloopt: radius 12, min. 44 px hoog, min. 9,5 em breed (in de tekstgrootte van de tegel: 142 px bij 100 %; een converter op `FontSize`, zoals de kolommen van Kleur nu), vlak `surface-raised`, 1 px `border` met padding 8 × 14 px, tekst 15 px/600 in `text`, links uitgelijnd. Gekozen: 3 px rand in `text`, zoals een gekozen **Preset-tegel**, met 2 px minder padding (6 × 12 px), zodat de tekst niet verschuift en de tegel niet hoger wordt; en een ✓ voor de naam, zodat de keuze niet alleen aan de rand te zien is. De breedte is gemeten: de langste naam met ✓, "✓ Kleurenloop", is 103 px in Bricolage Grotesque 15 px/600 ("✓ Eigen effect" 102, "✓ Regenboog" 97); met 2 × 12 px padding en 2 × 3 px rand is dat 133 px, dus bij 100 % past elke gekozen tegel op één regel, met wat ruimte voor het tekenen van WPF. Omdat elke naam binnen de minimale breedte past, zijn in het `WrapPanel` alle tegels even breed en even hoog, en verspringt de rij bij een andere keuze niet. Bij 200 % groeit de minimale breedte mee (285 px); is er toch minder ruimte dan één tegel, dan mag de naam over twee regels afbreken (`TextWrapping="Wrap"`, de tegel nooit smaller dan de ruimte), zonder afkappen. Geen bewegend plaatje op de tegel (minder beweging, en geen werk per beeld). Volgorde: Statisch, Ademen, Regenboog, Golf, Kleurenloop, Fonkelen, Komeet, Scanner, Vuur, Per LED en als laatste **Eigen effect**. Samen zijn de tegels één groep keuzerondjes. Onder de groep staat de zin over het gekozen effect (subtekst 13 px), zoals nu onder de keuzelijst, en daaronder altijd de links **Importeren…** en **Code plakken** (met hun namen van nu, "Scène of eigen effect importeren…" en "Code van een scène of eigen effect plakken"): ze halen ook scènes binnen, dus ze horen niet achter de tegel Eigen effect.

**De tegel Eigen effect** kiest net als de andere meteen, ook met de pijltjes (zie besluit 10 in 9.9):

- *Met eigen effecten* kiest hij het eigen effect dat je het laatst gebruikte (de app onthoudt dat zolang hij open is), of anders het eerste uit de lijst. Kaart 4 heet dan "Jouw effect" en begint met de keuzelijst van je eigen effecten en de link "+ Eigen effect maken"; daaronder de stappen zoals nu.
- *Zonder eigen effecten* kiest hij niets, ook niet met een klik, Spatie of via UI Automation (hoe, staat in 9.8): het licht houdt zijn effect, en de tegel van dat effect blijft gekozen. Kaart 4 heet "Jouw effect" en toont alleen de zin "Je hebt nog geen eigen effect." met de link "+ Eigen effect maken". Wie met de pijltjes langs de tegels gaat, verandert het licht dus niet door op Eigen effect te komen. De focus staat dan op een tegel die niet gekozen is; Verteller zegt "Eigen effect, keuzerondje, niet geselecteerd, 11 van 11", en de hulptekst "Nog geen eigen effect. Maak er een met + Eigen effect maken."

*Gebouwd:* met eigen effecten is de hulptekst van de tegel "Kiest je eigen effect '…'. Een ander eigen effect kies je bij Jouw effect." De keuzelijst in "Jouw effect" heet voor een schermlezer "Welk eigen effect?", zoals "Welk apparaat?". Onder de keuzelijst en "+ Eigen effect maken" staat de editor van nu: de uitleg met **Verwijderen** ernaast (in plaats van naast de kop, die nu de kop van kaart 4 is), Exporteren… en Code kopiëren, de naam, de stappen en de twee tijden, die op een breed venster naast elkaar staan; daaronder het mengpaneel. Komt er een eigen effect bij terwijl kaart 4 "Jouw effect" zonder eigen effecten toont (Importeren…, Code plakken, een tweede venster), dan blijft de kaart staan en toont hij de keuzelijst zonder keuze; het licht verandert pas als je er een kiest. De minimale breedte van een tegel (9,5 em) wordt niet begrensd door de breedte van de rij: op het kleinste venster (960 px) bij 225 % passen er nog twee per rij, dus dat gebeurt niet.

**Apparaat-chip** – een pil (volledig rond), min. 44 px hoog, 14 px opzij, vlak `surface`, 1 px `border`; links een **LED-bolletje** van 12 px in de huidige kleur van het apparaat (met gloed; uit: `led-off` met de ring van § 4), dan de naam, 14 px/600 in `text`. Onder de muis de rand `border-strong`, met de focus de ring van 2 px in `text`. Een chip heeft geen gekozen stand: hij springt, hij kiest niets. Een lange naam eindigt op "…" met de hele naam in de tooltip; bij 200 % mag de chip twee regels hoog worden. De chips staan in een rij die omloopt, zonder horizontale schuifbalk. In een contrastthema: `ButtonFace` met `ButtonText`, rand `ButtonText`, onder de muis `Highlight`; het bolletje houdt zijn kleur met een ring in `ButtonText`. De rij staat er alleen bij "Hele huis" met twee of meer apparaten.

**Apparaatkaart** – een **Kaart** (radius 18, `surface`, 1 px `border`) met een kop die openklapt:

- **Kop, dicht:** links een pijltje (lijnicoon 22 px, wijst naar rechts; open: naar beneden), het icoon van het type, de naam als kaarttitel (16 px/600) met eronder één regel subtekst met het aantal LED's in de notatie van de app: "92 LED's in 2 zones" (`LedCountInZones`) bij meer zones, anders "60 LED's" of "1 LED" (`LedCount`, `LedCountOne`), zoals `DeviceItem.Details` nu onder de titel van Kleur staat, maar zonder het type ervoor: dat toont het icoon al. Of het apparaat aan of uit staat, zegt de regel niet; dat toont de kleine strip (bolletjes in `led-off`); rechts een kleine strip van 12 bolletjes in de kleuren die het apparaat nu toont (**Ledstrip-voorbeeld**, ter versiering) en helemaal rechts **Knipperen** (gewone **Knop**, op dezelfde plek als in een open kaart). Pijltje, icoon, naam en regel zijn samen één klikvlak dat open- en dichtklapt; Knipperen is een eigen knop ernaast. De kop is minstens 56 px hoog.
- **Open:** onder de kop een lijn van 1 px `border`, daaronder eerst de uitleg van nu hoe je LED's kiest (`PreviewHint`, subtekst 13 px: "Klik op een LED en kies hierboven een kleur: de LED krijgt hem meteen. Met Shift+klik kies je een reeks, met een klik op de naam van een zone (bijv. een ring van een ventilator) de hele zone. Ctrl+klik voegt een LED toe of haalt hem eruit." – die laatste zin sinds 9.11), en daaronder de inhoud van de kaart van nu: **Zones gelijk**, per zone de **Kop van een zone** met de naam als **Link**, **Tellen** en **Knipperen**, de LED's, de **Stepper** voor de lengte en de teller van Tellen. Aan die inhoud verandert de kaart niets.
- **Knipperen met minder beweging:** de markering "Knippert nu op het apparaat" staat in de kop, onder de naam, ook als de kaart dicht is. *Gebouwd:* onder de rij van de kop, ingesprongen tot de naam (`DeviceCard.Note`), niet in het klikvlak zelf: zo blijft hij een live region die Verteller voorleest, en geen tekst in een knop.
- **Bij één gekozen apparaat** ("Welk apparaat?" bovenaan) is er één kaart, en die is open, zonder pijltje en zonder dichtklappen: je koos dat apparaat om het te zien. De kop is dan geen klikvlak maar gewoon de titel, zoals nu: het is de kaart van nu, geen **Apparaatkaart** (9.8). Zo ook bij een huis met één apparaat (9.7).

### 9.4 Toetsenbord

**Tab-volgorde**, van boven naar beneden zoals je leest: navigatie → "Welk apparaat?" → schakelaar → knoppen van de statusregel → effecttegels (één Tab-stop: de gekozen tegel) → Importeren… → Code plakken → wat bij het effect hoort (met gekozen LED's eerst Verloop vullen en Selectie opheffen, dan Per LED, Als laag boven het effect en Laag weghalen (9.10) en dan het mengpaneel; anders de lijst met + en −; bij Kleurenloop het vinkje "Kleuren over de LED's verdelen…", bij Fonkelen, Komeet, Scanner en Vuur Standaardkleuren; hex-code, Rood, Groen, Blauw, "Kleuren", "Eigen kleuren", +; bij Per LED zonder gekozen LED's Naar de apparaten) → Helderheid → Snelheid → Richting → Volgorde van de LED's (alleen met een richting) → Bewaar als scène → Alles openklappen → Alles dichtklappen (allebei alleen als de chiprij er staat) → Hervatten (alleen tijdens een pauze) → de chiprij (één Tab-stop) → per apparaatkaart de kop en dan Knipperen (bij één kaart die altijd open is alleen Knipperen: zijn titel is geen Tab-stop, 9.8); in een open kaart daarna de inhoud in de volgorde van nu (Zones gelijk, per zone de link, Tellen, Knipperen, de LED's, de stepper). Een dichte kaart heeft dus twee Tab-stops; zijn inhoud slaat Tab over.

- **Effect kiezen:** ← → ↑ ↓ gaan naar de vorige of volgende tegel en kiezen hem meteen, zoals keuzerondjes; Home en End naar de eerste en de laatste.
- **Openen en sluiten:** op de kop van een kaart klappen Enter en Spatie hem open of dicht; → klapt een dichte kaart open en ← een open kaart dicht, zoals een map in Verkenner. De focus blijft op de kop. Na Alles openklappen of Alles dichtklappen blijft de focus op de link.
- **Naar een apparaat springen:** in de chiprij gaan alleen ← en → naar de vorige of volgende chip, na de laatste weer naar de eerste (Home, End naar de eerste en laatste; ↑ en ↓ doen niets, ook als de chips over meer rijen lopen); Enter of Spatie springt: de kaart klapt open als hij dicht was, staat bovenaan in beeld en de focus gaat naar zijn kop. Kom je later weer in de chiprij, dan staat de focus op de chip van het laatste apparaat waar je heen sprong.
- **Dichtklappen met de focus in de kaart** (met de muis op de kop, of met Alles dichtklappen terwijl de focus nog in een kaart stond) zet de focus op de kop van die kaart, zodat hij niet naar het venster valt. Wacht de stepper nog op een lengte (de 600 ms na het laatste − of +), dan gaat die bij het dichtklappen alsnog naar het apparaat, alsof je Toepassen koos: wat je instelde, ging niet stil verloren.
- **Een apparaat verdwijnt terwijl de focus in zijn kaart staat:** komt het terug met hetzelfde id omdat OpenRGB opnieuw laadt (bijv. na een nieuwe lengte), dan houdt de kaart zijn stand en gaat de focus terug naar dezelfde plek (de stepper van die zone, of anders de kop van de kaart). Blijft het weg, dan gaat de focus naar de kop van de volgende kaart, of zonder volgende naar de vorige, of zonder kaarten (dan zijn de links en de chiprij ook weg, 9.6) naar "Welk apparaat?". Nooit naar het venster.
- **"Welk apparaat?" terug naar "Hele huis"** (met de kaart van dat apparaat open): de focus blijft op de keuzelijst, waar hij al stond. De kaarten staan zoals je ze bij "Hele huis" liet (9.7); de pagina springt niet naar de kaart van het apparaat dat je net bekeek en scrolt niet.

### 9.5 Wat een schermlezer hoort

- **Kop van een kaart:** de kaart is een `Expander` (type groep, met het patroon ExpandCollapse van UI Automation; geen losse `ToggleButton`: die zegt "aan" en "uit"; zie 9.8). Het klikvlak van de kop krijgt de focus, maar de Expander meldt die focus zelf, dus Verteller leest de kaart, niet het klikvlak. Verteller zegt bijv. "Gesimuleerde ARGB-controller, samengevouwen" met de regel "92 LED's in 2 zones" als hulptekst, en na Enter "uitgevouwen". De naam is een kop niveau 3, zoals de kaarten nu; "Per apparaat" is niveau 2. Naam, hulptekst en kopniveau staan daarom op de kaart zelf. **Knipperen** houdt zijn naam van nu ("Gesimuleerde ARGB-controller laten knipperen").
- **Alles openklappen en Alles dichtklappen** zijn een **Link** ("link"), met de naam "Alle apparaten openklappen" en "Alle apparaten dichtklappen".
- **Chiprij:** een werkbalk met de naam "Naar een apparaat", elke chip een knop met de naam van het apparaat en de hulptekst "Klapt de kaart open en gaat erheen". Verteller: "Naar een apparaat, werkbalk, Gesimuleerde ledstrip, knop, 1 van 9". Na de sprong leest hij de kop waar de focus landt: "Gesimuleerde ledstrip, uitgevouwen".
- **Effecttegels:** een groep keuzerondjes (`RadioButton`s, geen lijst; zie 9.8) met de naam "Effect", met de zin over wat elk effect doet als hulptekst: "Regenboog, keuzerondje, geselecteerd, 3 van 11". De kop van kaart 4 hoort hij als hij er komt, niet bij elke wissel: het is geen live region, want de keuze zegt het al.
- **Per LED en Als laag boven het effect** (9.10): twee keuzerondjes met hun tekst als naam en wat ze doen als hulptekst ("Als laag boven het effect, keuzerondje, geselecteerd"), samen in de groep "Waar komt de kleur?" (een `ChoiceGroup`, die UI Automation als groep met die naam ziet); **Laag weghalen** is een knop met zijn tooltip als hulptekst, en zegt "niet beschikbaar" zolang geen gekozen LED een laag heeft.
- **Namen uit OpenRGB of van de gebruiker** worden voorgelezen zoals ze zijn, ook met een `_` ("Zone ARGB_V2_1 van … laten knipperen", niet "ARGBV2_1"): knoppen in de app hebben geen onderstreepte letter om met Alt te kiezen (toegangstoetsen), dus een `_` is nooit zo'n toets (zie "Een `_` in een naam" in `ontwikkelaars.md`).
- **De kleine strip in een dichte kaart** en het bolletje in een chip zijn versiering en worden overgeslagen; de LED's van een open kaart noemen hun kleur zoals nu.

### 9.6 Weergave

- **Contrastthema:** kaarten, tegels en chips volgen § 4 en `ContrastTheme.SystemBrushes`: vlakken `Window` of `ButtonFace`, tekst `WindowText` of `ButtonText`, een gekozen effecttegel `Highlight` met `HighlightText`, het pijltje in de kleur van de tekst. LED's, bolletjes en de kleine strip houden hun echte kleur (dat is wat het apparaat doet), met de ring van § 4.
- **Tekstgrootte 200 %:** niets wordt afgekapt en er komt geen horizontale schuifbalk. De keuze van het apparaat gaat onder de titel (zoals nu); de effecttegels lopen om naar twee of drie per rij; kaart 4 zet het voorbeeld boven de schuiven (zoals nu); Helderheid, Snelheid en Richting komen onder elkaar; Alles openklappen en Alles dichtklappen gaan onder de sectiekop; de chips lopen om over meer rijen; in de kop van een apparaatkaart loopt de naam om, en gaan de kleine strip en Knipperen samen onder de naam als de regel te smal wordt, Knipperen rechts zoals in elke kaart. Breedtes in em groeien mee met de tekst, zoals de kolommen van Kleur nu.
- **Minder beweging:** openklappen gebeurt nooit met een animatie, ook zonder die instelling (zoals Details en Uitleg van de statusregel); een sprong naar een kaart schuift niet zacht maar staat meteen op zijn plek. De kleine strip staat dan stil, zoals het voorbeeld.
- **Lege staat:** zonder apparaten staan de bovenste lagen er (je kiest het effect, de kleur en de helderheid voor "Hele huis", en een apparaat dat later komt krijgt ze), maar onder "Per apparaat" staan geen chips, geen links en geen kaarten, alleen de zin van nu: "Nog geen apparaten gevonden. Kijk bij Instellingen of OpenRGB draait, of zet de gesimuleerde apparaten aan." Zonder verbinding met de engine is de hele pagina grijs met de reden erover, zoals nu.

### 9.7 Wat de pagina onthoudt

- **Open of dicht** geldt per apparaat (zijn id) zolang de app open is: ga je naar Scènes en terug, of kies je even één apparaat en dan weer "Hele huis", dan staan de kaarten zoals je ze liet. Na het opnieuw openen van de app zijn alle kaarten dicht. Het staat niet in `settings.json` en hoort niet bij een scène (zie besluit 3 in 9.9).
- **Een nieuw apparaat** komt er dicht bij; een apparaat dat verdwijnt, neemt zijn stand mee, behalve als het terugkomt met hetzelfde id omdat OpenRGB opnieuw laadt (bijv. na een nieuwe lengte): dan houdt het zijn stand (9.4). *Gebouwd:* de app kan "opnieuw laden" niet onderscheiden van "weg en later terug", dus een apparaat dat met hetzelfde id terugkomt, houdt altijd zijn stand zolang de app open is; een ander id komt dicht. Heeft het huis één apparaat, dan is zijn kaart open zonder pijltje, zoals bij één gekozen apparaat, en is er geen chiprij. **Alles openklappen** en **Alles dichtklappen** staan er alleen als de chiprij er staat: bij "Hele huis" met twee of meer apparaten.
- **Dichtklappen stopt wat er in de kaart loopt:** een telling stopt en de zone krijgt zijn eigen aantal LED's terug, zoals bij een ander apparaat kiezen, en gekozen LED's worden losgelaten, zodat een kleur niet naar LED's gaat die je niet ziet. Knipperen loopt gewoon af. *Gebouwd:* zoals bij een ander apparaat kiezen of van Kleur weggaan stopt alleen een telling die dit venster startte; een telling uit een tweede venster van de app telt door (idee in `docs/ideas.md`). Een kaart die altijd open was (één gekozen apparaat, of een huis met één apparaat) en dicht binnenkomt als de kaarten beginnen, zoals bij een tweede apparaat, als OpenRGB even één apparaat meldt of terug naar "Hele huis", geldt als dichtgeklapt: ook dan stopt de telling en worden de LED's losgelaten. Stond hij bij "Hele huis" open, dan komt hij open terug en loopt alles door.
- **Scènes en "Bewaar als scène":** de knop staat in de bovenste laag, onder Helderheid en snelheid, en bewaart zoals nu de verlichting van alle apparaten, ook bij één gekozen apparaat. Wat open of dicht is, hoort niet bij de scène. Gaat er een scène aan (tray-menu, op tijd, bij een programma), dan lopen de bovenste lagen mee zoals nu ("Loopt mee"); verandert het effect daarbij, dan verandert kaart 4 mee, maar klapt er geen kaart open of dicht.

### 9.8 Voor het bouwen

- Alles blijft in de app (`BuurmansRgb.UI`); de engine en de IPC veranderen niet. Een dichte kaart tekent geen LED's, dus de app doet minder per beeld; meet het tekenen met alle kaarten open en dicht.
- De inhoud van een open kaart is `LedPreviewPanel` van nu. *Gebouwd:* de kleine strip in de kop is één element (`SmallLedStrip`) dat twaalf bolletjes tekent met de gloed van `LedBulb`, in plaats van twaalf `LedBulb`s per kaart: die kostten met negen apparaten ~2,4 MB/s aan allocaties en meer CPU dan het voorbeeld zelf (gemeten in `kleur-lagen`).
- **De kop van een apparaatkaart** wordt een eigen template voor `Expander` in `Themes/Controls.Cards.xaml`. Die template houdt het part `HeaderSite` (de `ToggleButton` die open- en dichtklapt): dat is het deel dat de focus krijgt en dat Enter, Spatie en de muis afhandelt. Voor UI Automation is niet `HeaderSite` maar de `Expander` zelf het element: `ExpanderAutomationPeer` (type Group, met het patroon ExpandCollapse) overschrijft `HasKeyboardFocusCore` en `GetChildrenCore`, zodat de Expander de focus meldt zodra `HeaderSite` die heeft, en Verteller de Expander leest. **Knipperen** staat niet in de `Header`-content (dan zit er een knop in een knop: Verteller leest hem als deel van de kop en Enter op Knipperen klapt de kaart), maar naast `HeaderSite` in de template, bijv. in een eigen kolom van het `Grid` van de kop, via een eigen control (`DeviceCard : Expander`) met een property voor wat rechts staat (de kleine strip en Knipperen). `AutomationProperties.HeadingLevel` = Level3, `AutomationProperties.Name` (de naam van het apparaat) en `AutomationProperties.HelpText` (de regel van 9.3: `LedCountInZones` of `LedCount`/`LedCountOne`, zonder het type; bijv. een property naast `DeviceItem.Details`, dat het type ervoor zet) komen daarom op de `DeviceCard` zelf, niet op `HeaderSite`, zodat de kop een kop niveau 3 blijft, zoals de kaarten nu. Nacht 6 schrijft er een UI-test bij die na Tab naar de kop via UI Automation het element met de focus ophaalt (de `DeviceCard`) en daarop controleert dat het `ExpandCollapsePattern` heeft, dat `ExpandCollapseState` meegaat met Enter, → en ←, dat zijn `HeadingLevel` 3 is, dat naam en hulptekst kloppen (bij de gesimuleerde ARGB-controller "92 LED's in 2 zones", zonder het type), en dat Knipperen een eigen Tab-stop naast de kop is. *Gebouwd:* in `DeviceCardsViewTests`; "het element met de focus" is daar de `EventsSource` van de peer van `HeaderSite`, waaraan WPF de focus meldt (de test vraagt eerst de boom op, zoals een schermlezer, want `ExpanderAutomationPeer` zet die pas in `GetChildrenCore`).
- **Eén kaart, altijd open** (één gekozen apparaat, of een huis met één apparaat, 9.3 en 9.7): dan geen `DeviceCard` met het pijltje weg of `IsExpanded` vast op waar, want dan zegt Verteller nog "uitgevouwen" en blijft `HeaderSite` een Tab-stop die niets doet. Het `ItemsControl` kiest dan (met een `DataTemplateSelector` of een trigger op het aantal kaarten) de kaart van nu: de `Border` met `LargeCard` uit `LedPreviewPanel`, met in de `DockPanel` het icoon, de naam als `CardTitle` met `AutomationProperties.HeadingLevel` = Level3 en Knipperen rechts, en daaronder de inhoud van 9.3 (de uitleg hoe je LED's kiest, dan Zones gelijk en de zones). De UI-test erbij: kies één apparaat, en daarna "Hele huis" met één gesimuleerd apparaat; in beide standen heeft geen element op de pagina het `ExpandCollapsePattern`, is de naam van het apparaat een kop niveau 3, en gaat Tab vanaf Bewaar als scène (zonder pauze) meteen naar Knipperen van die kaart. *Gebouwd:* in `DeviceCardsViewTests.OneDevice_ShowsTheCardThatIsAlwaysOpen`. In `kleur-lagen` stond "Licht en beweging" nog tussen Bewaar als scène en "Per apparaat" en controleerde de test Shift+Tab vanaf Knipperen; sinds `kleur-volgorde` staat Bewaar als scène direct boven "Per apparaat" en controleert hij weer Tab vanaf Bewaar als scène.
- **De effecttegels** worden `RadioButton`s in een `WrapPanel` op dezelfde `EffectOption`s als de keuzelijst nu, geen `ListBox` (die maakt er voor Verteller "lijstitem" van). Een `RadioButton` geeft "keuzerondje" met `SelectionItemPattern`, maar is uit zichzelf elk een Tab-stop en kiest niet met de pijltjes. Daarom: op het `WrapPanel` `KeyboardNavigation.TabNavigation="Once"` en `KeyboardNavigation.DirectionalNavigation="Cycle"`; Tab landt op de gekozen tegel (bij binnenkomen de focus zelf naar de gekozen tegel zetten, want "Once" onthoudt de laatste focus, niet de keuze); de pijltjes, Home en End handelt de pagina zelf af in `PreviewKeyDown`: focus naar de tegel ernaast en meteen `IsChecked` (behalve Eigen effect zonder eigen effecten, 9.3). `AutomationProperties.PositionInSet` en `SizeOfSet` zet je zelf per tegel ("3 van 11"), want een `WrapPanel` geeft die niet door. *Gebouwd:* de groep is een eigen `EffectTileGroup : ItemsControl` (een peer van type Group met de naam "Effect", zodat de tegels geen lijstitems worden) die de pijltjes, Home en End afhandelt. "Bij binnenkomen de focus naar de gekozen tegel" doet hij in `OnGotKeyboardFocus`: komt de focus met het toetsenbord van buiten de groep op een tegel die niet gekozen is, dan zet hij hem op de gekozen tegel (bij een klik niet: die kiest de tegel waar je op klikt). Komt de focus terug zonder element ervoor, zoals na Alt+Tab, of zet UI Automation hem op een tegel (`SetFocus` van een schermlezer, `EffectTile.IsFocusedByAutomation`), dan blijft hij op die tegel. `MoveFocus` meldt dan `false`, omdat de focus niet bleef waar WPF hem zette; `PageViews.ReachedWithTab` in de tests kijkt daarom of de focus bewoog. De groep zelf kan de focus krijgen (geen Tab-stop) en geeft hem aan de gekozen tegel, zodat `FocusFallback` ernaar kan wijzen: na Selectie opheffen en na het verwijderen van een eigen effect gaat de focus zo naar de gekozen tegel. De tegels zijn view models `EffectTileItem` in `EffectTilesViewModel`, niet de `EffectOption`s zelf, want "Eigen effect" is één tegel voor alle eigen effecten; die laatste houdt ook bij welk eigen effect het laatst gekozen was. Het ✓ is een `DecorativeText`, zodat Verteller het niet voorleest. UI-tests: `EffectTilesViewTests`.
- **Eigen effect zonder eigen effecten kiest niets** (9.3), maar een gewone `RadioButton` zet zichzelf op `IsChecked` bij een klik, bij Spatie (allebei via `OnClick` → `OnToggle`) en bij `SelectionItemPattern.Select()` van UI Automation (`RadioButtonAutomationPeer` zet `IsChecked` daar rechtstreeks, buiten `OnToggle` om). Daarom worden de tegels een eigen control `EffectTile : RadioButton` met een property die zegt of de tegel nu mag kiezen (bij Eigen effect: of er eigen effecten zijn). Mag hij niet kiezen, dan roept `OnToggle` `base.OnToggle()` niet aan en laat hij alleen kaart 4 wisselen naar "Jouw effect" (een command of event naar de pagina); de tegel die gekozen was, blijft gekozen en houdt de Tab-stop. Een eigen `EffectTileAutomationPeer : RadioButtonAutomationPeer` implementeert `ISelectionItemProvider` opnieuw, zodat `Select()` dezelfde weg gaat als een klik. De pijltjes slaan het kiezen al over in `PreviewKeyDown` (hierboven). Nacht 6 schrijft er een UI-test bij: zonder eigen effecten blijft na een klik, na Spatie en na `Select()` op Eigen effect de vorige tegel gekozen (zijn `IsSelected` waar, dat van Eigen effect onwaar), het effect van het licht ongewijzigd, en heet kaart 4 "Jouw effect". *Gebouwd:* `EffectTile.CanChoose`; de tegels binden `IsChecked` eenrichting aan het view model (`IsChosen`), zodat de keuze altijd uit het view model komt. Test: `EffectTilesViewTests.EigenEffectWithoutOwnEffects_ChoosesNothing`.
- **De chiprij** wordt een `ItemsControl` met een `WrapPanel` en een eigen automation peer (`FrameworkElementAutomationPeer` met `GetAutomationControlTypeCore` = `ToolBar` en de naam "Naar een apparaat"), want een `ItemsControl` zegt zelf niets. Een echte `ToolBar` loopt niet om maar zet chips in een overloopmenu. Elke chip is een `Button` met `PositionInSet` en `SizeOfSet` ("1 van 9"). Op het paneel `TabNavigation="Once"`; ← en →, Home en End handelt de rij zelf af (↑ en ↓ doen er niets, zoals in een werkbalk, ook als de chips over meer rijen lopen). Na een sprong krijgt de chip waarmee je sprong de Tab-stop, zodat je bij terugkomen daar uitkomt.
- **"Bovenaan in beeld"** na een sprong: `BringIntoView` scrolt maar net genoeg, dus een kaart onder het beeld komt onderaan te staan. Reken het zelf uit: de bovenkant van de kaart in de inhoud van de `ScrollViewer` (`TransformToAncestor(...)`) min een kleine marge (12 px) wordt de nieuwe `VerticalOffset`, begrensd door `ScrollableHeight` (de laatste kaarten kunnen niet helemaal bovenaan). Eerst openklappen en de layout bijwerken (`UpdateLayout`), dan scrollen, dan de focus zetten, zodat de focus het beeld niet nog eens verschuift.
- **Hints die naar een plek wijzen:** de teksten die nu zeggen waar iets op de pagina staat, kloppen in de lagen misschien niet meer: "kies hierboven een kleur" in `PreviewHint` (blijft zo: de uitleg staat bovenaan in elke open kaart, en het mengpaneel van kaart 4 staat boven "Per apparaat"), "meng hierboven of rechts een nieuwe" in `ColorLoopHint` en `EffectColorsHint_comet`, `_scanner`, `_twinkle` en `_fire`, "Kies hieronder een kleur" in `LedSelectionHint` (klopt waarschijnlijk nog: het mengpaneel staat in dezelfde kaart eronder), en de andere hints van Kleur die "boven", "onder", "rechts" of "links" zeggen. Nacht 6 kijkt ze na tegen de nieuwe opbouw en past ze aan in `Strings.resx` (en in de handleiding). *Gebouwd:* `ColorLoopHint` en `EffectColorsHint_comet`, `_scanner`, `_twinkle` en `_fire` zeggen nu "meng hieronder een nieuwe" (de lijst staat boven het mengpaneel); `PreviewHint` ("hierboven") en `LedSelectionHint` ("hieronder") kloppen en bleven. De nieuwe zinnen wijzen ook goed: "Kies hieronder de helderheid …" bij Regenboog (kaart 5 staat eronder) en "Kies LED's in een apparaat hieronder …" bij Per LED. `EmptyZoneHint` ("hieronder") gaat over de stepper in dezelfde kaart. Test: `EffectTilesViewTests.Hints_PointToWhereTheMixingPanelIs`.

### 9.9 Besluiten voor nacht 6

De gebruiker heeft deze tien ontwerpvragen op 19 november 2026 beslist: bij alle tien geldt de aanbeveling. Nacht 6 bouwt zo.

1. **Effecttegels of een keuzelijst?** Besloten (zoals aanbevolen): tegels voor de tien vaste effecten en één tegel "Eigen effect" met de keuzelijst van je eigen effecten erachter. Tegels laten zien wat er is zonder eerst te klikken (Truus, Henk) en passen bij "eerst het effect"; een keuzelijst met tien vaste en een rij eigen effecten wordt lang.
2. **Het mengpaneel (schuiven en hex-code) altijd zichtbaar, of achter een knop "Zelf mengen"?** Besloten (zoals aanbevolen): altijd zichtbaar. De lagen gaan over de apparaten, niet over de kleur; de vaste kleuren staan er al voor wie niet wil mengen, en verbergen kost wie wel mengt (Sanne, Noor) bij elke kleur een klik.
3. **Open of dicht onthouden tot de app sluit, of bewaren in `settings.json`?** Besloten (zoals aanbevolen): tot de app sluit. Iedereen begint bij hetzelfde rustige beeld, en er komt geen instelling bij die terugzetten of een andere pc moet begrijpen.
4. **Wat doet een chip: springen en openklappen, alleen dat apparaat tonen, of dat apparaat bovenaan kiezen?** Besloten (zoals aanbevolen): springen en openklappen. Kiezen blijft de keuzelijst bovenaan, zodat je bij "Hele huis" blijft en een kleur niet ongemerkt naar één apparaat gaat.
5. **Wat staat er op een dichte kaart?** Besloten (zoals aanbevolen): pijltje, icoon, naam, één regel met het aantal LED's (en zones, bij meer dan één), een kleine strip van 12 bolletjes in de echte kleuren, en Knipperen. Knipperen blijft omdat "welk apparaat is wat?" juist de vraag is waarvoor je de lijst langsloopt; Tellen en de lengtes horen bij het werk in een open kaart.
6. **Wat doet dichtklappen met een telling of met gekozen LED's?** Besloten (zoals aanbevolen): de telling stopt en de LED's worden losgelaten, zoals bij een ander apparaat kiezen. Het alternatief, een kaart die niet dicht kan zolang hij telt, is een klikvlak dat soms niets doet.
7. **Hoe begint "Hele huis": alle kaarten dicht, of de eerste open?** Besloten (zoals aanbevolen): alle dicht. De chiprij en "Alles openklappen" staan er direct boven, en een open eerste kaart duwt de rest weer een scherm naar beneden.
8. **Heet de pagina nog "Kleur mengen"?** Besloten (zoals aanbevolen): "Kleur", zoals in de navigatie, nu het effect voorop staat en er bij Regenboog niets te mengen is.
9. **De chiprij: één Tab-stop met pijltjes, of elke chip een Tab-stop?** Besloten (zoals aanbevolen): één Tab-stop met pijltjes (een werkbalk), zodat negen apparaten geen negen keer Tab kosten op weg naar de kaarten. Bij de acceptatie laten nalopen met het toetsenbord en Verteller (Fatima, Kees).
10. **Wat doet de tegel Eigen effect zelf, en waar staan Importeren… en Code plakken?** Besloten (zoals aanbevolen): zonder eigen effecten kiest de tegel niets; kaart 4 toont alleen "+ Eigen effect maken" en het licht houdt zijn effect. Met eigen effecten kiest hij het eigen effect dat het laatst gebruikt is, en kaart 4 toont de keuzelijst om een ander te kiezen. Importeren… en Code plakken staan onder de tegelgroep, altijd zichtbaar: ze halen ook scènes binnen en mogen niet verdwijnen achter een tegel die je eerst moet kiezen. Zo verandert het licht nooit naar een leeg of onbekend effect als je met de pijltjes langs de tegels gaat.

### 9.10 Laag boven het effect (`toetsenbord-laag`)

**Status: gebouwd (nacht 7).** Voorstel van Jayden (testpanel): een vaste kleur op gekozen toetsen of LED's boven op het effect, bijv. WASD rood over Regenboog. Het werkt bij elk apparaattype, niet alleen bij toetsenborden.

- **Plek:** in kaart 4, met gekozen LED's (de Per-LED-editor), direct onder de regel met welke LED's het zijn en Verloop vullen en Selectie opheffen, boven het mengpaneel. Eerst de subtekst **Waar komt de kleur?**, eronder in een rij die omloopt twee keuzerondjes, **Per LED** (de bestaande manier: de LED's krijgen een eigen kleur en het apparaat het effect Per LED) en **Als laag boven het effect** (de LED's houden een vaste kleur boven elk effect, ook na een ander effect), en rechts daarvan de gewone knop **Laag weghalen**. De hulptekst boven het mengpaneel zegt bij de laag: "Kies hieronder een kleur: deze LED's houden hem boven elk effect, ook als je een ander effect kiest."
- **Component:** de keuzerondjes zijn de ronde knop met een korte tekst van § 4 (zoals een dag of het tempo van Tellen, `DayToggle`): gekozen een vlak in `text` (accent) met tekst in `bg`, anders een vlak `surface` met rand; bij een langere tekst een pil zo breed als de tekst. Laag weghalen is een **Knop**. Geen nieuw component om te zien; de rij met keuzerondjes is een `ChoiceGroup` (een `StackPanel` dat voor een schermlezer een groep met naam is).
- **Wat de keuze doet:** een kleur uit het mengpaneel, een vaste kleur, een eigen kleur of Verloop vullen gaat naar de gekozen keuze. Klik je een LED die al een laagkleur heeft, dan staat de keuze op Als laag boven het effect en toont het mengpaneel die kleur; anders blijft de keuze van de vorige LED's staan (Per LED zolang je niets koos). Een andere keuze toont in het mengpaneel de kleur van de LED daar, zonder iets te sturen.
- **Weghalen:** Laag weghalen haalt de laag van de gekozen LED's weg, zodat ze het effect weer tonen; Ctrl+A of "Alles selecteren" en dan Laag weghalen haalt de hele laag van het apparaat weg. De knop staat er altijd (§ 4, altijd zichtbaar) en is grijs met zijn tooltip zolang geen gekozen LED een laag heeft.
- **Toetsenbord:** Tab gaat van Selectie opheffen naar Per LED, Als laag boven het effect en Laag weghalen (elk een Tab-stop, zoals het tempo van Tellen), Spatie kiest; dan het mengpaneel. Met de focus op een knop de ring van 2 px (`FocusRingRound`).
- **Schermlezer:** zie 9.5.
- **Contrastthema:** de keuzerondjes en de knop hebben de kleuren van Windows zoals elke knop (`ButtonFace`, `ButtonText`, gekozen `Highlight` met `HighlightText`), uit `GrayText`.
- **Tekstgrootte 200 %:** de rij loopt om; Laag weghalen gaat onder de keuzerondjes.
- **Minder beweging:** er beweegt niets op het scherm; het voorbeeld toont de laag in de frames van de engine, zoals elk effect.
- **Wat de laag is:** een deel van de verlichting: hij gaat mee in `settings.json`, in een scène (ook bij Bewaar als scène, Bijwerken met wat nu brandt en een scène van een paar apparaten), in een gedeelde scène en in de zip van Instellingen bewaren…, en mee in de overgang naar een scène. Uit is uit: een apparaat dat uit staat toont ook zijn laag niet. Knipperen en Tellen gaan over de laag heen. Helderheid geldt ook voor de laag.

### 9.11 Losse LED's kiezen en letters op de toetsen (`ctrl-klik-selectie`)

**Status: gebouwd (nacht 7).** Voorstel van Jayden (testpanel): in de Per-LED-editor losse LED's bij de keuze zetten of eruit halen, zoals WASD en de pijltjes zonder alles ertussen, en de letters op de toetsen van een toetsenbord in het voorbeeld, zodat je ziet welke toets welke is zonder over elke toets te bewegen.

- **Losse LED's:** **Ctrl+klik** op een LED zet hem bij de gekozen LED's of haalt hem eruit, zoals in Verkenner; **Ctrl+Shift+klik** zet de reeks vanaf de laatst aangeklikte LED erbij. Met het toetsenbord gaan **Ctrl+pijltjes** (en Ctrl+Home, Ctrl+End) alleen met de focus, zonder de keuze te veranderen, en zet **Ctrl+Spatie** de LED met de focus erbij of haalt hem eruit. Haal je de laatste gekozen LED eruit, dan is het alsof je Selectie opheffen koos. Een kleur, Verloop vullen (in gelijke stappen over de gekozen LED's) en de laag (9.10) gaan alleen naar de gekozen LED's; de LED's ertussen houden hun kleur of laag, ook als een scène uit het systeemvak, een sneltoets of een tijd ze intussen veranderde: de app stuurt per aaneengesloten stuk gekozen LED's een eigen opdracht, zodat een LED die niet gekozen is nooit naar de engine gaat. De regel boven het mengpaneel zegt bij gaten ertussen "4 LED's van Gesimuleerd toetsenbord" in plaats van "LED 3 t/m 7". De uitleg in de kaart (`PreviewHint`) eindigt met "Ctrl+klik voegt een LED toe of haalt hem eruit."
- **Letters op de toetsen:** een toetsenbord met een toetsenkaart en toetsnamen (dezelfde namen als de tooltip) toont op elke toets een korte tekst zoals op de toets zelf: een letter, cijfer of teken zoals hij is ("W", ";", "F12", ook "7" op het numerieke blok), en voor de bekende langere namen een kort woord uit `Strings.resx` ("Esc", "Tab", "Caps", "Shift", "Ctrl", "Win", "Alt", "Menu", "Enter", "⌫", "Ins", "Del", "PgUp", "↑"). De spatiebalk en onbekende namen langer dan drie letters krijgen niets. Lettertype Bricolage Grotesque 600, token `KeyCapSize` (9 px bij 100 %, groeit mee met de tekstgrootte van Windows), in het midden van de toets.
- **Kleur van de letter:** zwart of wit, per toets en per beeld, wat het meest afsteekt tegen de kleur die de toets nu toont (altijd minstens 4,5:1). Tokens `KeyCapBrush` en `KeyCapHaloBrush`: zonder kleur (standaard `Transparent`) kiest het voorbeeld zelf zwart of wit; in een contrastthema `WindowText` met een rand van 3 px in `Window` om de letters, zodat ze op elke toetskleur leesbaar zijn.
- **Groter toetsenbord:** een toetsenbord met letters is tot 1,5 keer zo groot als zonder: toetsen van 21 px in plaats van 14, een volledig toetsenbord (24 vakjes breed) 642 px. Dat past op Kleur in het kleinste venster (960 px: 760 px ruimte bij 100 %, 678 px bij 225 %), dus geen horizontale schuifbalk en niets afgekapt. Bij grotere tekst groeien de toetsen mee met de letters (bij 200 % tot 3 keer, 42 px), maar nooit breder dan de ruimte; is er minder ruimte, dan krimpt het toetsenbord zoals zonder letters, en nooit kleiner dan zonder letters (tot de helft, `MinScale`). In het kleinste venster bij 200 % zijn de toetsen zo ~23 px en tonen 70 van de 104 toetsen hun tekst, bij 1180 px ~30 px en 84.
- **Te kleine toets:** past de tekst met 2 px ruimte rondom (en de rand in het contrastthema) niet op de toets, dan valt hij weg in plaats van over de rand te lopen of kleiner te worden; de toets toont dan alleen zijn kleur, zoals eerst. Bij het gesimuleerde toetsenbord vallen bij 100 % de woorden van vier of vijf tekens op een toets van één vakje weg ("Caps", "Shift", "Enter", "PrtSc", "Home", "PgUp"), de letters, cijfers, F-toetsen en pijltjes blijven; bij 200 % in het kleinste venster blijven alleen de tekens van één of twee breed (letters, cijfers, F1–F9, pijltjes).
- **Schermlezer:** de letters zijn versiering: de naam van een LED blijft wat hij was, de toetsnaam met zijn nummer en kleur zoals de tooltip ("W (LED 40)"). Een LED zegt of hij gekozen is met het `SelectionItemPattern` ("geselecteerd"), en het voorbeeld van een zone is een lijst met `SelectionPattern` waarin meer LED's gekozen kunnen zijn; `AddToSelection` en `RemoveFromSelection` werken zoals Ctrl+klik.
- **Minder beweging:** er beweegt niets extra; de letters wisselen alleen van zwart naar wit als de toets van kleur wisselt.
- *Gebouwd:* `KeyCapText` maakt de teksten, `KeyCapDrawings` per toets één bevroren tekening (zwart en wit), opnieuw gemaakt alleen bij andere toetsen, teksten, letter, grootte of kleuren, zodat een beeld geen tekst opmaakt. Gemeten: zie "Metingen" in `docs/ontwikkelaars.md`.
