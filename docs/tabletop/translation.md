# Translation audit

Status: **Evidence** (2026-09-14; Part 1 and Part 4 re-audited 2026-09-23, and their Evolution rows re-read
for ADR 0066 the same day). Phase 1 of [plan.md](plan.md).

**Two readings, and each Part says which it is.**

- **Part 1, Part 4 and Part 5 are read at content `4d7a841c`** — the hash
  `dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst` writes today — with the engine of
  this branch. That engine includes
  [ADR 0056](../adr/0056-a-pick-buys-a-package-every-other-round.md) (a pick buys a whole Tier; two picks at
  Round 1 and every second Round after; one initiative bonus a purchase),
  [ADR 0057](../adr/0057-a-package-is-authored-not-derived.md) (the 21 Tiers are authored in `data/Tiers`),
  [ADR 0058](../adr/0058-a-tier-is-the-package-the-balance-objective-reads.md) (a tier is the package, not a
  depth in the Talent tree), [ADR 0059](../adr/0059-retire-the-spell-initiative-the-package-pays-it-now.md)
  (no Spell initiative), [ADR 0063](../adr/0063-an-initiative-tie-is-rolled-on-a-d20.md) (a tie between the
  sides is rolled off on a d20, and each side orders its own tied Creatures in `TieOrder`, the eleventh
  sub-phase), [ADR 0066](../adr/0066-a-creature-buys-one-package-an-opportunity.md) (a Creature buys at most
  one Tier an opportunity, so the two picks go to two Creatures), and #160 (a `Quick` Creature rolls no
  critical, which game-rules.md states and no ADR records). The Evolution rows (1.3), the timeline rows
  (1.5), the critical rows of 1.9 and Candidate 3 were rewritten for them, and the `TieOrder` rows (1.6) and
  Candidate 6 are new. The rest of Part 1 was checked against the same engine and content; only the targeting
  counts in 1.8 had moved. Every citation into a file those changes rewrote — `Match.cs`, `Creature.cs`,
  `RuleSet.cs`, `Round.cs`, `ResolutionRules.cs`, the Planning rules, the board projection,
  `GameSchemaMapper.cs`, `ConditionTests.cs` — was re-pointed at the current line; ADR 0066 changed
  `EvolutionRules.cs` again, and its citations were re-pointed after it. Citations into files they left alone
  (`UpkeepRules.cs`, `ConditionSet.cs`, `Condition.cs`, the other Combat rules) were not re-checked line by
  line: the file and the method they name are right, and a few line numbers have drifted since the first
  audit (`ConditionSet.cs` and `Condition.cs` among them).
- **Parts 2 and 3 are still read at content `938bef5e`**, what
  [ADR 0043](../adr/0043-a-control-spell-is-not-an-attack-and-reach-is-not-force.md) left in `data/` after tune
  run 8, and are left there on purpose: re-reading them is a re-measurement of card text that
  [components.md](components.md) §2.3 owns, and it should be done once, with it. The rules they describe have
  not changed; the catalogue has. Tune run 9 and #172 moved ten Spells since, and on this document's own
  counting rule the differences at `4d7a841c` are these. Part 2: `EnergyDrain` is 3, not 2 (`soul_devourer`,
  whose damage is 6); `InitiativeDebuff` is 1 for 1 Round (`ice_spear`) and 2 for 2 Rounds
  (`protective_slam`), so "every `InitiativeDebuff` is exactly 2" no longer holds; `mortal_wound`'s Bleed 4
  lasts 1 Round; `pummel` deals 2. Part 3: `throwing_star` is Multi, up to 2 enemies, at cost 2, so it costs
  7 operations and moves from the trivially playable table to the middle one (17, 12 and 7 instead of 18, 11
  and 7), where its **keep as is** is to be re-read, since a second target is a choice a card must show;
  `ice_spear` costs 2 and `revenant_guards` 3; four Critical chances moved
  (`healing_screech` 0.55, `meteor` 0.35, `parasite_jab` 0.45, `tornado` 0.38), so 13 distinct chances are
  rolled instead of 11, still with 15 Spells at zero. No effect kind gained or lost a Spell.

Every count, value range and tracking cost below is its Part's catalogue and no other: a tuning pass moves
them, so rebuild and re-read this document's numbers whenever the hash moves.

## What this is, and what it is not

This document is the evidence the tabletop rule set is decided from. One row per mechanic: what the engine
does and where that is enforced, what a player does by hand to get the same result, a verdict, and what the
verdict costs. It decides nothing. It picks no number, no component, no card layout and no round cap; it
raises the questions those choices answer and hands them to the maintainer as ADR candidates. Three of the
four forks of [plan.md](plan.md) are settled and the verdicts here obey them: the board game is a faithful
port, so **cut from the tabletop rule set** is not an available verdict and **simplify (ADR)** always means
the engine changes too; a critical is a die roll, so the continuous Critical chance bonuses are reported as a
snapping job rather than argued about; and a divergence is an engine change with an ADR, never a table-only
exception. Counts and value ranges about the content are computed from `data/`, never from
[spells.md](../domain/spells.md), which is a historical record and has drifted: comparing its 36 rows against
`data/` at `938bef5e`, when a Spell still carried an initiative, on Spell initiative, energy cost, Critical
chance bonus, effect amounts and Durations, and the presence of a Caster effect, **33 of the 36 differ** and
only three still match (`chain_slash`, `guard`, `heavy_strike`). `basic_attack` deals 1 there and 2 here;
`engulfing_flames` 9 there and 10 here; `summon_minions` is an `EnergyGain 3` there and three Bleeds here.

Verdicts are exactly one of: **keep as is**, **restate**, **needs a component**, **simplify (ADR)**.

The numbers in the tracking columns are counts, not judgements. "Operations" means arithmetic or comparison
steps a player performs out loud; "tokens" means physical markers placed or moved; "lookups" means reading a
value off another component. Whether a count is too high is a playtest reading, not a claim made here.

## The board this audit assumes

`RuleSet.Default` (`src/DownfallArena.Domain/Matches/RuleSet.cs:30`): 3 Creatures a Team, 2 Energy a Round,
2 Evolution picks an opportunity, the first opportunity at Round 1 and one every second Round after, a
30-Round cap, a critical multiplier of 2.0. One Creature definition, `data/Creatures/main.v1.json`: Health
20, Energy 0, Defense 0, Base initiative 5, Critical chance 0 (ADR 0042), knowing `basic_attack`,
`heavy_strike` and `wait`. 21 Tiers in `data/Tiers` (ADR 0057): 3 at level 1 that require nothing and sell 2
Spells each, 9 at level 2 that sell 1 each, 9 at level 3 that sell 2 each, every one of levels 2 and 3
requiring exactly one Tier a level below. That is three families of seven: each level-1 Tier opens one, with
three level-2 Tiers on it and one level-3 Tier on each of those. The 33 Spells outside the starting kit are
each sold by exactly one Tier. The enabled Talent tree,
`data/TalentTrees/talent_tree.v1.json`, is still loaded, but it gates nothing a pick buys (ADR 0056,
ADR 0058). So six identical Creatures start a Match, each knowing the same three Spells, each free to buy
into any family.

One Round therefore asks for: up to 4 Evolution choices on a Round that offers an opportunity and none on
the others, 6 Speed choices, at most 2 Tie orders, 6 Intents, 6 target bindings and 6 Resolutions. Thirty
Rounds is the cap the engine plays to today.

---

## Part 1. The eleven sub-phases of ADR 0010

ADR 0010 named ten; ADR 0063 added `TieOrder` between `TurnOrderResolution` and `IntentSelection`. In the
order of `src/DownfallArena.Domain/Matches/Rounds/RoundSubPhase.cs:8-18`. Each mechanic appears in exactly
one sub-phase, filed where it is enforced. The sections after 1.5 are one number higher than in the first
audit: what was 1.6 `IntentSelection` is 1.7, and so on to 1.11 `Finalization`.

