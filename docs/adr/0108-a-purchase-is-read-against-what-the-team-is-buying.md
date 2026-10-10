# 0108. Read a purchase against what the team is buying this round

Date: 2026-10-10
Status: Proposed

## Context

A player buys up to two packages an opportunity, for two different creatures (ADR 0066), and the picks are
revealed together (ADR 0089). The heuristic agent read each pick alone: a package was worth what its best spell
adds over the best spell of its kind the creature knows (ADR 0102). The second pick of a round therefore valued
the package the first had just taken exactly as highly as the first did. At round 1 Brute's Guard reads 6.70
against 6.15 for Shock or Poison Slash, on its permanent defense, so Greedy opened on two Brutes in every one of
800 mirrors, and lookahead-39, whose rollouts play the same reading, in 588 of 600 round-1 picks.

Forcing the opening against that Greedy, on the same 800 matches (seeds 1 to 400, both seats): one Warped beside
one Brute scored 70.2 % (67 to 73 %), one Predator beside one Brute 49.4 %, two Predators 43.8 % and two Warpeds
9.9 %; two Brutes, the control, 50.0 %. The opening Greedy chose was not the best one it could have chosen, and
what it missed was the team: the second pick's worth depends on the first.

## Decision

The heuristic agent reads a package against the spells its player's picks of this round already teach, as well
as the ones the creature knows: a spell one of those picks teaches adds nothing, and one of its kind has to beat
it, as one the creature knew would. Nothing else of the purchase reading changes. The lookahead's purchases are
played by rollouts whose picks the heuristic agent makes, so they read the same way.

## Consequences

- Good: against the Greedy before it, this Greedy scored 66.5 % (63 to 70 %) on the 800 matches above and 62.1 %
  (59 to 65 %) on 800 from seed 300000, which no reading here was chosen on. It opens on one Brute and one
  Warped in every mirror.
- Good: its mirror is shorter and stalls less: 10.4 rounds against 12.3, 2.8 % at the round cap against 6.2 %
  (seeds 5000 to 5399), and the Warped family is played: 1 037 of about 2 300 creatures.
- Bad: Greedy is the tuner's mirror and the benchmark's player, so every reading the tuner takes and the
  benchmark digest move. A tuning pass played before this read the content with a player that opened badly.
- Neutral: a team still reads its picks one at a time, in the order the agent makes them; the first pick is
  read as before. The opening is now always the same one, so the three tier-1 families are still not opened
  evenly: what each opening is worth is measured on its own (ADR 0109).

## Alternatives considered

- Read a pick against every spell an ally knows, not only what the round buys: it scored 60.4 % against the old
  Greedy, below this, by treating a spell an ally can cast as one this creature can.
- Force a varied opening, or a random one: it plays worse on purpose, and the tuner would read content on
  matches decided by a dice roll at round 1.

## Follow-up

- The benchmark digest is rewritten for the new Greedy.
- The tuner reads what each tier-1 opening is worth (ADR 0109).
