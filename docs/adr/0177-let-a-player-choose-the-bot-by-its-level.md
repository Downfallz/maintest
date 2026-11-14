# 0177. Let a player choose the bot by its level, and show them nothing else of it

Date: 2026-11-14
Status: Accepted. Amends [0121](0121-the-public-lobby-offers-one-bot-the-strongest.md): the lobby offers three levels,
Hard still the strongest bot

## Context

Since ADR 0121 the lobby seats one bot, the strongest the journal has measured. A new player loses to it every time,
and a player who wants an easier game has nowhere to go. Its name ("Lookahead (lookahead-55)", a weights path in its
spec) means nothing to a player and changes with every search. Games of this kind offer a few named difficulties and
keep the engine behind them out of sight.

## Decision

- **Three levels: Easy, Medium, Hard.** `learning/seatable.json` says which bot each seats (`levels`): Easy is Greedy
  playing a random move three times in ten (`explore:0.3`), Medium is Greedy, Hard is the strongest,
  `lookahead-55` today. A stronger bot behind Hard is a change to that file, not to code. Measured on the 200
  benchmark seeds of content f3e8a42d: Easy wins 8% against Medium and every game against Random; Medium wins 31%
  against Hard.
- **A player chooses a level, never a bot.** `POST /api/lobby/tables {"opponent":"bot","level":"easy"}`; Hard when
  none is named, as before; any other word refused (`Lobby.BotLevel`). The create dialog's Bot difficulty offers the
  three, Hard first chosen, and a rematch asks for the same level again.
- **A player is shown the level and nothing else of the bot**: the lobby's cards, the battle view, the finished
  games, the record against bots and the notices say "Hard" (the cards tag it as a bot beside it; a notice says
  "the Hard bot"). A bot no level seats, which only the operator can seat, is "Bot".
- **The operator sees both**: the admin panel's tables, its choosers and a player's sheet name the bot and its level
  ("lookahead-55 · Hard"), and the operator can seat any level's bot.
- **The level is read off the bot, not stored.** A seat and a kept result already name their bot; a level knows the
  bots it seated before (`formerly`), so a game against last month's Hard still says Hard after Hard moves on.

## Consequences

- Good: a beginner has a bot they can beat, and the strongest is still one choice away.
- Good: the bot behind a level moves with the journal and no page changes.
- Bad: a level's bot changed without listing the old one under `formerly` makes the games against it read "Bot".
- Neutral: the rates measure bots against bots; how a person fares against each level is for playtests to say.

## Alternatives considered

- Store the level with the table and with each result: a field in the record, the results and the Table Storage rows
  for a word the bot's spec already gives.
- Show the bot's name with its level: the name is the operator's vocabulary, and it changes with every search.
- Easy as Random: it loses every game to a bot playing a random move three times in ten, and plays nothing like a game.
