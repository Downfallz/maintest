# Learning journal

One entry per change that moves a number: content, engine, agents, or the benchmark seeds. Each entry names
the run stamps involved so that any two results can be compared on one axis at a time (ADR 0013). Newest
first.

## 2026-09-10. The catalogue tuned for an agent that defends, and the first-mover share finally lands in its band

- **What this is**: `tune-content --seed 0` against the scorer of the entry below, once its two review
  findings were fixed. 72 candidates, 146 evaluations, **score 148.82 to 40.45**. Applied. Content
  `c0ec6984` to **`be58a32d`**, digest regenerated and verified.
- **Three moves**, and that is all it took:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `lightning_bolt` | damage | 3 | 4 |
  | `rejuvenate` | energy cost | 1 | 2 |
  | `basic_attack` | damage | 1 | 2 |

- **Six of the twelve targets are now on target**, which has never happened before:

  | Target | Before | After | Band |
  | --- | --- | --- | --- |
  | `player1WinShare` | 0.685 | **0.455** | 0.45..0.55 |
  | `averageRounds` | 13.39 | **8.29** | 8..16 |
  | `roundCapShare` | 0.175 | **0.045** | ..0.05 |
  | `drawRate` | 0.015 | 0.005 | ..0.05 |
  | `spellsNeverCast` | 1 | 2 | ..2 |
  | `skill.winRateA` | 1.000 | 1.000 | 0.65.. |

  **`player1WinShare` is the headline.** Going first was worth 0.64 to 0.69 in every entry of this journal,
  no content move had ever touched it, and it is now 0.455 — inside the band. What moved it was not a rule
  about turn order: it was making the defensive half of the catalogue playable, so the side that moves second
  has something to do with the tempo it loses. A heal at 2 energy instead of 1 is what tipped it.
- **What the board looks like now**: seven of nine spells cast, `guard` 573 casts and 1146 buffs applied,
  `rejuvenate` 625 casts and 1717 health restored. `poison_slash` (3) and `pummel` (2) are the two that fell
  away; `wait` and `basic_attack`, dead in every entry before this one, are cast.
- **What it cost**: entropy 1.75 to 1.46 and `spellUsageShare` 0.603 to 0.680, both worse. `lightning_bolt`
  keeps 3848 of 5655 landed casts. The two largest penalties left are `spellUsageShare` (18.53 of the 40.45)
  and `tierUsageShare` (13.71): the game is balanced between the sides and no longer stalls, but one spell
  still owns the catalogue.
- **Read the score against 148.82, not against the 119.40 of two entries ago.** The scorer changed between
  them, so the baseline it starts from is a different number for the same content. This is the same warning
  the tier entry carries, for the same reason.
- **What is next**: `spellUsageShare`. Three of the twelve targets carry 34.7 of the remaining 40.5, all three
  about one spell taking most of the casts, and no move in this pass touched it. Whether a knob search can
  reach it at all, or whether it needs a spell that does not exist yet, is the open question.

## 2026-09-10. Defence gets a price: the catalogue plays seven spells instead of four, and stalls

- **What changed**: ADR 0022 prices a defensive effect by the damage it prevents. No content moved in this
  entry; the digest for content `c0ec6984` is regenerated because the engine changed under it. Engine at the
  commit this entry lands with.
- **What it does**, greedy against greedy on the benchmark seeds:

  | | Before | After |
  | --- | --- | --- |
  | Spells cast | 4 of 9 | **7 of 9** |
  | `rejuvenate` | 0 | 2895 casts, 6509 health restored |
  | `guard` | 0 | 1424 casts, 2656 buffs applied |
  | `poison_slash` | 0 | 963 |
  | Spell entropy | 0.74 | **2.02** |
  | Average rounds | 8.1 | **18.1** |
  | Round-cap share | 0.000 | **0.385** |

  The defensive half of the catalogue came alive on the first try, and it broke the game: two bots that both
  value survival heal faster than they hurt, and nearly two matches in five ran out of rounds. That is the
  honest result of pricing defence correctly in content authored for agents that ignored it. The measurements
  above are the first cut of the scorer; two review findings then changed its arithmetic, so treat them as the
  shape of the effect rather than as numbers to compare against later runs.
- **Two bugs caught in review, both real, both in the direction of overvaluing defence**:
  - Prevention was priced at a point per point of buff. Defense comes off a hit before the floor at zero, so
    a point past a hit's plain damage only reaches its critical branch: against a 3-damage spell at 3
    defense, a fourth point is worth 0.05, not 1. It is now read as the difference between the unbuffed and
    the buffed threat.
  - The denied kill was priced per outcome, and `guard` carries two defense buffs. When one point alone
    crossed the survival threshold the cast was paid `kill` twice; when survival needed both points it was
    paid none. The whole defensive reading now happens once per target, with a target's healing and all of
    its buffs read together, and the buffs priced on top of each other rather than each from the bare board.

  Both ship with a test that fails without the fix. Both were found by review, not by the suite, which is
  worth saying plainly: the six tests written with the feature all passed on the buggy code, because the test
  board has zero defense and one buff per cast — the two cases where the bugs are invisible.
- **What is next**: re-tune the catalogue against this scorer. The proposal that first accompanied this change
  was searched against the arithmetic the two findings corrected and has been discarded rather than kept.

## 2026-09-10. `search-weights` on the nine-spell content: the weights are not what keeps defence off the board

- **What this is**: the experiment the entry below named as the next run. A weight search plays to win and
  nothing else, so it settles whether Greedy ignores `guard` and `rejuvenate` because its eight weights
  undervalue defence, or because defence is genuinely not worth buying in this catalogue.
  `search-weights -o runs/search-9spells --seed 1`, 10 iterations of 16, 161 evaluations, against `greedy`
  on the benchmark seeds (400 matches, mirrored). Content hash `c0ec6984`, engine `f9488f36136f`.
- **What it found**: a candidate at **0.5775** mean score against Greedy, interval [0.536, 0.619], found at
  iteration 2. The population had collapsed onto it by iteration 7 and the remaining four iterations found
  nothing better, so this is a converged search, not a truncated one.
- **The weights, read as ratios to `damage`** (only ratios matter — scaling every weight scales every score):

  | Weight | Greedy | Found | Change |
  | --- | --- | --- | --- |
  | `damage` | 1.000 | 1.000 | — |
  | `kill` | 5.000 | 5.569 | +11% |
  | `heal` | 0.800 | 0.811 | **+1%** |
  | `stun` | 3.000 | 3.141 | +5% |
  | `bleed` | 0.800 | 1.340 | +67% |
  | `buff` | 0.500 | 0.440 | **-12%** |
  | `energy` | 0.200 | 0.055 | -73% |
  | `risk` | 2.000 | 2.172 | +9% |
  | `initiative` | 0.500 | 0.369 | -26% |

- **The answer**: no. A search that cares only about winning left `heal` where it was (+1% is inside the
  noise of a 400-match evaluation) and moved `buff` **down**. Nothing in the objective told it to avoid
  defence; it declined to buy it. The two real moves are elsewhere: `bleed` +67% — damage over time is
  underpriced when a match lasts eight rounds — and `energy` -73%, hoarding energy buys almost nothing.
