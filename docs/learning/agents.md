# Baseline agents

The agents the engine ships without a model (learning phase L5), the scoring they share, and how to tune it.

| Agent | Spec | What it does |
| --- | --- | --- |
| Random | `random` | Picks uniformly among the options. The floor every other agent is measured against; deterministic for a seed. |
| Greedy | `greedy` | One-step lookahead with the built-in weights below. The deterministic baseline of the benchmark digest. |
| Heuristic | `heuristic:<weights file>` | The same lookahead with the weights read from a JSON file (`learning/weights/greedy.json` is the built-in set), so the weights can be searched (L6) without a model runtime. |
| Lookahead | `lookahead[:<rounds>x<rollouts>][:<weights file>\|:<agent>]` | Plays the round out on a hypothetical board before each combat move (ADR 0047) and keeps the move whose round ends best; the built-in weights, or a file's. With `<rounds>x<rollouts>` in front (`lookahead:4x4:<weights file>`) it also plays that many rounds after the move out, on that many rollouts, the way it reads a purchase; see [the rounds after a move](#the-rounds-after-a-move). Deterministic for a seed. Speed and the seats it has to guess are Greedy's, or the agent named after the kind — `lookahead:policy:<file>` searches over a trained policy (ADR 0055). It orders its own ties by playing each seating out, and prices a purchase by playing the rounds after it out (ADR 0094). See [the round played out](#the-round-played-out). |
| Minimax | `minimax[:<rounds>x<rollouts>][:<weights file>\|:<agent>]` | The lookahead with every enemy slot still ahead played as the reply that costs the actor most, rather than as the guessed one: the floor of a move's worth. It orders its ties as the lookahead does, and reads the rounds after a move the same way when the spec asks for them. Deterministic. See [the worst reply](#the-worst-reply). |
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
needs it, so an attack costs what it always did. Whether the round is lethal, which decides a denied kill, is
read from the enemies still to act after the cast on the timeline when the agent has one: an enemy that has
already acted, or acts before the cast, cannot be stopped by it this round (ADR 0085).

The score of one resolution, with the weights `w`:

| Term | Counts | Sign |
| --- | --- | --- |
| `w.damage` x effective damage | damage capped at the target's health, per target | for an enemy, against an ally |
| `w.kill` per kill | a target whose health the damage reaches | for an enemy, against an ally |
| `w.pressure` x share of the target's health | the effective damage over the health the target had, one for a kill: how much closer the hit brings that creature to a kill (ADR 0050) | for an enemy, against an ally |
| `w.heal` x effective healing | healing capped at what the target was missing | for an ally, against an enemy |
| `w.kill` per denied kill | a heal or a defense buff that takes its target from dying to this round's threat to surviving it (ADR 0022), the threat of the enemies still to act after the cast (ADR 0085) | for an ally, against an enemy |
| `w.stun` x rounds stunned | a Stun on a target still alive after the damage | for an enemy, against an ally |
| `w.bleed` x expected bleed damage | amount per round x rounds (a permanent condition counts three), capped at the health left after the hit | for an enemy, against an ally |
| `w.damage` x bleed the target's defense would have blocked | the same bleed points, up to the target's total defense x the bleed's rounds: what a hit a round would lose to that defense and the bleed does not (ADR 0073) | for an enemy, against an ally |
| `w.heal` x expected regeneration | amount per round x rounds, capped at what the target is still missing after the hit | for an ally, against an enemy |
| `w.defense` x damage prevented | a DefenseBuff, and only a DefenseBuff: amount x rounds x the hits the target is expected to face, its attackers spread over its living allies (ADR 0022) | for an ally, against an enemy |
| `w.defense` x damage let through x rounds / team | a DefenseDebuff (a permanent condition counts three): the threat on the target with the defense it takes off gone, less the threat on it now, shared across the target's living team the way a buff's is. It takes off at most the defense the target holds, so on a creature with no defense it is worth nothing (ADR 0098) | on an enemy counts for, on an ally against |
| `w.energy` x energy taken | an EnergyDrain, capped at what the target holds, which is all `Creature.LoseEnergy` takes (ADR 0035) | on an enemy counts for, on an ally against |
| `w.stun` x 1 | an EnergyDrain that leaves an enemy still to act unable to pay for any of its spells that cost energy, which takes its action this round as a stun would (ADR 0093) | on an enemy only |
| `w.initiative` x order changed x rounds | an InitiativeDebuff (a permanent condition counts three). The order it changes is how many living creatures of the other side the target falls behind, a tie counting half since a d20 decides it, read down to zero initiative (ADR 0088) | a debuff on an enemy counts for, on an ally against |
| `w.initiative` x order changed x rounds | an InitiativeBuff (a permanent condition counts three): how many living creatures of the other side the target gets ahead of, a tie counting half (ADR 0088). The same price as the debuff above: one price for one place in the order whether it is given or taken (ADR 0036) | a buff on an ally counts for, on an enemy against |
| `w.energy` x energy kept | the actor's energy after the cost, plus what the action gives it back, up to its reserve: its dearest known spell's cost, and for each of the two rounds after, what a round's energy does not cover (ADR 0103); the round before a purchase, the reserve it would have with the package it would buy, when larger (ADR 0104); a point past the reserve pays for no cast | always |
| the unlocked spell's terms | next purse: the best spell the actor's purse next round pays for (its energy, less the cost, plus what the action gives itself, plus a round's gain) less the best a round's gain alone pays for, when positive; each spell at its best target set on this board, read without these two terms and without the energy it leaves (ADR 0096) | always |
| the unlocked spell's terms | energy given to another creature: the best spell it then pays for next round that it could not without the gift, the creature taken to spend this round on the best spell it can pay for now (ADR 0096) | on an ally for, on an enemy against |

Decisions:

- **Intent**: for each castable spell, the best target set by expected score; the spell with the best
  score. Ties go to the first spell in ordinal id order. That tie-break is on an exact `double` comparison,
  and it is load-bearing more often than it looks: the weight ADR 0040 removed used to move one decision at
  3, 6 and 9 and at no other value tried, because its share term landed on an exact integer there and so on
  another candidate's score. Two weight values that differ can play identically while a third between them
  does not.
- **Targets**: the best target set of the declared spell, on the board as it stands; no target when the
  spell is no longer castable. An action resolves when its targets are confirmed (ADR 0083), so every slot
  before this one has already resolved and the board carries it: nothing is written off. Until then targets
  were bound a whole sub-phase before anything resolved, and the agent replayed the revealed actions to write
  off who would not survive them; that was 45.6 % of all fizzles (ADR 0039), and the replay is gone with the
  two passes.
- **Speed**: Quick when some castable spell kills an enemy without a critical. Otherwise Standard only when the
  critical it keeps raises the best expected score among the castable spells, Quick when it does not: a creature
  that cannot crit, or whose weights do not price what its critical adds, gains nothing by waiting (ADR 0084).
- **Tie order**: the order the roll-off left (ADR 0063). The scorer reads one action at a time and has no view
  of which of two of its own creatures should act first, so it does not pretend to; the lookahead plays each
  seating out ([the round played out](#the-round-played-out)), the random agent shuffles each tie and the
  exploring one does at its rate.
- **Evolution**: for each unlockable spell, what it adds over the best spell of its kind (offensive,
  defensive, passive) the creature already knows or the player's picks of this round already teach (ADR 0108),
  both read alike, as if the creature knew them and could
  afford them on the current board, and nothing when it adds nothing; its own energy term rides along when it
  adds something or moves energy (ADR 0102); plus the package's passive, priced by the casts
  it changes over three rounds (ADR 0101); plus `w.initiative` x the order the package's initiative bonus buys
  for the rest of the match -- the living enemies it takes the buyer past, a tie counting half (ADR 0017,
  priced by ADR 0018, read as order by ADR 0088) -- minus `w.energy` x
  the part of the cost the actor cannot cover (ADR 0026); unlock the highest, pass only when nothing can be
  unlocked. Only that part is charged here: the value is read on an energy raised to at least the spell's
  cost, so a creature that could not afford it keeps nothing either way and the difference cancels, while
  above the cost the energy the actor keeps already prices every point. The value includes the unlock terms
  (ADR 0096), so a dear package is read with what the buyer's purse reaches next round: that is what moves
  Greedy off cheap packages it never saves for.

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
- **Targets**: the same, from the actor's slot on, starting from the board as it stands: the earlier slots
  have resolved already (ADR 0083). The enemies still ahead are guessed on that board too, since the one they
  declared on is no longer in the state. No target when the spell is no longer castable. A round played out
  stops at the slot that wipes a team, as the match does.
- **Ties** go to the candidate with the best one-step score, then to the first in order. The second key
  decides whenever the round cannot: when the guessed slots have the actor dead or stunned before its own,
  every candidate leaves the same round, and the choice still matters in every world where the guess is
  wrong. Without it the tie went to the first spell in id order, and the weakest spell in the catalogue was
  cast three times as often as Greedy casts it. The same holds when the guess leaves the actor too drained to
  pay for any spell that costs energy: only a free spell still resolves, and it would win by what it gives in
  the one world the guess describes. When every paid candidate is stopped at the actor's slot -- dead,
  stunned or short of energy -- the one-step reading decides outright. Without it the lookahead cast Wait 248
  times in 400 matches against Greedy, which casts it once. Minimax keeps its round there: its reply is the
  worst the enemy can do, not a guess, and against a drain it cannot avoid the free spell is the one that
  still resolves.
- **The actor's critical roll** is weighted the way the scorer weights it: the round is played once on a
  forced critical and once on a miss, and the two are mixed by the actor's chance for that spell. Every
  other creature's roll is a miss, which keeps the cost at two rounds per candidate. On a miss alone, a spell
  that crits three casts in four was priced at half its worth and never cast. The chance is read at the
  speed the actor chose, which the timeline carries, so a Quick actor is priced at none.
- **Every reading uses the speed each creature chose**, the enemy's included: the timeline carries them all.
  The one-step score that breaks a tie, the targets each creature is taken to pick at its slot, and the spell
  an enemy is guessed to declare are all read at that speed, so an enemy that chose Quick is guessed to cast
  what it can roll rather than a critical it cannot.
- **Speed** is the heuristic agent's: the round it plans has no timeline yet to play out.
- **Evolution** is its own once it has dice (ADR 0094), which every agent the factory seats has: what a package
  is worth shows only over the rounds after it, so each purchase on offer is bought on a hypothetical board
  and those rounds are played out whole, every sub-phase of them, by `RoundRollout`. The side's earlier picks
  of the round, its remaining ones and the enemy's face-down ones (ADR 0089) are what the agent it is built
  on would buy, and every decision of the rounds after is that agent's, in both seats. The dice are rolled,
  not forced plain, derived from one seed the agent draws from the match's agent dice when it is made and
  from the board the purchase is made on, so a rebuilt table buys what its first host would have; every
  candidate is read on the same rolls, and an inner agent that decides at random keeps its own source; the
  candidate whose rollouts end best wins, by the share of matches they win, then by the scorer's sum over
  their actions. Creatures alike but for their id are one candidate. `PurchaseReading` sets how many rounds a
  rollout plays and how many rollouts a candidate is averaged over. A rollout plays the match the match plays:
  rolled from the first Speed sub-phase on dice that roll what the match's did, it ends where the match ends
  with the same sum (`RoundRolloutTests`). The minimax agent reads a purchase the same way.
- **Tie order** is its own, because it comes once the timeline is built; the agent it is built on is not
  asked (ADR 0067). Every seating of its tied creatures in the places its side holds is played out from the first slot,
  with no intent declared on either side: each ally plays what the agent it is built on would declare, each
  on its own, each enemy what the scorer would, the enemy's own tie as rolled, and every roll is a miss. The
  seating whose round is worth most wins, the order as rolled on a tie. That is team size factorial seatings,
  six on the default rules; past 24 it keeps the roll. The minimax agent reads it the same way: a worst reply
  is defined against one actor's move, and here every ally moves. On the benchmark seeds it almost never
  moves a creature (journal, 2026-09-23): most ties fall in the first rounds, before anyone can kill or stun
  first, and the few later ones rarely hinge on who acts first.

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

### The rounds after a move

A combat move can be read past its round, the way a purchase is (ADR 0094): `lookahead:4x4` plays, for each
candidate, the rest of the round as above and then the next four rounds out on four rollouts, every
sub-phase of them, every decision in both seats the agent the lookahead is built on, on dice derived from
the agent's seed and the decision (the round, the actor and the slot), so that every candidate of one
decision is read on the same rolls and only the candidate is left between them. What is read off them is
the match they end and nothing else: the candidate's outcome becomes the share of rollouts won less the
share lost, with the actor's critical mixed in as before, and its score stays the round's own. A candidate
whose rounds after end in a win outranks every other, one that ends in a loss ranks below, and between
candidates that end nothing the one-round reading decides, as it always did. The rollouts' summed scores
are deliberately not added: a sum of one-step scores over rounds a bot plays out cannot tell damage now from
damage later, and measured on the benchmark seeds it made Focus the most cast spell in the catalogue and
lost three matches in four to the one-round reading on the same weights, with the stock terms (energy,
defense, initiative) in the sum or without them.

`<rounds>x<rollouts>` goes right after the kind and in front of whatever named the weights or the inner
agent before: `lookahead:4x4`, `lookahead:4x4:learning/weights/lookahead/lookahead-34.json`,
`lookahead:2x2:policy:<file>`, `minimax:2x2:greedy`. Both numbers are at least one, and a spec without them
reads the round alone, so every spec written before this reads the same. The depth is part of the stamp
(`Lookahead:4x4:<file>@<fingerprint>`). It needs dice: an agent built without a random source reads the round
alone whatever the spec says, as it buys without rollouts.

What it sees that one round cannot: a kill that leaves the wrong enemy standing, a lethal two rounds out, a
ward that keeps the last creature alive through the next round. What it costs is the rollouts: each candidate of
a decision adds `2 x rollouts x rounds` rounds of play on top of the two round play-outs it already made (one
when the actor cannot crit), targets as well as intents, and the tie orders. Measured on the same weights
(journal, 2026-10-07), it is no stronger than the one-round reading: even at `2x2` (20 matches in 40) and
weaker at `4x4` (16 in 40), at three times the clock. Four rollouts give an outcome in quarters, and the
outcome is compared before the score, so once rollouts start ending a match one rollout's dice outrank
anything the round itself shows; before that, no rollout ends and the reading is the one-round one. It
stays opt-in, seated by its spec from a shell (`table --p2 lookahead:4x4:<weights file>`): not put forward
by `learning/seatable.json`, which offers what measured as worth playing against, and not in the tuner's
panel, which it would slow by the same factor for nothing measured.

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
| initiative | 2.1 | Two and a bit per place in the turn order, whether an unlock buys it or a debuff takes it off an enemy — one price for one place, so the bot cannot value giving and taking differently. Since ADR 0088 the unit is a place, the enemies a change moves a creature past, not a point: a point that passes nobody is worth nothing, which is what took Death Squad from 471 casts to 15 on the exploring run. ADR 0018 set it to 0.5 on the reasoning that initiative is indirect the way defense is, and said in the same breath that it was a guess. ADR 0032 measured it instead, by sweeping it alone on fixed content, and the reasoning was backwards: a point of initiative is bought once and kept for the match, in a game the first mover was winning 64 % of. At 2.1 that reading is 0.500. The sweep is in that ADR; 2.1 sits in the middle of its step rather than on an edge. Since ADR 0026 a debuff also multiplies by the rounds it lasts while the unlock's permanent gain does not, so a two-round debuff outvalues a permanent gain of the same size; the tension is recorded in that ADR and is now four times larger. |

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
entry says what it buys and what it costs). **The balance objective's `exploit` evaluation plays a panel of
them and reads its best exploiter** ([ADR 0052](../adr/0052-read-the-exploit-term-as-the-best-of-a-panel.md)),
and scores **how fast** that one wins rather than whether it does, ties going to the fastest
([ADR 0053](../adr/0053-score-the-exploit-term-on-the-clock-not-on-the-win-rate.md)):
`search-23`, `search-21`, `kill-first`, `mixture-mean` and `search-19` today, since 2026-10-05, when `search-31`
and `pressure-floor` left it for no longer beating Greedy on content `e6f72578`; `search-19` no longer does either,
and stays as the best exploiter of the catalogue before, the one a candidate moving the content back would wake.
`stun-first` and `search-4` were in it before. It named one file until 2026-09-17,
`pressure-floor` then, `search-4` before it and `search-3` before that, and the reason it no longer does is
measured: one agent reads what that agent punishes, so the same one-spell move was worth 0.013 to `search-4`
and 0.235 to `stun-first`, and on the moved catalogue `search-4` became the best exploiter of the two
(journal, 2026-09-17). A set still goes stale when the content moves, which is why `search-2` read 0.182
where `search-3` read 0.745 on the catalogue of the day; the answer is now to add the newest search to the
panel rather than to replace what is there. `mixture-mean.json` and `mixture-worst.json`
are the first sets searched against three opponents at once (`greedy`, `search-4` and `random`; journal,
2026-09-16), and either beats `search-4` head to head on seeds the search never saw while beating Greedy and
Random. `pressure-floor.json` is the first set searched with the ninth weight free, from `mixture-mean`
against four opponents under the floor (journal, 2026-09-17): on 200 seeds nothing had played it beats Greedy
and Random in every match, `search-4` 0.96 and `mixture-mean` 0.93, by banking energy for Crushing Stomp's
two-round stun;
its mirror runs twice as long as Greedy's and the first mover wins two thirds of it, so it is a rung of the
ladder and the tuner's exploit reading, not a baseline. `stun-first.json` is the rung above it, searched from it
with it in the panel (journal, 2026-09-17): the first set in which `stun` is the largest weight, above
`kill`, and on 200 seeds nothing had played it beats `pressure-floor` 0.80, `search-4` 0.97 and
`mixture-mean` 0.93 while taking every match from Greedy and Random. Its own mirror is worse still, the
first mover taking 0.925 of it, so the ladder's top two rungs are agents to play and not baselines to
balance against. `kill-first.json` is the only set here searched against a catalogue other than the
committed one — the weakened Crushing Stomp of the 2026-09-17 saturation entry — and it is on the ladder for
what that measured rather than for a rung: `stun` 0.70 where `stun-first` carries 10.21 and `kill` 18.01
above everything, a different route to the same result. On 200 seeds nothing had played it takes every match
from Greedy on **both** catalogues, where `stun-first` takes 0.72 of them on the moved one, which is why a
content move that blinds one set is not a content move that makes the game less exploitable. `search-19.json` is the first rung on the 30-health content (ADR 0068), searched from `stun-first` with
it in the panel beside Greedy and `pressure-floor` (journal, 2026-09-23): on 200 seeds nothing had played it
takes every match from all three, and from `search-4` and Random, while the lookahead's built-in reading
holds it to 0.78. Its own mirror, like `stun-first`'s and `pressure-floor`'s, never ends before the round
cap: those three are agents to play against, and their mirrors say something about how they read
initiative rather than about the game (journal, 2026-09-23). `lookahead-20.json` is a set for the lookahead
rather than for the one-step reading: searched as `lookahead:<weights>` from the built-in weights against
Greedy, `stun-first` and `pressure-floor` (journal, 2026-09-24), and replayed on 200 seeds nothing had played
with the stun immunity and the bleed price in, it beats the built-in lookahead against Greedy (0.98 to 0.54),
`stun-first` (0.70 to 0.56), `pressure-floor` (1.00 to 0.46) and `search-4`, and cannot be told from it against
`search-19` (0.37 to 0.34). `search-21.json` is the heuristic's rung under both rules, searched from
`search-19` with it in the panel beside Greedy and `stun-first` (journal, 2026-09-24): on 200 seeds nothing
had played it takes every match from Greedy, `stun-first` and `search-19`, and 0.995 from the built-in
lookahead, and is even with `lookahead-20` (0.495). It is the first set with a negative `stun` weight: it does
not stun. Its own mirror reaches the round cap in 0.67 of its matches, where `search-19`'s reached it in all
but a few. `search-23.json` is the first rung under the defense buff ceiling (ADR 0076), searched from `search-21`
against Greedy, `search-21` and the lookahead with `lookahead-20` (journal, 2026-09-24): on 200 seeds nothing
had played it takes every match from Greedy and `search-21` and beats `lookahead-20` 0.685, and it gives
`stun-first`, which was not in its panel, back to 0.560. `lookahead-30.json` is the lookahead's first rung under ADR 0083 to 0085, searched as `lookahead:<weights>` from `search-19` against `search-19`, `stun-first`, Greedy and `pressure-floor` (journal, 2026-09-28): on 200 seeds nothing had played it scores above the lookahead with `search-19`'s weights against all four, most against `pressure-floor` (0.95 to 0.78) and least against `search-19` itself (0.73 to 0.71). It nearly stops valuing energy. `search-31.json` is the heuristic's rung on content `9659f610`, searched from `search-19` against Greedy, `kill-first`, `search-21`, `stun-first`, `pressure-floor` and the lookahead on `search-19`'s weights (journal, 2026-09-30): on 200 seeds nothing had played it scores above `search-19` against `kill-first` (0.96 to 0.53), `search-21` and the lookahead, and ties it against the rest, but it loses to `search-19` itself 0.37. It values energy twice as much as `search-19`. `lookahead/lookahead-34.json` is the lookahead's set under ADR 0096 and 0098, kept in a folder of its own because the table offers every file directly under `learning/weights/` as a one-step heuristic seat, searched as `lookahead:<weights>` from the built-in weights against Greedy and `search-23` on 30 benchmark seeds (journal, 2026-10-05): on 60 seeds nothing had played, compared seed by seed with the built-in lookahead, it cannot be told from it against Greedy (0.89 to 0.88) and beats it against `search-23` (0.97 to 0.90, a paired difference of +0.07 clear of zero). It raises initiative, energy and heal and lowers bleed. `lookahead/lookahead-36.json` is the lookahead's set under ADR 0102, searched as `lookahead:<weights>` from `lookahead-34` against the Greedy that prices a purchase by what it adds, on 60 benchmark seeds (journal, 2026-10-08): on 60 seeds nothing had played it beats Greedy 104 matches in 120 where `lookahead-34` takes 55, a paired difference of +0.41 clear of zero. It halves damage against every other weight, which is to say it values kills, stuns, heals and initiative about twice as much against damage, and raises pressure to 0.116. `lookahead/lookahead-37.json` is the lookahead's set under ADR 0103, searched as `lookahead:<weights>` from `lookahead-36` against Greedy on the same 60 benchmark seeds (journal, 2026-10-09): on 60 seeds nothing had played it beats Greedy 82 matches in 120 where `lookahead-36` takes 65, a paired difference of +0.146 clear of zero. It nearly doubles stun, raises kill by a quarter and energy by half, and lowers bleed and pressure. `lookahead/lookahead-39.json` is the lookahead's set with the anticipation of ADR 0104 on, searched as `lookahead:<weights>` from `lookahead-37` against Greedy on the first 120 benchmark seeds, 4 rounds of 8 (journal, 2026-10-09): on 60 seeds nothing had played it beats Greedy 84 matches in 120, even with `lookahead-37` played without the anticipation (82) and above it played with it (74), neither difference settled. It doubles stun again and drops energy to 0.07, which leaves the anticipation, read through the energy term, nearly nothing to move. `lookahead-5`, `lookahead-20` and `lookahead-30` were removed on 2026-10-04, when they no longer beat the built-in lookahead; the sentences above say what they measured when they did. Changing `greedy.json` itself changes nothing for `greedy`, which reads
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
