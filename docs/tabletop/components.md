# Components and print-and-play

Status: **Specification** (2026-09-14; brought up to the package model and to the Speed cards of Part 6,
question 14, 2026-09-23, to one package a Creature an opportunity the same day, to 30 Health the same
day again, to a 20-Round cap the same day once more, and to Stun immunity the same day; to the redesigned
`momentum`, 2026-09-25, and to tune run 11's two package bonuses the same day; to one Activation a slot,
2026-09-28; to the content of 2026-10-04: Blood Hunt, Ambush, Crash's stun, and the hand-tuned
amounts; to Regeneration printed as `Regen` on the card, 2026-10-04; to the content of 2026-10-05: Blood
Hunt on two enemies, and Death Wail's Bleed on its targets; to the content of 2026-10-06: Basic Attack
removed, two starting Spells; to the renamed packages and Spells the same day). Phase 3 of [plan.md](plan.md). It answers the **needs a component** rows of
[translation.md](translation.md) and specifies a generator. **The generator is specified, not implemented**: there is no `printshop/` directory, and nothing
under `tools/` or `scripts/` prints a sheet.

What is current, exactly:

- **Evolution is the package model.** A pick buys a whole Tier: every Spell in it and one initiative bonus
  ([ADR 0056](../adr/0056-a-pick-buys-a-package-every-other-round.md)). Two picks at Round 1 and every second
  Round after, each for a different Creature: a Creature buys at most one package an opportunity
  ([ADR 0066](../adr/0066-a-creature-buys-one-package-an-opportunity.md)). The packages are authored in
  `data/Tiers/` ([ADR 0057](../adr/0057-a-package-is-authored-not-derived.md)). No Spell has an initiative of
  its own, and no Spell card prints one
  ([ADR 0059](../adr/0059-retire-the-spell-initiative-the-package-pays-it-now.md)).
- **A timeline tie is rolled off on a d20** between the sides, and each Player orders their own tied Creatures
  ([ADR 0063](../adr/0063-an-initiative-tie-is-rolled-on-a-d20.md)). The Creature number breaks no tie. It
  names the Creature, and it fixes the order tied Creatures roll in.
