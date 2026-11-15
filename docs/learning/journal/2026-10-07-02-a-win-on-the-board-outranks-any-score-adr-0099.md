# 2026-10-07. A win on the board outranks any score (ADR 0099)

All on content `3c9eb083`.

- **Why.** The owner, at the table, saw the bots able to kill the last creature and spend the round on Wait,
  Noxious Cure or Guard. A scan of 240 bot matches confirmed it: 80 each of lookahead-34 against Greedy in
  either seat, and against search-23. It flagged every round where one cast, on a plain roll, killed the last
  enemy and the round did not end the match. That gave 14 such rounds.
  - Greedy and search-23 had declared Guard, Soothing Chant, Revenant Guards, Wait or Full Plate beside a lethal
    Heavy Strike, because the scorer has no notion of a win.
  - The lookahead's 3 were each one attack beside a heal or a chant. It had guessed the enemy would attack, so one
    lethal cast won on the guess, and the enemy healed or raised its defense first.
- **What changed.**
  - `ActionScorer.Wins` reads whether a cast alone wipes the enemy on a plain roll.
  - The heuristic agent and the lookahead's guesses take such a cast before any score, even beside an ally
    already on the kill.
  - Among casts that win on the lookahead's guess, it takes the one that still wins against the enemy's worst
    reply.
- **After.** The same 240 matches, rescanned:
  - The heuristic agents declare the lethal cast: Heavy Strike from every creature that has it.
  - Their remaining rounds are kills the enemy, acting first, survived.
  - The lookahead's two remaining rounds were unwinnable after the enemy's heal or Bone Ward.
- **Strength did not move.** On the 200 benchmark seeds:
  - lookahead-34 against Greedy read 94.3 % before and 94.8 % after, paired -0.005 (-0.021 to +0.011).
  - search-23 against Greedy read 94.5 % both times.
  - The change only acts in the last round of a match.
- **Digest regenerated.** Greedy against itself changed 54 of 400 matches, and the average length went from
  11.44 to 11.27 rounds. The change was merged onto `9419f935`, the same matches under the final names, and
  that digest is regenerated too.
