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
| Match outcome | How a Match ended: the winning Player slot, or a draw, and the reason (`Elimination`, `RoundCap`). | decided |
| Match state | Where a Match is in its life: `WaitingForPlayers`, `InProgress`, `Ended`. | decided |
| Roster | The Creature definitions a Player brings to a Match; its size is the Rule set's team size. | decided |
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
| Phase driver | The loop inside the Match that runs each automatic step or asks the Progression gate, advances the Sub-phase, and raises an event, until the Round waits on a Player or the Match ends. | decided |
| Planning | The Phase in which Players make Evolution choices, then Speed choices, after which the Combat timeline is built. | decided |
| Evolution | A Planning decision where a Player unlocks a Spell for a Creature from its Talent tree, within the picks allowed by the Rule set. | decided |
| Evolution pass | A Planning decision where a Player gives up their remaining Evolution picks for the Round. | decided |
| Speed choice | A Planning decision setting a Creature's speed for the Round: `Quick` or `Standard`. | decided |
| Turn cursor | The position in the Combat timeline of the next Intent to reveal (reveal cursor) or the next Combat action to resolve (resolve cursor). | decided |
| Combat timeline | The ordered list of Activation slots for the Round: all Quick slots by Initiative descending, then all Standard slots, ties broken by Player slot then Creature id. | decided |
| Activation slot | A position in the Combat timeline at which one Creature acts. | decided |
| Combat | The Phase in which Creatures act in timeline order: Intent selection, Reveal and target, Action resolution. | decided |
| Upkeep | The automatic steps of a Round with no player decision: energy gain and Bleed ticks at the start, Condition countdown at Cleanup. | decided |
| Bleed tick | The damage a Creature takes from its bleed Conditions at the start of a Round; it ignores Defense. | decided |
| Intent | A Player's hidden declaration of the Spell a Creature will use in its Activation slot. | decided |
| Reveal and target | The step where the next Intent on the timeline is revealed and its targets chosen, producing a Combat action. | decided |
| Combat action | A revealed Intent bound to its targets. | decided |
| Combat step | The result of resolving one Combat action through the Match: the Resolution, and whether it completed the Round or the Match. | decided |
| Resolution | The step where a Combat action is computed (targeting check, effects, crit, energy cost) and applied. | decided |
| Fizzle | A Combat action that resolves with no effect and at no cost because its actor cannot act any more, its targeting failed globally, or no target remains; a per-target failure only removes that target. | decided |
| Targeting report | Every targeting failure of a Combat action at once: global ones (count, duplicates, self-only) and per-target ones (unknown, dead, wrong origin). | decided |
| Legal targets | The Creatures a Spell may target right now, with the minimum and maximum count. | decided |
| Outcome | One computed consequence of a Resolution on one target (damage after crit and Defense, heal, energy, a Condition to attach), applied by the execution step. | decided |
| Perspective | The read-only, actor-relative view of a Match (allies, enemies, phase, choices, timeline) handed to rules and projections instead of the aggregate. | decided |
| Snapshot | An immutable copy of a Creature's state (or of a Condition) used by Perspectives and projections. | decided |
| Player slot | The seat a Player occupies in a Match (`Player1`, `Player2`). Creatures and choices are attributed to a slot. | decided |

## Learning

