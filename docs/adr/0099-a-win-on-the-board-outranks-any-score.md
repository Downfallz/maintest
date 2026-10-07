# 0099. Let a win on the board outrank any score

Date: 2026-10-07
Status: Proposed

## Context

The owner, playing the table against the bots, saw them able to kill the last creature and spend the round on
Wait, Noxious Cure or Guard instead. Replayed bot matches showed two causes. `ActionScorer` has no notion of a
win: to Greedy and every heuristic weights file, killing the last enemy is one kill among others, and a guard
that denies a kill or a heal on three allies can score more. The lookahead does rank a round that wins above any
score (ADR 0047), but only on its guess of the hidden slots. When an ally's cast was guessed to end the match,
every candidate won on the guess, and the score picked one that spent the slot on something else. The win then
rested on a single attack, and was lost when the enemy acted first and mended itself or raised its defense.

## Decision

A cast that ends the match outranks any score. `ActionScorer.Wins` says whether an action alone, on a plain roll,
leaves none of the actor's enemies standing. The scorer's `Best` takes a winning target set before the best
score. The heuristic agent's declaration, and the lookahead's guesses of an ally or an enemy, take a winning spell
before any score. Every enemy counts, even one an ally is expected to kill first: if the ally's kill lands the
match is over and the round after it never comes, and if it does not, this cast is the win. A win only a critical
would bring is a chance, and the score keeps pricing it as one. Among candidates that win on the lookahead's
guess, the one whose round still wins against the enemy's worst replies goes first. That is read only when two
candidates tie on a win, so it costs a playout per enemy spell at the end of a match and nothing elsewhere.

## Consequences

- Good: in 240 replayed matches (lookahead-34 against Greedy in either seat, and against search-23), the
  heuristic agents' misses, a Guard or a heal declared beside a lethal Heavy Strike, became lethal declarations.
  The lookahead's remaining misses are rounds the enemy, acting first, made unwinnable.
- Bad: Greedy changes, so the benchmark digest is regenerated, and every reading the tuner takes moves with it.
- Neutral: weights files are unchanged. The win is not a term, so no search can trade it away.

## Alternatives considered

- A win term with a large weight: a weights file only has to be finite, so no weight could be trusted to dominate,
  and a search could price the win below a guard.
- A win chance, the plain win and the critical one mixed: a 5 % critical that wipes two enemies then outranked a
  certain kill. A chance belongs in the score, which already weighs a kill by the critical chance.
- Keep ADR 0039's reading for the last enemy, leaving a kill an ally already holds: it saves a round that never
  comes when the kill lands, and loses the match when it does not.

## Follow-up

The benchmark digest, `docs/learning/journal.md`.
