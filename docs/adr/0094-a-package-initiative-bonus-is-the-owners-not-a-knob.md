# 0094. A package's initiative bonus is the owner's, not a knob

Date: 2026-10-02
Status: Accepted. Supersedes [ADR 0061](0061-a-package-initiative-bonus-is-a-balance-knob.md).

## Context

ADR 0061 made a package's `initiativeBonus` a balance knob, the only one a package could have. Its own
measurement warned against using it: one move on `tier:prowler` shifted 54 of the 71 objective metrics, and
almost all of the gain came from the seat reading, not from balance. Since then every package knob was taken
out of `data/balance/knobs.json` (journal, 2026-10-01), and the owner set the bonuses by hand from play: the
Prowler line stays the fastest opener, and Berserker, Elementalist, Marauder, Plague Doctor and Shaman were set
on 2026-10-02. The owner is happy with them and wants them to stay put.

An empty knobs list only says that nothing moves the bonuses *today*. `check-knobs` still accepted an
`/initiativeBonus` knob on a package, and the studio still offered to add one, so putting a knob back was one
line that nothing would question.

## Decision

**A package carries no knob.** `check-knobs` refuses any knob on a package entry, its initiative bonus
included, and so does the preflight of `tune-content` and `score-content`, which runs the same check. The
studio's balance reading marks such a knob as refused, and its editor offers no knob to add on a package. A
package entry keeps its name, its intent and what has to stay true of it.

The bonuses are authored in `data/Tiers/*.json` and changed by hand, by the owner, like any other design
decision.

## Consequences

- Good: no tuning pass can change which line is fast. That is the class, not a tuning value, and initiative is
  the lever a search reaches the seat reading with (ADR 0061).
- Good: nothing in the content or in a match changes. The bonuses, the benchmark digest and every reading are
  exactly as they were.
- Neutral: the tuner's machinery still moves documents, packages included (ADR 0061). It is simply never handed
  a package knob, because the check that gates every search refuses one.
- Bad: if a later pass needs to tune initiative, it has to revisit this decision in a new record rather than
  adding a line to the knobs file. That friction is the point.

## Alternatives considered

- **Leave the knobs list empty and change nothing else.** That is the state since 2026-10-01. Rejected because
  nothing would stop a knob being added back, by hand or by the studio's "Add a knob".
- **Pin each bound to the current value.** A knob of zero width cannot move, but it is still a knob the files
  must carry and a reader must understand. Refusing knobs on packages says the same thing more plainly.

## Follow-up

- `learning/src/downfall_learning/knobs.py`: `validate` refuses every package knob.
- `studio/balance.js` and `studio/studio.js`: a package knob reads as refused, and none can be added.
- `data/balance/README.md` and the package entries in `data/balance/knobs.json`: the bonus is the owner's.
