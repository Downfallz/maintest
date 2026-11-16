# 2026-10-09. Tier 1 and the starting kit are settled, and the knobs that make no sense go

No content moves; `data/balance/knobs.json` does.

- **Why.** Tuning passes spent hours moving numbers the owner then put back (pass 22 kept one of six), often on
  the cheapest spells, where a step reads loudest on the objective. The owner settled tier 1 and the starting kit by hand.
- **What.** Pummel, Brace, Lightning Bolt, Rejuvenate, Poison Slash and Throwing Star (Brute, Occultist,
  Prowler), and the starting kit, Strike and Focus (`heavy_strike`, `wait`), keep their entries, intent and
  keeps, with an empty knob list and a keep that says why. 160 knobs become 136; a pass's opening sweep plays
  24 fewer. The packages had no knobs already.
- **`check-knobs`** has no problem. Its findings that a tier-2 spell cannot become a choice against Lightning
  Bolt or Rejuvenate "at the bottom of its own bounds" now read as settled: the tier-2 spell is the one to move.
- **Then a pass over the 136 left, for knobs that make no sense**, which brings them to 127. Each change is a
  bound, never a number in the content:
  - No spell costs more than 4: nine cost knobs that reached 5 or 6 stop at 4.
  - Permanent defense cannot be searched upward: Carapace's and Fortress's permanent buffs stop at 3, today's.
  - Degenerate corners closed: Overdrive cannot cost 0 (free energy for the whole team every round); Wild Swing
    and Bite lose their cost knob, which could take them to 1 energy for 6 damage, or 4 damage and a 5 heal;
    Whirlwind cannot cost 2 for 6 damage on three enemies.
  - Keeps the bounds could break, since the search moves each knob alone: Latch loses its four bleed and
    regeneration knobs (the halves must stay matched); Renewal Burst heals 8 or 9 at 3 energy and Vital Surge
    at most 7 for at most 3; Fury's crit starts at 0.8 and Crushing Stomp's stops at 0.75; Paralyzing Barb's
    stun stays one round; Eviscerate's bleed is at least 3 a round for at least 2 rounds, above the 5 it can
    hit for.
  - No package carries an initiative knob: Blighted's, the last one, goes. The owner sets them by hand.
  - Keeps that still named Basic Attack, removed on 2026-10-06, are dropped.
- **`check-knobs` refuses the two rules** rather than leaving them to the file: a cost knob past 4 energy, and
  a knob on a package's initiative bonus. The studio's knob editor flags both and no longer offers the
  initiative bonus as a pointer.
