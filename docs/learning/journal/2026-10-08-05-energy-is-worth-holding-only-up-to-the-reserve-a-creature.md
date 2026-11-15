# 2026-10-08. Energy is worth holding only up to the reserve a creature can spend (ADR 0103)

All on content `da8d744b`.

- **Why.** Played at the table, the lookahead on lookahead-36 cast Focus three rounds running with a creature
  that had no package, to 12 energy. In its mirror 175 of 297 Focus casts came at 6 to 12 energy, 240 of them
  from creatures with no package: the scorer priced energy kept point for point however much was held, and
  lookahead-36 weighs damage at 0.425 against energy at 0.512.
- **What changed.** A creature's own purse is worth the energy weight only up to its reserve: its dearest known
  spell's cost, and for each of the two rounds after, what a round's 2 energy does not cover. Crushing Stomp
  wants 8, a 3-cost spell 5, spells no dearer than 2 their cost. A first version capped every creature at the
  catalogue's dearest cost, 4, which ignored what a round gives back. The candidate terms are now also read at
  the speed the creature chose, which the heuristic decides on.
- **The lookahead on lookahead-36 against Greedy**, 60 seeds nothing had played (995377 to 995436, both seats),
  read with `paired`:

  | | wins | score |
  |---|---|---|
  | without the reserve | 38 of 120 | 0.317 |
  | with the reserve | 65 of 120 | 0.542 |

  A paired difference of +0.221 (+0.104 to +0.338), clear of zero.
- **Toxic Mend at 6 and 3 is what took lookahead-36 down.** On `cedcfb61`, before it, the same seeds read 104 of
  120. Its weights leaned on Toxic Mend, about ten casts a match. A weight search under both changes is running
  (#304).
- **Greedy** barely moves: the digest changes on 156 of 400 matches, and their mean length goes from 15.03 to
  15.07 rounds.
