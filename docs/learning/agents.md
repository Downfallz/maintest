# Baseline agents

The agents the engine ships without a model (learning phase L5), the scoring they share, and how to tune it.

| Agent | Spec | What it does |
| --- | --- | --- |
| Random | `random` | Picks uniformly among the options. The floor every other agent is measured against; deterministic for a seed. |
| Greedy | `greedy` | One-step lookahead with the built-in weights below. The deterministic baseline of the benchmark digest. |
| Heuristic | `heuristic:<weights file>` | The same lookahead with the weights read from a JSON file (`learning/weights/greedy.json` is the built-in set), so the weights can be searched (L6) without a model runtime. |
| Lookahead | `lookahead[:<weights file>]` | Plays the round out on a hypothetical board before each combat move (ADR 0047) and keeps the move whose round ends best; the built-in weights, or a file's. Deterministic. Evolution and speed are Greedy's. See [the round played out](#the-round-played-out). |
| Minimax | `minimax[:<weights file>]` | The lookahead with every enemy slot still ahead played as the reply that costs the actor most, rather than as the guessed one: the floor of a move's worth. Deterministic. See [the worst reply](#the-worst-reply). |
| Policy | `policy:<policy.json>` | A trained policy (`docs/learning/training.md`): scores the candidate actions with one weight row per action key and takes the best. Refused when its feature schema is not the current one. |
| Exploring | `explore:<rate>[:<agent>]` | Another agent, except that the given share of decisions is taken uniformly at random (ADR 0014). Bare, it wraps Greedy; a second colon names the agent it deviates from instead — `explore:0.2:heuristic:<weights>`, `explore:0.2:policy:<file>`, or a bare path as the shorthand for a weights file. For recording datasets a value regression can learn from, never for a baseline: it draws from a random source, so it is deterministic for a seed but not for the digest. |

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
affordable, no legal target) scores nothing — which still loses to anything that does something (ADR 0040).

A defensive term is priced by the damage it prevents, which needs a reading of the **threat** on a creature:
what the living, unstunned enemies could deal it in one round with the damaging spells they know and can
afford, after its defense (ADR 0022). It is read from the spells' own numbers, and only for an outcome that
needs it, so an attack costs what it always did.

The score of one resolution, with the weights `w`:

| Term | Counts | Sign |
| --- | --- | --- |
| `w.damage` x effective damage | damage capped at the target's health, per target | for an enemy, against an ally |
| `w.kill` per kill | a target whose health the damage reaches | for an enemy, against an ally |
| `w.pressure` x share of the target's health | the effective damage over the health the target had, one for a kill: how much closer the hit brings that creature to a kill (ADR 0050) | for an enemy, against an ally |
| `w.heal` x effective healing | healing capped at what the target was missing | for an ally, against an enemy |
| `w.kill` per denied kill | a heal or a defense buff that takes its target from dying to this round's threat to surviving it (ADR 0022) | for an ally, against an enemy |
| `w.stun` x rounds stunned | a Stun on a target still alive after the damage | for an enemy, against an ally |
| `w.bleed` x expected bleed damage | amount per round x rounds (a permanent condition counts three), capped at the health left after the hit | for an enemy, against an ally |
| `w.heal` x expected regeneration | amount per round x rounds, capped at what the target is still missing after the hit | for an ally, against an enemy |
| `w.defense` x damage prevented | a DefenseBuff, and only a DefenseBuff: amount x rounds x the hits the target is expected to face, its attackers spread over its living allies (ADR 0022) | for an ally, against an enemy |
| `w.defense` x amount x rounds | a DefenseDebuff (a permanent condition counts three). A stand-in, not the reading above: it does not know what the debuff lets through, and it never reaches the threat term, so the bot cannot see that lowering a defense raises what the next hit takes (ADR 0035) | on an enemy counts for, on an ally against |
| `w.energy` x energy taken | an EnergyDrain, capped at what the target holds, which is all `Creature.LoseEnergy` takes (ADR 0035) | on an enemy counts for, on an ally against |
| `w.initiative` x amount x rounds | an InitiativeDebuff (a permanent condition counts three) | a debuff on an enemy counts for, on an ally against |
| `w.initiative` x amount x rounds | an InitiativeBuff (a permanent condition counts three). The same price as the debuff above: one price for one point whether it is given or taken (ADR 0036) | a buff on an ally counts for, on an enemy against |
| `w.energy` x energy kept | the actor's energy after the cost | always |

Decisions:

