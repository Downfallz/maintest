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

- Good: **the waste the bot can avoid falls by 42 %.** `AllTargetsInvalid` goes 464 to 268 and the fizzle
  rate 0.175 to **0.124**. The remaining floor is `ActorDead`, which no choice of target can reach.
- Good: **the baseline is measurably stronger, on the reading that does not depend on the objective.**
  `exploit` — the searched agent `search-2`, unchanged and external, against Greedy — falls **0.198 to
  0.128**. `skill` holds at 0.985. This is the independent confirmation ADR 0032 used for the same purpose.
- Bad: **the content reads far worse against the stronger baseline.** The objective goes 49.32 to **77.25**,
  and almost all of it is two content targets: `tierWinSpread` 0.397 to 0.643 (24 points of the penalty) and
  `player1WinShare` 0.510 to 0.640 (10 points). A bot that wastes fewer actions makes combat more efficient,
  matches end sooner, and going first decides more. **This is a bill the next content pass inherits**,
  exactly as ADR 0032 left one, and scores either side of this do not compare term by term.
- Bad: **raising `initiative` does not buy `player1WinShare` back.** It was the obvious lever, since ADR 0032
  used it for precisely this reading, and the sweep refuses: 3.0 puts the share at 0.525 and collapses the
  agent (`skill` 0.985 to 0.730, `exploit` 0.128 to 0.525); 4.0 runs matches to the round cap. There is no
  price that fixes the share and keeps the agent.
- Bad: **`fizzle` is still not measurable.** Swept at 0, 1, 2, 3 and 5 against this agent it reads 78.44,
  77.84, 77.25, 79.52 and 77.25 — a spread of 2.3 and not monotonic. The fix works through the *zeroing* of
  a doomed target, not through the weight, so the weight still prices almost nothing. It stays at 2.0
  because no value is better than another.
- Bad: `Combat.NotEnoughEnergy` rises 12 to 30. Small against a fall of 161 overall, and **not understood**;
  it is recorded rather than explained away.
- Bad: **the benchmark digest moves**, and the agent every learned policy is measured against changes with it.
- Neutral: no weight value changes, so the fingerprint stays `1933f3ae` and every stamp still matches.

## Alternatives considered

- **Only the declaration-time reading.** Measured: objective 51.15, fizzle rate 0.161, `player1WinShare`
  0.510. It leaves the waste untouched, because targets are not chosen at declaration.
- **Only the reveal-time reading.** Measured, and *worse* than both together: objective 96.73,
  `player1WinShare` **0.725**. The weak declaration reading, which does nothing on its own, is what pulls the
  share back to 0.640. It is kept for that reason, found by measurement after being written off.
- **Ask for intents in timeline order**, on the theory that the declaration reading rarely fires because an
  earlier ally has not chosen yet. It is a no-op: `IntentRules.Evaluate` already builds its list from
  `Timeline.Slots`. Reverted, and caught by a measurement identical to three decimals rather than by review.
- **Read the enemy's plans at declaration too.** Refused there and unnecessary here: enemy intents are hidden
  until revealed, and by the time targets are bound the revealed ones are public, which is where the reading
  now sits.
- **Leave it alone and record the blind spot.** The honest option while the waste was unmeasured. It stops
  being honest once 45.6 % of fizzles are shown to be avoidable.

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
