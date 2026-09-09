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

The content studio edits all of this in a browser (`studio/README.md`):

```bash
dotnet run --project src/DownfallArena.Cli -- studio
```

## Turning content off

Any creature, spell or talent tree may carry `"enabled": false` (ADR 0015). A disabled item leaves the
consolidated schema, and every reference to a disabled **spell** is pruned: from `startingSpellIds`, from the
`spells` of a talent node, and from `allOf` and `anyOf` prerequisites. The builder prints one `note:` line per
thing it left out.

Three cases are errors rather than prunings, because removing the reference would change a rule instead of
removing content:

- a creature whose talent tree is disabled, since it cannot be played;
- a creature left with no starting spell, since it needs at least one;
- a talent node whose **every** `anyOf` spell is disabled, since an empty `anyOf` is no requirement at all —
  pruning it would unlock the branch it gates instead of closing it.

In each case, disable what depends on the item too, or leave one of those spells enabled.

The flag is authoring-only: it never reaches `game.schema.json`, so writing `"enabled": true` changes nothing,
including the content hash.

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
