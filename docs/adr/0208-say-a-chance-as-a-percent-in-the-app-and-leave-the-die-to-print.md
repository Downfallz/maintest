# 0208. Say a chance as a percent in the app, and leave the die to print

Date: 2026-11-27

Status: Accepted. Amends what [0154](0154-record-the-critical-roll-as-a-d20-face.md),
[0063](0063-an-initiative-tie-is-rolled-on-a-d20.md) and [0144](0144-read-the-turn-order-on-the-field.md) show,
not what they record; builds on [0100](0100-every-critical-chance-is-a-twentieth.md).
Rulebook: `what-the-app-does`, `the-numbers`, `tempo`, `turn-order`, `tie-order`, `resolving-a-spell`, `damage-bonus`, `the-critical-roll`

## Context

The first outside playtester read `Crit 35% · d20 14+` on a card and a die on the field, and wrote: "The game
shows a critical chance as a percent and also talks to me about a d20. That confuses me"
([first-playtest-plan.md](../tabletop/first-playtest-plan.md)). The d20 comes from the print format: the catalogue
was snapped to twentieths so a card could print a face (ADR 0100), the record kept the face (ADR 0154), and the
field drew the die. The game went back to the app, where nobody rolls anything, and the official rulebook is the
app's edition (ADR 0188). The owner settled it on 2026-11-27: the die leaves the app everywhere.

## Decision

In the app a chance is a percent and a tie between the two teams is settled at random; the die belongs to the
print edition.

- **A chance.** A chip and a card say `Crit 75%`, with the tempo it holds for. No page prints a threshold or a
  face. The host still serves `criticalThreshold` for the print generator and the studio, which is for authors.
- **A critical.** The field draws no die. A cast that crit says `Crit!` beside its caster as it lands; a cast that
  could crit and did not shows nothing there. The journal's line says the chance the cast had, from the card:
  `Crit · 75%` or `No crit · 75%`.
- **A tie.** The order of play, the tie window, the dock's card and the journal say `Tie settled at random`, and
  which team got the earlier place. No face and no reroll is shown.
- **The rulebook.** The app's edition, English and French, says a chance as a percent and a tie as settled at
  random. The frozen print draft (`docs/tabletop/rulebook.md`) keeps its die.
- **Nothing recorded changes.** Every chance stays a twentieth (ADR 0100). `CombatResolution.CriticalRoll` and
  `CombatTimeline.RollOffs` stay as they are, and the engine still draws the roll-off of ADR 0063. A page reads
  that a roll was made (the cast could crit, a tie was drawn for) and never which face it showed.

## Consequences

- Good: one way to say a chance, the one a player of a screen game already reads.
- Good: no rule, draw, board or record changes: a recorded game replays, the benchmark digest is untouched, and a
  print edition can still be cut from the same content.
- Bad: a player can no longer check a tie's draw or a critical's roll on the screen. The record still holds both.
- Bad: `table/battle-layout.js` loses the die's place and `battle/src/dice.ts` goes; putting the die back is a
  revert, not a switch.
- Neutral: the content stays on the d20 grid for a print edition no stage builds. If print is dropped, a later ADR
  may free the chances from the twentieths.

## Alternatives considered

- Keep both, percent and face: what the playtester found confusing.
- Keep the die on the field and drop it from the cards: the field would speak of a d20 no card explains.
- A setting, "show the dice": two wordings to keep true in every page, the rulebook and every test, for one reader.
- Stop recording the face and the roll-offs: a change to the engine's contract and its records for a wording.

## Follow-up

- `table/terminology.test.js` refuses "d20", "dice", "die", "roll" and "roll-off" in the table's pages and the
  battle view's labels; `docs/domain/player-terminology.md` ("Chance and ties") says the words.
- `rules/` rewritten through `/rules`, both editions; `table/rules*.json` rebuilt.
- `battle/README.md`, `table/README.md` and the journeys' briefs say the new words.
