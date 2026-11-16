# 2026-09-14. The bot stopped aiming at creatures that will already be dead

- **What changed**: the agent writes off a target it expects to be dead before its action lands (ADR 0039).
  Benchmark digest regenerated on content `91da955c`; **the weights fingerprint does not move** (`1933f3ae`),
  because no weight value changed.
- **Where the waste actually was, measured before anything was written.** 200 greedy mirror matches, 5796
  resolutions, **1017 fizzled**: `ActorDead` 490 (48.2 %), `AllTargetsInvalid` 464 (45.6 %), `ActorStunned`
  51, `NotEnoughEnergy` 12. `ActorDead` is not a decision — every spell is lost alike. `AllTargetsInvalid`
  is, and it happens because **`RevealAndTarget` is a whole sub-phase before `ActionResolution`**: every
  creature binds its targets on the board as it stood before combat, and then everything resolves.
- **The reading**: `RevealedActions` holds the slots ahead of this one, in timeline order, with targets
  bound, for **both** teams — binding order is timeline order, and a revealed action is public. The agent
  replays them against a board it carries forward, on the plain roll, and writes off who does not survive.
- **Result**: `AllTargetsInvalid` **464 to 206**, total fizzles 1017 to 816. **And it does not play better.**
  Against `random` — the one opponent a code change does not move — `skill` reads 0.985 to **0.988**, flat.
- **A claim this entry first made and had to withdraw**: that `exploit` falling 0.198 to 0.128 proved the
  baseline stronger. It does not. `exploit`'s attacker is `heuristic:search-2.json`, whose *weights file* is
  unchanged but which is still a `HeuristicAgent`, so a **code** change moves both sides of that evaluation.
  It is an independent reading for a weight change, which is how ADR 0032 used it, and not for this one.
  Once the over-prediction below was fixed it read 0.200 anyway.
- **And the content reads much worse against it.** Objective **49.32 to 83.77** and `player1WinShare` 0.510
  to **0.690**, from the centre of its band to well outside. Matches end sooner (7.8 rounds to 6.3) and going
  first decides more. Scores either side do not compare.
- **The assumption that broke**: the fizzle rate is not a measure of playing well. It falls by a quarter and
  nothing that measures strength moves with it.
- **Raising `initiative` does not buy the share back**, which was the obvious lever since ADR 0032 used it
  for this exact reading: 3.0 gives 0.525 and collapses the agent (`skill` 0.985 to 0.730, `exploit` 0.128 to
  0.525); 4.0 runs matches to the round cap. No price fixes the share and keeps the agent.
- **Four wrong guesses, each killed by a measurement and not by a re-read**, which is the part worth keeping:
  1. *The reading belongs at declaration.* It does not: an intent carries no targets. Measured 51.15 against
     a baseline of 49.32, with the fizzle rate unmoved at 0.161.
  2. *It rarely fires because an earlier ally has not declared yet.* False — `IntentRules.Evaluate` already
     builds its list from `Timeline.Slots`. The "fix" for it was a no-op that re-sorted a sorted list, caught
     by a measurement identical to three decimals, and reverted.
  3. *The declaration reading is dead weight now and should go.* Measured the other way: reveal-time alone
     reads **96.73** with `player1WinShare` at **0.725**, worse than the two together. The weak reading is
     what pulls it back to 0.640. It stays.
  4. *The enemy's plans are hidden, so only our own team can be read.* True at declaration, false at binding:
     by then the earlier actions are revealed, targets and all.
- **Still open**: `fizzle` is **still not measurable** — swept at 0, 1, 2, 3, 5 against the new agent it reads
  78.44, 77.84, 77.25, 79.52, 77.25, a spread of 2.3 and not monotonic. The fix works by scoring a doomed
  target at nothing, not by charging the weight. And `NotEnoughEnergy` rose 12 to 30, small against a fall of
  161, and not understood.
- **The review caught the reason it looked better than it was.** The replay carried health forward and
  nothing else, while `ResolutionRules.Resolve` rechecks stun, energy and defense: it resolved actions that
  would not have happened and killed creatures a heal or a defense buff would have saved, so the actor wrote
  off living creatures and picked worse targets on purpose. The doc comment asserted the heal case was
  harmless — "it can only keep a creature alive" — which is exactly the wrong direction. Fixed by stopping
  the replay where it would have to guess, and **that is what removed the apparent gain**.
- **Verified**: build, tests, format, ruff, pytest, studio tests, digest regenerated and re-verified.