- **Intent**: for each castable spell, the best target set by expected score; the spell with the best
  score. Ties go to the first spell in ordinal id order. That tie-break is on an exact `double` comparison,
  and it is load-bearing more often than it looks: the weight ADR 0040 removed used to move one decision at
  3, 6 and 9 and at no other value tried, because its share term landed on an exact integer there and so on
  another candidate's score. Two weight values that differ can play identically while a third between them
  does not.
- **Targets**: the best target set of the declared spell, on the board at reveal time **minus the creatures
  the actions already revealed will kill first**; no target when the spell is no longer castable. Targets are
  bound in `RevealAndTarget`, a whole sub-phase before anything resolves, so the board a creature binds on is
  the one from before combat. `RevealedActions` carries the slots ahead of this one, in timeline order and
  with their targets bound, for both teams — binding order is timeline order — so the agent replays them
  against a board it carries forward and writes off who does not survive — stopping at the first action it
  would have to guess about, so the answer is sound but incomplete. That was 45.6 % of all fizzles
  (ADR 0039).
- **Speed**: Quick when some castable spell kills an enemy without a critical, Standard otherwise.
- **Evolution**: for each unlockable spell, its value as if the creature knew it and could afford it (the
  best target set on the current board), plus `w.initiative` x the spell's Spell initiative, the base
  initiative the unlock buys for the rest of the match (ADR 0017, priced by ADR 0018), minus `w.energy` x
  the part of the cost the actor cannot cover (ADR 0026); unlock the highest, pass only when nothing can be
  unlocked. Only that part is charged here: the value is read on an energy raised to at least the spell's
  cost, so a creature that could not afford it keeps nothing either way and the difference cancels, while
  above the cost the energy the actor keeps already prices every point.

Both agents are deterministic: the same board gives the same decision, so a Greedy versus Greedy evaluation
on the benchmark seeds replays exactly. That is what makes the benchmark digest an engine-change detector.

## The round played out

The lookahead agent prices a combat move by the board the round leaves rather than by what the move does on
the board it is cast on. ADR 0047 gave the Domain `Advance`, which restores creatures from snapshots and runs
the match's own rules on them, so the agent can put a move on the board and keep going:

- **Intent**: for each castable spell, the round is played from its first activation slot. At the actor's
  slot the candidate spell is cast on the best target set the one-step scorer finds *on the board at that
  moment*. Every other slot plays a spell and the scorer's best targets for it on the board at its slot. The
  spell is the declared one for an ally that has declared, and for everyone else the spell the scorer would
  declare **on the board before combat** — the information the match gives, since every intent is declared
  before anything resolves. Guessed at the slot instead, the enemy becomes clairvoyant and punishes every
  aggressive move, which measured as the lookahead losing three matches in four. A dead or stunned creature
  plays nothing, as in the match. The round's worth is the **sum of what the scorer says of every action it
  holds**, the actor's team's for and the other's against, each scored on the board it lands on; a round
  that ends the match outranks any round that does not, won above every score and lost below every score,
  whatever the weights say, because the two are never added. The spell whose round is worth most wins.
- **Targets**: the same, from the actor's slot on, starting from the board the **revealed actions** leave.
  They are public and bound in timeline order, so `Advance` replays them exactly where the one-step agent
  could only carry health forward and stop at the first stun, heal or buff (ADR 0039). No target when the
  spell is no longer castable.
- **Ties** go to the candidate with the best one-step score, then to the first in order. The second key
  decides whenever the round cannot: when the guessed slots have the actor dead or stunned before its own,
  every candidate leaves the same round, and the choice still matters in every world where the guess is
  wrong. Without it the tie went to the first spell in id order, and the weakest spell in the catalogue was
  cast three times as often as Greedy casts it.
- **The actor's critical roll** is weighted the way the scorer weights it: the round is played once on a
  forced critical and once on a miss, and the two are mixed by the actor's chance for that spell. Every
  other creature's roll is a miss, which keeps the cost at two rounds per candidate. On a miss alone, a spell
  that crits three casts in four was priced at half its worth and never cast.
- **Speed and evolution** are the heuristic agent's: neither is a combat move, and the round they plan has no
  timeline yet to play out.

Summing the scorer's own scores keeps its calibration: a move with no consequence for the rest of the round
is worth exactly what Greedy says it is, and only the interactions are new. The first version valued the
board the round leaves instead, health, energy and conditions priced by the same weights, and measured eight
points of win rate below the sum against Greedy: the weights were swept for the one-step reading, not for a
reading of the board.

What it sees that the one-step reading cannot: a stun that takes away an enemy's kill, a kill that lands
before its target acts, a target another action will have killed first, a defense buff that turns a lethal
round into a survivable one. What it does not see is hidden: an enemy's intent before it is revealed, which
the rollout stands in for with the scorer's pick, and every roll but the actor's own.

