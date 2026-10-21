# BuurmansRgb – Bouwplan voor RGB-aansturing in C#

## Naam en concept

De app heet **BuurmansRgb**: een lichtgewicht Windows-app in C# die alle RGB in je pc vanuit één plek aanstuurt. Een knipoog naar de klussende buurmannen, want ook hier geldt: eerst simpel, dan stukje bij beetje uitbreiden.

BuurmansRgb draait als klein achtergrondproces met een tray-icoon. De instellingen-UI open je alleen als je iets wilt wijzigen; sluit je hem, dan verdwijnt hij volledig uit het geheugen.

## Stand

Taak 0 t/m 9 zijn gebouwd en in `main` gemerged, en daarna een reeks uitbreidingen (zie
"Uitbreidingen na taak 9" onderaan). Wat nog open staat, zijn de acceptatiecriteria die
alleen met echte hardware te testen zijn: taak 3 en taak 5 t/m 8. Die test de gebruiker zelf;
de stappen staan in `ontwikkelaars.md` onder "Zelf testen op echte hardware". De installer
(taak 9) is getest op de eigen pc van de gebruiker.

## Uitgangspunten en eisen

De kern is: hardware aansturen via OpenRGB, zelf alleen de engine, effecten en UI bouwen. Zo hoef je niet per merk een protocol te reverse-engineeren en draait BuurmansRgb zonder adminrechten.

| Eis | Uitwerking |
| --- | --- |
| Simpel starten | Eerst een gesimuleerd apparaat, daarna echte hardware via OpenRGB |
| Per onderdeel uitbreiden | Elk apparaattype (moederbord, GPU, fans, toetsenbord, muis, headset) is een eigen fase |
| Kleur en helderheid | Per apparaat, per zone en globaal instelbaar |
| Per LED een kleur | Effecten krijgen een lijst LED's en vullen die in een loop, bijvoorbeeld een regenboog over index |
| Licht op de achtergrond | Engine zonder UI-framework; UI is een apart proces dat stopt bij sluiten |
| Inspiratie | OpenRGB voor de device-laag, SignalRGB voor effecten en de LED-preview |

## Architectuur

BuurmansRgb bestaat uit twee processen: een kleine engine die altijd draait en de hardware aanstuurt, en een UI die je alleen opent om iets te wijzigen.

```text
┌───────────────────────────────┐
│  BuurmansRgb.UI (WPF)         │  start op verzoek, stopt bij sluiten
└───────────────┬───────────────┘
                │ named pipe · StreamJsonRpc
┌───────────────▼─────────────────────────────────────────────────────┐
│  BuurmansRgb.Engine · draait altijd, zonder UI-framework            │
│  ┌────────────┐ ┌────────────┐ ┌──────────────────┐ ┌─────────────┐ │
│  │ Tray-icoon │ │ IPC-server │ │ Render-loop      │ │ Instellingen│ │
│  │ openen,    │ │ named pipe │ │ effecten per LED │ │ JSON in     │ │
│  │ pauze,stop │ │            │ │ alleen bij anim. │ │ %AppData%   │ │
│  └────────────┘ └────────────┘ └────────┬─────────┘ └─────────────┘ │
│                         ┌───────────────┴──────────────┐            │
│  ┌──────────────────────▼──────┐  ┌────────────────────▼──────────┐ │
│  │ Providers.Simulated         │  │ Providers.OpenRgb             │ │
│  │ nep-apparaten (dev/test)    │  │ via NuGet OpenRGB.NET         │ │
│  └─────────────────────────────┘  └────────────────┬──────────────┘ │
└────────────────────────────────────────────────────┼────────────────┘
                                                     │ TCP 127.0.0.1:6742
                                   ┌─────────────────▼──────────────┐
                                   │ OpenRGB SDK-server             │
                                   │ eenmalig met adminrechten      │
                                   └─────────────────┬──────────────┘
                                                     │ USB, HID, SMBus
   moederbord · RAM · GPU · fans · strips · toetsenbord · muis · headset
```

De engine kent hardware alleen via `IDeviceProvider`, dus een nieuwe bron (bijvoorbeeld later een eigen HID-driver) is een extra provider zonder wijzigingen elders. Helderheid past de engine toe ná het effect, door elke kleur te schalen; zo werkt de helderheidsschuif op elk effect.

