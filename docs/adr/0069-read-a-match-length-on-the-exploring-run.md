# 0069. Read a match's length on the exploring run, not on the greedy mirror

Date: 2026-09-23

Status: Accepted

## Context

[ADR 0068](0068-a-match-lasts-ten-to-fifteen-rounds.md) set the length the game is designed for, ten to fifteen
rounds, and held the greedy mirror to it. At 30 base health the mirror reads 10.05 on the benchmark seeds and
9.97 on 200 seeds from 995317: on the edge of the band, where a move that shortens the mirror by a tenth of a
round is penalised whatever else it does. The first tuning pass with the package knobs ended at 10.005 on its own
seeds and 9.885 on unseen ones (journal, 2026-09-23).

The mirror is the wrong run to hold to it. Two greedy agents play the narrowest game the content allows: each
takes the one spell its scorer ranks first, so they trade the same damage every round and the match ends as soon
as that race allows. A person does not play that way, and neither does the exploring run
([ADR 0014](0014-exploration-in-recorded-datasets.md)), whose two sides play greedily four decisions in five and draw the
fifth: it reads 10.03 rounds on the benchmark seeds and 10.83 on the unseen ones, and it is already the run the
objective reads the seat ([ADR 0062](0062-read-the-seat-on-the-exploring-run.md)) and every package reading on.
The owner accepted that the greedy mirror plays badly and short (2026-09-23).

## Decision

**The objective's length band, ten to fifteen rounds, is read on the exploring run** (`variety.averageRounds`),
where it was read on the greedy mirror (`mirror.averageRounds`). The band, its scale and its weight do not move.
The mirror keeps the targets that are about the content rather than the players: draws, the round cap and
fizzles. The best exploiter's floor of ten rounds is unchanged, and now reads against the exploring run's band.

## Consequences

- Good: the length target reads a game played with choices in it, and the mirror's short race no longer holds
  every tuning move to the edge of the band.
- Good: every length-related reading the tuner makes now comes from one run, the same one as the package terms.
- Neutral: the content as it stands scores the same, 8.35 on the benchmark seeds and 10.76 on the unseen ones:
  both runs sit inside the band there. The objective is nonetheless a different one, the seventh change the
  `score` note records, and scores are comparable only under the same one.
- Bad: the exploring run carries a fifth of random decisions, so a move could lengthen it by making random play
  worse at finishing a match rather than by making the game longer. The exploit clock ([ADR 0053](0053-score-the-exploit-term-on-the-clock-not-on-the-win-rate.md)) reads the other
  end, how fast a player who only wants to win closes a match out.

## Alternatives considered

- **Raise base health again**, to 32 or 35, so the mirror sits inside the band with room. It answers the tuner's
  constraint by moving the game rather than the reading, and every tabletop count follows base health.
- **Widen the mirror's band down to 9.** It keeps a reading the owner no longer wants to steer by.
- **Read length on both runs.** Two targets for one design length, one of them on a run the owner accepts is
  short, would keep the constraint this removes.

## Follow-up

- `data/balance/knobs.json`: the target moves from `mirror` to `variety`, its `why`, the exploit floor's `why`, and
  the `score` note.
- ADR 0068 is marked amended by this one; the journal.
