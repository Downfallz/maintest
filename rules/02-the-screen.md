# What the screen shows {#the-screen}

The app is the table. It keeps the count of every number, settles every chance and walks every step nobody decides.
You make the decisions; this Part says what you see while you make them.

## Your creatures and theirs {#the-creatures}

Each team has {rules.teamSize} creatures. The first player's are numbered 1 to {rules.teamSize}, the second
player's {rules.teamSize + 1} to {rules.teamSize * 2}. The numbers never change, and the history names every
creature by its number: Creature 4.

Every creature shows, to both players:

- its **Health**, and the Health it started with;
- its **Energy**, which pays for its spells;
- its **Defense**, which lowers every hit it takes;
- its **initiative**, which orders it on the timeline;
- its **conditions**, each with the rounds it has left;
- its **mutations**, and the spells they taught it: its spellbook.

Nothing about a creature is hidden. What is hidden is a choice not yet revealed: a mutation picked this round, a
tempo, a tie order. Each is revealed for both players at once.

Every creature starts the match the same: {creature.health} Health, {creature.energy} Energy,
{creature.defense} Defense and {creature.initiative} initiative, and it knows two spells, Strike{?creature.knows = 'spell:heavy_strike:v1'} and Focus{?creature.knows = 'spell:wait:v1'}{?creature.knows.count = 2}.
Everything that will separate them is a decision you have not made yet.

sources: ADR 0011, ADR 0042, ADR 0089, game-rules#match-lifecycle

## What the app does for you {#what-the-app-does}

The steps nobody decides are played for you, and the history records each of them:

- **Upkeep.** Each creature's Energy gain, its Regeneration and its Bleed.
- **The timeline.** The order of the turns, built from the tempos and the initiatives. A tie between the teams
  is settled at random, and the timeline says which team got the earlier place.
- **The criticals.** Every critical chance, when its spell resolves. A cast that crits shows **Crit!** beside its
  caster; a cast that does not shows nothing there. The history says the chance the cast had.
- **The countdown.** Every condition counts down in the Aftermath, and ends at zero.
- **The end.** The match ends the moment a team is defeated, or when the last round ends.

When the app asks you something, it offers only what the rules allow: the mutations a creature may acquire, the
spells it can pay for now, the targets a spell may take. A choice it does not offer is not a legal one.

The **Help** dialog walks through the round you are in. The history explains each result after it resolves.

sources: ADR 0010, ADR 0063, ADR 0083, ADR 0112, ADR 0154, ADR 0208, game-rules#round-sequence-adr-0010

## The numbers of this table {#the-numbers}

The table plays one rule set. Its numbers are not rules: they move when the game is balanced, and the game plays
the same when they do. This book draws every number from the rule set and the content, so it moves with them.

| Value | What it decides | This table |
| --- | --- | --- |
| Team size | The creatures each player commands | {rules.teamSize} |
| Energy per round | The Energy every living creature gains at Upkeep | {rules.energyPerRound} |
| Picks per mutation round | The mutations a team may acquire on a mutation round | {rules.evolutionPicksPerOpportunity} |
| First mutation round | The first round that offers mutations | {rules.firstEvolutionRound} |
| Rounds between mutation rounds | The gap from one mutation round to the next | {rules.evolutionInterval} |
| Last round | The round after which the match ends on Health | {rules.roundCap} |
| Critical multiplier | What a critical multiplies the damage and the healing by | {rules.criticalMultiplier} |

A spell prints its critical chance as a percent: Incinerate prints Crit
{(creature.criticalChance + spell:engulfing_flames:v1.criticalChance) * 100}%. Every critical chance in the game is a multiple of 5%.

sources: ADR 0011, ADR 0056, ADR 0100, ADR 0105, ADR 0208, game-rules#implemented