- **What the winning agent actually cast** (6009 actions):

  | Spell | Casts |
  | --- | --- |
  | `lightning_bolt` | 5205 |
  | `heavy_strike` | 411 |
  | `pummel` | 351 |
  | `throwing_star` | 42 |

  Five of nine spells never cast: `wait`, `basic_attack`, `guard`, `poison_slash`, `rejuvenate`. Across both
  agents, 11,910 actions produced **0 healing, 0 defense buffs, 0 regenerations**. The winner plays the same
  four spells as Greedy in nearly the same proportions; its 7.75 points come from targeting and timing, not
  from a different spell mix.
- **What this rules out and what it leaves**: tuning the eight weights is not the lever. What remains is the
  scorer's pricing and the content itself — `HealScore` counts missing health with no notion of the damage
  actually incoming, and `DefenseBuff` is priced `buff × amount × rounds` on a flat
  `PermanentConditionRounds = 3` rather than by the damage it prevents. Both are ADR territory, in the line
  of ADR 0018 on the initiative weight. The horizon is the third suspect and the one no weight can fix: a
  one-step lookahead cannot see "I survive the round I would otherwise lose".
- **Decision**: apply nothing. `learning/weights/greedy.json` stays as authored — a 57.75% agent that plays
  the same four spells is not a better baseline, it is the same baseline with sharper aim, and changing the
  benchmark opponent would invalidate every comparison in this journal for no insight. The next entry should
  be about pricing a defensive effect by the damage it prevents, not about weights.

## 2026-09-10. First tuning pass on the tier objective: tier 1 becomes a choice, the defensive half stays dead

- **What this is**: the first run of `tune-content` against the objective that reads per tier. 58 candidates,
  118 evaluations, **score 119.40 to 34.73**. Content unchanged: this is a proposal, measured and written
  down, not applied. Scores here do not compare with the 104.05 and 15.56 of the entries below — those were
  a different objective (the entry below says why).
- **The five moves**:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `lightning_bolt` | energy cost | 2 | 3 |
  | `basic_attack` | damage | 1 | 2 |
  | `pummel` | critical chance | 0.667 | 0.717 |
  | `guard` | Spell initiative | 1 | 0 |
  | `rejuvenate` | heal | 3 | 2 |

- **What it does to the play**, built and played to check rather than read off the score:

  | Tier | Before | After |
  | --- | --- | --- |
  | 0 | `heavy_strike` **100%** | `heavy_strike` 81%, `basic_attack` 19% |
  | 1 | `lightning_bolt` 93%, `pummel` 6% | `lightning_bolt` 57%, `pummel` **43%** |

  Tier 1 becomes a real choice, two spells sharing the casts almost evenly where one took everything. Tier 0
  opens as well. Matches run **9.4 rounds**, inside the band. Entropy 0.74 to **1.93**. And `tierWinSpread`
  falls from 0.262 to **0.003**: spells offered together are now worth about the same in results, which is
  the reading that says a tier is a choice rather than a formality.
- **What it does not fix, and this is the finding**: `tierUsageShare` stays at **0.814**, still 19.8 of the
  remaining 34.7, and it is tier 0 — `heavy_strike` keeps 81% and `wait` is never cast. Five of the nine
  Spells are still never cast: `wait`, `guard`, `poison_slash`, `rejuvenate`, and now `throwing_star`. The
  whole defensive half of the catalogue is dead, and the first-mover share does not move on this path either
  (0.640).
- **Why the defensive half is dead, from the scorer rather than from a guess**: `ActionScorer` is a one-step
  lookahead, and on the same board `lightning_bolt` scores about 5.2 (3 damage, doubled by a critical 72% of
  the time, at `damage` 1.0) while `rejuvenate` scores at most 2.4 (`heal` 0.8 × 3 restored) and `guard`
  2.5 (`buff` 0.5 × 1 × 3 permanent rounds, plus 1 × 2 rounds). An attack is worth twice a defence to the
  agent that measures the content, every single time.
- **Which of the two is wrong is a measurable question, not an opinion**: either the weights undervalue
  defence, or defence genuinely is not worth it in a nine-round game with 20 health. `search-weights` tunes
  those eight numbers *for winning* and has never been run on this content. If the weights that win keep
  `heal` low, the content is the problem and no amount of tuning `rejuvenate`'s number will make a bot want
  it.
- **Decision**: apply nothing yet. The next run is `search-weights` on this content, and the entry after
  this one should say whether a bot that plays to win ever buys a heal.

## 2026-09-10. Balance read per tier, and the starting kit turns out not to be a choice at all

- **What changed**: the objective, so **no score from before this entry compares with a score after it**.
  Three targets are added and one is reweighted. No content moved.
- **Why**: the catalogue-wide `spellUsageShare` cannot see a monopolised tier. A Tier is the set of Spells
  offered at one depth of the Talent tree, which is what a player actually chooses between, so that is where
  the question "is this a choice" belongs.
- **The three readings**, each on its worst tier: `tierUsageShare`, the largest share of landed casts one
  Spell takes inside its tier; `tierDamageSpread`, how many times harder the best damaging Spell of a tier
  hits per landed cast than the worst; `tierWinSpread`, the gap between the best and worst win share. Each
  skips what it cannot read — a tier nobody cast, a Spell that deals no damage, a Spell too few sides
  declared — rather than guessing, using the engine's own threshold of eight sides.
- **The finding, and it is not small**: on the core content `spellUsageShare` reads 0.855 while
  **`tierUsageShare` reads 1.000**. `heavy_strike` takes *every* landed cast of tier 0; `basic_attack` and
  `wait` take none. Tier 1 is barely better, `lightning_bolt` at 93% against `pummel` 6% and
  `throwing_star` 0%. The starting kit every match is dealt is not a choice, and no rule caught it:
  `startingKitOffersAChoice` only refuses strict dominance, and `basic_attack` is cheaper than
  `heavy_strike`, so nothing is strictly better than anything. It is simply never worth casting.
- **What it costs at 3 energy**: with `lightning_bolt` at 3, `tierUsageShare` is still 0.874 — tier 0, the
  same problem — while `tierWinSpread` falls from 0.262 to 0.071. Fixing the Sorcerer's price does nothing
  for the starting kit, which the catalogue-wide reading could not have told us.
- **`spellUsageShare` drops to weight 1.** The tier reading is the better instrument and catches everything
  the wide one does; the wide one stays as a coarse guard rather than double-counting at full weight.
- **Decision**: keep, apply no content change. The next question is no longer only what `lightning_bolt`
  costs, it is why a creature never casts two of the three Spells it starts with.

## 2026-09-10. The search sweeps first: 104.05 to 15.56, and two good moves that do not add up

- **What changed**: the tuner, not the content. Three things, after the entry below left one spell holding
  85% of the casts and the search failing to touch it (ADR 0021).
- **Dominance reads the Talent tree now.** A Spell is compared against another at its own depth or deeper,
  because reaching a deeper node costs picks and prerequisites: being better there is the reward. On this
  content the report goes from three pairs to none, and all three were a tier-1 Spell beating a tier-0 one.
  Depth comes from the Creature's starting Spells and from walking the trees, shallowest wins.
- **The search sweeps every playable knob once before it climbs.** The reason is a measured miss: a uniform
  draw over 29 knobs with 40 candidates leaves a one-in-four chance a given knob is never tried, and the
  previous run lost that flip on `lightning_bolt`'s energy cost — the single best move in the catalogue.
  The random phase that follows leans four to one on the knobs the sweep showed can move a metric, and
  draws only from Spells the build carries: 110 of the 139 knobs sit on turned-off Spells.
