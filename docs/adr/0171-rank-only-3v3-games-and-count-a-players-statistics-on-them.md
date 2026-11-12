# 0171. Rank only 3v3 games, and count a player's statistics on them alone

Date: 2026-11-12

Status: Accepted. Amends [0155](0155-let-a-game-between-players-be-unranked.md) and
[0168](0168-keep-match-results-in-azure-table-storage.md)

## Context

A game between two players is ranked unless opened unranked (ADR 0155), in any of the three formats. A ladder
rating that mixes 1v1, 2v2 and 3v3 measures three games as one. A player's statistics (ADR 0168) count every game:
against bots, unranked, on a shared device, in every format. A record against the bot says little about how a
player plays, and the statistics needed a table by format and a table by creature to be read at all.

## Decision

We will rank only 3v3 games. A ranked game between two signed-in players is 3v3. A request that names another
format is refused (`Lobby.RankedFormat`), and one that names none is played 3v3. An unranked game, a game against a
bot and a game on a shared device may be 1v1, 2v2 or 3v3. A game on a shared device is opened
unranked, so another account taking a seat its player left never makes it ranked. The time to answer is chosen the same way, ranked or not.

A player's statistics count their ranked 3v3 games alone: the record, the streaks, the average length, and the
mutations and spells they played. The tables by format and by creature are removed. Every result is still kept
as it was, and the finished games, the recent opponents and the ladder read it as before.

The create dialog says so: a PvP tab, first, plays a player, and a Solo tab beside it plays a bot, with its difficulty and format.
While Ranked is on, the format is 3v3 and cannot be changed.

## Consequences

- Good: one rating for one game, and statistics that describe how a player plays against people.
- Good: nothing is stored differently. Whether a result counts is read off what it already keeps: whether it is
  ranked, and its format.
- Bad: ranked games played in 1v1 or 2v2 before this ADR stay on the ladder and leave the statistics.
- Neutral: the bot difficulty offers one bot for now: the strongest, as before.

## Alternatives considered

- Keep every format ranked, with a rating per format: three ladders for a small player base.
- Count every game in the statistics, ranked games apart: the bot's games would still dominate the creature and
  format tables, which is why they were hard to read.

## Follow-up

- `LobbyApi` (`Lobby.RankedFormat`), `PlayerStats.Counts`, `StatsApi`, the stats page, the lobby's create dialog.
- `AGENTS.md`, `docs/domain/glossary.md`, `docs/adr/README.md`.