### 1.1 `EnergyGain` (Start of round)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| Energy gain per Round | Every living Creature gains `RuleSet.EnergyPerRound` (`Rules/Rounds/UpkeepRules.cs:13-22`) | 6 token moves, no arithmetic if the track is a dial; 0 lookups | **needs a component** | An Energy track on each creature board, six of them. Nothing is lost. |
| The dead gain nothing | `creatures.Where(creature => creature.IsAlive)` (`UpkeepRules.cs:18`) | 0, a dead creature board is turned over | **keep as is** | Nothing. |
| Energy has no maximum | `Energy` is a `NonNegativeStat` with no ceiling (`SharedKernel/Stats/Energy.cs:3`); `GainEnergy` never clamps (`Creatures/Creature.cs:279-290`) | A track must end somewhere. A Creature casting `wait` every Round nets +4 a Round and spends nothing: 120 Energy over 30 Rounds | **needs a component** | An Energy track that ends, plus something for what passes it. Nothing is lost: ADR candidate 2 is settled the other way — the engine keeps no maximum and [components.md](components.md) §1.7 carries it. |

### 1.2 `OngoingEffects` (Start of round)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| The three passes, in order | Energy regeneration ticks, then Regeneration ticks, then Bleed ticks, each pass over every living Creature (`UpkeepRules.cs:35-73`, ADR 0019, ADR 0020) | 3 passes over 6 creature boards; 1 lookup per Condition token | **restate** | One sentence in the rulebook: energy, then healing, then bleeding. Nothing is lost. |
| Healing before Bleed is load-bearing | A Regeneration can carry a Creature through a Bleed that would have killed it (`UpkeepRules.cs:24-28`) | 1 comparison per Creature carrying both | **restate** | Nothing; the order has to be printed on the player aid or it will be got wrong. |
| A Bleed tick ignores Defense | `creature.TakeDamage(asked.Total)` with no Defense term (`UpkeepRules.cs:67`) | 1 subtraction, and the player must *not* read the Defense track | **restate** | Nothing. It is the only damage in the game that skips Defense, so it is the one players will get wrong. |
| A Regeneration tick is capped by Health missing | `Creature.Heal` clamps to `MaxHealth - Health` (`Creature.cs:271`) | 1 comparison | **keep as is** | Nothing. |
| An Energy regeneration tick is never wasted | Energy has no maximum, so nothing clamps (ADR 0020) | 1 addition | **keep as is** | Nothing, but it inherits the unbounded track of the "Energy has no maximum" row in 1.1. |
| Condition source and the tick shares | Every tick is split across the casts behind it by largest remainder (`UpkeepRules.cs:81-170`, ADR 0027) | **Zero.** The split changes no Health, no Energy, no death and no order — ADR 0027 says so and the benchmark digest did not move | **keep as is** | Nothing at the table: it is a reading for the learning pipeline, invisible on a board. See ADR candidate 4. |

