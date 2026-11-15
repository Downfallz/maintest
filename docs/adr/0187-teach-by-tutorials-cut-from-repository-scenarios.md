# 0187. Teach by tutorials cut from repository scenarios

Date: 2026-11-15
Status: Accepted

## Context

A new account gets phase tips (ADR 0140) and nothing that teaches by playing. The rule words a player meets
first, Defense, initiative, Energy, the fizzle, are explained only in the rulebook. The table already plays a
real board from a recorded round, with a scripted opponent and a briefing (ADR 0179, scenarios-plan.md S2 to S7),
and the repository's scenarios are rebuilt by a test. The owner settled the shape on 2026-11-15
(docs/tabletop/onboarding-plan.md): four short tutorials; the first opens at a new account's first visit; the
questions a lesson does not teach yet are played for the person; a tutorial is done when its last step is shown
and its round ends; signed-in accounts first; the concept cards in the tutorials only.

## Decision

We will build a **tutorial** as a repository scenario with a lesson block, not as a new way to make a match. A
file under `scenarios/tutorials/` adds a `tutorial` block to an ordinary scenario file: its order, its title, and
its **lesson steps**. A step names the question it shows at: its kind, round and creature. It holds two lines of
text at most, opens **concept cards**, and says what it points at. It shows by the question asked, never by an
exact board. The lesson names the question kinds it teaches. Every other kind is an **assisted question**. The
person's seat is answered from the source's recorded answers for that seat. Each answer is checked as the
scripted opponent checks its own: same kind, same creature, among the options. At the first answer that does not
fit, the question goes back to the person for good. The opponent stays the scripted opponent of ADR 0179. In a tutorial its steps take
the phase tips' place; every other game keeps the tips as they are. A tutorial is done when its last step was
shown and its round ended, or the match ended, win or lose. The account keeps which tutorials are done and which
were skipped, with its game settings (ADR 0140); a skipped tutorial never opens by itself again. A tutorial is
offered to signed-in accounts only, as a practice is today.

## Consequences

- Good: a tutorial is held by `ScenarioFileTests` like every repository scenario: it rebuilds on the current
  engine, its recorded answers reach every step, and every concept it names exists. A rule change that breaks a
  lesson fails the build, not a new player.
- Good: no new way to build a match and no domain change. The person's seat is answered by the same check the
  scripted opponent already uses.
- Good: the first lesson starts in combat and asks only spells and targets; the next ones hand over one question
  kind each.
- Bad: a lesson is tied to its recorded round. An engine change that moves the board means cutting the file
  again with `scenario-generate` and rereading its steps.
- Bad: guests get no tutorial until a later plan gives them a practice.
- Neutral: a person who plays something the lesson did not expect is not stopped. The opponent hands over to its
  bot, an assisted question that no longer fits is handed back, and the steps still show by the question asked.
- Neutral: the concept cards' text lives in the table's pages, summarised from the rulebook, which stays the
  source.

## Alternatives considered

- A tutorial engine with its own scripted boards: a second way to build a match the rules did not produce, which
  ADR 0159 and ADR 0179 declined.
- The person answers every question, helped by the phase tips: a first lesson would ask Evolution, Tempo and Tie
  order before a single spell, the opposite of starting in combat.
- One long tutorial of a whole match: too long for a first visit, and nothing to replay one idea at a time.
- Success conditions (win the round): a person who loses would be stuck, and scenarios-plan.md S6 left them for later.

## Follow-up

- `docs/tabletop/onboarding-plan.md` stages O2 to O5: the `tutorial` block in `ScenarioFile`, the hosted
  tutorials and the assisted seat, the coach and `table/concepts.js`, the account's progress and the first visit,
  and tutorials 2 to 4.
- Glossary: Tutorial, Lesson step, Concept card, Assisted question.
