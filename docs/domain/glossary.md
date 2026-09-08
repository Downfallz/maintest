# Glossary (ubiquitous language)

One entry per term. Code must use these exact words. Status: `decided`, `inherited` (from the legacy
prototypes, to be confirmed as it is re-implemented), or `open` (not yet defined).

| Term | Definition | Status |
| --- | --- | --- |
| Match | A complete game between two Players, played as a sequence of Rounds until a win condition is met. The aggregate root of the Matches context. | inherited |
| Player | A participant in a Match. Controls one Team. Has a Player slot (Player1, Player2). | inherited |
| Team | The set of Creatures a Player commands during a Match. | inherited |
| Creature | A combat unit on a Team, instantiated from a Creature definition, with health, energy, Spells, Talents, and active Conditions. | inherited |
| Creature definition | Static data describing a kind of Creature (base stats, available Spells, Talent tree). Loaded from game resources, not created during play. | inherited |
| Round | One cycle of play inside a Match, made of Phases: Start of round, Planning, Combat, End of round. | inherited |
| Phase | A stage of a Round with its own allowed Player actions and completion condition. | inherited |
| Planning | The Phase in which Players make Evolution choices and Speed choices before Combat. | inherited |
| Evolution | A Planning decision where a Player unlocks a Spell or Talent for a Creature. | inherited |
| Speed choice | A Planning decision setting a Creature's initiative for the Round; determines the Combat timeline. | inherited |
| Combat timeline | The ordered Activation slots for the Round, derived from Speed choices. | inherited |
| Activation slot | A position in the Combat timeline at which one Creature acts. | inherited |
| Combat | The Phase in which Creatures act in timeline order: Intent, Reveal and target, Resolution. | inherited |
| Intent | A Player's hidden declaration of the Spell a Creature will use in its Activation slot. | inherited |
| Reveal and target | The step where Intents are revealed and legal targets are chosen. | inherited |
| Resolution | The step where a Combat action is computed (damage, crits, Conditions) and applied. | inherited |
| Spell | An action a Creature can perform in Combat, with a cost and effects (instant or Condition-based). | inherited |
| Talent | A passive modifier unlocked through Evolution that alters a Creature's stats or Spells. | inherited |
| Condition | A lasting effect on a Creature (stun, shield, poison, ...) with a duration and resolution rule. | inherited |
| Rule set | The tunable parameters of a Match (team size, round limit, damage formulas). | inherited |
| Game resources | The static catalogue of Creature definitions, Spells, Talents, and Conditions. | inherited |
| Win condition | The rule that ends a Match. | open |
| Board | Whether positions on a board matter (range, adjacency) or the game is slot-based only. | open |
