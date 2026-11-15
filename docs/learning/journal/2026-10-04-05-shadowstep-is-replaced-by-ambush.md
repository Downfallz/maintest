# 2026-10-04. Shadowstep is replaced by Ambush

- **Why.** Shadowstep (5 damage, +2 initiative on its caster for a round) was cast, but the owner found the
  haste uninteresting on the Assassin, which is the fastest creature anyway. Ambush spends that initiative
  instead: a heavy hit, and its caster is slowed next round. It is the one spell paid for in initiative,
  beside the ones paid in health (Reckless Swing, Hateful Sacrifice) or in defense (Psycho Rush).
- **What was tried**, the Assassin line forced against Greedy on the 400 confirmation seeds; declarations
  count both sides, since Greedy buys the Assassin too:

  | Spell | Shape | Declared | Mortal Wound | Rounds |
  | --- | --- | --- | --- | --- |
  | Shadowstep | 5 damage, crit 0.5, +2 initiative, cost 3 | 360 | 537 | 10.5 |
  | Ambush | 8 damage, crit 0.5, -5 initiative, cost 3 | 887 | 395 | 10.3 |
  | Ambush | 7 damage, crit 0.5, -5 initiative, cost 3 | 534 | 310 | 10.3 |
  | Ambush | 7 damage, crit 0.35, -5 initiative, cost 3 | 260 | 341 | 10.3 |
  | Ambush | 6 damage, crit 0.5, -5 initiative, cost 3 | 219 | 222 | 10.4 |
  | **Ambush (kept)** | **8 damage, crit 0.35, -5 initiative, cost 3** | **578** | 349 | 10.4 |
  | Ambush | 6 damage, crit 0.5, -5 initiative, cost 2 | 884 | 229 | 10.1 |
  | Lethal Lunge | 12 damage, crit 0.5, stuns its caster, cost 3 | 1376 | 67 | 9.6 |
  | Vanish | 4 damage, crit 0.5, +6 defense on its caster, cost 2 | 2199 | 689 | 11.1 |

  The owner judged 8 at a 0.5 crit too strong for tier 2. Lethal Lunge made Greedy take the Assassin every
  match and emptied Mortal Wound, and a caster just out of a stun is immune to its own (ADR 0072), so it
  would cost nothing then. Vanish is Shield Bash made shorter. 7 at 0.5 was cast more than Shadowstep, but
  `check-knobs` read it as strictly worse than Enraged Charge (7 damage, crit 0.8, cost 3, no price), which
  any creature can buy at tier 2. 8 at 0.35 averages the same hit, is no longer dominated, and is cast as
  much; at a cost of 2 Ambush is paid from a round's income and Momentum fell from 594 declarations to 104.
  `check-knobs` reads Ambush at 3.53 a round at the top of its bounds, because it prices the initiative it
  costs; the agents, which play the turn order out (ADR 0088), cast it anyway.
- **Content `6d8838c0`**, benchmark digest regenerated. It takes Shadowstep's place in the package under a
  new id, `spell:ambush:v1`; its knobs are the cost (3 to 4), the damage (7 to 8), the crit (0.3 to 0.45)
  and the initiative it costs (3 to 5).
- **The tuner's reading** on content `6d8838c0`, the benchmark seeds: on the exploring run Ambush was declared
  125 times to Shadowstep's 64 and its sides won 0.605 to 0.476; on the mirrored Greedy run 143 to 101, winning
  0.408 to 0.290. The objective rose from 0.000 to **0.148**, and not on the Assassin: `tierWinSpread` reads
  0.189 against its 0.15, on the Ravager, where Psycho Rush's sides won 0.885 of 52 and Tornado's 0.500 of 34.
  The same pair read 0.101 on `5708132d` (0.821 of 56 against 0.542 of 48), so the Ravager was already the
  package nearest the bound. Matches 10.8 rounds.
