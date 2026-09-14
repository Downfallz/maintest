# Translation audit

Status: **Evidence** (2026-09-14). Phase 1 of [plan.md](plan.md).

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
`data/` on Spell initiative, energy cost, Critical chance bonus, effect amounts and Durations, and the
presence of a Caster effect, **30 of the 36 differ** and only six still match (`chain_slash`, `guard`,
`heavy_strike`, `ice_spear`, `mortal_wound`, `toxic_waves`). `basic_attack` deals 1 there and 2 here;
`engulfing_flames` 9 there and 10 here; `summon_minions` is an `EnergyGain 3` there and three Bleeds here.

Verdicts are exactly one of: **keep as is**, **restate**, **needs a component**, **simplify (ADR)**.

The numbers in the tracking columns are counts, not judgements. "Operations" means arithmetic or comparison
steps a player performs out loud; "tokens" means physical markers placed or moved; "lookups" means reading a
value off another component. Whether a count is too high is a playtest reading, not a claim made here.

## The board this audit assumes

`RuleSet.Default` (`src/DownfallArena.Domain/Matches/RuleSet.cs:20`): 3 Creatures a Team, 2 Energy a Round,
2 Evolution picks a Round, a 30-Round cap, a critical multiplier of 2.0. One Creature definition,
`data/Creatures/main.v1.json`: Health 20, Energy 0, Defense 0, Base initiative 5, Critical chance 0.05. One
enabled Talent tree, `data/TalentTrees/talent_tree.v1.json` (`core_classes.v1.json` carries
`"enabled": false`). So six identical Creatures start a Match, each knowing the same three Spells, each able
to become any of the nine sub-classes.

One Round therefore asks for: up to 4 Evolution choices, 6 Speed choices, 6 Intents, 6 target bindings and 6
Resolutions. Thirty of those is the cap the engine plays to today.

---

## Part 1. The ten sub-phases of ADR 0010

In the order of `src/DownfallArena.Domain/Matches/Rounds/RoundSubPhase.cs:8-17`. Each mechanic appears in
exactly one sub-phase, filed where it is enforced.

### 1.1 `EnergyGain` (Start of round)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| Energy gain per Round | Every living Creature gains `RuleSet.EnergyPerRound` (`Rules/Rounds/UpkeepRules.cs:13-22`) | 6 token moves, no arithmetic if the track is a dial; 0 lookups | **needs a component** | An Energy track on each creature board, six of them. Nothing is lost. |
| The dead gain nothing | `creatures.Where(creature => creature.IsAlive)` (`UpkeepRules.cs:18`) | 0, a dead creature board is turned over | **keep as is** | Nothing. |
| Energy has no maximum | `Energy` is a `NonNegativeStat` with no ceiling (`SharedKernel/Stats/Energy.cs:3`); `GainEnergy` never clamps (`Creatures/Creature.cs:147-160`) | A track must end somewhere. A Creature casting `wait` every Round nets +4 a Round and spends nothing: 120 Energy over 30 Rounds | **simplify (ADR)** | A cap changes what a hoarding line is worth; see ADR candidate 2. |

### 1.2 `OngoingEffects` (Start of round)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| The three passes, in order | Energy regeneration ticks, then Regeneration ticks, then Bleed ticks, each pass over every living Creature (`UpkeepRules.cs:35-73`, ADR 0019, ADR 0020) | 3 passes over 6 creature boards; 1 lookup per Condition token | **restate** | One sentence in the rulebook: energy, then healing, then bleeding. Nothing is lost. |
| Healing before Bleed is load-bearing | A Regeneration can carry a Creature through a Bleed that would have killed it (`UpkeepRules.cs:24-28`) | 1 comparison per Creature carrying both | **restate** | Nothing; the order has to be printed on the player aid or it will be got wrong. |
| A Bleed tick ignores Defense | `creature.TakeDamage(asked.Total)` with no Defense term (`UpkeepRules.cs:67`) | 1 subtraction, and the player must *not* read the Defense track | **restate** | Nothing. It is the only damage in the game that skips Defense, so it is the one players will get wrong. |
| A Regeneration tick is capped by Health missing | `Creature.Heal` clamps to `MaxHealth - Health` (`Creature.cs:139`) | 1 comparison | **keep as is** | Nothing. |
| An Energy regeneration tick is never wasted | Energy has no maximum, so nothing clamps (ADR 0020) | 1 addition | **keep as is** | Nothing, but it inherits the unbounded track of row 1.3 above. |
| Condition source and the tick shares | Every tick is split across the casts behind it by largest remainder (`UpkeepRules.cs:81-170`, ADR 0027) | **Zero.** The split changes no Health, no Energy, no death and no order — ADR 0027 says so and the benchmark digest did not move | **keep as is** | Nothing at the table: it is a reading for the learning pipeline, invisible on a board. See ADR candidate 4. |