### 1.3 `Evolution` (Planning)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| An opportunity at Round 1 and every second Round after | `RuleSet.IsEvolutionRound` and `EvolutionPicksIn` (`Matches/RuleSet.cs:80-84`), at 1, 2 and 2 by default (`RuleSet.cs:30`); the one schedule the validation, the gate and the projections all ask (ADR 0056) | 1 look at the Round track a Round, 0 arithmetic if the opportunity Rounds are marked on it. The table's 8 to 16 Rounds ([plan.md](plan.md)) hold 4 to 8 opportunities | **needs a component** | A Round track with the opportunity Rounds marked. Nothing is lost; every other Round has one step fewer. |
| Two picks an opportunity, per Player, shared across the Team | `rules.EvolutionPicksIn(round.Number) - round.EvolutionChoicesOf(slot).Count` (`Rules/Planning/EvolutionRules.cs:100-103`), refused past it (`EvolutionRules.cs:49-52`) | 2 tokens moved from a Player's mat onto the boards of the Creatures that bought, on an opportunity Round; the choice is which two Creatures and which Tier for each | **needs a component** | Two pick tokens a Player. Nothing is lost. |
| The Players pick in turn, Player 1 first | The domain takes the two Players' picks in any order and applies each at once (`Match.cs:116-153`). The order is the driver's: it asks Player 1, then Player 2, for one pick each, every pass (`Application/Matches/Driving/MatchDriver.cs:26-48`), and every host plays through it, the table's included. A purchase is public the moment it lands, since the other Player's board state carries full snapshots of these Creatures (`Application/Matches/Projections/PlayerBoardStateProjection.cs:19-20`) | 1 pick each in turn, at most 4 an opportunity; 0 arithmetic. Player 2 chooses their first pick knowing Player 1's first purchase | **restate** | Nothing at the table, and the rulebook already says it (rulebook.md §5.3). But it is the one place the seat still orders anything since ADR 0063, and it lives in an Application loop, not in the domain: ADR candidate 6. |
| A pick buys a whole Tier | `Creature.BuyTier` records the Tier as owned and teaches every Spell it sells at once (`Creatures/Creature.cs:202-229`), called only once the choice is validated (`Match.cs:127-148`, ADR 0056) | 1 Tier card set beside the creature board and its 1 or 2 Spell cards taken from the library; 0 arithmetic. Any number of Creatures, of either Player, may own the same Tier | **needs a component** | Tier cards, 21 kinds, each showing its level, its prerequisite, its Spells and its bonus. Nothing is lost. How many copies of each the box holds, Spell cards included, is the component-designer's count, made from how often one Tier is owned twice in a Match, which is the tabletop-mathematician's measurement. |
| Prerequisites are the only rule, and the Talent tree gates nothing | `TierEligibility.AvailableTiers`: a Tier the Creature does not own whose prerequisites it owns (`Rules/Planning/TierEligibility.cs:23-42`), checked by `EvolutionRules.ValidateChoice` (`EvolutionRules.cs:61-73`) and again by `BuyTier` (`Creature.cs:216-219`). No family is closed to a Creature, so multiclassing is free (ADR 0056, ADR 0058) | 0 lookups for the 3 openers; 1 for any other Tier: is the one Tier it names beside this creature board. A Creature chooses from 3 Tiers at Round 1, and from 5 once it owns one opener (the 2 other openers and that opener's 3 level-2 Tiers) | **restate** | Nothing. The prerequisite is one line on the Tier card, and eligibility is read off the board, not computed. The Talent tree mat the first audit asked for is not needed to play; if the box keeps one, it is a map of the families, not a gate. |
| A Creature buys at most one Tier an opportunity | A choice for a Creature that has already bought this Round is refused with `Planning.CreatureAlreadyEvolved` (`EvolutionRules.cs:56-59`), read off the Round's own choices (`HasEvolved`, `EvolutionRules.cs:128-134`; ADR 0066). The two picks go to two Creatures, so neither depends on the other: a Tier the first opens is one only its buyer may buy, and its buyer is done for the Round. It replaces ADR 0056's sequential picks, under which the greedy mirror put both picks on one Creature in half its opportunities | 1 look a pick, 0 arithmetic: the pick token a purchase moves lies on the buyer's board until the Sub-phase ends, and a board holding one is not picked. The top of a family arrives at Round 5 at the earliest: `tier:brute:v1` at Round 1, `tier:ironbound:v1`, which sells `full_plate`, at Round 3, `tier:dreadnought:v1` at Round 5 | **restate** | Nothing: the pick tokens of the row above carry it, laid on the buyer's board instead of set aside (components.md §1.5). It has to be said, with the one-Creature case, since it is the only thing that ever refuses a pick for a Tier the Creature could otherwise buy. |
| A purchase raises Base initiative by the Tier's initiative bonus, once, for the Match | `BaseInitiative = BaseInitiative.Plus(tier.InitiativeBonus.Value)` (`Creature.cs:227`, ADR 0056). No Spell carries an initiative any more (ADR 0059) | 1 marker move on an initiative track, once, at the purchase. Bonuses in `data/Tiers`: 1, 2 or 3 at level 1; 0 to 3 at level 2; 2 to 5 at level 3; a level-3 Tier and the two it stands on add 4 to 11 together. The largest Base initiative the content can produce is 52: 5, plus all 21 bonuses, for a Creature sold every Tier. At one Tier an opportunity (ADR 0066), the table's 16 Rounds sell one Creature at most 8, and the most they can reach is 29 ([components.md](components.md) §3.4) | **needs a component** | An initiative track per creature board, and the bonus printed on the Tier card, not on a Spell card. Nothing is lost; it is one move a purchase, not a per-cast cost. Where the track ends is the component-designer's call, as the Energy track's was. |
| The starting kit raises nothing | The definition's `baseInitiative` is where a Creature starts (`Creature.cs:38`); the three starting Spells belong to no Tier (ADR 0058) | 0 | **keep as is** | Nothing. The asymmetry the first audit reported is gone: every other Spell is sold by exactly one Tier, so two Creatures that know the same Spells own the same Tiers and carry the same Base initiative. |
| A Spell already known is granted, not refused | `Creature.Learn` is idempotent (`Creature.cs:236-240`), so a Tier selling a known Spell is still bought (`Creature.cs:221-225`) | 0. Unreachable with this content: no Spell is sold by two Tiers, and no Tier sells a starting Spell | **keep as is** | Nothing. The rulebook need not say it until the content makes it reachable. |
| A refused purchase changes nothing | `BuyTier` checks a dead Creature, a Tier already owned and a missing prerequisite before it changes anything (`Creature.cs:206-219`); `ValidateChoice` has already refused all three (`EvolutionRules.cs:31-73`) | 0 | **keep as is** | Nothing. |
| Evolution pass | A Player gives up their remaining picks (`Match.cs:155-174`) | 1 declaration | **restate** | Nothing. |
| The sub-phase ends when no Player has an *effective* pick left, and at once on a Round without an opportunity | Remaining picks are the schedule's less those spent, capped by how many of the Player's living Creatures have not bought this Round and have a Tier available (`EvolutionRules.cs:83-126`, ADR 0066); a Round the schedule skips is complete as it opens | 0 on a Round without an opportunity: the step is skipped. On an opportunity Round the cap bites when a Player has fewer living Creatures than picks: a Player down to one living Creature has one pick. The Tier half never bites: a Creature has a Tier left to buy until it owns all 21, which takes 21 opportunities at one a Round, more than the 15 a 30-Round cap offers | **restate** | Nothing. Three sentences in the rulebook: skip Evolution on the Rounds the track does not mark, one Tier a Creature, and the step ends when both Players have spent, passed, or have no Creature left to buy for. |

### 1.4 `Speed` (Planning)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| One Speed choice per living, unstunned Creature | `SpeedRules.ValidateChoice` and `Evaluate` (`Rules/Planning/SpeedRules.cs:13-45`) | 6 cards placed, one per creature board | **needs a component** | Two Speed cards per Creature, a Quick and a Standard behind one back (components.md, Part 6, question 14, answered). Nothing is lost. |
| Speed choices are hidden until the timeline is built | A Player's board state carries only their own choices (`Application/Matches/Projections/PlayerBoardStateProjection.cs:38`) | 6 cards placed face down, the other 6 kept in hand, then flipped together | **restate** | Nothing, and it is a genuine simultaneous decision the plan's inventory did not name. |
| A stunned Creature skips the whole Round | No Speed choice, so no Activation slot, so no Intent (`SpeedRules.cs:34`, and the timeline is built from the Speed choices, `Rules/Planning/TimelineBuilder.cs:23-28`) | 1 lookup on the Stun token; the creature board takes no Speed card | **restate** | Nothing. This is what makes Stun the biggest effect in the game and it must be taught as "loses the Round", not "loses its attack". |

### 1.5 `TurnOrderResolution` (Planning, automatic)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| The Combat timeline | All Quick Activation slots, then all Standard, Current initiative descending inside each (`TimelineBuilder.cs:23-48`) | Place 6 markers on a track: 1 lookup of Current initiative per Creature, then a sort of at most 6 | **needs a component** | An initiative track with six Creature markers. Nothing is lost. |
| Current initiative is Base plus buffs less debuffs, floored at zero | `Creature.cs:111-113` (ADR 0036, ADR 0035's order) | 1 addition and 1 subtraction per Creature carrying either, per Round | **needs a component** | The same track, read with the Condition tokens beside it. |
| A tie between the sides is rolled off on a d20 | Two slots tie on the same band and the same Current initiative, whoever holds them (`Rounds/ActivationSlot.cs:12-16`, `Rules/Planning/TieOrderRules.cs:106-122`); in a tie that holds both sides, every tied Creature rolls a d20 and the highest takes the first Place (`TimelineBuilder.cs:57-83`, ADR 0063), on the Match's own random source, so a seeded Match replays its rolls. Every roll a Creature made travels with the timeline, in the order it made them (`CombatTimeline.RollOffs`, `TimelineBuilder.cs:45-48`), public like the order (`PlayerBoardStateProjection.cs:41`), and the table page's initiative strip prints them beside each slot (`rollText`, `table/timeline.js:54-57`), so the app shows what a table rolls | 1 roll and 1 comparison per tied Creature, 2 to 6 of them, then the markers set in the order rolled; the rolls are held until the tie is placed. With the two d20s [components.md](components.md) puts in the box, a tie of three or more is rolled in turns | **needs a component** | A d20, which the box already holds for the critical roll (components.md §1.6). Nothing is lost, but a Round that needed no die here can now need six and a re-roll. How many ties between the sides a Round produces at the table's cap is the tabletop-mathematician's to measure: two sides that buy the same Tiers in the same Round tie on every Creature that bought them (ADR 0063). |
| Different sides on the same number roll again | Every Creature on a number that more than one side rolled rolls again, among themselves: each number's Creatures go back through `RollOff` (`TimelineBuilder.cs:76-80`), which rolls them again only when they hold both sides (`TimelineBuilder.cs:59-62`) | 1 more roll per Creature on that number, repeated until no number is shared across the sides | **restate** | Nothing, but "your own Creature re-rolls too when it shares the number with an enemy" is the sentence players will miss. |
| A tie held by one side alone rolls nothing | `RollOff` returns such a tie as it stands (`TimelineBuilder.cs:59-62`); its owner orders it in `TieOrder` (1.6) | 0 dice | **keep as is** | Nothing. |
| The seat and the Creature number only order the draws | Tied slots are sorted by Player slot, then Creature id, before rolling, and that order decides nothing but which draw each takes (`TimelineBuilder.cs:30-36`) | 0: the table throws the dice in any order, and the order matters only to a seeded replay | **keep as is** | Nothing. The printed 1 to 3 the first audit put on each creature board for the tiebreak is no longer a timeline component. |

### 1.6 `TieOrder` (Planning)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| A Player orders their own Creatures among the Places their side holds in one tie | `TieOrderRules.TiesOf` finds every tie in which the Player holds two Places or more, `ValidateOrder` asks for each of those Creatures once and nothing else, and `Apply` moves them among their own Places and never the other side's (`TieOrderRules.cs:19-48,66-81`, `Rounds/Round.cs:171-185`); `Match.SubmitTieOrder` takes it (`Match.cs:207-233`, ADR 0063). This includes a tie held by one side alone | 1 decision: swap your own markers among your own Places. With 3 Creatures a Team, a Player holds at most one such tie a Round, of 2 or 3 Creatures, so 2 or 6 orders to choose from; 0 arithmetic | **restate** | Nothing. One step more on a Round with a tie, and one rule: you move only your own markers. |
| Tie orders are hidden until both are in | The Round keeps both Players' orders, and a Player reads only their own (`Round.cs:148-150`); the reordered timeline is public once both are applied (`TiesOrdered`, `Match.cs:432-442`) | When both Players have a tie to order, each commits face down and both reveal together, the way Speed is chosen. When only one does, nobody is waiting on the other, so the chits turn as soon as they are laid | **needs a component** | Something to commit an order face down, on the Rounds where both Players hold a tie; the smallest is three ordinal chits a Player. It is a third hidden decision, beside Speed and Intent, and the rulebook counts it as one (rulebook.md Part 1, Part 4 and §5.5, with the chits in its setup). |
| A Round where no Player holds two Places in one tie skips the step | `TieOrderRules.Evaluate` opens the gate with nobody waiting (`TieOrderRules.cs:53-60`, `Match.cs:389`) | 0 | **restate** | Nothing. The rulebook says the step is skipped when nobody holds two Places in a tie; how often it is not skipped is the tabletop-mathematician's measurement, taken with the one on the Roll-off. |

### 1.7 `IntentSelection` (Combat)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| One hidden Intent per Creature on the timeline | Intents are stored per Player and never shown to the other (`Matches/Rounds/Round.cs:20,190`) | 6 cards played face down | **keep as is** | Nothing. This is the mechanic that translates best. |
| An Intent must name a known, affordable Spell | `IntentRules.CanAct`: alive, unstunned, knows it, `actor.Energy >= spell.Stats.Cost` (`Rules/Combat/IntentRules.cs:50-69`) | 1 lookup of the Energy track, 1 comparison, per Creature | **restate** | Nothing. Energy tracks are public, so affordability is checkable without revealing the Intent; the rulebook must say so once. |
| The declaration is not a reservation | The cost is checked again at Resolution (`Rules/Combat/ResolutionRules.cs:37-41`) and spent only then (`Rules/Combat/CombatExecution.cs:28`) | 1 re-check per cast, later in the Round | **restate** | Nothing, but two Creatures can both declare a Spell only one of them can afford after an Energy drain lands. |

### 1.8 `RevealAndTarget` (Combat)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| Reveal in timeline order, bind targets at reveal | The reveal cursor walks the timeline; targets are chosen after seeing what came before (`Rules/Combat/ActionRules.cs:16-52`) | 1 card flip and 1 to 3 target markers per Activation slot; 6 slots a Round | **needs a component** | Target markers, one set per Player. Nothing is lost; this is the other mechanic that translates for free. |
| Targeting spec: origin, scope, count | Origin `Self`, `Ally` or `Enemy`; scope single or multi; at most `maxTargets` (`Rules/Combat/TargetingRules.cs:40-50`) | 1 lookup on the card, then a count | **restate** | Nothing. In `data/`: 24 Enemy, 9 Ally, 3 Self; 25 single-target, 11 multi (9 at 3, 2 at 2). |
| A Multi Spell may take fewer targets | `LegalTargets` returns a minimum of 1 (`TargetingRules.cs:49`) | 1 decision per multi-target cast | **restate** | Nothing, but it is a real choice — hitting one enemy with `meteor` is legal — and nothing on the card says so today. |
| Ally includes the caster | `creature.Owner == actor.Owner`, the actor included (`TargetingRules.cs:43`) | 0 | **restate** | Nothing. A Creature can `guard` itself; the card does not say it. |
| No duplicate targets | `targets.Distinct().Count() != targets.Count` (`TargetingRules.cs:68`) | 0, physically impossible with one marker per target | **keep as is** | Nothing: the components enforce it. |
| A Spell with no legal target is revealed with no targets | `ActionRules.cs:40-42`; it Fizzles later | 1 card flip, no markers | **restate** | Nothing. The timeline always moves on, which is what keeps the track simple. |
| Dead and wrong-origin targets are refused here | Per-target failures block the binding (`TargetingRules.cs:79-102`) | 1 lookup per target | **keep as is** | Nothing. |

### 1.9 `ActionResolution` (Combat)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| Fizzle | A dead or stunned actor, one that no longer knows or can afford the Spell, a global targeting failure, or no target left (`ResolutionRules.cs:37-55`; ADR 0038 for the word) | 1 to 4 checks per cast, before anything moves | **restate** | Nothing. The rulebook owes one clear paragraph; it is the rule most likely to be played wrong. |
| A Fizzle costs nothing | `CombatResolution.Fizzle` spends no Energy and applies no outcome (`Rules/Combat/CombatResolution.cs:49-54`, `CombatExecution.cs:22-25`) | 0 | **keep as is** | Nothing. |
| One critical roll a cast | `random.NextDouble() < CriticalChanceOf(actor, spell, speed)`, the Creature's chance plus the Spell's (`ResolutionRules.cs:59,80-86`) | 1 die roll and 1 lookup, on a `Standard` cast of one of the 21 Spells that print a chance; the Creature's own chance is 0 since ADR 0042, so the 15 Spells at zero never roll | **needs a component** | A die, settled by fork B. At most six rolls a Round and often fewer. See Part 3 for what the snap has to cover. |
| A `Quick` Creature rolls no critical | `CriticalChanceOf` is 0 for `Quick`, whatever the two chances add up to (`ResolutionRules.cs:85`, #160; game-rules.md, `Speed`). The engine still draws, so a seeded Match reads the same stream whatever the Speed (`ResolutionRules.cs:57-59`) | 1 look at the Speed card already face up on the creature board; 0 rolls | **restate** | Nothing, and it saves a roll. But it is half of the Speed trade, made at `Speed` and paid here, so it must be printed where the Speed is chosen — on the `Quick` card or the player aid — or `Quick` reads as free. |
| A critical multiplies Damage and a direct Heal, floored | `Multiplied(amount, multiplier)` on `Damage` and `Heal` only (`ResolutionRules.cs:91-92`, ADR 0033) | 1 multiplication per affected Outcome, at a multiplier of 2.0 | **restate** | Nothing. At 2.0 it is a doubling, which is the cheapest arithmetic there is. |
| The critical applies *before* Defense | `Math.Max(0, Multiplied(damage.Amount, multiplier) - target.TotalDefense.Value)` (`ResolutionRules.cs:91`) | 1 ordering rule held in the head | **restate** | Nothing, but getting it backwards changes the result, so it must be printed on the player aid. |
| A critical reaches nothing else | Not a lasting Effect, not a Caster effect, not Energy (`ResolutionRules.cs:68,93-95`, ADR 0033, ADR 0031, ADR 0035) | 0, once the boundary is taught as one sentence | **restate** | Nothing. "What the cast puts on a target's Health now" is the whole rule. |
| Damage minus total Defense, floor zero | `ResolutionRules.cs:91` | 1 subtraction and 1 floor per target, on numbers up to 20 | **restate** | Nothing. |
| Total Defense is base plus buffs less debuffs, floored at zero | `Creature.cs:95-97` (ADR 0035) | 1 sum over the Condition tokens per target, per cast | **needs a component** | A Defense track holding the running total, so the sum is done once when a Condition lands and not once per cast. |
| The energy cost is spent | `actor.SpendEnergy(resolution.EnergySpent)` (`CombatExecution.cs:28`) | 1 token move | **keep as is** | Nothing. |
| A Heal is capped by Health missing | `Creature.cs:271` | 1 comparison | **keep as is** | Nothing. |
| An Energy drain takes at most what the target has | `Creature.cs:295-307` (ADR 0035) | 1 comparison | **keep as is** | Nothing. |
| Caster effects resolve once per cast, unmultiplied, never on a Fizzle | `ResolutionRules.cs:68` (ADR 0031) | 1 to 2 operations on the caster's own board | **restate** | Nothing. Seven Spells in `data/` carry one; the card face must show it as a separate line or it will be read as a target effect. |
| Damage is capped by the Health left, and an Outcome that changed nothing is dropped | `CombatExecution.cs:53-71` | 1 comparison | **keep as is** | Nothing. |
| A lasting Effect attaches as a Condition per its Stacking policy | `Creature.Apply` through `ConditionSet.Apply` (`Creatures/ConditionSet.cs:26-43`) | 1 token placed with an amount and a Duration | **needs a component** | Condition tokens, in eight kinds, with a Duration dial. Nothing is lost. |
| `Refresh` restarts the existing Condition and keeps its amount | Only a Stun refreshes since ADR 0041: `ConditionSet.cs:36-40` matches by effect **type**, and `Condition.Refresh` restarts the *existing* Effect's Duration (`Creatures/Condition.cs:46-51`); pinned by `tests/DownfallArena.Domain.Tests/Matches/Creatures/ConditionTests.cs:82-98` | 1 dial reset on the Stun token already there, and no second token | **restate** | Nothing. "Conditions add up, a Stun restarts" is one sentence and it is the whole rule since ADR 0041. See ADR candidate 1. |
| `Stack` adds another Condition | `ConditionSet.cs:29`, the default of every lasting Effect but Stun (ADR 0041) | 1 more token per application | **needs a component** | Enough tokens; how many is unbounded today, and since ADR 0041 a second Bleed is a second token rather than a lost amount. See ADR candidate 3. |

### 1.10 `Cleanup` (End of round)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| Every Condition counts one Round down and expires at zero | `UpkeepRules.Cleanup` calls `Creature.TickConditions` (`UpkeepRules.cs:173-188`, `ConditionSet.cs:48-58`) | 1 dial turn per token on the board | **needs a component** | A Duration dial on the Condition token. Nothing is lost. |
| The first countdown after an application does not count | A `_fresh` flag skips the first tick (`Condition.cs:12,53-59`) | 0, **if** the rule is restated: every Condition in `data/` is applied in Combat, after that Round's ticks, so `durationRounds: N` is exactly "N of the following Rounds" | **restate** | Nothing today. The mechanism is not the rule; see ADR candidate 5 for whether the engine should say it that way. |
| A permanent Condition never counts down | `RemainingRounds` is null (`Condition.cs:34-36`, `Duration.cs:20-22`) | 0 | **keep as is** | Nothing, but it is what makes ADR candidate 3 unbounded. |
| A refresh also resets the free tick | `Condition.Refresh` sets `_fresh = true` (`Condition.cs:50`) | 0 | **restate** | Nothing, once the rule reads "N of the following Rounds" from the refresh as well. |

### 1.11 `Finalization` (End of round)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| A defeated Team loses at the end of a Round | `WinCondition.Evaluate` (`Rules/WinCondition.cs:17-19`, ADR 0011) | 1 lookup per Team | **keep as is** | Nothing. |
| Both Teams defeated in the same Round is a draw | `WinCondition.cs:31-38` | 1 comparison | **keep as is** | Nothing. A Caster effect can kill its own caster (ADR 0031), so this is reachable, not theoretical. |
| The Round cap ends the Match on total remaining Health | `completedRound >= rules.RoundCap` (`WinCondition.cs:23-26`) | 2 sums of 3 numbers, once, at the end | **keep as is** | Nothing. The cap's *value* is a `RuleSet` parameter, so a shorter table Match costs no fidelity; the number is the maintainer's, set at 8 to 16 Rounds for a 15 to 30 minute Match ([plan.md](plan.md)), not a rule change. |
| Equal Health at the cap is a draw | `WinCondition.cs:41-48` | 1 comparison | **keep as is** | Nothing. |

---

## Part 2. The twelve effect kinds

The closed taxonomy of ADR 0012, extended by ADR 0019, ADR 0020, ADR 0035 and ADR 0036. Counts and value
ranges are computed from `data/Spells/**` with a Python pass over the 36 files, counting a Spell once per
kind whether the Effect sits in `effects` or in `casterEffects`. The counts are therefore Spells and not
Effects: the 36 files author 57 Effects in all, and a Spell carrying two `DefenseBuff`s counts once. No file
in `data/Spells/**` authors a `stacking` key, so every Condition uses its family default, and since ADR 0041
that default is `Stack` for every lasting kind except `Stun`, which keeps `Refresh`
(`src/DownfallArena.Infrastructure/Resources/GameSchemaMapper.cs:185-220` and the `Of` factories in
`src/DownfallArena.Domain/Resources/Effects/*.cs`).

### Instant effects

| Effect kind | Spells in `data/` | Values used | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- | --- | --- |
| `Damage` | 23 (22 on targets, 2 on the caster; `hateful_sacrifice` and `summon_minions` are the caster ones) | amounts 2, 3, 4, 5, 6, 7, 10 | Multiplied by the critical, then reduced by total Defense, floor zero (`ResolutionRules.cs:91`) | 3 operations per target: double or not, subtract Defense, subtract from Health | **restate** | Nothing. This is the arithmetic the plan flagged, and at a multiplier of 2.0 it is the cheapest shape it can have. |
| `Heal` | 7 (5 on targets, 2 on the caster: `parasite_jab`, `soul_devourer`) | amounts 2, 3, 4, 7 | Multiplied by the critical (ADR 0033), capped by Health missing (`Creature.cs:271`) | 2 operations per target | **keep as is** | Nothing. |
| `EnergyGain` | 2 (`wait`, `restorative_burst`) | amount 2 | Added, never clamped (`Creature.cs:279-290`) | 1 token move | **keep as is** | Nothing at the cast; the unbounded track is ADR candidate 2. |
| `EnergyDrain` | 1 (`soul_devourer`) | amount 2 | Takes at most what the target has (`Creature.cs:295-307`, ADR 0035) | 1 comparison, 1 token move | **keep as is** | Nothing. |

### Lasting effects (they become Conditions)

| Effect kind | Spells in `data/` | Values used | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- | --- | --- |
| `Bleed` | 6 (4 on targets: `mortal_wound`, `poison_slash`, `summon_minions`, `toxic_waves`; 2 on the caster: `crazed_specter`, `revenant_guards`) | 1, 2, 3 or 4 a Round for 1, 2 or 3 Rounds | Damage at the start of each of the Creature's Rounds, ignoring Defense (`UpkeepRules.cs:62-70`); `Stack` (ADR 0041) | 1 token with an amount and a dial per application; 1 sum over the tokens and 1 subtraction a Round | **needs a component** | A Bleed token that shows both numbers, and enough of them: since ADR 0041 a second Bleed is a second token, so one Creature can carry several. |
| `Regeneration` | 1 (`healing_screech`) | 3 a Round for 2 Rounds | Heals before the Bleeds (`UpkeepRules.cs:52-60`, ADR 0019); `Stack` (ADR 0041) | 1 token per application, 1 addition a Round | **needs a component** | A Regeneration token. Nothing is lost. |
| `EnergyRegeneration` | 1 (`momentum`) | 2 a Round for 3 Rounds | Gives Energy before the heals (`UpkeepRules.cs:42-50`, ADR 0020); `Stack` (ADR 0041) | 1 token per application, 1 addition a Round | **needs a component** | An Energy regeneration token. Nothing is lost. |
| `Stun` | 2 (`crushing_stomp`, `tranquilizer_dart`) | 2 Rounds, both | The Creature takes no Speed choice, no Activation slot and no Intent (`SpeedRules.cs:34`); `Refresh`, the one kind ADR 0041 left refreshing | 1 token; the creature board takes no Speed card for 2 Rounds | **needs a component** | A Stun token. Nothing is lost, but a 2-Round Stun removes a third of a Team for two full Rounds and the rulebook must say it plainly. |
| `DefenseBuff` | 4 (`full_plate`, `guard`, `revenant_guards`, `thundering_seal`) | amounts 1, 2, 3; Durations 1 Round, 2 Rounds, **permanent** | Added into total Defense (`Creature.cs:95-96`); `Stack`, so every application adds a token | 1 token and 1 addition on the Defense track per application | **needs a component** | A Defense track. All four carry a permanent Defense buff — three of them beside a timed one — and it stacks without a bound: ADR candidate 3. |
| `DefenseDebuff` | 3 (2 on targets: `infectious_blast`, `noxious_cure`; 1 on the caster: `psycho_rush`) | amount 2; Durations 1 Round and **permanent** | Subtracted from total Defense, floored at zero (`Creature.cs:95-97`, ADR 0035); `Stack` | 1 token and 1 subtraction | **needs a component** | The same track. Bounded below by the floor, so it does not run away the way the buff does. |
| `InitiativeBuff` | 1 (`death_squad`) | amount 2 for 1 Round | Added into Current initiative before the debuffs (`Creature.cs:111-113`, ADR 0036); `Stack` | 1 token and 1 marker move, read once when the timeline is built | **needs a component** | An Initiative track. Nothing is lost. |
| `InitiativeDebuff` | 2 (`ice_spear`, `protective_slam`) | amount 2 for 1 or 2 Rounds | Subtracted, floored at zero (`Creature.cs:111-113`); `Stack` | 1 token and 1 marker move | **needs a component** | The same track. |

Two readings the counts make plain. First, the taxonomy is used unevenly: `Damage` is in 23 of 36 Spells and
seven kinds are in one or two. Second, the authored values are already small and repetitive — every
`DefenseDebuff`, `InitiativeBuff` and `InitiativeDebuff` in the catalogue has an amount of exactly 2, and
Durations are only ever 1, 2, 3 or permanent. A token set is therefore small, which is good news for phase 3.

---

## Part 3. The Spell catalogue

All 36 files under `data/Spells/**`, read from `data/` and not from [spells.md](../domain/spells.md).

**Tracking cost** is counted for one cast at the Spell's maximum target count. *Ops* counts: the critical
roll (1 when the Spell prints a Critical chance, 0 for the fifteen that print zero, because the Creature's
own chance is 0 since ADR 0042), paying the energy cost (1 when the cost is above zero), then per target 3
for a `Damage`, 2 for a `Heal`, 1 for an `EnergyGain`, `EnergyDrain` or a
lasting Effect, plus the Caster effects at 2 for a self-`Damage` and 1 for anything else. *Tokens* counts
Condition tokens placed. *Targets* is `maxTargets`. *Tier* is the level of the Tier in `data/Tiers` that sells
the Spell, 0 for the starting kit ([ADR 0058](../adr/0058-a-tier-is-the-package-the-balance-objective-reads.md)).
The first audit read ADR 0034's depth in the Talent tree instead; the two agree on all 36 Spells, so no row's
number moved when the definition did.

**Critical chances the die has to cover.** The Creature's own chance is 0 (ADR 0042), so a Spell's printed
bonus *is* the chance rolled and the fifteen Spells at zero never roll at all. Twelve distinct bonuses are
authored: 0 (15 Spells), 0.22, 0.28, 0.283, 0.33 (4 Spells), 0.38, 0.45 (2 Spells), 0.5 (7 Spells), 0.617,
0.75, 0.767, 0.8 — so eleven distinct chances are ever rolled. Four of the eleven sit on a 1-in-20 grid
(0.45, 0.5, 0.75, 0.8) and one on a 1-in-6 grid (0.5); three of them (0.283, 0.617, 0.767) are not even
multiples of 0.05. `data/balance/knobs.json` declares `/criticalChance` a knob on 21 Spells with a step of
0.05 (one of them `throwing_star`, which prints 0), so a d20 snap is inside the declared search space and a d6
snap is not. The die is settled since, a d20 ([d20-criticals.md](d20-criticals.md), not built); the error each
candidate die costs is measured in components.md §1.6, and the per-Spell snapped values are the maintainer's.

**Card text.** The statline every card must carry — cost, targets, effects with amounts and Durations,
caster effects, critical chance — was generated for all 36 and measured. The measurement also carried an
unlock initiative, which no Spell has since ADR 0059: that number is on the Tier card now (1.3), so at
`938bef5e` every Spell statline is shorter than measured. The Spells that moved since, `throwing_star`'s
second target among them, are listed at the top of this document and re-measured with components.md §2.3.
`revenant_guards` is the longest and no Spell overflows a card on its statline alone. The figures this row
once printed are not repeated: they came from a rendering whose join was never defined, so they could not be
reproduced. [components.md](components.md) §2.3 defines both strings, measures them with a command that prints what it
measures, and is the number to quote. The flag below marks the seven whose statline is over 110 characters
*and* which need a second sentence the rulebook cannot carry for them (a Caster effect line, or two
Conditions of the same kind on one target): `revenant_guards`, `crazed_specter`, `psycho_rush`,
`summon_minions`, `soul_devourer`, `thundering_seal`, `guard`. Those counts were measured on the previous
content; the only two statlines the current one moves are `mortal_wound` and `meteor`, one character longer
each (a Critical chance of 0.5 became 0.45), and the flagged seven are unchanged, `revenant_guards` included.
ADR 0042 can only shorten a card: the fifteen Spells at zero need print no Critical chance line at all.
Whether that fits is the component-designer's measurement in phase 3, against a real card size and a real
type size; this document only reports the character counts and which rows carry an extra rule.

### Trivially playable — 5 operations or fewer and at most 2 tokens: 18 Spells

| Spell | Tier | Targets | Ops | Tokens | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- | --- | --- |
| `wait` | 0 | self | 1 | 0 | **keep as is** | Nothing. |
| `momentum` | 2 | self | 1 | 1 | **needs a component** | An Energy regeneration token. |
| `full_plate` | 2 | self | 2 | 1 | **needs a component** | A permanent Defense token; it is ADR candidate 3's worst case. |
| `rejuvenate` | 1 | 1 ally | 4 | 0 | **keep as is** | Nothing. |
| `restorative_gush` | 3 | 1 ally | 4 | 0 | **keep as is** | Nothing. |
| `guard` | 1 | 1 ally | 3 | 2 | **needs a component** | Two Defense tokens from one cast, one permanent and one timed. Flagged for card text. |
| `thundering_seal` | 3 | 1 ally | 3 | 2 | **needs a component** | The same, at amount 3. Flagged for card text. |
| `basic_attack` | 0 | 1 enemy | 4 | 0 | **keep as is** | Nothing. |
| `heavy_strike` | 0 | 1 enemy | 4 | 0 | **keep as is** | Nothing. |
| `throwing_star` | 1 | 1 enemy | 4 | 0 | **keep as is** | Nothing. |
| `pummel` | 1 | 1 enemy | 5 | 0 | **keep as is** | Nothing; its 0.767 is a snap job, not a rule. |
| `lightning_bolt` | 1 | 1 enemy | 5 | 0 | **keep as is** | Nothing; 0.617 likewise. |
| `enraged_charge` | 2 | 1 enemy | 5 | 0 | **keep as is** | Nothing. |
| `engulfing_flames` | 3 | 1 enemy | 5 | 0 | **keep as is** | Nothing. |
| `restorative_burst` | 3 | 1 ally | 4 | 0 | **keep as is** | Nothing. |
| `healing_screech` | 2 | 1 ally | 5 | 1 | **needs a component** | A Regeneration token. |
| `poison_slash` | 1 | 1 enemy | 5 | 1 | **needs a component** | A Bleed token. Since ADR 0041 a second Bleed is a second token, not a lost amount. |
| `tranquilizer_dart` | 3 | 1 enemy | 5 | 1 | **needs a component** | A Stun token; two Rounds lost. |

### A component, or a second reading — 6 or 7 operations, or 3 tokens: 11 Spells

| Spell | Tier | Targets | Ops | Tokens | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- | --- | --- |
| `death_squad` | 3 | 3 allies | 4 | 3 | **needs a component** | Three Initiative buff tokens, read once when the timeline is built. |
| `hateful_sacrifice` | 3 | 1 enemy | 7 | 0 | **restate** | Nothing; 10 damage and 4 back on the caster, which can kill it. Two Health tracks move. |
| `protective_slam` | 2 | 1 enemy | 6 | 1 | **needs a component** | An Initiative debuff token. |
| `ice_spear` | 3 | 1 enemy | 6 | 1 | **needs a component** | The same. |
| `mortal_wound` | 3 | 1 enemy | 6 | 1 | **needs a component** | A Bleed token at 4 a Round, the largest in the catalogue. |
| `crushing_stomp` | 3 | 1 enemy | 6 | 1 | **needs a component** | A Stun token, on top of 7 damage at cost 4, the only cost-4 Spell. |
| `psycho_rush` | 3 | 1 enemy | 6 | 1 | **needs a component** | A Defense debuff on its own caster. Flagged for card text. |
| `parasite_jab` | 2 | 1 enemy | 6 | 0 | **restate** | Nothing; the caster Heal needs its own line on the card. |
| `infectious_blast` | 3 | 3 enemies | 4 | 3 | **needs a component** | Three permanent Defense debuff tokens from one cast at cost 1. |
| `summon_minions` | 2 | 3 enemies | 6 | 3 | **needs a component** | Three Bleed tokens and 2 self-damage; the only Spell with no immediate effect on a target. Flagged for card text. |
| `soul_devourer` | 3 | 1 enemy | 6 | 0 | **restate** | Nothing, but it is three economies in one cast: Health, Energy and the caster's Health. Flagged for card text. |

### Expensive — 8 operations or more: 7 Spells

| Spell | Tier | Targets | Ops | Tokens | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- | --- | --- |
| `chain_slash` | 3 | 2 enemies | 8 | 0 | **restate** | Nothing; 2 targets, 3 operations each. |
| `revenant_guards` | 3 | 3 allies | 9 | 7 | **needs a component** | Six Defense tokens on the Team plus a Bleed on the caster, from one cast at cost 2. The heaviest cast in the catalogue and the longest card text (components.md §2.3). Flagged. |
| `meteor` | 2 | 3 enemies | 11 | 0 | **restate** | Nothing; 9 operations of damage arithmetic in one Activation slot. |
| `tornado` | 3 | 3 enemies | 11 | 0 | **restate** | The same. |
| `noxious_cure` | 2 | 3 allies | 11 | 3 | **needs a component** | Three Heals and three Defense debuff tokens; the cure shreds the cured. |
| `crazed_specter` | 3 | 3 enemies | 12 | 1 | **needs a component** | 9 operations of damage plus a Bleed on its own caster. Flagged for card text. |
| `toxic_waves` | 3 | 3 enemies | 14 | 3 | **needs a component** | The most expensive cast in the game: 9 operations of damage and three Bleed tokens placed. |

---

## Part 4. ADR candidates

Each is a question, with alternatives and what each one costs. None is decided here. Fork C is settled, so
each is framed as a change to the engine and its tests, never as a table-only exception.

### Candidate 1. A refreshing Condition discards the new amount

> **Settled (2026-09-14) by [ADR 0041](../adr/0041-a-condition-stacks-unless-it-is-a-stun.md).** The
> per-round family stacks; `Stun` keeps `Refresh`, because its only payload is a duration — a second stun can
> only take a round the target has already lost. One application, one token. Measured there on its own,
> decomposed from the crit change that shipped beside it: the objective reads 85.76 against a baseline of
> 85.68 on content `91da955c`, with `player1WinShare`, `averageRounds` and `fizzleRateA` identical to three
> decimals. It is fixed because it was wrong, not because it bought anything.

**What the table showed.** Before ADR 0041, `ConditionSet.Apply` found an existing Condition by effect
*type* only (`ConditionSet.cs:28`), and `Refresh` restarted the *existing* Effect's Duration and kept its
amount (`Condition.cs:46-51`). Six Spells in `data/` carry a Bleed and they shared one slot per Creature. So
`mortal_wound`'s Bleed 4 for 2 Rounds, cast on a Creature already carrying `toxic_waves`' Bleed 1 for 1
Round, left it bleeding **1** a Round — and credited that 1 to `mortal_wound` (ADR 0027). A player at a table
would have placed the new token and been wrong. Today they place it and are right: two Bleeds are two tokens,
5 a Round while both run, and the rule that has to be taught instead is that the tokens are summed before the
one subtraction. What is still pinned is the `Refresh` path itself, now reachable only through `Stun`
(`ConditionTests.cs:82-98`).

**The question.** When a `Refresh` Effect lands on a Creature that already carries one of its kind, which
amount and which Duration survive?

- *Keep the existing amount and Duration* (before ADR 0041). Costs: a stronger Bleed is silently wasted;
  unteachable.
- *Take the new amount and the new Duration.* Costs: a domain change plus a benchmark digest move; a weak
  Bleed can now overwrite a strong one, which is the same trap in the other direction.
- *Take the larger amount and the longer Duration, each independently.* Costs: a domain change, a digest
  move, and a Condition whose amount and Duration come from different casts, which ADR 0027's source
  attribution then has to answer for.
- *Change the family default from `Refresh` to `Stack`.* **Taken, by ADR 0041**, `Stun` excepted. Costs:
  unbounded Bleed tokens, and the table pays in components what it saves in surprise.

### Candidate 2. Energy has no maximum

> **Settled (2026-09-14) by the maintainer: the engine does not change.** A track that ends is a component
> problem, not a rule problem — a die, or tokens stacked in a space on the board. The number is settled too,
> and not by a measurement: [components.md](components.md) §1.7 sizes the track 0 to 32 (2 Energy a Round for
> at most 16 Rounds) and gives each Creature an overflow chit for the three Spells that can pass it.

**What the table shows.** `Energy` is a `NonNegativeStat` with no ceiling (`Energy.cs:3`) and `GainEnergy`
never clamps (`Creature.cs:279-290`). A Creature gains 2 a Round from the Rule set, 2 more from `wait`, and
2 more a Round from `momentum` for 3 Rounds. Nothing spends what it does not need. Over a 30-Round Match a
single Creature can bank well over a hundred Energy. A physical track ends at some number.

**The question.** Should a Creature's Energy be capped, and by what?

- *No cap* (today). Costs: the track has no end, so the component is a pile of tokens rather than a dial,
  and a hoarding line is unbounded.
- *A cap in the `Rule set`.* Costs: a new Rule set value, so every recorded run's stamp changes; the
  observation vector's unbounded Energy feature becomes bounded, which ADR 0020 already called a reason to
  normalise. It makes `wait` and `momentum` worth measurably less, which the four readings can price.
- *A cap on the Creature definition, like Health.* Costs: the same, plus a content change and a new content
  hash; it lets Creature kinds differ in how much they can bank, which nothing else in the model does yet.

### Candidate 3. Permanent stat buffs stack without a bound

> **Open, and this row overstated its case.** The maintainer holds that the line is balanced and that
> `full_plate` is not available in Round 1. Since ADR 0066 the Tiers read here agree: `tier:brute:v1` requires
> nothing, `tier:ironbound:v1` requires `tier:brute:v1` and sells `full_plate`, and a Creature buys at most one
> Tier an opportunity, so Round 1 buys the one and Round 3, the next opportunity, the other. Under ADR 0056's
> sequential picks, before it, Round 1 bought both. What this row left out is the price: casting it every
> Round spends that Creature's activation every Round, so it never attacks. Whether the line is degenerate is
> therefore still a measurement, not the proof this row claimed — and phase 2 having been dropped as a
> measurement exercise, who makes it is open. What is not in doubt: nothing bounds the total, so the table
> needs an unbounded supply of Defense tokens until something does.
>
> Re-read at content `4d7a841c`: the route runs through two Tiers now rather than the Talent tree, and since
> ADR 0066 it takes two opportunities, Rounds 1 and 3; `full_plate`'s cost and amount are unchanged, and so
> are the other three Defense buff Spells' amounts, so the reading below stands two Rounds later. ADR 0041
> does not touch it — `DefenseBuff` already defaulted to `Stack`.

**What the table shows.** `DefenseBuff` defaults to `Stack` (`Resources/Effects/DefenseBuff.cs:13`), a
permanent Duration never counts down (`Condition.cs:34-36`), and nothing caps total Defense above
(`Creature.cs:95-97`). `full_plate` is Self-targeted, costs 1, gives +3 permanent, and is castable from
Round 3: an Evolution pick buys `tier:brute:v1` in Round 1 and another `tier:ironbound:v1` in Round 3, and
the Creature gains 2 Energy every Round. Cast every Round from Round 3, its Defense is 3(k - 2) after Round k.
The largest single hit in the catalogue is 10 (`psycho_rush`, `engulfing_flames`, `hateful_sacrifice`);
doubled by a critical that is 20. From Round 9 the Creature takes zero from every attack in the game except a
Bleed, which ignores Defense. `thundering_seal`
does the same for an ally at +3, `guard` at +1 and `revenant_guards` for the whole Team at +2 a cast: all
four Defense buff Spells carry a permanent half. The plan
already calls this "probably not what anyone wants"; the table gives the round number.

**The question.** What bounds a permanent stat buff?

- *Nothing* (today). Costs: a degenerate line exists, and the component set needs an unbounded token supply.
- *A cap on total Defense, in the `Rule set`.* Costs: a new Rule set value and a stamp change; the cap is a
  number to measure, and buff Spells become worthless once it is reached.
- *Make the permanent halves `Ignore` instead of `Stack`.* Costs: a one-line default change per Effect, a
  digest move, and four Spells lose their re-cast value entirely — `full_plate` becomes a once-a-Match cast,
  which is closer to the `Passive` it is authored as.
- *Remove permanent Durations from the taxonomy and give those halves a long finite Duration.* Costs: a
  content change on the four Defense buff Spells — five, if `infectious_blast`'s permanent Defense debuff
  goes with them — and a new content hash; the arc of a Match loses its only permanent gain.

### Candidate 4. A Condition remembers the Spell that applied it

**What the table shows.** ADR 0027's source and its largest-remainder share change no Health, no Energy, no
death and no order — the ADR says so and the benchmark digest did not move. At a table the mechanic is
invisible: there is nothing to place, nothing to look up, nothing to compute. It exists for the balance
objective's `damagePerCast` and for `spellOutcomes`.

**The question.** Does a Condition still need to carry its source once the same rules are played on a board?

- *Keep it* (today). Costs: one field on `Condition` and on `ConditionSnapshot`, and the honesty of
  `spellOutcomes`. The table pays nothing. This is the cheapest answer and it is the status quo.
- *Drop it.* Costs: `damagePerCast` goes back to reading half a Condition Spell's output, which ADR 0027
  exists to fix, and `tune-content` scores become incomparable again. Nothing is gained at the table.
- *Keep it, and make it visible.* Costs: a Condition token would have to name the Spell that placed it —
  six Spells can place a Bleed alone — which is a component cost for a reading no player uses.

The evidence points one way, which is why this is raised as a question with a cheap answer rather than a
problem.

### Candidate 5. "The first countdown after an application does not count"

**What the table shows.** A `_fresh` flag skips the first tick (`Condition.cs:12,53-59`), and a refresh sets
it again (`Condition.cs:50`). Nothing in `data/` applies a Condition anywhere but `ActionResolution`, which
runs after that Round's `OngoingEffects`. So for every Condition the content can produce, the flag is exactly
equivalent to "the Condition lasts N of the following Rounds". A 2-Round Stun costs its target two whole
Rounds; a 1-Round Bleed ticks once. The table needs no flag, only the sentence.

**The question.** Should the engine express a Duration as the rule rather than as the mechanism?

- *Keep the flag* (today). Costs: nothing observable, but the domain states a mechanism where it means a
  rule, and any future Condition applied outside Combat — an Upkeep effect, a passive — would silently get
  an extra Round. The rulebook and the domain would then disagree without either being wrong.
- *Store the Round the Condition expires at, instead of a countdown.* Costs: a change to `Condition`,
  `ConditionSnapshot` and the Cleanup rule, with no behaviour change for any content that exists; the
  Condition needs to know the Round number, which it does not today.
- *Drop the flag and author every Duration one higher.* Costs: a content change on all 18 timed lasting
  Effects and a new content hash, to say the same thing with a worse number on the card.

### Candidate 6. Who picks first in an opportunity

**What the table shows.** The domain accepts the two Players' Evolution choices in any order and applies each
the moment it arrives (`Match.cs:116-153`), and a purchase is public at once: the other Player's board state
carries these Creatures' full snapshots, their Tiers and Base initiative included
(`PlayerBoardStateProjection.cs:19-20`). The order is the driver's. `MatchDriver.PlayAsync` asks Player 1,
then Player 2, for one pick each, every pass (`MatchDriver.cs:26-48`), and every host plays through it — the
table's seats are agents of the same driver (`Cli/Table/HumanSeat.cs`). So in every Match the engine has
played, Player 2 chooses a first pick knowing Player 1's first purchase, and Player 1 chooses a second knowing
Player 2's first. The rulebook copies that order (rulebook.md §5.3), which is the right thing for it to do
while the engine plays it. Two facts make it a question. It is the one place the seat still orders anything
since ADR 0063 took the tiebreak from it, and the seat is the asymmetry ADR 0062 and ADR 0063 exist to
remove. And the rule lives in an Application loop, not in the domain, so a host that took picks in another
order would play another game without breaking a test.

**The question.** Who picks first in an opportunity, and where is that rule enforced?

- *Player 1 first, alternating, in the driver* (today). Costs: nothing to build. A seat advantage nobody has
  measured — the tabletop-mathematician's reading, on the exploring run ADR 0062 moved the seat measurement
  to — and a rule one host away from being played differently.
- *The same order, enforced by the domain*: the Evolution gate takes a pick only from the Player whose turn it
  is. Costs: a domain change and its tests; the benchmark digest should not move, since the driver already
  plays this order, and the digest is what confirms it. The table and every host then play one rule, and
  the seat question stays open.
- *Alternate who starts, opportunity by opportunity.* Costs: a domain change, a first-player marker in the
  box, and a digest move. It spreads the seat's advantage or disadvantage evenly over a Match, and agents
  may learn to time purchases around it, which is what ADR 0063 said of alternating ties.
- *Hidden, simultaneous purchases, revealed together, like Speed.* Costs: a domain change (purchases held
  back from the other Player's board until both are in), a digest move, and a pick hidden behind a screen at
  the table. It removes the information the second picker has, which is what makes the seat matter here. A
  Player's own two picks no longer need a ruling: since ADR 0066 they go to two Creatures and cannot depend
  on each other. ADR 0066 rejected hiding those two for that reason, as a second hidden decision for no
  gain; it did not weigh what one Player's purchase tells the other, which is this candidate.

### Not raised, and why

- **The 30-Round cap.** It is a `RuleSet` parameter (`RuleSet.cs:30,42`), and the engine plays any value
  unchanged. A shorter table Match is the same rules with a different number, so it costs no fidelity and
  needs no ADR. The number is the maintainer's, set at 8 to 16 Rounds for a 15 to 30 minute Match
  ([plan.md](plan.md)).
- **Continuous critical chances.** Settled by fork B: a die, and the catalogue snapped to its grid. Part 3
  reports what the snap has to cover; the die and the per-Spell values are the maintainer's.
- **Team size, Energy per Round, the Evolution schedule, the critical multiplier.** The same as the Round
  cap: parameters, not rules. The schedule is three of them — picks per opportunity, the first opportunity
  Round, the interval (`RuleSet.cs:30`) — and a table that wanted an opportunity every Round would change a
  number, not the game's rules.
- **The Tiers' initiative bonuses.** Authored numbers and a balance knob (ADR 0061), read off a card at the
  table at the same cost whatever they are. Whether they are right is a balance question, not a translation
  one.

---

## Part 5. Coverage check

**Sub-phases.** All eleven appear — ADR 0010's ten and ADR 0063's `TieOrder` — each as exactly one
section, in the enum's order (`RoundSubPhase.cs:8-18`): `EnergyGain`, `OngoingEffects`, `Evolution`,
`Speed`, `TurnOrderResolution`, `TieOrder`, `IntentSelection`, `RevealAndTarget`, `ActionResolution`,
`Cleanup`, `Finalization`. 11 of 11. No mechanic is filed under two of them. Rows per section: 3, 6, 12, 3,
6, 3, 3, 7, 17, 4, 4.

**Effect kinds.** All twelve of the taxonomy appear, each as exactly one row in Part 2: `Damage`, `Heal`,
`EnergyGain`, `EnergyDrain`, `Bleed`, `Regeneration`, `EnergyRegeneration`, `Stun`, `DefenseBuff`,
`DefenseDebuff`, `InitiativeBuff`, `InitiativeDebuff`. 12 of 12, and every one has at least one Spell in
`data/` using it.

**Spells.** All 36 files under `data/Spells/**` appear, each as exactly one row in Part 3: 18 trivially
playable, 11 needing a component or a second reading, 7 expensive, at `938bef5e`. 18 + 11 + 7 = 36; at
`4d7a841c` it would be 17 + 12 + 7, `throwing_star` moving (see the top of this document). By Tier level:
3 at 0 (the starting kit), 6 at 1, 9 at 2, 18 at 3, which is what the 21 Tiers sell: 3 x 2, 9 x 1 and 9 x 2.

**Tiers.** All 21 files under `data/Tiers` are read by the Evolution rows of 1.3, which count them by level,
Spells sold, prerequisite and initiative bonus; no Tier needs a row of its own, because they differ only in
numbers the card prints.

**Verdicts.** 116 rows carry exactly one verdict each: 68 in Part 1, 12 in Part 2, 36 in Part 3. The totals,
counted over the file rather than recalled: **needs a component** 44, **keep as is** 36, **restate** 36,
**simplify (ADR)** 0. `cut from the tabletop rule set` is used zero times, as fork A requires. Before this
re-audit they were 105 rows, 42, 33 and 30. In Part 1, Evolution went from 8 rows to 12, the timeline from 3
to 6, `TieOrder` is 3 new rows, and `ActionResolution` gained the `Quick` critical row: 57 + 4 + 3 + 3 + 1 =
68. Its **needs a component** rows went from 15 to 17: it lost 4 (picks a Round, the Talent tree's
prerequisites, the Spell's unlock initiative, the printed tiebreak number) and gained 6 (the Round track, pick
tokens an opportunity, Tier cards, the Tier's bonus on the initiative track, the Roll-off die, the face-down
tie order). The two rows that once asked the engine to change still do not: ADR 0041 made the stacking one, and
the maintainer settled the Energy one the other way. This audit asks the engine for nothing, except what
Candidate 6 puts to the maintainer as a question.

| Verdict | Part 1 | Part 2 | Part 3 | Total |
| --- | --- | --- | --- | --- |
| keep as is | 22 | 3 | 11 | 36 |
| restate | 29 | 1 | 6 | 36 |
| needs a component | 17 | 8 | 19 | 44 |
| simplify (ADR) | 0 | 0 | 0 | 0 |
| cut from the tabletop rule set | 0 | 0 | 0 | 0 |
| **Total** | **68** | **12** | **36** | **116** |

**ADR candidates.** Six raised, four excluded with a reason.
