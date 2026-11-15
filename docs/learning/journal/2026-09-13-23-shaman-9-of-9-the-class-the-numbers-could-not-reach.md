# 2026-09-13. Shaman, 9 of 9: the class the numbers could not reach

- **What changed**: `restorative_burst`'s heal 3 to **4**, `toxic_waves`' bleed duration bound 3 rounds to
  **2**, and two keep clauses rewritten. Content `a940c07c` to **`afc1bee2`**. All three Shaman spells are
  faithful ports; nothing was dropped at the translation.
- **A third price keep that this refonte falsified, and again it was my own pass.** `toxic_waves` kept "costs
  more than Tornado because it keeps working" — true until the Berserker pass took `tornado` from 2 to 3 for
  its own good reasons, leaving both at 3. It is the second clause that pass invalidated on its way past;
  `tranquilizer_dart`'s was the first. **The claim cannot be restored by price**: 4 would tie
  `crushing_stomp`, whose first keep is "the most expensive cast in the catalogue" and whose entry says the
  price *is* the spell. So the clause is rewritten to what was always the real point — the lingering, not the
  receipt.
- **And a bound that tripled a bleed across three targets.** `toxic_waves` could reach 3 rounds where legacy
  carries `Length = 1` and so does the content, which put its ceiling at **26.40**, the second largest in the
  catalogue. Narrowed to 2: ceiling **21.60**, nothing a build reads moved. Third box narrowed this way after
  `ice_spear`'s and `tranquilizer_dart`'s.
- **`restorative_burst` cannot reach its tier and its entry now says so.** 2.80 a round against a band of 8 to
  14, with a **ceiling of 3.80** — the worst reading in the catalogue now that `death_squad` is fixed. Half
  the spell is 2 energy at 0.2 a point, which is 0.40; to clear 8 on the heal alone it would need to heal 10,
  more than `restorative_gush`, the pure heal it is meant to be a choice beside. The heal goes to 4 — the top
  of its box that keeps every clause — so its trade against the gush is 2 heal for 2 energy instead of 3 for
  2. **2.80 to 3.60, which fixes nothing structural**, and the entry says that so nobody reads it as a fix.
- **It is the only spell that hands energy to another creature.** `wait` is the only other spell that hands
  energy out at all and it hands it to itself. So the argument is `soul_devourer`'s drain read from the other
  side: giving an ally two energy does not give it 0.40 of anything, it buys it the cast it was saving for,
  and nothing in the scorers reads a cast bought any more than it reads a cast denied.
- **Both of this class's tier-3 spells are unplayed, and the numbers did not move it.** At `explore:0.2`,
  `restorative_burst` goes 1 declaration to **4** and `toxic_waves` stays at **3** — against every other
  class's tier-3 children at 23 to 161. `healing_screech` above them is declared 52 times, and the tree shape
  is the standard one every class has, so the branch is walked and its far end is not taken. I could not close
  that from this class's bounds and it is handed to the larger pass rather than guessed at.
- **Nine of nine classes.** Four of the five weights-and-rules findings this refonte produced are now pointing
  at two unswept numbers: the **energy** weight (`momentum`, `restorative_burst`, `soul_devourer`'s drain) and
  the **initiative** weight (`death_squad`, `ice_spear` and the three spells that cannot clear it). Neither is
  a content problem, and both are measurements someone can run the way ADR 0032 ran the first one.
