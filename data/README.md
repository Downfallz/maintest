# Game content

Authoring format for the game resources (ADR 0009). One JSON file per item:

- `Creatures/*.json`: creature definitions.
- `Spells/**/*.json`: spells, nested by class and specialisation for readability only.
- `TalentTrees/*.json`: talent trees.
- `aliases.json`: unversioned ids (`spell:pummel`) to their current versioned id (`spell:pummel:v1`).

References between items (starting spells, talent prerequisites) may use either form; the data builder resolves
aliases and validates every reference:

```bash
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst
```

The output, `data/dst/game.schema.json` and its SHA-256 in `game.schema.sha256`, is generated and ignored by git.
Unknown JSON properties are errors, so a typo in a field name is caught at build time.

## Effect kinds

| `kind` | Fields | Notes |
| --- | --- | --- |
| `Damage` | `amount` | instant |
| `Heal` | `amount` | instant |
| `EnergyGain` | `amount` | instant |
| `Bleed` | `amountPerRound`, `durationRounds`, `stacking?` | lasting, default stacking `Refresh` |
| `Stun` | `durationRounds`, `stacking?` | lasting, default stacking `Refresh` |
| `DefenseBuff` | `amount`, `durationRounds` or `permanent: true`, `stacking?` | lasting, default stacking `Stack` |
| `InitiativeDebuff` | `amount`, `durationRounds` or `permanent: true`, `stacking?` | lasting, default stacking `Stack` |

`stacking` is one of `Stack`, `Refresh`, `Ignore`.
