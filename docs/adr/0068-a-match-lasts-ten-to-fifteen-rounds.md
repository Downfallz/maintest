# 0068. A match lasts ten to fifteen rounds

Date: 2026-09-23

Status: Accepted, amended by [0069](0069-read-a-match-length-on-the-exploring-run.md)

## Context

The game is designed for matches of ten to fifteen rounds (owner, 2026-09-23). Under
[ADR 0066](0066-a-creature-buys-one-package-an-opportunity.md) the greedy mirror lasts 7.8 rounds and the
exploring run 7.2, and a family's top package arrives at round 5 at the earliest, so four spells go uncast on the
exploring run. The objective did not say so: its band for the mirror's length was 8 to 16 rounds, which passes a
match that ends a round after its first level-3 purchase. With one creature definition and every spell unchanged,
base health is the lever that moves length without moving anything the tuner reads as balance. On the benchmark
seeds, content otherwise at `4d7a841c`:

| base health | mirror rounds | exploring rounds | best exploiter rounds | spells never cast |
| --- | --- | --- | --- | --- |
| 20 | 7.8 | 7.2 | 9.6 | 4 |
| 25 | 9.0 | 9.3 | 8.6 | 1 |
| 30 | 10.1 | 10.0 | 14.6 | 2 |
| 35 | 12.1 | 11.9 | 19.5 | 1 |
| 40 | 14.0 | 13.4 | 17.7 | 0 |

## Decision

**A creature has 30 base health**, where it had 20, and the objective holds the mirror to **10 to 15 rounds**
where it held it to 8 to 16, with the exploiter's floor raised from 8 to 10 to match. The owner chose 30, the
lowest value that reaches the band, which leaves the tuner the rest of the band to move in.

## Consequences

- Good: matches last the length the game is designed for, and the objective now says what that length is.
- Good: longer matches give the deepest packages time to be played: the exploring run casts every spell but two.
- Bad: every measurement taken before this is incomparable once more, for two reasons at once: the content hash
  moves, and the objective's bands move. The benchmark digest is written for the new content.
- Bad: 10.05 rounds sits at the bottom of the band, so a tuning move that shortens matches is penalised at once.
  That is the intent -- the tuner should not buy variety with length -- but it makes the band a live constraint
  rather than a formality.
- Neutral: the tabletop's Health rail is printed from `baseHealth` and grows from 0-20 to 0-30.

## Alternatives considered

- **35 base health**, the middle of the band at 12.1 rounds. It leaves less room above and puts the best
  exploiter at 19.5 rounds; the owner preferred the lower value.
- **Make base health a balance knob** and let the tuner pick it. It needs creature knobs, which the tuner does not
  have, and the value is one number the owner can choose directly.
- **Change the cadence instead**, picks every round. It speeds up the climb without making matches longer, which
  is the thing the target asks for.

## Follow-up

- `data/Creatures/main.v1.json`, `data/balance/knobs.json` (the two bands, their `why`, the `score` note).
- The benchmark digest for the new content, the tabletop Health rail and examples, and the journal.
