# 2026-09-16. The content tuner gets the hold-out the weight search already had

- **Not a number that moved; a number that could not be trusted.** `tune.yml` picked the candidate that
  scored best on the seed file the objective names, and `improved` compared it with the starting content
  *on those same seeds*. That is the shape of problem ADR 0049 names for the learning loop, and
  `search.yml` had already answered it for the weight search: replay the winner and the starting point on a
  window of seeds no candidate saw. The tuner had no such step. I had written that both workflows lacked
  one; only the tuner did, and the entry that said otherwise was wrong about `search.yml`.

- **What exists now.** `score-content` plays a catalogue as it stands, once, on a seed file, and scores it by
  the objective — no search, no neighbours. The `Tune the catalogue` workflow keeps a copy of `data/` before
  the search touches it, and when a proposal is applied, plays both catalogues on a window of consecutive
  seeds placed clear of the objective's file (above its largest seed when that fits the signed range, below
  its smallest otherwise, the same rule as `search.yml`). Both scores go in the run summary and the commit
  message. **Lower is better there**, because the score is a penalty, and both places say so rather than
  letting the table be read the way the win-rate table reads.

- **It reports and does not gate**, which is what `search.yml` does too: the maintainer reads two numbers on
  the pull request and decides. A proposal that does not score below its starting point on unseen seeds has
  been shown to fit the file it was searched on, and nothing else.

- **Checked on the real engine, not only on the fakes.** `score-content` on the committed content with a
  three-seed window played all four objective evaluations, wrote `score.json` with the seed file it was
  given, and printed the breakdown — 248.6, almost all of it `exploit.winRateA` reading 1.000 on three
  matches, which is what three matches look like and not a finding. The jq window against the real
  `benchmark-seeds.json` starts at 995317, one past its largest seed, and shares none of its 200 seeds.

- **Two things the pull request's reviewers caught, both real.** Codex: the lower window was refused when
  its start would be negative, copied from `search.yml`'s rule — but a seed is an `int` the engine adds to
  unchecked, and the seed file loader refuses only an empty list and a repeat, so the floor is the signed
  minimum and not zero. Checked at the edges: a top at `int.MaxValue` with a low of 100 now gives −100, a
  low of 0 gives −200 where it used to give up, and one seed past the signed minimum still gives "none".
  `search.yml` carries the same `>= 0` and is not touched here. Sonar: the new command duplicated
  `tune-content`'s preflight line for line and had no test past that preflight, so both handlers now share
  one, and `score-content` grew `--builder` so the whole command runs end to end on the fake builder and
  engine in a test — the same call `tune.yml`'s hold-out step makes.

- **What is not claimed.** No tuning pass has run under this yet, so no proposal has been checked by it. And
  it is a window of 200 seeds against a search that played 200: one hold-out, not a spread. The tuner's
  score is a sum over four evaluations of 200 matches each and is steadier than a single win rate, but how
  steady is not measured here.