### 1.3 `Evolution` (Planning)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| Two picks a Round, per Player, shared across the Team | `round.EvolutionChoicesOf(slot).Count >= rules.EvolutionPicksPerRound` (`Rules/Planning/EvolutionRules.cs:46`) | 2 tokens spent from a Player supply; the choice is which Creature gets them | **needs a component** | Two pick tokens a Player. Nothing is lost. |
| Prerequisites, `allOf` and `anyOf` | `TalentUnlocks.UnlockableSpells` gates on the node's and the Spell's prerequisites against what the Creature knows (`Rules/Planning/TalentUnlocks.cs:13-25,52-58`) | 1 to 3 lookups on the tree per candidate; 36 Spells, 3 branches of 2, 9 sub-classes of 3 | **needs a component** | A Talent tree mat, or prerequisites printed on each card. The tree is the Match's arc, so this is the largest layout job in phase 3. |
| Picks inside a Round are sequential, not simultaneous | The unlock is applied before the next choice is validated (`Matches/Match.cs:142`), so the second pick sees the first | A Creature can open a sub-class and take one of its Spells in the same Round: `pummel` then `full_plate` in Round 1 | **restate** | Nothing, but it changes how fast the tree opens and has to be said. |
| An unlock raises Base initiative by the Spell initiative, for the Match | `BaseInitiative = BaseInitiative.Plus(spell.Stats.SpellInitiative.Value)` (`Creature.cs:106`, ADR 0017) | 1 marker move on an initiative track, once, at the unlock; values in `data/` are 0, 1, 2 or 3 | **needs a component** | An initiative track per creature board. Nothing is lost; it is one move, not a per-cast cost. |
| A starting Spell grants no Spell initiative | The definition's `baseInitiative` is authored knowing the kit (ADR 0017) | 0 | **keep as is** | Nothing, but it is an asymmetry a player will ask about: two Creatures knowing the same Spells can differ in Initiative. |
| A refused unlock raises nothing | Dead Creature or Spell already known (`Creature.cs:92-104`) | 0 | **keep as is** | Nothing. |
| Evolution pass | A Player gives up their remaining picks (`Match.cs:154`) | 1 declaration | **restate** | Nothing. |
| The sub-phase ends when no Player has an *effective* pick left | Remaining picks are capped by what the Player's living Creatures can actually unlock (`EvolutionRules.cs:82-100`) | 1 count per Player, up to 3 tree lookups | **restate** | Nothing. A Player with nothing left to unlock does not have to pass; the rulebook must say the sub-phase just ends. |

### 1.4 `Speed` (Planning)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| One Speed choice per living, unstunned Creature | `SpeedRules.ValidateChoice` and `Evaluate` (`Rules/Planning/SpeedRules.cs:13-45`) | 6 tokens placed, one per creature board | **needs a component** | A two-sided Quick/Standard token per Creature. Nothing is lost. |
| Speed choices are hidden until the timeline is built | A Player's board state carries only their own choices (`Application/Matches/Projections/PlayerBoardStateProjection.cs:37`) | 6 tokens placed face down, then flipped together | **restate** | Nothing, and it is a genuine simultaneous decision the plan's inventory did not name. |
| A stunned Creature skips the whole Round | No Speed choice, so no Activation slot, so no Intent (`SpeedRules.cs:34`, and the timeline is built from the Speed choices, `Rules/Planning/TimelineBuilder.cs:25`) | 1 lookup on the Stun token; the creature board takes no speed token | **restate** | Nothing. This is what makes Stun the biggest effect in the game and it must be taught as "loses the Round", not "loses its attack". |

