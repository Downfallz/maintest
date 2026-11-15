# 2026-10-04. Six tier-3 spells raised by hand

- **What the owner changed**, from the same usage readings: the tier-3 spells landed least on the exploring
  run, and the packages Greedy does not buy.

  | Spell | Package | Was | Now |
  | --- | --- | --- | --- |
  | Tornado | Ravager | Damage 5 to up to 3 | Damage 6 |
  | Toxic Waves | Spiritcaller | Damage 3, bleed 1 for 1 round | Damage 4, bleed 2 for 2 rounds |
  | Chain Slash | Warmonger | Damage 5 to up to 2 | Damage 7 |
  | Restorative Burst | Spiritcaller | Heal 6, 2 energy | Heal 7, 2 energy |
  | Crazed Specter | Lich | Damage 6 to up to 3 | Damage 8 |
  | Revenant Guards | Lich | 33 % crit, defense +4 for 1 round | No crit, defense +4 for 2 rounds |

  Four damage and heal knobs reached their old top and now reach the new value. Revenant Guards' crit did
  nothing a defensive spell could use. Restorative Burst now heals as much as Restorative Gush and adds its
  energy on top, for one more energy, so its keep says so instead of the point-for-point trade it was.
  Content `fa149991`, benchmark digest regenerated.
- **What the agents read**, the tuner's reading on the benchmark seeds, landed casts on the exploring run (and
  in the Greedy mirror), before and after:

  | Spell | Package | Exploring | Share of its package | Greedy |
  | --- | --- | --- | --- | --- |
  | Tornado | Ravager | 23 → 55 | 18 % → 42 % | 0 → 1 |
  | Toxic Waves | Spiritcaller | 3 → 22 | 27 % → 92 % | 49 → 136 |
  | Restorative Burst | Spiritcaller | 8 → 2 | 73 % → 8 % | 70 → 28 |
  | Chain Slash | Warmonger | 28 → 87 | 32 % → 69 % | 0 → 0 |
  | Crazed Specter | Lich | 15 → 26 | 50 % → 60 % | 4 → 2 |
  | Revenant Guards | Lich | 15 → 17 | 50 % → 40 % | 2 → 0 |

  The objective reads **0.000**, every measurement inside its band, for the first time since ADR 0095. Some of
  that is Deathstalker bought less (12 landed casts, so its usage bound falls to 0.65), not Death Squad
  cast more: it is still the spell nobody casts. Ravager is split instead of crushed (Psycho Rush 82 % to
  58 %), but Spiritcaller tipped the other way, Toxic Waves taking 92 % of its few casts. Matches are
  shorter, 11.6 to 10.9 rounds on the exploring run, still inside the 10-15 band.
