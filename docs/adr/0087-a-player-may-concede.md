# 0087. A player may concede

Date: 2026-09-30

Status: Accepted. Amends [0011](0011-win-condition-and-round-cap.md)

## Context

A match ends by elimination or at the round cap (ADR 0011) and no other way. At the table a player who is
plainly beaten, or who has to leave, has no move: the host keeps asking, the session stays open until the
registry lets an idle table go, and the recording ends without an outcome. A person's seat blocks the driver
on a question (`HumanSeat`), so ending the match from outside is not only a rule but a mechanism.

## Decision

We will let a player concede at any point of a match in progress. `Match.Concede(slot)` ends the match on
the spot with the other player as the winner and `MatchEndReason.Concession` on the `MatchEnded` event; the
round is left where it was, not played out, and there is no cleanup. Nothing about the board decides it: a
concession is the player's act, legal whatever the round waits on and refused only when the match is not in
progress.

The table host exposes it as `POST /api/seat/<seat>/concede`, on the seat's own token: in hotseat that is
whoever holds the device. The command runs behind the driver's lock; every seat a person holds is then
released, because the driver is blocked inside one of them, and the driver reads a refusal with
`Match.NotInProgress` as the end rather than as an agent's bug. The page offers it as a dock tool in two taps,
and the end screen names the winner and the reason. A session records it as a `Concession` note.

## Consequences

- Good: a beaten player can end a session cleanly, with an outcome the recording and the viewer can read.
- Good: the driver stays a loop over questions; the one thing added to it is a refusal it no longer throws on.
- Bad: a match can now end with a winner the board does not explain; anything that infers the reason from
  health totals has to read the outcome instead. Datasets carry the reason already (`BatchResultCsv`).
- Neutral: bots never concede. The evaluation and the learning loop are unchanged, and `MatchEndReason` is
  one value longer.
- Neutral: the practice table keeps its restart instead; its scenarios are exercises, not matches.

## Alternatives considered

- **Cancel the driver's wait**, as stopping a table does. It ends the driver without an outcome, which is the
  abandoned session this is meant to replace.
- **A decision kind the seat answers with.** Every agent method returns a typed value, so a "no decision"
  would have to be smuggled through each of them; ending the match first and letting the refusal say so keeps
  the agent contract as it is.
- **Only from the pilot's page.** The player who wants to stop is usually the one at the seat, and the
  operator is often one of the two players (stage 6).

## Follow-up

- `docs/domain/game-rules.md` and the glossary's Match outcome: the third reason.
- `table/README.md`: the Match tool and the end screen; `docs/tabletop/playtest-app.md` 5.3: the note kind.
- The tabletop rulebook may add a concession line when its next pass reads the outcomes.