- **Every count is read at content `3c9eb083`** (the second-last point below), and the schedule in
  `docs/tabletop/playtest.rules.json`. Re-run the commands when the hash moves. `3c9eb083` removed one Spell
  from `ad3e4d00`, and `ad3e4d00` changed two from `e6f72578`. A line that still names `e6f72578` or
  `ad3e4d00` was re-read at `3c9eb083` and did not move; the lines that moved name `3c9eb083`. Before
  `e6f72578` they were read at
  `4ab506fa`, except the ones `momentum`'s redesign and tune run 11 moved (read at `813bb91b`).
  `4ab506fa` is `4d7a841c` with a Creature's base Health at 30 rather than 20
  ([ADR 0068](../adr/0068-a-match-lasts-ten-to-fifteen-rounds.md)). What that moved is the Health rail
  ([1.3](#13-stat-markers-and-the-rails-they-ride), [3.1](#31-the-creature-board)) and the reach of the
  Bleed supply ([1.4](#14-condition-tokens)); no piece count moved.
- **A Match is 8 to 14 Rounds, and the table's Round cap is 20** (ADR 0086; the plan said 8 to 16). The
  Round track grew from 16 spaces to 20, and everything sized per Round was recomputed for 20: the pick marks
  ([3.6](#36-the-round-track)), the Energy rail and its chit ([1.7](#17-the-energy-track-what-ends-it)), the
  Base initiative tens rail ([3.4](#34-initiative-two-small-rails-instead-of-one-long-one)) and the cards a
  Match can put in hands ([1.1](#11-spell-cards-and-package-cards)). No piece count moved; the paper is 49.
- **A Stun ends, and leaves a Round of Stun immunity**
  ([ADR 0072](../adr/0072-a-creature-is-immune-to-stun-the-round-after-one.md)). A Stun on a Creature already
  stunned or immune to Stun is ignored, where it used to restart. The box gains **6 Immune tokens**
  ([1.5](#15-the-rest-of-the-pieces)) and the Cleanup one swap ([3.2](#32-the-condition-dock-and-the-countdown)).
  The token pieces go from 260 to 266, which the same 2 token sheets hold; the paper stays 49.
- **`momentum` is a free strike, and no card places an Energy regeneration**
  ([ADR 0078](../adr/0078-momentum-is-a-free-strike-that-gathers-energy.md), content `813bb91b`, which is
  `0f036b75` with that one Spell changed). It costs 0, deals `Damage 2` to one enemy and gives its caster
  `Energy +2` in the same cast, where it gave itself 2 Energy a Round for 3 Rounds. No Spell authors an
  `EnergyRegeneration` now; the engine keeps the kind and its place in the Start passes, so the rule and the
  Round track's strip stay, but the box loses its **6 Energy regeneration tokens**
  ([1.4](#14-condition-tokens)). The Condition tokens go from 150 to 144 in 7 kinds, the token pieces from
  266 to 260 on the same 2 sheets, and the paper stays 49. What else moved: the most Energy one Creature can
  gain in a Round ([1.7](#17-the-energy-track-what-ends-it)), the widest card line
  ([2.3](#23-the-measurement)), the Spells with a caster line ([2.1](#21-what-is-printed-and-where-it-comes-from),
  [2.4](#24-the-seven-that-need-a-second-sentence)), and Part 7. Those readings are at `813bb91b`.
- **Tune run 11 moved two package bonuses**, Warped from +2 to +3 and Tyrant from +4 to +3 (PR #208,
  journal 2026-09-24; it is what lies between `4ab506fa` and `0f036b75`). The bonuses still sum to 47 and
  still run 0 to 5, but the 10 packages one Creature can own now pay 29, not 28. So the Base initiative
  ceiling goes from 33 to **34** and the Current initiative ceiling from 39 to **40**
  ([3.4](#34-initiative-two-small-rails-instead-of-one-long-one)), read at `813bb91b`. **No piece count
  moves and no rail is reprinted**: the Base initiative rails read 0 to 39 and still cover 34, and Current
  initiative is on no rail. What else moved is text: [1.1](#11-spell-cards-and-package-cards)'s command
  output, [1.3](#13-stat-markers-and-the-rails-they-ride), the rejected value track of
  [3.5](#35-the-initiative-track) (41 cells, not 40) and Part 6, question 11; the package card's
  measurement ([4.1](#41-the-package-card)) was re-run and did not move.
- **An action resolves as soon as its targets are confirmed**
  ([ADR 0083](../adr/0083-an-action-resolves-when-its-targets-are-confirmed.md)). Combat walks the timeline
  once: at each slot the card is flipped, its targets are named on the board as it stands, and it resolves
  before the next slot comes up. No cast's targets wait on the table while another is chosen, so the
  **18 target markers and the `Targeted by` row are retired** ([1.5](#15-the-rest-of-the-pieces),
  [3.1](#31-the-creature-board), [3.7](#37-the-player-area-and-where-a-face-down-intent-sits),
  [3.8](#38-how-a-cast-is-declared-and-resolved-in-components)). A target is named by pointing at its board and
  saying its number. The token pieces go from 260 to **242** on the same 2 sheets, and the paper stays 49. No
  other count moves: the dice, the Condition supply and the Immune tokens are sized per slot or per Creature,
  and a Round still has at most six slots. A Match can now end on any slot, which no component has to show.
- **The content of 2026-10-04, `e6f72578`.** `night_raid` replaces `death_squad` (Damage 3 and Energy -2 on
  up to 3 enemies), `ambush` replaces `shadowstep` (Damage 8, and an Initiative debuff of 5 for a Round on
  its own caster), `protective_slam` stuns for a Round instead of lowering Initiative, and many amounts were
  tuned by hand (journal, 2026-10-04). Re-reading every count at this hash also caught a drift older than it:
  this document had not read the nine level-2 Spells of PR #245, nor its reworked `meteor`, `ice_spear` and
  `tranquilizer_dart`. What moved: the catalogue is **45** Spells, so **270 Spell cards** on 30 sheets, not
  216 on 24, and the paper is **55** sheets, not 49 ([1.1](#11-spell-cards-and-package-cards)). **No Spell
  places an Initiative buff**, so its 18 tokens leave the box, as the Energy regeneration ones did; the
  Regeneration faces grow from 1 to 3. The Condition tokens go from 144 in 7 kinds to **150 in 6**, the token
  pieces from 242 to **248**, still on 2 sheets ([1.4](#14-condition-tokens)). The Base initiative ceiling
  goes from 34 to **35**, and the Current initiative ceiling from 40 down to the same **35**
  ([3.4](#34-initiative-two-small-rails-instead-of-one-long-one)); no rail moves. `latch`'s
  `Caster: Regeneration 1 a round, 3 rounds` was 40 characters and wrapped, so the body box took a fifth
  line, until the maintainer's decision of the same day that the card body prints the Condition as `Regen`
  ([2.2](#22-the-words)): `Caster: Regen 1 a round, 3 rounds` is 33, no line wraps, and the box is 4
  lines again ([2.3](#23-the-measurement)). And the largest Damage is 11, past the 10 the Defense rails were sized on
  (Part 6, question 16).
- **The content of 2026-10-05, `ad3e4d00`.** `night_raid` reaches up to 2 enemies, not 3, deals Damage 4, not
  3, and drains 3 Energy, not 2; it still costs 3 and rolls no critical. `crazed_specter` deals Damage 4, not 9,
  places a Bleed of 4 a Round for 2 Rounds on each target, which it did not, and rolls no critical, where it
  printed 0.38; it still reaches up to 3 enemies, costs 3, and bleeds its caster 4 for a Round. What moved:
  a cast of `crazed_specter` places 4 Bleed-4 tokens, 3 on its targets and 1 on its caster, so the Bleed-4
  supply goes from 6 to **24** (6 slots x 4), the Condition tokens from 150 to **168**, still in 6 kinds, and
  the token pieces from 248 to **266**, still on 2 sheets; the paper stays 55 ([1.4](#14-condition-tokens)).
  Its card body goes from 3 lines to 4, inside the 4-line box, and no line wraps ([2.3](#23-the-measurement),
  [2.4](#24-the-seven-that-need-a-second-sentence)). 24 Spells roll, not 25, so a d20 moves 7 of them, not 8,
  and the critical chance knob is declared on 25 Spells, not 26 ([1.6](#16-dice)). `night_raid` moves no
  count: its Damage and its drain move rails already on the board ([1.7](#17-the-energy-track-what-ends-it)).
- **The content of 2026-10-06, `3c9eb083`.** `basic_attack` is gone (cost 1, `Damage 2` on one enemy, no
  critical). Every Creature starts with `heavy_strike` and `wait`. What moved: the catalogue is **44** Spells,
  2 starting and 42 taught, so **264 Spell cards**, not 270. 264 / 9 is 29 full sheets and one holding 3, so
  still **30** sheets, and the paper stays 55 ([1.1](#11-spell-cards-and-package-cards)). A setup puts 12
  cards in hands, not 18, and a 20-Round Match at most 92, not 98. Part 6, question 2's lighter deck is 96
  cards on 11 sheets, not 102 on 12, and its index deck 44 cards. 20 Spells never roll, not 21
  ([1.6](#16-dice)). `basic_attack` placed no token and had no critical chance knob, so no token, die, rail or
  knob count moves. Its card had 2 body lines, so the cards at 2 lines go from 15 to 14, and the body and
  statline medians from 40 and 68 to 41 and 68.5 ([2.3](#23-the-measurement)). The card faces' hash prefix
  is `3c9eb0`.
- **The names of 2026-10-06, `9419f935`.** Every package that moved and 40 of the 44 Spells take the display
  names of [package-renaming-plan.md](../domain/package-renaming-plan.md). Ids, files and play do not move
  (the benchmark digest is `3c9eb083`'s entry for entry), so every count read at `3c9eb083` holds. What reads a
  name was re-read at `9419f935`: the longest Spell card head line ([2.5](#25-three-card-faces-written-out)),
  the package card's widest line ([4.1](#41-the-package-card)), and the class on a Spell card, which now agrees
  with the package on none of the 42 taught Spells (Part 6, question 10). The card faces' hash prefix is
  `9419f9`. **This document names every package and Spell by its current name**, also where it reports a
  reading or an event from before `9419f935`; the ids did not move, so the same row is found at any earlier
  hash under the name the plan's table gives. Quoted command outputs are read at `9419f935`.

[Part 7](#part-7-coverage-the-needs-a-component-rows) answers translation.md's rows as its package re-audit
(phase 7 of [docs/domain/tier-evolution-plan.md](../domain/tier-evolution-plan.md)) reads on this branch.

## What this is

A manifest, a card face, a board and track layout, and the specification of the generator that prints them.
Every count has the rule it comes from beside it. No count is a round number someone liked: where a count
cannot be derived from the rule set or from the content, it is a question in [Part 6](#part-6-open-questions),
not a guess.

Two facts about the sources. `docs/domain/spells.md` is a historical record and has drifted from `data/`
(translation.md says 30 of 36 rows differ), so **every number here is computed from `data/` or from the code
that enforces the rule**, with the command beside it.
[ADR 0041](../adr/0041-a-condition-stacks-unless-it-is-a-stun.md) and
[ADR 0042](../adr/0042-a-creature-has-no-base-critical-chance.md) shipped in PR #76 and are now files here;
they say what the plan said they would, and what this document takes from them is unchanged: the Creature's
base Critical chance is zero (`data/Creatures/main.v1.json` reads `baseCriticalChance: 0`), and a Condition
stacks except a Stun, which refreshed until ADR 0072 made it ignored (above).

### How a count is marked

| Mark | Meaning |
| --- | --- |
| **RULE** | The count follows a rule of the game. It changes only if the rule changes. |
| **VALUE** | The count follows a `RuleSet` number or a content number a balancing pass may move. A move is a **reprint** of that component, never a redesign. |

A count is often both: 264 Spell cards is one card per Creature per Spell (RULE) times a team size of 3
(VALUE) times a catalogue of 44 (VALUE). Where that happens, the table names which input moves.

### What is given, and not decided here

From [plan.md](plan.md), phase 2 and the Decisions section:

- A **faithful port**. A rule that costs bookkeeping gets a component; nothing is dropped.
- A Match is **8 to 14 Rounds** (ADR 0086). The table's Round cap is **20**, so everything sized per Round
  is built for **20** and says so: a Match can run to its cap.
- Two Players, **three Creatures each**, all six from `data/Creatures/main.v1.json`.
- The Creature's base Critical chance is **zero**. A Spell's printed chance is the chance rolled. The 20
  Spells at zero never roll.
- A Condition **stacks**, except a Stun, which is **ignored** on a Creature already stunned or immune to Stun.
  One application is one token. A Stun that ends leaves its Creature immune to Stun for the next Round
  (ADR 0072).
- A critical is a **die roll** and the catalogue will be authored onto the die's grid. The die is a **d20**
  ([d20-criticals.md](d20-criticals.md), built on 2026-10-07 as ADR 0100: every chance is a twentieth, by
  rule); [Part 1.6](#16-dice) keeps what each candidate cost. A timeline tie between the sides is rolled on the same die (ADR 0063).
- Evolution buys **packages** (Tiers), two picks at Round 1 and every second Round after, and a package's
  prerequisites are the only rule for what a Creature may buy (ADR 0056). A Creature buys at most one
  package an opportunity, so the two picks go to two Creatures (ADR 0066).

The board this manifest is built on, and the commands that read it:

```bash
grep -n 'Default {' src/DownfallArena.Domain/Matches/RuleSet.cs   # new(3, 2, 2, 30, 2.0, 1, 2)
cat docs/tabletop/playtest.rules.json                             # the table's rule set: the same, with a 20-Round cap
cat data/Creatures/main.v1.json                                   # Health 30, Energy 0, Defense 0, Base initiative 5
find data/Spells -name '*.json' | wc -l                           # 44
ls data/Tiers/*.json | wc -l                                      # 21, none disabled
```

`RuleSet.Default` is 3 Creatures a Team, 2 Energy a Round, 2 Evolution picks an opportunity, the first
opportunity at Round 1 and one every 2 Rounds after it, a 30-Round cap, a critical multiplier of 2.0. The
table's rule set file (`table --rules`) has the same numbers and a 20-Round cap. The cap is the one value the
table replaces, and it is a setup: the Round track is built for 20, so a table may set any cap up to 20.

The 21 Tiers: 3 at level 1, 9 at level 2 and 9 at level 3, each with two Spells. They teach 42 Spells, each
exactly once. The other 2, `heavy_strike` and `wait`, are the starting kit, which no Tier teaches. It was 3
until 2026-10-06, with `basic_attack`.

---

## Part 1. What is in the box

Totals first, then the derivation of each line.

| Group | Pieces |
| --- | --- |
| Spell cards | 264 |
| Package cards | 126 |
| Speed cards | 12 |
| Boards and mats | 6 creature boards, 2 player mats, 1 initiative track, 1 round track |
| Condition tokens | 168 in 6 kinds |
| Markers and chits | 36 stat markers, 6 initiative markers, 6 tie order chits, 4 pick tokens, 2 round markers, 18 overflow chits, 6 Immune tokens, 20 blanks |
| Player aids | 2 |
| Dice | 2 d20 |
| Paper | about 55 A4 or Letter sheets |

The paper: 30 sheets of Spell cards, 14 of package cards and 2 of Speed cards (9 a sheet; the last Spell
sheet and the second Speed sheet hold 3 each), 3 of creature boards (2 a sheet), 2 player mats, 1 for the initiative and round tracks, 2 of
tokens (266 pieces, none over 15 mm, and about 185 to a sheet at 15 mm), 1 of player aids. 55. Card backs would
add 46 more; see Part 6, question 8.

What moved when evolution became packages, and why:

| Component | Before | Now | Why |
| --- | --- | --- | --- |
| Talent tree mat | 2 | **0** | The talent tree gates nothing (ADR 0056, ADR 0058). A mat that shows its gates would teach a rule the game does not have. |
| Package card | - | **126** | translation.md's verdict on "A pick buys a whole Tier": Tier cards, 21 kinds. A bought card lies face up with its Creature and is the public record of what it owns. [1.1](#11-spell-cards-and-package-cards), [Part 4](#part-4-the-packages-as-an-object). |
| Talent pips | 82 | **0** | The package card is the record, so nothing is marked on a mat. |
| Paper | 35 sheets | **47** | 14 sheets of package cards in, 2 talent tree mats out. |
| Pick tokens | 4 | **4** | Still 2 a Player, but only in a Round that offers an opportunity. Since ADR 0066 a token that buys lies on the buyer's board until the Sub-phase ends; one token marks one Creature, so the count holds. [1.5](#15-the-rest-of-the-pieces). |
| Round track | 16 spaces | **20** spaces, **10 pick marks** | The schedule is printed where a Player looks for the Round. [3.6](#36-the-round-track). 16 spaces and 8 marks until the cap went to 20. |
| Base initiative, tens rail | 0 to 5 | **0 to 3** | In 16 Rounds the ceiling fell from 52 to 44 with packages, and to 29 when a Creature could buy only one an opportunity (ADR 0066): a rail to 2. The 20-Round cap gives a Creature 10 purchases, and the ceiling was 33; tune run 11 made it 34, inside the same rail. [3.4](#34-initiative-two-small-rails-instead-of-one-long-one), and Part 6, question 11. |
| Spell card foot | `Unlock: +N initiative`, `Requires: ...` | **neither** | ADR 0059 and ADR 0056. [2.1](#21-what-is-printed-and-where-it-comes-from). |
| Spell card head | class and tree depth | **the package that teaches it, and its level** | The class names collide with the package names, and the tree depth is a number the game no longer reads (ADR 0058). [2.1](#21-what-is-printed-and-where-it-comes-from). |
| Tie order chit | - | **6** | The Tie order is hidden until both Players have given theirs (ADR 0063). [1.5](#15-the-rest-of-the-pieces). |
| Condition tokens | 144 | **150** | Content, not packages: `ice_spear` lowered Initiative by 1 then, a new face. [1.4](#14-condition-tokens). |

That table records the package change, and its paper line stops at 47. The maintainer's answer to Part 6,
question 14 moved three lines after it: the 6 two-sided Speed tokens became **12 Speed cards**
([1.5](#15-the-rest-of-the-pieces), [2.6](#26-the-speed-card)), the token pieces went from 266 to 260, and the
paper from 47 to **49** sheets. The 20-Round cap moved two cells of it, the Round track's and the tens rail's,
and no count: the Round track still shares one sheet with the initiative track ([3.6](#36-the-round-track)).
ADR 0072 moved one count after that: **6 Immune tokens**, so the token pieces went from 260 to **266**, still
on 2 sheets, and the paper stays 49 ([1.5](#15-the-rest-of-the-pieces)). ADR 0078 took one count out: no
Spell places an Energy regeneration, so its **6 tokens** leave the box, the Condition tokens go from 150 to
**144** and the token pieces from 266 back to **260**, and the paper stays 49 ([1.4](#14-condition-tokens)).
ADR 0083 took another out: an action resolves before the next slot is targeted, so the **18 target markers**
leave the box, the token pieces go from 260 to **242**, still on 2 sheets, and the paper stays 49
([1.5](#15-the-rest-of-the-pieces)). The content of 2026-10-04 moved three: 45 Spells make **270** Spell
cards and the paper **55** sheets ([1.1](#11-spell-cards-and-package-cards)), and the Condition tokens go from
144 to **150**, so the token pieces from 242 to **248**, still on 2 sheets ([1.4](#14-condition-tokens)).
The content of 2026-10-05 moved one: `crazed_specter` places a Bleed of 4 on its targets as well as on its
caster, so the Condition tokens go from 150 to **168** and the token pieces from 248 to **266**, still on 2
sheets, and the paper stays 55 ([1.4](#14-condition-tokens)). The content of 2026-10-06 moved one more:
`basic_attack` is gone, so 44 Spells make **264** Spell cards, still on 30 sheets, and the paper stays 55
([1.1](#11-spell-cards-and-package-cards)).

### 1.1 Spell cards and package cards

| Component | Count | The rule beside the count | Follows |
| --- | --- | --- | --- |
| Spell card | **264** = 44 Spells x 6 copies | A card in a hand is what lets an Intent be played face down, so a Creature needs its own copy of every Spell it knows. Any of the six Creatures can come to know any Spell a Tier teaches: a package's prerequisites are the only rule, so multiclassing is free (ADR 0056), and both Players play the same Creature definition. Six copies is the ceiling. A Spell that is neither in the starting kit nor taught by an enabled Tier can never be known, and gets **no** copy; at `3c9eb083` there is none, so all 44 are printed. 264 is 30 sheets, the last holding 3 (270, exactly 30, until 2026-10-06). | 44 is a **VALUE** (content: 2 starting, 42 taught; 3 and 42 until 2026-10-06); 6 is 2 Players x team size 3, a **VALUE** (`RuleSet.TeamSize`); one copy per Creature that could know it is a **RULE** |
| Package card | **126** = 21 Tiers x 6 copies | A bought card lies face up with the Creature that bought it: that is the public record that it owns the Tier (rulebook §5.3). So a Creature needs its own copy of every Tier it owns. Any of the six Creatures may buy any Tier, since prerequisites are the only rule (ADR 0056), and the ceiling is reachable: a level-3 Tier costs a Creature 3 purchases at 3 opportunities, 9 for a whole Team, inside the 20 a Player makes in 20 Rounds. So all six Creatures can own the same Tier in one Match. 126 is exactly 14 sheets. | 21 is a **VALUE** (content, enabled Tiers); 6 is 2 Players x team size, a **VALUE**; one copy per Creature that could own it is a **RULE** |

What a Match actually consumes is smaller, and it is the number the open question in Part 6 is about. The
command below also gives the Base initiative ceiling that
[3.4](#34-initiative-two-small-rails-instead-of-one-long-one) uses. It tries every set of packages one
Creature can own (prerequisites included, 2^21 sets, a few seconds):

```bash
python3 -c "
import json,glob
R=json.load(open('docs/tabletop/playtest.rules.json'))
T=[json.load(open(p)) for p in glob.glob('data/Tiers/*.json')];T=[t for t in T if t.get('enabled',True)]
o=[r for r in range(1,21) if r>=R['firstEvolutionRound'] and (r-R['firstEvolutionRound'])%R['evolutionInterval']==0]
P=R['evolutionPicksPerOpportunity']*len(o);C=len(o);ix={t['id']:i for i,t in enumerate(T)};n=len(T)
need=[sum(1<<ix[q] for q in t['prerequisites']) for t in T];bb={};bs={}
for m in range(1<<n):
  own=[i for i in range(n) if m>>i&1]
  if any(need[i]&~m for i in own): continue
  k=len(own);bb[k]=max(bb.get(k,0),sum(T[i]['initiativeBonus'] for i in own));bs[k]=max(bs.get(k,0),len({s for i in own for s in T[i]['spells']}))
f=lambda d,k:max(v for j,v in d.items() if j<=k)
print('opportunities',o,'picks a Player',P,'purchases one Creature',C)   # one package a Creature an opportunity
print('most Spells a Player adds',max(f(bs,a)+f(bs,b)+f(bs,P-a-b) for a in range(C+1) for b in range(C+1) if 0<=P-a-b<=C))   # a Team of 3
print('most Base initiative one Creature buys',f(bb,C))"
# opportunities [1, 3, 5, 7, 9, 11, 13, 15, 17, 19] picks a Player 20 purchases one Creature 10
# most Spells a Player adds 40
# most Base initiative one Creature buys 30
```

That output is read at `e6f72578`. At `813bb91b` the last two lines read 34 and 29, and at `4ab506fa` the
last read 28: tune run 11's Warped +3 and Tyrant +3 moved it to 29. Since then the level-2 packages
gained their second Spell (PR #245), which moved the first line, and several bonuses moved; the last of them,
Blighted's from 2 to 3 on 2026-10-04, took the second line from 29 to 30.

6 Creatures x 2 starting Spells = 12 cards in hands at setup (18 while the starting kit was 3 Spells, until
2026-10-06). The command reads the Rounds 1 to 20, the
Round track's spaces. A 20-Round Match offers 10 opportunities, so a Player makes at most 20 purchases, and one
Creature at most 10 of them, one an opportunity (ADR 0066). The most Spells 20 purchases add is 40: every
package teaches two Spells, no Spell is taught by two packages, and a Creature cannot buy a package twice, so
every purchase adds two Spells to its Creature's hand. So **at most 12 + 2 x 40 = 92 cards are in hands in a
20-Round Match**. It was 98 with three starting Spells; at `813bb91b` it was 86, while the level-2 packages
taught one Spell each; in 16 Rounds it was 74. The box still carries 264 because which 92 is a choice the
Players make, and six Creatures may all
buy the same package. The same holds for package cards: a Match lays out at most 2 x 20 = 40 of the 126,
one a purchase.

### 1.2 Boards and mats

| Component | Count | The rule beside the count | Follows |
| --- | --- | --- | --- |
| Creature board | **6** | One per Creature in play: 2 Players x `RuleSet.TeamSize` 3. Each carries the Creature's number, 1 to 6: who it is on the track and when a cast names its targets, and the order tied Creatures roll in. The number breaks no tie (ADR 0063). | **VALUE** (team size) |
| Player area mat | **2** | One per Player. A Match seats exactly two. | **RULE** |
| Initiative track | **1** | Six ordered slots, a Quick band above a Standard band. Six is the number of Activation slots a Round can have: one per living, unstunned Creature. | **VALUE** (team size) |
| Round track | **1**, 20 spaces, 10 pick marks | A Match is 8 to 14 Rounds (given, ADR 0086), and the table's Round cap is 20 (`playtest.rules.json`). The track is printed to the cap, so the Round cap marker always has its space. The Round cap marker is placed on the space equal to the `RuleSet`'s cap at setup. A pick mark is printed on every Round that offers an opportunity: Round 1 and every second Round after, so 1, 3, ..., 19 (`RuleSet.IsEvolutionRound`). | The 20 spaces, the cap marker and the pick marks are **VALUE**s (the table's cap; the cap; `FirstEvolutionRound`, `EvolutionInterval`) |

### 1.3 Stat markers and the rails they ride

One marker per rail per Creature. The rails are specified in [Part 3](#part-3-boards-and-tracks); here are the
counts and the ends.

| Rail | Markers | Where it ends, and why | Follows |
| --- | --- | --- | --- |
| Health | 6 | 0 to 30, in two rows. `baseHealth` is 30 (ADR 0068; it was 20) and `Creature.Heal` clamps to `MaxHealth - Health` (`Creature.cs:271`), so nothing goes above it. One row of 31 cells is 155 mm, past the 95 mm a board row holds ([3.4](#34-initiative-two-small-rails-instead-of-one-long-one)), so it runs 0 to 15 and 16 to 30. | **VALUE** (`baseHealth`) |
| Energy | 6 | 0 to 40, in three rows: 0 to 13, 14 to 27, 28 to 40. See [1.7](#17-the-energy-track-what-ends-it). 41 cells in two rows would be 21 in one, 105 mm, past the 95 mm a board row holds, so it takes a third. | **VALUE** (`EnergyPerRound`) x **VALUE** (the 20-Round cap) |
| Defense buffs | 6 | 0 to 20. See [3.3](#33-defense-two-rails-because-the-floor-is-applied-once). | **VALUE** (the largest Damage, the critical multiplier) |
| Defense debuffs | 6 | 0 to 20, the same reason mirrored. | **VALUE** |
| Base initiative, units | 6 | 0 to 9. | **RULE** (a decimal rail) |
| Base initiative, tens | 6 | 0 to 3. Together the two rails read 0 to 39, which covers the ceiling computed in [3.4](#34-initiative-two-small-rails-instead-of-one-long-one): a Base initiative of 35 at `e6f72578`. | **VALUE** (the packages' `initiativeBonus`, the schedule) x **RULE** (one package a Creature an opportunity, ADR 0066) |

**36 stat markers**, six of each of the six rails above. Print them as 10mm discs in six Creature colours.

### 1.4 Condition tokens

Permanent Conditions need **no token**: a permanent Defense change moves the Defense rail and is discarded,
because it never expires and never has to be undone. That is what keeps the unbounded stack of ADR candidate 3
off the token supply. Tokens exist for **timed** Conditions, whose expiry has to be remembered.

The supply rule, one rule for every kind: **the most tokens of that face that one Round of six Activation
slots can place.** Computed from the catalogue:

```bash
python3 -c "
import json,glob,collections
S=[json.load(open(p)) for p in glob.glob('data/Spells/**/*.json',recursive=True)]
LAST={'Bleed','Regeneration','EnergyRegeneration','Stun','DefenseBuff','DefenseDebuff','InitiativeBuff','InitiativeDebuff'}
for s in S:
  for where,es in (('target',s['effects']),('caster',s.get('casterEffects',[]))):
    for e in es:
      if e['kind'] in LAST:
        print(s['id'].split(':')[1], where, e['kind'], e.get('amount',e.get('amountPerRound')),
              'permanent' if e.get('permanent') else e.get('durationRounds'),
              s['targeting']['origin'], s['targeting']['maxTargets'])"
```

| Token | Face | Supply | The rule beside the count | Follows |
| --- | --- | --- | --- | --- |
| Bleed | 1 a Round | 6 | `latch` and `tranquilizer_dart` on one enemy, `bone_ward` on its own caster; one each; 6 slots x 1 | **VALUE** (content) |
| Bleed | 2 a Round | 18 | `summon_minions` and `meteor`, each on up to 3 targets; 6 slots x 3 = 18 | **VALUE** (content) |
| Bleed | 3 a Round | 18 | `toxic_waves`, up to 3 targets (`poison_slash` places one); 6 slots x 3 = 18 | **VALUE** |
| Bleed | 4 a Round | 24 | `crazed_specter` on up to 3 enemies and on its own caster, 4 a cast; `mortal_wound` on a target and `revenant_guards` on its own caster place one each; 6 slots x 4 = 24 | **VALUE** |
| Regeneration | 1 a Round | 6 | `latch` on its own caster; 6 slots x 1 | **VALUE** |
| Regeneration | 2 a Round | 18 | `soothing_chant`, up to 3 allies; 6 slots x 3 | **VALUE** |
| Regeneration | 3 a Round | 6 | `healing_screech`, one ally; 6 slots x 1 | **VALUE** |
| Stun | - | 12 | A Stun on a Creature already stunned is **ignored** (ADR 0072), so a Creature carries at most one, ever — but one Stun needs **two** tokens at once: one sits in the Speed slot so no Speed card can go there ([3.1](#31-the-creature-board)), and one counts the Duration down in the dock ([3.2](#32-the-condition-dock-and-the-countdown)). A token cannot be in two places. Two per Creature. The Round of Stun immunity after it is not a Condition and has its own token ([1.5](#15-the-rest-of-the-pieces)). | **RULE** (a Stun on a stunned Creature is ignored, and the two places a Stun is shown) x **VALUE** (team size) |
| Defense buff | +1 | 6 | `guard`'s timed half, one ally; 6 slots x 1 | **VALUE** |
| Defense buff | +3 | 6 | `thundering_seal`'s timed half and `bone_ward`, one ally, and `shield_bash` on its own caster; one each; 6 x 1 | **VALUE** |
| Defense buff | +4 | 18 | `revenant_guards`' timed half, up to 3 allies; 6 x 3 | **VALUE** |
| Defense debuff | -2 | 18 | `noxious_cure` on up to 3 allies; 6 x 3. `psycho_rush`'s caster debuff is the same face. | **VALUE** |
| Initiative debuff | -3 | 6 | `frostbite`, one enemy; 6 x 1 | **VALUE** |
| Initiative debuff | -5 | 6 | `ambush` on its own caster; 6 x 1 | **VALUE** |
| **Total** | | **168** | | |

Every timed amount in the catalogue is on this list and no other: Bleed is 1, 2, 3 or 4; Regeneration is 1,
2 or 3; a timed Defense buff is 1, 3 or 4; every timed Defense debuff is 2; an Initiative debuff is 3 or 5.
No Spell places an Initiative buff. The one permanent Defense debuff, `infectious_blast`'s -3, moves the rail
and takes no token. That is why a token set this small covers a 44-Spell catalogue. At `813bb91b` this table
read 144 in 7 kinds; what moved it to 150 in 6 is content alone. The Bleed faces are re-dealt (6, 18, 18, 6
where they were 18, 18, 6, 6) and still total 48; the Regeneration 1 and 2 faces arrive with `latch` and
`soothing_chant` (+24); the timed Defense buffs are +1, +3 and +4 where they were +1, +2 and +3, and still
total 30; the Initiative debuffs are -3 and -5 where they were -1 and -2, and still total 12; and the
Initiative buff leaves (-18, below). At `ad3e4d00` it reads 168, still in 6 kinds, and content alone moved
it again: `crazed_specter` places its Bleed of 4 on up to 3 enemies for 2 Rounds as well as on its caster for
1, so the most one slot places on that face goes from 1 to 4, and the Bleed-4 supply from 6 to 24 (+18).

**No Energy regeneration token.** At `813bb91b`, and still at `e6f72578`, no Spell authors an
`EnergyRegeneration`, so the command above prints none and no cast can place one: `momentum`, the only Spell
that did, deals Damage and gives its caster Energy on the spot since ADR 0078. The 6 tokens at 2 a Round
(`momentum`, Self only, 6 slots x 1) left the box with it, and that is the whole of the change from 150 to
144. The engine still has the kind and still ticks it first at the Start of a Round, so the rule stays in the rulebook and on the Round track; a
Spell that authored one again would bring the face back, sized by the same rule, as a reprint of the token
sheet. **VALUE** (content).

**No Initiative buff token**, by the same rule. At `e6f72578` no Spell authors an `InitiativeBuff`, so the
command above prints none and no cast can place one: `death_squad`, which placed one on up to 3 allies, is
gone (`night_raid` took its place in Deathstalker, and deals Damage and drains Energy), and `ambush`, which
took `shadowstep`'s, lowers its own caster's Initiative where `shadowstep` raised it. The 18 tokens at +2
(`death_squad`, 6 slots x 3) leave the box. The engine still has the kind and still adds it into Current
initiative ([3.4](#34-initiative-two-small-rails-instead-of-one-long-one)), so the rule stays in the
rulebook; a Spell that authored one again would bring the face back, sized by the same rule, as a reprint of
the token sheet. **VALUE** (content).

**The supply is one Round at the maximum rate, and Durations run to 3.** A Bleed from `summon_minions` lives 3
Rounds, so the rule's own ceiling is three times the table above for that face: 54 Bleed-2 tokens. To reach
it, all six Creatures cast `summon_minions` on all three enemies three Rounds running, and each Creature ends
up carrying 9 Bleeds of 2. By the third cast each has taken 6 + 12 in ticks and 6 from its own `Caster:`
lines: 24, and Bleed ignores Defense. At 20 Health, with nothing healed, that killed every Creature before
the third cast, so the ceiling was unreachable. **At 30 Health it is reachable**, but only if both Players
play that line together, and then every Creature dies to the next Round's 18 in ticks. The engine has no
cap either way, so the rulebook carries the standard supply escape: **a supply that runs out is replaced by a
blank token with the value written on it; the game has no maximum.** 20 blanks are in the box for that, which
is 16 short of that ceiling. Part 6, question 5.

**The Bleed-4 face reaches 48, over two Rounds** (`ad3e4d00`). All six Creatures cast `crazed_specter` on all
three enemies two Rounds running. A Condition stays until the Cleanup after its last countdown
(`Condition.Tick` skips the first one), so in the second Round each Creature carries the 3 target Bleeds of
the first, the first Round's caster Bleed, which leaves at that Round's Cleanup, and 4 new ones: 8 each, 48
in all, which is the face's supply times its longest Duration, 24 x 2. With no healing, it needs every
Creature at 2 Defense or more: by the end of the second Round each has taken 6 casts of `Damage 4` less its
Defense, and 16 in ticks, out of 30. A third Round cannot happen: its Start ticks 7 Bleeds of 4, 28, on
Creatures with at most 14 Health left, and nothing placed before the first of the two Rounds heals that much
then. 48 is 24 past the supply, and the 20 blanks cover 20 of them. That is a smaller gap than the Bleed-2 one
above, and the two cannot happen together: a slot casts one Spell.

### 1.5 The rest of the pieces

| Component | Count | The rule beside the count | Follows |
| --- | --- | --- | --- |
| Speed card, `Quick` or `Standard`, one common back, poker size | **12** = 6 per Player: a Quick and a Standard card for each Creature | One Speed choice per living, unstunned Creature (`SpeedRules.cs:13-45`), hidden until both Players have made theirs (`PlayerBoardStateProjection.cs:38`). A hidden choice of one of two needs both answers behind one back: the Player lays the chosen card face down in the Creature's Speed slot, keeps the other in hand, and both Players turn theirs together. Any Creature may take either Speed, and every Creature may take the same one, so a Player needs `RuleSet.TeamSize` cards of each: 2 Speeds x team size 3 x 2 Players. The Quick card carries the reminder that a Quick Creature rolls no critical that Round (`ResolutionRules.CriticalChanceOf`), since that cost is what makes the choice a choice. Size, back and face: [2.6](#26-the-speed-card). The maintainer's answer to Part 6, question 14. | **VALUE** (team size) x **RULE** (two Speeds, one hidden simultaneous choice) |
| Initiative marker, numbered 1 to 6 | **6** | One per Creature, placed on the initiative track. The number names the Creature on the track; ids are handed out in join order (`Match.Spawn`, `Match.cs:324-333`), so 1 to 3 are Player 1's. It breaks no tie: a tie between the sides is a d20 Roll-off, and tied Creatures roll in number order, which only fixes the order of the rolls (ADR 0063). | **VALUE** (team size) |
| Tie order chit, `1st`, `2nd`, `3rd`, one common back | **6** = 3 per Player | The Tie order is given by both Players at the same time and hidden until both are in (ADR 0063, "like a Speed choice"), so it needs something that commits face down. A Player lays one chit face down on each of their tied Creatures' boards, and both Players turn them together. A Player orders at most all of their own Creatures, `RuleSet.TeamSize` = 3; two separate ties are each read low number first, so 3 chits cover any Round. This is translation.md's smallest answer ("three ordinal chits a Player"). | **VALUE** (team size) x **RULE** (a hidden, simultaneous Tie order) |
| Evolution pick token | **4** | 2 per Player (`RuleSet.EvolutionPicksPerOpportunity`), put on the mat only in a Round with a pick mark on the Round track. A pick moves one from the mat onto the board of the Creature it was picked for, and it stays there until the Sub-phase ends: a Creature holding one has bought this opportunity and cannot be picked again (`Planning.CreatureAlreadyEvolved`, ADR 0066). A pass takes the tokens still on the mat off it; the end of the Sub-phase takes every token off the mats and the boards (rulebook §5.3). One token marks one Creature, and a Player's picks go to different Creatures, so 2 a Player still covers every opportunity. A Round with no opportunity gives nobody a pick (`RuleSet.EvolutionPicksIn`), so the tokens stay off the mat. | **VALUE** (picks an opportunity) |
| Round marker | **1** | One position on the Round track. | **RULE** |
| Round cap marker | **1** | Placed at setup on the space equal to the `RuleSet`'s Round cap, so the track's end is a component and not a memory. | **VALUE** |
| Target marker | **0**, retired by ADR 0083 (18 before it) | An action resolves as soon as its targets are confirmed, before the next slot comes up (`ActionRules.cs`, the `Activation` sub-phase, ADR 0083), so no cast's targets are still on the table while another's are chosen, and there is nothing for a marker to hold. The owner names each target by pointing at its board and saying its number. The 18 were 6 sets of 3 while every Intent was revealed and targeted before any resolved (`RevealAndTarget`, then `ActionResolution`), so that all six casts' targets could sit on the board at once. | **RULE** (an action resolves on confirmation) |
| Energy overflow chit, +40 | **6** | One per Creature. See [1.7](#17-the-energy-track-what-ends-it). | **RULE** |
| Defense overflow chit, +20 and -20 | **12** | Six of each. The Defense rails are bounded by what can matter, not by the rule. Buffs read at most 10 (ADR 0076) but the rail keeps the whole sum, and debuffs have no bound. | **RULE** (no bound exists) |
| Immune token, printed `Immune to Stun` | **6** | Stun immunity: a living Creature whose Stun ends at Cleanup is immune to Stun until the next Cleanup (`Creature.TickConditions`, `Creature.CanBeStunned`, ADR 0072). The Stun token leaving lane `1` is swapped for an Immune token in the same lane, so the next Cleanup's first move removes it and nobody counts ([3.2](#32-the-condition-dock-and-the-countdown)). A Creature carries at most one: it is immune only in the one Round after a Stun, and a Stun cannot land while it is. So one per Creature, 2 Players x team size 3. **Its own token, not the Stun token's back.** The print-and-play is single-sided (a blank back is the common back, [2.6](#26-the-speed-card) and Part 6, question 8), so an `Immune` back on the Stun token would be the only duplex print on the token sheets, for all 12 Stun tokens since any of them can be the one in the dock. Six more 15 mm pieces fit on the 2 token sheets already counted (266 of about 370), so they cost no paper; since ADR 0078 took out the 6 Energy regeneration tokens it is 260, and since ADR 0083 took out the 18 target markers, 242; the content of 2026-10-04 makes it 248, and that of 2026-10-05 makes it 266 again. | **RULE** (one Stun immunity a Creature at a time) x **VALUE** (team size) |
| Blank token | **20** | The supply escape of [1.4](#14-condition-tokens). | not derived; see Part 6, question 5 |
| Player aid | **2** | One a Player: the Round sequence, the timeline tiebreaks, the Condition timing, and the two orderings of [3.6](#36-the-round-track). Phase 4 writes what it says (plan.md); this manifest reserves the component and its sheet. | **RULE** |

### 1.6 Dice

One critical roll a cast (`ResolutionRules.cs:59`), at most 6 casts a Round, resolved one after the other in
timeline order. The Roll-off of a timeline tie uses the same die (ADR 0063): at most 6 tied Creatures, rolled
one after the other in number order, before any Intent. **One die is enough by the rule**, for both. Two are
in the box so each Player rolls their own casts and their own Creatures in a Roll-off, which is a convenience
and not a rule. The Roll-off adds no die and no component: its result is the Places on the initiative track.

Since the Creature's base chance is zero, only the Spells that print a chance roll at all:

```bash
python3 -c "
import json,glob,collections
v=collections.Counter(json.load(open(p))['criticalChance'] for p in glob.glob('data/Spells/**/*.json',recursive=True))
print(sorted(v.items()))"
# at b41ba55e (2026-10-07, ADR 0100): [(0, 20), (0.2, 1), (0.3, 2), (0.35, 6), (0.4, 2), (0.45, 2), (0.5, 7), (0.55, 1), (0.75, 2), (0.8, 1)]
# at 3c9eb083, before the snap: [(0, 20), (0.22, 1), (0.283, 1), (0.3, 1), (0.33, 3), (0.35, 3), (0.38, 1), (0.4, 1), (0.45, 2), (0.5, 7),
#  (0.55, 1), (0.75, 1), (0.767, 1), (0.8, 1)]
```

**24 of 44 Spells roll. 20 never touch a die.** Nine distinct chances are printed at content `b41ba55e`, every
one a whole number of twentieths (ADR 0100, 2026-10-07), so the d20 carries them all and moves none. Thirteen
were printed at content `3c9eb083`, and the die's grid had to carry them; the table below is that reading,
kept because it is what chose the die. It was 24 and 21 at `ad3e4d00`, until `basic_attack`, which printed 0,
left. It was 25 and 20 at `e6f72578`: `crazed_specter` printed 0.38 and prints 0 since 2026-10-05, and
`tornado` keeps 0.38 on the list. The table below is a reading, not a
constant — the maintainer is tuning, so re-run the command rather than trusting the cells. What each
candidate costs, snapping each of the 24 to the nearest face:

```bash
python3 -c "
import json,glob
r=[(json.load(open(p))['id'].split(':')[1],json.load(open(p))['criticalChance']) for p in glob.glob('data/Spells/**/*.json',recursive=True)]
r=[x for x in r if x[1]>0]
for d in (6,8,10,12,20,100):
  e=[(abs(round(c*d)/d-c),n) for n,c in r]
  print(d, sum(1 for x in e if x[0]>1e-9), round(max(e)[0],4), max(e)[1], round(sum(x[0] for x in e)/len(e),4))"
```

| Die | Spells whose chance moves | Worst move | Mean move | On the declared knob grid (step 0.05)? | What else it costs |
| --- | --- | --- | --- | --- | --- |
| d6 | 17 of 24 | 0.0833 | 0.0268 | **No.** 1/6 is not a multiple of 0.05 | Six faces for thirteen distinct chances, the largest mean error of the five, and a finest distinction of 16.7 points |
| d8 | 16 | 0.05 | 0.0238 | **No** | Two more moves than the d10 for the same worst move and a slightly larger mean, off the grid the d10 is on; and a die many households do not have |
| d10 | 14 | 0.05 | 0.0221 | **Yes**, 0.1 is a multiple of 0.05 | Ten faces for thirteen chances, and it cannot express 0.75, where `crushing_stomp` sits today |
| d12 | 16 | 0.0367 | 0.0150 | **No** | Values no declared knob can reach, and it no longer buys the smallest mean error either |
| **d20** | **7** | **0.02** (`tornado` 0.38 to 0.40, and four others by the same amount) | **0.0056** | **Yes**, exactly the declared step | One die, one reading, thresholds in whole numbers |
| d100 (2 dice) | 2 | 0.003 | 0.0002 | No, 0.01 | Two dice and a percentile read per cast, and it keeps the arbitrary precision fork B exists to remove |

**Settled: a d20** ([d20-criticals.md](d20-criticals.md)), **and the card prints both the chance and the
threshold** ("Critical 50% - d20: 11+",
which is seven Spells as the catalogue stands). The recommendation survives tune run 8, and on better terms
than it was made: the d20 now wins the error as well. It moves the fewest Spells (7 of 24 at `ad3e4d00`, 8 of
25 at `e6f72578`), has the smallest worst move (0.02) and, since run 8 pulled `crazed_specter` and
`protective_slam` off the d12 grid, the smallest mean error of the five single dice. `crazed_specter` rolls no
critical since 2026-10-05, and the d20 still has the smallest mean error without it. The load-bearing reason is still the grid rather than the error,
because the error moves with every tuning pass and the grid does not: `data/balance/knobs.json` already
declares `/criticalChance` a knob with a **step of 0.05 on 25 Spells**, so a d20 snap is inside the search
space a tuning pass already has, and d6, d8, d12 and d100 are not.

```bash
python3 -c "
import json,collections
k=json.load(open('data/balance/knobs.json'))['spells']
s=[(n,b) for n,e in k.items() for b in e.get('knobs',[]) if 'criticalChance' in b['path']]
print(len(s), collections.Counter(b['step'] for _,b in s))"   # 25 Counter({0.05: 25})
```

One of the 25 is `throwing_star`, which prints 0 today; the other 24 are every Spell that rolls. It was 26
until 2026-10-05, when `crazed_specter` lost its chance and its critical chance knob together.

One finding the maintainer owns before the snap is authored, not this document's to decide. A second, that
`revenant_guards` printed 0.33 and had no critical chance knob, is gone: it prints 0 since 2026-10-04.

- **Eight of the 25 knobbed Spells were off their own declared grid** until 2026-10-07, when ADR 0100 snapped
  the seven and moved the eighth's band floor to 0.15; `check-knobs` refuses a band off the twentieths since.
  Their printed value was not their band's `min` plus a whole number of steps. `pummel` 0.767, `protective_slam` 0.283, `tornado` 0.38,
  `engulfing_flames`, `noxious_cure` and `toxic_waves` at 0.33, `rejuvenate` 0.22, and `lightning_bolt` 0.5
  in a band of `[0.17, 0.8]`. Seven of the eight sit on a band whose `min` **is** a multiple of 0.05, so a d20
  snap fixes them outright; they are the 7 the d20 moves. The eighth is `lightning_bolt`, whose 0.5 is already
  a twentieth, but whose band floor 0.17 is the only knob band off its own grid, and it leaves the band itself
  to be moved: 0.17 plus multiples of 0.05 never lands on a multiple of 0.05. `crazed_specter`, at 0.38, was
  the ninth until 2026-10-05.

### 1.7 The energy track: what ends it

Energy has no maximum in the engine (`Energy.cs:3`, `GainEnergy` at `Creature.cs:279-290`), and the maintainer
has settled that the engine does not change: **the component is what ends it** (translation.md, ADR candidate
2).

**The track runs 0 to 40.** The rule beside the count: a living Creature gains `RuleSet.EnergyPerRound` = 2
every Round (`UpkeepRules.cs:13-22`), and a table's Match is at most 20 Rounds, its Round cap, so **2 x 20 = 40
is the Energy a Creature banks by doing nothing at all**. That is the gain no play can refuse, and it is the
honest end of a printed track. It ran to 32 while the track was built for 16 Rounds.

Seven Spells in the catalogue touch Energy. Five of them can push a Creature above 40, and all five cost a
cast:

```bash
python3 -c "
import json,glob
for p in glob.glob('data/Spells/**/*.json',recursive=True):
  d=json.load(open(p))
  for e in d['effects']+d.get('casterEffects',[]):
    if 'Energy' in e['kind']: print(d['id'].split(':')[1], e)"
# wait EnergyGain 2 | restorative_burst EnergyGain 2 | adrenaline_tonic EnergyGain 2 | momentum EnergyGain 2
# extort EnergyDrain 1, EnergyGain 1 | night_raid EnergyDrain 3 | soul_devourer EnergyDrain 3
```

`momentum`'s `EnergyGain` is its caster effect: it lands on the Creature that casts it, not on the enemy it
hits (ADR 0078). `extort`'s is too: it drains 1 from the enemy and gives 1 to its caster.

A drain only moves a marker down the same rail. `night_raid` takes 3 from each of up to 2 enemies since
2026-10-05 (2 from each of up to 3 before), more than the 2 a Round gives, and `soul_devourer` takes 3 from
one. A drain takes at most what the target has (`Creature.LoseEnergy`, ADR 0035), so a marker stops at 0 and
the rail needs no cell below it. One raid takes at most 6 Energy in all, 3 from each of 2, as it did before
at 2 from each of 3. No drain adds a component.

The most one Creature can gain in one Round is **8**: 2 from the Round, 4 from its two allies each casting
`restorative_burst` or `adrenaline_tonic` on it, and 2 from its own Activation slot, spent on `wait`, on
`momentum`, whose caster line gives the same 2, or on `adrenaline_tonic` with itself among its allies. A
Creature has one slot, so it is one of them. Nothing reaches it from earlier
Rounds any more: it was 14 while `momentum` placed an Energy regeneration on its own caster, 2 a Round for 3
Rounds, and three of those overlapping added 6.

**What a player does at the end of the track.** A Creature whose Energy would pass 40 takes an **Energy
overflow chit** worth 40 and its marker returns to 0. One chit per Creature is in the box: a second chit means
a Creature banking more than 80 Energy in 20 Rounds, which is an average of 4 a Round with nothing ever spent
while the most expensive Spell in the catalogue costs 4. If it happens, the blank tokens of
[1.4](#14-condition-tokens) cover it. The rulebook says the rule plainly: **Energy has no maximum; the track
is a track, not a cap.**

---

## Part 2. The spell card face

### 2.1 What is printed, and where it comes from

Everything needed to resolve a cast without the rulebook. Each line names the field in
`data/dst/game.schema.json` it is generated from.

| Zone | Line | From | Why it is on the card |
| --- | --- | --- | --- |
| Head | Name | `name` | |
| Head | Energy cost, as a numeral in a filled circle | `energyCost` | An Intent is only legal if the Creature can afford it (`IntentRules.cs:50-69`), checked against a public Energy rail |
| Head | Every package that teaches it, with its level: `Revenant . level 3`, or `Starting spell` | the enabled `tiers[]` whose `spells` name it; `creatures[].startingSpellIds` | Where the card is filed in the library, and which purchases bring it to a hand. It is not a gate: the package's gate is printed once, on its package card. A starting Spell belongs to no package and sits at level 0 (ADR 0058). A Spell two packages teach is still one face: the head lists them all, lowest level first and then by name, joined by ` / ` (`Starting spell` first when it is one too), and the card is filed under the first. At `e6f72578` every head names one |
| Body | Targeting, one line | `targeting.origin`, `scope`, `maxTargets` | Origin, scope and count are one sentence: `Self`, `One enemy`, `One ally`, `Up to 2 enemies`, `Up to 3 allies` |
| Body | One line per effect, with its amount and Duration | `effects[]` | |
| Body | One line per caster effect, prefixed `Caster:` and set below a rule | `casterEffects[]` | ADR 0031: once per cast, never multiplied, none of them on a Fizzle. Fourteen Spells carry one at `e6f72578` (eight at `813bb91b`; seven until ADR 0078 gave `momentum` its `Caster: Energy +2`), and it must not read as a target effect |
| Foot | Critical chance, as a percentage, and the d20 threshold when the chance is a whole number of twentieths | `criticalChance` | The printed chance is the chance rolled (ADR 0042). A chance off the twentieths prints no threshold rather than a rounded one ([d20-criticals.md](d20-criticals.md)) |
| Foot | Content hash, first 6 characters, and the Spell's versioned id | the build | A deck from two content hashes is a broken deck; see [Part 5](#part-5-the-generator-specified) |

**No Spell card prints an initiative, and none prints a prerequisite.** The two foot lines this face used to
carry, `Unlock: +N initiative` and `Requires: ...`, described rules the engine no longer applies. A Spell has
no initiative (ADR 0059: the field is gone from the schema, and a spell file that carries it is refused at the
build). What a Creature may buy is decided by package prerequisites alone, and the talent tree gates nothing
(ADR 0056). Both belong to the package: its bonus and its prerequisites are printed once, on its package
card ([Part 4](#part-4-the-packages-as-an-object)). A table plays what its cards say, so a card that
kept printing them would teach two rules the game does not have. The table app's card face says the same
(`CardFace`, `table/card.js`).

The head used to print the Spell's `creatureClass`. It does not: the class names are the talent tree's, the
package names are the Tiers', and at `9419f935` they agree on none of the 42 taught Spells. Until the
renaming of 2026-10-06 some collided: `tornado` was authored under the class `Berserker`, and the package then
named `Berserker` did not teach it (`Ravager` does). A Spell card and a package card that print the same word
must mean the same package. The table app still prints the class; Part 6, question 10.

Two rules that are **not** printed per card because they are true of every card, and belong on the player aid:
a Multi Spell may take fewer targets than its maximum (`TargetingRules.cs:49`), and `Ally` includes the caster
(`TargetingRules.cs:43`). Printing either on 44 cards costs a line each and teaches neither.

### 2.2 The words

The effect lines use the glossary's terms unchanged, with one abbreviation: `Damage`, `Heal`, `Energy`,
`Bleed`, `Regen`, `Energy regeneration`, `Stun`, `Defense`, `Initiative`, `Caster`, `permanent`. A per-Round
effect reads "N a round", which is the glossary's phrasing for a Bleed tick. A Duration reads "N rounds" or
"permanent", which is the Duration entry's own vocabulary.

**The card body prints the Regeneration Condition as `Regen`** (the maintainer's decision, 2026-10-04), on
every card that places one: at `e6f72578`, `latch` (`Caster: Regen 1 a round, 3 rounds`), `soothing_chant`
(`Regen 2 a round, 2 rounds`) and `healing_screech` (`Regen 3 a round, 2 rounds`). The constraint it answers
is the line: with the full word, `latch`'s caster line is 40 characters, two over the 38 a line holds at 8 pt
([2.3](#23-the-measurement)), so it wrapped and the body box needed a fifth line for one card. Abbreviated it is
33, every body line in the catalogue is within 38, and no line wraps. The abbreviation is the card body's
alone: the Condition is still `Regeneration` in the rules, the glossary, the rulebook and on its token
([1.4](#14-condition-tokens)), where the room is not a 38-character line. The generator applies it when it
renders the effect line, so it is part of the spec and not a hand edit. The table app's screen face
(`EffectLine.cs`) prints the full word, because a screen line is not 53.5 mm at 8 pt; Part 6, question 12.

| Effect kind | Printed as |
| --- | --- |
| `Damage` | `Damage 7` |
| `Heal` | `Heal 4` |
| `EnergyGain` / `EnergyDrain` | `Energy +2` / `Energy -2` |
| `Bleed` | `Bleed 4 a round, 2 rounds` |
| `Regeneration` | `Regen 3 a round, 2 rounds` (the abbreviation above; the full word was `Regeneration 3 a round, 2 rounds`) |
| `EnergyRegeneration` | `Energy regeneration 2 a round, 3 rounds` (no card prints it at `813bb91b` or since; the line stays because the schema still admits the kind, and the generator prints what the build hands it). It is 39 characters, so a Spell that authored one again would wrap and fail the build ([5.6](#56-how-it-is-tested)); whether it then prints `Energy regen` is the maintainer's to decide, not this document's. |
| `Stun` | `Stun, 2 rounds` |
| `DefenseBuff` / `DefenseDebuff` | `Defense +3, permanent` / `Defense -2, 1 round` |
| `InitiativeBuff` / `InitiativeDebuff` | `Initiative +2, 1 round` / `Initiative -3, 1 round` (no card prints an Initiative buff at `e6f72578`; the line stays for the same reason) |

### 2.3 The measurement

The card is a **standard poker card, 63.5 x 88.9 mm**. Which print constraint each choice answers:

- **63.5 x 88.9 mm**: the most common sleeve size, and the size a home printer's 3 x 3 grid fills on both A4
  and US Letter. See [5.3](#53-the-sheet).
- **5 mm margins**, so the text area is **53.5 mm** wide. 5mm is what survives a home printer's drift and a
  hand-held guillotine.
- **8 pt body text**, which is about **38 characters a line** at 53.5 mm in a humanist face.
- **4 lines of body text**, 14 mm, which is what is left after the head, the foot and the rule above the
  caster line. The foot is shorter than it was: the `Unlock` line and the one or two lines of `Requires` are
  gone, and that room is margin. Every card fits in 4 lines with no line wrapped. For one reading of
  `e6f72578` the box was 5 lines, 17.5 mm: `latch` has four body lines and its caster line, printed with the
  full word `Regeneration`, wrapped to a fifth. Printing the Condition as `Regen` on the card
  ([2.2](#22-the-words)) took the wrap away, and the box went back to 4.

Measured against the real catalogue, rendering every card face from `data/`. Two of the four readings measure
a **joined string**, so the join is part of the measurement and is stated here rather than left to a reader to
guess. The **body** is the body lines of [2.1](#21-what-is-printed-and-where-it-comes-from) - the targeting
line, one line per effect, one `Caster:` line per caster effect - joined by ` / `, the same separator
[2.4](#24-the-seven-that-need-a-second-sentence) writes them with. The **statline** was never a defined
string, so it is defined here: it is that body with the cost line in front of it and the critical line
behind it, joined the same way. Neither carries the Spell's name, its package or the content hash: the name
and the package are the head, and the hash names the deck rather than resolving a cast. The command prints
all four readings:

```bash
python3 -c "
import json,glob,statistics,collections
SEP=' / '   # the one separator both joined figures use
def dur(e): return 'permanent' if e.get('permanent') else str(e['durationRounds'])+' round'+('s' if e['durationRounds']!=1 else '')
def eff(e):
  k,a,ap=e['kind'],e.get('amount'),e.get('amountPerRound')
  return {'Damage':f'Damage {a}','Heal':f'Heal {a}','EnergyGain':f'Energy +{a}','EnergyDrain':f'Energy -{a}'}.get(k) or {
   'Bleed':f'Bleed {ap} a round, {dur(e)}','Regeneration':f'Regen {ap} a round, {dur(e)}',   # 2.2: Regen on the card
   'EnergyRegeneration':f'Energy regeneration {ap} a round, {dur(e)}','Stun':f'Stun, {dur(e)}',
   'DefenseBuff':f'Defense +{a}, {dur(e)}','DefenseDebuff':f'Defense -{a}, {dur(e)}',
   'InitiativeBuff':f'Initiative +{a}, {dur(e)}','InitiativeDebuff':f'Initiative -{a}, {dur(e)}'}[k]
W=[];L=[];B=[];S=[]
for p in glob.glob('data/Spells/**/*.json',recursive=True):
  d=json.load(open(p));t=d['targeting'];o,m=t['origin'],t['maxTargets'];n=d['id'].split(':')[1];c=d['criticalChance']
  body=['Self' if o=='Self' else (f'One {o.lower()}' if m==1 else f\"Up to {m} {'enemies' if o=='Enemy' else 'allies'}\")]
  body+=[eff(e) for e in d['effects']]+['Caster: '+eff(e) for e in d.get('casterEffects',[])]
  stat=[f\"Cost {d['energyCost']}\"]+body+[f'Critical {round(c*100)}%' if c else 'No critical roll']
  W.append((max(len(x) for x in body),n));L.append(len(body))
  B.append((len(SEP.join(body)),n));S.append((len(SEP.join(stat)),n))
def r(t,v): print(t,'max',max(v),'median',statistics.median(x[0] for x in v),'min',min(v))
print('widest line',max(W),' lines per card',sorted(collections.Counter(L).items()))
r('body    ',B);r('statline',S)"
# widest line (33, 'latch')  lines per card [(2, 14), (3, 24), (4, 6)]
# body     max (96, 'revenant_guards') median 41.0 min (16, 'wait')
# statline max (124, 'revenant_guards') median 68.5 min (41, 'rejuvenate')
```

| Reading | Value | What it means for the layout |
| --- | --- | --- |
| Body lines per card | 2, 3 or 4 | 14 cards at 2, 24 at 3, 6 at 4 (`crazed_specter`, `extort`, `latch`, `revenant_guards`, `soul_devourer`, `tranquilizer_dart`), read at `3c9eb083`; 15 at 2 at `ad3e4d00`, with `basic_attack`. No line wraps, so a card prints as many lines as it has, and the 4-line box holds every card. With the full word `Regeneration`, `latch` printed 5. `momentum` went from 2 lines to 3 with ADR 0078, and `crazed_specter` from 3 to 4 with its target Bleed on 2026-10-05; `night_raid` stays at 3. |
| Widest single line | **33 characters** (`latch`: `Caster: Regen 1 a round, 3 rounds`) | Five under the 38 a line holds, so **no line in the catalogue wraps**. With the full word it was 40 and wrapped ([2.2](#22-the-words)). The next widest are 32: `Caster: Bleed 4 a round, 1 round` on `revenant_guards` and `crazed_specter`, and `Caster: Bleed 1 a round, 1 round` on `bone_ward`. The line that wrapped before `latch`'s, `momentum`'s `Energy regeneration 2 a round, 3 rounds` at 39, left with ADR 0078. |
| Whole body, one string | max **96** characters (`revenant_guards`), median **41**, min **16** (`wait`), read at `3c9eb083` (median 40 at `ad3e4d00`, with `basic_attack`'s 20) | 96 characters is under three full lines. No card is tight on the body alone. `latch`'s body is 84 (91 with the full word), `healing_screech`'s 45 (52) and `soothing_chant`'s 42 (49). The max and the min do not move with the abbreviation. The median does at `3c9eb083`: with 44 cards it is the mean of the 22nd and 23rd bodies, `momentum`'s 40 and `soothing_chant`'s 42, and with the full word it would be 42.5. `crazed_specter`'s is 89 at `ad3e4d00`, the second longest (61 before its target Bleed), and `night_raid`'s is 38, as it was; neither of those two changes moved the three figures. |
| Whole statline (cost, targeting, effects, caster, critical) | max **124** (`revenant_guards`), median **68.5**, min **41** (`rejuvenate`), read at `3c9eb083` (median 68 at `ad3e4d00`) | The statline is never printed as one string - it is spread across the head, the body and the foot - so this is a total, not a line length: 124 characters over a head, four body lines and a foot. It was 143 while it carried the `Unlock` line, at a smaller catalogue. The audit reached the same conclusion on a rendering of its own, and it does not depend on the join: nothing overflows the 4-line box. `crazed_specter`'s is 117 at `ad3e4d00`, the second longest, and that change moved none of the three figures. |

### 2.4 The seven that need a second sentence

The audit flagged seven: `revenant_guards`, `crazed_specter`, `psycho_rush`, `summon_minions`,
`soul_devourer`, `thundering_seal`, `guard`. They are the Spells that carry either a **Caster effect**, which
must not be read as a target effect, or **two Conditions of one kind** on the same target, which must not be
read as one. Here is every one of them, line by line, with the character count of each line against the 38 a
line holds:

| Spell | Body lines | Longest line | Lines used of 4 |
| --- | --- | --- | --- |
| `revenant_guards` | `Up to 3 allies` (14) / `Defense +3, permanent` (21) / `Defense +4, 2 rounds` (20) / `Caster: Bleed 4 a round, 1 round` (32) | 32 | **4** |
| `crazed_specter` | `Up to 3 enemies` (15) / `Damage 4` (8) / `Bleed 4 a round, 2 rounds` (25) / `Caster: Bleed 4 a round, 1 round` (32) | 32 | **4** |
| `psycho_rush` | `One enemy` (9) / `Damage 10` (9) / `Caster: Defense -2, 1 round` (27) | 27 | 3 |
| `summon_minions` | `Up to 3 enemies` (15) / `Bleed 2 a round, 3 rounds` (25) / `Caster: Damage 2` (16) | 25 | 3 |
| `soul_devourer` | `One enemy` (9) / `Damage 7` (8) / `Energy -3` (9) / `Caster: Heal 4` (14) | 14 | **4** |
| `thundering_seal` | `One ally` (8) / `Defense +3, permanent` (21) / `Defense +3, 2 rounds` (20) | 21 | 3 |
| `guard` | `One ally` (8) / `Defense +1, permanent` (21) / `Defense +1, 2 rounds` (20) | 21 | 3 |

Three of the seven use four lines (`crazed_specter` since 2026-10-05, read at `ad3e4d00`), and none of their
lines is over 32 characters. **The layout that fits them is one effect to a line.** Not prose: a line per
effect, each with its own Duration, and a 0.3 pt rule above the caster line. That is what makes
`revenant_guards`' four separate things - two Defense Conditions on up to three allies and a Bleed on itself -
four things on the card instead of one sentence to parse. `crazed_specter` prints the same Condition twice,
`Bleed 4 a round, 2 rounds` above the rule and `Caster: Bleed 4 a round, 1 round` below it: one token face,
two Durations, two different Creatures. The rule is what says each target bleeds for two Rounds and the
caster for one. Nine
more Spells carry a Caster effect at `e6f72578` and are not among the audit's seven: `bone_ward`, `extort`,
`hateful_sacrifice`, `latch`, `momentum`, `ambush`, `parasite_jab`, `reckless_swing` and `shield_bash`. They
use the same rule and the same prefix. `momentum` reads `One enemy` (9) / `Damage 3` (8) /
`Caster: Energy +2` (17): three lines, and the rule is what says the 2 Energy go to the caster that struck
and not to the enemy it struck. `ambush` reads `One enemy` (9) / `Damage 8` (8) /
`Caster: Initiative -5, 1 round` (30), and the rule is what says the caster slows itself, not its victim.
`latch` reads `One enemy` (9) / `Bleed 1 a round, 3 rounds` (25) / `Damage 1` (8) /
`Caster: Regen 1 a round, 3 rounds` (33): four lines, the widest line in the catalogue, and no wrap. It is the
card the `Regen` abbreviation was made for ([2.2](#22-the-words)): with the full word its caster line was 40
and took a fifth line.

### 2.5 Three card faces, written out

Real Spells, generated from `data/`. `[ ]` marks a printed zone. `9419f9` is the first six characters of
the content hash this working tree builds (`cat data/dst/game.schema.sha256`); the generator prints
whatever the build it was handed says, and refuses to print when there is nothing to say.

**`revenant_guards`** - the longest body in the catalogue, and the heaviest cast, tied with `crazed_specter`
since 2026-10-05 ([Part 7](#part-7-coverage-the-needs-a-component-rows)).

```
+--------------------------------------+
| Wraithguard                      (3) |   name, energy cost
| Revenant . level 3                   |   the package that teaches it
|--------------------------------------|
| Up to 3 allies                       |   targeting: origin, scope, max targets
| Defense +3, permanent                |
| Defense +4, 2 rounds                 |
| ------------------------------------ |
| Caster: Bleed 4 a round, 1 round     |
|--------------------------------------|
| No critical roll                     |   the chance as authored: 0 since 2026-10-04
|                                      |
| spell:revenant_guards:v1     9419f9  |   versioned id, content hash prefix
+--------------------------------------+
```

`revenant_guards` printed `Critical 33%` until 2026-10-04, with the threshold blank because 0.33 is not a
whole number of twentieths. Seven Spells still print a chance off the twentieths (`toxic_waves` 33%, for one),
and their threshold stays blank until the catalogue is snapped ([1.6](#16-dice)); on a d20 `toxic_waves`
becomes 35% and `d20: 14+`. This is exactly the case the generator exists for: the card is reprinted from the
build, not corrected by hand.

**`crushing_stomp`** - the only cost-4 Spell in the catalogue, and one of the four Stuns.

```
+--------------------------------------+
| Crushing Stomp                   (4) |
| Colossus . level 3                   |
|--------------------------------------|
| One enemy                            |
| Damage 7                             |
| Stun, 2 rounds                       |
|--------------------------------------|
| Critical 75%  d20: 6+                |
|                                      |
| spell:crushing_stomp:v1      9419f9  |
+--------------------------------------+
```

**`wait`** - the floor of the game, and the shortest card. No critical line to read, because the chance is
zero and the Creature adds nothing: the card says so rather than leaving a blank a player reaches for a die
over.

```
+--------------------------------------+
| Focus                            (0) |
| Starting spell                       |
|--------------------------------------|
| Self                                 |
| Energy +2                            |
|--------------------------------------|
| No critical roll                     |
|                                      |
| spell:wait:v1                9419f9  |
+--------------------------------------+
```

The head line is the whole of what the card says about acquiring the Spell: which package to buy, and how
deep it sits. `Colossus . level 3` does not say what Colossus needs first or what it pays in initiative;
the Colossus package card does, once, for both of the Spells it teaches. The longest head line
at `9419f935` is 22 characters (`Deathstalker . level 3`, as are Blightweaver's and Transcendent's), inside the
38 a line holds. It was `Plague Doctor . level 2` (23) before the packages were renamed.

### 2.6 The Speed card

Twelve cards, a Quick and a Standard for each Creature ([1.5](#15-the-rest-of-the-pieces)): the maintainer's
answer to [Part 6, question 14](#14-a-two-sided-speed-token-cannot-be-placed-face-down). Nothing on it comes
from `data/`: its two faces are the two Speeds of the glossary, and its count is the rule set's team size.

```
+----------------------------------------------------+
| Quick                                              |   the Speed, in the largest type on any card
|====================================================|
| Acts before every Standard Creature.               |
| No critical roll this Round.                       |   the reminder: CriticalChanceOf is 0 for Quick
|                                                    |
| Speed card                                 9419f9  |   what it is, content hash prefix
+----------------------------------------------------+

+----------------------------------------------------+
| Standard                                           |
|====================================================|
| Acts after every Quick Creature.                   |
| Critical as printed on the Spell.                  |
|                                                    |
| Speed card                                 9419f9  |
+----------------------------------------------------+
```

| Choice | The constraint it answers |
| --- | --- |
| Poker size, 63.5 x 88.9 mm, the Spell card's and the package card's size | One card size in the box: one 3 x 3 grid for the generator to cut ([5.3](#53-the-sheet)) and one sleeve for a Player to buy, the reason [4.1](#41-the-package-card) gives. And a face-down Speed card hides the choice only if its face does not show through, which is the face-down Spell card's problem exactly: whatever question 8 settles for the one serves the other. A mini card (41 x 63 mm, 16 to a sheet) would print on one sheet instead of two, and need a second sleeve size to be as opaque. |
| The face printed landscape | The Speed slot holds the card landscape, 88.9 mm wide, because that costs a board the least height ([3.1](#31-the-creature-board)). The face reads the way it lies. |
| The Speed's name in the largest type on any card | It is read across the table: every Player reads every Speed when the cards are turned, to count the Quick cards and set the divider ([3.5](#35-the-initiative-track)), and again at Resolution, where a Quick Creature does not roll. |
| One back for all 12, the same on Quick and Standard | The back is the whole of the component: a choice of one of two is hidden only behind a back the two answers share. Printed, it reads `Speed`, so a face-down Speed card is not taken for a face-down Intent, and it tells nothing about the face. Printed single-sided, the blank back is the common back, and the card needs the Spell card's opaque-backed sleeve. Part 6, question 8. |
| The reminder on the Quick card, and its mirror on the Standard card | `CriticalChanceOf` is 0 for a Quick Creature whatever its Spell prints (`ResolutionRules.cs:85`). The cost is paid at Resolution and chosen here, so it is printed where it is chosen, or Quick reads as free (translation.md, the `Quick` critical row). `No critical roll` is the Spell card's own words for a cast that does not touch the die ([2.5](#25-three-card-faces-written-out)). |
| No Creature number | Any Quick card serves any Creature: the slot a card lies in says whose it is. A number would make twelve different cards and change nothing the rule reads. |
| The content hash prefix | Nothing on the card comes from the content, but [5.5](#55-the-invariant) holds every card face to the hash, and one rule for every card is one the generator cannot get wrong. |

Each line of the face is under the 38 characters a line holds at 8 pt: the longest is
`Acts before every Standard Creature.`, 36.

---

## Part 3. Boards and tracks

The design rule of this part: **a rule a player has to remember is a rule that will be got wrong.** Each
layout choice below names the rule it enforces physically.

### 3.1 The creature board

A5, 105 x 148 mm, two to an A4 sheet. Front is the living Creature; back is printed `Defeated` with no slots
at all, so a dead Creature cannot be given Energy, a Speed card, an Intent or a Condition - which is the rule
`creatures.Where(creature => creature.IsAlive)` enforces in five different places.

```
+-----------------------------------------------+
| [1]  Main                          Creature   |   the Creature's number: no tiebreak, see below
|-----------------------------------------------|
| Health   0 1 2 ................ 14 15         |   two rows, one marker, ends at 30
|          16 17 ................ 29 30         |
| Energy   0 1 2 ................ 12 13         |   three rows, one marker, ends at 40
|          14 15 ................ 26 27         |
|          28 29 ................ 39 40   [+40] |   the overflow chit's place
|-----------------------------------------------|
| Defense  buffs   0 ................. 20 [+20] |
|          debuffs 0 ................. 20 [-20] |
|          Defense = buffs - debuffs, never < 0 |
|-----------------------------------------------|
| Base initiative   tens 0..3   units 0..9      |
|-----------------------------------------------|
| Conditions   | new |   3   |   2   |   1   |  |   the duration dock
|              |     |       |       |       |  |
|-----------------------------------------------|
| Speed [ a card, 88.9 x 63.5 ] or a Stun token |
+-----------------------------------------------+
```

| Affordance | The rule it enforces, so nobody has to remember it |
| --- | --- |
| The number 1 to 6 in the corner | It names the Creature: on its initiative marker, and aloud when a cast names its targets. Ids are handed out in join order (`Match.Spawn`, `Match.cs:324-333`): 1 to 3 is Player 1, left to right. **It breaks no tie.** A tie between the sides is a d20 Roll-off, and a tie within one side is its owner's Tie order (ADR 0063). The number decides one thing more: tied Creatures roll in number order, lowest first. That fixes the order of the rolls and changes no result, so a table that rolls in another order has lost nothing. |
| The Health rail ending at 30 | A Heal is capped by the Health missing (`Creature.cs:271`). The marker cannot go past the end of the rail. |
| The `Defeated` back with no slots | A dead Creature takes no damage, no healing, no Energy, no Spell and no Condition. |
| The Speed slot, and a Stun token that occupies it | A stunned Creature takes no Speed choice, so it gets no Activation slot and no Intent (`SpeedRules.cs:34`, `TimelineBuilder.cs:23-28`). The Stun token is in the slot: there is nowhere to put a Speed card. The slot prints the token's place at its centre, since a 15 mm token no longer fills a card-sized slot and a card laid over it would hide it. The token comes off at the Cleanup that ends the Stun, when the dock's Stun token becomes an Immune token ([3.2](#32-the-condition-dock-and-the-countdown)); nothing goes in the Speed slot for the immunity, since an immune Creature takes a Speed card. This is the biggest effect in the game and the one most likely to be played as "loses its attack". |
| A pick token laid in the header, beside the number | A Creature buys at most one package an opportunity (`EvolutionRules.cs:56-59`, ADR 0066). The token a pick moves off the mat lies on that Creature's board until the Sub-phase ends, so a Creature that has bought is marked, and a second pick for it is not made. Nothing is printed for it: the header has room for a 15 mm token, and the token is there for one Sub-phase. |
| The Energy rail being face up | An Intent must be affordable (`IntentRules.cs:50-69`), and a Player must be able to check that without revealing the Intent. Energy is public in the engine's own projection, so the rail is public too. |

**The Speed slot is sized for a card now**, not a token (Part 6, question 14). A Speed card is poker size
([2.6](#26-the-speed-card)), so the slot is a 90 x 65 mm rectangle, the card laid landscape with about a
millimetre of play. Landscape costs the board the least height: 90 mm is inside the 95 mm of usable width
[3.4](#34-initiative-two-small-rails-instead-of-one-long-one) names, where a portrait card would take 90 mm of
the board's height instead of 65. On the 105 x 148 mm board drawn above, the slot takes 65 of the 148 mm, and the rails
and the dock share the rest. Whether they fit there is part of question 15, which already asks what size the
board is. The drawing is not to scale.

**The `Targeted by` row is retired** (ADR 0083). It was one box per caster number, and a caster's target
marker sat in it from the reveal until the resolution, so a Player could read who was pointing at a Creature
and one cast could not name a target twice. An action now resolves as soon as its targets are named, so
nothing points at a Creature for longer than one resolution, and the row gives its line back to the rails.
No duplicate targets (`TargetingRules.cs:68`) is a rule the rulebook states instead of a box that holds one
marker: a cast whose targets are named aloud cannot name one twice without saying so.

### 3.2 The condition dock, and the countdown

Four lanes: `new`, `3`, `2`, `1`. A Condition token is placed in `new` when it is applied. At **Cleanup**:

1. every token already in a numbered lane slides one lane left; a token leaving lane `1` is removed, except
   that a Stun token leaving lane `1` of a living Creature is swapped for an Immune token in lane `1`, and the
   Stun token in the Speed slot comes off with it;
2. every token in `new` moves into the lane matching the Duration printed on it.

That is the whole countdown, and it is why the board has a `new` lane: it makes "**the first countdown after
an application does not count**" (`Condition.cs:12,88-100`) a piece of geometry instead of a rule a player has
to recall on the Round they apply something. Nothing refreshes any more: a Stun on a Creature already stunned
or immune to Stun is ignored (ADR 0072), so no token is ever moved back into `new`.

The swap in move 1 does the same for Stun immunity (ADR 0072). The engine counts the immunity down at the
start of `Creature.TickConditions`, before the Stun expires in the same call, and sets it to one Round when a
Stun expires on a living Creature; so it runs through the next Round and ends at the next Cleanup. The
Immune token did not slide, so the next Cleanup's move 1 takes it out of lane `1` at exactly that moment. The
immunity is not a Condition, and the dock holds its token only because lane `1` is the lane the next Cleanup
empties.

Four lanes is derived: the longest Duration in the catalogue is 3 Rounds (`summon_minions`' Bleed, `latch`'s
Bleed and the Regeneration on its caster, and `shield_bash`'s Defense buff on its caster, at `e6f72578`),
plus the `new` lane. **VALUE**: a longer
Duration authored in `data/` is a fifth lane and a reprint of six boards.

Permanent Conditions never enter the dock. They move a rail and are discarded, because they never count down
(`Condition.cs:42-44`) and never have to be undone.

### 3.3 Defense: two rails, because the floor is applied once

`TotalDefense` is base plus the Defense buffs less the Defense debuffs, and the result floors at zero
(`Creature.cs:95-97`, ADR 0035). The floor is applied **to the total**, not to the intermediate. So a single
rail that stops at zero would be wrong: a Creature at 0 base carrying a -4 debuff and then a +3 buff has a
total Defense of 0 in the engine, and a rail clamped at zero would show 3.

Two rails hold the un-floored sums, and the printed line under them is the reading:
`Defense = buffs (at most 10) - debuffs, never below 0`. The buffs count for at most 10 together (ADR 0076),
but the buff rail keeps the whole sum: a buff past the ceiling is still held and counts again when another
one expires. That turns the audit's complaint - "1 sum over the Condition
tokens per target, per cast" (translation.md 1.8) - into **one subtraction of two numbers that are side by
side**, done when a Condition lands or expires rather than once per incoming cast. The debuff rail is at zero
in most games: only three Spells lower Defense.

Both rails run 0 to 20, with overflow chits. The rule beside 20: the largest Damage in the catalogue was 10,
the critical multiplier is 2.0, so **20 Defense blanked every attack in the game**, and the only damage that
got through was a Bleed tick, which ignores Defense (`UpkeepRules.cs:67`). It is a **VALUE**: a catalogue with
a bigger hit or a bigger multiplier reprints the boards. Since ADR 0076 the buffs *read* at most 10, so the
buff rail past 10 only keeps count of what is held; the debuff rail has no ceiling, which is why the chits
exist.

**At `e6f72578` the largest Damage is 11** (`hateful_sacrifice`, since PR #246), so the rule reads 22, two
past the printed end. The rails are not reprinted here: since ADR 0076 no Creature's Defense reads above 10,
so the end the rule gives no longer blanks anything a Creature can reach, and whether the rule or the rail
should move is Part 6, question 16.

```bash
python3 -c "
import json,glob
print(max(e['amount'] for p in glob.glob('data/Spells/**/*.json',recursive=True)
  for e in json.load(open(p))['effects']+json.load(open(p)).get('casterEffects',[]) if e['kind']=='Damage'))"   # 11
```

### 3.4 Initiative: two small rails instead of one long one

Base initiative only ever grows, by the `initiativeBonus` of every package bought, once a purchase
(ADR 0056; glossary, Base initiative). No Spell adds anything (ADR 0059). Its ceiling in a 20-Round Match is
the last line of the command in [1.1](#11-spell-cards-and-package-cards): **30** at `e6f72578`.

A Player makes 2 picks at each of 10 opportunities: 20 purchases. A Creature buys at most one package an
opportunity (ADR 0066), so **10 of them at most land on one Creature**. The 21 packages' bonuses sum to 50,
but a Creature cannot own all 21 with 10 picks, and a level-3 package cannot be bought without the two below
it. The 10 prerequisite-closed packages that pay the most pay 30, and four sets tie there. All four hold
Predator, Deathmarked, Deathstalker, Blighted and Blightweaver (18), and Warped and Stormborn (5);
the last three are Ethereal, Transcendent and Cataclysm; Parasite, Soulreaver and Cataclysm; Brute, Frenzied
and Ravager; or Brute, Frenzied and Cataclysm (7 each). At `813bb91b` it was 29, from one set; several bonuses
have moved since, and the last, Blighted's from 2 to 3, took it to 30. So **Base initiative tops out at
5 + 30 = 35.**

**Current initiative tops out at the same 35.** It is Base plus the Initiative buffs less the debuffs, and at
`e6f72578` no Spell places an Initiative buff ([1.4](#14-condition-tokens)): the only two that touch
Initiative lower it.

```bash
python3 -c "
import json,glob
for p in glob.glob('data/Spells/**/*.json',recursive=True):
  d=json.load(open(p))
  for w,es in (('target',d['effects']),('caster',d.get('casterEffects',[]))):
    for e in es:
      if e['kind'].startswith('Initiative'): print(d['id'].split(':')[1],w,e['kind'],e['amount'],e['durationRounds'])"
# frostbite target InitiativeDebuff 3 1
# ambush caster InitiativeDebuff 5 1
```

A line that read `InitiativeBuff` would raise the Current ceiling above the Base one. While `death_squad`
placed +2 for a Round on up to 3 allies, three of them could land on the Creature at the Base ceiling, so the
Current ceiling was 6 above it: 40 at `813bb91b`, 39 before tune run 11 (Warped +2, Tyrant +4, at
`4ab506fa`), where the Base ceiling was 34 and 33. In a 16-Round Match they were 29 and 35. While two packages
could land on one Creature an opportunity (ADR 0056, before ADR 0066), they were 44 and 46 in 16 Rounds;
under one Spell a pick, twice every Round, 52 and 58.

A rail to 35 is 36 cells and 180 mm at a readable 5 mm a cell, which no board holds. **Two rails, tens 0 to 3
and units 0 to 9, are 14 cells**, 70 mm, and read as one two-digit number. The print constraint is the
board's 95 mm of usable width; the rule is the ceiling of 35, which the rails' 0 to 39 covers with 4 to spare
(5 at `813bb91b`, 6 before tune run 11). A bonus is 1 to 5 at `e6f72578`, so a purchase is one marker move on
the units rail, sometimes carrying into the tens rail.

The tens rail is a **VALUE**: the bonuses are content, and a tuning pass may move the ones
`data/balance/knobs.json` declares as knobs (ADR 0061). At `e6f72578` it declares one, Blighted's, from 1
to 4. At that `max` the 1.1 command reads a ceiling of 5 + 31 = 36, inside the rails' 39, so no pass inside
the declared bounds reprints the rail. Part 6, question 11.

Current initiative is **not** on a rail. It is Base plus the dock's Initiative buff tokens less its Initiative
debuff tokens, floored at zero, and it is read **once a Round**, when the timeline is built. That is the
audit's own count: one marker move, read once. Its ceiling of 35 is the Base rails' own, so it needs no cell
of its own, and if a buff comes back it is a token in the dock, not a rail. Only 2 Spells in the catalogue
touch Initiative, `frostbite` on an enemy and `ambush` on its own caster, each for one Round and neither
permanent, so most boards have nothing to subtract.

### 3.5 The initiative track

Six slots in a row, with a divider the Players move between the Quick slots and the Standard ones, and the
ordering printed along the edge:

```
 |<-- Quick ------[ divider ]------ Standard -->|
 [ 1st ] [ 2nd ] [ 3rd ] [ 4th ] [ 5th ] [ 6th ]
 Quick before Standard. Initiative high to low.
 Tied across the sides: each rolls a d20, high first; a number both sides rolled, all on it roll again.
 Your own tied Creatures: tie order chits, face down, turned together; you order them in your Places.
```

The divider moves because a Round can have 0 to 6 Quick slots; it is set to the count of Quick cards
turned. Placing is: turn every Speed card in play together, put the Quick Creatures' markers in order of
Current initiative, then the Standard ones. Six markers, six lookups, a sort of at most
six - the audit's numbers, unchanged.

A tie is settled on the track in two steps (ADR 0063), and the components carry both:

1. **The Roll-off.** Tied markers of both sides sit side by side over the Places they share. Each tied
   Creature rolls a d20, in number order; the highest takes the first Place. The Creatures on a number both
   sides rolled roll again among themselves, a side's own Creatures on it included; a number one side alone
   rolled is not rolled again (`TimelineBuilder.cs:57-83`). The roll decides which Places each side holds,
   and nothing else.
2. **The Tie order.** A Player who holds two Places or more in one tie lays a tie order chit face down on
   each of those Creatures' boards: `1st` takes the side's first Place in that tie, and so on. Both Players
   turn their chits together and move the markers into the places the chits give. That is the same
   face-down-then-turn the Speed choice uses, and it keeps the order hidden until both are in, as the engine
   does. A tie one side holds alone skips step 1. A Round with no tie of two or more of one side's Creatures
   skips step 2.

The track is an **ordering** device and carries no numbers. The alternative, a value track a marker is placed
on, needs 36 cells, 0 to the Current initiative ceiling of 35 in 3.4 (41 cells to 40 at `813bb91b`, 40 to 39
before tune run 11), and would still need the tie rules printed.

### 3.6 The round track

Twenty spaces. The Round marker advances one space at Finalization. The **Round cap marker** is placed at
setup on the space equal to the `RuleSet`'s Round cap: when the Round marker reaches it, the Match ends on
total remaining Health (`WinCondition.cs:23-26`). The cap is a component rather than a memory, and moving it
is how a shorter or longer Match is set up without a reprint.

**Why twenty.** A Match is designed to last 8 to 14 Rounds (ADR 0086), and the table's Round cap is 20
(`playtest.rules.json`), so the Round cap marker needs a space 20. The track stops at the cap: a space past it
is one no Match reaches. It was 16 spaces while the band was 8 to 16. A cap above 20 needs a longer track,
and a reprint of this one sheet.

**How it is laid out.** A space is 16 mm, a 15 mm token with a millimetre of play. Twenty in one row would be
320 mm, past the long side of an A4 sheet (297 mm) and of a Letter one (279 mm), so the track runs in **two
rows of ten**, 1 to 10 above 11 to 20, both read left to right, which is 160 x 32 mm. It still shares its
sheet with the initiative track. On the last Round the Round marker stands on the cap marker: both are flat
15 mm tokens, so one sits on the other.

**The pick marks.** Evolution offers picks only at an opportunity: Round 1 and every second Round after
(`RuleSet.IsEvolutionRound`, ADR 0056). A Round without one gives nobody a pick, and its Evolution ends as it
opens. So every space that is an opportunity carries a printed pick mark, two small pick-token outlines: at
the table's schedule, spaces 1, 3, 5, ..., 17 and 19, **10 marks**. The rule beside the count: the
Rounds r from 1 to 20 with r >= `FirstEvolutionRound` and (r - `FirstEvolutionRound`) a multiple of
`EvolutionInterval`. The Players put their pick tokens on their mats when the Round marker stands on a mark,
and not otherwise. "Is it a pick Round?" is then a look at the track, not a parity sum. The marks are a
**VALUE**: a schedule with another first Round or interval reprints this one sheet. The cap is a marker
because a table changes it between Matches; whether the schedule should be one too is Part 6, question 9.

The track also carries the Round's shape as a printed strip, because it is where a Player looks when they lose
their place. In the engine's order (`RoundSubPhase.cs`, ten sub-phases since ADR 0083 merged reveal and
target with resolution into `Activation`; eleven from ADR 0063 until then):

```
 Start: energy -> energy regeneration -> regeneration -> bleed
 Planning: evolution (marked Rounds: 2 picks a player, one a creature) -> speed (face down)
           -> timeline, ties rolled off -> order your own ties (face down)
 Combat: intents (face down) -> each slot in turn: flip, name targets, resolve
 End: conditions count down -> check the round cap
 A team wiped ends the match at once, on an action or a bleed.
```

The two orderings that change results and will be got wrong are on it and on the player aid: **healing before
bleeding** (`UpkeepRules.cs:24-28`, ADR 0019), and **the critical is applied before Defense is subtracted**
(`ResolutionRules.cs:91`).

The strip keeps `energy regeneration` although no card places one since ADR 0078: it is a pass the engine
still runs in that place (`UpkeepRules.cs:35-73`), and the ADR keeps it in the rules. At `e6f72578` it has
nothing to tick, so a table skips it at no cost, and the strip does not need a reprint when content brings
one back.

### 3.7 The player area, and where a face-down intent sits

An A4 landscape mat a Player, three columns, one a Creature:

```
+---------------------------------------------------------------+
| Player 1        picks: [o][o]                                  |
|---------------------------------------------------------------|
|  creature 1        |  creature 2        |  creature 3         |
|  [ board ]         |  [ board ]         |  [ board ]          |
|  [ intent ]        |  [ intent ]        |  [ intent ]         |
|  [ packages ]      |  [ packages ]      |  [ packages ]       |
+---------------------------------------------------------------+
```

- **The intent slot** is a card-sized rectangle (63.5 x 88.9 mm) below each Creature's board. The Intent is
  the Spell card itself, played face down. It is the mechanic that translates for free (translation.md 1.6).
- **The hand is concealed.** This is the one place the components have to work for their hidden information.
  A Creature's known Spells are **public** in the engine (`CreatureSnapshot.KnownSpells` is on both teams'
  snapshots), so an open hand would be faithful - but then a card leaving an open hand for the intent slot
  tells the opponent exactly which Spell it is, and the hidden Intent is gone. So the hand is held, and the
  public record moves to the **package cards**: every purchase puts a package card face up in that
  Creature's column, where anyone can read what it owns (`CreatureSnapshot.AcquiredTiers`, beside
  `KnownSpells` on the same snapshot) and so what it knows: the starting kit, plus every Spell printed on its
  package cards. Public information stays public; the face-down card stays face down.
- **The package cards** of a Creature lie in a stagger below its intent slot, each covering the one before
  it but for its top band, which carries the name, the level and the bonus ([4.1](#41-the-package-card)). A
  Creature's record reads as a list, and the newest card shows whole.
- **The pick tokens** sit on the mat's header. Two a Player, put there only in a Round the Round track marks
  as an opportunity. A pick moves one onto the header of the board of the Creature it was picked for ([3.1](#31-the-creature-board)),
  where it marks that Creature as done for the opportunity (ADR 0066). A pass takes the tokens still on the
  mat off it, and the end of the Sub-phase takes every token off the mat and the boards.
- **No target markers.** The mat's header held three per Creature while every cast was targeted before any
  resolved. Since ADR 0083 an action resolves as soon as its targets are named, so the header holds the pick
  tokens only.
- **Speed cards**: a Player holds a Quick and a Standard card for each Creature, lays the chosen one face down
  in that board's Speed slot and keeps the other in hand. Both Players turn theirs together, which is what
  makes the Speed choice the genuine simultaneous decision it is in the engine
  (`PlayerBoardStateProjection.cs:38`). The kept card needs no place on the mat: it stays in the concealed
  hand, and it must, because a kept card seen is a played card known. So the player area holds nothing new
  for question 14; what it gains is on the board, the card-sized Speed slot of
  [3.1](#31-the-creature-board), which adds to question 15.

### 3.8 How a cast is declared and resolved, in components

1. **Intent**: put a Spell card from the hand face down in the Creature's intent slot. Legal if the Creature
   knows it - a starting Spell, or one printed on a package card lying with that Creature - and the Energy
   rail is at or above the printed cost.
2. **Activate**, one slot of the initiative track at a time. If the Creature is dead, stunned, can no longer
   pay, or has no legal target, turn its card face up with no targets: it Fizzles. Otherwise its owner turns
   the card face up and names its targets on the board as it stands, pointing at each board and saying its
   number. Up to `maxTargets`, and fewer is allowed.
3. **Resolve** it at once, before the next slot: move the Energy marker down by the cost, roll the die if the
   card prints a chance and the Speed card is Standard, apply each effect line to each target, then the
   caster line. A Team left with no living Creature ends the Match there.

---

## Part 4. The packages as an object

A pick buys a Tier: a named package of Spells with a level, the Tiers it requires, and one initiative bonus
(ADR 0056). 21 are enabled at `e6f72578`: 3 at level 1, 9 at level 2, 9 at level 3, two Spells each. Each
level-2 package requires one level-1 package and each level-3 package requires one level-2 package, so the
21 form three families of seven, one opened by each level-1 package. **Prerequisites are the only rule**: the talent tree
gates nothing, and multiclassing is free, so a Creature may own packages from all three families. A Creature
buys at most one package an opportunity, so the two picks of an opportunity go to two Creatures
([ADR 0066](../adr/0066-a-creature-buys-one-package-an-opportunity.md)), and the top of a family arrives at
Round 5 at the earliest.

What the table has to hold, for each Creature: which Tiers it owns, whether the next one's prerequisite is
among them, and what it knows because of them. The package card holds all three, face up with the Creature,
which is translation.md's verdict ("Tier cards, 21 kinds") and the rulebook's record (§5.3). A package mat
with a pip per purchase does the same job on less paper; it is the alternative in Part 6, question 7.

### 4.1 The package card

One card a Tier a Creature, the same poker size and the same sheet grid as the Spell cards, so the generator
has one card layout to cut and a player has one card size to sleeve. It prints what a pick is chosen on, and
nothing a cast needs:

```
+--------------------------------------+
| Colossus                        [+1] |   name; the bonus, in a square
| level 3 . +1 initiative              |   the band ends here
|======================================|
| Needs Ironhide                       |   every Tier it requires, or "Needs nothing"
|--------------------------------------|
| Vital Surge                          |   every Spell it teaches, one a line
| Crushing Stomp                       |
|                                      |
|                                      |
| tier:dreadnought:v1          9419f9  |   versioned id, content hash prefix
+--------------------------------------+
```

Every line comes from `tiers[]` in the build: `name`, `level`, `initiativeBonus`, the `name` of every Tier in
`prerequisites`, and the `name` of every Spell in `spells`. The Spell text is not repeated: the Spell cards
carry it, and a package card that did would be a second place for a tuning pass to reprint.

What each piece of the layout answers:

| Choice | Why |
| --- | --- |
| The top band, two lines: name, then `level N . +B initiative` | A Creature's cards lie in a stagger, each covering the last but for its band ([3.7](#37-the-player-area-and-where-a-face-down-intent-sits)). The band alone must say which Tier it is, how deep, and what it paid. |
| The bonus twice, in a square at the top right and in words | The square is where the eye goes on a card, as the cost circle is on a Spell card; the words stop `+3` being read as a cost. It is the package's number and no Spell's (ADR 0059). A bonus of 0 prints `+0`, not a blank; no package has one at `e6f72578` (`Ethereal` did). |
| A heavy rule under the band, and no cost circle | What tells a package card from a Spell card in a library pile, in greyscale. A package card never enters a hand. |
| `Needs` on every card, by name | The rule, and what the check reads: a Creature may buy a Tier only if every Tier it `Needs` already lies face up with that Creature. A level-1 card prints `Needs nothing`, so no card has a blank a player has to interpret. |
| No talent tree class, no family map | The tree gates nothing (ADR 0056, ADR 0058). A card that drew its gates would teach a second eligibility rule, the alternative ADR 0056 rejected. |

The measurement, at `9419f935` (the widest line was 23 characters at `e6f72578`, `813bb91b` and `4ab506fa`
too, where it was Tyrant's `level 3 . +4 initiative`; the body lines read `[2, 3]` while the level-2 packages
taught one Spell):

```bash
python3 -c "
import json,glob
S={json.load(open(p))['id']:json.load(open(p))['name'] for p in glob.glob('data/Spells/**/*.json',recursive=True)}
T={json.load(open(p))['id']:json.load(open(p)) for p in glob.glob('data/Tiers/*.json')}
L=[]
for t in T.values():
  lines=[t['name'], f\"level {t['level']} . +{t['initiativeBonus']} initiative\", 'Needs '+(' and '.join(T[q]['name'] for q in t['prerequisites']) or 'nothing')]+[S[x] for x in t['spells']]
  L.append((max(len(x) for x in lines), t['name'], len(lines)-2))
print('widest line', max(L)); print('body lines', sorted(set(x[2] for x in L)))"
# widest line (23, 'Warped', 3)
# body lines [3]
```

The widest line on any package card is 23 characters (`level 3 . +2 initiative`, and every band's second
line is as long) against the 38 a line holds at 8 pt, and a body is 3 lines, a `Needs` line and two Spells,
of the 4 the Spell card's body box holds. The package card is the
easy card to print. The band is two lines at 8 pt, about 10 mm with its rule, so a stagger costs 10 mm a card.

### 4.2 How a purchase reaches the hand, and how the bonus is recorded

A pick lays a copy of the Tier's package card **face down** in that Creature's column and moves one pick
token from the mat onto its board; nothing else moves (ADR 0089). When the Sub-phase ends, both Players turn
every face-down card over together, and each becomes a purchase: **three actions in a fixed order**, the order
of rulebook §5.3:

1. **Package card.** The card, now face up, goes on the top of that Creature's stagger. This is the public
   record that the Creature owns the Tier, and it is what the opponent reads instead of the concealed hand.
2. **Spell cards.** Take one copy of each Spell the package card names from the library into the hand. The
   library is the 264 Spell cards filed by the first package in their head, six copies of each Spell
   together. A Spell the Creature already knows is not taken again (ADR 0056: the grant is idempotent). At
   `e6f72578` no Spell is taught by two Tiers, so this never happens, but authored content may make it
   happen, and [2.1](#21-what-is-printed-and-where-it-comes-from) says what that card's head prints.
3. **Initiative.** Move that Creature's Base initiative rails up by the card's bonus. **This is the only
   place Base initiative ever moves** (ADR 0056), which is why the package card prints it and no Spell card
   does. Values at `e6f72578` are 1 to 5, so it is one marker move on the units rail, sometimes carrying into
   the tens rail.

Rules of the sub-phase that the components carry rather than the rulebook:

- **A Creature cannot buy a Tier it owns.** The card is already in its stagger; a second copy there is a
  mistake anyone can see.
- **A prerequisite is a card.** Every Tier a card `Needs` must already lie face up with the same Creature.
  The check is a read down one stagger's bands.
- **A pick token on a board is the one-a-Creature rule.** A Creature board holding a pick token has
  been picked for this opportunity, and a second pick for it is refused (`Planning.CreatureAlreadyEvolved`,
  ADR 0066). A face-down card is not owned yet, so the face-up stagger is the board as the Sub-phase opened,
  which is the board the engine validates against.
- **A refused pick changes nothing** (ADR 0056: no half-taught package, no bonus without the Tier). It is
  refused before its card is laid, so no Spell card or rail ever moves for it. The pick token moves with the
  card, not before it.
- **The starting kit grants no bonus.** No Tier teaches it, so it has no package card and no `+B`, and a
  Player never adds initiative for it.

A pick names a living Creature. The cards do not enforce that; the creature board does, since a `Defeated`
board has no hand and no rails to receive a purchase ([3.1](#31-the-creature-board)).

---

## Part 5. The generator, specified

Interface and behaviour only. **The generator is not implemented.** There is no `printshop/` directory, and
nothing under `tools/` (the data builder alone) or `scripts/` (`iterate.sh`, `sweep-weight.py` and the
`build-tiers.py` migration, ADR 0057) prints a sheet. It is built where code is built, with its own pull
request and the usual gate.

What exists, and is not a print: the table app draws the same card words on screen. `CatalogueProjection`
(`src/DownfallArena.Application/Catalogue/`) turns the build into `CardFace` and `PackageCard` records,
stamped with the content hash and the rule set, and `table/card.js` renders them. Its card face already
follows [2.1](#21-what-is-printed-and-where-it-comes-from) on the two lines ADR 0056 and ADR 0059 removed, and
differs on the head (Part 6, question 10).

### 5.1 Inputs

| Input | What it is | Why it is this and not something else |
| --- | --- | --- |
| `data/dst/game.schema.json` | The consolidated, validated catalogue the data builder writes (ADR 0009): `spells`, `creatures`, `tiers`, `talentTrees` | It is the only place aliases are resolved, references are validated and `"enabled": false` items are pruned. Reading `data/Spells/**` or `data/Tiers/**` would print content a build does not have, and would have to reimplement the pruning rules of `data/README.md`. The generator reads `tiers[]` for the package cards and the Spell card heads, and does not read `talentTrees[]` at all: the tree gates nothing (ADR 0056). |
| `data/dst/game.schema.sha256` | The content hash | The stamp every sheet carries, and the identity of the deck. |
| A rule set file | Team size, energy a Round, picks an opportunity, the first opportunity Round, the interval, the Round cap, the critical multiplier: the seven fields of `docs/tabletop/playtest.rules.json` | **The content hash does not cover the `RuleSet`**, and half the counts in Part 1 come from it: the copies of both decks, the pick tokens, the pick marks, the Round track's spaces and the Energy rail's end. A deck plus a set of boards is only valid for a content hash **and** a rule set, so both are stamped. The table host already reads this file (`table --rules`, `RuleSetFile`), and the generator takes the same one. Where that file should live is still Part 6, question 6. |
| The die | A d20 ([d20-criticals.md](d20-criticals.md), built as ADR 0100) | The threshold `d20: N+` is printed when the chance is a whole number of twentieths, and omitted when it is not, rather than rounded. Every chance is a twentieth by rule since 2026-10-07, and the data builder refuses one that is not, so every card that rolls prints its threshold and the omission is never taken. |

Cards are **generated, never transcribed**. A tuning pass reprints the deck rather than invalidating it, which
is the whole reason phase 3 specifies a generator instead of a table of card texts.

### 5.2 Outputs

- **Card sheets**: every Spell face that some Creature could know - a starting Spell, or one an enabled Tier
  teaches - repeated 2 x team size times, laid out 9 to a sheet. 44 faces and 264 cards, 30 sheets, the last
  holding 3, at `3c9eb083` (45 faces and 270 cards at `e6f72578` and `ad3e4d00`).
- **Package card sheets**: every enabled Tier's face, repeated 2 x team size times, 9 to a sheet. 21 faces
  and 126 cards, 14 sheets, at `e6f72578`.
- **Speed card sheets**: the Quick face and the Standard face of [2.6](#26-the-speed-card), each repeated 2 x
  team size times, 9 to a sheet. 12 cards, 2 sheets. The rule set gives the count; the content gives nothing
  but the hash.
- **Component sheets**: 6 creature boards, 2 player mats, the initiative track, the round
  track with a space for every Round up to the rule set's Round cap and its pick marks computed from the rule
  set's schedule, and the token sheets.
- **A manifest page**: Part 1's table, generated, with the content hash, the rule set, and the date. It is the
  page that tells a reader whether their box matches their game. It lists any Spell that gets no copy because
  no package teaches it, since that is a content defect a printer should see (ADR 0058 calls it unacquirable).
- **A card back**, one design, optional to print.

The format is **a static page that prints**: HTML and CSS with `@page` at the sheet size, printed to PDF by
the browser. No PDF library, no build step, nothing installed - the same weight as the viewer and the studio
(ADR 0015's reasoning, ADR 0024's constraint).

### 5.3 The sheet

| Constraint | Choice | Why |
| --- | --- | --- |
| Paper | A4 (210 x 297) and US Letter (216 x 279), the same layout on both | 3 x 63.5 = 190.5 mm wide and 3 x 88.9 = 266.7 mm tall, plus 3 mm bleed on the outer edge, is 196.5 x 272.7 mm. It fits inside both. One layout, two papers. |
| Cards a sheet | 9 | The 3 x 3 grid above. 264 Spell cards is **30 sheets**, the last holding 3 and padded with blanks; 126 package cards is **14**; 12 Speed cards is **2**, the second holding 3 and padded with blanks. |
| Bleed | 3 mm on the outer edge only; cards abut inside the grid | Neighbours share a cut line, so no bleed is wasted between them and a single cut serves two cards. |
| Cut marks | Hairline marks in the outer margin, at every grid line, never across a card | A mark that crosses the card is printed on the card. Marks in the margin survive a guillotine and a craft knife. |
| Fold marks | None | Cards are cut, not folded. Boards are printed one to a face. |
| Colour | Everything readable in greyscale; a package family's colour is a strip **and** a printed package name | Home printers run out of one ink. A card that only says "Revenant" in purple stops saying it. |

### 5.4 Where it lives

A new static directory, `printshop/`, beside `viewer/` and `studio/`: `index.html`, one stylesheet, and ES
modules with no framework and no build step. It is **not** in the engine's layers, references nothing under
`src/`, and nothing references it.

It reads its inputs the way the hosted studio reads the catalogue (ADR 0023): the local CLI host serves the
directory and the built content, and `studio --export` already writes the same files beside the published
page, so the printshop published to Pages prints from what CI built. The transport is **injected**, exactly as
ADR 0024 requires of `backend.js`: the module takes its `fetch` as an argument, so a test drives it with a
stub.

A second source is possible now and was not when this was written: the table host serves the catalogue as
`CardFace` and `PackageCard` records, already in the printed words and already stamped with the hash and the
rule set. Reading those would put the screen and the print on one renderer of the words. Reading
`game.schema.json` keeps the generator free of a running host. Part 6, question 12.

### 5.5 The invariant

**Every sheet carries the content hash it was built from.** Concretely:

- every sheet's footer carries the full hash, the rule set, the sheet number and the date;
- **every card face** carries the first 6 characters of the hash next to its versioned id, because the failure
  this invariant exists to catch is a deck mixed from two content hashes, and a footer on a sheet that was cut
  up an hour ago catches nothing;
- the generator **refuses to emit anything** when the hash file is missing, or does not match the catalogue it
  was handed. A sheet with no hash is the one output that must not exist.

### 5.6 How it is tested

`node --test printshop/*.test.js`, added to the gate beside `node --test studio/*.test.js`. Pure modules, no
DOM, a fixture catalogue, a stub transport. What the tests hold:

| Behaviour | Why it is worth a test |
| --- | --- |
| A catalogue of N Spells produces exactly N faces, and no face for a Spell the catalogue does not have | The deck is the catalogue, including what `"enabled": false` pruned. |
| A face carries cost, targeting, every effect with its amount and Duration, every caster effect, the printed critical chance and its d20 threshold when it has one, and the package that teaches it with its level | This is Part 2 as an assertion. A missing Duration is a card that cannot be resolved. |
| A Regeneration effect prints as `Regen N a round, ...` on a Spell card face, and the Regeneration token's face keeps the word `Regeneration` | [2.2](#22-the-words): the abbreviation is the card body's, made to keep every line within 38 characters, and is not a second name for the Condition. |
| **No face carries an initiative or a prerequisite**, and none carries the talent tree's class | ADR 0059 and ADR 0056. A card that prints a rule the engine stopped applying is a card a table plays. |
| A package's level is its own `level`, and a starting Spell's is 0 | ADR 0058 superseded the tree depth of ADR 0034. A depth computed from the tree is a number the game does not consult. |
| A catalogue of N enabled Tiers produces exactly N package faces, each with its name, level, bonus, a `Needs` line naming every prerequisite (`Needs nothing` for none) and every Spell it teaches, and 2 x team size copies of each | The package card is where a purchase is checked. A missing prerequisite is a card that sells what the engine refuses. |
| The copy count is 2 x the rule set's team size for a Spell some Creature could know, and 0 for one it could not | A rule set change reprints the deck; it must not need an edit. |
| The Speed cards are 2 x team size of each face, the Quick face carries `No critical roll this Round`, and both faces share one back | Part 6, question 14: a Speed card whose back differs by face, or a Quick card without its cost, is a choice that is not hidden or not a trade. |
| The Round track has one space per Round up to the rule set's Round cap (20 in `playtest.rules.json`), and the pick marks are exactly the Rounds on it that `IsEvolutionRound` answers yes for, with the rule set's first Round and interval | The schedule is the rule set's, answered in one place (ADR 0056); a mat that worked out its own parity is a second schedule. A track shorter than the cap leaves the Round cap marker nowhere to go. |
| Rendering fails, loudly, when a body exceeds 4 printed lines, or any line wraps (is over the 38 characters a line holds), on either kind of card | The measurements in 2.3 and 4.1 hold for today's content, where no line wraps since the card prints `Regen` ([2.2](#22-the-words)) and the widest is 33 (`latch`). While the full word made `latch`'s caster line wrap, the test allowed 5 lines and one wrap. A tuning pass that lengthens a Duration or adds an effect, or an author who puts a fourth Spell in a package, must break the build rather than clip the card. |
| Every emitted sheet carries the hash, and a missing or mismatched hash produces no output at all | The invariant of 5.5, as a property over the whole output. |
| 9 cards a sheet, cards abutting, marks only in the outer margin, and a short last sheet padded with blanks rather than a wrapped card | A card split across two sheets is 264 cards of waste. The Spell deck has a short last sheet at `3c9eb083`: 264 is 29 sheets and 3 cards. |

Alongside them, the text contracts ADR 0024 keeps in C# (`tests/DownfallArena.Cli.Tests`) for the fact that
the host serves the new directory at all - that is a fact about the host, not about the module.

### 5.7 The one command

```bash
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst   # build the content and its hash
dotnet run --project src/DownfallArena.Cli -- studio                    # serve printshop/ and data/dst
# open http://127.0.0.1:5099/printshop/ and print to PDF
```

None of this runs today: the host serves no `printshop/`, because there is none. Phase 3 is done, per
plan.md, when that produces a print-and-play PDF from a clean checkout and the deck it prints matches the hash
of the content it was built from.

---

## Part 6. Open questions

Each one is a count or a choice this document cannot derive. None is answered here; questions 1 and 13 were
answered elsewhere, question 14 by the maintainer, and each says so. Question 16 is new with the content of
2026-10-04.

### 1. Which die

**Answered: a d20** ([d20-criticals.md](d20-criticals.md), built on 2026-10-07 as ADR 0100), for the reason in
[1.6](#16-dice) - two candidate grids sit inside the 0.05 step `knobs.json` already declares on 25 Spells,
d10 and d20, and the d20 is the finer of the two: it moves 7 of the 24 Spells that roll where the d10 moves
14 (8 of 25 and 15 at `e6f72578`), its worst move is 0.02 rather than 0.05, and it can still express the 0.75 `crushing_stomp` is on. ADR 0063 put a
second use on the same die, the Roll-off. The question stays here for what is left with it:
`lightning_bolt`'s knob band starts at 0.17, so its own grid contains no multiple of 0.05. That is a content
change with a journal entry and a new hash. (`revenant_guards`, which printed a chance with no critical
chance knob, prints 0 since 2026-10-04.)

### 2. The deck's copy count

- **264 cards** (this manifest). Any legal game is playable. 30 sheets, which is most of the print-and-play.
- **Six copies of the two starting Spells and two of each of the other 42: 12 + 84 = 96 cards, 11 sheets.**
  (102 cards and 12 sheets while the starting kit was 3 Spells.) A Match
  uses at most 92 cards ([1.1](#11-spell-cards-and-package-cards)), so this is enough for almost every
  game - and a game where three Creatures buy the same package runs out, which is a rule change by the back
  door and fork A forbids it. How often that game happens is the measurement question 7 waits on for the
  package cards: how often one Tier is owned by more than two Creatures.
- **One card a Spell plus a hidden intent device** (a two-digit chit pair or a dial per Creature, reading a
  catalogue number 1 to 44). 44 cards, and the Intent stops being a card: every declaration becomes a lookup,
  and the thing that translates best in the whole game is the thing that gets worse.

### 3. Health is 30 and moving

The Health rail is printed 0 to 30 from `baseHealth`, in two rows. The move this question warned of has
happened once: ADR 0068 took `baseHealth` from 20 to 30. Nothing was printed yet, so it cost an edit and not
six boards; the shaded rail to 30 this question offered would have absorbed it. ADR 0068 measured 20 to 40
and chose 30 as the lowest value inside its band, so a later move up is possible. Print the rail to 30 now,
or to 40 with the space past 30 shaded? Two rows to 40 are 21 cells, 105 mm, past the 95 mm a row holds, so
the second costs a third row of board height, which question 15 is already short of.

### 4. The energy overflow chit

The track ends at 40 because 2 a Round for 20 Rounds, the table's cap, is the gain no play can refuse. One
overflow chit a Creature is in the box on the reasoning that a second means banking over 80. Is one chit a
Creature right, or should the box carry the theoretical rate (8 a Round for 20 Rounds is 160, so 4 chits a
Creature; it was 14 a Round, 280 and 7 chits, until ADR 0078 took `momentum`'s Energy regeneration out) and
accept the punch-out?

### 5. The condition supply, and what a supply that runs out means

The supplies in [1.4](#14-condition-tokens) are **one Round at the maximum rate**. The rule's own ceiling is up
to three times that for the Durations over one Round - 54 Bleed-2 tokens - which 20 Health made
unreachable and 30 does not ([1.4](#14-condition-tokens)), though only if both Players play for it. The
20 blanks cover 20 of the 36 tokens past that supply. Since 2026-10-05 a second face goes past its supply:
`crazed_specter` takes the Bleed-4 face to 48, 24 past it, over two Rounds ([1.4](#14-condition-tokens)), and
the same 20 blanks cover 20 of those. Three answers: print one Round's worth and carry the
blank-token escape (this manifest); print the rule's ceiling - each face's supply times its own longest
Duration, which is 330 condition tokens at `ad3e4d00` (294 at `e6f72578`, 204 at `813bb91b`), the Stun's 12
counted once since it cannot stack - and one more sheet; or bound the rule, which is an engine change and
belongs to ADR candidates 2 and 3, not here.

### 6. Where the rule set comes from

`RuleSet.Default` is a static in `src/DownfallArena.Domain/Matches/RuleSet.cs`. The table host now reads a
rule set from a file (`table --rules`, `RuleSetFile`), and `docs/tabletop/playtest.rules.json` is one, with
seven fields since ADR 0056 added the schedule. So the generator has a file to read, and [5.1](#51-inputs)
takes it. What is still open is where the file belongs: in `docs/tabletop/` as a playtest setting, in `data/`
where the engine would read it as content, or written out by `studio --export`. The second makes the rule set
content, which is a decision with consequences well beyond a print sheet. `RuleSetFile`'s own comment points
here.

### 7. How a table records who owns which package: cards, or a mat

Every choice below records the same thing publicly: the Tiers each Creature owns. It is what the prerequisite
check reads, what tells a Player what an opposing Creature knows, and what stops a Creature buying a Tier
twice.

- **Package cards**, face up with their Creature (this manifest, [Part 4](#part-4-the-packages-as-an-object)):
  126 cards, 14 sheets. The record sits with the Creature, and it is what translation.md's verdict and the
  rulebook's §5.3 describe. The prerequisite check is a read down one stagger.
- **Two package mats**, one a Player: a box per Tier in three columns by level, three pip boxes in each, one
  pip a purchase. 2 sheets and 40 pips instead of 14 sheets: a Player makes at most 2 picks at each of 10
  opportunities in 20 Rounds, so 20 pips a Player. The whole Team's progress and the families are one glance,
  and the record is away from the boards. The rulebook would change its step 1 from "card" to "pip".
- **Fewer package cards.** 126 is the rule's ceiling, and it is reachable. A Match lays out at most 40. How
  often one Tier is owned by two, three or six Creatures in a real Match is the `tabletop-mathematician`'s
  measurement (translation.md says so), and a smaller box is a question only that measurement can open, on the
  same terms as question 2: a copy count that a legal game can exceed is a rule change by the back door.

The first is this manifest's choice because it answers the verdict as written. The second is a playtest
reading. The third waits on a measurement.

### 8. Card backs

A single back design costs 30 more sheets for the Spell cards, 14 for the package cards and 2 for the Speed
cards, 46 in all, and nearly doubles the print, for a deck whose hands are concealed and whose Intents are
played face down - so the Spell card backs must at least be uniform. The Speed card's back is not optional at
all: it is what hides the choice ([2.6](#26-the-speed-card)), so it goes with the Spell cards on either
answer. Package cards are never hidden, so they need a back only to be told apart from Spell cards in a pile;
the heavy band of [4.1](#41-the-package-card) already does that. Printing single-sided and sleeving the Spell
and Speed cards with an opaque backing card is the cheaper answer and needs sleeves. Which one the
print-and-play assumes changes the sheet count from 55 to 87 (30 + 2 back sheets), or to 101 with package card
backs too. A double-sided print would also let the 6 Immune tokens become the back of the 12 Stun tokens and
leave the box ([1.5](#15-the-rest-of-the-pieces)); single-sided, they are pieces of their own.

### 9. The schedule: printed marks or placed markers

The Round track prints 10 pick marks, on the Rounds the table's schedule makes opportunities
([3.6](#36-the-round-track)). A schedule with another first Round or interval reprints the track, which is one
sheet. The alternative is the Round cap's answer: pick markers placed at setup, as many as the schedule has
opportunities in 20 Rounds (10 at interval 2, 20 at interval 1, so the box would carry 20). Which one depends
on whether the schedule is a setting a table changes between Matches, like the cap, or a rule it plays, like
the picks themselves. That is the maintainer's to say, and it is the same question as where the rule set file
lives (question 6).

### 10. The class on a Spell card

The spec prints the package that teaches a Spell in the card's head, and not its `creatureClass`
([2.1](#21-what-is-printed-and-where-it-comes-from)): the two sets of names agree on none of the 42 taught
Spells at `9419f935`, and until the packages were renamed some collided, so `tornado` would have printed
`Berserker` while the package then named `Berserker` did not teach it. The table app prints the class (`cardHead` in `table/card.js`, from `CardFace.CreatureClass`). The screen
and the deck should say the same thing. Which is it? And does the class mean anything a player needs, now that
the talent tree gates nothing? ADR 0058 leaves "what the tree is for" open, and this is one place that answer
lands.

### 11. The Base initiative rail and the bonus knobs

The tens rail runs 0 to 3 because the most Base initiative one Creature can buy in 20 Rounds is 30 at
`e6f72578`, for a Base of 35 (29 and 34 at `813bb91b`, 28 and 33 before tune run 11,
[3.4](#34-initiative-two-small-rails-instead-of-one-long-one)): 10 packages, one an opportunity (ADR 0066).
A package's `initiativeBonus` may be a balance knob (ADR 0061), with a declared `max` in
`data/balance/knobs.json`. At `813bb91b` every package had one, and at every `max` the ceiling was
5 + 48 = 53. At `e6f72578` only Blighted's is declared, 1 to 4, and at its `max` the ceiling is
5 + 31 = 36, inside the rail. Tune run 11 was the first pass to test the first answer: it raised the
ceiling by one, and the rail to 3 absorbed it with 5 left; the content of 2026-10-04 raised it by one more,
with 4 left. Print the tens rail to 3 and reprint six boards when a pass takes the ceiling past 39, or print
it to 5 (16 cells, 80 mm, the rail printed before packages) so no pass reprints anything if the knobs come
back? Inside today's declared bounds the first answer reprints nothing. It is question 3's shape, with a
derived bound instead of a guessed one. ADR 0066 made the second answer cheaper: in 16 Rounds it was 18
cells to cover 76. The 20-Round cap made it dearer again by one cell: in 16 Rounds it was a rail to 4, 15
cells, to cover 45.

### 12. Where the generator reads the card words from

[5.1](#51-inputs) reads `game.schema.json`. The table host already renders the same content into card words
(`CatalogueProjection`, `CardFace`, `PackageCard`), stamped with the hash and the rule set. Two renderers of
the same words will drift; the head line of question 10 is a drift that has already happened, and `Regen`
([2.2](#22-the-words)) is a second, deliberate one: the print abbreviates where the screen's `EffectLine`
does not. Should the
generator read the host's catalogue, so the screen and the print cannot disagree, at the price of needing a
running host to print?

### 13. The hidden Tie order at a table

**Answered in the rulebook.** ADR 0063 hides a Tie order until both Players have given theirs, like a Speed
choice. This manifest answers it with 6 tie order chits, face down on the tied Creatures' boards
([1.5](#15-the-rest-of-the-pieces), [3.5](#35-the-initiative-track)), which is translation.md's smallest
answer, and the rulebook now says the same: its setup hands each Player the chits, and its §5.5 and §6.6 lay
them face down and turn them together. What stays here is the maintainer's option: if a table need not hide
the order, the chits leave the box and those sentences with them.

### 14. A two-sided Speed token cannot be placed face down

**Answered by the maintainer, 2026-09-23: one card per Speed, placed face down** ("une carte par vitesse
posée face cachée"). Each Creature gets two Speed cards, a Quick card and a Standard card behind one common
back: 12 in all, 6 per Player. The Player lays the one they choose face down in the Creature's Speed slot and
keeps the other, and both Players turn theirs together. It is the Intent's own pattern, a card from a hand
played face down. The Quick card carries the reminder that a Quick Creature rolls no critical that Round
(`ResolutionRules.CriticalChanceOf`). This manifest now says so in [1.5](#15-the-rest-of-the-pieces),
[2.6](#26-the-speed-card), [3.1](#31-the-creature-board), [3.5](#35-the-initiative-track) and
[3.7](#37-the-player-area-and-where-a-face-down-intent-sits), and the rulebook's setup, §5.4 and §6.4 say the
same.

The question as it was found, while choosing the tie order chits: [1.5](#15-the-rest-of-the-pieces) specified
one Speed token a Creature, Quick on one face and Standard on the other, "made face down". A token whose two
faces are the two answers shows one of them whichever way it lies, so the choice is not hidden. The two
answers offered were two faces behind one common back (two a Creature, one played and one kept), or the six
tokens played under a cover. The maintainer chose the first, as cards.

What stays here is layout, not rule. The Speed slot now holds a poker card, landscape, so it takes 65 mm of
the creature board's height ([3.1](#31-the-creature-board)), which question 15 has to fit. And a 15 mm Stun
token occupies a card-sized slot without filling it, so the slot prints the token's place rather than being too
small for anything else. The count moved from 6 to 12 and the paper from 47 to 49 sheets.

### 15. The player area does not hold what 3.7 puts on it

Found while placing the package cards, and older than them. [3.1](#31-the-creature-board) calls the creature
board "A5, 105 x 148 mm, two to an A4 sheet", but A5 is 148 x 210 mm, and 105 x 148 mm is A6, four to a sheet.
Either way the A4 landscape player area of [3.7](#37-the-player-area-and-where-a-face-down-intent-sits),
297 x 210 mm, cannot hold three boards side by side with an 88.9 mm intent slot below each: three A6 boards in
portrait are 315 mm wide, and a board over an intent slot is 237 mm tall. The package stagger adds 10 mm a
card below that. Which gives: the board's size (and the sheet count of 3 that follows from "two to a sheet"),
an A3 player area, or a player area that is a printed guide for the table rather than a mat that holds the
pieces? This is a layout question, not a rule, and the counts in Part 1 do not depend on it except the 3
board sheets.

Question 14's answer adds to it on the board and not on the mat. The Speed slot now holds a poker card
landscape, 90 x 65 mm, so a board has 65 mm less height for its rails and its dock
([3.1](#31-the-creature-board); its `Targeted by` row too, until ADR 0083 retired it): on the A6 board that is
44% of it, on an A5 board 31%. The card a Player keeps
goes in the concealed hand, so the player area holds nothing new.

ADR 0068 adds to it on the board too. The Health rail runs 0 to 30 in two rows
([1.3](#13-stat-markers-and-the-rails-they-ride)), so the board gives one more row to its rails. The rail at 20
did not fit either: one row of 21 cells is 105 mm at the 5 mm a cell of
[3.4](#34-initiative-two-small-rails-instead-of-one-long-one), past the 95 mm a row holds, and the drawing in
[3.1](#31-the-creature-board) did not say so. The player area and the counts in Part 1 are unchanged.

The 20-Round cap adds to it once more. The Energy rail runs 0 to 40 ([1.7](#17-the-energy-track-what-ends-it)):
41 cells, and two rows of 21 would be 105 mm, so it takes three rows where it took two. That is one more row
of board height, and nothing else: the counts in Part 1 are unchanged.

### 16. The Defense rails' end, and the rule it was derived from

Found while re-reading the content of 2026-10-04. [3.3](#33-defense-two-rails-because-the-floor-is-applied-once)
ends both Defense rails at 20 by the rule "the largest Damage times the critical multiplier", which was
10 x 2.0. At `e6f72578` the largest Damage is 11 (`hateful_sacrifice`, since PR #246), so the rule reads 22.
But since ADR 0076 the buffs read at most 10, and base Defense is 0, so no Creature's Defense reads above 10:
the rule's end no longer marks anything a Creature can reach. Three answers: reprint both rails to 22 by the
rule as written (23 cells, past the 95 mm a row holds, so a second row of board height, which question 15 is
short of); keep 20 and give it another rule, since past 10 the buff rail only keeps count of what is held
(ADR 0076) and the overflow chit takes over at its end anyway; or end the buff rail at the 10 ADR 0076 lets
it read and count the rest in chits. The first applies this document's rule as written, the other two change
the rule beside the count, so it is the maintainer's call. The counts in Part 1 do not depend on it.

---

## Part 7. Coverage: the "needs a component" rows

Every **needs a component** verdict in [translation.md](translation.md), and what answers it. The row names
are translation.md's as it reads on this branch after its package re-audit, its ADR 0072 re-read and its
ADR 0078 re-read, its spell rows re-read for the content of 2026-10-04, and its `night_raid` and
`crazed_specter` rows for that of 2026-10-05: 18 from Part 1, 6 from Part 2, 24
from Part 3; 48 of 48. ADR 0078 took two rows out, the
`EnergyRegeneration` kind and the `momentum` Spell, because no card places an Energy regeneration any more
and their verdicts are no longer **needs a component** ([1.4](#14-condition-tokens)). The re-audit was
written alongside this document, so if a row name has moved since, the component beside it has not. ADR 0083
emptied one row without the audit being re-run: 1.8's target markers left the box, and the row is kept below
with what answers it now. The content of 2026-10-04 emptied two more the same way, also without a re-run:
no Spell places an Initiative buff, and `death_squad` is gone. Both rows are kept below with what answers
them now, and the Spells that place a token but are not among the audit's rows follow its 18.

### The 18 sub-phase rows

| translation.md row | Component |
| --- | --- |
| 1.1 Energy gain per Round | The Energy rail, [1.3](#13-stat-markers-and-the-rails-they-ride) and [1.7](#17-the-energy-track-what-ends-it) |
| 1.1 Energy has no maximum | The Energy rail's end at 40 and the overflow chit, [1.7](#17-the-energy-track-what-ends-it) |
| 1.3 An opportunity at Round 1 and every second Round after | The 10 pick marks on the Round track, [3.6](#36-the-round-track) |
| 1.3 Two picks an opportunity, per Player, shared across the Team | 4 Evolution pick tokens, [1.5](#15-the-rest-of-the-pieces); a token that buys lies on the buyer's board until the Sub-phase ends, [3.1](#31-the-creature-board), which also carries the restate row "A Creature buys at most one Tier an opportunity" |
| 1.3 A pick buys a whole Tier | 126 package cards, face up with the Creature that bought them, [1.1](#11-spell-cards-and-package-cards) and [Part 4](#part-4-the-packages-as-an-object). Each card's `Needs` line is the prerequisite check; no Spell card prints a gate. |
| 1.3 A purchase raises Base initiative by the Tier's initiative bonus, once, for the Match | The two Base initiative rails and the package card's bonus, [3.4](#34-initiative-two-small-rails-instead-of-one-long-one) and [4.2](#42-how-a-purchase-reaches-the-hand-and-how-the-bonus-is-recorded). No Spell card prints an initiative. |
| 1.4 One Speed choice per living, unstunned Creature | 12 Speed cards, a Quick and a Standard a Creature behind one back, one laid face down and one kept, [1.5](#15-the-rest-of-the-pieces) and [2.6](#26-the-speed-card); the card-sized Speed slot a Stun token occupies, [3.1](#31-the-creature-board). Part 6, question 14, answered by the maintainer |
| 1.5 The Combat timeline | The initiative track and 6 numbered markers, [3.5](#35-the-initiative-track) |
| 1.5 Current initiative is Base plus buffs less debuffs, floored at zero | The Base initiative rails read with the dock's Initiative tokens, [3.4](#34-initiative-two-small-rails-instead-of-one-long-one) |
| 1.5 A tie between the sides is rolled off on a d20 | The two d20s, [1.6](#16-dice), and the tie rules printed on the initiative track, [3.5](#35-the-initiative-track). The number on each board, [3.1](#31-the-creature-board), only fixes the order tied Creatures roll in. |
| 1.6 Tie orders are hidden until both are in | 6 tie order chits, face down on the tied Creatures' boards, [1.5](#15-the-rest-of-the-pieces) and [3.5](#35-the-initiative-track) |
| 1.8 Reveal in timeline order, bind targets at reveal | **No component since ADR 0083.** It was 18 target markers and the `Targeted by` row. The reveal and the resolution are one turn of `Activation` now, so the Spell card turned face up in its intent slot and the targets named aloud are the whole of it, [3.8](#38-how-a-cast-is-declared-and-resolved-in-components). translation.md keeps the row as it measured it, with a note. |
| 1.9 One critical roll a cast | The die, [1.6](#16-dice), and the card's printed chance |
| 1.9 Total Defense is base plus buffs less debuffs, floored at zero | The two Defense rails, [3.3](#33-defense-two-rails-because-the-floor-is-applied-once) |
| 1.9 A lasting Effect attaches as a Condition per its Stacking policy | The 168 Condition tokens and the dock, [1.4](#14-condition-tokens) and [3.2](#32-the-condition-dock-and-the-countdown) |
| 1.9 `Stack` adds another Condition | The same, plus the supply rule and the blank tokens |
| 1.10 Every Condition counts one Round down and expires at zero | The dock's four lanes and the two-step Cleanup, [3.2](#32-the-condition-dock-and-the-countdown) |
| 1.10 A Creature whose Stun ends is immune to Stun for the next Round | 6 Immune tokens, [1.5](#15-the-rest-of-the-pieces), swapped for the Stun token in lane `1` and taken off by the next Cleanup's slide, [3.2](#32-the-condition-dock-and-the-countdown) |

### The 7 effect kinds

| Effect kind | Token, and its supply |
| --- | --- |
| `Bleed` | 66 tokens: 6 at 1, 18 at 2, 18 at 3, 24 at 4 (6 at 4 until 2026-10-05) |
| `Regeneration` | 30 tokens: 6 at 1, 18 at 2, 6 at 3 |
| `Stun` | 12 tokens, two a Creature: a Stun on a stunned Creature is ignored, so a Creature carries one, and it needs a token in the Speed slot and one in the dock. Plus 6 Immune tokens, one a Creature, for the Round of Stun immunity after it ([1.5](#15-the-rest-of-the-pieces)) |
| `DefenseBuff` | 30 timed tokens (6 at +1, 6 at +3, 18 at +4); a permanent buff moves the rail and needs none |
| `DefenseDebuff` | 18 timed tokens at -2; a permanent debuff moves the rail |
| `InitiativeBuff` | **No token** since the content of 2026-10-04: no Spell places one, so the 18 at +2 left the box ([1.4](#14-condition-tokens)). The rule stays, read with the Base rails, [3.4](#34-initiative-two-small-rails-instead-of-one-long-one). translation.md's row reads **keep as is** since its re-read of 2026-10-04. |
| `InitiativeDebuff` | 12 tokens: 6 at -3, 6 at -5 |

### The 18 spells

Each of the 18 Spells the audit sent to phase 3, and what one cast of it puts on the table. Permanent halves
move a rail and place nothing. `momentum` was the 19th until ADR 0078: it places nothing now, and its
`Damage 3` and `Caster: Energy +2` move two rails that are already on the board. Read at `ad3e4d00`, where
the `night_raid` and `crazed_specter` rows moved. translation.md re-reads those two rows at `ad3e4d00` and
keeps both verdicts: `crazed_specter` still **needs a component**, now four Bleed tokens a cast, and
`night_raid` is still **restate**.

| Spell | What a cast places |
| --- | --- |
| `full_plate` | Nothing. +3 on its own Defense buff rail, permanent |
| `guard` | 1 Defense buff +1 (2 rounds); +1 on the rail, permanent |
| `thundering_seal` | 1 Defense buff +3 (2 rounds); +3 on the rail, permanent |
| `healing_screech` | 1 Regeneration 3 (2 rounds) |
| `death_squad` | **Gone** since 2026-10-04. `night_raid` took its place in Deathstalker and places nothing: its `Damage 4` and `Energy -3` on up to 2 enemies move rails already on the board (`Damage 3` and `Energy -2` on up to 3 until 2026-10-05) |
| `poison_slash` | 1 Bleed 3 (1 round) |
| `protective_slam` | 1 Stun (1 round), two tokens: one in the Speed slot, one in the dock. It placed an Initiative debuff -2 until 2026-10-04 |
| `ice_spear` | 1 Stun (1 round), two tokens. It placed an Initiative debuff until PR #245 |
| `mortal_wound` | 1 Bleed 4 (2 rounds) |
| `tranquilizer_dart` | 1 Stun (1 round) and 1 Bleed 1 (2 rounds) |
| `crushing_stomp` | 1 Stun (2 rounds) |
| `psycho_rush` | 1 Defense debuff -2 on its own caster |
| `infectious_blast` | Nothing. -3 on up to 3 enemy Defense debuff rails, permanent |
| `summon_minions` | Up to 3 Bleeds 2 (3 rounds), one of the longest Durations in the game |
| `revenant_guards` | Up to 3 Defense buffs +4 (2 rounds) and 1 Bleed 4 on its own caster; +3 on up to 3 rails, permanent. The heaviest cast, with `crazed_specter`: 4 tokens and 3 rail moves |
| `noxious_cure` | Up to 3 Defense debuffs -2 |
| `crazed_specter` | Up to 3 Bleeds 4 (2 rounds), and 1 Bleed 4 on its own caster (1 round); up to 3 Health rail moves for its `Damage 4`. 4 tokens and 3 rail moves, as heavy as `revenant_guards`. Until 2026-10-05 it placed the caster's Bleed alone |
| `toxic_waves` | Up to 3 Bleeds 3 (2 rounds) |

The Spells that place a token and are not among the audit's 18, because they were authored or reworked after
it (PR #245 and the content of 2026-10-04). translation.md has rows for all of them since its re-read of
2026-10-04. The components that answer them are the ones above.

| Spell | What a cast places |
| --- | --- |
| `ambush` | 1 Initiative debuff -5 on its own caster (1 round). It replaced `shadowstep`, which placed an Initiative buff +2 there |
| `frostbite` | 1 Initiative debuff -3 (1 round) |
| `meteor` | Up to 3 Bleeds 2 (1 round) |
| `latch` | 1 Bleed 1 (3 rounds), and 1 Regeneration 1 on its own caster (3 rounds) |
| `bone_ward` | 1 Defense buff +3 (2 rounds), and 1 Bleed 1 on its own caster (1 round) |
| `shield_bash` | 1 Defense buff +3 on its own caster (3 rounds) |
| `soothing_chant` | Up to 3 Regenerations 2 (2 rounds) |

---

## What this document does not decide

- The die. It is a d20, settled in [d20-criticals.md](d20-criticals.md) and built as ADR 0100, not here
  ([Part 6](#part-6-open-questions), question 1).
- The evolution rules. A package, its prerequisites, its bonus and the schedule are ADR 0056 and the content
  in `data/Tiers/`; this document counts what they need and changes none of them.
- Any rule. Where a component could not be built without one - a cap on Energy, a bound on permanent Defense -
  the question went back to the audit's ADR candidates 2 and 3 and to the `boardgame-director`, not into a
  component that quietly does something else.
- The rulebook's words. Phase 4 owns the teaching order, the examples and the player aid; this document owes
  it the printed reminders in [3.6](#36-the-round-track) and the affordance table in
  [3.1](#31-the-creature-board), which are the rules the components are already carrying.
