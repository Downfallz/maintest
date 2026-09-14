# Game content

Authoring format for the game resources (ADR 0009). One JSON file per item:

- `Creatures/*.json`: creature definitions.
- `Spells/**/*.json`: spells, nested by class and specialisation for readability only. What each one does,
  and where its numbers come from, is in `docs/domain/spells.md`.
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

## Tuning it

`balance/knobs.json` says which numbers of each spell a balance pass may move, what each spell is for, and
what balanced means as a score (ADR 0021). It is authoring metadata: the builder does not read it, so it
never reaches `game.schema.json` and never moves the content hash. See `balance/README.md`.

## Turning content off

Any creature, spell or talent tree may carry `"enabled": false` (ADR 0015). A disabled item leaves the
consolidated schema, and every reference to a disabled **spell** is pruned: from `startingSpellIds`, from the
`spells` of a talent node, and from `allOf` and `anyOf` prerequisites. The builder prints one `note:` line per
thing it left out.

Three cases are errors rather than prunings, because removing the reference would change a rule instead of
removing content:

- a creature whose talent tree is disabled, since it cannot be played;
- a creature left with no starting spell, since it needs at least one;
- a prerequisite gate — `allOf` or `anyOf`, on a node or on one of its spells — whose **every** spell is
  disabled, since an empty gate is no requirement at all: pruning it would unlock what it gates instead of
  closing it. A gate that keeps at least one spell is pruned normally.

In each case, disable what depends on the item too, or leave one of those spells enabled.

The flag is authoring-only: it never reaches `game.schema.json`, so writing `"enabled": true` changes nothing,
including the content hash.

## Effect kinds

| `kind` | Fields | Notes |
| --- | --- | --- |
| `Damage` | `amount` | instant |
| `Heal` | `amount` | instant |
| `EnergyGain` | `amount` | instant |
| `EnergyDrain` | `amount` | instant; takes at most the energy the target has |
| `Bleed` | `amountPerRound`, `durationRounds`, `stacking?` | lasting, default stacking `Stack` |
| `Regeneration` | `amountPerRound`, `durationRounds`, `stacking?` | lasting, default stacking `Stack`; heals before bleeds tick |
| `EnergyRegeneration` | `amountPerRound`, `durationRounds`, `stacking?` | lasting, default stacking `Stack`; gives energy at the start of each round, on top of the round's own gain |
| `Stun` | `durationRounds`, `stacking?` | lasting, default stacking `Refresh` — the one kind that does not stack (ADR 0041) |
| `DefenseBuff` | `amount`, `durationRounds` or `permanent: true`, `stacking?` | lasting, default stacking `Stack` |
| `DefenseDebuff` | `amount`, `durationRounds` or `permanent: true`, `stacking?` | lasting, default stacking `Stack`; subtracts from the same total the buff adds to, floored at zero |
| `InitiativeBuff` | `amount`, `durationRounds` or `permanent: true`, `stacking?` | lasting, default stacking `Stack`; adds to the same total the debuff subtracts from, floored at zero |
| `InitiativeDebuff` | `amount`, `durationRounds` or `permanent: true`, `stacking?` | lasting, default stacking `Stack` |

`stacking` is one of `Stack`, `Refresh`, `Ignore`.

## Effects on the caster

A spell may carry `casterEffects` next to `effects`, from the same table above, resolved **once per cast
against whoever cast the spell** (ADR 0031). The critical roll does not reach them, a cast that fizzles
applies none of them, and they go through the same rules as any other outcome — so a self-damage is reduced
by the caster's own defense, and it can kill its caster.

```json
"effects": [{ "kind": "Damage", "amount": 3 }],
"casterEffects": [{ "kind": "Heal", "amount": 2 }]
```

A spell still needs at least one ordinary effect: this is a half of a spell, never a whole one. Leave the
field out when there are none — an empty list is not content, and the builder drops one to nothing so that a
spell without caster effects hashes exactly as it did before the field existed. A knob addresses them the
usual way, `/casterEffects/0/amount`.