- **A bug the sweep found immediately**: the first sweep returned zero playable candidates. `violations()`
  rebuilt the candidate catalogue with only its Spells and files, so the tiers went missing, so a tiered
  "before" was compared against an untiered "after" and every candidate read as adding the catalogue's three
  progression pairs. `Content.with_spells` is the one way to say "the same catalogue with other numbers in
  it" now. 40 of 58 sweep moves are legal; 6 are refused for genuinely creating a same-tier dominance.
- **The run**: 52 candidates, 106 evaluations, **score 104.05 to 15.56** against 88.76 for the run before.
  Five moves, and the first is the one that was never tried:

  | Move | From | To |
  | --- | --- | --- |
  | `lightning_bolt` energy cost | 2 | 3 |
  | `guard` Spell initiative | 1 | 0 |
  | `basic_attack` damage | 1 | 2 |
  | `pummel` critical chance | 0.667 | 0.717 |
  | `rejuvenate` heal | 3 | 2 |

  Five of the nine targets are inside their band: average rounds **9.44**, draws, round cap, fizzle rate
  **0.156**, and the skill gap. `spellUsageShare` falls from 0.855 to **0.357** and entropy rises from 0.74
  to **1.93**. Only 3 of 52 candidates changed no metric, against 11 of 32 before, which is the draw no
  longer landing on Spells nobody casts.
- **What is left is the first-mover edge**: `player1WinShare` 0.640, 9.7 of the remaining 15.6 points.
- **And a negative result worth more than the run**: the entry below said the two findings should compose.
  They do not. `lightning_bolt` at 3 *and* `pummel` at Spell initiative 0, measured together, score
  **25.27** — worse than the sweep's five moves, and `player1WinShare` goes to **0.680**, worse than either
  change alone. Taking the cheap unlock's tempo away helped while `lightning_bolt` cost 2 and hurts once it
  costs 3. A proposal is only valid for the catalogue it was measured on, which is an argument for
  searching again after every accepted change rather than stacking proposals.
- **Decision**: still apply nothing. The tool is now worth pointing at the question, and the question is
  what `lightning_bolt` should cost, with the first-mover edge measured again afterwards.

## 2026-09-10. `tune-content` on the nine Spells: the first-mover edge finally moves, and it costs length

- **What this is**: the entry the one below promised. No content changed — this is what the search proposes,
  measured, and nothing from it is applied. 40 candidates at `--seed 1 --iterations 10 --neighbours 4`, 82
  evaluations, about a quarter of an hour on content `c0ec6984`.
- **Score 104.05 to 88.76** over five moves (ADR 0021 scores the distance outside every band, zero being on
  target):

  | Move | From | To |
  | --- | --- | --- |
  | `pummel` Spell initiative | 1 | 0 |
  | `pummel` damage | 2 | 1 |
  | `lightning_bolt` damage | 3 | 4 |
  | `lightning_bolt` critical chance | 0.667 | 0.617 |
  | `poison_slash` energy cost | 2 | 3 |

- **The number that moved is the one nothing had moved**: `player1WinShare` **0.665 to 0.535**, inside the
  0.45 to 0.55 band for the first time since the first digest. Making matches half again as long did not
  touch it (the entry below); taking a point of Spell initiative off the cheapest unlock did. That is ADR
  0017 read backwards: unlocking is how a Creature gets faster, so the cheapest unlock decides who acts
  first for the rest of the match, and `pummel` at one energy is the cheapest there is.
- **It paid for that in length**: `averageRounds` **8.135 to 5.865**, back outside the 8 to 16 band we had
  just entered, and `fizzleRateA` 0.180 to 0.209. The objective weighs the first-mover share at 3 with a
  scale of 0.05 and length at 2 with a scale of 3, so a tenth of the share is worth more to it than two
  rounds. That is a choice written in `data/balance/knobs.json`, not a fact about the game, and this run is
  the first evidence about whether it is the right one.
- **The dominant Spell is untouched**: `spellUsageShare` 0.855 to 0.846, still 71 of the remaining 88.8
  points. A hill climb moving one number one step cannot close a gap that wide, and it raised
  `lightning_bolt`'s damage rather than lowering it, because damage barely moves its share while it does
  move the length the objective is also chasing. `spellsNeverCast` stayed at 5 of 9.
- **Decision**: apply nothing. The proposal names the right lever and the wrong price for it. What
  `lightning_bolt` should cost is a design question, and answering it first is what would let a search
  spend its budget on the rest.

## 2026-09-10. The core three classes only: matches lengthen, and one spell takes 86% of the casts

- **What changed**: the content, not the engine. The Creature moved from the full talent tree onto a new
  `talent-tree:core_classes:v1` — the same root and the same three class nodes, without the nine
  specialisations — and the old tree and the 27 specialisation Spells were turned off with
  `"enabled": false` (ADR 0015). The build carries **9 Spells** instead of 36: `wait`, `basic_attack`,
  `heavy_strike`, then `pummel` and `guard`, `poison_slash` and `throwing_star`, `lightning_bolt` and
  `rejuvenate`. Nothing on disk was deleted; turning the flags back restores the catalogue.
- **Why**: the balance signals on 36 Spells were dominated by content no match reaches. 26 of them were
  never cast, so two thirds of the tuner's score was dead content and no single number could move it
  (ADR 0021). Nine reachable Spells is a catalogue a balance pass can actually close.
- **Digest**: `benchmarks/c0ec6984e2c1df0805941aab44649f2d54ebcb5aadb4fc8c1de56a2ee4a7a960.json`, `Greedy`
  against `Greedy` on the 200 benchmark seeds, mirrored, engine `5475d117d0eb`, against
  `50a291d5...` before. Content is the only axis that moved.
- **Matches got longer, which is what we wanted**: **8.1 rounds on average** against 5.8, spread 6 to 10
  against 5 to 9, still every one by elimination and none by the round cap. The winner ends on **10.1
  health of 60** against 17.0, so the matches are longer *and* closer. Taking the specialisations away took
  away the big single casts — Psycho Rush and Hateful Sacrifice hit for 9 and 10 — and what is left trades
  in twos and threes.
- **The first-mover edge did not move**: 133 of 200, **66.5%**, against 128 and 64.0%. Well outside the
  band the objective asks for and unchanged by making matches half again as long, which says the edge is
  not about how long the race is.
- **Entropy collapsed, and the reason is one Spell**: **0.74 bits** against 2.21. `Greedy` declares four
  Spells of the nine, and `lightning_bolt` takes **4172 of 4881 landed casts, 85.5%**, for 20094 of the
  22000 damage dealt. `heavy_strike` lands 400, `pummel` 288, `throwing_star` 21. `wait`, `guard`,
  `poison_slash`, `rejuvenate` and `basic_attack` are never declared at all.
- **Which the audit already predicted**: `check-knobs` reports three strict dominances in this catalogue,
  and one of them is `lightning_bolt` over `heavy_strike` — same targeting, same cost of 2, same Spell
  initiative, 3 damage each, and a critical chance of 0.667 against 0. There is no reason to ever declare
  the second, and `Greedy` does not. The other two are `pummel` and `throwing_star` over `basic_attack`.
- **Fizzles fell to 18.0%** from 21.4%, and criticals rose to 53.7% from 25.4%: with `lightning_bolt`
  taking most casts, the run's critical rate is close to its own.
