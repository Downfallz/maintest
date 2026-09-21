# Tier evolution: stage 0 inventory

Status: **inventory only** (2026-09-21). Nothing is decided and no code has moved. This is the checkout audit
the migration plan's stage 0 asks for: what the tree is today, what the 21 packages map onto, which surfaces
the change touches, and the hazards a mechanical migration would walk into.

Read with `tier-evolution-plan.md`, which sits beside it. Where the two disagree, this file is the one that
was measured.

## 1. The tree as it exists

Thirteen nodes, one root plus three openers plus nine specializations. Thirty-three acquired spells and three
starting spells, which matches the plan's arithmetic exactly (36 files under `data/Spells`).

```
BaseCreature     [3] basic_attack, heavy_strike, wait
  Brawler        [2] pummel, guard
    Mercenary    [3] protective_slam, chain_slash, thundering_seal
    Warlord      [3] full_plate, restorative_gush, crushing_stomp
    Berserker    [3] enraged_charge, tornado, psycho_rush
  Scoundrel      [2] poison_slash, throwing_star
    Leech        [3] parasite_jab, hateful_sacrifice, soul_devourer
    Assassin     [3] momentum, death_squad, mortal_wound
    Trickster    [3] noxious_cure, tranquilizer_dart, infectious_blast
  Sorcerer       [2] lightning_bolt, rejuvenate
    Wizard       [3] meteor, engulfing_flames, ice_spear
    Necromancer  [3] summon_minions, revenant_guards, crazed_specter
    Shaman       [3] healing_screech, toxic_waves, restorative_burst
```

**The plan's class names are a rename, not a reading.** The tree says `Brawler`, `Mercenary`, `Warlord`,
`Scoundrel`, `Leech`, `Trickster`, `Sorcerer`, `Wizard`; the plan says Brute, Marauder, Ironbound, Prowler,
Parasite, Plague Doctor, Occultist, Elementalist. Both describe the same spells. The plan never claims
otherwise, but a reader who takes its table as the current state will not find these nodes.

## 2. Old node to new package

The split rule is mechanical and holds for all nine specializations: **the first spell becomes the tier 2
package, the remaining two become the tier 3 package.** Every one of the plan's 33 spells lands where the
plan's table puts it, and no spell is orphaned or duplicated.

| current node | → tier 1 | → tier 2 | → tier 3 |
| --- | --- | --- | --- |
| `Brawler` | **Brute** — pummel, guard | | |
| `Mercenary` | | **Marauder** — protective_slam | **Warmonger** — chain_slash, thundering_seal |
| `Warlord` | | **Ironbound** — full_plate | **Dreadnought** — restorative_gush, crushing_stomp |
| `Berserker` | | **Berserker** — enraged_charge | **Ravager** — tornado, psycho_rush |
| `Scoundrel` | **Prowler** — poison_slash, throwing_star | | |
| `Leech` | | **Parasite** — parasite_jab | **Soulreaver** — hateful_sacrifice, soul_devourer |
| `Assassin` | | **Assassin** — momentum | **Deathstalker** — death_squad, mortal_wound |
| `Trickster` | | **Plague Doctor** — noxious_cure | **Blightweaver** — tranquilizer_dart, infectious_blast |
| `Sorcerer` | **Occultist** — lightning_bolt, rejuvenate | | |
| `Wizard` | | **Elementalist** — meteor | **Harbinger** — engulfing_flames, ice_spear |
| `Necromancer` | | **Necromancer** — summon_minions | **Lich** — revenant_guards, crazed_specter |
| `Shaman` | | **Shaman** — healing_screech | **Spiritcaller** — toxic_waves, restorative_burst |

`BaseCreature` is not a package: its three spells stay the starting kit.

## 3. Hazards a mechanical migration walks into

### 3.1 Four names survive with a different meaning

`Berserker`, `Assassin`, `Necromancer` and `Shaman` are node names today **and** package names after the
migration — but they name the tier 2 opener, not the whole specialization. Spells carry the old node name in
`creatureClass`:

| `creatureClass` today | spells | still correct after? |
| --- | --- | --- |
| `Necromancer` | summon_minions, revenant_guards, crazed_specter | only summon_minions; the other two are `Lich` |
| `Berserker` | enraged_charge, tornado, psycho_rush | only enraged_charge; the rest are `Ravager` |
| `Assassin` | momentum, death_squad, mortal_wound | only momentum; the rest are `Deathstalker` |
| `Shaman` | healing_screech, toxic_waves, restorative_burst | only healing_screech; the rest are `Spiritcaller` |

**A migration that leaves `creatureClass` alone produces data that validates and is wrong**, for these four
families and twelve spells. Every other family fails loudly, because `Mercenary` and the rest stop existing.
So the four that look safest are the four to check: the migration must rewrite `creatureClass` for all 33
acquired spells, and any validation that only checks "the class exists" will pass this corruption.

### 3.2 The initiative baseline is uneven, with numbers

Summing the former per-spell bonuses, as the plan proposes for the migration baseline:

