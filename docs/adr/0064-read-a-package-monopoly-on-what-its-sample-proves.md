# 0064. Read a package's monopoly on what its sample proves, and not against an even split

Date: 2026-09-23

Status: Accepted

## Context

`tierUsageShare` asks whether every spell a package sells earns part of the pick that bought it
([ADR 0058](0058-a-tier-is-the-package-the-balance-objective-reads.md)). It read the raw share of a package's
landed casts that its most-cast spell takes, worst package, against a band of at most 0.5 at scale 0.1 and
weight 2. Since ADR 0062 and ADR 0063 took the seat out of the objective, it is the largest term left: 37.6 of
53.2 on the shipped content. Two measurements say that most of that number is not about the content.

- **The band is the floor.** Twelve packages teach more than one spell, and all twelve teach exactly two. The
  lowest share a pair can read is 0.5, a perfect split. A band of at most 0.5 asked all eleven packages that
  were cast to split perfectly at once.
- **The worst of eleven raw shares is mostly noise.** Several packages are bought rarely on the exploring run:
  `harbinger`, `ravager` and `dreadnought` land 15 or 16 casts each. Drawn 2000 times with every pair truly
  splitting 50/50 at the observed sample sizes, the worst raw share reads **0.667** at the median and 0.750 at
  the 90th percentile. That catalogue pays 5.6 points that no move can remove.

## Decision

`tierUsageShare` reads the **lower bound of the 95 % Wilson interval** of each package's top share, and still
takes the worst package. With the same draw of truly even pairs it reads 0.497 at the median and 0.548 at the
90th percentile. A handful of casts proves little and is read that way. A monopoly on hundreds of casts is
read close to its real share: `tier:soulreaver:v1` casts one of its spells 267 times out of 286, which reads
0.934 raw and 0.899 bounded.

The band becomes **at most 0.8, at scale 0.05**, with the weight still 2. The band is not an even split.
Spells sold together must differ in play ([ADR 0060](0060-spells-sold-together-must-differ-in-play.md)), and
a heal sold beside an attack is not cast half the time. What the band refuses is the spell that came along for
nothing: at 0.8, the other spell takes at least one cast in five, as far as the sample shows. The scale is
halved so that a real monopoly still costs something. A package bounded at 0.9 pays 8 points, and one at 1.0
pays 32.

## Consequences

- Good: the term measures what it was built for. Every package bounded above 0.8 is a real monopoly on
  hundreds of casts: `soulreaver` at 0.899 (267 of 286), `deathstalker` at 0.861 (109 of 118) and `prowler` at
  0.840 (1760 of 2057). The next is `warmonger` at 0.762 on 61 casts, inside the band.
- Good: the tuner can reach the band. Before, a perfect catalogue still read far outside it.
- Bad: every score before this change is incomparable with every score after it, the fourth time the `score`
  note in `knobs.json` records that.
- Bad: the bound needs sample. A package the exploring run rarely buys can hide a monopoly until it is bought
  more; 16 casts split 16 to 0 read only 0.806. `spellsNeverCast` and `spellsBarelyCast` still see a spell
  that nobody casts at all.
- Good: on the shipped content, read from one run both ways, the objective goes from **53.18 to 23.36**:
  `tierUsageShare` costs 7.77 instead of 37.60, all of it `soulreaver`.
- Open: `tierWinSpread` is now the largest term (13.03). It is a worst-of-eleven reading built on the same
  small samples, and its noise floor has not been measured.
- Neutral: 0.8 and 0.05 are a judgement, set in `knobs.json` where a tuning author reads it. The measurement
  behind them is the noise floor above, and it does not change if the band does.

## Alternatives considered

- **Keep the raw share and move the band.** At 0.8 with the raw share, `harbinger` at 12 of 16 (0.750) sits a
  sample away from the edge, and the worst of eleven noisy shares still drifts with how often each package is
  bought, not with the content.
- **Drop packages below a minimum number of casts.** A hard cut discards the evidence a small sample does
  carry, and moves the reading across the cut as a package goes from 29 casts to 31. The bound falls smoothly.
- **Normalise by package size.** Every package that teaches more than one spell teaches two today, so this
  changes nothing now. The bound works the same way for a package of three.

## Follow-up

- `learning/src/downfall_learning/tune_content.py`: `_usage_share` returns the Wilson lower bound, with tests.
- `data/balance/knobs.json`: the target's band, scale and `why`, and the `score` note.
- `data/balance/README.md` and `docs/learning/training.md`: what the metric reads.
- `docs/learning/journal.md`: the score before and after, on the same run.
