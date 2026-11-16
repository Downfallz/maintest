# 2026-09-23. The lookahead reads every creature at the speed it chose, and the benchmark does not move

- **What changed.** The lookahead's one-step readings priced every cast at Standard: the one-step score
  that breaks a tie between two rounds, the targets it takes each creature to pick at its slot, the spell it
  guesses an enemy declares, and the critical chance its rollout is mixed by. A Quick cast never crits, so an
  enemy that chose Quick was guessed to cast a critical it could not roll. Every one of those now reads the
  speed the timeline carries for that creature ([agents.md](../agents.md#the-round-played-out)), as the heuristic
  agent already did for its own. The rollout's own resolution already did.
- **The measurement.** The lookahead against four opponents on the benchmark seeds, content `4ab506fa`,
  `main` against this change, read seed by seed with `paired`: greedy 0.5162 and 0.5162, search-4 0.6275 and
  0.6275, random 1.0000 and 1.0000, all exactly zero, and stun-first 0.2150 and 0.2100, -0.005 anywhere from
  -0.0205 to +0.0105. The investigation in the entry below measured the same change on the 20-health content
  (0.383) and left it out for that reason.
- **Why nothing moves.** The heuristic agents pick Quick only when a spell kills without a critical, and on
  that board the kill decides every reading whatever the roll. The guess this changes is the rare one where a
  Quick creature has a critical-hungry spell and a safer one, and the test that pins it builds that board on
  purpose. A correction, not a lever: it is here so that the next agent built on these readings is not
  measuring a critical that cannot happen.
