# 0039. The bot binds its targets on a board that has not happened yet

Date: 2026-09-14
Status: Accepted

## Context

[ADR 0038](0038-a-wasted-action-is-a-fizzle-whatever-wasted-it.md) renamed the `fizzle` weight and left the
real problem standing: the agent cannot see the waste it is about to cause, so the term reaches no decision.

Where that waste actually is was measured rather than guessed. Over 200 greedy mirror matches, **1017 of
5796 resolutions fizzle**, and the reasons are not evenly spread:

| reason | count | share |
| --- | --- | --- |
| `Combat.ActorDead` | 490 | 48.2 % |
| `Combat.AllTargetsInvalid` | 464 | 45.6 % |
| `Combat.ActorStunned` | 51 | 5.0 % |
| `Combat.NotEnoughEnergy` | 12 | 1.2 % |

`ActorDead` is not a decision: if a creature dies before acting, every spell it could have declared is lost
alike. `AllTargetsInvalid` is, and the reason it happens is structural. **`RevealAndTarget` is a whole
sub-phase before `ActionResolution`**: every creature binds its targets first, and only then does anything
resolve. So a creature choosing targets is looking at the board as it stood before combat, and every
creature that dies during the resolution invalidates a target bound on it — including the ones its own team
is about to kill.

## Decision

We will give `ActionScorer` a set of creatures expected to be dead before an action lands. They score
nothing — `RemainingHealth` starts them at zero, which is what the per-target terms already read, and the
damage path skips them — and the action pays `fizzle` for the share of its targets that are in the set. The
set is empty for every reading but a decision, so nothing else moves.

The replay that fills it **stops where it would have to guess**. It carries health forward and nothing else,
which is enough for the case it exists for — attacks piling onto one creature — and not enough for anything
that changes whether a later action happens or lands: a stun on its actor, a drain below its cost, a heal or
a defense buff on its target, all of which `ResolutionRules.Resolve` rechecks. Rather than re-implement
`CombatExecution` against snapshots, which would put game logic in the Application layer, a creature touched
by an unmodelled outcome becomes uncertain and the first action whose actor or targets are uncertain ends
the replay. What comes back is sound but incomplete, which is the safe direction: a creature left out is an
opportunity missed, one written off wrongly makes the actor pick a worse target on purpose.

`HeuristicAgent` fills it from **public state it was already handed and never looked at**, in two places:

- **Binding targets** (`DecideTargets`) is where it pays. `PlayerBoardState.RevealedActions` holds the slots
  ahead of this one, in timeline order, with their targets already bound, for **both** teams — a revealed
  action is public, and binding order is timeline order. The agent replays them against a board it carries
  forward, on the plain roll, and reports who does not survive. This is not a guess about a hidden choice.
- **Declaring an intent** (`DecideIntent`) is weaker, because an intent carries a spell and no targets. It
  reads `Timeline` and the player's own `Intents` and can only decline a spell whose good targets are
  already spoken for.

`MatchDriver` re-reads the board between creatures in its intent loop. It used to hand every creature the
same board, captured before the first of them chose, so the player's own intents were always empty.

## Consequences

- Good: **the waste the bot can avoid falls by 56 %.** `AllTargetsInvalid` goes 464 to **206** over the same
  200 mirror matches, and total fizzles 1017 to 816. The floor left is `ActorDead`, which no choice of target
  can reach.
- Bad: **it does not play better, and this ADR must not be read as saying so.** Against `random` — the one
  opponent a code change does not move — `skill` reads 0.985 to **0.988**, which is flat. An earlier draft of
  this ADR claimed `exploit` proved the baseline stronger; **that was wrong on its own terms**. `exploit`'s
  attacker is `heuristic:search-2.json`, whose weights *file* is unchanged but which is still a
  `HeuristicAgent`, so a **code** change moves both sides of that evaluation. It is an independent reading for
  a *weight* change, which is how ADR 0032 used it, and not for this one. Corrected, it reads 0.198 to 0.200.