- **Objective**: `spellsNeverCast` moved from a band of 12 to a band of **2**. Twelve was written for a
  catalogue of 36 and cannot be exceeded by one of 9, so it had stopped being a target at all.
- **Decision**: keep. This is the content the balance work continues on, and it poses exactly one obvious
  question — what `lightning_bolt` should cost — plus the two dominances under it. The next entry is what
  `tune-content` does with that.

## 2026-09-10. Energy gets a price and a lasting kind: nothing moves, and that is the finding

- **What changed**: (a) `ActionScorer` scores `EnergyOutcome`, which it never did — the switch matched
  `HealOutcome` and `ConditionOutcome` and let energy fall through to zero, while the `weights.Energy` term
  beside it priced only the energy the actor *keeps* after paying. (b) `EnergyRegeneration` joins the effect
  taxonomy, `Regeneration` with energy in place of health, given before the healing and the bleeds and never
  wasted because energy has no cap (ADR 0020). (c) `SpellOutcome.Buffs` splits into `DefenseBuffs` and
  `InitiativeDebuffs`: one number for two stats could not say which one a spell moved. (d) An outcome on a
  creature the same action kills is no longer scored on any of the three paths, since `Heal` and `GainEnergy`
  both return zero on a corpse.
- **Digest**: unchanged. `benchmarks/50a291d5…7a87.json` verifies with **400 of 400 matches identical**,
  engine `094515bf7822`, schema `features:v3+18b1bd690dff`. This entry exists anyway, because the absence of
  movement is the result: the journal's rule is one entry per change that moves a number, and the number that
  refused to move is the whole point.
- **What started it**: 200 recorded `Greedy`-vs-`Greedy` matches, read off the traces. Of **5096
  declarations, none was one of the five `EnergyGain` spells** — not Wait, which every creature knows from
  round one, not Summon Minions, which gives 3 energy for a cost of 2. The agent was structurally unable to
  value any of them, so the count is not a preference, it is a blind spot.
- **Why fixing the blind spot changed nothing**: a point of energy is worth 0.2, and the wall is arithmetic.
  Best-case one-step score of what `Greedy` actually picks against what it never picks:

  | Spell | Score | | Spell | Score |
  | --- | --- | --- | --- | --- |
  | `meteor` | 18.0 | | `restorative_gush` | 4.8 |
  | `engulfing_flames` | 12.0 | | `healing_screech` | 3.2 |
  | `ice_spear` | 7.0 | | `restorative_burst` | 2.8 |
  | `lightning_bolt` | 5.0 | | `rejuvenate` | 2.4 |
  | `heavy_strike` | 3.0 | | `summon_minions` | 0.6 |
  | `basic_attack` | 1.0 | | `wait` | 0.2 |

  The best heal in the game, cast at the one moment it caps out, loses to `lightning_bolt`. The best energy
  spell loses to `basic_attack`. No decision flips, so the 400 mirrored seeds replay identically.
- **The same measurement, for healing**: **0 of 5096** declarations were a heal, and the reason is not the
  declaration step. In **1798** declarations the actor was missing health — often 8 to 14 of 20 — and in
  exactly **2** of those did it know a heal at all. The break is at the unlock: **2 heal unlocks out of
  4660**. And **47% of evolution choices are made at full health**, where a heal is worth exactly zero by
  construction, round 1 being 800 choices at 100% full — the one round `rejuvenate` is offered beside
  `lightning_bolt`. The spell is on the table precisely when it cannot score.
- **What the fix does buy, then**: the knob exists. Before, no value of `weights.energy` could make an energy
  spell visible, because the gain was multiplied by nothing; `search-weights` could not have found a setting
  that worked, whatever it tried. Now it can, and whether one exists is an empirical question about the
  content rather than a property of the code.
- **Two things I predicted and measured to be false**, both corrected in ADR 0020. I expected the digest to
  move — it does not. And I wrote that energy touching no health made its place in the tick order irrelevant
  — it is not: every upkeep loop skips the dead, so a creature its own bleed kills that round keeps the
  energy it was just given and still reports a tick. No health number and no match result depends on the
  position, which is the narrower claim that survives.
- **`features:v3`**: publishing a condition kind changes the observation layout, so nothing trained under v1
  or v2 is comparable. `EnergyRegeneration` sits beside `Regeneration`, so the three over-time effects stay
  together and every index from +10 on shifts. The Python side reads all three versions; the engine plays
  only the one it reads.
- **No spell uses the new kind yet**, deliberately. Re-pricing Momentum and Summon Minions in the change that
  adds the kind would leave nothing able to say which half moved the numbers. That is also why this change is
  measurably behaviour-preserving, which is what let the two review fixes ride along without muddying it.
- **Still not a balance pass**: nothing here was tuned. Both findings now point at the same place — the
  content's numbers, not the agent — and neither the heal amounts nor the energy amounts have ever been
  chosen by measurement.

## 2026-09-09. Initiative on unlock, priced, and a healing over time: the first-mover edge falls to 64%

- **What changed**: three things, and the digest cannot separate them, because `main` took ADR 0017 and
  ADR 0018 without regenerating. (a) Unlocking a spell raises the creature's Base initiative by the Spell
  initiative (ADR 0017), so evolving is also how a creature gets faster. (b) The heuristic agents price that:
  a new `initiative` weight at 0.5, an unlock scored as its combat value plus what it buys, and an
  `InitiativeDebuff` moved from `w.buff` to `w.initiative` so one point has one price (ADR 0018).
  (c) `Regeneration` joins the effect taxonomy, the healing counterpart of `Bleed`, healing before bleeds
  tick; Healing Screech goes back to the prototype's `Heal 2` plus `Regeneration 2` for a round (ADR 0019).
- **Digest**: `benchmarks/50a291d52dbafd5ba25ee92843b04ed92831d1064f436ea667ddc11f5a5c7a87.json`, `Greedy`
  against `Greedy` on the 200 benchmark seeds, mirrored, default rule set, engine `93b090446b0d`. Schema
  `features:v2+0129dfba4876`: publishing a condition kind changes the observation layout, so this is the first
  run under `features:v2` and nothing trained on v1 is comparable to it.
- **The number that moved**: player 1 wins **128 of 200** distinct matches, **64.0%** (95% interval 57.3% to
  70.7%), against **163 of 200, 81.5%** on the previous content. Read on 200, not 400: both agents are
  `Greedy` and an agent is seeded from the match seed and the slot, so the mirrored pass replays the same
  match, confirmed here on 200 of 200 seeds. The first-mover edge is still far outside noise, but a third of
  it is gone, and initiative is the only thing that could have moved it: the creature that unlocks first is no
  longer the creature that acts first for the rest of the match.
- **The rest barely moved**: 368 of 400 entries differ but 286 keep the same winner. Matches run 5 to 9
  rounds, **5.8 on average** against 5.7, still every one by elimination and none by the round cap. The
  winner ends on **17.0 health of 60** against 24.2, so the matches are closer as well as less decided by the
  slot. Fizzles 21.4% against 24.5%, crits 25.4% against 24.7%, spell entropy **2.21 bits** against 2.33.
- **Regeneration is in the engine and absent from the play**: `Greedy` declares ten spells and Healing Screech
  is not among them — it is one of the two declared by fewer than eight sides, and the `Heal` column of the
  spell table is zero on every listed row. The effect resolves, the content uses it, and the baseline still
  never heals. That is the same finding as the entry below, unchanged by giving the defensive half a better
  tool: a one-step lookahead that scores damage does not buy a heal, and a match that ends in under six rounds
  does not get to want one.