| package | tier | initiative from the sum |
| --- | --- | --- |
| Brute | 1 | 1 |
| Occultist | 1 | 2 |
| Prowler | 1 | **3** |
| Shaman | 2 | **0** |
| Marauder, Ironbound, Berserker, Parasite, Plague Doctor, Elementalist, Necromancer | 2 | 1 |
| Assassin | 2 | **3** |
| Ravager, Lich | 3 | 2 |
| Dreadnought, Harbinger, Spiritcaller | 3 | 3 |
| Warmonger, Blightweaver | 3 | 4 |
| Soulreaver, Deathstalker | 3 | **5** |

Tier 1 spans 1 to 3, a threefold spread between packages a creature chooses between at round 1. Tier 2 spans
0 to 3, and **`Shaman` would grant no initiative at all** — a purchasable package whose initiative benefit is
nothing. Tier 3 spans 2 to 5.

The plan already says this baseline is not a balance guarantee. The numbers say how far from one it is: these
spreads were never authored as package values, they are the accident of how many spells happened to sit in a
node. Any validation rule of the "nonnegative" kind passes all of them, `Shaman`'s zero included.

### 3.3 The cadence does not fit the matches

`RuleSet.Default` is `(3, 2, 2, 30, 2.0)`: team size 3, energy 2, **2 evolution picks per round, every
round**, round cap 30. The plan proposes one pick at rounds 1, 3, 5.

Against the current benchmark digest, 400 entries:

| | |
| --- | --- |
| Rounds, mean | **6.79** |
| Median | 6 |
| 90th percentile | 8 |
| Entries reaching round 17 or later | **12 of 400** (3 %) |

So a match contains roughly **four** evolution opportunities under the proposal, against roughly fourteen
today. The plan notes that three creatures cannot all reach tier 3 before round 17; what it does not say is
that 97 % of matches are over before then. A team ends a typical match with about four of its nine packages
bought, and most of the 21-package tree is never seen in play.

That may be the intent — choices that cost something, a climb you do not finish. It is not written down as
the intent, and "measure it in playtests before changing it" arrives too late if the cadence is already in
`RuleSet` and its serialized form. **How many purchases a match should contain is a question for before
stage 1, not after stage 6.**

## 4. Surfaces, counted

Tracked files containing each term, excluding `legacy/` and excluding these two audit documents themselves.
Counted at `99d1239` with one command, so the numbers can be reproduced and will move as the repository does:

```bash
git ls-files -z | grep -zv '^legacy/' | grep -zv '^docs/domain/tier-evolution-' \
  | xargs -0 grep -lE '<term>' | wc -l
```

| term | files |
| --- | --- |
| `creatureClass` / `CreatureClass` | **69** |
| `talentTree` / `TalentTree` | **64** |
| `EvolutionChoice` | **47** |
| `EvolutionOption` | 33 |
| `DecideEvolution` | 23 |
| `SpellInitiative` | 10 |
| `TalentUnlocks` / `EvolutionRules` | 10 |
| `UnlockValue` / `UnlockTerms` | 7 |

An earlier draft of this table read 70 and 67 for the first two rows. It was produced by grepping a list of
directories rather than the tracked set, which swept in three git-ignored files of generated output under
`data/dst`, and it did not say whether this document counted itself. Both were the same mistake: an audit
whose method is not written down is not an audit. These are the call sites stage 0 is asked to enumerate, and
they remain a floor rather than a ceiling, because a rename reaches things none of these strings name.

`EvolutionChoice` by area: Application/Agents (5), Cli and Cli/Table (4), Application/Matches
Commands + Driving + Projections (6), Domain/Matches/Rounds (2), tests across Domain and Application (8),
plus a recorded trace in `viewer/samples`. That last one matters: **a sample artifact carries the old shape**,
so the viewer's compatibility work in the plan's section 12 has a concrete first case in the repository.

Python: `learning/src/downfall_learning/` — `knobs.py`, `tune_content.py`, `export.py`, `cli.py`.

ADRs to supersede exist as the plan says: `0017-spell-initiative-on-unlock.md` and
`0034-a-tier-is-a-depth-a-player-climbs.md`.

## 5. Confirmed from the plan, verbatim where it matters

- `ActionEncoder.cs:28` encodes evolution as `evolve:{slot}:{choice.Spell.Value}` with
  `schema.SpellIndex(choice.Spell)`. The plan's fear that a `spellIndex` would be silently reinterpreted as a
  `tierIndex` is real, not theoretical.
- `SpellStats` is `record SpellStats(Initiative SpellInitiative, Energy Cost, CriticalChance CriticalChance)`,
  so removing the field changes a positional record every construction site depends on.
- 36 spell files: 33 acquired plus 3 starting, as the plan's totals require.

## 6. What stage 0 leaves open

- **The purchases-per-match question** (3.3). It decides the cadence parameters, and the cadence is stage 1.
- **Authored tier initiative** (3.2), which the sum cannot supply. Somebody chooses 21 numbers.
- **The benchmark baseline.** The mirror currently reads 70.5 % for Player 1 after the speed rule landed
  (#160), and why is not understood. A tier migration moves initiative, which is what the timeline orders by,
  so regenerating the baseline after both changes would make the two causes inseparable. Either the 70.5 % is
  explained first, or stage 6 records a baseline nobody can attribute.