Naast wat in de tekening staat, heeft de engine inmiddels ook: scènes (`Profiles`: opslaan in
`profiles.json`, aanzetten op tijd of bij een programma), automatisch pauzeren bij vergrendelen
en slaapstand en herstellen na het ontwaken (`Power`), het bewaren en terugzetten van
zonelengtes (`ZoneLengthKeeper`) en het wisselen tussen gesimuleerde en echte apparaten
(`SimulatedDevicesSwitch`).

Kern-interfaces in `Core`. Zo zijn ze nu; taak 0 begon met een kleinere versie (`RgbZone` met
alleen `Resizable`, minder apparaattypes):

```csharp
public readonly record struct RgbColor(byte R, byte G, byte B);

public enum DeviceType
{
    Motherboard, Ram, Gpu, Fan, LedStrip, Case, Keyboard, Mouse, Headset, Other,
    Mousemat, HeadsetStand, Speaker, Gamepad, Microphone, Keypad,
}

// Resizable als MinLedCount < MaxLedCount; Matrix en LedNames komen van OpenRGB.
public sealed record RgbZone(string Name, int LedCount, int MinLedCount, int MaxLedCount);

// Met daarnaast Source (OpenRGB of gesimuleerd), Location en HasDirectMode.
public sealed record RgbDevice(string Id, string Name, DeviceType Type,
    IReadOnlyList<RgbZone> Zones, int LedCount);

public interface IDeviceProvider
{
    string Name { get; }
    Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct);
    void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors);
}

public interface IEffect
{
    bool IsAnimated { get; }
    void Render(Span<RgbColor> leds, TimeSpan time);
}
```

Een provider kan meer: `IDynamicDeviceProvider` (meldt een nieuwe apparatenlijst),
`IResizableDeviceProvider` (zonelengte instellen), `IRestorableDeviceProvider` (apparaten
opnieuw klaarzetten na slaapstand) en `IServerStatusSource` (de OpenRGB-status).

De loop per LED ziet er zo uit, hier als regenboog die over de strip schuift:

```csharp
public sealed class RainbowEffect(double roundsPerSecond = RainbowEffect.DefaultRoundsPerSecond) : IEffect
{
    public const double DefaultRoundsPerSecond = 0.2;

    public double RoundsPerSecond { get; } = EffectMath.FiniteSpeed(roundsPerSecond);

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var shift = time.TotalSeconds * RoundsPerSecond;
        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = RgbColor.FromHsv(i / (double)leds.Length + shift, 1.0, 1.0);
        }
    }
}
```

## Tech stack en projectstructuur

Gebruik .NET 10 (de huidige LTS-versie) met WPF voor de UI en een UI-loos engine-proces. Controleer bij de start of er inmiddels een nieuwere LTS is.

