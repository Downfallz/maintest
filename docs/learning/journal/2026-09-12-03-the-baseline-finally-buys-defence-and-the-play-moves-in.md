# 2026-09-12. The baseline finally buys defence, and the play moves in steps rather than smoothly

- **What changed**: [ADR 0028](../../adr/0028-name-the-defense-weight-and-move-its-price-one-step-up.md). The
  weight called `buff` is now called `defense`, the only thing it has priced since ADR 0018, and its default
  goes **0.5 to 0.65**.
- **Digest**: regenerated and verified, **174 of 400 entries moved**, content `d4a21a55`. `Greedy` is a
  different player now, fingerprint `@7aff3a10` to **`@a4e83485`**, so nothing stamped before this compares
  term by term with anything after it.
- **Why the number needed deciding at all.** ADR 0022 changed what this term multiplies — from
  `buff x amount x rounds` to the damage a buff actually prevents — and deliberately left the value at 0.5. So
  the number had been reasoned about one quantity and was scaling another. Asked what 0.5 meant, nobody could
  say. Keeping it needed an argument as much as moving it did.
- **Sweeping this weight alone**, mirror evaluations on the benchmark seeds:

  | `defense` | rounds | round cap | entropy | `guard` | `rejuvenate` | `lightning_bolt` | never cast |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | 0.5 (before) | 7.78 | 4.5 % | 1.800 | 6.9 % | 11.9 % | 62.2 % | 2 |
  | 0.6 | 7.88 | 4.5 % | 1.791 | 8.1 % | 12.0 % | 62.1 % | 3 |
  | 0.62 | 10.64 | 17.0 % | 1.984 | 13.1 % | 25.3 % | 48.5 % | 2 |
  | **0.65** | **10.73** | 17.5 % | **1.992** | 13.2 % | **25.4 %** | 48.3 % | 2 |
  | 0.68 | 12.36 | 23.0 % | 2.222 | 18.6 % | 23.4 % | 41.8 % | 1 |
  | 0.7 | 12.36 | 23.0 % | 2.222 | 18.6 % | 23.4 % | 41.8 % | 1 |
  | 1.0 | 22.81 | 68.5 % | 2.365 | 26.0 % | 24.2 % | 21.9 % | 1 |
  | 1.5 | 30.00 | **100 %** | 1.612 | 28.9 % | 44.5 % | **0 %** | 5 |

- **It is a staircase, not a slope**, and that is the finding. 0.62 and 0.65 read identically; so do 0.68 and
  0.7. The agents take an argmax, so a decision flips only when an ordering flips, and nothing moves in
  between. Picking a price is picking a step, not a point, and 0.65 sits in the middle of its step rather than
  on an edge — a small error in it changes nothing, which a boundary value could not promise.
- **What 0.65 buys**: matches go 7.78 to **10.73 rounds**, inside the 8..16 band for the first time on this
  content, and the two defensive spells roughly double — `guard` 6.9 % to 13.2 %, `rejuvenate` 11.9 % to
  25.4 %. The defensive half of the catalogue was not dead because it was badly designed. It was dead because
  the baseline would not buy it.
- **What it costs**: `roundCapShare` 4.5 % to **17.5 %**, well past its 5 % target. That bill goes to the next
  tuning pass, which can answer it by cheapening attacks or making healing cost more. And the catalogue on
  `main` was tuned against a baseline that would not defend, so it is now tuned for a player who no longer
  exists.
- **The baseline plays better, checked rather than assumed.** `search-2`, the searched agent of the entry
  below, beat the old `Greedy` **0.5875** on hold-out seeds and beats the new one **0.5650** — 226 of 400. It
  kept 2.25 points of its edge and the new baseline closed the rest.
- **Why 1.5 was not taken** (it was the value asked for): every match runs out of rounds and
  `lightning_bolt` is never cast at all. Both sides turtle and nobody dies. The cause is compounding, and it
  is what the shared unit hides — `damage` prices a hit paid once, `DefensiveScore` multiplies what a buff
  prevents by the rounds it holds. A `guard` taking 2 off an incoming hit for 3 rounds prevents 6, so at 1.5
  it scores 9, more than any attack in this catalogue can. And the intuition behind it is already paid for
  separately: `DefensiveScore` adds `weights.Kill` when a buff turns a lethal round survivable, so this weight
  prices attrition only.
- **Also fixed**: the glossary's Scoring weights entry listed eight terms and missed `initiative`, which ADR
  0018 added three days ago. The ubiquitous language had drifted off the code it names.
- **What is next**: a `tune-content` pass against this baseline. It inherits a 17.5 % round cap and a
  catalogue tuned for the previous player, and for the first time it is searching for content a defender will
  actually pick up.
