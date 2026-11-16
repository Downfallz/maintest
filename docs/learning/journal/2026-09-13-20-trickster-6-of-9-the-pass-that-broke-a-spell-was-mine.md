# 2026-09-13. Trickster, 6 of 9: the pass that broke a spell was mine

- **What changed**: `tranquilizer_dart` alone — price 3 to **2**, damage 3 to **2**, stun one round to
  **two**. Every move is inside bounds already declared; no knob was widened. Content `25d0aed7` to
  **`3762ee24`**. Two of the class's three spells were re-authored under ADR 0035 and are left alone.
- **Legacy is not what went wrong with it.** The port is faithful — 3 energy, 3 damage, a one-round stun — and
  so was the relationship it was built on: legacy Crushing Stomp is 4 energy, 6 damage and a stun of **one
  round**. Pay 4 for a big hit and a stolen activation, or 3 for a small hit and the same stolen activation.
  That is a choice.
- **The second round is mine.** The Warlord pass, four entries down, took `crushing_stomp` to damage 7, a
  critical chance of 0.75 **and a two-round stun**. The Warlord entry justifies all three and none of it is
  wrong on its own; what it did to the Trickster is that the cheap stun became a strictly worse expensive one
  — **75% of the price for 44% of the reading** — and it was declared **1 time in 400 matches**.
- **Nothing caught it, and the reason is worth keeping.** `outclassed` compares a spell's *ceiling* against a
  rival's *current* value. This spell's ceiling is 10.00 and `ice_spear` carries 10.20, so the check has been
  reporting the pair for entries — and it never once named `crushing_stomp`, because the stomp's 9.12 sits
  *below* that ceiling. The check was right and pointing at the wrong spell.
- **The fix is the spell's own intent, applied.** It has always read "the Trickster buys the tempo the Warlord
  pays full price for, and pays for it in damage". At 2 energy for a two-round stun and a damage of 2 it does
  exactly that: **4.00 a round to 8.00**, inside the band, still under `crushing_stomp`'s 9.12 — cheaper and
  weaker, which is its first keep — while buying the tempo at half the Warlord's price.
- **Measured: 1 declaration to 78**, 72 stuns landed, and `crushing_stomp` unmoved at 93.1% over 99
  declarations. The dart does not cannibalise it; it is a second way to spend a turn.
- **And the split reads the other way round from `death_squad`'s.** Greedy wins **0.387** of the sides that
  declare the dart; the exploring agent wins **0.659 over 44**. Last entry greedy over-cast a spell and lost
  with it; here greedy under-plays one that wins comfortably in varied play. Both are the same fact about the
  scorer, seen from two sides.
- **The finding stays, and it is not this class's to clear.** The dart's ceiling is held down by a damage bound
  its own identity requires — "the damage is a rounding error, not a second half". The rival is `ice_spear` at
  10.20, a Wizard spell, so the bounds that need to move are the Wizard's.
- **`crushing_stomp` at 93.1% over 58 sides is the strongest reading in the catalogue** and is written down
  here rather than acted on: it belongs to a class already passed, and the larger tuning pass is where a
  number like that gets arbitrated against everything else.
