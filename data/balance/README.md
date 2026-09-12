# Balance knobs

`knobs.json` says three things a tuning pass cannot work out from the content alone:

- **what may move**, per spell, between which bounds and by what step;
- **what each spell is for**, in words, so a number is never changed into something the spell was not;
- **what balanced means**, as a score over the metrics an evaluation already reports.

It is authoring metadata. The data builder reads `Creatures`, `Spells`, `TalentTrees` and `aliases.json`
only, so this file never reaches `game.schema.json` and never moves the content hash.

The content studio reads and writes it, and shows each spell's entry beside the numbers it governs — the
intent, the invariants, and every knob against the value the content carries today (`studio/README.md`). It
writes this file as **part of the same change** as the spell it is about ([ADR 0025](../../docs/adr/0025-the-balance-knobs-are-a-part-of-a-studio-change.md)),
so creating a spell seeds its entry, deleting one prunes it, and a session cannot leave `check-knobs` failing
through either door. The page refuses only what a browser can check — an empty intent, a pointer that addresses
no number, a value outside its own bounds, a duplicate pointer, bad bounds, a step of zero. Everything below
that needs the catalogue and the engine is still `check-knobs`', and it stays the authority.

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

Most targets read a metric of the whole run. Three read a **tier** instead — the spells offered at one depth
of the talent tree, which is the set a player is choosing between at that moment — and report the worst
tier: `tierUsageShare` (does one spell own its tier), `tierDamageSpread` (do its damaging spells hit
comparably hard per landed cast) and `tierWinSpread` (do they win comparably often). They exist because the
catalogue-wide reading hides a monopolised tier: on the nine-spell core content `spellUsageShare` reads
0.855 while `tierUsageShare` reads 1.000, because `heavy_strike` takes every landed cast of tier 0 and the
starting kit is not a choice at all.

Each skips what it cannot read rather than guessing: a tier nobody cast (that is `spellsNeverCast`), a spell
with no `Damage` effect at all (a heal and an attack share no unit), and a spell too few sides declared for
its own number to be anything but noise. Whether a spell is a damaging one is read from the content, never
from what its casts landed: otherwise lowering an attack until its hits are all absorbed would drop it out
of the comparison and *improve* the reading, paying the search to break spells.

A run explains its own score rather than leaving it to be reconstructed: `tune-content` reports what it
changed in the content's own words, which measurement the gain came from, what that gain cost elsewhere, and
what is still outside its range with the number it reads and the number it should be. Worth knowing when you
read one: a whole run explained by a single measurement is a run to be suspicious of, and a target sitting
inside its range costs the same anywhere inside it, so a change that makes one worse is invisible until it
leaves the range.

What "balanced" means is a choice, not a formula, and this block is where that choice is written down. Move
a band and say why in `docs/learning/journal.md`, the same as any other change that moves a number. Two
things are worth knowing before you do: the targets compete, because the score is a sum, so asking hard for
one thing is paid for elsewhere; and adding or reweighting a target makes every score before it
incomparable with every score after.

## The constraints

Hard rules. A candidate that breaks one is not scored at all.

| Constraint | What it refuses |
| --- | --- |
| `noNewStrictDominance` | A new pair where one spell is better than another on every axis and worse on none, **at the same depth in the talent tree or shallower**. A deeper spell outclassing a shallower one is what the tree is for and is not a pair. |
| `noIndistinguishableSpells` | Two spells with the same cost, Spell initiative, critical chance, targeting and effects. Effects are compared whole and as a multiset, the way the engine's `Spell.Indistinguishable` audit compares them; the engine reads the built schema and stays the authority. |
| `startingKitOffersAChoice` | Any of the three spells every creature starts with being strictly better than another. |

Dominance reads the talent tree. A spell is only compared against another at its own depth or deeper:
reaching a deeper node costs picks and prerequisites, so being better there is the reward, not a defect.
What is left is two spells offered at once with nothing to choose between them, or a pick that buys a
downgrade. Beyond that, the constraint is scoped to pairs a candidate **adds**: the ones already in the
catalogue are findings `check-knobs` lists with the exit code still 0. `startingKitOffersAChoice` is named
separately because that is the one hand every match is dealt, and journal entry `ci-9` is the whole story of
getting it wrong.

