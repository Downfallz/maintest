# Components and print-and-play

Status: **Specification** (2026-09-14; brought up to the package model 2026-09-23). Phase 3 of
[plan.md](plan.md). It answers the **needs a component** rows of [translation.md](translation.md) and specifies
a generator. **The generator is specified, not implemented**: there is no `printshop/` directory, and nothing
under `tools/` or `scripts/` prints a sheet.

What is current, exactly:

- **Evolution is the package model.** A pick buys a whole Tier: every Spell in it and one initiative bonus
  ([ADR 0056](../adr/0056-a-pick-buys-a-package-every-other-round.md)). Two picks at Round 1 and every second
  Round after. The packages are authored in `data/Tiers/`
  ([ADR 0057](../adr/0057-a-package-is-authored-not-derived.md)). No Spell has an initiative of its own, and no
  Spell card prints one ([ADR 0059](../adr/0059-retire-the-spell-initiative-the-package-pays-it-now.md)).
- **A timeline tie is rolled off on a d20** between the sides, and each Player orders their own tied Creatures
  ([ADR 0063](../adr/0063-an-initiative-tie-is-rolled-on-a-d20.md)). The Creature number breaks no tie. It
  names the Creature, and it fixes the order tied Creatures roll in.
- **Every count is read at content `4d7a841c`** and the schedule in `docs/tabletop/playtest.rules.json`.
  Re-run the commands when the hash moves.

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
stacks except a Stun, which refreshes.

### How a count is marked

| Mark | Meaning |
| --- | --- |
| **RULE** | The count follows a rule of the game. It changes only if the rule changes. |
| **VALUE** | The count follows a `RuleSet` number or a content number a balancing pass may move. A move is a **reprint** of that component, never a redesign. |

A count is often both: 216 Spell cards is one card per Creature per Spell (RULE) times a team size of 3
(VALUE) times a catalogue of 36 (VALUE). Where that happens, the table names which input moves.

### What is given, and not decided here

From [plan.md](plan.md), phase 2 and the Decisions section:

- A **faithful port**. A rule that costs bookkeeping gets a component; nothing is dropped.
- A Match is **8 to 16 Rounds**. Everything sized per Round is built for **16** and says so.
- Two Players, **three Creatures each**, all six from `data/Creatures/main.v1.json`.
- The Creature's base Critical chance is **zero**. A Spell's printed chance is the chance rolled. The 15
  Spells at zero never roll.
