# 0095. The tuner reads packages on a run that buys them well

Date: 2026-10-03
Status: Proposed

## Context

The tuner (ADR 0021) reads every per-package target (`tierUsageShare`, `tierWinSpread`, `spellUsageShare`,
the uncast counts) and the seat question on `variety`, an exploring run that plays Greedy four decisions in
five (ADR 0014, ADR 0029). So it is Greedy's purchases those readings judge, and they are poor: Greedy opens
Occultist on 88 % of round-1 picks, and every other opening beats that one (journal, 2026-10-02). Scored
with the shipped objective on content `0a515da0`, the run read 5.98, 5.22 of it `tierUsageShare`, with five
packages over its 0.8 band: Blightweaver, Deathstalker, Occultist, Plague Doctor and Assassin. Played with
the lookahead instead (ADR 0094), which buys by playing the rounds after a purchase out, the same content read
3.14, with one package over the band, Plague Doctor (Adrenaline Tonic 13 casts to Noxious Cure's 143), and
no penalty on the seat (0.575 to 0.47). Forcing each tier-2 package against Greedy had singled out the same
one (journal, 2026-10-03). A tuning pass on the Greedy reading would move four packages for a bot's mistake.

## Decision

We will play `variety` as `explore:0.2:lookahead` on both sides: the same exploring run, with the lookahead
in place of Greedy behind it. The other evaluations are unchanged: `mirror` and `skill` read length, draws
and whether skill pays, where both sides' purchases are Greedy's alike, and `exploit` reads its own panel. A
candidate went from 124 to 581 seconds locally, about 410 on the runner at the same ratio, so `tune.yml` plays
the opening sweep in twelve slices instead of six, with a four-hour limit, and the search's default budget
comes down from 6 rounds of 6 to 3 rounds of 4.

## Consequences

- Good: the per-package targets and the seat target judge the content, not Greedy's argmax. The one package
  they still flag is the one the forced experiment flagged.
- Bad: a pass explores less by default: about 20 candidates after the sweep instead of about 51. A run can
  still ask for more rounds; each costs about 27 minutes.
- Bad: a pass costs about four times the runner minutes it did, mostly in the twelve sweep slices.
- Neutral: every score before this one is incomparable with every score after it, as ADR 0062, 0064, 0065,
  0068, 0071 and 0090 each made them. The objective's fingerprint names the agents, so the tuner already
  refuses a slice or a score read the old way.

## Alternatives considered

- An agent that buys like the lookahead and fights like Greedy: cheaper, but a third player whose readings
  match neither the lookahead nor Greedy, measured on nothing yet.
- The lookahead only to confirm a pass's leader: the search would still climb on the Greedy reading and
  spend its rounds on the four packages that are a bot's mistake.
- Keep Greedy and widen the `tierUsageShare` band: hides Plague Doctor with the rest.

## Follow-up

- `data/balance/README.md`, `AGENTS.md` and `tune.yml` carry the new agent, the twelve slices and the budget.
- The runner's pace of a candidate under the new agent, measured on the first pass, replaces the estimate.