The cost is the branching: one decision plays the rest of the round twice per candidate, and each slot it
plays asks the scorer for a best target set, so a decision costs on the order of the timeline length times
what a one-step decision costs. The journal entry that introduced it carries the measurement, and the
measurement is the thing to read before playing it: on the catalogue of that day it does **not** beat Greedy.

## The worst reply

The minimax agent is the lookahead agent with one change: an enemy slot still ahead is not played as the
guessed spell but as the **reply that costs the actor most** among the spells that enemy can cast. One enemy
at a time in timeline order, each over its castable spells with the earlier enemies already settled and the
later ones still at their guess: a joint worst case over every enemy would cost the product of their spell
counts where this costs the sum, and the round is short enough that the two rarely disagree on a reply. The
value of a candidate is therefore a **floor**: what the round is worth against an opponent who sees the
actor's move and answers it. No opponent in this game sees it, since intents are simultaneous, so the floor
is not the expectation, and the question the journal answers is whether a floor plays better than a guess.
Allies and the actor's own targets are read exactly as the lookahead reads them. `Replies` on the agent hands
out, for a board, an actor and a candidate, the spell each other creature was taken to play, which is how a
test or a viewer sees the difference between the two.

## Built-in weights

Every weight is expressed in the same unit: **one point of effective damage**. `damage` is 1.0 by
definition, and each other weight says how many points of damage that thing is worth to the bot. So a kill at
5.0 means "worth five damage on top of the damage that killed", and the bot takes a kill over five points of
damage spread elsewhere.

