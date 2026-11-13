# 0173. Let players react with a fixed set of messages, and notify nobody of one

Date: 2026-11-13
Status: Accepted

## Context

Two people play a match against each other on two devices, often over days (ADR 0169), and the battle view gives
them no way to say anything to each other: no "good luck", no "well played". Free chat would need moderation,
reporting and blocking, and it lets one player make the other read anything. Games of this kind (card and tactics
games on phones) settle it with a short set of predefined messages, rate-limited, which the receiver can mute. A
match played over days also means the other player is usually not looking when a reaction is sent.

## Decision

- **A fixed set, never free text.** Eight reactions: Hello, Good luck, Well played, Nice move, Oops, Thinking, Thanks,
  Good game. The host knows them by name (`TableReactions.Catalogue`) and refuses any other; the page holds their
  words and glyphs (`table/battle-reactions.js`), so changing a label needs no deploy of the host.
- **Between two people only.** A seat reacts once the match has begun, while both seats are held by people, and
  not when one account holds both (one person on a shared device); a page holding both seats offers none either.
  Against a bot, in a replay or in practice there is no one to read a reaction.
- **One every four seconds a seat**, refused with 429 and the time to wait (`Reaction.TooSoon`).
  `POST /api/seat/<seat>/reaction {"reaction":"well-played"}` under the seat's token; it moves the table, so both
  pages read it on the change they already hear (ADR 0164, ADR 0165).
- **Kept with the table, the last eight.** A recorded table keeps them in `reactions.json` beside its record,
  written only over the version read, so a deploy keeps them and two processes cannot lose one. Every seat view
  carries them as `reactions: { available, cooldownMs, recent }`. They are not events of the match: not in the
  feed, the trace, the journal, the replay or the training data.
- **Shown once, newest first.** The page shows the other player's newest reaction it has not shown as a bubble by
  their side for a few seconds, with how long ago it was sent when that was more than a minute; this browser
  remembers the last one it showed per game. The bubble is a status the screen reader announces.
- **Muted by the receiver.** "Opponent reactions" is one more game setting (ADR 0140): kept with the account, on
  by default, and overridden for one game from the reaction set. A muted reaction is counted as shown. The sender
  is not told.
- **No notification and no bell entry.** A reaction sends no Web Push and counts nothing in the bell. The game's
  own notices already bring a player back ("your turn", the result), and the reaction waits for them there.

## Consequences

- Good: players can be courteous to each other without anything to moderate, and a player away for a day still
  sees the "good luck" they were sent.
- Good: a muted or hidden reaction costs the receiver nothing: no sound, no vibration, no badge.
- Bad: one more small blob write per reaction, and one read per table a process picks up or is told moved.
- Bad: a process that holds a table reads its reactions again only when told the table moved, so with more than one
  API replica a page reading from another replica can see a reaction only at its next change: without a relay
  because its stream is told nothing, and with one because the relay tells the page before it tells the other
  replicas. The API runs one replica today (ADR 0165); raising it means reading reactions by their tag on a view.
- Neutral: the set is English, like the rest of the page.

## Alternatives considered

- Free chat: moderation, reporting and blocking for a game played between friends and strangers alike, for little
  more than the set says.
- Notify each reaction by Web Push, or count it in the bell: a reaction is the least urgent thing a player can be
  sent, and a notice for each would teach players to turn notices off, losing "your turn" with them.
- Ephemeral reactions held in memory only: lost on every deploy, and in a game played over days the other player
  would almost never see one.
- Reactions as events of the match: they would reach the replays and the training data, and change no rule.

## Follow-up

- `AGENTS.md` (the route, the setting), `docs/domain/glossary.md` (Reaction), `table/README.md`, `battle/README.md`.
