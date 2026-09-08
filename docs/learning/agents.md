# Baseline agents

The agents the engine ships without a model (learning phase L5), the scoring they share, and how to tune it.

| Agent | Spec | What it does |
| --- | --- | --- |
| Random | `random` | Picks uniformly among the options. The floor every other agent is measured against; deterministic for a seed. |
| Greedy | `greedy` | One-step lookahead with the built-in weights below. The deterministic baseline of the benchmark digest. |
| Heuristic | `heuristic:<weights file>` | The same lookahead with the weights read from a JSON file (`learning/weights/greedy.json` is the built-in set), so the weights can be searched (L6) without a model runtime. |

Every command takes `--p1` and `--p2`:

```bash
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --p1 greedy --p2 random
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 greedy --p2 heuristic:learning/weights/greedy.json --seeds benchmarks/benchmark-seeds.json
```

## The lookahead

`ActionScorer` scores an action (an actor, a spell, a target set) on the current board without touching it:
it resolves the action with the domain's own `ResolutionRules` on the snapshots twice, once with a forced
critical roll and once with a forced miss, and weighs the two scores by the actor's critical chance for that
spell. So the expected damage includes the critical contribution, and a spell that fizzles (not known, not
affordable, no legal target) scores as a wasted action.

The score of one resolution, with the weights `w`:

| Term | Counts | Sign |
| --- | --- | --- |
| `w.damage` x effective damage | damage capped at the target's health, per target | for an enemy, against an ally |
| `w.kill` per kill | a target whose health the damage reaches | for an enemy, against an ally |
| `w.heal` x effective healing | healing capped at what the target was missing | for an ally, against an enemy |
| `w.stun` per stun | a Stun on a target still alive after the damage | for an enemy, against an ally |
| `w.bleed` x expected bleed damage | amount per round x rounds (a permanent condition counts three), capped at the health left after the hit | for an enemy, against an ally |
| `w.buff` x amount x rounds | a DefenseBuff, or an InitiativeDebuff (amount only) | a buff for an ally, a debuff for an enemy, and the reverse against |
| `w.energy` x energy kept | the actor's energy after the cost | always |
| `-w.risk` | a fizzle, or the share of targets dropped at resolution | always |

Decisions:

- **Intent**: for each castable spell, the best target set by expected score; the spell with the best
  score. Ties go to the first spell in ordinal id order.
- **Targets**: the best target set of the declared spell on the board at reveal time; no target when the
  spell is no longer castable.
- **Speed**: Quick when some castable spell kills an enemy without a critical, Standard otherwise.
- **Evolution**: for each unlockable spell, its value as if the creature knew it and could afford it (the
  best target set on the current board); unlock the highest, pass only when nothing can be unlocked.

Both agents are deterministic: the same board gives the same decision, so a Greedy versus Greedy evaluation
on the benchmark seeds replays exactly. That is what makes the benchmark digest an engine-change detector.

## Built-in weights

| Weight | Value | Reading |
| --- | --- | --- |
| damage | 1.0 | one point per effective damage |
| kill | 5.0 | a kill is worth five damage |
| heal | 0.8 | healing counts a little less than damage |
| stun | 3.0 | a stun is worth three damage |
| bleed | 0.8 | future damage is discounted |
| buff | 0.5 | per point of defense per round |
| energy | 0.2 | keeping energy for later is worth a little |
| risk | 2.0 | a wasted action costs two damage |

A heuristic agent is stamped as `Heuristic:<path>@<fingerprint>`, the fingerprint being eight hex digits of the
weights the file held when the run started, so two runs on different weights at the same path never share a
stamp.

A weights file lists any subset of these names in camelCase (`{ "kill": 8, "risk": 1 }`); a missing name
keeps the built-in value, an unknown one is an error, every value must be a finite number.