| Term | Definition | Status |
| --- | --- | --- |
| Run stamp | The identity of a simulation, evaluation, or training run: Engine version, Content hash, Rule set, feature schema version, Player agent kinds and versions, base seed. Present on every artifact. | decided |
| Engine version | The git commit the engine was built from, plus a dirty flag, injected into the assemblies at build. | decided |
| Observation | The numeric view of a Match for one Player at one decision: the feature vector built from their Player board state. | decided |
| Feature schema | The published, immutable layout of an Observation for one content and Rule set: which feature sits at which index, under a version such as `features:v1`; its id adds a fingerprint of the concrete layout. | decided |
| Board slot | A Creature's position in an Observation: its index among the Player's own Creatures, or the Team size plus its index among the enemies. Action keys name the acting Creature by it. | decided |
| Action | One decision as the engine sees it: an Evolution choice or pass, a Speed choice, an Intent, a target set; keyed with the acting Creature's team slot. | decided |
| Step | One (Observation, options, Action) taken by one Player in one Match. | decided |
| Episode | One Match from a Player's point of view: its Steps and its final Return. | decided |
| Return | The reward of an Episode: win, loss, or draw, shaped by the remaining-Health margin. | decided |
| Policy | A function from Observation and options to an Action. Random agent, greedy agent, and learned agents are policies. | decided |
| Dataset | Recorded Steps and Episodes, in JSON lines, with a manifest carrying the Run stamp. | decided |
| Match trace | The full record of one Match: every domain event with both Player board states after the command that raised it, enough to replay it in the Viewer without the engine. | decided |
| Run manifest | The `manifest.json` of a recorded run: its Run stamp, the Feature schema id and feature names, and the counts of Matches, Steps, and Episodes. | decided |
| Evaluation | A batch of mirrored Matches between two Player agents on the Benchmark seeds, reported as win rates with confidence intervals over seed pairs, and a Run stamp. | decided |
| Agent spec | Which Player agent to seat: an agent kind and, for kinds that read a file, its path; written `random` or `kind:path`. | decided |
| Seed pair | One Benchmark seed played twice with the two Player agents swapped; the unit an Evaluation's intervals are computed over. | decided |
| Benchmark seeds | The fixed seed set every Evaluation uses, so two content versions, two engine versions, or two agents compare on the same Matches. | decided |
| Benchmark digest | The committed outcomes of the Benchmark seeds played by the deterministic baseline agents, per Content hash; CI verifies it. | decided |
| Viewer | The static HTML page that renders Match traces, batches, Evaluations, training runs, and comparisons. | decided |
| Training run | The record of one training of a Policy on the Python side, one line per iteration (loss, evaluation win rate), with its Run stamp. | decided |
| Policy file | A trained Policy as the engine reads it (`policy.json`): the Run stamp of its data, the Feature schema, one weight row and one bias per action key, a fallback score; the best-scoring candidate wins. | decided |
| Weight search | Tuning the Scoring weights of the Heuristic agent by evaluating candidate weights files with the engine on the Benchmark seeds (cross-entropy method). | decided |
| Behaviour cloning | Training a Policy to reproduce the actions of a recorded Dataset: a classifier from Observation to action key. | decided |
| Value regression | Training a Policy to predict the Return of an Episode from an Observation and an action; the agent takes the option with the highest predicted Return. | decided |

## Engineering terms

| Term | Definition | Status |
| --- | --- | --- |
| Shared kernel | The dependency-free project holding primitives, identifiers, stats, and shared ports (ADR 0007). | decided |
| Command | A request to change a Match (`JoinMatch`, `SubmitIntent`, ...), one per aggregate method, handled by one `ICommandHandler` (ADR 0008). | decided |
| Query | A read-only request (`GetBoardStateForPlayer`, `GetPlayerOptions`) handled by one `IQueryHandler`. | decided |
| Player board state | The projection of a Match for one Player: both teams as Snapshots, the round position, their own hidden choices, the public timeline and revealed actions. | decided |
| Player options | The projection of the one decision a Player can make in the current Sub-phase, with everything they may pick from; built from the Progression gates and the targeting rules. | decided |
| Player agent | Something that decides for a Player from their Player options: a bot, a script, a UI adapter. | decided |
| Random agent | The Player agent that picks uniformly among the options; the floor every other agent is measured against. | decided |
| Greedy agent | The deterministic Player agent with a one-step lookahead on the domain rules and the built-in Scoring weights; the baseline of the Benchmark digest. | decided |
| Heuristic agent | The Greedy agent's lookahead with Scoring weights read from a file, so the weights can be tuned by search. | decided |
| Scoring weights | What the lookahead values in an action's expected outcome: damage, kill, heal, stun, bleed, buff, energy kept, risk. | decided |
| Match driver | The application service that plays a started Match to its outcome through the commands, asking each Player agent in turn. | decided |
| Simulation scenario | What a batch plays: Rule set, both rosters and Player agents, match count, base seed; match `i` uses seed `base + i`. | decided |
| Match result | One simulated Match: seed, Match outcome, rounds played, remaining Health per Team. | decided |
| Simulation summary | The balance numbers of a batch: win rates by Player slot, draw rate, rounds, remaining Health. | decided |
| Domain error | The stable, coded outcome of a rule violation (`Match.AlreadyStarted`), returned in a `Result`. Invariant violations throw instead. | decided |
