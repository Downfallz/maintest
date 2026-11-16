# 2026-09-15. A policy keeps six decimals, and the saving is in what git stores rather than on disk

- **What changed**: `policy.json` rounds its weights to six decimals on the way out (`WEIGHT_DECIMALS`), and
  `models/clone/ci-69/` is rewritten at that precision with both its evaluations regenerated. No content
  moves, no agent default moves, the benchmark digest is untouched. Nothing on the C# side changes: the
  engine reads the same field of the same shape, with fewer digits in it.
- **The number that matters is the compressed one, and it is the only place the saving is large.** Measured
  on `ci-69`'s clone:

  | | on disk | git (zlib) |
  | --- | --- | --- |
  | full precision, indented | 1.91 MB | **0.57 MB** |
  | six decimals, indented | 1.36 MB | **0.30 MB** |
  | full precision, compact | 1.27 MB | 0.53 MB |
  | six decimals, compact | 0.73 MB | 0.27 MB |

  Rounding roughly halves what git stores. Dropping the indentation as well takes 0.73 MB off the disk and
  only **0.03 MB** off the repository, because zlib already pays for whitespace — so the file stays indented
  and the change is the rounding alone.
- **A claim of mine this corrects.** I have said several times that rounding "halves `policy.json`". That was
  measured on the *value* policy in compact form, where it does; on this clone, on disk, it is 71%. The
  halving is real but it is of the compressed size, which is a different sentence and the one that was worth
  making.
- **Six decimals is chosen with room to spare, not at the edge.** The committed clone plays the benchmark
  seeds identically at six, four and three decimals — 0.710, score 0.725, average rounds 11.5175, the whole
  spell-usage table equal — and over **5326 recorded steps and 37886 candidate scorings, not one argmax
  differs** at any of the three. A score is a dot product of 431 terms, so a weight's seventh decimal is
  orders of magnitude below the gap between two candidates. Three was tested and was harmless; six is what
  ships.
- **Rewriting the model forced its evaluations to be rewritten too**, which is the part worth remembering.
  A policy's identity in a run stamp is a fingerprint of its bytes, so `ci-69` went
  `Policy:…@17b063c6` to `Policy:…@4126ba18` while playing exactly the same. The two `evaluation*.json`
  beside it still named the old bytes, and an evaluation that names an agent which no longer exists is the
  same defect as a `keep` describing content that has moved. Both are regenerated against the file they sit
  beside: 0.710 against `Greedy`, 0.9925 against `Random`, unchanged.
- **Verified**: the rewritten policy replayed on the benchmark seeds against `Greedy` and against `Random`,
  the per-precision argmax comparison above, 312 pytest, 779 .NET tests, format, ruff, and the benchmark
  digest.
