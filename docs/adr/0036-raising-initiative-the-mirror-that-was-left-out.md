# 0036. Raising initiative, the mirror that was left out

Date: 2026-09-13
Status: Accepted

## Context

[ADR 0035](0035-lowering-defense-and-taking-energy.md) added `DefenseDebuff` and `EnergyDrain` because the
taxonomy could only ever raise a stat, and four spells had been substituted into tempo for want of a way to
say what they meant. It left one pair asymmetric: `InitiativeDebuff` exists and has no mirror. So the same
argument now runs the other way round, and Death Squad is the spell standing in it.

Legacy Death Squad is `Temporary Initiative +10` and `Temporary Critical +100%`, one round, on three allies —
a team haste. Neither half was expressible, so it was approximated as *the tempo it was meant to buy*: one
energy to each ally. Energy is 0.2 a point ([ADR 0020](0020-energy-regeneration-and-the-price-of-energy.md)),
so the approximation reads **0.60 a round against a tier-3 band of 8 to 14**, its own bounds reach **1.20**,
and it is cast **0 times in 400 matches**. `check-knobs` has reported it as a spell no bound can make a
choice for as long as the check has existed, and the finding is right: the numbers are not what is wrong with
it.

Every way of fixing it without a new kind makes it a copy of another class's spell — a team heal, a team
buff, a sweep — which is the defect this pass exists to remove.

## Decision

We will add **`InitiativeBuff`**: a `LastingEffect` with an amount and a `Duration` that may be permanent,
exactly `InitiativeDebuff`'s shape and exactly the shape `DefenseDebuff` took from `DefenseBuff`.
`Creature.CurrentInitiative` adds the sum of them before subtracting the debuffs, the way `TotalDefense`
already adds buffs and subtracts debuffs, and `Initiative` floors at zero like every other stat.

It is priced with the `initiative` weight that already exists, at `initiative x amount x rounds`, the same
reading `ConditionScore` gives a debuff — one price for one point whether it is given or taken, which is what
[ADR 0032](0032-measure-the-initiative-weight.md) measured that weight to be.

Death Squad becomes `InitiativeBuff 2` for one round on up to three allies.

The critical half of legacy Death Squad stays out. A condition that changes the critical roll is not a mirror
of anything the taxonomy has: `CriticalChance` belongs to a creature and a spell, is read once at resolution,
and is a probability rather than a quantity. That is a different shape and so a different decision.

## Consequences

- Good: Death Squad says what it meant, and the Assassin's tier-3 pick stops being a spell the bots never
  cast. It reads 12.60 a round, inside the band, on the first shape tried.
- Good: the stat pairs are symmetric at last. Defense and initiative can each be raised and lowered, and no
  spell has to borrow another stat to mean "faster" or "slower".
- Bad: **the feature schema changes again**, `features:v4` to `features:v5`, the second bump in a day. A new
  condition kind is a new observation layout and there is no cheaper way to add one.
- Bad: **the benchmark digest moves**, and scores either side of this are not comparable.
- Bad: initiative is the most expensive weight in the game at 2.1, so this kind is the easiest one to
  over-tune with. Death Squad's bounds stop at an amount of 3 and carry no duration knob for that reason;
  the top corner still reads 18.90 a round, above the band, which is headroom for the tuner rather than a
  target for it.
- Neutral: `features.md` records that base initiative is recoverable as `initiative + InitiativeDebuff_amount`
  (ADR 0017). It becomes `initiative + InitiativeDebuff_amount - InitiativeBuff_amount`, still recoverable,
  and still lossy only where the debuffs floor the current initiative at zero.
- Neutral: no new scoring weight. The weights list and its fingerprint are unchanged.

## Alternatives considered

- **Leave it substituted and accept a dead spell.** It has been dead since the port, `check-knobs` has said so
  the whole time, and the class's other support spell is dead beside it. Two dead spells in one class is the
  class not existing.
- **Fix it with its bounds.** Its ceiling is 1.20 against a floor of 8. There is no move inside the box.
- **Re-theme it to something expressible** — a team heal, a team defense buff, a sweep. Each one already
  belongs to another class, and a tier-3 spell that copies a tier-2 one from elsewhere is what this pass is
  removing.
- **Add the critical buff at the same time**, so Death Squad is whole. It is a new shape rather than a mirror,
  it is the one thing in the taxonomy that is a probability, and pairing it here would hide a real decision
  inside an easy one.
- **Signed amounts on `InitiativeDebuff`.** Refused for the same reason ADR 0035 refused it: the codebase has
  no signed effect anywhere, and a sign would have to be read by `CurrentInitiative`, `ConditionScore`,
  `cast_value`, `dominates` and the studio's editor.

## Follow-up

- `src/DownfallArena.Domain/Resources/Effects/InitiativeBuff.cs`; `Creature.CurrentInitiative`.
- `ActionScorer.ConditionScore`; `FeatureSchema.ConditionKinds` and `ObservationBuilder.Amount` — both lists,
  which is the pair ADR 0035 shipped broken.
- `GameSchemaMapper`, `data/README.md`, the studio's effect editor, the viewer's condition labels and columns.
- `learning/src/downfall_learning/knobs.py`: the kinds and `cast_value`.
- `docs/learning/features.md` (a new published version), `docs/learning/agents.md`, `docs/learning/artifacts.md`.
- `docs/domain/spells.md` (the third bullet of "what did not survive"), `glossary.md`, `game-rules.md`.
- The benchmark digest, and `models/` when a policy is next trained.
