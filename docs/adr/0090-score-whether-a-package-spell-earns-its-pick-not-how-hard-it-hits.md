# 0090. Score whether a package's spell earns its pick, not how hard it hits

Date: 2026-10-02
Status: Accepted

## Context

`tierDamageSpread` reads, in each package, how many times harder its best attack hits one target than its
worst, and scores the worst package against a band of 2 (ADR 0043, ADR 0058). After ADR 0089 it was the
largest term of the objective on content `f1bc21d0`: 12.18 of 18.81 on the benchmark seeds, 6.19 of 10.03
on the confirmation seeds, 8.72 of 13.19 on the hold-out seeds. Three packages were over the band, and each
was split that way on purpose:

- Assassin, 3.2 to 3.7: Momentum (about 1.9 a target) is free and gives back 2 energy; Shadowstep (about
  6.5) costs 3.
- Soulreaver, 2.3 to 2.5: Soul Devourer (about 4.9) drains energy and heals its caster; Hateful Sacrifice
  (about 12.1) costs its caster health.
- Prowler, 2.3: Throwing Star (about 2.9 a target) reaches two enemies, so about 5.9 a cast; Poison Slash
  (about 6.8) reaches one. The same price buys about the same damage a cast.

A ratio of raw hits cannot see a spell's cost, its reach or its riders, so this term asked a tuning pass to
flatten spells that differ by design. The question it stood for, whether every spell of a package earns its
pick, has two readings that look at what happens in play: `tierUsageShare` asks whether casts go to every
spell, and `tierWinSpread` asks whether the sides that cast each one win comparably. `tierWinSpread` reads
about 0.07 against its band of 0.15.

## Decision

We will drop `tierDamageSpread` from the balance objective's targets. The metric is still computed and
reported with the others, so a run can still show it, but it no longer scores a candidate. Whether a
package's spells earn their pick is read by `tierUsageShare` and `tierWinSpread` only.

## Consequences

- Good: the objective on `f1bc21d0` under ADR 0089 reads about 6.63, 3.84 and 4.47 on the benchmark,
  confirmation and hold-out seeds, against 18.81, 10.03 and 13.19. That is the same runs with the one term
  removed. What remains is `tierUsageShare` (0.87 to 0.89 against 0.8), a little of `spellUsageShare`, and
  `player1WinShare` where its sample is small.
- Good: a tuning pass no longer moves Momentum, Throwing Star or Hateful Sacrifice toward their packagemates
  against their keeps.
- Bad: a package whose attack is a real dud, weak on every axis and not just in raw hits, is now caught only
  through play: by `tierUsageShare` when nobody casts it, and by `tierWinSpread` when casting it loses. Both
  are read on samples, so a rarely bought package can hide one.
- Neutral: every objective score before this one is on a different set of targets, the way ADR 0062 and ADR
  0064 changed it. A score is compared only with scores from the same set.

## Alternatives considered

- Hand-tune the three packages into the band (Momentum to 3, Shadowstep's critical to 0.3, Soul Devourer to
  7, Hateful Sacrifice's critical to 0.4, Poison Slash to 2). It stays within the bounds, but it bends
  five spells toward a number that does not price what they are for. Measured, it still read 2.66 to 2.77,
  outside the band, and `tierUsageShare` rose to 0.929 on the benchmark seeds.
- Redefine the term as damage a cast per energy: it would see reach and cost, but still not the energy a
  spell gives back, the energy it drains, the initiative it buys, or the health it costs its caster.
- Keep it at a lower weight: a term that reads a designed split as a fault is wrong at every weight.

## Follow-up

`data/balance/knobs.json` (the target and the `score` note), `data/balance/README.md`, a journal entry.
ADR 0043 and ADR 0058 still describe how the metric is read; this ADR only takes it out of the score.