## The search

`tune-content` opens with a **sweep**: every knob on a spell the build carries, one step each way, played
once. Then it hill climbs, playing neighbours of the best thing it has found, each one a single knob nudged
one step, leaning four to one on the knobs the sweep showed can move a metric. A proposal that breaks a
constraint or moves more than `--max-changes` knobs is redrawn before the engine ever sees it, so the budget
goes on content worth playing.

The sweep is there because of a real miss. A uniform draw over 29 knobs with a budget of 40 candidates
leaves a one-in-four chance that any given knob is never tried, and the first full run lost that coin flip
on `lightning_bolt`'s energy cost — one move worth more than everything the search did find. `--no-sweep`
skips it when you want a quick look rather than an answer.

Every candidate costs one content build plus one evaluation per entry of `objective.evaluations`. On the 200
benchmark seeds an evaluation is about seven seconds, so a candidate is about twenty. The sweep is up to two
candidates per playable knob — on the nine-spell core content that is 29 knobs and 41 legal single steps.
The paired moves below add up to 80 more, and the deepening up to 18 on top, so the opening tops out at 139
candidates. The workflow then climbs 24 rounds of 6, which is 284 candidates and about 95 minutes against
its 180-minute timeout; the CLI's own defaults stay at 8 rounds of 4, because a local run should not take an
hour and a half unasked. `--no-pairs` and `--pair-depth 1` are the switches if a run gets tight. Raise the
budget rather than the step size: a wider step reaches further and reads worse in the diff.

The run writes `tune.json` (every candidate, its moves, its penalties and its metrics) and `content/`, the
changed spell files under the same tree they came from, so applying a proposal is a copy and reading one is
a diff. `--apply` does that copy. Nothing else is written to `data/`: the search works on a copy in
`<output>/work`.

**Run more than one seed.** A hill climb keeps only what improves, so which knobs it happens to draw first
decides what it finds. On this catalogue, `--seed 1` at the full budget draws 32 candidates and improves
nothing, while `--seed 11` finds a move in its first two: Ice Spear's Spell initiative from 2 to 1, and the
score from 33.7 to 29.6. Neither run is wrong; the space is mostly flat and the good moves are sparse.

What to expect here today: 26 of the 36 spells are never cast in a mirrored run, so a third of the
candidates change no metric at all and the report names them. Two thirds of the score is that dead content
and the first-mover share, neither of which one number on one spell can fix. The search is the right shape
for the last mile of a balance pass; the first mile is still a human deciding which spells should be worth
casting at all.

A proposal is a proposal. Read the moves against the `intent` of each spell it touched, then rebuild the
content, regenerate the benchmark digest, and write the journal entry — the same steps as any other content
change (`docs/learning/explained.md`).

## Keeping it honest

`check-knobs` fails when:

- a spell has no entry, or an entry names a spell no alias resolves to;
- an entry has no `intent`, so nothing says what its numbers are for;
- a pointer addresses nothing, or something that is not a number, or the same one is listed twice;
- bounds are the wrong way round, or a step of zero moves nothing;
- the value the content carries today falls outside its own bounds;
- a target reads an evaluation the objective does not declare, which would silently drop a term from
  every score;
- `startingKitOffersAChoice` names a spell nothing resolves to, which would silently check nothing.

`learning/tests/test_knobs.py` runs the same check against this repository, so adding, retuning or cutting
a spell without saying what it is for fails the build.

It also **reports**, with the exit code still 0, a spell whose whole box sits under a rival: the most it can
be worth anywhere inside its own bounds, against what a spell at its depth or shallower carries today. The
case it exists for is `pummel`, which tops out around 5.4 against `lightning_bolt`'s 6.7 — so a tuning pass
asked to make it a choice was searching a box that did not contain the answer, and then reported that it had
found nothing as though it had looked in the right place. Either side of such a pair is a way out, which is
why it is a finding and not a failure: widening the one and lowering the other are both answers, and picking
between them is a design decision.

