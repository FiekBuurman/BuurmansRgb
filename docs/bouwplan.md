# BuurmansRgb – Bouwplan voor RGB-aansturing in C#

## Naam en concept

De app heet **BuurmansRgb**: een lichtgewicht Windows-app in C# die alle RGB in je pc vanuit één plek aanstuurt. Een knipoog naar de klussende buurmannen, want ook hier geldt: eerst simpel, dan stukje bij beetje uitbreiden.

BuurmansRgb draait als klein achtergrondproces met een tray-icoon. De instellingen-UI open je alleen als je iets wilt wijzigen; sluit je hem, dan verdwijnt hij volledig uit het geheugen.

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

Kern-interfaces in `Core`, als startpunt voor taak 0:

```csharp
public readonly record struct RgbColor(byte R, byte G, byte B);

public enum DeviceType { Motherboard, Ram, Gpu, Fan, LedStrip, Case, Keyboard, Mouse, Headset, Other }

public sealed record RgbZone(string Name, int LedCount, bool Resizable);

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

De loop per LED ziet er zo uit, hier als regenboog die over de strip schuift:

```csharp
public sealed class RainbowEffect : IEffect
{
    public bool IsAnimated => true;
    public double Speed { get; set; } = 0.2; // rondes per seconde

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        for (int i = 0; i < leds.Length; i++)
        {
            double hue = (i / (double)leds.Length + time.TotalSeconds * Speed) % 1.0;
            leds[i] = RgbColor.FromHsv(hue, 1.0, 1.0);
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
| Logging | `Microsoft.Extensions.Logging` naar bestand | Standaard, uitzetbaar |
| Tests | xUnit | Effecten en mapping testen zonder hardware |

Solution-indeling:

```text
BuurmansRgb.slnx
├─ src/
│  ├─ BuurmansRgb.Core                // modellen, interfaces, kleur-wiskunde
│  ├─ BuurmansRgb.Effects             // Static, Breathing, Rainbow, Wave, PerLed
│  ├─ BuurmansRgb.Contracts           // IPC-interface en DTO's
│  ├─ BuurmansRgb.Providers.Simulated // nep-apparaten voor ontwikkeling
│  ├─ BuurmansRgb.Providers.OpenRgb   // koppeling met OpenRGB SDK-server
│  ├─ BuurmansRgb.Engine              // tray, render-loop, IPC-server, opslag
│  └─ BuurmansRgb.UI                  // WPF, start op verzoek, stopt bij sluiten
└─ tests/
   ├─ BuurmansRgb.Core.Tests
   └─ BuurmansRgb.Effects.Tests
```

## Prestatiebudget voor achtergrondgebruik

Doel: met gesloten UI en een statische kleur gebruikt de engine vrijwel 0% CPU, omdat er dan niets te verversen valt. Onderstaande waarden zijn streefwaarden; meet ze in elke fase met Taakbeheer of `dotnet-counters`.

| Situatie | CPU | Geheugen (working set) |
| --- | --- | --- |
| Engine, statische kleur, UI dicht | ~0% (geen timer actief) | < 50 MB |
| Engine, animatie op 30 fps, UI dicht | < 1% | < 60 MB |
| UI open met live preview | geen harde eis | < 150 MB |

Maatregelen die dit mogelijk maken:

- **Render-loop alleen bij animatie.** Een statisch effect wordt één keer verstuurd; daarna stopt de `PeriodicTimer`.
- **Geen allocaties in de loop.** LED-buffers worden hergebruikt (`Span<RgbColor>`, vooraf gealloceerde arrays).
- **Alleen wijzigingen versturen.** Vergelijk met het vorige frame en stuur per apparaat alleen als er iets veranderde.
- **Instelbare framerate.** Standaard 30 fps, maximaal 60; animaties zijn tijdgebaseerd, niet frame-gebaseerd.
- **Lichte GC-instellingen in de engine:** `ServerGarbageCollector=false`, `ConcurrentGarbageCollection=false`, `InvariantGlobalization=true`.
- **Pauzeren bij vergrendelen of slaapstand** via `SystemEvents.SessionSwitch` en `PowerModeChanged`; na hervatten alles opnieuw toepassen, want veel apparaten resetten na slaap.
- **UI is een apart proces.** Sluiten = proces stopt = geheugen vrij.

## Fasering

Bouw in negen fasen; na fase 3 heb je al een werkende app met één echte kleur op echte hardware. Elke fase eindigt met iets dat je kunt starten en zien.

0. **Fundament.** Solution, `Core`-modellen, gesimuleerd apparaat, eerste tests.
1. **Engine + tray.** Achtergrondproces met tray-menu (UI openen, effecten pauzeren, afsluiten), opslag in JSON, optioneel autostart.
2. **UI + IPC.** WPF-venster met apparatenlijst, kleurkiezer, helderheidsschuif en een LED-preview; praat via de named pipe met de engine.
3. **OpenRGB-koppeling.** Verbinden met de SDK-server, apparaten ophalen, kleuren versturen, automatisch opnieuw verbinden.
4. **Effect-engine.** Static, Breathing, Rainbow, Wave en een per-LED-editor (de "loop" over LED's).
5. **Moederbord en RAM.** Eerste echte apparaattype; zones en headers tonen.
6. **GPU.** Apparaattype GPU, zelfde flow.
7. **Fans, case-LED en ARGB-strips.** Zones instelbaar in lengte (aantal LED's per strip), ventilatoren per ring.
8. **Toetsenbord, muis en headset.** Toetsenbord met matrixweergave per toets, muis met zones, headset waar OpenRGB het ondersteunt.

Na fase 8 als losse uitbreidingen: profielen per moment (gamen, werken, nacht), alle apparaten synchroon laten lopen, en eventueel eigen HID-providers voor apparaten die OpenRGB niet kent.

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

### Taak 3 – OpenRGB-koppeling

**Opdracht:** Bouw `Providers.OpenRgb` met NuGet `OpenRGB.NET`. Verbind met `127.0.0.1:6742`, vertaal OpenRGB-apparaten, zones en LED's naar de `Core`-modellen en stuur kleuren met de update-LED-aanroepen. Zet bij verbinden elk apparaat in de modus "Direct" of "Custom". Probeer bij verlies elke 5 seconden opnieuw te verbinden.

**Acceptatie:**

- [ ] Met OpenRGB-server actief verschijnen echte apparaten in de UI naast de gesimuleerde
- [ ] Statische kleur komt aan op minstens één echt apparaat
- [ ] OpenRGB stoppen en weer starten: BuurmansRgb herstelt zonder herstart
- [x] Gesimuleerde apparaten zijn uit te zetten in de instellingen (gelezen als `settings.json`: `simulatedDevices` op `false`, daarna de engine herstarten; een schakelaar in de instellingen-app staat in `docs/ideas.md`; de gebruiker bevestigt deze lezing)

### Taak 4 – Effect-engine

**Opdracht:** Implementeer in `Effects`: `StaticEffect`, `BreathingEffect`, `RainbowEffect` (kleur per LED-index), `WaveEffect` en `PerLedEffect` (een opgeslagen kleur per LED). Effecten zijn tijdgebaseerd en schrijven in een aangeleverde buffer. De engine draait een `PeriodicTimer` op de ingestelde fps, alleen als minstens één actief effect animeert. Voeg in de UI een per-LED-editor toe: klik een LED in de preview en geef hem een kleur, of selecteer een reeks en vul die met een verloop.

**Acceptatie:**

- [x] Unit-tests per effect op vaste tijdstippen
- [ ] 0 allocaties per frame in de render-loop (controleren met `dotnet-counters`) (een test
  meet 0 bytes per frame met `GC.GetAllocatedBytesForCurrentThread`; `dotnet-counters` staat
  niet op de bouw-pc, dus die controle doet de gebruiker nog, met de instellingen-app dicht:
  met de app open maakt de engine per frame een kopie voor het voorbeeld)
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

Stand taak 5 (moederbord en RAM): icoon, type en zones in de instellingen-app, een
gesimuleerd moederbord en twee RAM-sticks, en herstellen na het ontwaken zijn gebouwd en
getest zonder hardware (nepdata van OpenRGB en een nepklok). De vier criteria hierboven
vragen echte hardware; die test de gebruiker zelf, zie "Moederbord en RAM" in `README.md`.

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

Open vraag: welke merken en modellen zitten er in de pc? Vul dat in `docs/hardware.md` in, zodat fase 5–8 vooraf getoetst kunnen worden tegen de OpenRGB-lijst.