- **What the spell table does say**: `pummel` 84.2% on 19 sides and `engulfing_flames` 68.7% on 249 lead;
  `throwing_star` 36.5% and `basic_attack` 39.0% trail. The Berserker line has all but vanished —
  `tornado` 7 declarations, `psycho_rush` 2, against 90 and 131 before — which is what pricing initiative
  did to a line whose spells cost a lot and buy no tempo.
- **Supersedes**: the bullet of the entry below reading "`spell.Stats.Initiative` is dead data ... nothing in
  the domain reads a spell's initiative". True of the engine when it was written, false from ADR 0017 on. The
  stat is `SpellStats.SpellInitiative` now, and `Creature.UnlockSpell` reads it.
- **Still not a balance pass**: none of these numbers was chosen. The `initiative` weight at 0.5 is reasoning,
  not measurement, and `search-weights` has never seen it.

## 2026-09-09. The spells stop being placeholders: matches get four times shorter, player 1 takes 81.5%

- **What changed**: content only. The 36 spells were placeholders — every one of them `Damage 1`, cost 0,
  initiative 1, one enemy — and now carry the prototype's numbers, read from
  `legacy/DownfallArena/DA.GameResources` and documented spell by spell in `docs/domain/spells.md`. Costs 0 to
  4, initiatives 1 to 3 (inert — see the last bullet), Critical chance bonuses 0 to 0.667, 27 single-target
  against 9 multi, 23 aimed at enemies against 9 at allies and 4 at the caster, all seven effect kinds in use
  instead of one, and the real Creature class on each. Nothing in the engine or the agents moved.
- **Digest**: `benchmarks/a63d952bbb54d31f74b66244018cd9aa015b1cc4c3ef5e74cc8da66df3b93153.json`, played by
  `Greedy` against `Greedy` on the 200 benchmark seeds, mirrored, under the default rule set. Engine
  `dc6e40ffb2a2`. It does not supersede `34c616d3...` so much as leave it behind: different content, so the
  two are comparable as a whole and not term by term.
- **Numbers**: 400 matches, player 1 wins **326**, player 2 wins **74**, **no draw at all**, every match by
  elimination. The placeholder content gave 218 / 96 / **86 draws**. Matches now last **5 to 8 rounds, 5.7 on
  average** (170 at five, 172 at six, 52 at seven, 6 at eight) against 19 to 23 and 22.0 before; the round cap
  of 30 is as far out of reach as it ever was. The winner ends with **24.2 health of 60 on average**, spread 1
  to 40, against 3.4 and a spread of 1 to 12. All 400 entries differ from the old digest, and 204 of them keep
  the same winner.
- **What drives the shortening**: the starting kit is `wait`, `basic_attack` and `heavy_strike`, and
  `heavy_strike` went from 1 damage to 3. Three times the damage per activation against unchanged health is
  the whole of the four-fold drop in length, and the rest follows from it: a match decided in five rounds
  leaves no room to trade back, so the loser is eliminated wholesale rather than ground down to a draw, and
  the winner keeps two thirds of a creature's health that the twenty-round grind used to consume.
- **The player 1 edge got worse, not better**: 54.5% of the wins became **81.5%**. Read it on 200 matches,
  not 400: both agents are `Greedy` and an agent is seeded from the match seed and the slot, so the mirrored
  pass replays the same match and the digest holds each one twice. 163 of 200, 95% interval 76.1% to 86.9%,
  is far outside the noise all the same. Reading it with the length: the damage race is now short enough that
  the slot that wins the initiative tie is close to deciding it. This is the number a rule change should move
  (initiative, pick order, the energy curve), and it now has room to move in.
- **Entropy 0.23 to 2.33 bits, which is the answer `ci-9` asked for**: that entry closed the value-learning
  investigation on the finding that the content posed no decision, and named the sign to watch. `Greedy` now
  declares **nine** distinct spells where it declared two, and 2.33 bits is 74% of the 3.17 available over
  nine. Of 5182 declarations: `heavy_strike` 44.9%, `ice_spear` 18.7%, `lightning_bolt` 15.7%, `meteor` 8.1%,
  `engulfing_flames` 4.7%, `pummel` 2.6%, `psycho_rush` 2.5%, `tornado` 1.7%, `basic_attack` 1.0%. The
  content poses a decision now. Whether the decision is *good* is the rest of this entry.
- **Fizzles 5.1% to 24.5%, and that is the cost of the short match**: a quarter of all declarations no longer
  land. Resolve rates split the field — `pummel` 94.1%, `lightning_bolt` 87.3%, `engulfing_flames` 84.6%,
  `heavy_strike` 80.0% against `ice_spear` 55.5%, `basic_attack` 49.0%, `psycho_rush` 48.9%, `tornado` 43.3%.
  Reading, not measurement: the timeline is fixed before intents are declared, so in a five-round match the
  actor or its target is often dead by the time the slot comes up. The fizzle breakdown by reason is not in
  the output; it is what would confirm this.
- **Crits 4.9% to 24.7%**: the placeholder spells all had a Critical chance bonus of 0 on a creature at 0.05,
  so a crit was the creature's own rate. The bonuses now run to 0.667 and the roll moves.
- **Twenty-seven of the thirty-six spells are never declared**, and the shape of the nine that are is one
  shape: `Heal`, `Stun` and `Bleed` are **zero** across the whole table. `Greedy` never heals, never stuns,
  never bleeds; the only non-damage effect it ever applies is `ice_spear`'s initiative debuff, 378 times. It
  evolves down Sorcerer to Wizard and Brawler to Berserker and never touches the defensive half of the
  catalogue. Two readings, and they are not exclusive: a one-step lookahead that scores damage is the wrong
  instrument for a heal, and a match that ends in five rounds never gets to want one.
- **The class lines are not close**: win share of the sides that declared each spell, against 50% —
  `engulfing_flames` **72.0%** (218 sides), `meteor` 54.9% (293), `lightning_bolt` and `heavy_strike` 50.0%
  (400 each), `ice_spear` 49.4% (395), `tornado` 33.3% (60), `pummel` **27.9%** (136), `psycho_rush`
  **16.8%** (131). Correlation, not cause: a side that declared `pummel` is a side that went Brawler, and it
  is the Brawler line that loses. The Wizard line wins, the Berserker line is a trap, and that gap is the
  first balance question this content actually poses.
- **`spell.Stats.Initiative` is dead data**: the initiatives 1 to 3 the prototype gave its spells change
  nothing. `TimelineBuilder` orders the round from the speed choices and `creature.CurrentInitiative`, and the
  timeline is built in Planning, before any intent exists; nothing in the domain reads a spell's initiative.
  Only `ContentAudit` does, and its `FlatSpellStat` line for it — "the spell a creature declares never changes
  when it acts" — describes an effect the engine does not implement. `InitiativeDebuff` is the only thing that
  moves turn order today, which is most of why `ice_spear` earns its place.
- **Not settled**: none of these numbers is a balance pass. They are the prototype's, and
  `docs/domain/spells.md` lists the six legacy mechanics that have no counterpart in the effect taxonomy
  (ADR 0012) and were dropped or approximated — among them the caster-side costs that made
  `hateful_sacrifice` and `parasite_jab` a choice rather than a nuke.