The reading is coarse on purpose — no board, no targets, no defense, no cap at a target's health, no threat
behind a defensive effect (so a `DefenseBuff` is priced as `buff x amount x rounds`, a stand-in and not what
the scorer does with one), no kill term, which is the largest weight in the game and a threshold so it
rewards a reliable hit over a bigger average one, and neither the energy cost nor the Spell initiative that
`ActionScorer` prices when it picks an unlock. That last one is why `throwing_star` is reported: its entry
says its Spell initiative is worth more to the class than its damage, and none of that is in the number the
report prints. It is read off the agents'
own weights (`learning/weights/greedy.json`, which mirrors `ScoringWeights.Default`) rather than restated,
and it skips any spell with no `Damage` effect on either side of the comparison, for the reason
`tierDamageSpread` skips one: a heal and an attack share no unit. Without that rule it reports `rejuvenate`
and `guard`, cast for a survival it cannot see, and `wait`, which is *meant* to stay worse than acting.

## Two things a single step cannot find

`spellsNeverCast` counts exact zeros, and that leaves a hole a catalogue can sit in for a long time: on the
core content `pummel` took 6 of 5283 landed casts, dead in every sense that decides a game, and a buff that
moved it from 4 casts to 6 read as no change at all. `spellsBarelyCast` counts the spells under 1% of the
landed casts of **their own tier** — the tier and not the catalogue, because a tier is the set a player
chooses between at one moment, so a spell can be rare overall and still be the right pick where it is
offered. It counts **strictly the spells the other one does not**: a zero is a zero in either reading, and
with the same band and weight on both, counting it twice would double what the objective asks of that one
case. A run whose content carries no tiers reports it as missing rather than as zero, the way every tier
reading does.

And the opening sweep now also plays **two knobs of one spell together**, for the spells where no single step
could improve the score. `poison_slash` is why. On the `studio/content` branch, where it carries a bleed
of 1 per round over 2 rounds, raising the amount alone reaches 59 landed casts and the duration alone 17,
while raising both reaches 5155 of about 8900 — the largest move found against that catalogue's monopoly.
A climb that only ever moves one knob has to accept the flat step in between, worth 0.005, to find the path.

Read the size of that with the bounds it came from. **This** branch carries 1 over 1, so one step of each
knob lands on 2 over 2, which measures 310 landed casts of about 6150 against 3 today — fifty times better,
and still two steps of the duration short of the interior point. One step of each bridges one flat step; a
plateau needs more.

So a pair is **stepped further when its first step earned it**: when one step of each moved a measurement
and still scored *worse* than the content it came from. The reading answering says the knobs are live; the
score says the answer is not yet the one worth keeping, and nothing about the first move says the curve has
stopped rising — on the studio/content readings above, 2 over 3 is the point and 3 over 3, the corner, is
worse again. The deeper
step counts are **asymmetric** (`--pair-depth`, two by default: one-and-two, two-and-one, two-and-two),
because the move that matters here is one step of the amount and two of the duration, and a search that only
ever moved both knobs together would walk straight past it.

Two cases are deliberately left alone. A pair that **improves** needs nothing: it can become the best
candidate, and the climb steps on from there one knob at a time. A pair that **moves nothing at all** is a
pair on dead content, and a longer step into the dark costs an evaluation to learn the same thing again.

**The gate that picks which spells get paired is what decides whether any of this runs at all**, and the
first version of it was too narrow to fire. It admitted only spells where no single step moved a
measurement; on this content exactly one spell qualified — `wait`, which has a single knob, so no pair could
be built from it — and the pass played zero pairs. `poison_slash`, the spell the whole thing exists for, was
excluded, because its single steps do move readings, they just never move them anywhere better. Improving is
the right line: a spell one step already helps needs nothing here, because the climb takes that step and
goes on from it.

Only same-direction combinations are played. `--no-pairs` turns the whole thing off, `--pair-depth 1` leaves
the pairs at one step each, and deepening is capped at the six pairs closest to paying off — it is the only
part of the opening that multiplies, and a cap is cheaper to reason about than a rate.

**Deepening only has room where a knob does.** A second step that clamps at a bound lands on the content the
first step already produced, and that candidate is dropped rather than replayed. On the nine-spell core
content most knobs are whole numbers one step from their own ceiling, so a full run deepened exactly one
pair: the critical chance, whose step of 0.05 across 0.4 to 0.8 is the one axis with room to walk. Read a
small deepening count as bounds that are tight, not as a pass that declined to look.
