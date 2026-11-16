# 2026-10-04. Seven spells the agents left on the shelf are raised by hand

- **What the owner changed**, after the spell usage page (exploring run on content `2fe1a207`) listed the
  spells that are barely cast or sit in packages Greedy does not buy:

  | Spell | Package | Was | Now |
  | --- | --- | --- | --- |
  | Reckless Swing | Berserker | Damage 5 | Damage 6 |
  | Meteor | Elementalist | Bleed 1/round | Bleed 2/round |
  | Frostbite | Elementalist | Initiative -2 | Initiative -3 |
  | Protective Slam | Marauder | Damage 4, initiative -2 | Damage 5, stuns 1 round |
  | Extort | Marauder | Damage 3 | Damage 4 |
  | Parasite Jab | Parasite | Damage 3, heals its caster 3 | Damage 4, heals 4 |
  | Latch | Parasite | Bleed and regeneration | Plus an instant 1 damage |

  Protective Slam first went to initiative -3, then the owner made it a one-round stun instead; its two
  initiative knobs are gone. Every other value sits inside its knob but two: Parasite Jab's damage knob now reaches 4, and Latch's new damage
  is a knob of 1 to 2. On that run Greedy bought Marauder zero times, and Extort, Meteor and Parasite Jab were
  among the least cast spells of their tier. Content `85423648`, benchmark digest regenerated; the reading below is on `b914e82a`, with the slow.
- **What the agents read**, the tuner's own reading (`score-content` on the benchmark seeds) before and after,
  landed casts on the exploring run and in the Greedy mirror:

  | Spell | Exploring | Greedy |
  | --- | --- | --- |
  | Reckless Swing | 118 → 176 | 10 → 19 |
  | Meteor | 41 → 77 | 1 → 51 |
  | Frostbite | 50 → 60 | 188 → 630 |
  | Protective Slam | 81 → 136 | 0 → 0 |
  | Extort | 47 → 78 | 0 → 0 |
  | Parasite Jab | 57 → 185 | 20 → 90 |
  | Latch | 90 → 89 | 41 → 36 |

  Every package touched is cast more on the exploring run, and Elementalist and Parasite are bought far more
  by Greedy; Marauder still never is. The objective went from 0.017 to 2.749, all of it but 0.03 from
  `tierUsageShare` on Deathstalker: Mortal Wound 35 landed casts to Death Squad's 1, read now on more casts,
  so the bound sits further over 0.8 (0.858). That is the dead spell the owner has set aside, more visible,
  and nothing these seven changes did to it.
- **Protective Slam as a stun**, read the same way on content `85423648`: the exploring run landed it 351 times
  where the slow landed 136, and Marauder's casts went from 214 to 486, with the Brute opener that leads to it
  from 1158 to 1582. Greedy still never buys Marauder. The objective read 1.209, all of it Deathstalker again
  (0.839).
