# The round, step by step {#the-round}

Every round walks the same steps, in this order, never backwards: **Upkeep → Command → Combat → Aftermath**.
Each section below is one step. It says when it comes, who decides, and what happens. Every example uses this
table's numbers and the real spells.

## Energy gain {#energy-gain}

*Upkeep.* The round begins, and every **living** creature gains {rules.energyPerRound} Energy, and the Energy
its Archmage passives give. Nobody decides anything. A defeated creature gains nothing, now or ever.

Archmage is shared with the whole team: each copy a team owns gives every living creature of that team
{tier:archmage:v1.upkeepEnergy} Energy more at every Upkeep, from the round after it was acquired.

*Example.* Round 8 begins. Creature 1 has 1 Energy and Creature 2 has 4; Creature 3 is defeated. Creature 1 goes
to {1 + rules.energyPerRound} and Creature 2 to {4 + rules.energyPerRound}. Creature 3 gains nothing. Had
Creature 2 acquired Archmage in round 7, every living creature of its team would gain
{rules.energyPerRound + tier:archmage:v1.upkeepEnergy}; with a second copy on the team,
{rules.energyPerRound + 2 * tier:archmage:v1.upkeepEnergy}.

sources: ADR 0010, ADR 0101, ADR 0114, ADR 0132, game-rules#round-sequence-adr-0010

```card
id: energy
title: Energy
Every living creature gains Energy at Upkeep, when a round begins.
A spell costs Energy. It is spent when the spell resolves, never before.
> A creature at 1 Energy gains {rules.energyPerRound} at Upkeep and has {1 + rules.energyPerRound}: enough for a spell that costs {1 + rules.energyPerRound}.
```

## Ongoing effects {#ongoing-effects}

*Upkeep.* Right after the Energy gain, the conditions that work at Upkeep apply, in three passes. The order
matters:

1. **Energy regeneration** gives its Energy.
2. **Regeneration** heals, never above the Health the creature started with.
3. **Bleed** deals its damage. **A Bleed ignores Defense**: it is the only damage in the game that does.

A creature with two conditions of one kind takes both. Healing comes before bleeding on purpose: a Regeneration
can carry a creature through a Bleed that would otherwise defeat it.

**A Bleed can end the match.** When the Bleed pass leaves a team with no living creature, the match ends there,
before anything is chosen. Both teams defeated by the same pass is a draw.

*Example.* Creature 5 is at 1 Health, with a Regeneration of {spell:healing_screech:v1.regeneration} from Vital
Echo and a Bleed of {spell:toxic_waves:v1.bleed} from Void Pulse. Its Defense is 3. The Regeneration heals it to
{1 + spell:healing_screech:v1.regeneration}. The Bleed deals {spell:toxic_waves:v1.bleed}, and its Defense does
not count: it ends at {1 + spell:healing_screech:v1.regeneration - spell:toxic_waves:v1.bleed} Health. In the
other order, the Bleed would have defeated it before the healing came.

sources: ADR 0019, ADR 0020, ADR 0083, game-rules#round-sequence-adr-0010

## Mutations {#evolution}

*Command · Evolution.* Only on a mutation round: round {rules.firstEvolutionRound}, then every
{rules.evolutionInterval|round|rounds}. On any other round this step asks nobody and the round goes on to the tempos.

Each team has {rules.evolutionPicksPerOpportunity} picks. One pick acquires one mutation for one of your
**living** creatures, and **a creature acquires at most one mutation a round**: your picks go to different
creatures, and no creature climbs two Stages in one round. A team down to one living creature has one pick.

Both players pick in secret, at the same time. Nothing changes until both are done; then every pick of both
teams is revealed and acquired at once. A pick you do not want, you pass. The Mutation Tree opens with the step:
choose a creature, read a mutation, confirm it.

**What a mutation is.** A named set of spells. Acquiring it teaches the creature every one of them at once,
and raises its base initiative by the mutation's bonus, for the rest of the match. That bonus is the only
thing that ever moves a base initiative: acquiring is how a creature gets stronger, and also how it gets faster.

**Which mutation.** There are three families, Brute, Warped and Predator, each a tree of four Stages. A Stage I
mutation needs nothing. Every other one needs one named mutation of its family, a Stage below it: the Mutation
Tree shows which. A creature may acquire a mutation it does not own when it owns what that mutation needs, and
it may mix families by acquiring another family's Stage I. Two creatures may take the same mutation. What a
creature may acquire is read from what it
owns as the step opens: a pick of this round opens nothing until the next mutation round.

