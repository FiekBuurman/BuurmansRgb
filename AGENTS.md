# BuurmansRgb – instructies voor agents

BuurmansRgb is een lichtgewicht Windows-app in C# (.NET 10, WPF) die RGB-verlichting
in een pc aanstuurt via de OpenRGB SDK-server. Het volledige plan, inclusief architectuur,
taken, acceptatiecriteria en de uitbreidingen na taak 9, staat in `docs/bouwplan.md`. Lees dat
eerst. De instellingen-app volgt het ontwerp in `design/DESIGN.md`; afwijkingen daarvan staan
in `README.md`.

## Werkafspraken

- **Eén taak per keer.** Voer alleen de taak uit die de gebruiker noemt. Niet vooruitwerken
  naar een volgende fase; ideeën noteren in `docs/ideas.md`.
- **Code en namen in het Engels, UI-teksten in het Nederlands.** UI-teksten in een
  resourcebestand (`Resources/Strings.resx` van de engine en de instellingen-app), zodat
  vertalen later kan.
- **`BuurmansRgb.Core` kent geen hardware, geen UI en geen IPC.** Afhankelijkheden lopen
  alleen naar binnen, richting `Core`.
- **Elke wijziging bouwt en test groen.** `dotnet build -warnaserror` en `dotnet test`
  voor elke commit.
- **Testen zonder hardware is de standaard.** Gebruik `Providers.Simulated`; echte hardware
  alleen voor de acceptatie van taak 3 en 5–8 (en Windows Sandbox voor taak 9), en die test
  de gebruiker zelf.
- **Prestaties meten, niet aannemen.** Bij elke engine-wijziging CPU en geheugen noteren
  in de commit- of PR-beschrijving.
- **Kleine commits** met een duidelijke boodschap per stap; elke taak in een eigen branch
  (`taak-0-fundament`, `taak-1-engine-tray`, ...), een uitbreiding na taak 9 in een branch met
  een korte Nederlandse naam (`effect-snelheid`, `profielen-tray`, ...).
- **Definitie van klaar:** alle acceptatiecriteria van de taak afgevinkt in
  `docs/bouwplan.md`, `README.md` bijgewerkt met hoe je het nieuwe deel start of test. Een
  gebouwd idee gaat uit `docs/ideas.md`.

## Afsluiten van een taak

Meld aan het eind per acceptatiecriterium of het gehaald is, wat de gebruiker zelf nog
moet testen, en wat je in `docs/ideas.md` hebt gezet.
