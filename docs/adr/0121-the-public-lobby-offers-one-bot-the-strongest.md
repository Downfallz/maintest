# 0121. Offer one bot in the public lobby: the strongest

Date: 2026-11-01
Status: Accepted

## Context

The player lobby of ADR 0115 lets a visitor open a table against another player, on a shared device, or against
Greedy or Random. Both bots are baselines for the tuner and the journal. Greedy loses to every bot the table
features, and Random plays at random. A person who sits down to play the game alone is better served by the
strongest bot the journal has measured. That bot changes as the content and the searches move: on
2026-11-01 it is the lookahead guessing the other side as search-51.

## Decision

The lobby offers one bot. A visitor who picks it gets the first bot `learning/seatable.json` puts forward. That
file lists the bots in the order the journal measured them, so the lobby follows the next strongest without a
change to the host or the page. A host given no list seats Greedy. The lobby refuses `greedy` and `random` as an
opponent. The operator's admin panel and pilot still seat any bot, Greedy and Random included.

## Consequences

- Good: a visitor plays the best the game has, and a new search reaches the lobby by its line in
  `seatable.json`.
- Bad: the strongest bot can be slower to answer than Greedy, since a lookahead plays the round out before each
  move.
- Neutral: the part of ADR 0115 that named Greedy and Random as the lobby's opponents is replaced by this;
  the rest of it stands.

## Alternatives considered

- Keep Greedy and Random beside the strongest: the owner wants the public tables to offer the best bot only.
- Name the bot in the lobby's code: every new search would be a change to the host.

## Follow-up

- `table/lobby.html` and `table/lobby.js` offer "the strongest bot", and `LobbyApi` seats the first featured
  agent.
- `learning/seatable.json` is now also what the public lobby reads.
