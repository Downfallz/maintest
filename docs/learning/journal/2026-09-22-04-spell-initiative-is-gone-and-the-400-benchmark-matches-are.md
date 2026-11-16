# 2026-09-22. Spell initiative is gone, and the 400 benchmark matches are identical entry for entry

- **What changed.** `SpellStats` loses `SpellInitiative`, the 36 spell files lose `initiative`, and every
  reading built on it goes with them: the content audit's flat-stat finding and its indistinguishable
  signature, `SpellReach`'s column, and the Python scorer's `dominates` and `_signature` (ADR 0059,
  superseding ADR 0017). The bonus moved to the package at ADR 0056 and nothing removed the per-spell stat,
  so the catalogue carried 36 authored numbers no rule read.

- **Content hash `d367db1c` to `6df8dc30`, and the consolidated schema 2 to 4.** A new digest, and it is
  worth saying exactly what changed in it: **nothing**. The 200 entries are byte-identical to the previous
  digest's; only `contentHash` and `engineVersion` differ. The same holds for a `greedy` against `random`
  evaluation on the benchmark seeds — every winner, round count and remaining health the same, in both
  directions.

- **The schema version had to move, and nearly did not.** Dropping a member from the consolidated document is
  the same compatibility break as adding one, in the same place: the hash is taken over the document, so an
  engine that still knows `initiative` deserializes it back as 0, writes it into the canonical form, and
  reports a hash that does not match — *corruption*, for a catalogue that is sound. The mirror is as bad: this
  engine reading an old version-2 document accepted the version, then threw a `JsonException` naming the
  member, which is the exact sentence the version check sits ahead of to replace. Versions are 3 (no packages)
  and 4 (with them); 1 and 2 are refused with "written by an older builder ... rebuild it from `data/`".
  Caught by Codex on #171, with two tests that failed before the fix.

- **Measured before the removal, not after.** Moving every spell's initiative to a different value (0 becomes
  3, everything else becomes 0) and replaying the same 400 matches changed no outcome either. That is the
  evidence the stat was inert in play; the digest above is the evidence the removal took nothing else with
  it.

- **What it was not inert for.** `check-knobs` compared it in `noNewStrictDominance` and
  `noIndistinguishableSpells`, so a tuning candidate could be refused for an initiative it gave up — a
  rejection over a number no match reads. 33 of the 155 knobs addressed it. The nine `check-knobs` findings
  are byte-identical before and after on 122 knobs, which says the ceilings never read it (`_value_ceiling`
  skipped it by name) and the constraints were the only place it could bite.

- **Two entries in `knobs.json` were describing spells by what they no longer do.** `momentum`'s intent
  claimed it made its owner "permanently faster either way" on a Spell initiative of three, the highest in
  the catalogue; what is left is the energy trade. `throwing_star` is the worse one: its whole stated purpose
  was the two points of Base initiative it bought on unlock, and its `note` used that to argue the
  `check-knobs` finding against it was an artifact. It is not an artifact any more. `tier:prowler:v1` pays
  that bonus once for both its spells, the package splits its casts 1813 to 140 toward `poison_slash`
  (2026-09-22, above), and what the spell's own bounds can reach is `basic_attack` with a class on it.
  **Deliberately left open**: giving it an identity is a content decision.

- **Not done here.** The 33 knobs are removed, not moved. Giving the tuner the packages' `initiativeBonus`
  instead needs a new knob kind in `knobs.json`, the loader, the tuner and the studio's Balance view, and its
  own measurement — whether tuning package initiative moves the objective at all. Removing an inert knob
  needs no evidence; adding a live one does.