## 2026-09-09. `ci-9`: the baseline works, and it says the content has no decision in it

- **What changed**: ADR 0016 implemented. Same run as `ci-5` otherwise — the thousand-match explored dataset,
  alpha 10, min samples 10 — so the two-part fit is the only difference. Engine `1cc41a7797e3`.
- **The fit improved**: loss 0.8050 to **0.7705**, r² 0.1876 to **0.2225**. 293 fitted actions of 455, the same
  as `ci-5`, as expected. Against `Greedy`: 0 of 400, the eighth time. Against `Random`: 83.2%.
- **The number that ends the investigation**: **`baselineR2` 0.2815 against `r2` 0.2225.** The position alone
  explains more of the held-out return than the position and the action together. The action rows do not
  merely add nothing; they add variance, and the prediction is better without them.
- **Why, and it is not the learner**: a creature starts with three spells, and they are
  `basic_attack` (1 damage), `wait` (1 damage, effects identical to `basic_attack` to the character) and
  `heavy_strike` (1 damage plus Bleed 1 for one round). All three cost 0 energy, all three have initiative 1,
  all three target one enemy. `heavy_strike` therefore strictly dominates: same cost, same speed, same
  targeting, strictly more damage. `wait` and `basic_attack` are one spell under two names. There is no
  trade-off on any axis, and `baseEnergy` is 0, so the resource axis is inert too.
- **Which explains every earlier number**: `Greedy`'s spell entropy of 0.23 is not a defect, it is correct
  play; exploration's deviations are uniformly worse by an amount the position already carries; and an action
  that carries no information cannot be fitted, however the data is recorded or the model is shaped. The
  metric added to diagnose the model diagnosed the content instead.
- **Decision**: stop here on value regression. It is not broken, it is asking a question this content does not
  pose, and the temporal-difference target considered next in ADR 0016 is dropped with it: better credit
  assignment for a decision that does not exist would refine an instrument aimed at nothing. Behaviour cloning
  stays the loop's working learner. The loop's own balance signals — spell entropy, player 1 share, draw rate,
  round cap share — are the instrument for the content work that comes next, and `Greedy`'s entropy rising
  above 0.23 is the sign that the content finally offers a choice.

## 2026-09-09. `ci-5`: filling the empty rows changes nothing, so the shape is the answer

- **What changed**: `--value-min-samples` 50 to 10 on the same thousand-match explored dataset, alpha still
  10, so the regularization does the work the threshold was doing. Engine `a621693e9d91`.
- **The rows filled up**: **293 fitted actions of 455**, against 191. A hundred and two keys that were
  constants now have a regression of their own.
- **Nothing else moved**: r² 0.1895 to **0.1876**, loss 0.8031 to 0.8050, accuracy 0.2941 to 0.2947. The
  held-out fit is flat to three digits, and against `Random` the policy got worse, 88.8% to 82.0%. Against
  `Greedy`: 0 of 400, the seventh time.
- **What that closes**: the data axis. Exploration was necessary and not sufficient (`ci-1` to `ci-3`); five
  times the matches tripled the fit and moved nothing (`ci-4`); giving two thirds of the actions a model
  instead of two fifths moved nothing either. The rows that had no model were not the bottleneck, so no
  amount of recording is going to be.
- **What is left**: the shape. Each action key is a regression of its own, fitted on the raw match return of
  the steps where it was taken, and then compared with the others at one state. A step's return is the
  outcome of a match of about a hundred and fifty decisions: it measures the position far more than the move,
  and each row's intercept is calibrated on its own slice of positions. That is the same sentence as the very
  first diagnosis in this journal, and the data has now ruled out every explanation except it.
- **Decision**: ADR 0016, since accepted: fit one state-value model on every step and regress each action on the
  residual instead of the return. The baseline is the best-determined part of the model and subtracting it
  leaves each row only the part of the outcome its own action is responsible for.

## 2026-09-09. `ci-4`, a thousand matches: the fit triples, the win rate does not move

- **What changed**: `--matches` 200 to 1000, everything else as in `ci-3` (explored at 0.2 from seed 1,
  alpha 10, min samples 50). Engine `007057d2103b`, content `34c616d3…80d7`. Fifteen minutes on a runner.
- **The data**: 313,297 steps over 2,000 episodes, and **455 action keys** where 200 matches found 382.
- **The fit**: **191 fitted actions of 455** (42%, against 24% before), loss 0.9607 to 0.8031, r² 0.047 to
  **0.190**, accuracy 0.304 to 0.294.
- **The win rate**: 0.0% against `Greedy`, 0 of 400, the sixth time. 88.8% against `Random`, down from 94.8%,
  and a spell entropy of 2.58 against 2.70.
- **The part that matters for what to do next**: the number of action keys grows with the data. Five times
  the matches found seventy-three new keys, so the share of rows that clear the threshold climbs slowly
  instead of converging. More matches is not a trajectory that ends anywhere.
- **Also**: the clone, trained on the pure 1000-match dataset, came out at 35.5% against `Greedy` (interval
  32.3% to 38.7%) where the 200-match clone reached 38.0%, with its best epoch at 20 instead of 3. Recorded
  as a fact, not read as a regression: it is one run and the intervals nearly touch.
- **Decision**: one more cheap run before blaming the shape of the model. On this dataset, drop
  `--value-min-samples` to 10 and keep alpha 10, so the regularization does the work the threshold was
  doing. If that fits most of the 455 rows and the win rate is still zero, the threshold is no longer an
  excuse and the suspect is the shape itself: one independent regression per action key, compared with each
  other at a single state, with nothing tying them together. That would be an ADR.

## 2026-09-09. `ci-3`: three quarters of the actions have no model at all

- **What changed**: nothing but the diagnostic. `ci-3` repeats `ci-2` exactly — same explored dataset, same
  alpha 10 and min samples 50 — and returns the same numbers to the digit: loss 0.9607, r² 0.04694, accuracy
  0.3044, 0 of 400 against `Greedy`, 94.75% against `Random`. The loop is deterministic and `fittedActions`
  costs nothing.
- **The number**: **93 fitted actions out of 382**. Two hundred and eighty-nine keys kept the mean of their
  few examples instead of a regression, and a row that is a constant scores the same in every state. So for
  three quarters of the legal moves the policy cannot tell one position from another; it simply prefers
  whichever constant is largest. That is what the spell entropy of 2.70 is made of.
- **What it means, read with `ci-1`**: this is a squeeze, not a mystery. At a threshold of 5 nearly every row
  gets a regression, on far too few examples, and the fit comes out worse than the mean. At 50 the fit turns
  positive and three quarters of the rows lose their model. 62,358 steps over 382 keys is about 163 per key
  on average, skewed enough that only 93 clear fifty in the training split. Exploration multiplied the action
  keys by five and the dataset did not follow.
- **Decision**: raise `--matches` to 1000 before concluding anything about the shape of the model. The
  question "is one independent regression per action the wrong shape" cannot be answered on a dataset where
  most of those regressions were never fitted.

## 2026-09-09. The tuned exploring run (`ci-2`): the fit moved, the win rate did not

- **What changed**: the same explored dataset as `ci-1` (`explore:0.2`, 200 matches from seed 1, content
  `34c616d3…80d7`), trained with `--value-alpha 10 --value-min-samples 50` instead of the defaults. It was
  asked for by committing `learning/experiments/next.json`, and the loop ran on the pull request that
  carried it. Engine `454dc1a937e6`, clean: the stamp defect of `ci-1` is gone.
