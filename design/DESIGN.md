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

De `.dc.html`-bestanden zijn **ontwerpreferenties, geen draaibare code**: ze gebruiken een eigen template-runtime (`{{hole}}`-plaatshouders en `<sc-for>`-lussen). De data die in de plaatshouders komt staat onderaan elk bestand in het `<script type="text/x-dc">`-blok (`renderVals()`). Lees ze voor exacte maten, kleuren en teksten.

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

**Buren-knop** – pil, min-hoogte 44, `buren-accent` met donkere tekst ("Samen").

**"+ nieuw"-knop** – pil, 2 px gestippelde rand `border-strong`, transparant ("+ Nieuwe scène in elkaar zetten").

**Preset-tegel** – 44 px hoog, radius 12, gevuld met de kleur, 1 px `border`; geselecteerd: 3 px rand in `text`.

**Iconen** – lijniconen, 2 px streep, ronde uiteinden, 22 px. Geen emoji.

## 5. Schermen

### 5.1 Lampen (home) – `screens/Main.dc.html`
1. Kop: icoon + woordmerk links, ronde instellingenknop (moersleutel) rechts.
2. Kaart **Hele huis**: label "Hele huis", waarde "3 van 4 aan", grote schakelaar; daaronder een ledstrip in regenboogvolgorde (rood → paars).
3. Sectiekop "Lampen in huis" met link "+ Erbij knutselen".
4. Lijst met lampen: LED-bolletje, naam, subregel "Ruimte · helderheid", schakelaar. Voorbeelddata: Ledstrip woonkamer (oranje, 80%), Werkbank (klus-wit), Onder de trap (paars, 40%), Tuinhuisje (uit).
5. Buren-kaart: icoon met twee huisjes, "Buurman is online", "Zet jullie lampen in dezelfde kleur", knop "Samen".

### 5.2 Kleur mengen – `screens/Kleur.dc.html`
1. Terugknop, titel "Kleur mengen", subregel met de gekozen lamp.
2. Voorbeeldkaart (`preview-bg`): ledstrip van 12 bolletjes in de huidige kleur, daaronder "Huidige kleur" + hexcode (mono, 30 px) en rechts "R · G · B".
3. Drie schuifregelaars Rood / Groen / Blauw (0–255), elk met label links en waarde rechts (mono). Accentkleur van de schuif = zijn eigen kanaalkleur.
4. "Uit de gereedschapskist": 6 presets (Klus-wit, Rood, Oranje, Groen, Blauw, Paars).
5. Primaire knop "Op de strip zetten".
Gedrag: schuiven en presets werken de strip, hexcode en waarden direct bij (live voorbeeld); pas bij de knop gaat de kleur naar de lamp.

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
  - *Kleur mengen:* twee kolommen – links groot voorbeeld (strip + hexcode), rechts de schuiven, presets en de knop. Bovenaan een keuzelijst welke lamp je bewerkt.
  - *Scènes:* buren-kaart bovenaan over de volle breedte, scènekaarten in een raster van 3–4 kolommen.
- **Bediening met muis en toetsenbord:** hover-toestand op kaarten en knoppen (rand `border` → `#4A4A54`), duidelijke focusring (2 px `text`), schuiven bedienbaar met pijltjestoetsen.
- **Icoon:** `buurmansrgb.ico` voor de applicatie en het venster. Voor MSIX-verpakking kunnen de benodigde formaten uit `buurmansrgb-icon.svg` gegenereerd worden (en `-small.svg` voor de allerkleinste).

## 7. Toegankelijkheid

- Contrast tekst ≥ 4.5:1 (de tokens hierboven halen dat op `bg` en `surface`).
- Elke schakelaar heeft een toegankelijke naam ("<lampnaam> aan of uit"); icoonknoppen hebben een label.
- Kleur is nooit de enige drager van betekenis: aan/uit staat ook in tekst ("3 van 4 aan", "uit" in de subregel).

## 8. Toon

Speels Nederlands met een knipoog naar klussen: "Erbij knutselen", "Uit de gereedschapskist", "In elkaar zetten", "Klusmodus". Kort, vriendelijk, nooit technisch jargon in de UI.
