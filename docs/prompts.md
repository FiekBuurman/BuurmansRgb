# Prompts voor agents

Start voor elke taak een nieuwe agent-sessie in deze map en plak de prompt.
Commit en check de acceptatiecriteria voordat je aan de volgende taak begint.
Taak 0 t/m 9 zijn gebouwd (zie `docs/bouwplan.md`); de prompts blijven staan als voorbeeld.

## Taak 0 – Fundament

```text
Lees AGENTS.md en docs/bouwplan.md.
Voer alleen Taak 0 – Fundament uit, precies zoals beschreven.
Werk niet vooruit naar volgende taken.
Controleer aan het eind alle acceptatiecriteria van Taak 0,
vink ze af in docs/bouwplan.md en meld per criterium of het gehaald is.
```

## Taak 1 t/m 4 en 9

Zelfde prompt, vervang alleen de taaknaam:

- Taak 1 – Engine en tray
- Taak 2 – UI en IPC
- Taak 3 – OpenRGB-koppeling
- Taak 4 – Effect-engine
- Taak 9 – Installer

## Taak 5 t/m 8 – per apparaattype

```text
Lees AGENTS.md, docs/bouwplan.md en docs/hardware.md.
Voer "Taak 5 t/m 8 – Per apparaattype" uit voor: <TYPE>.
Werk alleen aan dit apparaattype.
Controleer aan het eind de acceptatiecriteria, zeg welke ik zelf
op mijn hardware moet testen, en werk docs/hardware.md bij.
```

Vul voor `<TYPE>` in: moederbord en RAM · GPU · fans, case-LED en ARGB-strips ·
toetsenbord, muis en headset.

## Een uitbreiding na taak 9

```text
Lees AGENTS.md, docs/bouwplan.md en docs/ideas.md.
Bouw alleen dit: <IDEE>.
Werk niet vooruit; nieuwe ideeën gaan naar docs/ideas.md.
Haal het idee uit docs/ideas.md als het gebouwd is, voeg het toe aan
"Uitbreidingen na taak 9" in docs/bouwplan.md en werk docs/handleiding.md of docs/ontwikkelaars.md bij.
Meld aan het eind wat ik zelf nog moet testen.
```