- **The fit moved**: r² -0.146 to **0.047**, loss 1.155 to 0.961, accuracy 0.293 to 0.304. Against `Random`
  the policy went from 82.2% to **94.8%** and its spell entropy fell from 3.40 to 2.70. The regularization
  did produce a measurably better policy.
- **The number that matters did not**: 0.0% against `Greedy`, 0 of 400, for the fifth time.
- **What that settles**: the knobs were the confound in `ci-1`, and they are not the obstacle. Two runs now
  bracket them — worse than the mean at alpha 1 and min samples 5, positive at alpha 10 and min samples 50 —
  and the win rate against `Greedy` is exactly zero in both. Exploration at this rate, on a dataset this
  size, does not make value regression competitive with the bot that produced the data. ADR 0014 was
  necessary, since the counterfactuals now exist, and it is not sufficient.
- **The one thing still unmeasured**: with 382 action keys and a threshold of 50, an unknown share of the
  rows kept a mean instead of a model, and a row without a model cannot tell two states apart. `train-value`
  now reports `fittedActions` beside `actions`, so the next run says it outright.
- **Decision**: read that number first. If most rows are starved, the answer is more matches. If most are
  fitted, the remaining suspect is the shape itself — one independent regression per action key, ranked
  against each other at a single state — and the next thing to try is a single model over the state and the
  action together, which needs its own ADR.

## 2026-09-09. First exploring run (`ci-1`), still 0 of 400, and two things moved at once

- **What changed**: the value policy trained on a dataset recorded with `explore:0.2` instead of pure
  `Greedy` self-play (ADR 0014), on the same content `34c616d3…80d7`. This is also the first run of the
  `Learning loop` workflow, on the merge commit of pull request #24, so the whole turn played on the CI
  runners in three minutes with nobody at a keyboard. Its stamp reads `2988cd742f9c-dirty` because the
  workflow wrote its log inside the checkout before the build stamped the version; the tree was otherwise
  that commit exactly. Fixed in the same change as this entry, so the next stamp is clean.
- **The two datasets**, 200 matches each from seed 1: pure `Greedy`, 63,706 steps over 75 distinct action
  keys; explored, 62,358 steps over **382** action keys.
- **Value policy**, at the defaults (alpha 1.0, min samples 5): loss 1.155, **r² -0.146**, accuracy 0.293 on
  12,320 held-out steps. 0.0% against `Greedy`, 0 of 400 for the fourth time, and 82.2% against `Random`.
  Its spell entropy against `Greedy` is 3.40, next to `Random`'s 4.25 and nowhere near `Greedy`'s 0.20: the
  policy scatters instead of choosing.
- **Clone policy**, still trained on the pure dataset and therefore still the control: 98.6% accuracy, 38.0%
  against `Greedy` (score 0.475), 100% against `Random`. Unchanged, as it should be.
- **What this settles and what it does not**: exploration did deliver what ADR 0014 asked of it. Seventy-five
  action keys became 382, so the actions `Greedy` never plays now carry samples of their own. But the same
  62,000 steps are spread over five times as many independent regressions, and this run used the defaults
  rather than the `--value-alpha 10 --value-min-samples 50` that had taken r² from 0.216 to 0.372 on the
  greedy dataset. The dataset and the knobs moved together, so a fit that is now worse than predicting the
  mean does not by itself refute the ADR.
- **Decision**: repeat the run with those two knobs on the explored dataset, which is one dispatch of the
  workflow. If the win rate is still zero once the fit is no longer worse than the mean, the remaining
  suspect is the shape of the model itself, one independent row per action, and the next thing to try is a
  single model over the state and the action together.

## 2026-09-09. Two tuning attempts and a mixed dataset, all still 0 of 400

- **What changed**: nothing in the engine or the content; three trainings of the value policy on the same
  content `34c616d3…80d7`, evaluated against `Greedy` on the 200 benchmark seeds, mirrored.
- **The three attempts**: 200 matches of `Greedy` self-play at the defaults (run "premier"); the same
  fivefold larger with `--alpha 10 --min-samples 50` (run "second"); and the union of a 200-match `Greedy`
  self-play with a 200-match `Random` self-play, `--allow-mixed`, at those same settings. Win rate against
  `Greedy`: 0.0000 each time, no draw, 400 matches each time. The held-out fit did improve between the first
  two (r² 0.216 to 0.372, loss 0.729 to 0.508), so the models are genuinely different and the metric that
  matters ignored it.
- **The control that matters**: behaviour cloning on the same data, through the same `PolicyAgent`, reaches
  98.5% accuracy and 38.0% against `Greedy`, statistically `Greedy`'s own 39.25%. The encoding, the policy
  file, the agent and the evaluation are therefore sound, and the fault is in what value regression is asked
  to learn, not in the plumbing.
- **Why**: each action key gets its own regression, fitted only on the steps where that action was taken.
  Under a deterministic policy those subsets are disjoint state distributions, so `heavy_strike` is fitted on
  the states where `Greedy` wanted it and `basic_attack` on the leftovers. The return then measures how good
  those situations were, not how good the action is, and ranking two such rows at one state compares models
  calibrated on different worlds. `Random` self-play does not repair it: it covers actions in states no
  strong policy visits, with returns a single action barely moves. This supersedes the "overfitting on a rare
  action" reading of the entry below, which explained the extreme weights but not why more data and more
  regularization changed nothing.
- **Decision**: stop tuning this learner. ADR 0014 proposes recording with an exploring agent, which is the
  one change that puts the same state distribution behind every row. Both cheap attempts it names as
  prerequisites are now done and both failed.

## 2026-09-09. First full turn of the loop (`scripts/iterate.sh`), run "premier"

- **What changed**: nothing in the engine or the content on purpose; this is the first end-to-end run of the
  L7 loop, on content `34c616d3…80d7` (the digest already committed), engine `d3f1fe3284bc-dirty` (the L7
  pull request's head at the time, `#23`, with local uncommitted state, so read this as a smoke test of the
  loop rather than a citable baseline; a clean re-run on the merged engine is the number to keep). 200
  matches of `Greedy` self-play recorded (`simulate --record`, base seed 1), a `train-value` and a
  `train-clone` policy trained on them, both evaluated against `Greedy` and `Random` on the 200 benchmark
  seeds, mirrored (seed set `733404048`).
- **Baselines** (match the committed digest and the L5/L6 journal entries, as expected since content and
  agents did not change): `Greedy` vs `Greedy` 39.25% each, 54.5% player 1 share, 21.5% draws, 22.0 rounds;
  `Greedy` vs `Random` and `Random` vs `Random` unchanged from before.
- **Clone policy**: 38.0% against `Greedy` (interval 34.7% to 41.3%, score 0.475) — statistically the same as
  `Greedy` playing itself (39.25%, interval 36.4% to 42.1%). This is the ceiling behaviour cloning is supposed
  to reach (`docs/learning/explained.md`: "the clone can never be better than what it copies") and it reached
  it: on 200 matches of `Greedy` self-play, the clone reproduced `Greedy`'s own strength almost exactly. 100%
  against `Random`.
