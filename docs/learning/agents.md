# Baseline agents

The agents the engine ships without a model (learning phase L5), the scoring they share, and how to tune it.

| Agent | Spec | What it does |
| --- | --- | --- |
| Random | `random` | Picks uniformly among the options. The floor every other agent is measured against; deterministic for a seed. |
| Greedy | `greedy` | One-step lookahead with the built-in weights below. The deterministic baseline of the benchmark digest. |
| Heuristic | `heuristic:<weights file>` | The same lookahead with the weights read from a JSON file (`learning/weights/greedy.json` is the built-in set), so the weights can be searched (L6) without a model runtime. |
| Policy | `policy:<policy.json>` | A trained policy (`docs/learning/training.md`): scores the candidate actions with one weight row per action key and takes the best. Refused when its feature schema is not the current one. |
| Exploring | `explore:<rate>` | Greedy, except that the given share of decisions is taken uniformly at random (ADR 0014). For recording datasets a value regression can learn from, never for a baseline: it draws from a random source, so it is deterministic for a seed but not for the digest. |

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

Every weight is expressed in the same unit: **one point of effective damage**. `damage` is 1.0 by
definition, and each other weight says how many points of damage that thing is worth to the bot. So a kill at
5.0 means "worth five damage on top of the damage that killed", and the bot takes a kill over five points of
damage spread elsewhere.

| Weight | Value | In plain words |
| --- | --- | --- |
| damage | 1.0 | The unit. One point per point of damage that actually lands (damage past a target's health is not counted). |
| kill | 5.0 | Finishing a creature is worth five damage on top of the hit. It buys the bot the enemy's whole future turn, so it is the strongest pull in the table. |
| heal | 0.8 | Healing an ally is worth a little less than hurting an enemy: it only counts what the target was missing, and it does not shorten the match. |
| stun | 3.0 | Taking a round away from a creature is worth three damage. Between a kill (all its rounds) and a plain hit (none). |
| bleed | 0.8 | Damage over time is discounted against damage now: the target may die first, and the bot only counts the health it could still reach. |
| buff | 0.5 | Half a point per point of defense per round. Defense is indirect: it may prevent damage that was never going to come. |
| energy | 0.2 | Keeping a point of energy for the next round is worth a fifth of a damage. Enough to break a tie towards the cheaper spell, not enough to make the bot hoard. |
| risk | 2.0 | A wasted action (a fizzle, or the share of targets that vanished before the spell resolved) costs two damage. Roughly one average hit thrown away. |

### Where these numbers come from

They were **hand-set as a starting point**, in the commit that introduced the agents (phase L5), from the
readings above: pick damage as the unit, then say what a kill, a stun and a wasted turn are worth in damage.
They are **not** the output of a search, and no searched weights file is committed. `ScoringWeights.Default`
is the single source; `learning/weights/greedy.json` holds the same eight numbers so `heuristic:<file>` and
`greedy` start from the same place, and a test on each side of the repository pins the two together.

To move them, do not edit them by feel: run `search-weights` (`docs/learning/training.md`), which plays each
candidate set against a fixed opponent on the benchmark seeds and keeps what wins, and leave the result next
to `greedy.json` under its own name. Changing `greedy.json` itself changes nothing for `greedy` (which reads
the built-in values); changing `ScoringWeights.Default` changes the benchmark baseline, so the digest moves
and CI asks for a new one.

A heuristic agent is stamped as `Heuristic:<path>@<fingerprint>`, the fingerprint being eight hex digits of the
weights the file held when the run started, so two runs on different weights at the same path never share a
stamp.

A weights file lists any subset of these names in camelCase (`{ "kill": 8, "risk": 1 }`); a missing name
keeps the built-in value, an unknown one is an error, every value must be a finite number.
