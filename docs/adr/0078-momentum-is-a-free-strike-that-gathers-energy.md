# 0078. Momentum is a free strike that gathers energy

Date: 2026-09-25

Status: Accepted

## Context

Momentum is the one spell the Assassin package teaches (ADR 0056). Legacy Momentum did nothing, so the port
gave it `EnergyRegeneration`, 2 energy a round for 3 rounds, which [ADR 0020](0020-energy-regeneration-and-the-price-of-energy.md)
had added and named it for. It was Wait's opposite trade: energy built over the next rounds rather than handed
over now.

No agent played it. On 800 seeds from 3000000, on content `0f036b75`:

- the exploring run resolved it 21 times;
- the Greedy mirror resolved it 9 times;
- the exploit panel resolved it once.

`check-knobs` read the top of its box at 2.40 a round, against rivals at 7 and more. The only other way out
was to cut the four spells above it until they were as dead as it was. The Assassin was buying a package for
its initiative bonus and nothing else.

The owner asked for a different spell: an attack that costs no energy, deals two or three and gives two or
three energy back.

## Decision

**Momentum costs nothing, deals 2 damage to one enemy, and gives its caster 2 energy in the same cast.**

- It is Offensive, with one enemy as its target and a critical chance of 0.
- The energy is a caster effect ([ADR 0031](0031-an-effect-that-lands-on-the-caster.md)), the same shape as
  Parasite Jab's heal on its caster. It is Wait with a blade in it: the same activation, the same price, and a
  hit on top of the income.
- Its knobs are the damage and the energy, each from 1 to 3, so a tuning pass can take it anywhere in the
  owner's range. The cost is not a knob, because costing nothing is the point of the spell.
- No spell carries `EnergyRegeneration` any more. The kind stays in the engine and in the rules, in its place
  in the start-of-round passes, and the tabletop books say no card applies it.

## Consequences

The measurements below are on 800 seeds from 3000000, and on the 200 from 995317 that the tuning workflow's
hold-out replays.

- Good: Momentum gets played.

  | run | resolved before | resolved after |
  | --- | --- | --- |
  | exploring | 21 | 101 |
  | Greedy mirror | 9 | 207 |

- Good: the objective does not get worse. It reads 7.485 to 5.191 on the 800 seeds, most of it
  `tierWinSpread`, the noisy term, and 4.051 to 4.132 on the 200. Match length barely moves: the exploring run
  goes from 10.56 to 10.69 rounds, the Greedy mirror from 10.35 to 10.67.
- Neutral: at 3 damage and 3 energy, the top of the owner's range, the same 800 seeds read 5.735. That is the
  same within the noise, with Momentum resolved 146 times on the exploring run. 2 and 2 ship, and the knobs
  leave the rest to a tuning pass.
- Neutral: in the Greedy mirror the side that casts Momentum wins 213 of its 222 sides, and on the exploring
  run 72 of 109. Nothing here separates what the spell does from which side gets to cast it. The package's win
  gap is a term the tuner already reads (`tierWinSpread`), so a pass will push back if it is the spell.
- Bad: `check-knobs` still reports Momentum. It reads a spell a round and floors a cast at one round of income
  (`_rounds_a_cast`), so it gives a spell that costs nothing no credit for the energy it leaves free. It reads
  3.90 at the top of the new box. The game does not agree with that reading, and this ADR does not change the
  reading.
- Bad: the tabletop box loses a use for its Energy regeneration tokens (`components.md`).

## Alternatives considered

- **Keep the regeneration and widen its bounds.** Clearing a rival at 7 alone would take about 24 energy a
  cast, which is no longer the spell the entry described.
- **Momentum as a buff to the next attack.** Nothing in the effect taxonomy expresses "the next cast", and the
  owner asked for an attack.

## Follow-up

- `data/Spells/scoundrel/assassin/momentum.v1.json`, its entry in `data/balance/knobs.json`, and
  `docs/domain/spells.md` (its row is marked with a †).
- `docs/tabletop/rulebook.md`, `player-aid.md`, `components.md` and `translation.md`.
- The benchmark digest of content `813bb91b`.