- **Value policy — root cause found, with `policy.json` and `training.jsonl` in hand**: 0.0% against
  `Greedy` (0 wins, 0 draws, 400 matches), 99.5% against `Random`. `training.jsonl` shows why the fit itself
  is weak before the match numbers even come in: r² 0.216 and accuracy 25.7% on the 12,683 held-out steps
  (validated on one match in five, never trained on) against 51,023 training steps — a linear model over 383
  features explains barely a fifth of the return's variance. The evaluation's `spellUsage` shows what that
  training failure does at the table: against `Greedy` the policy casts `heavy_strike` 16 times against 9499
  `basic_attack`s (11996 actions total) — almost the mirror image of `Greedy`'s own play, which casts
  `heavy_strike` roughly 96% of the time (L5 journal entry). Against `Random` it is far more reasonable
  (10819 `heavy_strike` against 6193 `basic_attack`), so the policy is not broken in general, only around this
  one choice.
  The weights explain the "why": `intent:X:spell:basic_attack:v1` and `speed:X:Quick` carry the exact same
  bias for creature slots 0 and 1 (`-0.467` and `-0.228` respectively, to the last digit) and an extreme
  outlier for slot 2 (`-6.958` for `Quick`, `-6.355` for `basic_attack`, against a fallback of `0.012` and
  every other bias in roughly `[-0.6, 1.7]`). The identical biases are not a coincidence: `docs/learning/artifacts.md`
  already says the speed and intent sub-phases hand the agent the same board observation for a creature, and
  `Greedy` picks `basic_attack` (and, separately, `Standard`) far less often than `heavy_strike`/`Quick` — the
  same benchmark showed a 578-versus-15039 split. A linear ridge regression over 383 features, fit per action
  key at the default `alpha=1.0`, overfits that thin, low-variance slice of `basic_attack` rows into a large
  negative coefficient; slot 2 happening to draw the worst luck of the three explains the outlier. The value
  agent then reads that coefficient at the table and avoids `heavy_strike` almost entirely.
  Concretely worth trying next: raise `--min-samples` well past the default 5 so a rare key like
  `basic_attack` falls back to the dataset-wide mean instead of fitting its own noisy row; raise the ridge
  `--alpha` (383 features per key is a lot to regularize at the default strength); and record more than 200
  matches so the rare choice gets enough support to fit honestly. None of these are engine bugs — the loop,
  the encoding, and the training code did exactly what they were asked to; the data given to them was small
  enough for the regression to memorize noise on one specific, rare action.
- **Why it matters**: the loop runs end to end, produces a report, and the two learners diverge exactly as
  the theory predicts — cloning is a safe, bounded reproduction of `Greedy` (and reached that bound), value
  regression is more ambitious and, on 200 matches, currently overfits one rare-but-important choice into a
  losing bot. This value policy is not committed under `models/`: on this evidence it is a "tune more" case,
  not a "keep" case, and a citable number needs a clean (non-dirty) engine commit besides. Next: retrain with
  a higher `--min-samples` and `--alpha` on a bigger recorded dataset, re-evaluate against `Greedy`, and only
  then decide whether to commit it with its evaluation.

## 2026-09-08. Greedy against Random, the first measured gap

- **What changed**: nothing; this is the first measurement across two agents on the same engine and content,
  run by the `Evaluate` workflow (engine `d25c67db04d1`, the merge commit of the L5 pull request, content
  `34c616d3…80d7`, schema `features:v1+31987e1de3a9`, the 200 benchmark seeds, stamped as seed set
  `733404048`), `Greedy` against `Random`, mirrored. The `Seed` the command prints at start is the session
  seed of the interactive commands, drawn at random when `--seed` is absent; an evaluation seeds every match
  from the seed file and never uses it.
- **Numbers**: 400 matches, Greedy wins 400 (100.0%, interval 100.0% to 100.0%), score 1.000, 24.5 health
  left on average, no draw, every match by elimination in 15.5 rounds on average, none by the round cap.
  Intent entropy 0.20 bits for Greedy against 4.07 for Random; fizzles 4.0% against 1.6%; crits 4.9% against
  5.2%. Greedy casts `spell:heavy_strike:v1` 17914 times and `spell:basic_attack:v1` 582 times; Random
  spreads its 12000 or so casts over all 36 spells, `spell:heavy_strike:v1`, `spell:wait:v1` and
  `spell:basic_attack:v1` leading at about 1850 each.
- **Why it matters**: the one-step lookahead beats a uniform policy without losing a match, so the baseline
  is a real bar for the learned agents (L6, L7) rather than a coin flip. The gap is so wide that it says
  little about the content: a stronger opponent than Random is needed to grade the heuristic itself, which
  is what the seed pairs against a tuned heuristic will provide. Greedy's higher fizzle rate most likely
  comes from its focus: several creatures aim at the same enemy, the first kill leaves the later actions
  without a target, and they fizzle. Random spreads its targets and rarely loses one.

## 2026-09-08. Greedy replaces Random as the benchmark baseline

- **What changed**: nothing in the engine or the content; the greedy and heuristic agents arrived (learning
  phase L5) and the benchmark now plays `Greedy` against `Greedy`, so the digest is regenerated once here.
  The previous Random digest for the same content hash is superseded, not comparable.
- **Digest**: `benchmarks/34c616d340ed0d4ac67b0874ce3ac1895c126509b94ebe2a9963dfff45af80d7.json`, the same
  content hash as before, played by `Greedy` against `Greedy` on the 200 benchmark seeds, mirrored, under the
  default rule set. Generated by CI on the L5 pull request (engine `24e35df135f7`).
- **Numbers**: 400 matches, player 1 wins 218, player 2 wins 96, 86 draws by mutual elimination, every match
  by elimination between rounds 19 and 23 (22.0 on average, none by the round cap). Per agent: 157 wins,
  39.3% win rate (36.4% to 42.1%), score 0.500, intent entropy 0.23 bits, 5.1% fizzles, 4.9% crits.
  `spell:heavy_strike:v1` is cast 15039 times against 578 `spell:basic_attack:v1`; no other spell is used.
- **Why it matters**: two identical greedy agents give player 1 a 54.5% win share against 24.0%, far outside
  the noise of 400 matches, where Random against Random showed no edge. The first mover wins the damage race
  when both sides play the same deterministic plan; a rule change that tempers it (initiative, pick order)
  now has a number to move. The low entropy and the two-spell repertoire say the current content offers the
  greedy scorer little to choose from. Greedy against Random is measured by the `Evaluate` workflow, not by
  this digest.

## 2026-09-08. First benchmark digest

- **What changed**: nothing in the engine or the content; the evaluation harness and the benchmark digest
  arrived (learning phase L4).
- **Digest**: `benchmarks/34c616d340ed0d4ac67b0874ce3ac1895c126509b94ebe2a9963dfff45af80d7.json`, the
  content of `data/` at that hash, played by `Random` against `Random` on the 200 benchmark seeds, mirrored,
  under the default rule set (team of three, two energy and two picks per round, thirty rounds, double damage
  on a crit). Generated by CI on the merge commit of the L4 pull request (engine `0554a2aa8fe3`).
- **Numbers**: 400 matches, player 1 wins 206, player 2 wins 194, no draw, every match by elimination.
  Read as a first-mover check: 51.5% for player 1 with two identical random agents, inside the noise of 400
  matches, and the mirrored pairs cancel it in every evaluation anyway.
- **Why it matters**: from this commit, any engine or content change that alters an outcome on these seeds
  fails CI until the digest is regenerated here on purpose. The greedy agent (L5) will replace the random
  baseline and regenerate it once.
