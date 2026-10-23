# Hardware

Vul hier je onderdelen in. Agents gebruiken dit voor taak 5–8; de teststappen staan in
`ontwikkelaars.md` onder "Zelf testen op echte hardware".

De pc thuis, stand 23-10-2026: AMD Ryzen 7 9850X3D, Windows 11. Wat er onder "Ondersteund door
OpenRGB?" staat, komt uit productinformatie en de USB-lijst van Windows; op deze pc is het nog
niet in OpenRGB of BuurmansRgb getest.

| Onderdeel | Merk en model | Ondersteund door OpenRGB? | Werkt in BuurmansRgb? | Opmerkingen |
| --- | --- | --- | --- | --- |
| Moederbord | Gigabyte B850 AORUS ELITE WIFI7, BIOS F10 (5-2-2026) | Waarschijnlijk: de RGB Fusion 2-controller, ITE `048D:5711` via USB | Nog niet getest | De ARGB-headers hebben in OpenRGB 0 LED's tot je de lengte instelt; welke header wat is, nog invullen |
| CPU-koeler | Lian Li Galahad II Lite 360R (waterkoeling) | Via een ARGB-header van het moederbord, niet als eigen apparaat | Nog niet getest | Heeft waarschijnlijk geen eigen USB-controller: pomp en fans hangen doorgelust aan één ARGB-kabel (niet bevestigd, zie de handleiding van de koeler). In de app een zone van het moederbord; header en aantal LED's nog invullen |
| RAM | 2× Kingston FURY Beast 16 GB DDR5-6000 CL30 (`KF560C30-16`) | Waarschijnlijk geen RGB | – | De code mist de "A" van de RGB-variant (bijv. `KF560C30BBEA-16`), maar Windows meldt alleen de verkorte code: kijk op de sticker of de modules oplichten |
| GPU | PowerColor Radeon RX 9070 XT Hellhound 16GB OC | Nee, niet gevonden | Nee | Vaste ijsblauwe LED met een "Dark"-stand, geen RGB; geen software gevonden die hem aanstuurt. De "Microsoft Basic Display Adapter" is de grafische chip van de processor zonder driver |
| Fans / fan-controller | Casefans van de behuizing | Onbekend | Nog niet getest | Merk, aantal en of ze ARGB hebben nog invullen; met ARGB hangen ze aan een header van het moederbord |
| Case-LED / ARGB-strips | Lian Li Vector V100R (behuizing) | Onbekend | Nog niet getest | Nog invullen of de behuizing eigen LED's heeft |
| Toetsenbord | Logitech G213 (`046D:C336`) | Waarschijnlijk | Nog niet getest | |
| Muis | Logitech G502 HERO (`046D:C08B`) | Waarschijnlijk | Nog niet getest | Twee zones van één LED; met "Zones gelijk" krijgen ze tijdens een effect dezelfde kleur |
| Headset | Logitech G733 (`046D:0AB5`) | Onbekend, te controleren | Nog niet getest | Kent OpenRGB hem niet, dan staat hij ook niet in BuurmansRgb |

Overige USB-apparaten, geen RGB in BuurmansRgb:

- **CH340 (COM3, `1A86:7523`):** een USB-naar-serieel-chip, geen ARGB-hub; er hangt
  vermoedelijk een microcontrollerbordje aan.
- **ESP32-S3 (COM6, `303A:1001`):** mogelijk de ATOM S3 Lite met WLED.

Handig om bij "Opmerkingen" te noteren:

- of het apparaat in OpenRGB een "Direct"-modus heeft, en of het de kleur zonder die modus
  toch volgt;
- de locatie die OpenRGB geeft (tooltip van de lampkaart), vooral bij RAM-sticks en twee
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

- **GIGABYTE Control Center** 25.07.21.01: moederbord, en daarmee de koeler en de casefans aan
  de ARGB-headers.
- **Logitech G HUB** 2026.6.981214: toetsenbord, muis en headset.
- **SignalRGB** 2.5.78: kan alle apparaten aansturen.
- **OpenRGB** 1.1.0 staat ook los geïnstalleerd; laat er één OpenRGB tegelijk draaien.