| Weight | Value | In plain words |
| --- | --- | --- |
| damage | 1.0 | The unit. One point per point of damage that actually lands (damage past a target's health is not counted). |
| kill | 5.0 | Finishing a creature is worth five damage on top of the hit. It buys the bot the enemy's whole future turn, so it is the strongest pull in the table. |
| heal | 0.8 | Healing an ally is worth a little less than hurting an enemy: it only counts what the target was missing, and it does not shorten the match. A heal that saves a life is worth `kill` on top, because denying a kill and scoring one are the same thing seen from two sides. |
| stun | 3.0 | Taking a round away from a creature is worth three damage. Between a kill (all its rounds) and a plain hit (none). |
| bleed | 0.8 | Damage over time is discounted against damage now: the target may die first, and the bot only counts the health it could still reach. |
| defense | 0.65 | Two thirds of a point per point of damage the buff actually takes off the hits the creature is expected to face. Defense subtracts from every incoming hit, so the same buff is worth more to the last creature standing than to a full team. **Not read against `damage` point for point**, whatever the shared unit suggests: an attack is paid once, this is paid for every round the buff holds, so it compounds where `damage` does not. That is why it sits below one. Measured rather than felt (ADR 0028): the play moves in steps as this price rises, 0.65 sits in the middle of the step that puts matches inside the 8..16 round band, and at 1.5 every match runs out of rounds and no attack is ever cast. |
| energy | 0.3 | Just under a third of a damage per point of energy — kept for the next round, handed to an ally, regenerated over rounds, or taken off an enemy. It was hand-set at 0.2 in phase L5, when the only thing it priced was energy *kept* and it was meant as no more than a tie-breaker towards the cheaper spell. ADR 0020, 0026 and 0035 gave it three more jobs without ever re-measuring it, and ADR 0037 swept it: at 0.0 the first mover wins 0.720 of the mirror, so the term was never a tie-breaker at all. 0.3 is the middle of the step 0.2..0.4, whose right edge breaks hard (0.5 reads 108.33 on the objective with the exploiter at 0.790). Read what it buys precisely: **the mirror's first-mover share, not a stronger agent** — `player1WinShare` goes 0.575 to 0.510, and a 0.3 agent against a 0.2 one is a dead heat. |
| pressure | 0.0 | The share of a creature's health a hit takes, so the same three points are worth more on a creature at six than on one at twenty and a kill takes the whole share. `damage` cannot tell those apart and `kill` pays only once the last point lands; this is the term between them, and the first added because a search of the other eight had no term for it (journal, 2026-09-16). Zero until measured: ADR 0050 has the sweep. |
| initiative | 2.1 | Two and a bit per point of initiative, whether an unlock buys it or a debuff takes it off an enemy — one price for one point, so the bot cannot value giving and taking differently. ADR 0018 set it to 0.5 on the reasoning that initiative is indirect the way defense is, and said in the same breath that it was a guess. ADR 0032 measured it instead, by sweeping it alone on fixed content, and the reasoning was backwards: a point of initiative is bought once and kept for the match, in a game the first mover was winning 64 % of. At 2.1 that reading is 0.500. The sweep is in that ADR; 2.1 sits in the middle of its step rather than on an edge. Since ADR 0026 a debuff also multiplies by the rounds it lasts while the unlock's permanent gain does not, so a two-round debuff outvalues a permanent gain of the same size; the tension is recorded in that ADR and is now four times larger. |

To feel out what one of them does, the content studio's run panel can play a heuristic agent from a box per
weight instead of a file: it writes what you set as `weights.json` next to the run, so the result keeps the weights it
was played with, and two such runs compare side by side (`studio/README.md`). That is a way to look, not a way
to tune — tuning is `search-weights` below.

### Where these numbers come from

They were **hand-set as a starting point**, in the commit that introduced the agents (phase L5), from the
readings above: pick damage as the unit, then say what a kill, a stun and a wasted turn are worth in damage.
They are **not** the output of a search. Three were then measured one at a time, by sweeping that one weight
on fixed content and playing every value, and moved: `defense` (ADR 0028), `initiative` (ADR 0032) and
`energy` (ADR 0037). `scripts/sweep-weight.py` is that method written down. The five that were left —
`kill`, `stun`, `heal`, `bleed` and the one ADR 0040 removed — have since been swept the same way on content `91da955c`, and
**none of them moved**: each hand-set value sits inside the step the sweep found, so the starting guesses
were good and are now measured rather than assumed. `damage` is not swept, because it is the unit: moving it
alone is the same experiment as scaling the other eight the other way. The one thing those five sweeps did
turn up is that one of the five priced nothing at all: ADR 0039 gave it a decision to reach and it still
priced nothing, so ADR 0040 removed it and the table was eight until ADR 0050 added `pressure`, the first
weight added because a search of the others had nowhere left to go. `ScoringWeights.Default` is the single
source; `learning/weights/greedy.json` holds the same nine numbers so `heuristic:<file>` and `greedy` start
from the same place, and a test on each side of the repository pins the two together.

To move them, do not edit them by feel: run `search-weights` (`docs/learning/training.md`), which plays each
candidate set against a fixed opponent on the benchmark seeds and keeps what wins, and leave the result next
to `greedy.json` under its own name. `search-2.json` was the first of those: a searched set that beats `Greedy`
on seeds it never saw, committed to be played and compared, not to be the baseline (the 2026-09-12 journal
entry says what it buys and what it costs). **`search-3.json` is the current one**, searched on content
`938bef5e`, and it is what the balance objective's `exploit` evaluation plays. That evaluation names a file,
so it is the one reading that goes stale on its own: a tuning pass changes what there is to exploit, and an
agent searched against a catalogue that no longer exists understates the gap rather than overstating it.
`search-2` had gone four content changes without a refresh and read 0.182 where `search-3` reads 0.745.
Refresh it from the newest search rather than keeping the old file. `mixture-mean.json` and `mixture-worst.json`
are the first sets searched against three opponents at once (`greedy`, `search-4` and `random`; journal,
2026-09-16), and either beats `search-4` head to head on seeds the search never saw while beating Greedy and
Random. `pressure-floor.json` is the first set searched with the ninth weight free, from `mixture-mean`
against four opponents under the floor (journal, 2026-09-17): on 200 seeds nothing had played it beats Greedy
and Random in every match, `search-4` 0.96 and `mixture-mean` 0.93, by banking energy for Crushing Stomp's
two-round stun;
its mirror runs twice as long as Greedy's and the first mover wins two thirds of it, so it is a rung of the
ladder and a reading for the tuner, not a baseline. Changing `greedy.json` itself changes nothing for `greedy`, which reads
the built-in values; only `heuristic:learning/weights/greedy.json` sees it. Changing `ScoringWeights.Default`
does change the benchmark baseline, but the digest records the outcome of each seed and not the weights, so it
only moves when the new values actually change a decision: scaling all nine by the same positive factor
leaves every ranking, and the digest, untouched. A change that does move an outcome fails the benchmark check
until `benchmark --write` regenerates the digest.

A heuristic agent is stamped as `Heuristic:<path>@<fingerprint>`, the fingerprint being eight hex digits of the
weights the file held when the run started, so two runs on different weights at the same path never share a
stamp.

A weights file lists any subset of these names in camelCase (`{ "kill": 8, "stun": 4 }`); a missing name
keeps the built-in value, an unknown one is an error, every value must be a finite number.