- A Condition **stacks**, except a Stun, which **refreshes**. One application is one token.
- A critical is a **die roll** and the catalogue will be authored onto the die's grid. The die is a **d20**
  ([d20-criticals.md](d20-criticals.md), settled, not built); [Part 1.6](#16-dice) keeps what each candidate
  cost. A timeline tie between the sides is rolled on the same die (ADR 0063).
- Evolution buys **packages** (Tiers), two picks at Round 1 and every second Round after, and a package's
  prerequisites are the only rule for what a Creature may buy (ADR 0056).

The board this manifest is built on, and the commands that read it:

```bash
grep -n 'Default {' src/DownfallArena.Domain/Matches/RuleSet.cs   # new(3, 2, 2, 30, 2.0, 1, 2)
cat docs/tabletop/playtest.rules.json                             # the table's rule set: the same, with a 12-Round cap
cat data/Creatures/main.v1.json                                   # Health 20, Energy 0, Defense 0, Base initiative 5
find data/Spells -name '*.json' | wc -l                           # 36
ls data/Tiers/*.json | wc -l                                      # 21, none disabled
```

`RuleSet.Default` is 3 Creatures a Team, 2 Energy a Round, 2 Evolution picks an opportunity, the first
opportunity at Round 1 and one every 2 Rounds after it, a 30-Round cap, a critical multiplier of 2.0. The
table's rule set file (`table --rules`) has the same numbers and a 12-Round cap. The cap is the one value the
table replaces, and it is a setup: the Round track is built for 16.

The 21 Tiers: 3 at level 1 with two Spells each, 9 at level 2 with one Spell each, 9 at level 3 with two
Spells each. They teach 33 Spells, each exactly once. The other 3 are the starting kit, which no Tier
teaches.

---

## Part 1. What is in the box

Totals first, then the derivation of each line.

| Group | Pieces |
| --- | --- |
| Spell cards | 216 |
| Package cards | 126 |
| Boards and mats | 6 creature boards, 2 player mats, 1 initiative track, 1 round track |
| Condition tokens | 150 in 8 kinds |
| Markers and chits | 36 stat markers, 6 initiative markers, 6 tie order chits, 6 speed tokens, 4 pick tokens, 2 round markers, 18 target markers, 18 overflow chits, 20 blanks |
| Player aids | 2 |
| Dice | 2 d20 |
| Paper | about 47 A4 or Letter sheets |

The paper: 24 sheets of Spell cards and 14 of package cards (9 a sheet), 3 of creature boards (2 a sheet), 2
player mats, 1 for the initiative and round tracks, 2 of tokens (266 pieces, none over 15 mm, and about 185 to
a sheet at 15 mm), 1 of player aids. 47. Card backs would add 38 more; see Part 6, question 8.

What moved when evolution became packages, and why:

| Component | Before | Now | Why |
| --- | --- | --- | --- |
| Talent tree mat | 2 | **0** | The talent tree gates nothing (ADR 0056, ADR 0058). A mat that shows its gates would teach a rule the game does not have. |
| Package card | - | **126** | translation.md's verdict on "A pick buys a whole Tier": Tier cards, 21 kinds. A bought card lies face up with its Creature and is the public record of what it owns. [1.1](#11-spell-cards-and-package-cards), [Part 4](#part-4-the-packages-as-an-object). |
| Talent pips | 82 | **0** | The package card is the record, so nothing is marked on a mat. |
| Paper | 35 sheets | **47** | 14 sheets of package cards in, 2 talent tree mats out. |
| Pick tokens | 4 | **4** | Still 2 a Player, but only in a Round that offers an opportunity. |
| Round track | 16 spaces | 16 spaces, **8 pick marks** | The schedule is printed where a Player looks for the Round. [3.6](#36-the-round-track). |
| Base initiative, tens rail | 0 to 5 | **0 to 4** | The ceiling fell from 52 to 44. [3.4](#34-initiative-two-small-rails-instead-of-one-long-one). |
| Spell card foot | `Unlock: +N initiative`, `Requires: ...` | **neither** | ADR 0059 and ADR 0056. [2.1](#21-what-is-printed-and-where-it-comes-from). |
| Spell card head | class and tree depth | **the package that teaches it, and its level** | The class names collide with the package names, and the tree depth is a number the game no longer reads (ADR 0058). [2.1](#21-what-is-printed-and-where-it-comes-from). |
| Tie order chit | - | **6** | The Tie order is hidden until both Players have given theirs (ADR 0063). [1.5](#15-the-rest-of-the-pieces). |
| Condition tokens | 144 | **150** | Content, not packages: `ice_spear` lowers Initiative by 1 now, a new face. [1.4](#14-condition-tokens). |

### 1.1 Spell cards and package cards

| Component | Count | The rule beside the count | Follows |
| --- | --- | --- | --- |
| Spell card | **216** = 36 Spells x 6 copies | A card in a hand is what lets an Intent be played face down, so a Creature needs its own copy of every Spell it knows. Any of the six Creatures can come to know any Spell a Tier teaches: a package's prerequisites are the only rule, so multiclassing is free (ADR 0056), and both Players play the same Creature definition. Six copies is the ceiling. A Spell that is neither in the starting kit nor taught by an enabled Tier can never be known, and gets **no** copy; at `4d7a841c` there is none, so all 36 are printed. | 36 is a **VALUE** (content: 3 starting, 33 taught); 6 is 2 Players x team size 3, a **VALUE** (`RuleSet.TeamSize`); one copy per Creature that could know it is a **RULE** |
| Package card | **126** = 21 Tiers x 6 copies | A bought card lies face up with the Creature that bought it: that is the public record that it owns the Tier (rulebook §5.3). So a Creature needs its own copy of every Tier it owns. Any of the six Creatures may buy any Tier, since prerequisites are the only rule (ADR 0056), and the ceiling is reachable: a level-3 Tier costs a Creature 3 purchases, 9 for a whole Team, inside the 16 a Player makes in 16 Rounds. So all six Creatures can own the same Tier in one Match. 126 is exactly 14 sheets. | 21 is a **VALUE** (content, enabled Tiers); 6 is 2 Players x team size, a **VALUE**; one copy per Creature that could own it is a **RULE** |

What a Match actually consumes is smaller, and it is the number the open question in Part 6 is about. The
command below also gives the Base initiative ceiling that
[3.4](#34-initiative-two-small-rails-instead-of-one-long-one) uses. It tries every set of packages one
Creature can own (prerequisites included, 2^21 sets, a few seconds):

```bash
python3 -c "
import json,glob
R=json.load(open('docs/tabletop/playtest.rules.json'))
T=[json.load(open(p)) for p in glob.glob('data/Tiers/*.json')];T=[t for t in T if t.get('enabled',True)]
o=[r for r in range(1,17) if r>=R['firstEvolutionRound'] and (r-R['firstEvolutionRound'])%R['evolutionInterval']==0]
P=R['evolutionPicksPerOpportunity']*len(o);ix={t['id']:i for i,t in enumerate(T)};n=len(T)
need=[sum(1<<ix[q] for q in t['prerequisites']) for t in T];bb={};bs={}
for m in range(1<<n):
  own=[i for i in range(n) if m>>i&1]
  if any(need[i]&~m for i in own): continue
  k=len(own);bb[k]=max(bb.get(k,0),sum(T[i]['initiativeBonus'] for i in own));bs[k]=max(bs.get(k,0),len({s for i in own for s in T[i]['spells']}))
f=lambda d,k:max(v for j,v in d.items() if j<=k)
print('opportunities',o,'picks a Player',P)
print('most Spells a Player adds',max(f(bs,a)+f(bs,b)+f(bs,P-a-b) for a in range(P+1) for b in range(P+1-a)))   # a Team of 3
print('most Base initiative one Creature buys',f(bb,P))"
# opportunities [1, 3, 5, 7, 9, 11, 13, 15] picks a Player 16
# most Spells a Player adds 28
# most Base initiative one Creature buys 39
```

6 Creatures x 3 starting Spells = 18 cards in hands at setup. A 16-Round Match offers 8 opportunities, so a
Player makes at most 16 purchases. The most Spells 16 purchases add is 28: the 3 level-1 packages on each of
three Creatures (9 purchases, 18 Spells, two a purchase), then 4 level-2 packages and the 3 level-3 packages
above them (7 purchases, 10 Spells). So **at most 18 + 2 x 28 = 74 cards are in hands in a 16-Round Match**,
down from 82 when a pick bought one Spell every Round. The box still carries 216 because which 74 is a choice
the Players make, and six Creatures may all buy the same package. The same holds for package cards: a Match
lays out at most 2 x 16 = 32 of the 126, one a purchase.

### 1.2 Boards and mats

| Component | Count | The rule beside the count | Follows |
| --- | --- | --- | --- |
| Creature board | **6** | One per Creature in play: 2 Players x `RuleSet.TeamSize` 3. Each carries the Creature's number, 1 to 6: who it is on the track and on a target marker, and the order tied Creatures roll in. The number breaks no tie (ADR 0063). | **VALUE** (team size) |
| Player area mat | **2** | One per Player. A Match seats exactly two. | **RULE** |
| Initiative track | **1** | Six ordered slots, a Quick band above a Standard band. Six is the number of Activation slots a Round can have: one per living, unstunned Creature. | **VALUE** (team size) |
| Round track | **1**, 16 spaces, 8 pick marks | A Match is 8 to 16 Rounds (given). The track is printed for the top of that band. The Round cap marker is placed on the space equal to the `RuleSet`'s cap at setup. A pick mark is printed on every Round that offers an opportunity: Round 1 and every second Round after, so 1, 3, ..., 15 (`RuleSet.IsEvolutionRound`). | **RULE** for 16 (the given band); the cap marker and the pick marks are **VALUE**s (the cap; `FirstEvolutionRound`, `EvolutionInterval`) |

### 1.3 Stat markers and the rails they ride

One marker per rail per Creature. The rails are specified in [Part 3](#part-3-boards-and-tracks); here are the
counts and the ends.

| Rail | Markers | Where it ends, and why | Follows |
| --- | --- | --- | --- |
| Health | 6 | 0 to 20. `baseHealth` is 20 and `Creature.Heal` clamps to `MaxHealth - Health` (`Creature.cs:139`), so nothing goes above it. | **VALUE** (`baseHealth`) |
| Energy | 6 | 0 to 32. See [1.7](#17-the-energy-track-what-ends-it). | **VALUE** (`EnergyPerRound`) x **RULE** (16 Rounds) |
| Defense buffs | 6 | 0 to 20. See [3.3](#33-defense-two-rails-because-the-floor-is-applied-once). | **VALUE** (the largest Damage, the critical multiplier) |
| Defense debuffs | 6 | 0 to 20, the same reason mirrored. | **VALUE** |
| Base initiative, units | 6 | 0 to 9. | **RULE** (a decimal rail) |
| Base initiative, tens | 6 | 0 to 4. Together the two rails read 0 to 49, which covers the ceiling computed in [3.4](#34-initiative-two-small-rails-instead-of-one-long-one): a Base initiative of 44. | **VALUE** (the packages' `initiativeBonus`, the picks an opportunity, the schedule) |

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
| Bleed | 1 a Round | 18 | `toxic_waves` places one on each of up to 3 targets; 6 slots x 3 = 18 | **VALUE** (content) |
| Bleed | 2 a Round | 18 | `summon_minions`, up to 3 targets; 6 slots x 3 = 18 | **VALUE** (content) |
| Bleed | 3 a Round | 6 | `poison_slash`, one target; 6 slots x 1 | **VALUE** |
| Bleed | 4 a Round | 6 | `mortal_wound` on a target, `crazed_specter` and `revenant_guards` on their own caster; one each; 6 slots x 1 | **VALUE** |
| Regeneration | 3 a Round | 6 | `healing_screech`, one ally; 6 slots x 1 | **VALUE** |
| Energy regeneration | 2 a Round | 6 | `momentum`, Self only; 6 slots x 1 | **VALUE** |
| Stun | - | 12 | A Stun **refreshes**, so a Creature carries at most one, ever — but one Stun needs **two** tokens at once: one fills the Speed slot so no Speed token can go there ([3.1](#31-the-creature-board)), and one counts the Duration down in the dock ([3.2](#32-the-condition-dock-and-the-countdown)). A token cannot be in two places. Two per Creature. | **RULE** (the stacking policy, and the two places a Stun is shown) x **VALUE** (team size) |
| Defense buff | +1 | 6 | `guard`'s timed half, one ally; 6 slots x 1 | **VALUE** |
| Defense buff | +2 | 18 | `revenant_guards`' timed half, up to 3 allies; 6 x 3 | **VALUE** |
| Defense buff | +3 | 6 | `thundering_seal`'s timed half, one ally; 6 x 1 | **VALUE** |
| Defense debuff | -2 | 18 | `noxious_cure` on up to 3 allies; 6 x 3. `psycho_rush`'s caster debuff is the same face. | **VALUE** |
| Initiative buff | +2 | 18 | `death_squad`, up to 3 allies; 6 x 3 | **VALUE** |
| Initiative debuff | -1 | 6 | `ice_spear`, one enemy; 6 x 1 | **VALUE** |
| Initiative debuff | -2 | 6 | `protective_slam`, one enemy; 6 x 1 | **VALUE** |
| **Total** | | **150** | | |

Every amount in the catalogue is on this list and no other: Bleed is 1, 2, 3 or 4; Regeneration is 3; Energy
regeneration is 2; timed Defense is 1, 2 or 3; every Initiative buff is 2; an Initiative debuff is 1 or 2;
every Defense debuff is 2. That is why a token set this small covers a 36-Spell catalogue. The -1 face is new
at `4d7a841c` (`ice_spear` was -2 when this table was first read), and it is the whole of the change from
144 to 150.

**The supply is one Round at the maximum rate, and Durations run to 3.** A Bleed from `summon_minions` lives 3
Rounds, so the rule's own ceiling is three times the table above for that face: 54 Bleed-2 tokens. It is
unreachable at this Health scale - 9 Bleeds of 2 on one Creature is 18 damage a Round against 20 Health, and
Bleed ignores Defense - but the engine has no cap, so the rulebook carries the standard supply escape: **a
supply that runs out is replaced by a blank token with the value written on it; the game has no maximum.**
20 blanks are in the box for that. Part 6, question 5.

### 1.5 The rest of the pieces

| Component | Count | The rule beside the count | Follows |
| --- | --- | --- | --- |
| Speed token, Quick on one face, Standard on the other | **6** | One Speed choice per living, unstunned Creature (`SpeedRules.cs:13-45`). Two-sided because the choice is one of two and is made face down; a two-sided token cannot hide which, Part 6, question 15. The Quick face needs a reminder that it forfeits the Critical roll (`ResolutionRules.CriticalChanceOf`), since that cost is what makes the choice a choice. | **VALUE** (team size) |
| Initiative marker, numbered 1 to 6 | **6** | One per Creature, placed on the initiative track. The number names the Creature on the track; ids are handed out in join order (`Match.cs:286-296`), so 1 to 3 are Player 1's. It breaks no tie: a tie between the sides is a d20 roll-off, and tied Creatures roll in number order, which only fixes the order of the rolls (ADR 0063). | **VALUE** (team size) |
| Tie order chit, `1st`, `2nd`, `3rd`, one common back | **6** = 3 per Player | The Tie order is given by both Players at the same time and hidden until both are in (ADR 0063, "like a Speed choice"), so it needs something that commits face down. A Player lays one chit face down on each of their tied Creatures' boards, and both Players turn them together. A Player orders at most all of their own Creatures, `RuleSet.TeamSize` = 3; two separate ties are each read low number first, so 3 chits cover any Round. This is translation.md's smallest answer ("three ordinal chits a Player"). | **VALUE** (team size) x **RULE** (a hidden, simultaneous Tie order) |
| Evolution pick token | **4** | 2 per Player (`RuleSet.EvolutionPicksPerOpportunity`), put on the mat only in a Round with a pick mark on the Round track, spent one a purchase, and the rest returned when the Player passes. A Round with no opportunity gives nobody a pick (`RuleSet.EvolutionPicksIn`), so the tokens stay off the mat. | **VALUE** (picks an opportunity) |
| Round marker | **1** | One position on the Round track. | **RULE** |
| Round cap marker | **1** | Placed at setup on the space equal to the `RuleSet`'s Round cap, so the track's end is a component and not a memory. | **VALUE** |
| Target marker | **18** = 6 sets of 3 | Every Intent on the timeline is revealed and targeted **before any of them resolves** (`ActionRules.cs:16-52`, and `ActionResolution` is a later sub-phase), so all six casts have their targets on the board at once. 3 is the largest `maxTargets` in the catalogue: 25 Spells at 1, two at 2, nine at 3. Each set carries its caster's number. | **VALUE** (team size, `maxTargets`) |
| Energy overflow chit, +32 | **6** | One per Creature. See [1.7](#17-the-energy-track-what-ends-it). | **RULE** |
| Defense overflow chit, +20 and -20 | **12** | Six of each. The Defense rails are bounded by what can matter, not by the rule, and the rule has no bound: permanent Defense buffs and debuffs both stack (ADR candidate 3, open). | **RULE** (no bound exists) |
| Blank token | **20** | The supply escape of [1.4](#14-condition-tokens). | not derived; see Part 6, question 5 |
| Player aid | **2** | One a Player: the Round sequence, the timeline tiebreaks, the Condition timing, and the two orderings of [3.6](#36-the-round-track). Phase 4 writes what it says (plan.md); this manifest reserves the component and its sheet. | **RULE** |

### 1.6 Dice

One critical roll a cast (`ResolutionRules.cs:56`), at most 6 casts a Round, resolved one after the other in
timeline order. The roll-off of a timeline tie uses the same die (ADR 0063): at most 6 tied Creatures, rolled
one after the other in number order, before any Intent. **One die is enough by the rule**, for both. Two are
in the box so each Player rolls their own casts and their own Creatures in a roll-off, which is a convenience
and not a rule. The roll-off adds no die and no component: its result is the places on the initiative track.

Since the Creature's base chance is zero, only the Spells that print a chance roll at all:

```bash
python3 -c "
import json,glob,collections
v=collections.Counter(json.load(open(p))['criticalChance'] for p in glob.glob('data/Spells/**/*.json',recursive=True))
print(sorted(v.items()))"
# [(0, 15), (0.22, 1), (0.28, 1), (0.283, 1), (0.33, 3), (0.35, 1), (0.38, 2), (0.45, 2), (0.5, 5), (0.55, 1),
#  (0.617, 1), (0.75, 1), (0.767, 1), (0.8, 1)]
```

**21 of 36 Spells roll. 15 never touch a die.** Thirteen distinct chances are printed at content `4d7a841c`,
and the die's grid has to carry them. The table below is a reading, not a constant — the maintainer is tuning,
so re-run the command rather than trusting the cells. What each candidate costs, snapping each of the 21 to
the nearest face:

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
| d6 | 16 of 21 | 0.0833 | 0.0314 | **No.** 1/6 is not a multiple of 0.05 | Six faces for thirteen distinct chances, the largest mean error of the five, and a finest distinction of 16.7 points |
| d8 | 15 | 0.05 | 0.0232 | **No** | Ties the d10 exactly at this content - same moves, same worst, same mean - so nothing separates them but the grid, which the d10 is on and it is not; and a die many households do not have |
| d10 | 15 | 0.05 | 0.0232 | **Yes**, 0.1 is a multiple of 0.05 | Ten faces for thirteen chances, and it cannot express 0.75, where `crushing_stomp` sits today |
| d12 | 15 | 0.0367 | 0.0180 | **No** | Values no declared knob can reach, and it no longer buys the smallest mean error either |
| **d20** | **10** | **0.02** (`tornado` 0.38 to 0.40, and six others by the same amount) | **0.0091** | **Yes**, exactly the declared step | One die, one reading, thresholds in whole numbers |
| d100 (2 dice) | 3 | 0.003 | 0.0004 | No, 0.01 | Two dice and a percentile read per cast, and it keeps the arbitrary precision fork B exists to remove |

**Settled: a d20** ([d20-criticals.md](d20-criticals.md)), **and the card prints both the chance and the
threshold** ("Critical 50% - d20: 11+",
which is five Spells as the catalogue stands). The recommendation survives tune run 8, and on better terms
than it was made: the d20 now wins the error as well. It moves the fewest Spells (10 of 21), has the smallest
worst move (0.02) and, since run 8 pulled `crazed_specter` and `protective_slam` off the d12 grid, the
smallest mean error of the five single dice. The load-bearing reason is still the grid rather than the error,
because the error moves with every tuning pass and the grid does not: `data/balance/knobs.json` already
declares `/criticalChance` a knob with a **step of 0.05 on 21 Spells**, so a d20 snap is inside the search
space a tuning pass already has, and d6, d8, d12 and d100 are not.

```bash
python3 -c "
import json,collections
k=json.load(open('data/balance/knobs.json'))['spells']
s=[(n,b) for n,e in k.items() for b in e.get('knobs',[]) if 'criticalChance' in b['path']]
print(len(s), collections.Counter(b['step'] for _,b in s))"   # 21 Counter({0.05: 21})
```

One of the 21 is `throwing_star`, which prints 0 today; the other 20 are Spells that roll.

Two findings the maintainer owns before the snap is authored, neither of them this document's to decide:

- **One Spell that prints a chance has no critical chance knob**: `revenant_guards` (0.33). A snap would move
  a number no declared knob covers.
- **Nine of the 21 knobbed Spells are already off their own declared grid**: their printed value is not their
  band's `min` plus a whole number of steps. `pummel` 0.767, `protective_slam` 0.283, `crazed_specter` and
  `tornado` at 0.38, `engulfing_flames` and `toxic_waves` at 0.33, `noxious_cure` 0.28, `rejuvenate` 0.22, and
  `lightning_bolt` 0.617 in a band of `[0.17, 0.8]`. Eight of the nine sit on a band whose `min` **is** a
  multiple of 0.05, so a d20 snap fixes them outright. The ninth is `lightning_bolt`, whose band floor 0.17 is
  the only knob band off its own grid, and it leaves the band itself to be moved: 0.17 plus multiples of 0.05
  never lands on a multiple of 0.05.

### 1.7 The energy track: what ends it

Energy has no maximum in the engine (`Energy.cs:3`, `Creature.cs:147-160`), and the maintainer has settled that
the engine does not change: **the component is what ends it** (translation.md, ADR candidate 2).

**The track runs 0 to 32.** The rule beside the count: a living Creature gains `RuleSet.EnergyPerRound` = 2
every Round (`UpkeepRules.cs:13-22`), and a Match is at most 16 Rounds, so **2 x 16 = 32 is the Energy a
Creature banks by doing nothing at all**. That is the gain no play can refuse, and it is the honest end of a
printed track.

Four Spells in the catalogue touch Energy. Three of them can push a Creature above 32, and all three cost a
cast:

```bash
python3 -c "
import json,glob
for p in glob.glob('data/Spells/**/*.json',recursive=True):
  d=json.load(open(p))
  for e in d['effects']+d.get('casterEffects',[]):
    if 'Energy' in e['kind']: print(d['id'].split(':')[1], e)"
# wait EnergyGain 2 | restorative_burst EnergyGain 2 | momentum EnergyRegeneration 2 for 3 rounds | soul_devourer EnergyDrain 3
```

The most one Creature can gain in one Round is **14**: 2 from the Round, 6 from three overlapping `momentum`
Conditions of its own (Self-targeted, 3 Rounds, and the per-Round family stacks), 4 from its two allies each
casting `restorative_burst` on it, and 2 from spending its own Activation slot on `wait`.

**What a player does at the end of the track.** A Creature whose Energy would pass 32 takes an **Energy
overflow chit** worth 32 and its marker returns to 0. One chit per Creature is in the box: a second chit means
a Creature banking more than 64 Energy in 16 Rounds, which is an average of 4 a Round with nothing ever spent
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
| Head | The package that teaches it, and its level: `Lich . level 3`, or `Starting spell` | the enabled `tiers[]` whose `spells` name it; `creatures[].startingSpellIds` | Where the card is filed in the library, and which purchase brings it to a hand. It is not a gate: the package's gate is printed once, on its package card. A starting Spell belongs to no package and sits at level 0 (ADR 0058) |
| Body | Targeting, one line | `targeting.origin`, `scope`, `maxTargets` | Origin, scope and count are one sentence: `Self`, `One enemy`, `One ally`, `Up to 2 enemies`, `Up to 3 allies` |
| Body | One line per effect, with its amount and Duration | `effects[]` | |
| Body | One line per caster effect, prefixed `Caster:` and set below a rule | `casterEffects[]` | ADR 0031: once per cast, never multiplied, none of them on a Fizzle. Seven Spells carry one, and it must not read as a target effect |
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
package names are the Tiers', and at `4d7a841c` they disagree on 29 of the 33 taught Spells. Some collide:
`tornado` is authored under the class `Berserker`, and the `Berserker` package does not teach it (`Ravager`
does). A Spell card that says `Berserker` and a package card that says `Berserker` must mean the same
package. The table app still prints the class; Part 6, question 10.

Two rules that are **not** printed per card because they are true of every card, and belong on the player aid:
a Multi Spell may take fewer targets than its maximum (`TargetingRules.cs:49`), and `Ally` includes the caster
(`TargetingRules.cs:43`). Printing either on 36 cards costs a line each and teaches neither.

### 2.2 The words

The effect lines use the glossary's terms unchanged: `Damage`, `Heal`, `Energy`, `Bleed`, `Regeneration`,
`Energy regeneration`, `Stun`, `Defense`, `Initiative`, `Caster`, `permanent`. A per-Round effect reads
"N a round", which is the glossary's phrasing for a Bleed tick. A Duration reads "N rounds" or "permanent",
which is the Duration entry's own vocabulary.

| Effect kind | Printed as |
| --- | --- |
| `Damage` | `Damage 7` |
| `Heal` | `Heal 4` |
| `EnergyGain` / `EnergyDrain` | `Energy +2` / `Energy -2` |
| `Bleed` | `Bleed 4 a round, 2 rounds` |
| `Regeneration` | `Regeneration 3 a round, 2 rounds` |
| `EnergyRegeneration` | `Energy regeneration 2 a round, 3 rounds` |
| `Stun` | `Stun, 2 rounds` |
| `DefenseBuff` / `DefenseDebuff` | `Defense +3, permanent` / `Defense -2, 1 round` |
| `InitiativeBuff` / `InitiativeDebuff` | `Initiative +2, 1 round` / `Initiative -2, 2 rounds` |

### 2.3 The measurement

The card is a **standard poker card, 63.5 x 88.9 mm**. Which print constraint each choice answers:

- **63.5 x 88.9 mm**: the most common sleeve size, and the size a home printer's 3 x 3 grid fills on both A4
  and US Letter. See [5.3](#53-the-sheet).
- **5 mm margins**, so the text area is **53.5 mm** wide. 5mm is what survives a home printer's drift and a
  hand-held guillotine.
- **8 pt body text**, which is about **38 characters a line** at 53.5 mm in a humanist face.
- **4 lines of body text**, 14 mm, which is what is left after the head, the foot and the rule above the
  caster line. The foot is shorter than it was: the `Unlock` line and the one or two lines of `Requires` are
  gone. The body box keeps its 4 lines, because every card fits in 4, and the room the foot gave back is
  margin.

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
   'Bleed':f'Bleed {ap} a round, {dur(e)}','Regeneration':f'Regeneration {ap} a round, {dur(e)}',
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
# widest line (39, 'momentum')  lines per card [(2, 17), (3, 17), (4, 2)]
# body     max (95, 'revenant_guards') median 38.0 min (16, 'wait')
# statline max (119, 'revenant_guards') median 64.5 min (41, 'rejuvenate')
```

| Reading | Value | What it means for the layout |
| --- | --- | --- |
| Body lines per card | 2, 3 or 4 | 17 cards at 2, 17 at 3, 2 at 4. The 4-line box is enough for every card in the catalogue. |
| Widest single line | **39 characters** (`momentum`: `Energy regeneration 2 a round, 3 rounds`) | One character over the 38 a line holds. It wraps to a second line with a 3 mm hanging indent, and `momentum` has only 2 lines, so the card has the room. Nothing else in the catalogue wraps. |
| Whole body, one string | max **95** characters (`revenant_guards`), median **38**, min **16** (`wait`) | 95 characters is under three full lines. No card is tight on the body alone. |
| Whole statline (cost, targeting, effects, caster, critical) | max **119** (`revenant_guards`), median **64.5**, min **41** (`rejuvenate`) | The statline is never printed as one string - it is spread across the head, the body and the foot - so this is a total, not a line length: 119 characters over a head, four body lines and a foot. It was 143 while it carried the `Unlock` line. The audit reached the same conclusion on a rendering of its own, and it does not depend on the join: nothing overflows. |

### 2.4 The seven that need a second sentence

The audit flagged seven: `revenant_guards`, `crazed_specter`, `psycho_rush`, `summon_minions`,
`soul_devourer`, `thundering_seal`, `guard`. They are the Spells that carry either a **Caster effect**, which
must not be read as a target effect, or **two Conditions of one kind** on the same target, which must not be
read as one. Here is every one of them, line by line, with the character count of each line against the 38 a
line holds:

| Spell | Body lines | Longest line | Lines used of 4 |
| --- | --- | --- | --- |
| `revenant_guards` | `Up to 3 allies` (14) / `Defense +2, permanent` (21) / `Defense +2, 1 round` (19) / `Caster: Bleed 4 a round, 1 round` (32) | 32 | **4** |
| `crazed_specter` | `Up to 3 enemies` (15) / `Damage 6` (8) / `Caster: Bleed 4 a round, 1 round` (32) | 32 | 3 |
| `psycho_rush` | `One enemy` (9) / `Damage 10` (9) / `Caster: Defense -2, 1 round` (27) | 27 | 3 |
| `summon_minions` | `Up to 3 enemies` (15) / `Bleed 2 a round, 3 rounds` (25) / `Caster: Damage 2` (16) | 25 | 3 |
| `soul_devourer` | `One enemy` (9) / `Damage 6` (8) / `Energy -3` (9) / `Caster: Heal 4` (14) | 14 | **4** |
| `thundering_seal` | `One ally` (8) / `Defense +3, permanent` (21) / `Defense +3, 2 rounds` (20) | 21 | 3 |
| `guard` | `One ally` (8) / `Defense +1, permanent` (21) / `Defense +1, 2 rounds` (20) | 21 | 3 |

Two of the seven use all four lines, and none of their lines is over 32 characters. **The layout that fits
them is one effect to a line.** Not prose: a line per effect, each with its own Duration, and a 0.3 pt rule
above the caster line. That is what makes `revenant_guards`' four separate things - two Defense Conditions on
up to three allies and a Bleed on itself - four things on the card instead of one sentence to parse. Two more
Spells carry a Caster effect and are not in the seven because their statline is short
(`hateful_sacrifice`, `parasite_jab`); they use the same rule and the same prefix.

### 2.5 Three card faces, written out

Real Spells, generated from `data/`. `[ ]` marks a printed zone. `4d7a84` is the first six characters of
the content hash this working tree builds (`cat data/dst/game.schema.sha256`); the generator prints
whatever the build it was handed says, and refuses to print when there is nothing to say.

**`revenant_guards`** - the longest body in the catalogue, and the heaviest cast.

```
+--------------------------------------+
| Revenant Guards                  (3) |   name, energy cost
| Lich . level 3                       |   the package that teaches it
|--------------------------------------|
| Up to 3 allies                       |   targeting: origin, scope, max targets
| Defense +2, permanent                |
| Defense +2, 1 round                  |
| ------------------------------------ |
| Caster: Bleed 4 a round, 1 round     |
|--------------------------------------|
| Critical 33%                         |   the chance as authored; no threshold, see below
|                                      |
| spell:revenant_guards:v1     4d7a84  |   versioned id, content hash prefix
+--------------------------------------+
```

`revenant_guards` prints 33% today and 0.33 is not a whole number of twentieths, so the threshold is blank
until the catalogue is snapped ([1.6](#16-dice)). On a d20 it becomes 35% and `d20: 14+`. This is exactly the
case the generator exists for: the card is reprinted from the build, not corrected by hand.

**`crushing_stomp`** - the only cost-4 Spell in the catalogue, and one of the two Stuns.

```
+--------------------------------------+
| Crushing Stomp                   (4) |
| Dreadnought . level 3                |
|--------------------------------------|
| One enemy                            |
| Damage 7                             |
| Stun, 2 rounds                       |
|--------------------------------------|
| Critical 75%  d20: 6+                |
|                                      |
| spell:crushing_stomp:v1      4d7a84  |
+--------------------------------------+
```

**`wait`** - the floor of the game, and the shortest card. No critical line to read, because the chance is
zero and the Creature adds nothing: the card says so rather than leaving a blank a player reaches for a die
over.

```
+--------------------------------------+
| Wait                             (0) |
| Starting spell                       |
|--------------------------------------|
| Self                                 |
| Energy +2                            |
|--------------------------------------|
| No critical roll                     |
|                                      |
| spell:wait:v1                4d7a84  |
+--------------------------------------+
```

The head line is the whole of what the card says about acquiring the Spell: which package to buy, and how
deep it sits. `Dreadnought . level 3` does not say what Dreadnought needs first or what it pays in initiative;
the Dreadnought package card does, once, for both of the Spells it teaches. The longest head line
at `4d7a841c` is `Plague Doctor . level 2` (23 characters), inside the 38 a line holds.

---

## Part 3. Boards and tracks

The design rule of this part: **a rule a player has to remember is a rule that will be got wrong.** Each
layout choice below names the rule it enforces physically.

### 3.1 The creature board

A5, 105 x 148 mm, two to an A4 sheet. Front is the living Creature; back is printed `Defeated` with no slots
at all, so a dead Creature cannot be given Energy, a Speed token, an Intent or a Condition - which is the rule
`creatures.Where(creature => creature.IsAlive)` enforces in five different places.

```
+-----------------------------------------------+
| [1]  Main                          Creature   |   the Creature's number: no tiebreak, see below
|-----------------------------------------------|
| Health   0 1 2 3 4 ............ 18 19 20      |   one rail, one marker
| Energy   0 1 2 ................ 15 16         |   two rows, one marker, ends at 32
|          17 18 ................ 31 32   [+32] |   the overflow chit's place
|-----------------------------------------------|
| Defense  buffs   0 ................. 20 [+20] |
|          debuffs 0 ................. 20 [-20] |
|          Defense = buffs - debuffs, never < 0 |
|-----------------------------------------------|
| Base initiative   tens 0..4   units 0..9      |
|-----------------------------------------------|
| Conditions   | new |   3   |   2   |   1   |  |   the duration dock
|              |     |       |       |       |  |
|-----------------------------------------------|
| Speed [        ]   <- a Stun token sits here  |
| Targeted by  [1][2][3][4][5][6]               |
+-----------------------------------------------+
```

| Affordance | The rule it enforces, so nobody has to remember it |
| --- | --- |
| The number 1 to 6 in the corner | It names the Creature: on its initiative marker, on its target markers and in the `Targeted by` row. Ids are handed out in join order (`Match.cs:286-296`): 1 to 3 is Player 1, left to right. **It breaks no tie.** A tie between the sides is a d20 roll-off, and a tie within one side is its owner's Tie order (ADR 0063). The number decides one thing more: tied Creatures roll in number order, lowest first. That fixes the order of the rolls and changes no result, so a table that rolls in another order has lost nothing. |
| The Health rail ending at 20 | A Heal is capped by the Health missing (`Creature.cs:139`). The marker cannot go past the end of the rail. |
| The `Defeated` back with no slots | A dead Creature takes no damage, no healing, no Energy, no Spell and no Condition. |
| The Speed slot, and a Stun token that occupies it | A stunned Creature takes no Speed choice, so it gets no Activation slot and no Intent (`SpeedRules.cs:34`, `TimelineBuilder.cs:25`). The token physically fills the slot: there is nowhere to put a Speed token. This is the biggest effect in the game and the one most likely to be played as "loses its attack". |
| The `Targeted by` row, one box per caster number | No duplicate targets (`TargetingRules.cs:68`): a caster has one marker per box, and a box holds one marker, so naming the same target twice is impossible. |
| The Energy rail being face up | An Intent must be affordable (`IntentRules.cs:50-69`), and a Player must be able to check that without revealing the Intent. Energy is public in the engine's own projection, so the rail is public too. |

### 3.2 The condition dock, and the countdown

Four lanes: `new`, `3`, `2`, `1`. A Condition token is placed in `new` when it is applied. At **Cleanup**:

1. every token already in a numbered lane slides one lane left; a token leaving lane `1` is removed;
2. every token in `new` moves into the lane matching the Duration printed on it.

That is the whole countdown, and it is why the board has a `new` lane: it makes "**the first countdown after
an application does not count**" (`Condition.cs:12,53-59`) a piece of geometry instead of a rule a player has
to recall on the Round they apply something. A refresh - which is only Stun - removes the old token and places
the new one in `new`, which is exactly `Condition.Refresh` setting the flag again (`Condition.cs:50`).

Four lanes is derived: the longest Duration in the catalogue is 3 Rounds (`summon_minions`' Bleed,
`momentum`'s Energy regeneration), plus the `new` lane. **VALUE**: a longer Duration authored in `data/` is a
fifth lane and a reprint of six boards.

Permanent Conditions never enter the dock. They move a rail and are discarded, because they never count down
(`Condition.cs:34-36`) and never have to be undone.

### 3.3 Defense: two rails, because the floor is applied once

`TotalDefense` is base plus the Defense buffs less the Defense debuffs, and the result floors at zero
(`Creature.cs:58-60`, ADR 0035). The floor is applied **to the total**, not to the intermediate. So a single
rail that stops at zero would be wrong: a Creature at 0 base carrying a -4 debuff and then a +3 buff has a
total Defense of 0 in the engine, and a rail clamped at zero would show 3.

Two rails hold the un-floored sums, and the printed line under them is the reading:
`Defense = buffs - debuffs, never below 0`. That turns the audit's complaint - "1 sum over the Condition
tokens per target, per cast" (translation.md 1.8) - into **one subtraction of two numbers that are side by
side**, done when a Condition lands or expires rather than once per incoming cast. The debuff rail is at zero
in most games: only three Spells lower Defense.

Both rails run 0 to 20, with overflow chits. The rule beside 20: the largest Damage in the catalogue is 10,
the critical multiplier is 2.0, so **20 Defense blanks every attack in the game**, and the only damage that
gets through is a Bleed tick, which ignores Defense (`UpkeepRules.cs:67`). It is a **VALUE**: a catalogue with
a bigger hit or a bigger multiplier reprints the boards. It is not a cap - nothing caps Defense (ADR candidate
3, open) - which is why the chits exist.

```bash
python3 -c "
import json,glob
print(max(e['amount'] for p in glob.glob('data/Spells/**/*.json',recursive=True)
  for e in json.load(open(p))['effects']+json.load(open(p)).get('casterEffects',[]) if e['kind']=='Damage'))"   # 10
```

### 3.4 Initiative: two small rails instead of one long one

Base initiative only ever grows, by the `initiativeBonus` of every package bought, once a purchase
(ADR 0056; glossary, Base initiative). No Spell adds anything (ADR 0059). Its ceiling in a 16-Round Match is
the last line of the command in [1.1](#11-spell-cards-and-package-cards): **39**.

A Player makes 2 picks at each of 8 opportunities: **16 purchases**, all of them possible on one Creature.
The 21 packages' bonuses sum to 47, but a Creature cannot own all 21 with 16 picks, and a level-3 package
cannot be bought without the two below it. The 16 prerequisite-closed packages that pay the most pay 39, so
**Base initiative tops out at 5 + 39 = 44.** Add the largest Initiative buff a Creature can carry -
`death_squad` is +2 for a Round on up to 3 allies, it stacks, and all three of a Team can cast it in the same
Round, so +6 - and **Current initiative tops out at 50**. Under one Spell a pick, twice every Round, the
ceilings were 52 and 58.

A rail to 44 is 45 cells and 225 mm at a readable 5 mm a cell, which no board holds. **Two rails, tens 0 to 4
and units 0 to 9, are 15 cells** and read as one two-digit number. The print constraint is the board's 95 mm
of usable width; the rule is the ceiling of 44. A bonus is 0 to 5 at `4d7a841c`, so a purchase is one marker
move on the units rail, sometimes carrying into the tens rail.

The tens rail is a **VALUE** twice over. The bonuses are balance knobs now (ADR 0061), and
`data/balance/knobs.json` lets a tuning pass move each one up to its declared `max`. At every package's `max`,
the same command reads a ceiling of 5 + 71 = 76, which is a tens rail to 7. Part 6, question 11.

Current initiative is **not** on a rail. It is Base plus the dock's Initiative buff tokens less its Initiative
debuff tokens, floored at zero, and it is read **once a Round**, when the timeline is built. That is the
audit's own count: one marker move, read once. Only 3 Spells in the catalogue touch Initiative, and none of
them is permanent, so most boards have nothing to add.

### 3.5 The initiative track

Six slots in a row, with a divider the Players move between the Quick slots and the Standard ones, and the
ordering printed along the edge:

```
 |<-- Quick ------[ divider ]------ Standard -->|
 [ 1st ] [ 2nd ] [ 3rd ] [ 4th ] [ 5th ] [ 6th ]
 Quick before Standard. Initiative high to low.
 Tied across the sides: each rolls a d20, high first; equal rolls across the sides roll again.
 Your own tied Creatures: you order them in your places.
```

The divider moves because a Round can have 0 to 6 Quick slots; it is set to the count of Quick tokens
revealed. Placing is: reveal all six Speed tokens together, put the Quick Creatures' markers in order of
Current initiative, then the Standard ones. Six markers, six lookups, a sort of at most
six - the audit's numbers, unchanged.

A tie is settled on the track in two steps (ADR 0063), and the components carry both:

1. **The roll-off.** Tied markers of both sides sit side by side over the places they share. Each tied
   Creature rolls a d20, in number order; the highest takes the first place, and equal rolls across the sides
   roll again. The roll decides which places each side holds, and nothing else.
2. **The Tie order.** A Player who holds two places or more in one tie lays a tie order chit face down on
   each of those Creatures' boards: `1st` takes the side's first place in that tie, and so on. Both Players
   turn their chits together and move the markers into the places the chits give. That is the same
   face-down-then-turn the Speed choice uses, and it keeps the order hidden until both are in, as the engine
   does. A tie one side holds alone skips step 1. A Round with no tie of two or more of one side's Creatures
   skips step 2.

The track is an **ordering** device and carries no numbers. The alternative, a value track a marker is placed
on, needs 51 cells for the ceiling of 3.4 and would still need the tie rules printed.

### 3.6 The round track

Sixteen spaces. The Round marker advances one space at Finalization. The **Round cap marker** is placed at
setup on the space equal to the `RuleSet`'s Round cap: when the Round marker reaches it, the Match ends on
total remaining Health (`WinCondition.cs:23-26`). The cap is a component rather than a memory, and moving it
is how a shorter or longer Match is set up without a reprint.

**The pick marks.** Evolution offers picks only at an opportunity: Round 1 and every second Round after
(`RuleSet.IsEvolutionRound`, ADR 0056). A Round without one gives nobody a pick, and its Evolution ends as it
opens. So every space that is an opportunity carries a printed pick mark, two small pick-token outlines: at
the table's schedule, spaces 1, 3, 5, 7, 9, 11, 13 and 15, **8 marks**. The rule beside the count: the
Rounds r from 1 to 16 with r >= `FirstEvolutionRound` and (r - `FirstEvolutionRound`) a multiple of
`EvolutionInterval`. The Players put their pick tokens on their mats when the Round marker stands on a mark,
and not otherwise. "Is it a pick Round?" is then a look at the track, not a parity sum. The marks are a
**VALUE**: a schedule with another first Round or interval reprints this one sheet. The cap is a marker
because a table changes it between Matches; whether the schedule should be one too is Part 6, question 9.

The track also carries the Round's shape as a printed strip, because it is where a Player looks when they lose
their place. In the engine's order (`RoundSubPhase.cs`, eleven sub-phases since ADR 0063):

```
 Start: energy -> energy regeneration -> regeneration -> bleed
 Planning: evolution (marked Rounds: 2 picks a player) -> speed (face down)
           -> timeline, ties rolled off -> order your own ties (face down)
 Combat: intents (face down) -> reveal and target, all six -> resolve, all six
 End: conditions count down -> check the win condition
```

The two orderings that change results and will be got wrong are on it and on the player aid: **healing before
bleeding** (`UpkeepRules.cs:24-28`, ADR 0019), and **the critical is applied before Defense is subtracted**
(`ResolutionRules.cs:73`).

### 3.7 The player area, and where a face-down intent sits

An A4 landscape mat a Player, three columns, one a Creature:

```
+---------------------------------------------------------------+
| Player 1        picks: [o][o]      target markers: 1 [][][]    |
|                                                   2 [][][]    |
|                                                   3 [][][]    |
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
  as an opportunity, one spent a purchase, the rest returned when the Player passes.
- **The target markers** are three per Creature, in that Creature's colour, carrying its number. Reveal and
  target walks the whole timeline before anything resolves, so all six casts' markers are on the table at
  once: 18 markers, and a Creature's `Targeted by` row shows who is pointing at it.
- **Speed tokens** are placed face down on each board's Speed slot and turned together, which is what makes
  the Speed choice the genuine simultaneous decision it is in the engine
  (`PlayerBoardStateProjection.cs:37`).

### 3.8 How a cast is declared and resolved, in components

1. **Intent**: put a Spell card from the hand face down in the Creature's intent slot. Legal if the Creature
   knows it - a starting Spell, or one printed on a package card lying with that Creature - and the Energy
   rail is at or above the printed cost.
2. **Reveal**: at the Creature's slot on the initiative track, turn the card face up and place its target
   markers, one per target, in the `Targeted by` boxes of the targets' boards. Up to `maxTargets`, and fewer
   is allowed. Do this for all six slots before resolving any.
3. **Resolve**, in the same order: check the Fizzle conditions, roll the die if the card prints a chance, move
   the Energy marker down by the cost, apply each effect line to each target, then the caster line, then take
   the markers back.

---

## Part 4. The packages as an object

A pick buys a Tier: a named package of Spells with a level, the Tiers it requires, and one initiative bonus
(ADR 0056). 21 are enabled at `4d7a841c`: 3 at level 1, 9 at level 2, 9 at level 3. Each level-2 package
requires one level-1 package and each level-3 package requires one level-2 package, so the 21 form three
lines of seven. **Prerequisites are the only rule**: the talent tree gates nothing, and multiclassing is
free, so a Creature may own packages from all three lines. The two picks of an opportunity resolve in
sequence, so a Creature can buy a package and the one above it in the same Round.

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
| Dreadnought                     [+3] |   name; the bonus, in a square
| level 3 . +3 initiative              |   the band ends here
|======================================|
| Needs Ironbound                      |   every Tier it requires, or "Needs nothing"
|--------------------------------------|
| Restorative Gush                     |   every Spell it teaches, one a line
| Crushing Stomp                       |
|                                      |
|                                      |
| tier:dreadnought:v1          4d7a84  |   versioned id, content hash prefix
+--------------------------------------+
```

Every line comes from `tiers[]` in the build: `name`, `level`, `initiativeBonus`, the `name` of every Tier in
`prerequisites`, and the `name` of every Spell in `spells`. The Spell text is not repeated: the Spell cards
carry it, and a package card that did would be a second place for a tuning pass to reprint.

What each piece of the layout answers:

| Choice | Why |
| --- | --- |
| The top band, two lines: name, then `level N . +B initiative` | A Creature's cards lie in a stagger, each covering the last but for its band ([3.7](#37-the-player-area-and-where-a-face-down-intent-sits)). The band alone must say which Tier it is, how deep, and what it paid. |
| The bonus twice, in a square at the top right and in words | The square is where the eye goes on a card, as the cost circle is on a Spell card; the words stop `+3` being read as a cost. It is the package's number and no Spell's (ADR 0059). `Shaman` prints `+0`, not a blank. |
| A heavy rule under the band, and no cost circle | What tells a package card from a Spell card in a library pile, in greyscale. A package card never enters a hand. |
| `Needs` on every card, by name | The rule, and what the check reads: a Creature may buy a Tier only if every Tier it `Needs` already lies face up with that Creature. A level-1 card prints `Needs nothing`, so no card has a blank a player has to interpret. |
| No talent tree class, no family map | The tree gates nothing (ADR 0056, ADR 0058). A card that drew its gates would teach a second eligibility rule, the alternative ADR 0056 rejected. |

The measurement, at `4d7a841c`:

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
# widest line (23, 'Warmonger', 3)
# body lines [2, 3]
```

The widest line on any package card is 23 characters (`level 3 . +4 initiative`) against the 38 a line
holds at 8 pt, and a body is 2 or 3 lines of the 4 the Spell card's body box holds. The package card is the
easy card to print. The band is two lines at 8 pt, about 10 mm with its rule, so a stagger costs 10 mm a card.

### 4.2 How a purchase reaches the hand, and how the bonus is recorded

A purchase spends one pick token and is **three actions in a fixed order**, the order of rulebook §5.3:

1. **Package card.** Put a copy of the Tier's package card face up in that Creature's column, on the top of
   its stagger. This is the public record that the Creature owns the Tier, and it is what the opponent reads
   instead of the concealed hand.
2. **Spell cards.** Take one copy of each Spell the package card names from the library into the hand. The
   library is the 216 Spell cards filed by the package in their head, six copies of each Spell together. A
   Spell the Creature already knows is not taken again (ADR 0056: the grant is idempotent). At `4d7a841c` no
   Spell is taught by two Tiers, so this never happens, but authored content may make it happen.
3. **Initiative.** Move that Creature's Base initiative rails up by the card's bonus. **This is the only
   place Base initiative ever moves** (ADR 0056), which is why the package card prints it and no Spell card
   does. Values at `4d7a841c` are 0 to 5, so it is one marker move on the units rail, sometimes carrying into
   the tens rail.

Rules of the sub-phase that the components carry rather than the rulebook:

- **A Creature cannot buy a Tier it owns.** The card is already in its stagger; a second copy there is a
  mistake anyone can see.
- **A prerequisite is a card.** Every Tier a card `Needs` must already lie with the same Creature. The check
  is a read down one stagger's bands.
- **The second pick sees the first.** The first purchase's card is on the table before the second pick is
  chosen, so the table is always the board the engine validates against.
- **A refused purchase changes nothing** (ADR 0056: no half-taught package, no bonus without the Tier). Step 1
  is the step that can be refused, and it comes first, so a refusal happens before any Spell card or rail
  moves. The pick token is spent with the card, not before it.
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
| A rule set file | Team size, energy a Round, picks an opportunity, the first opportunity Round, the interval, the Round cap, the critical multiplier: the seven fields of `docs/tabletop/playtest.rules.json` | **The content hash does not cover the `RuleSet`**, and half the counts in Part 1 come from it: the copies of both decks, the pick tokens, the pick marks. A deck plus a set of boards is only valid for a content hash **and** a rule set, so both are stamped. The table host already reads this file (`table --rules`, `RuleSetFile`), and the generator takes the same one. Where that file should live is still Part 6, question 6. |
| The die | A d20 ([d20-criticals.md](d20-criticals.md), settled) | The threshold `d20: N+` is printed when the chance is a whole number of twentieths, and omitted when it is not, rather than rounded. The rule that every chance is a twentieth is settled and not built, so today some cards print a percentage alone. |

Cards are **generated, never transcribed**. A tuning pass reprints the deck rather than invalidating it, which
is the whole reason phase 3 specifies a generator instead of a table of card texts.

### 5.2 Outputs

- **Card sheets**: every Spell face that some Creature could know - a starting Spell, or one an enabled Tier
  teaches - repeated 2 x team size times, laid out 9 to a sheet. 36 faces and 216 cards at `4d7a841c`.
- **Package card sheets**: every enabled Tier's face, repeated 2 x team size times, 9 to a sheet. 21 faces
  and 126 cards, 14 sheets, at `4d7a841c`.
- **Component sheets**: 6 creature boards, 2 player mats, the initiative track, the round
  track with its pick marks computed from the rule set's schedule, and the token sheets.
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
| Cards a sheet | 9 | The 3 x 3 grid above. 216 Spell cards is **24 sheets**; 126 package cards is **14**. |
| Bleed | 3 mm on the outer edge only; cards abut inside the grid | Neighbours share a cut line, so no bleed is wasted between them and a single cut serves two cards. |
| Cut marks | Hairline marks in the outer margin, at every grid line, never across a card | A mark that crosses the card is printed on the card. Marks in the margin survive a guillotine and a craft knife. |
| Fold marks | None | Cards are cut, not folded. Boards are printed one to a face. |
| Colour | Everything readable in greyscale; a package line's colour is a strip **and** a printed package name | Home printers run out of one ink. A card that only says "Lich" in purple stops saying it. |

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
| **No face carries an initiative or a prerequisite**, and none carries the talent tree's class | ADR 0059 and ADR 0056. A card that prints a rule the engine stopped applying is a card a table plays. |
| A package's level is its own `level`, and a starting Spell's is 0 | ADR 0058 superseded the tree depth of ADR 0034. A depth computed from the tree is a number the game does not consult. |
| A catalogue of N enabled Tiers produces exactly N package faces, each with its name, level, bonus, a `Needs` line naming every prerequisite (`Needs nothing` for none) and every Spell it teaches, and 2 x team size copies of each | The package card is where a purchase is checked. A missing prerequisite is a card that sells what the engine refuses. |
| The copy count is 2 x the rule set's team size for a Spell some Creature could know, and 0 for one it could not | A rule set change reprints the deck; it must not need an edit. |
| The pick marks are exactly the Rounds from 1 to 16 that `IsEvolutionRound` answers yes for, with the rule set's first Round and interval | The schedule is the rule set's, answered in one place (ADR 0056); a mat that worked out its own parity is a second schedule. |
| Rendering fails, loudly, when a body line exceeds the text area or a body exceeds 4 lines, on either kind of card | The measurements in 2.3 and 4.1 hold for today's content. A tuning pass that lengthens a Duration or adds an effect, or an author who puts a fourth Spell in a package, must break the build rather than clip the card. |
| Every emitted sheet carries the hash, and a missing or mismatched hash produces no output at all | The invariant of 5.5, as a property over the whole output. |
| 9 cards a sheet, cards abutting, marks only in the outer margin, and a short last sheet padded with blanks rather than a wrapped card | A card split across two sheets is 216 cards of waste. |

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

Each one is a count or a choice this document cannot derive. None is answered here; question 1 was answered
elsewhere, and says so.

### 1. Which die

**Answered: a d20** ([d20-criticals.md](d20-criticals.md), settled, not built), for the reason in
[1.6](#16-dice) - two candidate grids sit inside the 0.05 step `knobs.json` already declares on 21 Spells,
d10 and d20, and the d20 is the finer of the two: it moves 10 of the 21 Spells that roll where the d10 moves
15, its worst move is 0.02 rather than 0.05, and it can still express the 0.75 `crushing_stomp` is on. ADR 0063 put a
second use on the same die, the roll-off. The question stays here for what is left with it: `revenant_guards`
prints a chance and has **no critical chance knob**, and `lightning_bolt`'s knob band starts at 0.17, so its
own grid contains no multiple of 0.05. Both are content changes with a journal entry and a new hash.

### 2. The deck's copy count

- **216 cards** (this manifest). Any legal game is playable. 24 sheets, which is most of the print-and-play.
- **Six copies of the three starting Spells and two of each of the other 33: 84 cards, 10 sheets.** A Match
  uses at most 74 cards ([1.1](#11-spell-cards-and-package-cards)), so this is enough for almost every
  game - and a game where three Creatures buy the same package runs out, which is a rule change by the back
  door and fork A forbids it. How often that game happens is the measurement question 7 waits on for the
  package cards: how often one Tier is owned by more than two Creatures.
- **One card a Spell plus a hidden intent device** (a two-digit chit pair or a dial per Creature, reading a
  catalogue number 1 to 36). 36 cards, and the Intent stops being a card: every declaration becomes a lookup,
  and the thing that translates best in the whole game is the thing that gets worse.

### 3. Health is 20 and moving

The Health rail is printed 0 to 20 from `baseHealth`. A balancing pass that moves it reprints six boards.
Print the rail to 20 now, or print it to 30 with the space past 20 shaded, so a rebalance inside that range is
a setup note rather than a reprint? The second costs 30 mm of board width it has nothing else to do with.

### 4. The energy overflow chit

The track ends at 32 because 2 a Round for 16 Rounds is the gain no play can refuse. One overflow chit a
Creature is in the box on the reasoning that a second means banking over 64. Is one chit a Creature right, or
should the box carry the theoretical rate (14 a Round, so 7 chits a Creature) and accept the punch-out?

### 5. The condition supply, and what a supply that runs out means

The supplies in [1.4](#14-condition-tokens) are **one Round at the maximum rate**. The rule's own ceiling is up
to three times that for the Durations over one Round - 54 Bleed-2 tokens - which the Health scale makes
unreachable but the rules do not forbid. Three answers: print one Round's worth and carry the blank-token
escape (this manifest); print the rule's ceiling - each face's supply times its own longest Duration, which
is 216 condition tokens - and one more sheet; or bound the rule, which is an engine change and belongs to ADR
candidates 2 and 3, not here.

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
  pip a purchase. 2 sheets and 32 pips instead of 14 sheets: a Player makes at most 2 picks at each of 8
  opportunities in 16 Rounds, so 16 pips a Player. The whole Team's progress and the families are one glance,
  and the record is away from the boards. The rulebook would change its step 1 from "card" to "pip".
- **Fewer package cards.** 126 is the rule's ceiling, and it is reachable. A Match lays out at most 32. How
  often one Tier is owned by two, three or six Creatures in a real Match is the `tabletop-mathematician`'s
  measurement (translation.md says so), and a smaller box is a question only that measurement can open, on the
  same terms as question 2: a copy count that a legal game can exceed is a rule change by the back door.

The first is this manifest's choice because it answers the verdict as written. The second is a playtest
reading. The third waits on a measurement.

### 8. Card backs

A single back design costs 24 more sheets for the Spell cards and 14 for the package cards, 38 in all, and
nearly doubles the print, for a deck whose hands are concealed and whose Intents are played face down - so
the Spell card backs must at least be uniform. Package cards are never hidden, so they need a back only to be
told apart from Spell cards in a pile; the heavy band of [4.1](#41-the-package-card) already does that.
Printing single-sided and sleeving the Spell cards with an opaque backing card is the cheaper answer and
needs sleeves. Which one the print-and-play assumes changes the sheet count from 47 to 71, or to 85 with
package card backs too.

### 9. The schedule: printed marks or placed markers

The Round track prints 8 pick marks, on the Rounds the table's schedule makes opportunities
([3.6](#36-the-round-track)). A schedule with another first Round or interval reprints the track, which is one
sheet. The alternative is the Round cap's answer: pick markers placed at setup, as many as the schedule has
opportunities in 16 Rounds (8 at interval 2, 16 at interval 1, so the box would carry 16). Which one depends on
whether the schedule is a setting a table changes between Matches, like the cap, or a rule it plays, like the
picks themselves. That is the maintainer's to say, and it is the same question as where the rule set file
lives (question 6).

### 10. The class on a Spell card

The spec prints the package that teaches a Spell in the card's head, and not its `creatureClass`
([2.1](#21-what-is-printed-and-where-it-comes-from)): the two sets of names disagree on 29 of the 33 taught
Spells, and some collide, so `tornado` would print `Berserker` while the `Berserker` package does not teach
it. The table app prints the class (`cardHead` in `table/card.js`, from `CardFace.CreatureClass`). The screen
and the deck should say the same thing. Which is it? And does the class mean anything a player needs, now that
the talent tree gates nothing? ADR 0058 leaves "what the tree is for" open, and this is one place that answer
lands.

### 11. The Base initiative rail and the bonus knobs

The tens rail runs 0 to 4 because the most Base initiative one Creature can buy in 16 Rounds is 39, for a
Base of 44 ([3.4](#34-initiative-two-small-rails-instead-of-one-long-one)). Each package's `initiativeBonus` is
a balance knob now (ADR 0061), with a declared `max` in `data/balance/knobs.json`. At every `max` the ceiling
is 5 + 71 = 76. Print the tens rail to 4 and reprint six boards when a tuning pass raises a bonus, or print it
to 7 (18 cells, 90 mm, inside the 95 mm the board has) so no pass inside the declared bounds reprints
anything? It is question 3's shape, with a derived bound instead of a guessed one.

### 12. Where the generator reads the card words from

[5.1](#51-inputs) reads `game.schema.json`. The table host already renders the same content into card words
(`CatalogueProjection`, `CardFace`, `PackageCard`), stamped with the hash and the rule set. Two renderers of
the same words will drift; the head line of question 10 is a drift that has already happened. Should the
generator read the host's catalogue, so the screen and the print cannot disagree, at the price of needing a
running host to print?

### 13. The hidden Tie order at a table

ADR 0063 hides a Tie order until both Players have given theirs, like a Speed choice. This manifest answers
it with 6 tie order chits, face down on the tied Creatures' boards ([1.5](#15-the-rest-of-the-pieces),
[3.5](#35-the-initiative-track)), which is translation.md's smallest answer. The rulebook's own step (§5.5,
"Both Players do this at the same time") does not yet say face down. That is a sentence for the
`rulebook-writer`, not a component, but the two have to agree: if the maintainer rules that a table need not
hide the order, the chits leave the box.

### 15. A two-sided Speed token cannot be placed face down

Found while choosing the tie order chits, and older than them. [1.5](#15-the-rest-of-the-pieces) specifies one
Speed token a Creature, Quick on one face and Standard on the other, "made face down". A token whose two faces
are the two answers shows one of them whichever way it lies, so the choice is not hidden. A hidden choice of
one of two needs two faces behind one common back: two Speed tokens a Creature, 12 in all, one played and one
kept, or the six tokens played under a cover. The first is the Intent's own answer (a card from a hand, face
down). This moves a count from 6 to 12; it is not changed here because it is not part of the package update,
and the maintainer should see it first.

### 14. The player area does not hold what 3.7 puts on it

Found while placing the package cards, and older than them. [3.1](#31-the-creature-board) calls the creature
board "A5, 105 x 148 mm, two to an A4 sheet", but A5 is 148 x 210 mm, and 105 x 148 mm is A6, four to a sheet.
Either way the A4 landscape player area of [3.7](#37-the-player-area-and-where-a-face-down-intent-sits),
297 x 210 mm, cannot hold three boards side by side with an 88.9 mm intent slot below each: three A6 boards in
portrait are 315 mm wide, and a board over an intent slot is 237 mm tall. The package stagger adds 10 mm a
card below that. Which gives: the board's size (and the sheet count of 3 that follows from "two to a sheet"),
an A3 player area, or a player area that is a printed guide for the table rather than a mat that holds the
pieces? This is a layout question, not a rule, and the counts in Part 1 do not depend on it except the 3
board sheets.

---

## Part 7. Coverage: the "needs a component" rows

Every **needs a component** verdict in [translation.md](translation.md), and what answers it. The row names
are translation.md's as it reads on this branch after its package re-audit: 17 from Part 1, 8 from Part 2, 19
from Part 3; 44 of 44. The re-audit was written alongside this document, so if a row name has moved since,
the component beside it has not.

### The 17 sub-phase rows

| translation.md row | Component |
| --- | --- |
| 1.1 Energy gain per Round | The Energy rail, [1.3](#13-stat-markers-and-the-rails-they-ride) and [1.7](#17-the-energy-track-what-ends-it) |
| 1.1 Energy has no maximum | The Energy rail's end at 32 and the overflow chit, [1.7](#17-the-energy-track-what-ends-it) |
| 1.3 An opportunity at Round 1 and every second Round after | The 8 pick marks on the Round track, [3.6](#36-the-round-track) |
| 1.3 Two picks an opportunity, per Player, shared across the Team | 4 Evolution pick tokens, [1.5](#15-the-rest-of-the-pieces) |
| 1.3 A pick buys a whole Tier | 126 package cards, face up with the Creature that bought them, [1.1](#11-spell-cards-and-package-cards) and [Part 4](#part-4-the-packages-as-an-object). Each card's `Needs` line is the prerequisite check; no Spell card prints a gate. |
| 1.3 A purchase raises Base initiative by the Tier's initiative bonus, once, for the Match | The two Base initiative rails and the package card's bonus, [3.4](#34-initiative-two-small-rails-instead-of-one-long-one) and [4.2](#42-how-a-purchase-reaches-the-hand-and-how-the-bonus-is-recorded). No Spell card prints an initiative. |
| 1.4 One Speed choice per living, unstunned Creature | 6 Speed tokens and the Speed slot a Stun fills, [3.1](#31-the-creature-board); Part 6, question 15 |
| 1.5 The Combat timeline | The initiative track and 6 numbered markers, [3.5](#35-the-initiative-track) |
| 1.5 Current initiative is Base plus buffs less debuffs, floored at zero | The Base initiative rails read with the dock's Initiative tokens, [3.4](#34-initiative-two-small-rails-instead-of-one-long-one) |
| 1.5 A tie between the sides is rolled off on a d20 | The two d20s, [1.6](#16-dice), and the tie rules printed on the initiative track, [3.5](#35-the-initiative-track). The number on each board, [3.1](#31-the-creature-board), only fixes the order tied Creatures roll in. |
| 1.6 Tie orders are hidden until both are in | 6 tie order chits, face down on the tied Creatures' boards, [1.5](#15-the-rest-of-the-pieces) and [3.5](#35-the-initiative-track) |
| 1.8 Reveal in timeline order, bind targets at reveal | 18 target markers and the `Targeted by` row, [3.7](#37-the-player-area-and-where-a-face-down-intent-sits) |
| 1.9 One critical roll a cast | The die, [1.6](#16-dice), and the card's printed chance |
| 1.9 Total Defense is base plus buffs less debuffs, floored at zero | The two Defense rails, [3.3](#33-defense-two-rails-because-the-floor-is-applied-once) |
| 1.9 A lasting Effect attaches as a Condition per its Stacking policy | The 150 Condition tokens and the dock, [1.4](#14-condition-tokens) and [3.2](#32-the-condition-dock-and-the-countdown) |
| 1.9 `Stack` adds another Condition | The same, plus the supply rule and the blank tokens |
| 1.10 Every Condition counts one Round down and expires at zero | The dock's four lanes and the two-step Cleanup, [3.2](#32-the-condition-dock-and-the-countdown) |

### The 8 effect kinds

| Effect kind | Token, and its supply |
| --- | --- |
| `Bleed` | 48 tokens: 18 at 1, 18 at 2, 6 at 3, 6 at 4 |
| `Regeneration` | 6 tokens at 3 |
| `EnergyRegeneration` | 6 tokens at 2 |
| `Stun` | 12 tokens, two a Creature: a Stun refreshes, so a Creature carries one, and it needs a token in the Speed slot and one in the dock |
| `DefenseBuff` | 30 timed tokens (6 at +1, 18 at +2, 6 at +3); a permanent buff moves the rail and needs none |
| `DefenseDebuff` | 18 timed tokens at -2; a permanent debuff moves the rail |
| `InitiativeBuff` | 18 tokens at +2 |
| `InitiativeDebuff` | 12 tokens: 6 at -1, 6 at -2 |

### The 19 spells

Each of the 19 Spells the audit sent to phase 3, and what one cast of it puts on the table. Permanent halves
move a rail and place nothing.

| Spell | What a cast places |
| --- | --- |
| `momentum` | 1 Energy regeneration 2 |
| `full_plate` | Nothing. +3 on its own Defense buff rail, permanent |
| `guard` | 1 Defense buff +1 (2 rounds); +1 on the rail, permanent |
| `thundering_seal` | 1 Defense buff +3 (2 rounds); +3 on the rail, permanent |
| `healing_screech` | 1 Regeneration 3 |
| `death_squad` | Up to 3 Initiative buffs +2 |
| `poison_slash` | 1 Bleed 3 |
| `protective_slam` | 1 Initiative debuff -2 (2 rounds) |
| `ice_spear` | 1 Initiative debuff -1 (1 round) |
| `mortal_wound` | 1 Bleed 4 |
| `tranquilizer_dart` | 1 Stun |
| `crushing_stomp` | 1 Stun |
| `psycho_rush` | 1 Defense debuff -2 on its own caster |
| `infectious_blast` | Nothing. -2 on up to 3 enemy Defense debuff rails, permanent |
| `summon_minions` | Up to 3 Bleeds 2 (3 rounds), the longest Duration in the game |
| `revenant_guards` | Up to 3 Defense buffs +2 (1 round) and 1 Bleed 4 on its own caster; +2 on up to 3 rails, permanent. The heaviest cast: 4 tokens and 3 rail moves |
| `noxious_cure` | Up to 3 Defense debuffs -2 |
| `crazed_specter` | 1 Bleed 4 on its own caster |
| `toxic_waves` | Up to 3 Bleeds 1 |

---

## What this document does not decide

- The die. It is a d20, settled in [d20-criticals.md](d20-criticals.md), not here
  ([Part 6](#part-6-open-questions), question 1).
- The evolution rules. A package, its prerequisites, its bonus and the schedule are ADR 0056 and the content
  in `data/Tiers/`; this document counts what they need and changes none of them.
- Any rule. Where a component could not be built without one - a cap on Energy, a bound on permanent Defense -
  the question went back to the audit's ADR candidates 2 and 3 and to the `boardgame-director`, not into a
  component that quietly does something else.
- The rulebook's words. Phase 4 owns the teaching order, the examples and the player aid; this document owes
  it the printed reminders in [3.6](#36-the-round-track) and the affordance table in
  [3.1](#31-the-creature-board), which are the rules the components are already carrying.