*Example.* Round {rules.firstEvolutionRound}. Every creature knows Strike{?creature.knows = 'spell:heavy_strike:v1'} and Focus{?creature.knows = 'spell:wait:v1'}{?creature.knows.count = 2} and has
{creature.initiative} initiative. The first player picks Brute for Creature 1 and Warped for Creature 2. They
would like Oppressor for Creature 1 too, but it needs Brute, which Creature 1 does not own yet, and Creature 1
already has its pick. At the same time, the second player picks Predator for Creature 4 and Brute for Creature 5.
Both are done, and the four mutations are acquired together:

- Creature 1, Brute: Pummel{?tier:brute:v1.teaches = 'spell:pummel:v1'} and Brace{?tier:brute:v1.teaches = 'spell:guard:v1'}{?tier:brute:v1.teaches.count = 2}, and {creature.initiative + tier:brute:v1.initiativeBonus} initiative.
- Creature 2, Warped: Shock{?tier:occultist:v1.teaches = 'spell:lightning_bolt:v1'} and Revitalize{?tier:occultist:v1.teaches = 'spell:rejuvenate:v1'}{?tier:occultist:v1.teaches.count = 2}, and {creature.initiative + tier:occultist:v1.initiativeBonus}
  initiative.
- Creature 4, Predator: Venom Claw{?tier:prowler:v1.teaches = 'spell:poison_slash:v1'} and Quill{?tier:prowler:v1.teaches = 'spell:throwing_star:v1'}{?tier:prowler:v1.teaches.count = 2}, and {creature.initiative + tier:prowler:v1.initiativeBonus}
  initiative.
- Creature 5, Brute: Pummel{?tier:brute:v1.teaches = 'spell:pummel:v1'} and Brace{?tier:brute:v1.teaches = 'spell:guard:v1'}{?tier:brute:v1.teaches.count = 2}, and {creature.initiative + tier:brute:v1.initiativeBonus} initiative.

Oppressor waits for Creature 1 until round {rules.firstEvolutionRound + rules.evolutionInterval}, the next
mutation round.

sources: ADR 0056, ADR 0057, ADR 0059, ADR 0066, ADR 0089, game-rules#round-sequence-adr-0010

```card
id: mutation
title: Mutation
A mutation gives a creature new spells and an initiative bonus, for the rest of the match.
The last mutation of a family, its capstone, gives a passive in place of spells and an initiative bonus{?tier:titan:v1.initiativeBonus = 0}{?tier:archmage:v1.initiativeBonus = 0}{?tier:apex:v1.initiativeBonus = 0}.
On a mutation round, the team shares its picks, and a creature takes at most one mutation.
> A creature at {creature.initiative} base initiative acquires Brute, with a bonus of {tier:brute:v1.initiativeBonus}: it knows new spells and acts at {creature.initiative + tier:brute:v1.initiativeBonus}.
```

## Capstones {#capstones}

Each family closes with a Stage IV mutation, its **capstone**. A capstone needs **any one** of its family's
Stage III mutations. It teaches no spell and adds no initiative. It gives a **passive** instead, held from the
reveal that acquires it to the end of the match:

| Capstone | Family | Needs one of | Its passive |
| --- | --- | --- | --- |
| Titan | Brute | Ravager, Colossus, Tyrant | Every hit ignores {tier:titan:v1.sunder} Defense |
| Archmage | Warped | Cataclysm, Revenant, Transcendent | Energy +{tier:archmage:v1.upkeepEnergy} at every Upkeep, for every creature of the team |
| Apex | Predator | Blightweaver, Deathstalker, Soulreaver | Damage +{tier:apex:v1.damageBonus} on every hit |

**Archmage is shared.** The creature that acquires it owns it, and every creature of its team holds its
passive. It stacks: a teammate may acquire its own copy, and each copy gives every creature of the team its
passive once more. A copy keeps giving after the creature that acquired it is defeated: only the defeated
creature's own Upkeep gains stop.

