# 0124. Rank players with Glicko-2, over the results of games between two accounts

Date: 2026-11-03

Status: Accepted. Builds on [0122](0122-a-player-signs-in-with-google-and-owns-their-seats.md).

## Context

ADR 0122 keeps a result for every finished match with an account in it, and marks a match *ranked* when its two
seats are two different accounts. Players asked for a leaderboard. Games here are few and irregular: a handful of
people, some evenings, long gaps. A rating that pretends to know a player after two games, or that forgets nothing
after a month away, would misrank the people who use it most.

## Decision

- **Glicko-2** (Glickman, 2012) rates each player: a rating on the familiar scale, a deviation saying how sure it is,
  and a volatility. Everyone starts at 1500 ± 350; τ is 0.5. It is computed in Application (`Glicko2`,
  `Standings`), pure and tested against the worked example of Glickman's paper.
- **Only ranked results count**: two seats, two accounts. Games against a bot are tallied per bot for the player
  and never rated; a shared device, or a seat without an account, counts for nothing.
- **Each ranked game is its own rating period** for its two players, rated against each other as they stood before
  it, oldest first. A day without a game is an idle period: the deviation grows, the rating stays. So a player who
  stops playing drifts back toward provisional instead of holding a rank they no longer show.
- A player whose deviation is above **110** is *provisional*, listed after established players.
- The ladder is computed from the kept results on each read and held for 30 seconds, served at `GET
  /api/leaderboard` to anybody, and drawn at `/leaderboard`. Players are named by their account's username; a
  deleted account leaves the ladder, though its games still moved the ratings of whoever it played.

## Consequences

- Good: a new player's rating moves quickly and says it is uncertain; an upset against a stronger player is worth
  more; nothing about the formula lives in a page.
- Good: no state but the results: the ladder can be recomputed, and a change to τ or to the threshold applies to
  the whole history.
- Bad: every read of a stale cache lists every result. That is fine for hundreds or thousands of games; a busy
  ladder would keep ratings incrementally instead.
- Neutral: a pilot handing a person's seat to a bot mid-match still counts as that person's game (stage 5 records
  no handover).

## Alternatives considered

- **Elo**: one number, no notion of how sure it is, so a player with two lucky wins outranks a regular.
- **Win rate or points**: rewards playing a lot against weak opponents.
- **Rating periods of a week**: Glickman's recommendation for many games per period; with a few games a week, a
  per-game period moves ratings when people expect them to move.

## Follow-up

- `table/README.md`, `docs/tabletop/accounts-plan.md`, `AGENTS.md` (the `table` command's routes).
