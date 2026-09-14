# Components and print-and-play

Status: **Specification** (2026-09-14). Phase 3 of [plan.md](plan.md). It answers the 41 **needs a component**
rows of [translation.md](translation.md) and specifies a generator that is not written here.

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
- A critical is a **die roll** and the catalogue will be authored onto the die's grid. **Which die is open**;
  [Part 1.6](#16-dice) reports what each candidate costs and recommends one.

The board this manifest is built on, and the commands that read it:

```bash
sed -n '20,22p' src/DownfallArena.Domain/Matches/RuleSet.cs   # RuleSet.Default: 3, 2, 2, 30, 2.0
cat data/Creatures/main.v1.json                                # Health 20, Energy 0, Defense 0, Base initiative 5
find data/Spells -name '*.json' | wc -l                        # 36
```

`RuleSet.Default` is 3 Creatures a Team, 2 Energy a Round, 2 Evolution picks a Round, a 30-Round cap, a
critical multiplier of 2.0. The cap is the one value the table replaces: the Round track is built for 16.

---

## Part 1. What is in the box

Totals first, then the derivation of each line.

| Group | Pieces |
| --- | --- |
| Spell cards | 216 |
| Boards and mats | 6 creature boards, 2 player mats, 2 talent tree mats, 1 initiative track, 1 round track |
| Condition tokens | 138 in 8 kinds |
| Markers and chits | 36 stat markers, 6 initiative markers, 6 speed tokens, 4 pick tokens, 2 round markers, 18 target markers, 82 talent pips, 18 overflow chits, 20 blanks |
| Player aids | 2 |
| Dice | 2 (recommended: d20) |
| Paper | about 35 A4 or Letter sheets |

The paper: 24 sheets of cards (9 a sheet), 3 of creature boards (2 a sheet), 2 player mats, 2 talent tree
mats, 1 for the initiative and round tracks, 2 of tokens (330 pieces at 15 mm, about 185 to a sheet), 1 of player aids. 35.
Card backs would add 24 more; see Part 6, question 8.

### 1.1 Spell cards

| Component | Count | The rule beside the count | Follows |
| --- | --- | --- | --- |
| Spell card | **216** = 36 Spells x 6 copies | A card in a hand is what lets an Intent be played face down, so a Creature needs its own copy of every Spell it knows. Any of the six Creatures can come to know any Spell: nothing in the Talent tree is exclusive to a side, and both Players play the same Creature definition. Six copies is the ceiling. | 36 is a **VALUE** (content); 6 is 2 Players x team size 3, a **VALUE** (`RuleSet.TeamSize`); one copy per knowing Creature is a **RULE** |

What a Match actually consumes is smaller, and it is the number the open question in Part 6 is about:

```bash
# starting spells per creature, and picks per player per round
python3 -c "import json;d=json.load(open('data/Creatures/main.v1.json'));print(len(d['startingSpellIds']))"   # 3
```

6 Creatures x 3 starting Spells = 18 cards in hands at setup, plus at most 2 picks x 2 Players x 16 Rounds =
64 unlocks, so **at most 82 cards are in hands in a 16-Round Match**. The box still carries 216 because which
82 is a choice the Players make, and six Creatures may all choose the same Spell.

### 1.2 Boards and mats

| Component | Count | The rule beside the count | Follows |
| --- | --- | --- | --- |
| Creature board | **6** | One per Creature in play: 2 Players x `RuleSet.TeamSize` 3. Each carries the number 1 to 6 that breaks initiative ties. | **VALUE** (team size) |
| Player area mat | **2** | One per Player. A Match seats exactly two. | **RULE** |
| Talent tree mat | **2** | One per Player, holding all 36 Spells of the tree with three pip boxes per Spell, one for each of that Player's Creatures. One tree is enabled (`data/TalentTrees/talent_tree.v1.json`; `core_classes.v1.json` carries `"enabled": false`), and all six Creatures are on it. | **VALUE** (the enabled tree, the team size) |
| Initiative track | **1** | Six ordered slots, a Quick band above a Standard band. Six is the number of Activation slots a Round can have: one per living, unstunned Creature. | **VALUE** (team size) |
| Round track | **1**, 16 spaces | A Match is 8 to 16 Rounds (given). The track is printed for the top of that band. The Round cap marker is placed on the space equal to the `RuleSet`'s cap at setup. | **RULE** for 16 (the given band); the cap marker is a **VALUE** |

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
| Base initiative, tens | 6 | 0 to 5. Together the two rails read 0 to 59, which covers the ceiling computed in [3.4](#34-initiative-two-small-rails-instead-of-one-long-one): a Current initiative of 58. | **VALUE** (the catalogue's Spell initiative, the picks a Round) |

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
| Stun | - | 6 | A Stun **refreshes**, so a Creature carries at most one, ever. One per Creature. | **RULE** (the stacking policy) x **VALUE** (team size) |
| Defense buff | +1 | 6 | `guard`'s timed half, one ally; 6 slots x 1 | **VALUE** |
| Defense buff | +2 | 18 | `revenant_guards`' timed half, up to 3 allies; 6 x 3 | **VALUE** |
| Defense buff | +3 | 6 | `thundering_seal`'s timed half, one ally; 6 x 1 | **VALUE** |
| Defense debuff | -2 | 18 | `noxious_cure` on up to 3 allies; 6 x 3. `psycho_rush`'s caster debuff is the same face. | **VALUE** |
| Initiative buff | +2 | 18 | `death_squad`, up to 3 allies; 6 x 3 | **VALUE** |
| Initiative debuff | -2 | 6 | `ice_spear` and `protective_slam`, one enemy; 6 x 1 | **VALUE** |
| **Total** | | **138** | | |

Every amount in the catalogue is on this list and no other: Bleed is 1, 2, 3 or 4; Regeneration is 3; Energy
regeneration is 2; timed Defense is 1, 2 or 3; every Initiative change is 2; every Defense debuff is 2. That
is why a token set this small covers a 36-Spell catalogue.

**The supply is one Round at the maximum rate, and Durations run to 3.** A Bleed from `summon_minions` lives 3
Rounds, so the rule's own ceiling is three times the table above for that face: 54 Bleed-2 tokens. It is
unreachable at this Health scale - 9 Bleeds of 2 on one Creature is 18 damage a Round against 20 Health, and
Bleed ignores Defense - but the engine has no cap, so the rulebook carries the standard supply escape: **a
supply that runs out is replaced by a blank token with the value written on it; the game has no maximum.**
20 blanks are in the box for that. Part 6, question 5.

### 1.5 The rest of the pieces

| Component | Count | The rule beside the count | Follows |
| --- | --- | --- | --- |
| Speed token, Quick on one face, Standard on the other | **6** | One Speed choice per living, unstunned Creature (`SpeedRules.cs:13-45`). Two-sided because the choice is one of two and is made face down. | **VALUE** (team size) |
| Initiative marker, numbered 1 to 6 | **6** | One per Creature, placed on the initiative track. The number is the tiebreak (`TimelineBuilder.cs:27-28`): ids are handed out in join order (`Match.cs:286-296`), so 1 to 3 are Player 1's. | **VALUE** (team size) |
| Evolution pick token | **4** | 2 per Player a Round (`RuleSet.EvolutionPicksPerRound`), spent and returned each Round. | **VALUE** |
| Round marker | **1** | One position on the Round track. | **RULE** |
| Round cap marker | **1** | Placed at setup on the space equal to the `RuleSet`'s Round cap, so the track's end is a component and not a memory. | **VALUE** |
| Target marker | **18** = 6 sets of 3 | Every Intent on the timeline is revealed and targeted **before any of them resolves** (`ActionRules.cs:16-52`, and `ActionResolution` is a later sub-phase), so all six casts have their targets on the board at once. 3 is the largest `maxTargets` in the catalogue: 26 Spells at 1, one at 2, nine at 3. Each set carries its caster's number. | **VALUE** (team size, `maxTargets`) |
| Talent pip | **82** = 2 x 41 | A Player marks 3 starting Spells on each of 3 Creatures (9) and at most 2 picks x 16 Rounds (32). | **VALUE** (picks a Round, starting Spells) x **RULE** (16 Rounds) |
| Energy overflow chit, +32 | **6** | One per Creature. See [1.7](#17-the-energy-track-what-ends-it). | **RULE** |
| Defense overflow chit, +20 and -20 | **12** | Six of each. The Defense rails are bounded by what can matter, not by the rule, and the rule has no bound: permanent Defense buffs and debuffs both stack (ADR candidate 3, open). | **RULE** (no bound exists) |
| Blank token | **20** | The supply escape of [1.4](#14-condition-tokens). | not derived; see Part 6, question 5 |
| Player aid | **2** | One a Player: the Round sequence, the timeline tiebreaks, the Condition timing, and the two orderings of [3.6](#36-the-round-track). Phase 4 writes what it says (plan.md); this manifest reserves the component and its sheet. | **RULE** |

### 1.6 Dice

One critical roll a cast (`ResolutionRules.cs:56`), at most 6 casts a Round, resolved one after the other in
timeline order. **One die is enough by the rule.** Two are in the box so each Player rolls their own casts,
which is a convenience and not a rule.

Since the Creature's base chance is zero, only the Spells that print a chance roll at all:

```bash
python3 -c "
import json,glob,collections
v=collections.Counter(json.load(open(p))['criticalChance'] for p in glob.glob('data/Spells/**/*.json',recursive=True))
print(sorted(v.items()))"
# [(0, 15), (0.22, 1), (0.28, 1), (0.283, 1), (0.33, 4), (0.38, 1), (0.45, 2), (0.5, 7), (0.617, 1), (0.75, 1),
#  (0.767, 1), (0.8, 1)]
```

**21 of 36 Spells roll. 15 never touch a die.** Eleven distinct chances are printed today, and the die's grid
has to carry them. What each candidate costs, snapping each of the 21 to the nearest face:

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
| d6 | 14 of 21 | 0.0833 (`crushing_stomp` 0.75 to 0.667) | 0.0262 | **No.** 1/6 is not a multiple of 0.05 | Six faces for eleven distinct chances, the largest mean error of the five, and a finest distinction of 16.7 points |
| d8 | 13 | 0.05 (`enraged_charge` 0.8 to 0.75) | 0.0216 | **No** | Dominated by the d10 - the same 13 moves and the same worst move, at a larger mean error - and a die many households do not have |
| d10 | 13 | 0.05 (`crushing_stomp` 0.75 to 0.8) | 0.0189 | **Yes**, 0.1 is a multiple of 0.05 | Ten faces for eleven chances, and it cannot express 0.75, where `crushing_stomp` sits today |
| d12 | 13 | 0.0367 (`crazed_specter` 0.38 to 0.417) | 0.0140 | **No** | Values no declared knob can reach, and since tune run 8 it no longer buys the smallest mean error either |
| **d20** | **10** | **0.02** (`rejuvenate` 0.22 to 0.20) | **0.0091** | **Yes**, exactly the declared step | One die, one reading, thresholds in whole numbers |
| d100 (2 dice) | 3 | 0.003 | 0.0004 | No, 0.01 | Two dice and a percentile read per cast, and it keeps the arbitrary precision fork B exists to remove |

**Recommendation: a d20, and the card prints both the chance and the threshold** ("Critical 50% - d20: 11+",
which is seven Spells as the catalogue stands). The recommendation survives tune run 8, and on better terms
than it was made: the d20 now wins the error as well. It moves the fewest Spells (10 of 21), has the smallest
worst move (0.02) and, since run 8 pulled `crazed_specter` and `protective_slam` off the d12 grid, the
smallest mean error of the five single dice. The load-bearing reason is still the grid rather than the error,
because the error moves with every tuning pass and the grid does not: `data/balance/knobs.json` already
declares `/criticalChance` a knob with a **step of 0.05 on 20 Spells**, so a d20 snap is inside the search
space a tuning pass already has, and d6, d8, d12 and d100 are not.

```bash
python3 -c "
import json,collections
k=json.load(open('data/balance/knobs.json'))['spells']
s=[(n,b) for n,e in k.items() for b in e.get('knobs',[]) if 'criticalChance' in b['path']]
print(len(s), collections.Counter(b['step'] for _,b in s))"   # 20 Counter({0.05: 20})
```

Two findings the maintainer owns before the snap is authored, neither of them this document's to decide:

- **One Spell that prints a chance has no critical chance knob**: `revenant_guards` (0.33). A snap would move
  a number no declared knob covers.
- **Nine of the 20 knobbed Spells are already off their own declared grid**: their printed value is not their
  band's `min` plus a whole number of steps. `pummel` 0.767, `protective_slam` 0.283, `crazed_specter` 0.38,
  `tornado`, `engulfing_flames` and `toxic_waves` at 0.33, `noxious_cure` 0.28, `rejuvenate` 0.22, and
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
# wait EnergyGain 2 | restorative_burst EnergyGain 2 | momentum EnergyRegeneration 2 for 3 rounds | soul_devourer EnergyDrain 2
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
| Head | Class, then tier | `creatureClass`, the Talent tree | Where the card sits in the tree, at a glance |
| Body | Targeting, one line | `targeting.origin`, `scope`, `maxTargets` | Origin, scope and count are one sentence: `Self`, `One enemy`, `One ally`, `Up to 2 enemies`, `Up to 3 allies` |
| Body | One line per effect, with its amount and Duration | `effects[]` | |
| Body | One line per caster effect, prefixed `Caster:` and set below a rule | `casterEffects[]` | ADR 0031: once per cast, never multiplied, none of them on a Fizzle. Seven Spells carry one, and it must not read as a target effect |
| Foot | Critical chance, as a percentage and a die threshold | `criticalChance` | The printed chance is the chance rolled (ADR 0042) |
| Foot | `Unlock: +N initiative` | `initiative` | ADR 0017: what the Creature's Base initiative gains once, at the unlock |
| Foot | `Requires: ...` | the Talent tree node's and the Spell's `prerequisites` | The gate, so a pick can be checked on the card |
| Foot | Content hash, first 6 characters, and the Spell's versioned id | the build | A deck from two content hashes is a broken deck; see [Part 5](#part-5-the-generator-specified) |

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
  caster line.

Measured against the real catalogue, rendering every card face from `data/`:

```bash
python3 -c "
import json,glob,statistics
def dur(e): return 'permanent' if e.get('permanent') else str(e['durationRounds'])+' round'+('s' if e['durationRounds']!=1 else '')
def eff(e):
  k,a,ap=e['kind'],e.get('amount'),e.get('amountPerRound')
  return {'Damage':f'Damage {a}','Heal':f'Heal {a}','EnergyGain':f'Energy +{a}','EnergyDrain':f'Energy -{a}'}.get(k) or {
   'Bleed':f'Bleed {ap} a round, {dur(e)}','Regeneration':f'Regeneration {ap} a round, {dur(e)}',
   'EnergyRegeneration':f'Energy regeneration {ap} a round, {dur(e)}','Stun':f'Stun, {dur(e)}',
   'DefenseBuff':f'Defense +{a}, {dur(e)}','DefenseDebuff':f'Defense -{a}, {dur(e)}',
   'InitiativeBuff':f'Initiative +{a}, {dur(e)}','InitiativeDebuff':f'Initiative -{a}, {dur(e)}'}[k]
W=[];L=[]
for p in glob.glob('data/Spells/**/*.json',recursive=True):
  d=json.load(open(p));t=d['targeting'];o,m=t['origin'],t['maxTargets']
  lines=['Self' if o=='Self' else (f'One {o.lower()}' if m==1 else f\"Up to {m} {'enemies' if o=='Enemy' else 'allies'}\")]
  lines+=[eff(e) for e in d['effects']]+['Caster: '+eff(e) for e in d.get('casterEffects',[])]
  W.append((max(len(x) for x in lines),d['id']));L.append(len(lines))
print('widest line',max(W),'lines per card',sorted(set(L)),'max lines',max(L))"
```

| Reading | Value | What it means for the layout |
| --- | --- | --- |
| Body lines per card | 2, 3 or 4 | 17 cards at 2, 17 at 3, 2 at 4. The 4-line box is enough for every card in the catalogue. |
| Widest single line | **39 characters** (`momentum`: `Energy regeneration 2 a round, 3 rounds`) | One character over the 38 a line holds. It wraps to a second line with a 3 mm hanging indent, and `momentum` has only 2 lines, so the card has the room. Nothing else in the catalogue wraps. |
| Whole body, one string | max **99** characters (`revenant_guards`), median 38.5, min 15 (`wait`) | 99 characters is under three full lines. No card is tight on the body alone. |
| Whole statline (cost, targeting, effects, caster, critical, unlock) | max **148**, median **85.5**, min **61** | The audit measured 153 / 90 / 66 on its own rendering; this face is five characters shorter because it writes `2 rounds` rather than `for 2 rounds`. The conclusion is the audit's: nothing overflows. |

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
| `soul_devourer` | `One enemy` (9) / `Damage 5` (8) / `Energy -2` (9) / `Caster: Heal 4` (14) | 14 | **4** |
| `thundering_seal` | `One ally` (8) / `Defense +3, permanent` (21) / `Defense +3, 2 rounds` (20) | 21 | 3 |
| `guard` | `One ally` (8) / `Defense +1, permanent` (21) / `Defense +1, 2 rounds` (20) | 21 | 3 |

Two of the seven use all four lines, and none of their lines is over 32 characters. **The layout that fits
them is one effect to a line.** Not prose: a line per effect, each with its own Duration, and a 0.3 pt rule
above the caster line. That is what makes `revenant_guards`' four separate things - two Defense Conditions on
up to three allies and a Bleed on itself - four things on the card instead of one sentence to parse. Two more
Spells carry a Caster effect and are not in the seven because their statline is short
(`hateful_sacrifice`, `parasite_jab`); they use the same rule and the same prefix.

### 2.5 Three card faces, written out

Real Spells, generated from `data/`. `[ ]` marks a printed zone. `938bef` is the first six characters of
the content hash this working tree builds (`cat data/dst/game.schema.sha256`); the generator prints
whatever the build it was handed says, and refuses to print when there is nothing to say.

**`revenant_guards`** - the longest body in the catalogue, and the heaviest cast.

```
+--------------------------------------+
| Revenant Guards                  (2) |   name, energy cost
| Necromancer . Tier 3                 |
|--------------------------------------|
| Up to 3 allies                       |   targeting: origin, scope, max targets
| Defense +2, permanent                |
| Defense +2, 1 round                  |
| ------------------------------------ |
| Caster: Bleed 4 a round, 1 round     |
|--------------------------------------|
| Critical 33%                         |   the chance as authored; no threshold yet, see below
| Unlock: +1 initiative                |
| Requires: any of Lightning Bolt,     |
|   Rejuvenate; Summon Minions         |
| spell:revenant_guards:v1     938bef  |   versioned id, content hash prefix
+--------------------------------------+
```

`revenant_guards` prints 33% today and 0.33 is on no candidate die's grid, so the threshold line is blank
until the catalogue is snapped ([1.6](#16-dice)). On a d20 it becomes 35% and `d20: 14+`. This is exactly the
case the generator exists for: the card is reprinted from the build, not corrected by hand.

**`crushing_stomp`** - the only cost-4 Spell in the catalogue, and one of the two Stuns.

```
+--------------------------------------+
| Crushing Stomp                   (4) |
| Warlord . Tier 3                     |
|--------------------------------------|
| One enemy                            |
| Damage 7                             |
| Stun, 2 rounds                       |
|--------------------------------------|
| Critical 75%  d20: 6+                |
| Unlock: +1 initiative                |
| Requires: any of Pummel, Guard;      |
|   Full Plate                         |
| spell:crushing_stomp:v1      938bef  |
+--------------------------------------+
```

**`wait`** - the floor of the game, and the shortest card. No critical line to read, because the chance is
zero and the Creature adds nothing: the card says so rather than leaving a blank a player reaches for a die
over.

```
+--------------------------------------+
| Wait                             (0) |
| Creature . Tier 0                    |
|--------------------------------------|
| Self                                 |
| Energy +2                            |
|--------------------------------------|
| No critical roll                     |
| Unlock: +1 initiative                |
| Starting spell                       |
| spell:wait:v1                938bef  |
+--------------------------------------+
```

The prerequisite line is generated from both gates the engine checks (`TalentUnlocks.cs:13-25,52-58`): the
node's, then the Spell's own, joined by a semicolon, with `anyOf` written "any of". `Requires: any of Pummel,
Guard; Full Plate` is exactly "be on the Warlord branch, and know Full Plate". The longest such line in the
catalogue is 60 characters (`restorative_burst`), which is two lines at 6 pt in the foot.

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
| [1]  Main                          Creature   |   the number that breaks initiative ties
|-----------------------------------------------|
| Health   0 1 2 3 4 ............ 18 19 20      |   one rail, one marker
| Energy   0 1 2 ................ 15 16         |   two rows, one marker, ends at 32
|          17 18 ................ 31 32   [+32] |   the overflow chit's place
|-----------------------------------------------|
| Defense  buffs   0 ................. 20 [+20] |
|          debuffs 0 ................. 20 [-20] |
|          Defense = buffs - debuffs, never < 0 |
|-----------------------------------------------|
| Base initiative   tens 0..5   units 0..9      |
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
| The number 1 to 6 in the corner | Ties on the timeline break by Player slot then Creature id (`TimelineBuilder.cs:27-28`). Ids are handed out in join order (`Match.cs:286-296`): 1 to 3 is Player 1, left to right. "First player's boards, left to right" is the whole tiebreak. |
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

Base initiative only ever grows, by the Spell initiative of every unlock (`Creature.cs:106`, ADR 0017). Its
ceiling in a 16-Round Match:

```bash
python3 -c "
import json,glob
S={json.load(open(p))['id'].split(':')[1]:json.load(open(p)) for p in glob.glob('data/Spells/**/*.json',recursive=True)}
start={'basic_attack','heavy_strike','wait'}
v=sorted((s['initiative'] for k,s in S.items() if k not in start),reverse=True)
print(len(v),'unlockable, spell initiative sum',sum(v),'best 32 picks',sum(v[:32]))"
# 33 unlockable, spell initiative sum 47, best 32 picks 47
```

A Player makes 2 picks a Round for at most 16 Rounds: **32 unlocks**, all of them possible on one Creature.
33 Spells are unlockable and their Spell initiative sums to 47; three of them are worth 0, so 32 picks still
reach 47. **Base initiative tops out at 5 + 47 = 52.** Add the largest Initiative buff a Creature can carry -
`death_squad` is +2 for a Round on up to 3 allies, it stacks, and all three of a Team can cast it in the same
Round, so +6 - and **Current initiative tops out at 58**.

A rail to 58 is 59 cells and 295 mm at a readable 5 mm a cell, which no board holds. **Two rails, tens 0 to 5
and units 0 to 9, are 16 cells** and read as one two-digit number. The print constraint is the board's 95 mm
of usable width; the rule is the ceiling of 58.

Current initiative is **not** on a rail. It is Base plus the dock's Initiative buff tokens less its Initiative
debuff tokens, floored at zero, and it is read **once a Round**, when the timeline is built. That is the
audit's own count: one marker move, read once. Only 3 Spells in the catalogue touch Initiative, and none of
them is permanent, so most boards have nothing to add.

### 3.5 The initiative track

Six slots in a row, with a divider the Players move between the Quick slots and the Standard ones, and the
tiebreak printed along the edge:

```
 |<-- Quick ------[ divider ]------ Standard -->|
 [ 1st ] [ 2nd ] [ 3rd ] [ 4th ] [ 5th ] [ 6th ]
 Quick before Standard. Initiative high to low.
 Ties: Player 1 first, then the lower creature number.
```

The divider moves because a Round can have 0 to 6 Quick slots; it is set to the count of Quick tokens
revealed. Placing is: reveal all six Speed tokens together, put the Quick Creatures' markers in order of
Current initiative, then the Standard ones. Six markers, six lookups, a sort of at most
six - the audit's numbers, unchanged.

The track is an **ordering** device and carries no numbers. The alternative, a value track a marker is placed
on, needs 60 cells for the ceiling of 3.4 and would still need the tiebreak rule printed.

### 3.6 The round track

Sixteen spaces. The Round marker advances one space at Finalization. The **Round cap marker** is placed at
setup on the space equal to the `RuleSet`'s Round cap: when the Round marker reaches it, the Match ends on
total remaining Health (`WinCondition.cs:23-26`). The cap is a component rather than a memory, and moving it
is how a shorter or longer Match is set up without a reprint.

The track also carries the Round's shape as a printed strip, because it is where a Player looks when they lose
their place. In the engine's order (`RoundSubPhase.cs:8-17`):

```
 Start: energy -> energy regeneration -> regeneration -> bleed
 Planning: evolution (2 picks a player) -> speed (face down) -> timeline
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
+---------------------------------------------------------------+
```

- **The intent slot** is a card-sized rectangle (63.5 x 88.9 mm) below each Creature's board. The Intent is
  the Spell card itself, played face down. It is the mechanic that translates for free (translation.md 1.6).
- **The hand is concealed.** This is the one place the components have to work for their hidden information.
  A Creature's known Spells are **public** in the engine (`CreatureSnapshot.KnownSpells` is on both teams'
  snapshots), so an open hand would be faithful - but then a card leaving an open hand for the intent slot
  tells the opponent exactly which Spell it is, and the hidden Intent is gone. So the hand is held, and the
  public record moves to the **talent tree mat**: every unlock puts a pip on the mat, where anyone can read
  what a Creature knows. Public information stays public; the face-down card stays face down.
- **The pick tokens** sit on the mat's header. Two a Player a Round, returned at the end of Evolution.
- **The target markers** are three per Creature, in that Creature's colour, carrying its number. Reveal and
  target walks the whole timeline before anything resolves, so all six casts' markers are on the table at
  once: 18 markers, and a Creature's `Targeted by` row shows who is pointing at it.
- **Speed tokens** are placed face down on each board's Speed slot and turned together, which is what makes
  the Speed choice the genuine simultaneous decision it is in the engine
  (`PlayerBoardStateProjection.cs:37`).

### 3.8 How a cast is declared and resolved, in components

1. **Intent**: put a Spell card from the hand face down in the Creature's intent slot. Legal if the Creature
   knows it - the pip is on the mat - and the Energy rail is at or above the printed cost.
2. **Reveal**: at the Creature's slot on the initiative track, turn the card face up and place its target
   markers, one per target, in the `Targeted by` boxes of the targets' boards. Up to `maxTargets`, and fewer
   is allowed. Do this for all six slots before resolving any.
3. **Resolve**, in the same order: check the Fizzle conditions, roll the die if the card prints a chance, move
   the Energy marker down by the cost, apply each effect line to each target, then the caster line, then take
   the markers back.

---

## Part 4. The talent tree as an object

Three classes, three sub-classes each, three Spells a sub-class, plus the three starting Spells: 3 + 6 + 27 =
36. A Player makes two picks a Round, and the second pick sees the first (`Match.cs:142`), so the tree is the
Match's arc and has to be readable without tracing prerequisites by hand.

### 4.1 The layout

One mat a Player, A4 portrait, three horizontal class bands. Each band is the class gate, its two opener
Spells, and its three sub-classes side by side; each sub-class is its gate, its free Spell, and the two Spells
behind it.

```
+-------------------------------------------------------------------------+
|  Start: Basic Attack [1][2][3]   Heavy Strike [1][2][3]   Wait [1][2][3] |
|-------------------------------------------------------------------------|
|  BRAWLER            needs Basic Attack + Heavy Strike + Wait             |
|  Pummel [1][2][3]              Guard [1][2][3]                           |
|  +--- MERCENARY ------+ +--- WARLORD --------+ +--- BERSERKER ---------+ |
|  | needs Pummel or    | | needs Pummel or    | | needs Pummel or       | |
|  | Guard              | | Guard              | | Guard                 | |
|  | Protective Slam    | | Full Plate         | | Enraged Charge        | |
|  |      [1][2][3]     | |      [1][2][3]     | |      [1][2][3]        | |
|  |   |                | |   |                | |   |                   | |
|  |   +-> Chain Slash  | |   +-> Restorative  | |   +-> Tornado         | |
|  |   |     [1][2][3]  | |   |     Gush       | |   |     [1][2][3]     | |
|  |   +-> Thundering   | |   |     [1][2][3]  | |   +-> Psycho Rush     | |
|  |         Seal       | |   +-> Crushing     | |         [1][2][3]     | |
|  |         [1][2][3]  | |         Stomp      | |                       | |
|  +--------------------+ +--------------------+ +-----------------------+ |
|-------------------------------------------------------------------------|
|  SCOUNDREL          needs Basic Attack + Heavy Strike + Wait             |
|  ... Leech, Assassin, Trickster                                          |
|-------------------------------------------------------------------------|
|  SORCERER           needs Basic Attack + Heavy Strike + Wait             |
|  ... Wizard, Necromancer, Shaman                                         |
+-------------------------------------------------------------------------+
```

What each piece of the layout answers:

| Choice | Why |
| --- | --- |
| Three pip boxes on every Spell, labelled with that Player's Creature numbers | One mat serves three Creatures, and the whole Team's progress is one glance. Six mats, one a Creature, would be three times the paper and would hide the comparison a Player actually makes: which of my three is deep in which branch. |
| The gate is printed on the band and on the box, not on every Spell | Nine sub-class gates are nine identical lines ("needs Pummel or Guard"); printing them 27 times is noise. The **card** carries its own full gate for the cases where the mat is not in reach. |
| An arrow from a sub-class's free Spell to the two behind it | `chain_slash` and `thundering_seal` require `protective_slam` (`prerequisites.allOf`). The arrow is the prerequisite, and it is why these two sit at tier 3 while `protective_slam` sits at tier 2 (ADR 0034: a Spell sits one deeper than the deepest Spell it requires). |
| `allOf` drawn as `+`, `anyOf` drawn as `or` | The two gates in the tree read differently and mean differently: a class needs **all three** starting Spells, a sub-class needs **either** opener. |
| A `2 picks a round` reminder beside the header, with "the second pick sees the first" | The picks are sequential, not simultaneous (`Match.cs:142`), so a Creature can open a sub-class and take one of its Spells in the same Round. It changes how fast the tree opens and is invisible otherwise. |

### 4.2 How an unlocked spell reaches the hand, and how the initiative gain is recorded

An unlock is **three actions in a fixed order**, printed on the mat beside the pick tokens:

1. **Pip.** Put a pip in that Creature's box on the Spell's slot. This is the public record that the Creature
   knows the Spell, and it is what the opponent reads instead of the concealed hand.
2. **Card.** Take that Spell's card from the library and put it in the Creature's hand. The library is the 216
   cards sorted by class and tier; six copies of each sit behind one divider.
3. **Initiative.** Move that Creature's Base initiative rails up by the card's printed `Unlock: +N
   initiative`. **This is the only place Base initiative ever moves** (`Creature.cs:106`, ADR 0017), which is
   why the card prints it and the mat's reminder names it. Values in `data/` are 0, 1, 2 or 3, so it is one
   marker move on the units rail, sometimes carrying into the tens rail.

Two rules of the sub-phase that the components carry rather than the rulebook:

- **A refused unlock raises nothing** (`Creature.cs:92-104`): a Spell already known already has its pip, and a
  box that already holds a pip cannot take a second. The component refuses the mistake.
- **A starting Spell grants no Spell initiative** (ADR 0017). The three starting slots on the mat are printed
  with their pips already in place and no `Unlock` value, so a Player never adds initiative for them. It is an
  asymmetry that will be asked about, and the rulebook owes it a sentence.

---

## Part 5. The generator, specified

Interface and behaviour only. **No code is written in this branch.** The generator is built where code is
built, with its own pull request and the usual gate.

### 5.1 Inputs

| Input | What it is | Why it is this and not something else |
| --- | --- | --- |
| `data/dst/game.schema.json` | The consolidated, validated catalogue the data builder writes (ADR 0009) | It is the only place aliases are resolved, references are validated and `"enabled": false` items are pruned. Reading `data/Spells/**` would print cards for content a build does not have, and would have to reimplement the pruning rules of `data/README.md`. |
| `data/dst/game.schema.sha256` | The content hash | The stamp every sheet carries, and the identity of the deck. |
| A rule set document | Team size, energy a Round, picks a Round, Round cap, critical multiplier | **The content hash does not cover the `RuleSet`**, and half the counts in Part 1 come from it. A deck plus a set of boards is only valid for a content hash **and** a rule set, so both are stamped. Today the rule set lives in code (`RuleSet.Default`); the generator needs it as a file, which is a question in Part 6. |
| A die | Which die the critical thresholds are printed for | Open. Until it is settled, the generator prints the chance as a percentage and omits the threshold line. |

Cards are **generated, never transcribed**. A tuning pass reprints the deck rather than invalidating it, which
is the whole reason phase 3 specifies a generator instead of a table of card texts.

### 5.2 Outputs

- **Card sheets**: the 36 faces, each repeated 2 x team size times, laid out 9 to a sheet.
- **Component sheets**: 6 creature boards, 2 player mats, 2 talent tree mats, the initiative track, the round
  track, and the token sheets.
- **A manifest page**: Part 1's table, generated, with the content hash, the rule set, and the date. It is the
  page that tells a reader whether their box matches their game.
- **A card back**, one design, optional to print.

The format is **a static page that prints**: HTML and CSS with `@page` at the sheet size, printed to PDF by
the browser. No PDF library, no build step, nothing installed - the same weight as the viewer and the studio
(ADR 0015's reasoning, ADR 0024's constraint).

### 5.3 The sheet

| Constraint | Choice | Why |
| --- | --- | --- |
| Paper | A4 (210 x 297) and US Letter (216 x 279), the same layout on both | 3 x 63.5 = 190.5 mm wide and 3 x 88.9 = 266.7 mm tall, plus 3 mm bleed on the outer edge, is 196.5 x 272.7 mm. It fits inside both. One layout, two papers. |
| Cards a sheet | 9 | The 3 x 3 grid above. 216 cards is **24 sheets**. |
| Bleed | 3 mm on the outer edge only; cards abut inside the grid | Neighbours share a cut line, so no bleed is wasted between them and a single cut serves two cards. |
| Cut marks | Hairline marks in the outer margin, at every grid line, never across a card | A mark that crosses the card is printed on the card. Marks in the margin survive a guillotine and a craft knife. |
| Fold marks | None | Cards are cut, not folded. Boards are printed one to a face. |
| Colour | Everything readable in greyscale; class colour is a strip **and** a printed class name | Home printers run out of one ink. A card that only says "Necromancer" in purple stops saying it. |

### 5.4 Where it lives

A new static directory, `printshop/`, beside `viewer/` and `studio/`: `index.html`, one stylesheet, and ES
modules with no framework and no build step. It is **not** in the engine's layers, references nothing under
`src/`, and nothing references it.

It reads its inputs the way the hosted studio reads the catalogue (ADR 0023): the local CLI host serves the
directory and the built content, and `studio --export` already writes the same files beside the published
page, so the printshop published to Pages prints from what CI built. The transport is **injected**, exactly as
ADR 0024 requires of `backend.js`: the module takes its `fetch` as an argument, so a test drives it with a
stub.

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
| A face carries cost, targeting, every effect with its amount and Duration, every caster effect, the printed critical chance, the class, the tier and the prerequisites | This is Part 2 as an assertion. A missing Duration is a card that cannot be resolved. |
| Tier follows ADR 0034: a Spell sits one deeper than the deepest Spell its prerequisites name | Node depth alone prints 27 Spells as tier 2, and the tree a Player climbs is 3 / 6 / 9 / 18. |
| The prerequisite line names the node gate and the Spell gate, and renders `anyOf` as "any of" | An `allOf` printed as an `anyOf` is a card that lies about what unlocks it. |
| The copy count is 2 x the rule set's team size | A rule set change reprints the deck; it must not need an edit. |
| Rendering fails, loudly, when a body line exceeds the text area or a body exceeds 4 lines | The measurement in 2.3 holds for today's content. A tuning pass that lengthens a Duration or adds an effect must break the build rather than clip the card. |
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

Phase 3 is done, per plan.md, when that produces a print-and-play PDF from a clean checkout and the deck it
prints matches the hash of the content it was built from.

---

## Part 6. Open questions

Each one is a count or a choice this document cannot derive. None is answered here.

### 1. Which die

Recommended: **d20**, for the reason in [1.6](#16-dice) - two candidate grids sit inside the 0.05 step
`knobs.json` already declares on 20 Spells, d10 and d20, and the d20 is the finer of the two: it moves 10 of
the 21 Spells rather than 13, its worst move is 0.02 rather than 0.05, and it can still express the 0.75
`crushing_stomp` is on. Alternatives and their
costs are the table in 1.6. Two things come with the answer, whichever it is: `revenant_guards` prints a
chance and has **no critical chance knob**, and `lightning_bolt`'s knob band starts at 0.17, so its own grid
contains no multiple of 0.05. Both are content changes with a journal entry and a new hash.

### 2. The deck's copy count

- **216 cards** (this manifest). Any legal game is playable. 24 sheets, which is most of the print-and-play.
- **Six copies of the three starting Spells and two of each of the other 33: 84 cards, 10 sheets.** A Match
  uses at most 82 cards, so this is enough for almost every game - and a game where three Creatures learn the
  same Spell runs out, which is a rule change by the back door and fork A forbids it.
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

`RuleSet.Default` is a static in `src/DownfallArena.Domain/Matches/RuleSet.cs`. The generator needs team size,
picks a Round, energy a Round and the Round cap as **input**, and every sheet has to be stamped with them. Add
a rule set document to `data/` and have the engine read it, write one out with `studio --export`, or pass the
five numbers to the generator on the command line? The first makes the rule set content, which is a decision
with consequences well beyond a print sheet.

### 7. The talent mat: two or six

Two mats (one a Player, three pip boxes a Spell) is this manifest's choice: the whole Team's arc on one sheet.
Six mats (one a Creature) is three times the paper, and it gives each Creature its own uncluttered tree. The
answer is a playtest reading, not a derivation.

### 8. Card backs

A single back design costs 24 more sheets and doubles the print, for a deck whose hands are concealed and
whose Intents are played face down - so the backs must at least be uniform. Printing single-sided and sleeving
the cards with an opaque backing card is the cheaper answer and needs sleeves. Which one the print-and-play
assumes changes the sheet count from 35 to 59.

---

## Part 7. Coverage: the 41 "needs a component" rows

Every **needs a component** verdict in [translation.md](translation.md), and what answers it. 14 from Part 1,
8 from Part 2, 19 from Part 3; 41 of 41.

### The 14 sub-phase rows

| translation.md row | Component |
| --- | --- |
| 1.1 Energy gain per Round | The Energy rail, [1.3](#13-stat-markers-and-the-rails-they-ride) and [1.7](#17-the-energy-track-what-ends-it) |
| 1.3 Two picks a Round, per Player | 4 Evolution pick tokens, [1.5](#15-the-rest-of-the-pieces) |
| 1.3 Prerequisites, `allOf` and `anyOf` | The talent tree mat, [Part 4](#part-4-the-talent-tree-as-an-object), and the `Requires:` line on every card, [2.1](#21-what-is-printed-and-where-it-comes-from) |
| 1.3 An unlock raises Base initiative | The two Base initiative rails and the card's `Unlock: +N initiative`, [3.4](#34-initiative-two-small-rails-instead-of-one-long-one) and [4.2](#42-how-an-unlocked-spell-reaches-the-hand-and-how-the-initiative-gain-is-recorded) |
| 1.4 One Speed choice per Creature | 6 two-sided Speed tokens and the Speed slot a Stun fills, [3.1](#31-the-creature-board) |
| 1.5 The Combat timeline | The initiative track and 6 numbered markers, [3.5](#35-the-initiative-track) |
| 1.5 Current initiative | The Base initiative rails read with the dock's Initiative tokens, [3.4](#34-initiative-two-small-rails-instead-of-one-long-one) |
| 1.5 Ties by Player slot then Creature id | The number 1 to 6 printed on each board, [3.1](#31-the-creature-board) |
| 1.7 Reveal in timeline order, bind targets at reveal | 18 target markers and the `Targeted by` row, [3.7](#37-the-player-area-and-where-a-face-down-intent-sits) |
| 1.8 One critical roll a cast | The die, [1.6](#16-dice), and the card's printed chance |
| 1.8 Total Defense | The two Defense rails, [3.3](#33-defense-two-rails-because-the-floor-is-applied-once) |
| 1.8 A lasting Effect attaches as a Condition | The 138 Condition tokens and the dock, [1.4](#14-condition-tokens) and [3.2](#32-the-condition-dock-and-the-countdown) |
| 1.8 `Stack` adds another Condition | The same, plus the supply rule and the blank tokens |
| 1.9 Every Condition counts one Round down | The dock's four lanes and the two-step Cleanup, [3.2](#32-the-condition-dock-and-the-countdown) |

### The 8 effect kinds

| Effect kind | Token, and its supply |
| --- | --- |
| `Bleed` | 48 tokens: 18 at 1, 18 at 2, 6 at 3, 6 at 4 |
| `Regeneration` | 6 tokens at 3 |
| `EnergyRegeneration` | 6 tokens at 2 |
| `Stun` | 6 tokens, one a Creature, because a Stun refreshes |
| `DefenseBuff` | 30 timed tokens (6 at +1, 18 at +2, 6 at +3); a permanent buff moves the rail and needs none |
| `DefenseDebuff` | 18 timed tokens at -2; a permanent debuff moves the rail |
| `InitiativeBuff` | 18 tokens at +2 |
| `InitiativeDebuff` | 6 tokens at -2 |

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
| `ice_spear` | 1 Initiative debuff -2 (1 round) |
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

- The die. Recommended, not settled ([Part 6](#part-6-open-questions), question 1).
- Any rule. Where a component could not be built without one - a cap on Energy, a bound on permanent Defense -
  the question went back to the audit's ADR candidates 2 and 3 and to the `boardgame-director`, not into a
  component that quietly does something else.
- The rulebook's words. Phase 4 owns the teaching order, the examples and the player aid; this document owes
  it the printed reminders in [3.6](#36-the-round-track) and the affordance table in
  [3.1](#31-the-creature-board), which are the rules the components are already carrying.
