# 0178. Cut a recorded match at the top of a round, and replay it into a match of its own

Date: 2026-11-14
Status: Accepted

## Context

A bug seen in a match is hard to reproduce: the board at the moment it showed came out of a seed, a roster,
rules, content and every decision before it. Teaching and puzzles need the same thing: a real board to start
from, and a different choice played from there. The table already rebuilds a match from how it was created and
its decision journal (ADR 0159), on the content and rules it began on (ADR 0163). A finished match's official
result must not move, whatever is played from it afterwards.

## Decision

A **match scenario** is a recorded match cut at the top of a round: its source's creation (seed, rules, roster),
the journal entries before the round's first question, the entries after it, and both seats' boards at the cut.
The rounds offered are those the match reached with no answer held. A round whose top rebuilds is offered even
when a later line of it no longer does, because that is the round an engine change shows in. A scenario is
started by `MatchReplay`, under a new match id and a new run: created again from the source's seed and handed
the entries before the cut, so the random stream stands where it stood at the table. The match it builds is
then held to the recorded boards, and refused with `Scenario.Diverged` if they differ. Nothing is snapshotted,
nothing is written to the source, and the domain is untouched.

## Consequences

- Good: a reproduction is exact by construction. The source's own decisions after the cut, handed to the
  scenario's match, end it as the source ended (tested on the repository's content under both combat
  declarations).
- Good: no new way to build a match. A scenario goes through the same commands and gates as any replay.
- Good: an engine change is caught at the start, not halfway through a practice.
- Bad: starting a scenario replays the journal up to the cut, and finding the rounds replays it once per entry
  with a board read. That costs milliseconds for a match, and it happens when an operator cuts one.
- Neutral: the cut is at the top of a round only. A cut mid-round would hold one seat's answers before the
  other's, and its board would show something one seat had hidden.

## Alternatives considered

- A memento of the match at the cut: fast to load, but it is the way to build a match the rules did not produce,
  which ADR 0159 declined. Under another engine it would also be read without any check.
- Cut anywhere in the journal: a hidden-intent round would start with answers held, which a shared link would
  show.

## Follow-up

- `docs/tabletop/scenarios-plan.md` stages S2 to S6: the scripted opponent, `scenario.json` and the CLI, hosted
  play, the admin panel, tutorial metadata.
- Glossary: Match scenario, Scenario cut, Scenario checkpoint, Scripted opponent, Diagnosis.
