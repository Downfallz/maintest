# Balance knobs

`knobs.json` says three things a tuning pass cannot work out from the content alone:

- **what may move**, per spell, between which bounds and by what step;
- **what each spell is for**, in words, so a number is never changed into something the spell was not;
- **what balanced means**, as a score over the metrics an evaluation already reports.

It is authoring metadata. The data builder reads `Creatures`, `Spells`, `TalentTrees` and `aliases.json`
only, so this file never reaches `game.schema.json` and never moves the content hash.

```bash
uv run --project learning check-knobs            # the file against the content it describes
uv run --project learning check-knobs --strict   # and fail on the content findings too
uv run --project learning tune-content -o runs/tune-1        # search it for a better catalogue
uv run --project learning tune-content -o runs/tune-1 --apply  # and write the winning numbers into data/
```

The decision behind all of this is [ADR 0021](../../docs/adr/0021-tune-the-catalogue-with-a-declared-search-space.md).

## Why it exists

The loop already measures everything a balance pass needs: player 1's share of a mirrored run, average
rounds, the share ending at the round cap, spell usage and its entropy, the fizzle rate
(`docs/learning/training.md`). What it has no way to know is that Psycho Rush is meant to be the Berserker's
all-in and Wait is meant to stay worse than acting. Without that, an optimizer that only chases the metrics
will happily make every spell the same spell.

## Keys

Spells are keyed by their **unversioned alias** (`spell:pummel`), never by the versioned id. Cutting a `:v2`
in the studio repoints the alias, and the entry follows the spell instead of going stale on the version it
replaced.

## A knob

```json
{ "path": "/criticalChance", "min": 0.4, "max": 0.8, "step": 0.05 }
```

- `path` is a JSON pointer into the spell's own document: `/energyCost`, `/initiative`,
  `/effects/0/amount`, `/effects/1/durationRounds`.
- A move is `step` added to **the value the content carries today**, not to a grid, so a critical chance
  authored at 0.667 can reach 0.717 and 0.617 and stays reachable from itself. Results are rounded to three
  decimals and clamped to `[min, max]`.
- **Only the listed pointers may move.** Everything else is the spell's identity: its kind of effect, its
  targeting, whether a buff is permanent, how many targets it reaches. Changing one of those is a design
  decision and belongs in a commit with a reason, not in a search.
- `min` and `max` are drawn around what the spell is, not around what is legal. Hateful Sacrifice may fall
  to 7 damage and Basic Attack may rise to 3, and neither may become the other.

## What a spell entry carries

| Field | What it is for |
| --- | --- |
| `name`, `class` | What the content calls it, so a diff of this file reads without opening another. |
| `intent` | What the spell is for, in one or two sentences. The part a number cannot say. |
| `keep` | The invariants the intent rests on, in words: what has to stay true whatever the numbers do. |
| `note` | Optional. Something about this spell the bounds would otherwise hide. |
| `knobs` | The numbers that may move. |

Several entries say that a spell is currently half of itself: the caster-side effects, the defense and
energy debuffs, and the minions did not survive the port (`docs/domain/spells.md`). Those spells are marked
as waiting on a rule, and the point of saying so here is that a search must not "fix" them by making the
half that exists strong enough to compensate.

## The objective

`objective` is the score. Every target names a metric, the evaluation it is read from, a band, a `scale`
(how much deviation counts as one unit) and a `weight`:

```
score = sum over targets of  weight * (distance outside the band / scale) ** 2
```

Zero is on target and lower is better. A metric no evaluation measured is listed as missing rather than
counted as zero. Two evaluations are played on the benchmark seeds: `mirror` (greedy against greedy) reads
the content with skill held equal, and `skill` (greedy against random) checks that the content still rewards
playing well. A change that balances the first by flattening the second has removed the decisions instead of
balancing them.

What "balanced" means is a choice, not a formula, and this block is where that choice is written down. Move
a band and say why in `docs/learning/journal.md`, the same as any other change that moves a number.

## The constraints

Hard rules. A candidate that breaks one is not scored at all.

| Constraint | What it refuses |
| --- | --- |
| `noNewStrictDominance` | A new pair where one spell is better than another on every axis and worse on none. |
| `noIndistinguishableSpells` | Two spells with the same cost, Spell initiative, critical chance, targeting and effects. Same signature as the engine's `Spell.Indistinguishable` audit. |
| `startingKitOffersAChoice` | Any of the three spells every creature starts with being strictly better than another. |

Dominance is scoped to pairs a candidate **adds**. A spell three nodes down the talent tree outclassing a
starting spell is what progression means, so the pairs the catalogue already carries are not defects to be
optimized away: `check-knobs` lists them as findings and the exit code stays 0. What is a defect is taking a
decision out of a hand that had one, which is why `startingKitOffersAChoice` is named separately: that is
the one hand every match is dealt, and journal entry `ci-9` is the whole story of getting it wrong.

## The search

`tune-content` hill climbs: it plays the content as it stands, then plays neighbours of the best thing it
has found, each one a single knob nudged one step. A proposal that breaks a constraint or moves more than
`--max-changes` knobs is redrawn before the engine ever sees it, so the budget goes on content worth
playing.

Every candidate costs one content build plus one evaluation per entry of `objective.evaluations`. On the 200
benchmark seeds an evaluation is about seven seconds, so a candidate is about twenty and the default budget
of `--iterations 8 --neighbours 4` is thirty-three candidates, near ten minutes. Raise the budget rather
than the step size: a wider step reaches further and reads worse in the diff.

The run writes `tune.json` (every candidate, its moves, its penalties and its metrics) and `content/`, the
changed spell files under the same tree they came from, so applying a proposal is a copy and reading one is
a diff. `--apply` does that copy. Nothing else is written to `data/`: the search works on a copy in
`<output>/work`.

What to expect on this catalogue today: a full-budget run of 32 candidates moved nothing. Twenty-six of the
thirty-six spells are never cast in a mirrored run, so most single-knob moves change no metric at all, and
the report names them. The score is 33.7, and two thirds of it is that dead content and the first-mover
share, neither of which one number on one spell can fix. The search is the right shape for the last mile of
a balance pass; the first mile is still a human deciding which spells should be worth casting at all.

A proposal is a proposal. Read the moves against the `intent` of each spell it touched, then rebuild the
content, regenerate the benchmark digest, and write the journal entry — the same steps as any other content
change (`docs/learning/explained.md`).

## Keeping it honest

`check-knobs` fails when a spell has no entry, when an entry names a spell no alias resolves to, when a
pointer addresses nothing or something that is not a number, when bounds are the wrong way round, and when
the value the content carries today falls outside its own bounds. `learning/tests/test_knobs.py` runs the
same check against this repository, so adding, retuning or cutting a spell without saying what it is for
fails the build.