### 1.5 `TurnOrderResolution` (Planning, automatic)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| The Combat timeline | All Quick Activation slots, then all Standard, Current initiative descending inside each (`TimelineBuilder.cs:25-28`) | Place 6 markers on a track: 1 lookup of Current initiative per Creature, then a sort of at most 6 | **needs a component** | An initiative track with six Creature markers. Nothing is lost. |
| Current initiative is Base plus buffs less debuffs, floored at zero | `Creature.cs:74-76` (ADR 0036, ADR 0035's order) | 1 addition and 1 subtraction per Creature carrying either, per Round | **needs a component** | The same track, read with the Condition tokens beside it. |
| Ties break by Player slot, then Creature id | `.ThenBy(slot => slot.Owner).ThenBy(slot => slot.Creature.Value)` (`TimelineBuilder.cs:27-28`) | 0 arithmetic: Creature ids are 1 to 6, handed out in join order (`Match.cs:287-296`), so it reads as "the first Player's boards, left to right" | **needs a component** | A printed number, 1 to 3, on each creature board. Nothing is lost. |

### 1.6 `IntentSelection` (Combat)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| One hidden Intent per Creature on the timeline | Intents are stored per Player and never shown to the other (`Matches/Rounds/Round.cs:144-151`) | 6 cards played face down | **keep as is** | Nothing. This is the mechanic that translates best. |
| An Intent must name a known, affordable Spell | `IntentRules.CanAct`: alive, unstunned, knows it, `actor.Energy >= spell.Stats.Cost` (`Rules/Combat/IntentRules.cs:50-69`) | 1 lookup of the Energy track, 1 comparison, per Creature | **restate** | Nothing. Energy tracks are public, so affordability is checkable without revealing the Intent; the rulebook must say so once. |
| The declaration is not a reservation | The cost is checked again at Resolution (`Rules/Combat/ResolutionRules.cs:36-40`) and spent only then (`Rules/Combat/CombatExecution.cs:28`) | 1 re-check per cast, later in the Round | **restate** | Nothing, but two Creatures can both declare a Spell only one of them can afford after an Energy drain lands. |

### 1.7 `RevealAndTarget` (Combat)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| Reveal in timeline order, bind targets at reveal | The reveal cursor walks the timeline; targets are chosen after seeing what came before (`Rules/Combat/ActionRules.cs:16-52`) | 1 card flip and 1 to 3 target markers per Activation slot; 6 slots a Round | **needs a component** | Target markers, one set per Player. Nothing is lost; this is the other mechanic that translates for free. |
| Targeting spec: origin, scope, count | Origin `Self`, `Ally` or `Enemy`; scope single or multi; at most `maxTargets` (`Rules/Combat/TargetingRules.cs:40-50`) | 1 lookup on the card, then a count | **restate** | Nothing. In `data/`: 24 Enemy, 9 Ally, 3 Self; 26 single-target, 10 multi (9 at 3, 1 at 2). |
| A Multi Spell may take fewer targets | `LegalTargets` returns a minimum of 1 (`TargetingRules.cs:49`) | 1 decision per multi-target cast | **restate** | Nothing, but it is a real choice — hitting one enemy with `meteor` is legal — and nothing on the card says so today. |
| Ally includes the caster | `creature.Owner == actor.Owner`, the actor included (`TargetingRules.cs:43`) | 0 | **restate** | Nothing. A Creature can `guard` itself; the card does not say it. |
| No duplicate targets | `targets.Distinct().Count() != targets.Count` (`TargetingRules.cs:68`) | 0, physically impossible with one marker per target | **keep as is** | Nothing: the components enforce it. |
| A Spell with no legal target is revealed with no targets | `ActionRules.cs:40-42`; it Fizzles later | 1 card flip, no markers | **restate** | Nothing. The timeline always moves on, which is what keeps the track simple. |
| Dead and wrong-origin targets are refused here | Per-target failures block the binding (`TargetingRules.cs:79-102`) | 1 lookup per target | **keep as is** | Nothing. |

### 1.8 `ActionResolution` (Combat)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| Fizzle | A dead or stunned actor, one that no longer knows or can afford the Spell, a global targeting failure, or no target left (`ResolutionRules.cs:36-54`; ADR 0038 for the word) | 1 to 4 checks per cast, before anything moves | **restate** | Nothing. The rulebook owes one clear paragraph; it is the rule most likely to be played wrong. |
| A Fizzle costs nothing | `CombatResolution.Fizzle` spends no Energy and applies no outcome (`Rules/Combat/CombatResolution.cs:49-54`, `CombatExecution.cs:22-25`) | 0 | **keep as is** | Nothing. |
| One critical roll a cast | `random.NextDouble() < actor.CriticalChance + spell.Stats.CriticalChance` (`ResolutionRules.cs:56`) | 1 die roll and 1 lookup per cast — **every** cast, because the Creature's own chance is 0.05, so the 15 Spells with a bonus of 0 still roll | **needs a component** | A die, settled by fork B. Six rolls a Round. See Part 3 for what the snap has to cover. |
| A critical multiplies Damage and a direct Heal, floored | `Multiplied(amount, multiplier)` on `Damage` and `Heal` only (`ResolutionRules.cs:73-74`, ADR 0033) | 1 multiplication per affected Outcome, at a multiplier of 2.0 | **restate** | Nothing. At 2.0 it is a doubling, which is the cheapest arithmetic there is. |
| The critical applies *before* Defense | `Math.Max(0, Multiplied(damage.Amount, multiplier) - target.TotalDefense.Value)` (`ResolutionRules.cs:73`) | 1 ordering rule held in the head | **restate** | Nothing, but getting it backwards changes the result, so it must be printed on the player aid. |
| A critical reaches nothing else | Not a lasting Effect, not a Caster effect, not Energy (`ResolutionRules.cs:65,75-77`, ADR 0033, ADR 0031, ADR 0035) | 0, once the boundary is taught as one sentence | **restate** | Nothing. "What the cast puts on a target's Health now" is the whole rule. |
| Damage minus total Defense, floor zero | `ResolutionRules.cs:73` | 1 subtraction and 1 floor per target, on numbers up to 20 | **restate** | Nothing. |
| Total Defense is base plus buffs less debuffs, floored at zero | `Creature.cs:58-60` (ADR 0035) | 1 sum over the Condition tokens per target, per cast | **needs a component** | A Defense track holding the running total, so the sum is done once when a Condition lands and not once per cast. |
| The energy cost is spent | `actor.SpendEnergy(resolution.EnergySpent)` (`CombatExecution.cs:28`) | 1 token move | **keep as is** | Nothing. |
| A Heal is capped by Health missing | `Creature.cs:139` | 1 comparison | **keep as is** | Nothing. |
| An Energy drain takes at most what the target has | `Creature.cs:163-176` (ADR 0035) | 1 comparison | **keep as is** | Nothing. |
| Caster effects resolve once per cast, unmultiplied, never on a Fizzle | `ResolutionRules.cs:65` (ADR 0031) | 1 to 2 operations on the caster's own board | **restate** | Nothing. Seven Spells in `data/` carry one; the card face must show it as a separate line or it will be read as a target effect. |
| Damage is capped by the Health left, and an Outcome that changed nothing is dropped | `CombatExecution.cs:53-71` | 1 comparison | **keep as is** | Nothing. |
| A lasting Effect attaches as a Condition per its Stacking policy | `Creature.Apply` through `ConditionSet.Apply` (`Creatures/ConditionSet.cs:26-43`) | 1 token placed with an amount and a Duration | **needs a component** | Condition tokens, in eight kinds, with a Duration dial. Nothing is lost. |
| `Refresh` keeps the existing amount and discards the new one | `ConditionSet.cs:36-40` matches by effect **type** only; `Condition.Refresh` restarts `Effect.Duration` — the *existing* Effect's (`Creatures/Condition.cs:47-51`); pinned by `tests/DownfallArena.Domain.Tests/Matches/Creatures/ConditionTests.cs:58-73` | 1 lookup, then a rule that surprises everyone: `mortal_wound`'s Bleed 4 landing on a Creature already carrying `toxic_waves`' Bleed 2 leaves it bleeding **2** | **simplify (ADR)** | The stronger Bleed is lost. Six Spells carry a Bleed and share one Condition slot per Creature. See ADR candidate 1. |
| `Stack` adds another Condition | `ConditionSet.cs:29` | 1 more token per application | **needs a component** | Enough tokens; how many is unbounded today. See ADR candidate 3. |

### 1.9 `Cleanup` (End of round)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| Every Condition counts one Round down and expires at zero | `UpkeepRules.Cleanup` calls `Creature.TickConditions` (`UpkeepRules.cs:173-188`, `ConditionSet.cs:48-58`) | 1 dial turn per token on the board | **needs a component** | A Duration dial on the Condition token. Nothing is lost. |
| The first countdown after an application does not count | A `_fresh` flag skips the first tick (`Condition.cs:12,53-59`) | 0, **if** the rule is restated: every Condition in `data/` is applied in Combat, after that Round's ticks, so `durationRounds: N` is exactly "N of the following Rounds" | **restate** | Nothing today. The mechanism is not the rule; see ADR candidate 5 for whether the engine should say it that way. |
| A permanent Condition never counts down | `RemainingRounds` is null (`Condition.cs:34-36`, `Duration.cs:20-22`) | 0 | **keep as is** | Nothing, but it is what makes ADR candidate 3 unbounded. |
| A refresh also resets the free tick | `Condition.Refresh` sets `_fresh = true` (`Condition.cs:50`) | 0 | **restate** | Nothing, once the rule reads "N of the following Rounds" from the refresh as well. |

### 1.10 `Finalization` (End of round)

| Mechanic | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- |
| A defeated Team loses at the end of a Round | `WinCondition.Evaluate` (`Rules/WinCondition.cs:17-19`, ADR 0011) | 1 lookup per Team | **keep as is** | Nothing. |
| Both Teams defeated in the same Round is a draw | `WinCondition.cs:31-38` | 1 comparison | **keep as is** | Nothing. A Caster effect can kill its own caster (ADR 0031), so this is reachable, not theoretical. |
| The Round cap ends the Match on total remaining Health | `completedRound >= rules.RoundCap` (`WinCondition.cs:23-26`) | 2 sums of 3 numbers, once, at the end | **keep as is** | Nothing. The cap's *value* is a `RuleSet` parameter, so a shorter table Match costs no fidelity; which number lands the median Match in an evening is a phase-2 measurement, not a rule change. |
| Equal Health at the cap is a draw | `WinCondition.cs:41-48` | 1 comparison | **keep as is** | Nothing. |

---

## Part 2. The twelve effect kinds

The closed taxonomy of ADR 0012, extended by ADR 0019, ADR 0020, ADR 0035 and ADR 0036. Counts and value
ranges are computed from `data/Spells/**` with a Python pass over the 36 files, counting a Spell once per
kind whether the Effect sits in `effects` or in `casterEffects`. No file in `data/Spells/**` authors a
`stacking` key, so every Condition uses its family default from the domain factories: `Refresh` for Bleed,
Regeneration, Energy regeneration and Stun; `Stack` for the four Defense and Initiative kinds
(`src/DownfallArena.Domain/Resources/Effects/*.cs`).

### Instant effects

| Effect kind | Spells in `data/` | Values used | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- | --- | --- |
| `Damage` | 23 (22 on targets, 2 on the caster; `hateful_sacrifice` and `summon_minions` are the caster ones) | amounts 2, 3, 4, 5, 6, 7, 10 | Multiplied by the critical, then reduced by total Defense, floor zero (`ResolutionRules.cs:73`) | 3 operations per target: double or not, subtract Defense, subtract from Health | **restate** | Nothing. This is the arithmetic the plan flagged, and at a multiplier of 2.0 it is the cheapest shape it can have. |
| `Heal` | 7 (5 on targets, 2 on the caster: `parasite_jab`, `soul_devourer`) | amounts 2, 3, 4, 7 | Multiplied by the critical (ADR 0033), capped by Health missing (`Creature.cs:139`) | 2 operations per target | **keep as is** | Nothing. |
| `EnergyGain` | 2 (`wait`, `restorative_burst`) | amount 2 | Added, never clamped (`Creature.cs:147-160`) | 1 token move | **keep as is** | Nothing at the cast; the unbounded track is ADR candidate 2. |
| `EnergyDrain` | 1 (`soul_devourer`) | amount 2 | Takes at most what the target has (`Creature.cs:163-176`, ADR 0035) | 1 comparison, 1 token move | **keep as is** | Nothing. |

### Lasting effects (they become Conditions)

| Effect kind | Spells in `data/` | Values used | What the engine does | By hand | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- | --- | --- |
| `Bleed` | 6 (4 on targets: `mortal_wound`, `poison_slash`, `summon_minions`, `toxic_waves`; 2 on the caster: `crazed_specter`, `revenant_guards`) | 2, 3 or 4 a Round for 1, 2 or 3 Rounds | Damage at the start of each of the Creature's Rounds, ignoring Defense (`UpkeepRules.cs:62-70`); `Refresh` | 1 token with an amount and a dial; 1 subtraction a Round | **needs a component** | A Bleed token that shows both numbers. Six Spells sharing one slot per Creature is ADR candidate 1. |
| `Regeneration` | 1 (`healing_screech`) | 3 a Round for 2 Rounds | Heals before the Bleeds (`UpkeepRules.cs:52-60`, ADR 0019) | 1 token, 1 addition a Round | **needs a component** | A Regeneration token. Nothing is lost. |
| `EnergyRegeneration` | 1 (`momentum`) | 2 a Round for 3 Rounds | Gives Energy before the heals (`UpkeepRules.cs:42-50`, ADR 0020) | 1 token, 1 addition a Round | **needs a component** | An Energy regeneration token. Nothing is lost. |
| `Stun` | 2 (`crushing_stomp`, `tranquilizer_dart`) | 2 Rounds, both | The Creature takes no Speed choice, no Activation slot and no Intent (`SpeedRules.cs:34`); `Refresh` | 1 token; the creature board takes no speed token for 2 Rounds | **needs a component** | A Stun token. Nothing is lost, but a 2-Round Stun removes a third of a Team for two full Rounds and the rulebook must say it plainly. |
| `DefenseBuff` | 4 (`full_plate`, `guard`, `revenant_guards`, `thundering_seal`) | amounts 1, 2, 3; Durations 1 Round, 2 Rounds, **permanent** | Added into total Defense (`Creature.cs:58`); `Stack`, so every application adds a token | 1 token and 1 addition on the Defense track per application | **needs a component** | A Defense track. Three of the four Spells carry a permanent half that stacks without a bound: ADR candidate 3. |
| `DefenseDebuff` | 3 (2 on targets: `infectious_blast`, `noxious_cure`; 1 on the caster: `psycho_rush`) | amount 2; Durations 1 Round and **permanent** | Subtracted from total Defense, floored at zero (`Creature.cs:58-60`, ADR 0035); `Stack` | 1 token and 1 subtraction | **needs a component** | The same track. Bounded below by the floor, so it does not run away the way the buff does. |
| `InitiativeBuff` | 1 (`death_squad`) | amount 2 for 1 Round | Added into Current initiative before the debuffs (`Creature.cs:74-76`, ADR 0036); `Stack` | 1 token and 1 marker move, read once when the timeline is built | **needs a component** | An Initiative track. Nothing is lost. |
| `InitiativeDebuff` | 2 (`ice_spear`, `protective_slam`) | amount 2 for 1 or 2 Rounds | Subtracted, floored at zero (`Creature.cs:74-76`); `Stack` | 1 token and 1 marker move | **needs a component** | The same track. |

Two readings the counts make plain. First, the taxonomy is used unevenly: `Damage` is in 23 of 36 Spells and
five kinds are in one or two. Second, the authored values are already small and repetitive — every
`DefenseDebuff`, `InitiativeBuff` and `InitiativeDebuff` in the catalogue has an amount of exactly 2, and
Durations are only ever 1, 2, 3 or permanent. A token set is therefore small, which is good news for phase 3.

---

## Part 3. The Spell catalogue

All 36 files under `data/Spells/**`, read from `data/` and not from [spells.md](../domain/spells.md).

**Tracking cost** is counted for one cast at the Spell's maximum target count. *Ops* counts: the critical
roll (1, always, because the Creature's own Critical chance is 0.05), paying the energy cost (1 when the cost
is above zero), then per target 3 for a `Damage`, 2 for a `Heal`, 1 for an `EnergyGain`, `EnergyDrain` or a
lasting Effect, plus the Caster effects at 2 for a self-`Damage` and 1 for anything else. *Tokens* counts
Condition tokens placed. *Targets* is `maxTargets`. Tier is ADR 0034's depth in
`data/TalentTrees/talent_tree.v1.json`.

**Critical chances the die has to cover.** Ten distinct bonuses are authored: 0 (15 Spells), 0.22, 0.28,
0.33 (5 Spells), 0.333, 0.5 (8 Spells), 0.617, 0.717, 0.75, 0.8. Added to the Creature's 0.05 they make ten
chances actually rolled: 0.05, 0.27, 0.33, 0.38, 0.383, 0.55, 0.667, 0.767, 0.8, 0.85. Four of the ten sit
on a 1-in-20 grid (0.05, 0.55, 0.8, 0.85); none sits on a 1-in-6 grid. `data/balance/knobs.json` already
declares `/criticalChance` a knob on 20 Spells with a step of 0.05, so a d20 snap is inside the declared
search space and a d6 snap is not. Which die, the per-Spell snapped value and the error each snap costs are
phase 2's, measured.

**Card text.** The statline every card must carry — cost, targets, effects with amounts and Durations,
caster effects, critical chance, unlock initiative — was generated for all 36 and measured. The longest is
`revenant_guards` at 153 characters; the median is 90 and the shortest is 66; nothing exceeds 160. No Spell
overflows a card on its statline alone. The flag below marks the seven whose statline is over 110 characters
*and* which need a second sentence the rulebook cannot carry for them (a Caster effect line, or two
Conditions of the same kind on one target): `revenant_guards`, `crazed_specter`, `psycho_rush`,
`summon_minions`, `soul_devourer`, `thundering_seal`, `guard`. Whether that fits is the component-designer's
measurement in phase 3, against a real card size and a real type size; this document only reports the
character counts and which rows carry an extra rule.

### Trivially playable — 5 operations or fewer and at most 2 tokens: 16 Spells

| Spell | Tier | Targets | Ops | Tokens | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- | --- | --- |
| `wait` | 0 | self | 2 | 0 | **keep as is** | Nothing. |
| `momentum` | 2 | self | 2 | 1 | **needs a component** | An Energy regeneration token. |
| `full_plate` | 2 | self | 3 | 1 | **needs a component** | A permanent Defense token; it is ADR candidate 3's worst case. |
| `rejuvenate` | 1 | 1 ally | 4 | 0 | **keep as is** | Nothing. |
| `restorative_gush` | 3 | 1 ally | 4 | 0 | **keep as is** | Nothing. |
| `guard` | 1 | 1 ally | 4 | 2 | **needs a component** | Two Defense tokens from one cast, one permanent and one timed. Flagged for card text. |
| `thundering_seal` | 3 | 1 ally | 4 | 2 | **needs a component** | The same, at amount 3. Flagged for card text. |
| `basic_attack` | 0 | 1 enemy | 5 | 0 | **keep as is** | Nothing. |
| `heavy_strike` | 0 | 1 enemy | 5 | 0 | **keep as is** | Nothing. |
| `throwing_star` | 1 | 1 enemy | 5 | 0 | **keep as is** | Nothing. |
| `pummel` | 1 | 1 enemy | 5 | 0 | **keep as is** | Nothing; its 0.717 is a snap job, not a rule. |
| `lightning_bolt` | 1 | 1 enemy | 5 | 0 | **keep as is** | Nothing; 0.617 likewise. |
| `enraged_charge` | 2 | 1 enemy | 5 | 0 | **keep as is** | Nothing. |
| `engulfing_flames` | 3 | 1 enemy | 5 | 0 | **keep as is** | Nothing. |
| `restorative_burst` | 3 | 1 ally | 5 | 0 | **keep as is** | Nothing. |
| `healing_screech` | 2 | 1 ally | 5 | 1 | **needs a component** | A Regeneration token. |

### A component, or a second reading — 6 or 7 operations, or 3 tokens: 13 Spells

| Spell | Tier | Targets | Ops | Tokens | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- | --- | --- |
| `death_squad` | 3 | 3 allies | 5 | 3 | **needs a component** | Three Initiative buff tokens, read once when the timeline is built. |
| `hateful_sacrifice` | 3 | 1 enemy | 7 | 0 | **restate** | Nothing; 10 damage and 4 back on the caster, which can kill it. Two Health tracks move. |
| `poison_slash` | 1 | 1 enemy | 6 | 1 | **needs a component** | A Bleed token; shares one slot with five other Spells (ADR candidate 1). |
| `protective_slam` | 2 | 1 enemy | 6 | 1 | **needs a component** | An Initiative debuff token. |
| `ice_spear` | 3 | 1 enemy | 6 | 1 | **needs a component** | The same. |
| `mortal_wound` | 3 | 1 enemy | 6 | 1 | **needs a component** | A Bleed token at 4 a Round, the largest in the catalogue. |
| `tranquilizer_dart` | 3 | 1 enemy | 6 | 1 | **needs a component** | A Stun token; two Rounds lost. |
| `crushing_stomp` | 3 | 1 enemy | 6 | 1 | **needs a component** | The same, on top of 7 damage at cost 4, the only cost-4 Spell. |
| `psycho_rush` | 3 | 1 enemy | 6 | 1 | **needs a component** | A Defense debuff on its own caster. Flagged for card text. |
| `parasite_jab` | 2 | 1 enemy | 6 | 0 | **restate** | Nothing; the caster Heal needs its own line on the card. |
| `infectious_blast` | 3 | 3 enemies | 5 | 3 | **needs a component** | Three permanent Defense debuff tokens from one cast at cost 1. |
| `summon_minions` | 2 | 3 enemies | 7 | 3 | **needs a component** | Three Bleed tokens and 2 self-damage; the only Spell with no immediate effect on a target. Flagged for card text. |
| `soul_devourer` | 3 | 1 enemy | 7 | 0 | **restate** | Nothing, but it is three economies in one cast: Health, Energy and the caster's Health. Flagged for card text. |

### Expensive — 8 operations or more: 7 Spells

| Spell | Tier | Targets | Ops | Tokens | Verdict | What the verdict costs |
| --- | --- | --- | --- | --- | --- | --- |
| `chain_slash` | 3 | 2 enemies | 8 | 0 | **restate** | Nothing; 2 targets, 3 operations each. |
| `revenant_guards` | 3 | 3 allies | 9 | 7 | **needs a component** | Six Defense tokens on the Team plus a Bleed on the caster, from one cast at cost 2. The heaviest cast in the catalogue and the longest card text (153 characters). Flagged. |
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

> **Settled (2026-09-14) by [ADR 0040](../adr/0040-a-condition-stacks-except-a-stun.md).** The per-round
> family stacks; `Stun` keeps `Refresh`, because its only payload is a duration. One application, one token.
> Measured before and after on the benchmark seeds: the four readings barely move, and the digest changes in
> one mirrored pair out of 400 matches.

**What the table shows.** `ConditionSet.Apply` finds an existing Condition by effect *type* only
(`ConditionSet.cs:28`), and `Refresh` restarts the *existing* Effect's Duration and keeps its amount
(`Condition.cs:46-51`), pinned by `ConditionTests.cs:57-73`. Six Spells in `data/` carry a Bleed and share
one slot per Creature. So `mortal_wound`'s Bleed 4 for 2 Rounds, cast on a Creature already carrying
`toxic_waves`' Bleed 2 for 1 Round, leaves it bleeding **2** a Round — and credits that 2 to `mortal_wound`
(ADR 0027). A player at a table will place the new token and be wrong.

**The question.** When a `Refresh` Effect lands on a Creature that already carries one of its kind, which
amount and which Duration survive?

- *Keep the existing amount and Duration* (today). Costs: a stronger Bleed is silently wasted; unteachable.
- *Take the new amount and the new Duration.* Costs: a domain change plus a benchmark digest move; a weak
  Bleed can now overwrite a strong one, which is the same trap in the other direction.
- *Take the larger amount and the longer Duration, each independently.* Costs: a domain change, a digest
  move, and a Condition whose amount and Duration come from different casts, which ADR 0027's source
  attribution then has to answer for.
- *Change the family default from `Refresh` to `Stack`.* Costs: unbounded Bleed tokens, and the table pays
  in components what it saves in surprise.

### Candidate 2. Energy has no maximum

> **Settled (2026-09-14) by the maintainer: the engine does not change.** A track that ends is a component
> problem, not a rule problem — a die, or tokens stacked in a space on the board. What phase 2 owes is the
> number: the highest Energy actually banked over the benchmark seeds, so the component is sized by evidence
> rather than by a guess.

**What the table shows.** `Energy` is a `NonNegativeStat` with no ceiling (`Energy.cs:3`) and `GainEnergy`
never clamps (`Creature.cs:147-160`). A Creature gains 2 a Round from the Rule set, 2 more from `wait`, and
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
> `full_plate` is not available in Round 1. The tree says it is reachable in Round 1 — `Brawler` asks for the
> three starting Spells, `Warlord` asks for any of `pummel`/`guard`, `full_plate` asks for nothing of its own,
> and Evolution picks inside a Round are sequential, so the two picks of Round 1 buy `pummel` then
> `full_plate`, which the Creature's 2 Energy affords. What this row left out is the price: casting it every
> Round spends that Creature's activation every Round, so it never attacks. Whether the line is degenerate is
> therefore a measurement (phase 2), not the proof this row claimed. What is not in doubt: nothing bounds the
> total, so the table needs an unbounded supply of Defense tokens until something does.

**What the table shows.** `DefenseBuff` defaults to `Stack` (`Resources/Effects/DefenseBuff.cs:13`), a
permanent Duration never counts down (`Condition.cs:34-36`), and nothing caps total Defense above
(`Creature.cs:58-60`). `full_plate` is Self-targeted, costs 1, gives +3 permanent, and is castable from
Round 1: both Evolution picks of Round 1 buy `pummel` then `full_plate`, and the Creature has 2 Energy. Cast
every Round, its Defense is 3k after Round k. The largest single hit in the catalogue is 10
(`psycho_rush`, `engulfing_flames`, `hateful_sacrifice`); doubled by a critical that is 20. From Round 7 the
Creature takes zero from every attack in the game except a Bleed, which ignores Defense. `thundering_seal`
does the same for an ally at +3, and `revenant_guards` does it for the whole Team at +2 a cast. The plan
already calls this "probably not what anyone wants"; the table gives the round number.

**The question.** What bounds a permanent stat buff?

- *Nothing* (today). Costs: a degenerate line exists, and the component set needs an unbounded token supply.
- *A cap on total Defense, in the `Rule set`.* Costs: a new Rule set value and a stamp change; the cap is a
  number to measure, and buff Spells become worthless once it is reached.
- *Make the permanent halves `Ignore` instead of `Stack`.* Costs: a one-line default change per Effect, a
  digest move, and four Spells lose their re-cast value entirely — `full_plate` becomes a once-a-Match cast,
  which is closer to the `Passive` it is authored as.
- *Remove permanent Durations from the taxonomy and give those halves a long finite Duration.* Costs: a
  content change on four Spells and a new content hash; the arc of a Match loses its only permanent gain.

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
  six Bleed sources, nine sub-classes — which is a component cost for a reading no player uses.

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

### Not raised, and why

- **The 30-Round cap.** It is a `RuleSet` parameter (`RuleSet.cs:20,31`), and the engine plays any value
  unchanged. A shorter table Match is the same rules with a different number, so it costs no fidelity and
  needs no ADR. Which number lands the median Match in an evening is a phase-2 measurement.
- **Continuous critical chances.** Settled by fork B: a die, and the catalogue snapped to its grid. Part 3
  reports what the snap has to cover; the die and the per-Spell values are phase 2's.
- **Team size, Energy per Round, Evolution picks, the critical multiplier.** The same as the Round cap:
  parameters, not rules.

---

## Part 5. Coverage check

**Sub-phases.** All ten of ADR 0010 appear, each as exactly one section, in the enum's order
(`RoundSubPhase.cs:8-17`): `EnergyGain`, `OngoingEffects`, `Evolution`, `Speed`, `TurnOrderResolution`,
`IntentSelection`, `RevealAndTarget`, `ActionResolution`, `Cleanup`, `Finalization`. 10 of 10. No mechanic is
filed under two of them.

**Effect kinds.** All twelve of the taxonomy appear, each as exactly one row in Part 2: `Damage`, `Heal`,
`EnergyGain`, `EnergyDrain`, `Bleed`, `Regeneration`, `EnergyRegeneration`, `Stun`, `DefenseBuff`,
`DefenseDebuff`, `InitiativeBuff`, `InitiativeDebuff`. 12 of 12, and every one has at least one Spell in
`data/` using it.

**Spells.** All 36 files under `data/Spells/**` appear, each as exactly one row in Part 3: 16 trivially
playable, 13 needing a component or a second reading, 7 expensive. 16 + 13 + 7 = 36. By tier: 3 at tier 0,
6 at tier 1, 9 at tier 2, 18 at tier 3.

**Verdicts.** 105 rows carry exactly one verdict each: 57 in Part 1, 12 in Part 2, 36 in Part 3. The totals,
counted over the file rather than recalled: **needs a component** 41, **keep as is** 33, **restate** 29,
**simplify (ADR)** 2. `cut from the tabletop rule set` is used zero times, as fork A requires.

| Verdict | Part 1 | Part 2 | Part 3 | Total |
| --- | --- | --- | --- | --- |
| keep as is | 19 | 3 | 11 | 33 |
| restate | 22 | 1 | 6 | 29 |
| needs a component | 14 | 8 | 19 | 41 |
| simplify (ADR) | 2 | 0 | 0 | 2 |
| cut from the tabletop rule set | 0 | 0 | 0 | 0 |
| **Total** | **57** | **12** | **36** | **105** |

**ADR candidates.** Five raised, three excluded with a reason.
