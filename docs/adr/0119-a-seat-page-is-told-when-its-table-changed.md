# 0119. A seat's page is told when its table changed

Date: 2026-11-01

Status: Accepted. Amends ADR 0054 (the page polls, and nothing is pushed).

## Context

ADR 0054 chose a poll every 700 ms over long-polling, server-sent events and WebSockets: hotseat had nothing to
push to, two devices needed a one-second poll and no more, and each was one more thing to keep alive across a
screen lock. Two devices are now the ordinary game, over the hosted table (ADR 0080, 0115), and the poll is what
a waiting player's phone spends its battery on: eighty-five requests a minute a seat, nearly all of them
answering that nothing changed, with the other player's action still up to 700 ms away when it comes. The
screen lock is answered by ADR 0118 (a push notice), not by the connection.

## Decision

- The seat route gains `GET /api/seat/{seat}/events`, a stream of server-sent events under the seat's token:
  `ready` with the table's version, `changed` with each new one, a comment every twenty seconds while nothing
  changes. It names no change: the seat route still answers one seat at a time, and the stream only says to
  read it now. It ends when the browser goes, when the table is let go of, or once the match is over.
- A table's version moves when a seat is asked something, when a decision, a concession or a swap is taken,
  when a person reaches or leaves their seat, and when the match ends (`TableChanges`).
- The page opens one stream a seat, with `fetch` so the token travels in a header, and reads its seat on each
  event. A stream that ends is opened again after a moment, a little later each time. The poll stays as the
  fallback: every 700 ms while no stream is open, every five seconds while one is, in case a change was missed.
  A host without the route (an older one) answers 404 and the page polls as before.
- The host writes the stream itself (`StudioResponse.OfStream`), chunked, under the same `HttpListener`.

## Consequences

- Good: a waiting player sees the other's action as it lands, and a phone in front of somebody asks the host a
  dozen times a minute instead of eighty-five.
- Bad: one more connection per seat for the host to hold open, under an ingress that closes a quiet one after
  four minutes; the keep-alive is what keeps it. Sixteen tables of two seats is thirty-two, which is nothing.
- Neutral: hotseat opens two streams on one browser; both say the same thing, and the second read is skipped.
- Neutral: the hidden-page rule of ADR 0118 is unchanged. A phone whose screen went off keeps its stream a
  while and reads what it announces, acknowledges nothing, and is notified.

## Alternatives considered

- WebSockets: two-way, and the page has nothing to say that a POST does not; and a token can travel in a
  WebSocket's address only, which is the thing ADR 0054 keeps out of addresses.
- `EventSource`: the browser's own client, with its reconnection, which cannot send the token in a header.
- Long-polling: a request that waits is a request that times out under the ingress, to be made again; the
  stream is the same connection kept.
- Stream the seat's view itself: two seats' views are two streams of a boundary the host keeps per payload;
  announcing a change and answering the seat route keeps that boundary where it is.
