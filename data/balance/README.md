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
uv run --project learning score-content -o runs/score --seeds unseen.json   # score the content as it stands, no search
```

`score-content` exists for one reason: a tuning pass picks the candidate that scored best on the seed file
the objective names, so the winner's score is the winner's and not a fair one. Played again on a seed file no
candidate saw, the same catalogue gets a number that is — and so does the catalogue it replaced, which is
the comparison the `Tune the catalogue` workflow makes before it pushes a proposal.

The decision behind all of this is [ADR 0021](../../docs/adr/0021-tune-the-catalogue-with-a-declared-search-space.md).

## Why it exists

The loop already measures everything a balance pass needs: player 1's share of an evenly matched run, average
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

- `path` is a JSON pointer into the spell's own document: `/energyCost`, `/criticalChance`,
  `/effects/0/amount`, `/effects/1/durationRounds`. Not `/initiative`: a spell carries none since ADR 0059,
  and `check-knobs` refuses a knob that addresses a field the spell does not have.
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
counted as zero. Four evaluations are played on the benchmark seeds. `mirror` (greedy against greedy) reads
how long a match lasts and how often it runs out of rounds, with skill held equal. `variety`
(`explore:0.2` against itself) reads whether the content offers a choice, and since ADR 0062 who wins it:
under packages the greedy mirror ties every initiative and gives the tie to the seat, so Player 1 took 400 of
400 there whatever the content, and the seat question is only answerable where the two sides diverge. `skill` (greedy against random)
checks that the content still rewards playing well. `exploit` (a panel of searched weights files against
greedy) reads how far a player who only wants to win gets against the way the game is meant to be played —
scored on **how fast** the best of the panel closes it out, since 2026-09-17, because whether it wins at all
stopped being able to tell two catalogues apart ([ADR 0053](../../docs/adr/0053-score-the-exploit-term-on-the-clock-not-on-the-win-rate.md)).

`variety` exists because `Greedy` takes an argmax: two spells of near equal value do not split the casts, the
marginally better one takes nearly all of them, and no content makes the largest share fall below about a
half (ADR 0029 has the sweep). Seven targets that need a spell to be cast in order to mean anything are read
there instead — the spread, the uncast counts, and the per-tier hit and win readings. Their bands did not
change when they moved; the readings did, and two spells every journal entry called never cast turn out to be
cast the moment a player looks at them. The rate stays at 0.2 because exploration is a dial between measuring
the content and measuring the dice: a higher rate reads better precisely because the play is more random.

The four are one definition and are read together. `Greedy` is the taste the catalogue is balanced for: nine
prices, each decided rather than felt (ADR 0018, ADR 0028). `exploit` seats an agent that shares none of that
taste and beats `Greedy` anyway, so its gap is the price the content charges for playing it the intended way.
Closing that gap by flattening the game fails `skill`; keeping one dominant line open fails `tierUsageShare`
on `variety`. What the four together ask for is content where playing well matters, where more than one line
wins, and where winning does not require abandoning the taste.

`exploit` is the only evaluation that names files, and since [ADR 0052](../../docs/adr/0052-read-the-exploit-term-as-the-best-of-a-panel.md)
it names a **panel**: every searched set that has beaten Greedy on this catalogue is played and the one with
the highest win rate decides the evaluation, its rounds and its spell outcomes included, ties going to the
fastest win (ADR 0053). One agent only ever
reads what that agent happens to punish — the same one-spell move was worth 0.013 to `search-4` and 0.235 to
`stun-first`, and on the moved catalogue the ranking inverted (journal, 2026-09-17) — so a single reading
credited a content change for blinding one agent. A set still goes stale when the content moves, but the
answer is now to **add** the newest search to the panel rather than to replace the file: an older set costs
one evaluation and is sometimes the only one that still sees the hole. On `7e199df4` the panel reads 1.000,
and so does the one reachable move measured so far — Crushing Stomp at the weakest of its own bounds — once a
set is searched against that catalogue too, which is why the win rate is no longer what this term scores: the
panel closes the current catalogue in 5.785 rounds and the moved one in 7.63, and **that** is what the
objective reads (ADR 0053, and the 2026-09-17 entries for how the three earlier prices of that same move,
116, 55 and 0, were each an artefact of which agent was asked). A candidate costs five evaluations here and
eight in all, which is the price of the reading. Only
agent A is read as a panel: agent B is the opponent it is measured against, and `check-knobs` refuses a list
there. It also refuses an empty panel, and a knobs file whose evaluation names a weights or policy file that
is not there, because otherwise the engine fails one candidate at a time, once a search has already started.

Most targets read a metric of the whole run. Three read a **package** instead — the spells one evolution pick
buys together ([ADR 0058](../../docs/adr/0058-a-tier-is-the-package-the-balance-objective-reads.md)) — and
report the worst package: `tierUsageShare` (do its casts all go to one of them), `tierDamageSpread` (do its
attacks hit comparably hard, per target of a landed cast) and `tierWinSpread` (do they win comparably often).
They exist because the catalogue-wide reading hides a package that sold its pick short: on the shipped
catalogue `spellUsageShare` reads 0.297 while `tierUsageShare` reads 0.928, because `tier:prowler:v1` splits
1813 casts of `poison_slash` against 140 of `throwing_star`.

The spells of a package are a bundle and not alternatives — one pick buys all of them — so this is not the
"is it a choice" the tree depth claimed to read. It is the narrower question: did the other spells in the
package come along for nothing.

Each skips what it cannot read rather than guessing: a package nobody cast (that is `spellsNeverCast`), a
package teaching one spell (a lone spell takes all of its own casts whatever the content does, and nine of
the twenty-one shipped packages teach one), a spell that is not an attack (a heal and an attack share no
unit), and a spell too few sides declared for its own number to be anything but noise. Whether a spell is an attack is read from the content, never from what its
casts landed: otherwise lowering an attack until its hits are all absorbed would drop it out of the
comparison and *improve* the reading, paying the search to break spells.

An **attack** is a spell whose `Damage` is at least a third of what its cast puts on a board, priced with the
agents' own weights, and its hits are compared **per target** rather than per cast
([ADR 0043](../../docs/adr/0043-a-control-spell-is-not-an-attack-and-reach-is-not-force.md)). Both readings
were cruder until that ADR, and between them they held this metric at its cap of 5.000 for every candidate a
search could build — 36 of an objective of 42.67, flat. Carrying a `Damage` effect at all was the test, so
`tranquilizer_dart` (two damage and a two-round stun, whose own `keep` calls the damage a rounding error)
anchored tier 3 at 0.27 against the heaviest sweep in the game at 18.92; and reading per cast asked a
three-target spell to total what a single-target one totals, charging it for reach rather than for force. A
term at its cap is worse than useless: a search cannot be graded on it, so it may freely damage the thing the
term names, which is what happened to `soul_devourer` in tune run 8.

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
| `noIndistinguishableSpells` | Two spells with the same cost, critical chance, targeting and effects. Effects are compared whole and as a multiset, the way the engine's `Spell.Indistinguishable` audit compares them; the engine reads the built schema and stays the authority. |
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

Every candidate costs one content build plus one evaluation per **agent** the objective names, which is eight
since [ADR 0052](../../docs/adr/0052-read-the-exploit-term-as-the-best-of-a-panel.md) seated a panel of five
on `exploit`: three entries of one agent, and five of it. On the 200 benchmark seeds an evaluation is about
**3.8 seconds** on a four-core machine, so a candidate is about **30 seconds** across the eight, where it was
14 across the four (ADR 0030 plays the 400 matches of an evaluation at once; it was 7 seconds an evaluation
and 34 a candidate when they went one at a time). The sweep is up to two candidates per playable
knob — on the nine-spell core content that is 29 knobs and 41 legal single steps. The paired moves below add
up to 80 more, and the deepening up to 18 on top, so the opening tops out at 139 candidates. The workflow
then climbs.

**Those counts are from the nine-spell core content and the catalogue has outgrown them.** Today it is **155
playable knobs and 243 legal single steps**, so the opening sweep alone is 243 candidates. And on a GitHub
runner a candidate is **61.7 seconds**, not 14: tune 10 played 349 of them in 5h59m at an even pace. The 3.8
seconds above is what an evaluation costs *here*, on four cores; the runner is the machine a workflow has to
fit, and there the sweep alone is **4h10 of a six-hour job**.

So the workflow's defaults are what fits rather than what used to:

| | candidates | at 61.7 s |
| --- | --- | --- |
| the opening sweep | 243 | 4h10 |
| plus the catalogue itself and 6 rounds of 6 | 280 | **4h48** |
| 24 rounds, as tune 10 ran it, paired opening on | killed at 349 | **> 6h, nothing kept** |

Tune 10 was killed at the ceiling with nothing to show, since the proposal is only written when the search
finishes. The paired opening is off by default for the same reason: it builds every legal pair of one spell's
knobs for every spell the sweep did not improve, in both directions, which is not capped and grows with the
catalogue. The `pairs` field turns it back on for a run willing to pay for it, `--no-sweep` skips the opening
when you want a quick look rather than an answer, and raising any budget is worth doing against a measured
pace rather than against this paragraph's. `--no-pairs` and
`--pair-depth 1` are the switches if a run gets tight. Raise the budget rather than the step size: a wider
step reaches further and reads worse in the diff.

A catalogue the search has already played is not played again: the same neighbour comes up in more than one
round, and a paired move can land where a single one did. On the last 149-candidate pass 30 were replays and
one move set came up five times, so the engine sees about four fifths of what the search proposes. The report
says which: *"played 149 version(s) … the engine only had to play 119 of the 150 it was handed"*.

Two budget notes the last local pass earned. The climb converged after about ten candidates past the opening
sweep, so 149 of a possible 235 were played and 16 rounds read the same as 24 would have: on this catalogue
the opening sweep does nearly all the work, and the rounds are not the binding constraint. And the whole pass
now costs about **28 minutes** where it cost seventy.

The run writes `tune.json` (every candidate, its moves, its penalties and its metrics) and `content/`, the
changed spell files under the same tree they came from, so applying a proposal is a copy and reading one is
a diff. `--apply` does that copy. Nothing else is written to `data/`: the search works on a copy in
`<output>/work`.

Both are written **after the opening pass and after every round**, not only at the end. A pass that takes
hours is killed by a job timeout rather than ended by one, and a killed process gets no chance to write
anything, so tune 10 played 349 candidates over six hours and reported none of them. What is on disk at any
moment is the leader as it stands: a real catalogue the engine really played, and one worth applying.
`tune.json` carries `"complete": false` while the search is still running, which is how a reader tells the
answer to a finished search from the best a killed one had reached — the run summary says so too.

**Run more than one seed.** A hill climb keeps only what improves, so which knobs it happens to draw first
decides what it finds. On this catalogue, `--seed 1` at the full budget draws 32 candidates and improves
nothing, while `--seed 11` found a move in its first two: Ice Spear's Spell initiative from 2 to 1, and the
score from 33.7 to 29.6. Neither run was wrong; the space is mostly flat and the good moves are sparse. That
particular move is no longer available — no spell carries an initiative since ADR 0059 — and the shape of the
lesson is what survives, not the example.

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

The reading is coarse on purpose — no board, no defense, no cap at a target's health, no threat
behind a defensive effect (so a `DefenseBuff` is priced as `defense x amount x rounds`, a stand-in and not
what the scorer does with one, and a `DefenseDebuff` is the same stand-in the other way, blind in the same
way to the damage the shred lets through), no kill term, which is the largest weight in the game and a threshold so it
rewards a reliable hit over a bigger average one, and not the energy cost that `ActionScorer` prices when it
picks a package.

`throwing_star` used to be the case this reading missed, and is no longer: its entry said its Spell
initiative was worth more to the class than its damage, and none of that was in the number the report
printed. The bonus belongs to its package now (ADR 0059), so the ceiling is the whole truth about the spell
and the finding against it is a finding. The kill and threat terms are read off the agents'
own weights (`learning/weights/greedy.json`, which mirrors `ScoringWeights.Default`) rather than restated.

An attack is only compared with another attack, and a spell that deals no damage only with another that
deals none, for the reason `tierDamageSpread` skips one: a heal and an attack share no unit. Inside the
defensive half the comparison holds, which is why it is made rather than skipped — what the reading misses
about a defensive spell (the kill it denies, the threat it is priced against) it misses on **both** sides of
a defensive pair, so it very largely cancels, while against an attack it does not cancel at all. Skipping
them outright left a dead defensive spell invisible: `full_plate` was cast 0 times in 400 matches and `guard`
471, and nothing here told them apart.

What a spell does to **its own caster** (ADR 0031) is read too, and read with a sign: a heal on the caster
counts for the spell, a recoil counts against it, because on the caster a harmful kind is the price rather
than the point. It is counted **once per cast** — added after the target count, never multiplied by it — and
never multiplied by the critical chance. The same sign runs through every check: dominance treats the caster
half as its own axis, where an absent group is a zero rather than a gap (carrying no recoil is being better
on that axis, not failing to match it), and the ceiling of a knob that addresses a harmful caster effect is
its **minimum**, since more of a price is not a better spell.

**A harmful effect aimed at a friend is a price too**, and the targeting origin is the only thing that says
so: a stun or an initiative debuff is the point of a spell aimed at enemies and a cost in one aimed at allies
or at the caster's own creature. `noxious_cure` heals a team and slows the team it heals; read unsigned, that
slowing was an extra effect for free, so the spell read as *strictly better* than a plain heal of the same
size and `cast_value` priced the cost as three points of upside. The same three readings turn on it — the
value, the dominance comparison, and which end of a knob is the spell's best corner. Only the named origins
`Ally` and `Self` count: a document whose targeting cannot be read is not evidence that its effects land on a
friend, and reading it as one turns every hit in it into a price.

Everything is read **a round, not a cast**. Energy carries between rounds, so a spell costing three at an
income of two comes up twice in three rounds: 1.5 rounds a cast, floored at one because a creature acts once
a round however cheap the spell is. Without that, `enraged_charge` at 12.60 a cast reported
`protective_slam` as never a choice, when per round it is 8.40 against the slam's 7.33. The price is
divided by rather than filtered on, because a cheaper spell can still be outclassed through it — `pummel` at
one energy really does lose to `lightning_bolt` at two, 5.15 a round against 6.47, which is the case this
whole check was written for.

The targets it **does** read, from the spell rather than from a board: a cast is priced for every target it
is allowed, because the question is whether a spell can ever be a choice and a sweep is at its best when it
finds them all. It therefore overstates a `Multi` spell later in a match, when the creatures it would have
hit are dead — `meteor` lands 1.79 of its three on average over the benchmark seeds, where the single-target
spells land 0.88 to 1.01 of their one. Before the count was read at all, a sweep priced at a third of what it
plays, which is how the strongest spell in the catalogue passed every check. A rival is only the bar for a
spell that reaches **as many targets or more**: a single-target spell cannot match a sweep without ceasing to
be single-target, which is `noNewStrictDominance`'s axis and not this one.

It also reports a spell whose **permanent effect costs no energy**. A permanent effect never expires and
re-casting stacks it, so a free one has no brake but the round cap; every reading here prices a single cast,
and no horizon finds a stack, so the price is what decides it and not the magnitude. `full_plate` is authored
that way today because nothing implements `SpellType.Passive` yet — the finding is there so a tuning pass
cannot put a price back to zero and ship an unbounded spell with every check green.

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
