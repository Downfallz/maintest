# 2026-09-22. The content hash moved twice for a game that did not change: the evolution packages, then the schema version they made necessary, with all 400 benchmark entries byte-identical both times

- **What moved.** Content `7e199df4` → `4f453e87` when `data/Tiers/` arrived (21 packages the data builder
  now validates, nothing plays yet), then `4f453e87` → `d367db1c` when the consolidated document became
  version 2. Baseline agents `greedy` versus `greedy`, so the digest is deterministic: every one of the 400
  entries is byte-identical to the previous digest's, on both moves. Only `contentHash` and `engineVersion`
  differ. The digest for `4f453e87` is deleted rather than kept: that document existed for three commits on a
  branch and never on `main`, and a digest no content hashes to is dead weight in a directory whose rule is
  one digest per content hash.

- **Why the version had to move, which the first attempt got wrong.** The consolidated document *is* its own
  hash — `ComputeHash` serialises the whole record — so an always-present `tiers` member reaches the canonical
  form of every catalogue ever built, including those built before the member existed.
  `runs/tune-4/work/data/dst/game.schema.json` carries hash `7dc96614…` and hashed to `f5066a7d…` under the
  new record: `Load` rejected a file nobody had touched, with a message that reads as corrupted content. The
  first attempt kept `schemaVersion` at 1 and argued that a catalogue with no packages is the catalogue that
  exists today. It is not, once its hash changes. So: no member at all when there are no packages (ADR 0031
  drops an empty caster-effect list and ADR 0015's switch clears itself for the same reason), version 2 only
  when the document carries them, and the pairing verified in both directions on load so that one content hash
  means one document. Three historical run documents load again; a document from a newer builder is now
  refused as a version rather than as a bad hash.

- **And a validation that was not the rule it claimed.** Prerequisites had to sit *above* what they open,
  which a level-3 package requiring only a level-1 package satisfies — two picks for a package priced at
  three, with the pacing of section 3.3 costed on nine purchases for three creatures. An advanced package now
  needs a prerequisite at exactly the level below it, which the 21 authored packages already satisfy, so no
  content moved for it.