| Onderdeel | Keuze | Waarom |
| --- | --- | --- |
| Runtime | .NET 10, `net10.0-windows` | LTS, snel, goede tooling |
| UI | WPF + CommunityToolkit.Mvvm | Volwassen, simpel MVVM, kleurenkiezers beschikbaar |
| Tray-icoon (engine) | WinForms `NotifyIcon` | Klein, geen WPF nodig in de engine |
| Hardware | OpenRGB SDK-server + NuGet `OpenRGB.NET` | Ondersteunt honderden apparaten via één TCP-protocol (poort 6742) |
| UI ↔ engine | Named pipe + StreamJsonRpc | Lokaal, snel, geen netwerkpoort open |
| Opslag | JSON in `%AppData%\BuurmansRgb\` | Leesbaar, makkelijk te debuggen |
| Logging | `Trace` naar een bestand (`FileLog`) in `%AppData%\BuurmansRgb\logs` | Geen extra pakketten of geheugen in de engine; elke bestaande `Trace`-melding komt erin |
| Tests | xUnit v3 op Microsoft.Testing.Platform | Effecten en mapping testen zonder hardware |
| Installer | Inno Setup 7.1 | Eén setup met OpenRGB en PawnIO, zie `docs/openrgb-meeleveren.md` |

Solution-indeling:

```text
BuurmansRgb.slnx
├─ src/
│  ├─ BuurmansRgb.Core                // modellen, interfaces, kleur-wiskunde
│  ├─ BuurmansRgb.Effects             // Static, Breathing, Rainbow, Wave, ColorLoop, PerLed
│  ├─ BuurmansRgb.Contracts           // IPC-interface, DTO's en de pipe- en RPC-opzet
│  ├─ BuurmansRgb.Providers.Simulated // nep-apparaten voor ontwikkeling
│  ├─ BuurmansRgb.Providers.OpenRgb   // koppeling met OpenRGB SDK-server
│  ├─ BuurmansRgb.Engine              // tray, render-loop, IPC-server, opslag, scènes, pauze
│  └─ BuurmansRgb.UI                  // WPF, start op verzoek, stopt bij sluiten
├─ tests/
│  ├─ BuurmansRgb.<project>.Tests     // een testproject per project hierboven
│  ├─ BuurmansRgb.Providers.OpenRgb.Testing // nep-OpenRGB-server, zonder eigen tests
│  └─ Shared                          // helpers die elk testproject meebouwt
├─ installer/                         // build.ps1, BuurmansRgb.iss, Sandbox-test
└─ design/                            // ontwerppakket van de instellingen-app
```

## Prestatiebudget voor achtergrondgebruik

Doel: met gesloten UI en een statische kleur gebruikt de engine vrijwel 0% CPU, omdat er dan niets te verversen valt. Onderstaande waarden zijn streefwaarden; meet ze in elke fase met Taakbeheer of `dotnet-counters`.

| Situatie | CPU | Geheugen (working set) |
| --- | --- | --- |
| Engine, statische kleur, UI dicht | ~0% (geen timer actief) | < 50 MB |
| Engine, animatie op 30 fps, UI dicht | < 1% | < 60 MB |
| UI open met live preview | geen harde eis | < 150 MB |

Gemeten (Release, gesimuleerde apparaten, zonder OpenRGB; de details staan in `ontwikkelaars.md`
onder "Metingen"): statisch ~0,07 % van één kern en ~21–24 MB working set, regenboog op 30 fps
~0,35 % van één kern en ~22–27 MB, de instellingen-app (Release, venster zichtbaar, regenboog)
~165–190 MB, dus boven het budget (zie `docs/ideas.md`).

Maatregelen die dit mogelijk maken:

- **Render-loop alleen bij animatie.** Een statisch effect wordt één keer verstuurd; daarna stopt de `PeriodicTimer`.
- **Geen allocaties in de loop.** LED-buffers worden hergebruikt (`Span<RgbColor>`, vooraf gealloceerde arrays).
- **Alleen wijzigingen versturen.** Vergelijk met het vorige frame en stuur per apparaat alleen als er iets veranderde.
- **Instelbare framerate.** Standaard 30 fps, maximaal 60; animaties zijn tijdgebaseerd, niet frame-gebaseerd.
- **Lichte GC-instellingen in de engine:** `ServerGarbageCollector=false`, `ConcurrentGarbageCollection=false`, `InvariantGlobalization=true`.
- **Pauzeren bij vergrendelen of slaapstand** via `SystemEvents.SessionSwitch` en `PowerModeChanged`; na hervatten alles opnieuw toepassen, want veel apparaten resetten na slaap.
- **UI is een apart proces.** Sluiten = proces stopt = geheugen vrij.

## Fasering

Bouw in tien fasen; na fase 3 heb je al een werkende app met één echte kleur op echte hardware. Elke fase eindigt met iets dat je kunt starten en zien.

0. **Fundament.** Solution, `Core`-modellen, gesimuleerd apparaat, eerste tests.
1. **Engine + tray.** Achtergrondproces met tray-menu (UI openen, effecten pauzeren, afsluiten), opslag in JSON, optioneel autostart.
2. **UI + IPC.** WPF-venster met apparatenlijst, kleurkiezer, helderheidsschuif en een LED-preview; praat via de named pipe met de engine.
3. **OpenRGB-koppeling.** Verbinden met de SDK-server, apparaten ophalen, kleuren versturen, automatisch opnieuw verbinden.
4. **Effect-engine.** Static, Breathing, Rainbow, Wave en een per-LED-editor (de "loop" over LED's).
5. **Moederbord en RAM.** Eerste echte apparaattype; zones en headers tonen.
6. **GPU.** Apparaattype GPU, zelfde flow.
7. **Fans, case-LED en ARGB-strips.** Zones instelbaar in lengte (aantal LED's per strip), ventilatoren per ring.
8. **Toetsenbord, muis en headset.** Toetsenbord met matrixweergave per toets, muis met zones, headset waar OpenRGB het ondersteunt.
9. **Installer.** Eén setup die BuurmansRgb, OpenRGB als service en PawnIO neerzet.

Na fase 9 als losse uitbreidingen; wat er al is, staat in "Uitbreidingen na taak 9" onderaan
(o.a. profielen, die in de app scènes heten). Nog open zijn onder meer alle apparaten synchroon
laten lopen en eigen HID-providers voor apparaten die OpenRGB niet kent; zie `docs/ideas.md`.

## Agent-taken

Geef agents één taak tegelijk, in volgorde. Een taak is pas klaar als alle acceptatiecriteria kloppen. Kant-en-klare prompts staan in `docs/prompts.md`.

### Taak 0 – Fundament

**Opdracht:** Maak de solution volgens de projectstructuur. Implementeer in `Core` de modellen `RgbColor`, `RgbDevice`, `RgbZone`, `DeviceType` en de interfaces `IDeviceProvider` en `IEffect` uit de architectuur. Bouw `Providers.Simulated` met drie nep-apparaten: een strip van 30 LED's, een fan van 12 LED's en een toetsenbord van 104 LED's.

**Acceptatie:**

- [x] `dotnet build` en `dotnet test` slagen zonder waarschuwingen
- [x] Tests voor `RgbColor`: helderheid schalen, HSV naar RGB, interpolatie
- [x] De gesimuleerde provider levert de drie apparaten met juiste LED-aantallen

### Taak 1 – Engine en tray

**Opdracht:** Bouw `BuurmansRgb.Engine` als WinExe zonder venster, met een `NotifyIcon` en menu: "Instellingen openen", "Effecten pauzeren", "Afsluiten". Laad en bewaar instellingen in `%AppData%\BuurmansRgb\settings.json`. Voeg een optie toe voor autostart via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Sta slechts één instantie toe (named mutex).

**Acceptatie:**

- [x] Engine start, toont tray-icoon, past een opgeslagen statische kleur toe op de gesimuleerde apparaten
- [x] Met statische kleur: geen actieve timers, ~0% CPU en < 50 MB in Taakbeheer
- [x] Tweede start opent geen tweede engine maar vraagt de bestaande om de UI te openen

### Taak 2 – UI en IPC

**Opdracht:** Definieer in `Contracts` de interface `IEngineApi` (apparaten opvragen, kleur/helderheid zetten, effect kiezen, frames voor preview ontvangen). Host die in de engine via StreamJsonRpc op named pipe `BuurmansRgb.Engine`. Bouw de WPF-UI met MVVM: apparatenlijst links, rechts een kleurkiezer, helderheidsschuif (0–100%) en een preview die elke LED als bolletje tekent. De tray start de UI-exe; de UI stopt volledig bij sluiten.

**Acceptatie:**

- [x] Kleur wijzigen in de UI is binnen 100 ms zichtbaar in de preview
- [x] UI sluiten laat de engine draaien; het UI-proces staat niet meer in Taakbeheer
- [x] UI geeft een nette melding als de engine niet draait

Later veranderd: de pipe heet `BuurmansRgb.Engine.<sessie-id>`, zodat elke ingelogde gebruiker
zijn eigen engine heeft, en sinds het nieuwe ontwerp (#26) heeft de UI geen apparatenlijst
links meer maar de pagina's Lampen, Kleur, Scènes, Buren en Instellingen (zie `handleiding.md`).

### Taak 3 – OpenRGB-koppeling

**Opdracht:** Bouw `Providers.OpenRgb` met NuGet `OpenRGB.NET`. Verbind met `127.0.0.1:6742`, vertaal OpenRGB-apparaten, zones en LED's naar de `Core`-modellen en stuur kleuren met de update-LED-aanroepen. Zet bij verbinden elk apparaat in de modus "Direct" of "Custom". Probeer bij verlies elke 5 seconden opnieuw te verbinden.

**Acceptatie:**

- [ ] Met OpenRGB-server actief verschijnen echte apparaten in de UI; met "Gesimuleerd: Altijd" naast de gesimuleerde (standaard, "Automatisch", verdwijnen de gesimuleerde zodra er echte zijn)
- [ ] Statische kleur komt aan op minstens één echt apparaat
- [ ] OpenRGB stoppen en weer starten: BuurmansRgb herstelt zonder herstart
- [x] Gesimuleerde apparaten zijn uit te zetten in de instellingen (in de instellingen-app bij Instellingen: "Gesimuleerde lampen tonen" op "Automatisch", "Altijd" of "Nooit", zonder herstart en bewaard als `simulatedDevices` in `settings.json`; "Automatisch" is de standaard en verbergt ze zodra OpenRGB echte apparaten levert)

Stand taak 3: gebouwd en getest met nepdata en een kleine SDK-server. De drie open criteria
vragen OpenRGB met echte apparaten; die test de gebruiker zelf (zie "Zelf testen op echte
hardware" in `ontwikkelaars.md`).

### Taak 4 – Effect-engine

**Opdracht:** Implementeer in `Effects`: `StaticEffect`, `BreathingEffect`, `RainbowEffect` (kleur per LED-index), `WaveEffect` en `PerLedEffect` (een opgeslagen kleur per LED). Effecten zijn tijdgebaseerd en schrijven in een aangeleverde buffer. De engine draait een `PeriodicTimer` op de ingestelde fps, alleen als minstens één actief effect animeert. Voeg in de UI een per-LED-editor toe: klik een LED in de preview en geef hem een kleur, of selecteer een reeks en vul die met een verloop.

**Acceptatie:**

- [x] Unit-tests per effect op vaste tijdstippen
- [x] 0 allocaties per frame in de render-loop (controleren met `dotnet-counters`) (een test
  meet 0 bytes per frame met `GC.GetAllocatedBytesForCurrentThread`. Met `dotnet-counters` op
  de draaiende engine in Release, de instellingen-app dicht en 5 minuten per stand: regenboog
  op 30 fps ~101 KB per minuut, statisch zonder render-timer ~89 KB per minuut (73–114 KB per
  minuut). Dat verschil valt binnen de spreiding van de statische meting, ook al zijn het 1.800
  frames per minuut, dus geen meetbare allocatie per frame. De rest komt van het verbinden met
  OpenRGB en van de meting zelf. Met de app open maakt de engine per frame een kopie voor het
  voorbeeld)
- [x] Animatie op 30 fps met UI dicht: < 1% CPU (Release, regenboog op de gesimuleerde
  apparaten: ~0,02% van de pc, < 0,5% van één kern)
- [x] Per-LED-kleuren blijven bewaard na herstart

### Taak 5 t/m 8 – Per apparaattype

Gebruik deze opdracht vier keer, telkens met één type: **5** moederbord en RAM, **6** GPU, **7** fans, case-LED en ARGB-strips, **8** toetsenbord, muis en headset.

**Opdracht:** Maak de ondersteuning voor `<type>` af. Toon het juiste icoon en de zones in de UI. Test met de echte hardware van de gebruiker en noteer wat werkt in `docs/hardware.md`. Specifiek per type:

- **Fans en strips:** zonegrootte instelbaar als OpenRGB dat toestaat (resizable zones), fan-ringen apart te kiezen
- **Toetsenbord:** preview als matrix met de echte toetsposities uit OpenRGB's matrix-map
- **Headset:** als OpenRGB het model niet ondersteunt, alleen netjes vermelden, geen eigen driver bouwen

**Acceptatie:**

- [ ] Alle apparaten van dit type van de gebruiker verschijnen met juiste LED-aantallen
- [ ] Statische kleur, helderheid en minstens Rainbow werken erop
- [ ] Na slaapstand en hervatten staan de kleuren weer goed
- [ ] `docs/hardware.md` bijgewerkt

De vier criteria hierboven vragen echte hardware en staan daarom voor alle vier de types nog
open; de gebruiker test ze zelf, zie "Zelf testen op echte hardware" in `ontwikkelaars.md`. Wat per
type gebouwd en zonder hardware getest is (met nepdata van OpenRGB, een kleine SDK-server en
een nepklok):

- **Taak 5, moederbord en RAM:** icoon, type en zones in de instellingen-app, een gesimuleerd
  moederbord en twee RAM-sticks, en herstellen na het ontwaken.
- **Taak 6, GPU:** icoon en typenaam "Videokaart", een gesimuleerde videokaart met twee zones,
  kaarten zonder "Direct"-modus, twee dezelfde kaarten en herstellen na het ontwaken.
- **Taak 7, fans, case-LED en ARGB-strips:** iconen en typenamen ("Ventilator", "LED-strip",
  "Behuizing"), het case-type van nieuwere OpenRGB-versies, de lengte van resizable zones
  instellen (met per-LED-kleuren die meeschuiven), een ring of andere zone kiezen door op de
  naam te klikken, en gesimuleerde apparaten (fan met twee ringen, ARGB-controller,
  behuizing). Later uitgebreid met fans als ring (#23) en zonelengtes bewaren (#24).
- **Taak 8, toetsenbord, muis en headset:** iconen en typenamen ("Toetsenbord", "Muis",
  "Headset"), de matrix-map van OpenRGB als `LedMatrix` bij de zone, een voorbeeld dat elke
  toets op zijn plek tekent en meekrimpt in een smal venster, en gesimuleerde apparaten
  (toetsenbord van 104 toetsen, muis met vier zones, headset met twee zones). Een headset die
  OpenRGB niet kent, staat netjes vermeld in `handleiding.md`; er komt geen eigen driver. Later
  uitgebreid met toetsnamen en brede toetsen (#23).

### Taak 9 – Installer

**Opdracht:** Maak één `BuurmansRgb-setup.exe` (Inno Setup) volgens `docs/openrgb-meeleveren.md`. De installer zet BuurmansRgb self-contained in `Program Files`, installeert stil PawnIO (`-install -silent`) en OpenRGB als service (de MSI met `ADDLOCAL=OpenRGBApplication,OpenRGBRegisterService`, overgeslagen als de service `OpenRGB` al bestaat), en start daarna de engine als de gewone gebruiker met `--first-run`, die "Starten met Windows" aanzet. Een build-script publiceert de apps, haalt de MSI en PawnIO op met een SHA-256-controle, en bouwt de installer. Verwijderen haalt BuurmansRgb en de autostart weg, en vraagt of OpenRGB en PawnIO ook weg moeten als de installer die zelf heeft geïnstalleerd.

**Acceptatie:**

- [x] Eén `BuurmansRgb-setup.exe`, met één UAC-vraag en zonder losse .NET-runtime
- [x] Na installeren draait de service `OpenRGB` en luistert er iets op `127.0.0.1:6742`
- [x] De engine draait als de gewone gebruiker, staat in "Starten met Windows" en verbindt met OpenRGB
- [x] Verwijderen haalt BuurmansRgb, de autostart en (op verzoek) OpenRGB en PawnIO weg
- [x] Getest in Windows Sandbox, niet op de pc van de gebruiker (in plaats daarvan, op keuze van
  de gebruiker, getest op de eigen pc thuis)

Stand taak 9: de installer (Inno Setup 7.1, self-contained, ongeveer 77 MB), het build-script
met SHA-256-controle, `--first-run` in de engine (met tests) en een Windows Sandbox-test die
installeert, controleert en weer verwijdert zijn gebouwd. Windows Sandbox stond niet aan op de
ontwikkel-pc. Op 20-10-2026 heeft de gebruiker de installer op de eigen pc thuis gedraaid:
installeren, de service `OpenRGB`, de engine met autostart en verwijderen werken. De
Sandbox-test blijft beschikbaar om een nieuwe installer te testen zonder iets op je eigen pc
te installeren, zie "Installer bouwen en testen" in `ontwikkelaars.md`.

## Uitbreidingen na taak 9

Na het bouwplan kwamen er losse uitbreidingen bij, elk in een eigen PR. Ze zijn gebouwd en
getest zonder hardware; wat op echte hardware getest moet worden, staat in `ontwikkelaars.md` onder
"Zelf testen op echte hardware".

| PR | Uitbreiding |
| --- | --- |
| #17 | Snelheid van bewegende effecten (10–300 %) en "Opgeslagen ✓" in de app |
| #18 | Effect Kleurenloop: eigen 2–8 kleuren die in elkaar overlopen |
| #19 | Profielen (nu scènes) opslaan, laden en verwijderen in `profiles.json` |
| #20 | Apparaten herkennen: knipperen, echt en gesimuleerd apart, uitleg bij een lege zone, locatie (adres of bus) bij dezelfde apparaten |
| #21 | OpenRGB-status in de app en de tooltip, gesimuleerde apparaten "Automatisch", "Altijd" of "Nooit" |
| #22 | Scènes aanzetten vanuit het tray-menu, elke dag op een tijd en zolang een programma draait |
| #23 | Voorbeeld: fans als ring, toetsnamen, brede toetsen en meer apparaattypes |
| #24 | Zonelengtes met − en + en een schuif, bewaren in `settings.json` en terugzetten |
| #25 | Pauzeren bij vergrendelen en slaapstand, met een melding en "Hervatten" in de app |
| #26 | Nieuw ontwerp "Werkplaats bij nacht" (`design/DESIGN.md`): navigatie Lampen, Kleur, Scènes, Buren en Instellingen, aan en uit per lamp, scènes als kaarten |
| #28 | Korte README met schermafbeeldingen; de uitleg naar `docs/handleiding.md` en `docs/ontwikkelaars.md` |
| #29 | Een kleur in de instellingenbestanden die niet te lezen is, alleen die overslaan |
| #30 | Logbestanden in `%AppData%\BuurmansRgb\logs` |
| #31 | De instellingen-app maar één keer open: een tweede start haalt het venster naar voren |
| #32 | De pagina Kleur loopt mee met een scène uit het tray-menu |
| #33 | Bijhouden welke scène aan staat: witte rand op de kaart en een vinkje in het tray-menu |
| #34 | Hooguit 20 beelden per seconde naar het voorbeeld, en geen als de app ze niet toont |
| #35 | De installer als download via GitHub Releases (`.github/workflows/release.yml`) |
| #39 | Ook een waarde van het verkeerde soort in de instellingenbestanden alleen overslaan (`LenientJson`) |
| #40 | De scène die aan staat onthouden na een herstart (`activeScene` in `settings.json`) |
| #41 | "Alles aan", "Alles uit" en "Logbestand openen" in het tray-menu |
| #42 | Vloeiendheid (`framesPerSecond`) en pauzeren bij vergrendelen in de instellingen-app, zonder herstart |
| #43 | Op Kleur alleen de beelden van de gekozen lamp, en uitgezocht waar het geheugen van de app zit |
| #44 | Kleine verbeteringen: Nederlandse namen voor de gesimuleerde lampen, knipperen grijs tijdens een pauze, meldingen, − en + herhalen, knipperen pas als de keuze rust, spoor van een schakelaar, "Alles selecteren", namen voor schermlezers, `--exit`, autostart na verplaatsen, niet pollen tijdens vergrendelen, geheugen na het tray-menu |
| #46 | Tijden van scènes alleen op gekozen dagen (`loadOn`), met dag-knoppen bij "Vanzelf aanzetten" |

Daarvoor kwamen al: een refactor na de code-review (#11), de installatie-uitleg (#12, #15) en
het onderzoek naar OpenRGB meeleveren (#13).

## Risico's en aandachtspunten

Het grootste risico is fabrikantsoftware die tegelijk dezelfde LED's aanstuurt; sluit die af voordat je test.

| Risico | Gevolg | Aanpak |
| --- | --- | --- |
| iCUE, Armoury Crate, Synapse, G Hub, Mystic Light draaien mee | Flikkeren, kleuren springen terug | Uitschakelen of autostart uitzetten; noteren in `docs/hardware.md` |
| Moederbord, RAM en GPU gaan via SMBus | OpenRGB heeft eenmalig adminrechten en een low-level driver nodig | Dat regelt OpenRGB; BuurmansRgb zelf blijft zonder admin. Volg de actuele OpenRGB-documentatie voor de driver |
| Anti-cheat in sommige games | Low-level hardwaretoegang kan gemarkeerd worden | Geldt voor OpenRGB, niet voor BuurmansRgb; bij twijfel OpenRGB sluiten tijdens het spelen |
| Apparaat niet ondersteund door OpenRGB | Geen aansturing mogelijk | Check de supported-devices-lijst van OpenRGB vóór fase 5–8; headsets zijn het vaakst niet ondersteund |
| Apparaten resetten na slaapstand | Standaard fabriekseffect na hervatten | Na `PowerModeChanged.Resume` alles opnieuw toepassen (zit in fase 5–8 acceptatie) |
| OpenRGB SDK-protocolversie wijzigt | Verbinding of functies vallen weg | `OpenRGB.NET` en OpenRGB samen bijwerken; protocolversie loggen bij verbinden |

Open vraag: welke merken en modellen zitten er in de pc? Vul dat in `docs/hardware.md` in, zodat de criteria van taak 5–8 getoetst kunnen worden tegen de OpenRGB-lijst.
