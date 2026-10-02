# 0089. Pick packages face down and reveal them together

Date: 2026-10-02
Status: Accepted

## Context

A purchase was public the moment it was made, and the driver asks Player 1, then Player 2, once per pass.
So in every Evolution sub-phase Player 2 picked knowing Player 1's last pick, and had the last word. While
the agents priced initiative by the point, the information was worth nothing to them. Since ADR 0088 they
count the enemies a purchase moves a creature past, and the second picker uses what it sees. Round 1 is
where this matters: every creature starts at the same initiative, Player 1 buys Occultist (+2), and Player 2
answers with Prowler (+3).

On 800 matches of the exploring self-play (the benchmark, confirmation and hold-out seeds), Player 1 won
0.501 under the old reading and 0.422 under the new one. Asking Player 2 first in round 1 alone turned it
to 0.610. Asking Player 2 first in round 3 alone, or for speed, ties or intents, moved nothing. A tuning
pass cannot fix this: tune 21 gained nothing on unseen seeds, and `player1WinShare` read 0.36 there
(journal, 2026-10-02).

## Decision

We will take Evolution picks face down and buy them together. A pick is recorded when it is made and
changes nothing on the board. Neither the pick nor a pass is shown to the other player. When neither
player has a pick left, every package picked that round is bought at once, Player 1's picks and then
Player 2's, and `PurchasesRevealed` names them all. The picks cannot depend on each other: a creature buys
at most one package an opportunity (ADR 0066), and its prerequisites are its own. So the order of the
purchases changes nothing. The validation does not change. At the table, each player lays the picked
package cards face down and both turn them over together, the way Speed cards are.

## Consequences

- Good: on the same 800 matches Player 1 wins 0.512 (± 0.035), against 0.422.
- Good: Evolution joins Speed, Tie order and Intent as decisions made face down at the same time. "You
  commit before you see" now holds for every planning decision.
- Bad: a player's own second pick is made against a board that does not show the first one yet. The
  player's picks are listed on their board (`PlayerBoardState.EvolutionChoices`). A bot never needed the
  first pick, because it cannot change what the second may buy.
- Bad: a pass is hidden too. Who is still choosing is no longer visible, only that the sub-phase has not
  ended.
- Neutral: the benchmark digest changes. A recorded trace gains a `PurchasesRevealed` entry, and the
  creatures' tiers and initiative change at the reveal rather than at each pick. The feature schema does
  not change.

## Alternatives considered

- Roll a d20 for who picks first, as for ties (ADR 0063): the seats even out over many matches, but the
  second picker keeps an advantage worth about 0.6 in round 1, and the die hands it out.
- Alternate who picks first at each opportunity: round 1 holds the whole effect, so Player 1 still picks
  first where it matters. Measured, 0.411.
- Price initiative by the point again: the board-reading pricing of ADR 0088 is right. A human would use
  the information the same way the bots now do.

## Follow-up

`Match` (`SubmitEvolutionChoice`, `RevealPurchases`), the `PurchasesRevealed` event, `SeatVisibility`, the
console log, the viewer, `docs/domain/game-rules.md`, the rulebook and the player aid, the translation
audit, the benchmark digest, a journal entry.
