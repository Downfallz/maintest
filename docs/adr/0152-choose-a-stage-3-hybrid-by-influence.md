# 0152 — Choose a Stage 3's hybrid by the influences of the creature's other mutations

Date: 2026-11-08

Status: Accepted for the opt-in battle view. Extends [ADR 0151](0151-show-each-creature-as-its-evolution.md): its
identity rule, its hybrid ranks and its registry stay.

## Context

PR #495 staged twelve approved Predator hybrid portraits, four for each Predator Stage 3 (Soulreaver, Deathstalker,
Blightweaver). The owner approved what each one shows:

| Stage 3 | Portrait | Shows when the creature also holds |
| --- | --- | --- |
| Soulreaver | `predator-soulreaver-01` | the Blightweaver line's Stage 2 (Plague Doctor): venom reservoirs, parasitic tissue |
| | `predator-soulreaver-02` | a Warped mutation: energy membranes, supernatural fissures |
| | `predator-soulreaver-03` | a Brute mutation: reinforced carapace, massive musculature |
| | `predator-soulreaver-04` | Ultimate: every one of those influences at once |
| Deathstalker | `predator-deathstalker-05` | another Predator line's Stage 2 (Plague Doctor or Parasite): toxic glands |
| | `predator-deathstalker-06` | a Warped mutation |
| | `predator-deathstalker-07` | a Brute mutation |
| | `predator-deathstalker-08` | Ultimate |
| Blightweaver | `predator-blightweaver-09` | the Soulreaver line's Stage 2 (Parasite): widened maw, fangs, red tissue |
| | `predator-blightweaver-10` | a Brute mutation |
| | `predator-blightweaver-11` | a Warped mutation |
| | `predator-blightweaver-12` | Ultimate |

The art direction weighs them: a single influence is 85% Stage 3 and 15% the secondary mutation. A double influence
would be 70/15/15. The Ultimate is 70% Stage 3 and 30% spread over all the other Stage 2 influences.

ADR 0151's registry finds a hybrid by the exact pair of illustrations it combines. "A Warped mutation" is any of six
illustrations, and the Ultimate combines several. No portrait is drawn for a double influence, and none for the
Deathstalker line (Assassin) under Soulreaver or Blightweaver.

## Decision

- **An entry names an influence, not only a pair.** A hybrid entry varies one Stage 3 (`dominant`) and shows for one
  of these:
  - `influence.artworks`: one of these illustrations is held. ADR 0151's `hybrid` is the one-illustration case.
  - `influence.family`: a mutation of that family is held.
  - `ultimate`: every other influence drawn for that Stage 3 is held.

  Entries name the page's own illustration ids, never a content id. Which package draws which illustration remains
  presentation data served with the catalogue (`data/presentation/mutation-art.json`).
- **The Ultimate comes first.** It shows once, for each other influence drawn for the creature's Stage 3, the
  creature holds a Stage 2 carrying it. The Stage 2 that led to the Stage 3 never counts. A Stage 1 does not count
  either: the Ultimate spreads its 30% over Stage 2 mutations.
- **Otherwise one influence shows,** by ADR 0151's ranks, the latest bought first within each:
  1. another Stage 2 of its own family;
  2. a Stage 2 of another family;
  3. a Stage 1 of another family.

  The first mutation carrying a drawn influence picks the portrait.
- **A double influence shows its first influence by rank,** as no 70/15/15 portrait is drawn. Adding one later is a
  registry entry and a precedence line, not a change of identity.
- **An influence nobody drew is skipped.** With none, the Stage 3's own image shows, as before.
- **Resolution stays pure and presentation only.** It reads the mutations the host says the creature owns and the
  purchase order ADR 0151 recovers. It never changes what a creature owns, can buy or does. With no recorded order,
  the same mutations give the same portrait every time.
- **The host serves each portrait by name** under `battle-art/evolutions/hybrids/`. The page fetches one only when
  a creature on the field resolves to it.

## Consequences

- Good: the twelve approved portraits show exactly where the owner placed them, and the selection is deterministic.
- Good: a family influence covers every mutation of that family with one entry instead of one per illustration.
- Neutral: a creature with two influences and no Ultimate shows only the first influence's traits until a double
  portrait is drawn.
- Neutral: the Assassin line lends no traits to Soulreaver or Blightweaver: no portrait was approved for it.
- Bad: the Ultimate needs three Stage 2 mutations besides the one that led to the Stage 3. Few matches will see it.

## Alternatives considered

- One pair entry per illustration: 24 entries for the Warped and Brute influences alone, which a new illustration of
  either family would silently miss.
- The Ultimate only once every other Stage 2 tier is held: eight tiers, which no match can reach.
- A double influence shown as the Ultimate: it would show 30% of traits the creature does not hold.

## Verification

- `table/battle-evolution.test.js` covers:
  - every single-influence portrait, reached through real tier paths;
  - the lines with no portrait;
  - each Ultimate and its lenders;
  - a Stage 1 never completing an Ultimate;
  - double influences by rank;
  - a selection with no recorded order.
- `table/battle-evolution-art.test.js` and `table/battle-predator-hybrid-art.test.js` check that:
  - every portrait is a registered WebP with a silhouette that fills the audited box;
  - every influence names a real illustration or another family;
  - each Stage 3 has one Ultimate.
- `TableFilesTests` serves every file of the hybrids folder.
- `battle-evolution.spec.js` shows a single hybrid and an Ultimate on the field, fetched only when needed.