How each passive plays is in [the passives](#passives).

sources: ADR 0101, ADR 0106, ADR 0114, game-rules#implemented

## Tempo {#tempo}

*Command · Tempo.* For each of your living creatures that is not stunned, choose a tempo: **Instinct** or
**Precision**. Both players choose in secret, and the tempos are revealed together.

- **Instinct** acts before every Precision creature, and **cannot roll a critical this round**, whatever its spell prints.
- **Precision** acts after every Instinct creature, and keeps its critical roll.

Choose Instinct to land a blow that does not need the dice, or to act before something that would defeat you.
Choose Precision when the critical is what you hope for, or when you want the board to change before you act.
A tempo chooses no spell: the spell is chosen when the creature's turn comes.

**A stunned creature takes no tempo.** It is not in the round: no tempo, no turn, nothing it can cast. A
defeated creature is not in the round either.

*Example.* Creature 4 was stunned last round. The second player chooses tempos for Creatures 5 and 6 only; the
first player for Creatures 1, 2 and 3. Five tempos are revealed, and the round will have five turns, not six.

sources: ADR 0010, ADR 0072, ADR 0112, game-rules#round-sequence-adr-0010

```card
id: tempo
title: Tempo
Each round, every creature that is not stunned takes a tempo: Instinct or Precision.
Instinct acts before every Precision creature, and cannot roll a critical this round.
Precision acts after every Instinct creature, and keeps the critical roll.
> An Instinct creature at {creature.initiative} initiative acts before a Precision creature at {creature.initiative + 4}.
```

## The order of the turns {#turn-order}

*Command · Turn order resolution.* The app builds the round's **timeline**, one turn for each creature that has
a tempo:

1. The Instinct creatures first, the highest initiative first.
2. Then the Precision creatures, the same way.

A creature's initiative is its base initiative, raised by its initiative buffs and lowered by its initiative
debuffs, never below zero. It is read once, when the timeline is built: a debuff that lands during the combat
changes the next round's order, not this one's.

Instinct always comes first. An Instinct creature at 0 initiative acts before a Precision creature at 20.

**A tie between the teams** is settled by a **roll-off**: each tied creature rolls a d20, and the highest takes
the first place. Creatures on a number both teams rolled roll again; a number only one team rolled stands. The
roll-off decides which places each team holds. How a team orders its own creatures in its places is
[the next step](#tie-order).

*Example.* Creatures 1, 2, 4 and 5 have acquired Warped, and Creatures 1 and 4 have since acquired Stormborn. Creatures 1
and 4 read {creature.initiative + tier:occultist:v1.initiativeBonus + tier:elementalist:v1.initiativeBonus}
initiative, Creatures 2 and 5 read {creature.initiative + tier:occultist:v1.initiativeBonus}, and Creatures 3
and 6 read {creature.initiative}. Last round, an Ice Grip lowered Creature 2's initiative by
{spell:frostbite:v1.initiativeDebuff}: it reads
{max(0, creature.initiative + tier:occultist:v1.initiativeBonus - spell:frostbite:v1.initiativeDebuff)} this round.
Creatures 1 and 4 chose Instinct, the rest Precision. Creatures 1 and 4 tie, and roll 8 and 15: Creature 4 acts
first, then Creature 1. Then the Precision creatures, by their initiative: Creature 5, then Creatures 3 and 6,
which tie between the teams and roll off as well, and Creature 2 last.

sources: ADR 0036, ADR 0059, ADR 0063, game-rules#round-sequence-adr-0010

```card
id: initiative
title: Initiative
Initiative orders the creatures inside their tempo: the highest acts first.
A creature starts with its base initiative. Each mutation adds its bonus, for the rest of the match.
A tie between the two teams is settled by a d20 roll-off.
> Two Precision creatures at {creature.initiative + 2} and {creature.initiative} initiative: the one at {creature.initiative + 2} acts first.
```

## Ordering your ties {#tie-order}

*Command · Tie order.* Where your team holds two places or more in one tie, you choose which of your tied
creatures takes which place. The other team's places do not move. A tie your team holds alone is yours to order
too, with no roll. Both players order in secret, and the orders are revealed together. A round with no such tie
asks nobody.

*Example.* Creatures 3, 5 and 6 tie in the Precision band and roll 11, 11 and 2. Both teams rolled 11, so
Creatures 3 and 5 roll again: 4 and 17. The places go to the second player, then the first, then the second.
The second player holds two of them and wants Creature 6 to act first: Creature 6 takes the first place,
Creature 3 the second, Creature 5 the third.

sources: ADR 0063, game-rules#round-sequence-adr-0010

## Your creature's turn {#your-turn}

*Combat · Activation.* The timeline is walked once, turn by turn. When the turn of one of your creatures comes,
you choose its **spell** and its **targets**, on the board as it stands, and confirm. The spell resolves at once,
before the next turn. The spell and its targets become public together.

Nothing is declared in advance. You choose among the spells the creature knows and can pay for now, and you
choose knowing everything that has happened this round. Every creature knows Focus{?creature.knows = 'spell:wait:v1'}, which costs
{spell:wait:v1.energyCost} Energy and targets only its caster{?spell:wait:v1.origin = 'Self'}: a creature in the round always has a spell to cast.

**Nothing is a reservation.** The Energy is spent when the spell resolves. Until its turn comes, a creature can
be defeated, stunned or drained. A creature defeated or stunned before its turn is
[skipped](#skipped-turn).

sources: ADR 0083, ADR 0110, ADR 0112, game-rules#implemented, game-rules#round-sequence-adr-0010

```card
id: intent
title: Choosing a spell
No spell is declared in advance: a creature chooses its spell when its turn comes.
You choose the spell and its targets on the board as it stands, and it resolves at once.
> A creature at {1 + rules.energyPerRound} Energy, at its turn, may cast any spell it knows that costs {1 + rules.energyPerRound} or less.
```

## Targets {#targets}

A spell says whom it may target:

- **Self**: the caster, and only the caster.
- **Ally**: any living creature of your team, **the caster included**.
- **Enemy**: any living creature of the other team.

A spell for one target takes exactly one. A spell for several takes **one or more, up to its maximum**:
Emberstorm, for several{?spell:meteor:v1.targets > 1} enemies{?spell:meteor:v1.origin = 'Enemy'}, may take a single one. A defeated creature cannot be a target, and no
creature can be named twice by one cast.

*Example.* Creature 6's turn comes, and its spell is Brace, for one{?spell:guard:v1.targets = 1} ally{?spell:guard:v1.origin = 'Ally'}: it may target itself or Creature 4,
but not Creature 5, defeated earlier this round. Then Creature 3 casts Emberstorm while two enemies stand: it
may name one or both.

sources: ADR 0083, game-rules#round-sequence-adr-0010

## Resolving a spell {#resolving-a-spell}

A spell resolves completely before the next turn comes, in this order:

1. **Pay.** The caster's Energy goes down by the spell's cost.
2. **Roll for a critical**, once for the whole cast, if the caster is Precision and the spell prints a chance.
   See [the critical roll](#the-critical-roll).
3. **Each effect, on each target.** A **hit** is the spell's damage, plus the caster's **damage bonus**,
   multiplied on a critical, less the target's **Defense**, never below zero. A heal never goes above the
   Health a creature started with. An Energy drain takes at most what the target has. A lasting effect becomes a
   [condition](#conditions).
4. **The caster's own line**, if the spell has one, once for the whole cast. Damage there is lowered by the
   caster's own Defense, and never takes a damage bonus or a critical.

A creature's **Defense** is its base Defense plus its Defense buffs, which count for {engine.defenseBuffCeiling} at most
however many there are, less its Defense debuffs, never below zero.

A hit never goes past the Health left: a creature at 3 Health hit for 10 loses 3. A spell that changes nothing on
a target, 0 damage through a Defense or a heal at full Health, did nothing to it.

*Example.* Creature 2 is Precision, at 4 Energy, and casts Incinerate{?spell:engulfing_flames:v1.origin = 'Enemy'} on Creature 5: cost
{spell:engulfing_flames:v1.energyCost}, damage {spell:engulfing_flames:v1.damage}, d20:
{21 - (creature.criticalChance + spell:engulfing_flames:v1.criticalChance) * 20}+. Creature 5 cast Carapace last round: its Defense is
{creature.defense + spell:full_plate:v1.defenseBuff}.

- Pay: Creature 2 goes to {4 - spell:engulfing_flames:v1.energyCost} Energy.
- The d20 is a critical: {spell:engulfing_flames:v1.damage} × {rules.criticalMultiplier} =
  {floor(spell:engulfing_flames:v1.damage * rules.criticalMultiplier)}.
- Less the Defense: Creature 5 loses
  {max(0, floor(spell:engulfing_flames:v1.damage * rules.criticalMultiplier) - (creature.defense + spell:full_plate:v1.defenseBuff))},
  and goes from {creature.health} to
  {max(0, creature.health - max(0, floor(spell:engulfing_flames:v1.damage * rules.criticalMultiplier) - (creature.defense + spell:full_plate:v1.defenseBuff)))}
  Health.

Had the roll missed, it would have lost
{max(0, spell:engulfing_flames:v1.damage - (creature.defense + spell:full_plate:v1.defenseBuff))}. **Multiply first, subtract
second**: the other order gives a number the game never deals.

sources: ADR 0033, ADR 0035, ADR 0076, ADR 0083, ADR 0101, game-rules#round-sequence-adr-0010

```card
id: defense
title: Defense
Defense lowers the damage of every hit a creature takes, never below zero.
Defense buffs add at most {engine.defenseBuffCeiling} to it, however many it holds.
Only damage over time, applied at Upkeep, ignores it.
> A hit of 7 on a creature with {creature.defense + 2} Defense deals {max(0, 5 - creature.defense)}. A creature starts at {creature.defense} Defense: the same hit on it deals {max(0, 7 - creature.defense)}.
```

## The damage bonus {#damage-bonus}

A creature's **damage bonus** is added to every hit it deals: Apex gives
{tier:apex:v1.damageBonus}, and each damage buff on it adds its own number. It is added **before** the critical
multiplies and before Defense is subtracted. It never touches a Bleed, a heal, or the caster's own line.

*Example.* Creature 4 owns Apex, is Precision, and casts Blood Price{?spell:hateful_sacrifice:v1.origin = 'Enemy'} on Creature 1, whose Defense is 3: damage
{spell:hateful_sacrifice:v1.damage}, d20: {21 - (creature.criticalChance + spell:hateful_sacrifice:v1.criticalChance) * 20}+. Add the bonus:
{spell:hateful_sacrifice:v1.damage + tier:apex:v1.damageBonus}. On a critical,
{floor((spell:hateful_sacrifice:v1.damage + tier:apex:v1.damageBonus) * rules.criticalMultiplier)}, less 3:
Creature 1 loses
{max(0, floor((spell:hateful_sacrifice:v1.damage + tier:apex:v1.damageBonus) * rules.criticalMultiplier) - 3)}.
Without one, {max(0, spell:hateful_sacrifice:v1.damage + tier:apex:v1.damageBonus - 3)}. **Add, multiply,
subtract**, in that order. Blood Price's own line then deals {spell:hateful_sacrifice:v1.caster.damage} damage to
Creature 4, less its own Defense of {creature.defense}: it loses
{max(0, spell:hateful_sacrifice:v1.caster.damage - creature.defense)}, with no bonus and no critical.

*Example, damage buffs.* Alpha Roar gave Creature 5 a damage buff of {spell:thundering_seal:v1.damageBuff}, and
Overdrive another of {spell:adrenaline_tonic:v1.damageBuff}. Its Strike, for {spell:heavy_strike:v1.damage},
deals
{spell:heavy_strike:v1.damage + spell:thundering_seal:v1.damageBuff + spell:adrenaline_tonic:v1.damageBuff} to a
creature with no Defense. Damage buffs stack: add every one.

sources: ADR 0101, game-rules#round-sequence-adr-0010

## Sunder {#sunder}

Titan's passive is a **sunder**: every hit its creature deals meets the target's Defense less
{tier:titan:v1.sunder}, never below zero. A sunder takes Defense away; it never adds damage, and against a target
with no Defense it does nothing. It moves no Defense: the target's Defense is the same against everyone else.

*Example.* Creature 3 owns Titan and casts Dominate{?spell:chain_slash:v1.origin = 'Enemy'}, damage {spell:chain_slash:v1.damage}, on Creature 5, which
holds Alpha Roar's Defense of {creature.defense + spell:thundering_seal:v1.defenseBuff}, and on Creature 4, at its base Defense of
{creature.defense}. With no
critical, both{?spell:chain_slash:v1.targets > 1} lose
{max(0, spell:chain_slash:v1.damage - max(0, creature.defense + spell:thundering_seal:v1.defenseBuff - tier:titan:v1.sunder))}. A
caster with no sunder would have dealt Creature 5 only
{max(0, spell:chain_slash:v1.damage - creature.defense - spell:thundering_seal:v1.defenseBuff)}.

sources: ADR 0106, game-rules#round-sequence-adr-0010

## Cleanup {#cleanup}

*Aftermath · Cleanup.* Every turn has come, and both teams still stand. Every condition counts one round down,
and a condition at zero ends. A condition applied **this** round does not count down yet: it lasts for the rest
of this round, then for every round it prints. See [durations](#durations).

A living creature whose Stun ends here cannot be stunned for the next round. See
[the stun](#stunned).

sources: ADR 0041, ADR 0072, game-rules#round-sequence-adr-0010

## The end of the round {#finalization}

*Aftermath · Finalization.* If this was round {rules.roundCap}, the match ends: the team with the **most
Health left**, added over its creatures, wins, and equal totals are a draw. Otherwise the next round begins.

*Example.* Round {rules.roundCap} ends with both teams standing. The first player's creatures have 4, 5 and 6
Health: 15. The second player's have 18, 0 and 0: 18. **The second player wins**, with fewer creatures standing.
Health decides, not survivors.

sources: ADR 0011, ADR 0105, game-rules#match-lifecycle
