# Glossary (ubiquitous language)

One entry per term. Code must use these exact words. Status: `decided`, `inherited` (from the legacy
prototypes, to be confirmed as it is re-implemented), or `open` (not yet defined).

## Match and players

| Term | Definition | Status |
| --- | --- | --- |
| Match | A complete game between two Players, played as a sequence of Rounds until the Win condition is met. The aggregate root of the Matches context. | decided |
| Player | A participant in a Match. Controls one Team. Occupies a Player slot (`Player1`, `Player2`). | decided |
| Team | The set of Creatures a Player commands during a Match. Defeated when all its Creatures are dead. | decided |
| Rule set | The tunable parameters of a Match: team size, evolution picks per round, energy gain per round, round cap, damage and crit formulas. | decided |
| Win condition | The match ends when a Team is defeated at the end of a round, or when the round cap is reached (ADR 0011). | decided |
| Board | Whether positions on a board matter (range, adjacency) or the game is slot-based only. | open |

## Creatures and content

| Term | Definition | Status |
| --- | --- | --- |
| Creature | A combat unit on a Team, instantiated from a Creature definition, with Health, Energy, Defense, Initiative, Critical chance, known Spells, and active Conditions. | decided |
| Creature definition | Static content describing a kind of Creature (base stats, starting Spells, Talent tree). Loaded from Game resources, never created during play. | decided |
| Stat | A non-negative value object on a Creature: Health, Energy, Defense, Initiative. Critical chance is a probability in [0, 1]. | decided |
| Creature stats | The stat block of a Creature: Health, Energy, Defense, Initiative, Critical chance. A Creature definition carries the base block. | decided |
| Spell stats | The numbers of a Spell: Initiative, energy cost, Critical chance. | decided |
| Spell | An action a Creature can perform in Combat: type, class, initiative, energy cost, critical chance, targeting spec, and effects. | decided |
| Effect | One consequence of a Spell on a target, from a closed taxonomy (ADR 0012): instant `Damage`, `Heal`, `EnergyGain`; lasting `Bleed`, `Stun`, `DefenseBuff`, `InitiativeDebuff` with a Duration and a Stacking policy. | decided |
| Condition | A lasting Effect attached to a Creature (stun, bleed, defense buff) with a Duration and a Stacking policy. Bleeds deal damage at the start of the round; every Condition counts down at Cleanup, and the first countdown after an application does not count. | decided |
| Duration | How long a lasting Effect stays: a number of rounds, or permanent. | decided |
| Stacking policy | What applying a lasting Effect does when the Creature already carries it: `Stack` (add another), `Refresh` (restart the duration), `Ignore`. | decided |
| Data builder | The tool that consolidates the authored content under `data/` into one validated `game.schema.json` with a Content hash (ADR 0009). | decided |
| Talent tree | The tree of Spells a Creature can unlock, with `allOf`/`anyOf` prerequisites per node. | decided |
| Game resources | The static, versioned catalogue of Creature definitions, Spells, and Talent trees. | decided |
| Content hash | The SHA-256 of the consolidated Game resources; stamped on every match and simulation result as the resources version (ADR 0009). | decided |
| Versioned id | A content identifier in the form `kind:name:vN` (`spell:pummel:v1`). Aliases without a version resolve to the latest at build time. | decided |

## Rounds and phases

| Term | Definition | Status |
| --- | --- | --- |
| Round | One cycle of play inside a Match, driven by Phases and Sub-phases (ADR 0010). | decided |
| Phase | One of Start of round, Planning, Combat, End of round. Forward-only within a Round. | decided |
| Sub-phase | A step inside a Phase with its own expected player actions and completion rule. | decided |
| Progression gate | A pure domain service that says whether the current Sub-phase is complete and, if not, what is missing (which Creatures, how many picks). Shared by the engine, the UI, and bots. | decided |
| Planning | The Phase in which Players make Evolution choices, then Speed choices, after which the Combat timeline is built. | decided |
| Evolution | A Planning decision where a Player unlocks a Spell for a Creature from its Talent tree, within the picks allowed by the Rule set. | decided |
| Speed choice | A Planning decision setting a Creature's speed for the Round: `Quick` or `Standard`. | decided |
| Turn cursor | The position in the Combat timeline of the next Intent to reveal (reveal cursor) or the next Combat action to resolve (resolve cursor). | decided |
| Combat timeline | The ordered list of Activation slots for the Round: all Quick slots by Initiative descending, then all Standard slots, ties broken by Player slot then Creature id. | decided |
| Activation slot | A position in the Combat timeline at which one Creature acts. | decided |
| Combat | The Phase in which Creatures act in timeline order: Intent selection, Reveal and target, Action resolution. | decided |
| Intent | A Player's hidden declaration of the Spell a Creature will use in its Activation slot. | decided |
| Reveal and target | The step where the next Intent on the timeline is revealed and its targets chosen, producing a Combat action. | decided |
| Combat action | A revealed Intent bound to its targets. | decided |
| Resolution | The step where a Combat action is computed (targeting check, effects, crit, energy cost) and applied. | decided |
| Fizzle | A Combat action that resolves with no effect and at no cost because its actor cannot act any more, its targeting failed globally, or no target remains; a per-target failure only removes that target. | decided |
| Targeting report | Every targeting failure of a Combat action at once: global ones (count, duplicates, self-only) and per-target ones (unknown, dead, wrong origin). | decided |
| Legal targets | The Creatures a Spell may target right now, with the minimum and maximum count. | decided |
| Outcome | One computed consequence of a Resolution on one target (damage after crit and Defense, heal, energy, a Condition to attach), applied by the execution step. | decided |
| Perspective | The read-only, actor-relative view of a Match (allies, enemies, phase, choices, timeline) handed to rules and projections instead of the aggregate. | decided |
| Snapshot | An immutable copy of a Creature's state (or of a Condition) used by Perspectives and projections. | decided |
| Player slot | The seat a Player occupies in a Match (`Player1`, `Player2`). Creatures and choices are attributed to a slot. | decided |

## Engineering terms

| Term | Definition | Status |
| --- | --- | --- |
| Shared kernel | The dependency-free project holding primitives, identifiers, stats, and shared ports (ADR 0007). | decided |
| Domain error | The stable, coded outcome of a rule violation (`Match.AlreadyStarted`), returned in a `Result`. Invariant violations throw instead. | decided |
