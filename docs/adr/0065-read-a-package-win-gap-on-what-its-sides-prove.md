# 0065. Read a package's win gap on what its sides prove

Date: 2026-09-23

Status: Accepted

## Context

`tierWinSpread` asks whether two spells bought by the same pick are worth comparable results
([ADR 0058](0058-a-tier-is-the-package-the-balance-objective-reads.md)). It reads the gap between the best and
the worst win share among the spells of a package, counting only spells at least eight sides declared. It
takes the worst package and scores it against a band of at most 0.15, at scale 0.1 and weight 1. Once
[ADR 0064](0064-read-a-package-monopoly-on-what-its-sample-proves.md) bounded `tierUsageShare` by its sample,
this became the largest term: 13.03 of 23.36 on the shipped content. It has the same weakness in a milder
form. The exploring run gives most packages a few dozen sides, and a gap between two shares measured on so
few sides moves a long way by chance.

Eight packages are read. Drawn 2000 times at their observed side counts, with each pair truly winning alike,
the worst raw gap reads **0.254** at the median and **0.399** at the 90th percentile. That is 1 to 6 points a
perfectly balanced catalogue pays, and a tuner comparing two candidates would be choosing on it. The draw
treats the two spells' sides as independent. Sides that bought a package often declare both of its spells, so
the real noise is somewhat smaller than this, though of the same order.

The worst package today is not noise. `tier:blightweaver:v1` wins 0.659 of 22 sides with `tranquilizer_dart`
and 0.148 of 27 with `infectious_blast`. That gap is 3.7 standard deviations wide against the pooled share
of the two.

## Decision

`tierWinSpread` reads the **lower bound of the 95 % Newcombe interval** of the gap between two win shares.
The interval is built from each share's Wilson interval, and the reading is zero where it crosses zero. The
term takes the largest such bound over the pairs of a package's spells that enough sides declared, and still
reads the worst package. The band, scale and weight do not change. With the same draw of pairs that truly win
alike, the worst bound reads 0.000 at the median and 0.061 at the 90th percentile. `blightweaver` reads 0.238
where it read 0.511.

## Consequences

- Good: the term only counts a gap that the sides prove. On the shipped content one package clears zero
  besides `blightweaver`: `prowler`, at 0.061 on 206 and 364 sides. Every other gap, from `warmonger`'s 0.292
  on 8 sides to `deathstalker`'s 0.236 on 9, is one its sample cannot tell from chance.
- Good: the objective no longer charges a balanced catalogue for its sample sizes, so the tuner stops choosing
  on noise here.
- Bad: every score before this change is incomparable with every score after it, the fifth time the `score`
  note in `knobs.json` records that.
- Bad: a real gap needs sides to show. A package the exploring run rarely buys can hide a wide gap until it is
  bought more, just as ADR 0064 accepted for the monopoly reading.
- Neutral: a win share is an association, not a cause. A side that casts a spell while losing lowers that
  spell's share. The bound makes the reading honest about its sample, not about causation, and that caveat
  was true of the raw reading too.

## Alternatives considered

- **Raise the band to the noise floor**, about 0.4 raw. The noise floor moves with how often each package is
  bought, so a fixed band would be wrong for every catalogue that changes those counts, which is the thing the
  tuner changes.
- **Require more sides before a spell counts.** The eight-side floor stays as the engine's own threshold for
  listing a spell. Raising it discards evidence in steps, where the bound lets it fade smoothly.

## Follow-up

- `learning/src/downfall_learning/tune_content.py`: `_win_spread` reads the Newcombe lower bound, with tests.
- `data/balance/knobs.json`: the target's `why` and the `score` note.
- `data/balance/README.md`, `docs/learning/training.md`, and the journal.