- Bad: **the fizzle rate turns out not to measure playing well**, which is the assumption this ADR was built
  on. It falls by a quarter and nothing that measures strength moves with it.
- Bad: **the content reads far worse against the changed baseline.** The objective goes 49.32 to **83.77**
  and `player1WinShare` 0.510 to **0.690**, from the centre of its band to well outside it. A bot that wastes
  fewer actions makes combat more efficient, matches end sooner (7.8 rounds to 6.3), and going first decides
  more. Scores either side of this do not compare term by term.
- Bad: **raising `initiative` does not buy `player1WinShare` back.** It was the obvious lever, since ADR 0032
  used it for precisely this reading, and the sweep refuses: 3.0 puts the share at 0.525 and collapses the
  agent (`skill` 0.985 to 0.730); 4.0 runs matches to the round cap. There is no price that fixes the share
  and keeps the agent. Measured against the over-predicting replay, and not re-run against the corrected one,
  because no value came close.
- Bad: **`fizzle` is still not measurable.** Swept at 0, 1, 2, 3 and 5 against this agent it reads 78.44,
  77.84, 77.25, 79.52 and 77.25 — a spread of 2.3 and not monotonic. The fix works through the *zeroing* of
  a doomed target, not through the weight, so the weight still prices almost nothing. It stays at 2.0
  because no value is better than another.
- Bad: `Combat.NotEnoughEnergy` rises 12 to 36 and `ActorDead` 490 to 548. Small against a fall of 201
  overall, and **not understood**; recorded rather than explained away.
- Bad: **the benchmark digest moves**, and the agent every learned policy is measured against changes with it.
- Neutral: no weight value changes, so the fingerprint stays `1933f3ae` and every stamp still matches.

## Alternatives considered

- **Only the declaration-time reading.** Measured: objective 51.15, fizzle rate 0.161, `player1WinShare`
  0.510. It leaves the waste untouched, because targets are not chosen at declaration.
- **Only the reveal-time reading.** Measured, and *worse* than both together: objective 96.73,
  `player1WinShare` **0.725**. The weak declaration reading, which does nothing on its own, is what pulls the
  share back. It is kept for that reason, found by measurement after being written off. Measured on the
  over-predicting replay, before the correction below.
- **Ask for intents in timeline order**, on the theory that the declaration reading rarely fires because an
  earlier ally has not chosen yet. It is a no-op: `IntentRules.Evaluate` already builds its list from
  `Timeline.Slots`. Reverted, and caught by a measurement identical to three decimals rather than by review.
- **Read the enemy's plans at declaration too.** Refused there and unnecessary here: enemy intents are hidden
  until revealed, and by the time targets are bound the revealed ones are public, which is where the reading
  now sits.
- **Leave it alone and record the blind spot.** The honest option while the waste was unmeasured, and the one
  the final numbers argue for: the waste is real and avoidable, and avoiding it buys nothing that measures
  strength while costing 34 points of objective. This ADR is accepted for the correctness of the model and
  **not** for a gain in play; a reader who wants the gain will not find it here.

## Follow-up

- `ActionScorer` (`Expected`, `Best`, `Score`, `Kills`, `RemainingHealth`, `Damage`), `HeuristicAgent`
  (`DecideTargets`, `DecideIntent`), `MatchDriver`'s intent loop.
- The benchmark digest for content `91da955c`, regenerated.
- `docs/learning/agents.md`: the term table, the `fizzle` row and the decisions section.
- **A content pass against this baseline**, which owes `tierWinSpread` and `player1WinShare` an answer the
  agent weights cannot give.
- `models/` when a policy is next trained: they load, but their win rates are against an agent that is gone.
- The fourth way an action comes to nothing, named by the maintainer and still unpriced: **the actor stunned
  between declaring and acting**, 5 % of fizzles today.
