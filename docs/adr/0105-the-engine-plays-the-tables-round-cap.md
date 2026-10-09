# 0105. The engine plays the table's round cap of twenty

Date: 2026-10-09
Status: Accepted

## Context

The round cap is a rule-set value (ADR 0011). The table has played to 20 rounds since its Round track was built
(docs/tabletop/plan.md), and ADR 0086 kept it there when it set the designed length at 8 to 14 rounds. The
engine's `RuleSet.Default` stayed at the prototype's 30, and every simulation, benchmark digest, tuning pass and
weight search reads it. So the bots were measured and tuned on a game the table does not play: in the
lookahead-39 mirror, 18 matches in 60 went past round 20, and 72 of the 400 Greedy mirror matches of the
benchmark did (2026-10-09). The owner asked for one cap.

## Decision

We will set `RuleSet.Default`'s round cap to **20**, the table's. A match still standing at the end of round 20
is decided by total remaining Health, as before (ADR 0011). `docs/tabletop/playtest.rules.json` already says 20
and does not change.

## Consequences

- Good: the simulator, the tuner, the weight searches and the table play one game. A stalled match costs 20
  rounds of a batch, not 30.
- Good: what the objective calls a stall (`roundCapShare`) is what a table would see end on the Health count.
- Bad: every committed policy under `models/` is stamped with a round cap of 30 and is refused under the new
  rules until it is trained again. The owner accepted that (2026-10-09); the next turn of the loop replaces them.
- Bad: every benchmark digest, tuning score and weight search before this is on another rule set. The journal
  entry of the day scores the content on both sides.
- Bad: the Health count decides more matches. A side that banks Health instead of trading can win at the cap
  where at 30 it had to finish the job; `roundCapShare` and the length band watch for it.
- Neutral: the weights files carry no rule-set stamp and still load; they were searched at 30.

## Alternatives considered

- Keep 30 in the engine and 20 at the table: the two keep measuring different games.
- Pass the table's rule file to every tool: every command, workflow and search would need `--rules`, and any
  one that forgot would measure the other game.
- A cap of 14, the top of the band: it would decide by Health matches the band allows to run long.

## Follow-up

- `src/DownfallArena.Domain/Matches/RuleSet.cs` and the tests that read the default.
- A benchmark digest for the content under the new rules.
- `docs/domain/game-rules.md`, `docs/tabletop/plan.md` and `docs/tabletop/components.md` where they quote
  `RuleSet.Default`. The audits that quote 30 as it stood when they were written keep it.
