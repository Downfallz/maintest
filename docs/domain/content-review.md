# Content review: an adversarial reading of the game as it stands

Status: **review only** (2026-10-06). Nothing here is decided and no content, rule or number moves with this
file. It is what one careful, hostile reader found when asked "is this balanced, is this fun, is anything
broken, and is the physical playtest the next step". Read it as a list of hypotheses to test at the table,
ranked by how much each would cost if true, not as a list of defects to fix before anyone plays.

Content `9419f935`, engine `6e3d1cb`, the default rule set (3v3, 2 energy a round, 2 picks at rounds 1, 3,
5, ..., round cap 30, critical multiplier 2) unless the table's rule set (`docs/tabletop/playtest.rules.json`,
round cap 20) is named. Every number below was measured in this session with the commands in the appendix,
or is quoted from `docs/learning/journal.md` with its date. **Every measurement is bot play**: the agents
are a strong instrument for length, economy and dominance, and a weak one for feel. Where a finding is about
feel, it says so and names the question a table answers.

## 1. The short version

The game is in better shape than its author's doubt suggests. The things that usually kill a tactical card
game at this stage are measured and inside their bands: a match lasts 10 to 11 rounds and ends by elimination
rather than at the cap, the first seat wins half, a bot that plays well beats one that plays at random every
time, the energy economy binds, every spell is cast somewhere, and the side that loses the first creature
still wins about three matches in ten. The balance tooling around it (an objective with confirmation seeds,
an exploiter panel, forced-package experiments, a benchmark digest that fails CI) is beyond what most
published games ever get.

Four things are worth worrying about, in this order:

1. **Armour.** The strongest hand-written agents converge on stacking defense to the ceiling of 10 and
   waiting for bleeds. Bleeds ignore defense and they do win those matches, so armour is not dominant; it is
   slow. One defensive player turns an 11-round game into a 22-round one while losing, and at the table's
   cap of 20 more than half of those matches are decided by the health tiebreak. A person finds this line
   in an evening. This is the one finding that can make a playtest report "it was long and nothing happened".
2. **The Speed card is a tell.** Twenty of the 44 spells cannot roll a critical, so Quick costs them nothing
   and Standard announces a critical spell. With the deterministic agents, Standard means "a spell that can
   crit" 95 times in 100. Speed is simultaneous, so nobody answers a Standard with a Quick; the answer is in
   the intents and targets chosen after the reveal. Whether that is a poker layer or a non-decision is a
   question for the table.
3. **Matches are decided early and played out, in Greedy self-play.** There the eventual loser is never
   ahead again after 38 % of the match, one match in five has no lead change, and seven rounds are played
   after the first death. The owner's own matches against the lookahead say comebacks happen; the appendix
   reads the same numbers under the lookahead, and the table decides which reading holds. Concession exists;
   a comeback valve does not.
4. **The three families share their verbs.** Each has a heal, a stun and a sweep; what differs is how each
   says them, and whether that is legible at a table is the question. One line (Oppressor, Tyrant) has no
   theme at all; Greedy never buys it, the lookahead buys it often.

None of these needs a new mechanic, and the owner's follow-up on the pull request (2026-10-07) goes further:
the core is frozen for the physical alpha, and a mechanic is authorised only when a playtest names the
problem it solves. If one were added later, the review's candidate is **asymmetry at setup** (a second
creature definition or a draft of the opening package), not positions, passives or a new resource. The
physical playtest is the right next step, with three things done first: snap the seven off-grid critical
chances to the d20, decide whether armour is tested at the table before anything bounds it, and print the
lighter kit. Section 5 lists the hypotheses the first sessions should be designed to answer, in the owner's
priority order, and F8 adds the one the owner raised after reading this review: late Evolution picks that
may be compulsory clutter.

## 2. What the evidence says is in good shape

| Reading | Value | Source |
| --- | --- | --- |
| Length, Greedy mirror, 200 benchmark seeds | 11.4 rounds on average, median 10, 90th percentile 16; 0.5 % draws, 0 % at the cap | this session |
| Length, exploring self-play (explore:0.2), 300 matches | 11.1 rounds, 10th to 90th percentile 8 to 15 | this session |
| Seat | Player 1 wins 98 of 200 benchmark matches; 144 of 300 exploring matches | digest `9419f935`, this session |
| Skill | Greedy beats Random 400 of 400, in 9.3 rounds | this session |
| Energy binds | at activation the actor holds a median of 2 energy; 55 % of activations hold less than 3; 17 % hold 6 or more; Focus is 5 % of landed casts | exploring run, this session |
| Fizzles | 4.9 % of activations (exploring), 9.7 % (Greedy mirror) | this session |
| Depth is reached | 962 of 1735 buying creatures reach a tier-3 package; 4.3 tier-3 packages a match; the first at round 5, the earliest the schedule allows | exploring run |
| Multiclassing happens | 25 % of buying creatures own packages of two families (Greedy's taste); 74 to 80 % under the stronger weights | exploring run, search-23 and turtle batches |
| Comebacks exist | the side that loses the first creature wins 29 % (88 of 300); a side down to one creature against three wins 1 % | exploring run |
| The dice roll | 27 % of landed casts roll a critical and 44.5 % of those rolls hit | exploring run |
| Tier-2 packages are close | forced against Greedy on 400 confirmation seeds, every tier-2 package is within −0.05 and +0.08 of its parent alone | journal, 2026-10-03 |

Two design decisions deserve to be named as right, because they are the kind a reviewer would otherwise
propose: the critical multiplies before defense is subtracted, so armour never makes a critical pointless;
and Quick forfeits the critical, which is what makes Speed a decision at all and also gives a player a way
to opt out of the dice when the hit must land.

## 3. Findings

### F1. The armour ceiling turns strong play into a stall

**What the engine does.** A creature's defense buffs count for at most 10 together (ADR 0076). Damage is
reduced by total defense, floor zero. Four spells stack permanent defense (Brace +1, Fortress +3, Carapace +3
on itself for 1 energy, Wraithguard +3 on three allies), and nothing removes it but Contagion (−3 permanent,
up to three enemies, 1 energy, tier 3 of the Predator family). Every printed hit but Blood Price (11) is 10
or less, so a creature at 10 defense takes nothing from any non-critical hit in the game. Bleeds ignore
defense; eight spells carry one.

**What was measured.**

| Matchup | Rounds | Matches past round 20 | Hits fully absorbed | Matches with a creature at 10 defense |
| --- | --- | --- | --- | --- |
| Greedy against Greedy, exploring, 300 matches | 11.1 | 3 % | 3.8 % | 10 % |
| search-23 against Greedy, 200 matches | 12.9 (every match 12 or 13) | 0 % | **68 %** | **100 %** |
| a defense-heavy heuristic (defense weight 3.0, heal 1.6) against Greedy, 120 matches | **21.6**, 10th to 90th percentile 11 to 30 | **58 %** | 43 % | 75 % |

search-23 is the strongest one-step weights file and it wins 95 % of those matches, but the way it wins is
the point: both sides reach 10 defense, nothing dies before round 12, and then the loser's whole team dies
in rounds 12 and 13 to bleeds. The defense-heavy heuristic loses (43 wins of 120, 46 under the table's cap
of 20 with the health tiebreak), so armour is not dominant. It is something worse for a playtest: a way
for the weaker player to double the length of the match while losing. Under the table's cap, 70 of those
120 matches are settled by comparing total health at round 20.

The journal saw the same shape on 2026-09-24 with the ceiling in place: the search-21 mirror reached the
30-round cap in every match, stun-first in 58 %, search-19 in 55 %, and "10 still stops every plain hit".
The owner kept 10. Nothing since has changed what a plain hit does against 10 defense.

**The counter exists and works.** Bleeds ignore defense, eight spells carry one, and they decide every stalled
match above: the defense-heavy heuristic loses, and Greedy's casts against it move on their own to Bonewall,
Death Wail, Infestation and Void Pulse. So armour is not a dominant line. The finding is about the clock, not
the result: the counter takes until round 12 to land, and until it does two hits in three do nothing. Whether
that is a flaw is a question about the evening, not about the win rate.

**Why a person finds it.** "Buy Brute then Ironhide, cast Carapace four rounds running, and nothing but a
critical hurts you" is one sentence a player says to themselves in the first evening. The counterplay exists
(eight bleeds, four stuns, three drains, Contagion, criticals) but is spread across families, and the Brute
family has no bleed at all: a team that opened two Brutes has none until one of them buys Predator, round 3
at the earliest, and no heavy one before round 5.

**Options**, cheapest first. Each is a rule, so an ADR, a digest and a journal entry; none is a component.

- *A hit never deals less than 1.* One sentence, no component, no new number. At 10 defense a stalled board
  still loses about 6 health a side a round, so the "nothing happens" state cannot last ten rounds. It does
  not make armour bad; it makes the stall visible as a slow loss instead of a freeze. The first thing to
  measure, because it is the cheapest.
- *A critical ignores defense.* Thematic ("a critical finds the gap"), makes Standard the anti-armour choice
  and so gives the Speed decision a second axis (F2). Raises variance (F5).
- *A lower ceiling* (6 or 7). The journal measured 5 on 2026-09-24: the strong mirrors still ran 19 to 24
  rounds. Shock and Strike still bounce entirely at 6.
- *Carapace as the passive it is authored as*: once a match, +3, never recast. Removes the cheapest source;
  Fortress and Wraithguard remain and cost more.
- *Timed instead of permanent buffs.* Measured 2026-09-24: both stalled mirrors ended at about 21 rounds.
  Costs the table its "a permanent condition takes no token" rule and adds tokens. The worst option for the
  box.

What the table should record either way: the defense each creature holds at the end of each round, and how
many hits landed for zero. The traces already carry both.

### F2. The Speed card is a tell

**What the engine does.** Speed is chosen face down and revealed before intents are chosen. A Quick creature
acts before every Standard one and cannot roll a critical. Twenty of the 44 spells have a critical chance of
zero, both starting spells among them.

**What was measured.** Which spell a creature declared after each Speed choice:

| Batch | Declared a spell that can crit, chose Standard | Declared a spell that cannot crit, chose Quick | When Standard was chosen, the spell could crit |
| --- | --- | --- | --- |
| search-23 against Greedy (deterministic) | 4169 of 4188 (99.5 %) | 9801 of 10003 (98 %) | **95 %** |
| defense-heavy against Greedy | 91 % | 94 % | 91 % |
| exploring self-play (one decision in five is random) | 77 % | 79 % | 83 % |

For the agents, Speed is not a tempo decision: it is "will my spell roll". Going Standard with a spell that
cannot crit buys only the later slot, which a heal cast after the damage, a reactive ward or a target chosen
on a changed board may want; the bots' speed rule does not price that, so part of the correlation above is
the heuristic's own. Going Quick with a spell that can crit forfeits its best half. A human will lean the
same way, and a human who watches the Speed reveal learns
which of the three enemy creatures is about to cast Shock, Fury, Incinerate or Blood Price rather than
Strike, Focus, a heal or a ward. Speed is chosen face down by both sides at once, so nobody can answer a
Standard with a Quick. But it is revealed **before** intents are chosen, so the reader can act on it with the
intents and the targets: a creature already set Quick aims its stun or its kill at the one that went Standard,
and a heal or a ward goes where that creature's spell is likely to land.

**Two readings.** As a poker layer this is good design: a bluff costs something real on either side, and the
information is actionable. As a tempo decision it is thin: in the deterministic batches 58 to 69 % of timeline
slots were Quick, and the "act first" half of the trade is rarely why a player chose it. The question for the
table is whether players notice the tell and whether noticing it is fun. If the owner wants Speed to be a
tempo decision as well, the levers are: more critical chances above zero (today twenty are at zero), or a
cost to Quick other than the critical (a point of damage, a point of initiative next round) so that a
no-crit spell still pays for acting first.

### F3. Matches are decided early and played out

**What was measured**, exploring self-play, 300 matches, reading the total health of each side after every
action:

| Reading | Value |
| --- | --- |
| Lock-in: last round the eventual loser was ahead or level, as a fraction of the match | median 0.38 of the match; in 64 % of matches it is before the midpoint; in 17 % the loser is never level after round 1 |
| Lead changes a match | 1.76 on average; 22 % of matches have none |
| Winner's remaining health share of 90 | 0.45 on average; 42 % of matches end with the winner above half |
| First death | median round 4; a median of 7 more rounds are then played |
| Side that loses the first creature wins | 29 % |

Lock-in reads the total health lead and nothing else: not the energy banked, the defense held, the bleeds and
regenerations queued, the initiative or the depth of the packages. It is a health-stabilisation reading, not
a solved-position one, and a side behind on health with a Blood Price in hand is not behind. With that said,
a 10-round match in which the health lead settles by round 4 and the first death lands at round 4 is a match
whose second half looks like an execution from the outside. Elimination games usually accept this if the end is quick; here
it is six or seven rounds, which at the table is ten minutes.

**What the owner's own play says.** The owner has played the lookahead often and been surprised by it coming
back. The readings above are exploring Greedy self-play, where a losing side plays its losing position out
mechanically and a fifth of the decisions are random; a stronger player converts more of the 29 %. The
appendix reads the same metrics on traced matches of `explore:0.2:lookahead`, and the table is the reading
that counts: when people concede, and whether a match that looked lost at round 5 was. ADR 0066 compounds the loss by design: a side
with fewer living creatures has fewer picks, less energy generation, fewer actions and fewer initiative
bonuses, with nothing pulling the other way. Concession (ADR 0087) is the only valve.

**Options.**

- *Measure first.* The table records every state; when humans concede, and whether they say "it was over
  at round 5", is the reading that decides whether this matters.
- *The picks a dead creature would have had go to the survivors*: a side with fewer living creatures may
  buy more than one package on one creature in an opportunity. It reverses ADR 0066's compounding exactly
  where it compounds, and the alternative that ADR rejected ("a creature spikes two levels") becomes a
  comeback rather than an opening.
- *A dying creature's energy passes to an ally.* Small, thematic, one sentence at the table.
- *Rally*: a side with fewer living creatures gains one more energy a round per missing creature. The
  simplest to track (the round track already says who gains what) and the easiest to overshoot.

Each is measurable with what exists: the lock-in fraction, the lead changes and the comeback rate above,
read on the exploring run.

### F4. The families are near-isomorphic, and one line has no theme

**What the catalogue is.** Three families of seven packages each: an opener and three branches of two
levels, two spells a package, 44 spells. Mapped by verb rather than by name:

| Verb | Brute family | Predator family | Warped family |
| --- | --- | --- | --- |
| Single-target hit | Pummel, Fury, Wild Swing, Body Slam, Crash, Claim, Deranged Charge | Venom Claw, Bite, Ambush, Pursuit, Eviscerate, Soul Feast, Blood Price | Shock, Ice Grip, Deep Freeze, Incinerate |
| Sweep | Whirlwind (3), Dominate (2) | Quill (2), Blood Hunt (2), Contagion (3) | Emberstorm, Infestation, Death Wail, Void Pulse (3 each) |
| Stun | Crash, Crushing Stomp | Paralyzing Barb | Deep Freeze |
| Heal | Vital Surge | Toxic Mend (team), Bite, Soul Feast, Latch (self) | Revitalize, Vital Echo, Whisper, Renewal Burst |
| Armour | Brace, Carapace, Fortress, Body Slam | — | Bonewall, Wraithguard |
| Bleed | — | Venom Claw, Latch, Paralyzing Barb, Eviscerate | Emberstorm, Infestation, Death Wail, Void Pulse |
| Energy | — | Pursuit, Claim, Overdrive, Soul Feast, Blood Hunt (drains) | Renewal Burst |
| Self-cost | Wild Swing (health), Deranged Charge (defense) | Blood Price (health), Ambush (initiative) | Infestation, Bonewall, Wraithguard, Death Wail (health) |

The families do differ: Brute is armour and big swings, Predator is bleed, drain and speed, Warped is sweeps,
healing and blood-paid casts. But every family has at least one stun, one sweep and one heal, every branch
ends in "a bigger hit and a rider", and the choice between families is mostly a choice between numbers. That
is a strength for balance and a weakness for identity: at the table, "I am the Brute player" has to mean
something a player can say in one sentence, and today it is "I hit a bit harder and armour a bit more".

**The line without a theme.** Oppressor then Tyrant teaches Crash (hit and stun), Claim (hit and drain),
Dominate (hit two) and Fortress (armour): four verbs, no sentence. The knobs file's own intent for the
Mercenary ("protection through tempo") describes Crash alone. Whether it is also weak depends on who is
buying: Oppressor read −0.033 against Brute alone when forced (journal, 2026-10-03), Greedy never buys it
on its own (Crash 9 casts, Claim 13, Dominate 5, Fortress 5 in 300 exploring matches), but the lookahead
makes it the fifth most cast package of the 21 (Crash 354, Claim 160 of 9188 landed casts on the benchmark
seeds) and reaches Tyrant 120 times. So the finding is legibility, not strength. Frenzied then Ravager, by
contrast, is one sentence ("I gamble, and I pay for it") and the best tier-2 package when forced.

**Spells the bots leave on the shelf.** Under exploring Greedy, 300 matches and 13,505 landed casts: Fortress
5, Dominate 5, Crash 9, Paralyzing Barb 9, Blood Price 13, Claim 13, Soul Feast 20, Latch 21, Blood Hunt 39,
Contagion 42, Whirlwind 43, most of them tier 3 of lines Greedy does not walk. Under the lookahead, the fairer
buyer, 200 benchmark seeds and 9188 landed casts: every spell is cast, and the least are Deep Freeze 14,
Renewal Burst 20, Wraithguard 22, Incinerate 28 and Whirlwind 29. The two buyers disagree on almost
everything (Greedy casts Incinerate 492 times, the lookahead 28; Greedy casts Pummel 232 times, the lookahead
970), which is itself a reading: no spell is dead for both, and which spells look dead depends on who is
buying. Pummel is outclassed by Shock on paper, which the knobs file says and accepts, and the lookahead casts
it more than any spell but Strike.

### F5. The dice swing hard

A critical doubles the printed damage before defense. Fury deals 14 four times in five when Standard, nearly
half a creature; Blood Price deals 22 one time in two, Deranged Charge 20, Crushing Stomp 14 three times in
four with a two-round stun. Against 30 health, one d20 roll regularly moves a creature from healthy to one
hit from dead. In the exploring run 44.5 % of the 3597 rolls hit.

This is not a defect: the Quick rule lets a player refuse the dice when the hit must land, and a game with
hidden intents needs some variance so that reading the opponent is not everything. It is a feel risk: a
player who loses a creature to a 20 on a d20 in round 3 is the player who writes "the dice decided". The
table should record how many matches were decided by a single roll (a critical that killed, with the match
lost within two rounds), which the traces allow.

### F6. The table is not yet playable faithfully

Three things stand between the rulebook and a faithful session.

1. **Seven critical chances are off the d20 grid**: Pummel 0.767, Crash 0.283, Whirlwind 0.38, Incinerate,
   Void Pulse and Toxic Mend 0.33, Revitalize 0.22. The rulebook says so itself (6.7): such a card prints a
   percentage and no threshold, and "this book has no faithful way to roll such a card". The snap is a
   content pass of seven numbers, each by 0.02 at most, with a digest and a journal entry
   (`docs/tabletop/d20-criticals.md`). It should happen before the first printed deck.
2. **The handling cost.** The manifest counts 168 condition tokens in 6 kinds, 402 cards (144 with the dial
   kit), 55 sheets to print (26 with the lighter kit), and a creature can carry up to 13 tokens at once. The
   rulebook's own "what was hard to write" names four rules longer than a rule should be: the fizzle (five
   causes, two unreachable), the critical (five statements, one ordering that will be played wrong), target
   binding (two rules printed on no card), and duration ("the first countdown does not count"). The lighter
   kit in `component-options.md` addresses most of this and has one fork left (dial or deck). A first
   session with the heavy kit would measure the kit, not the game.
3. **The cap.** The table plays to 20 with a health tiebreak. Under F1, one defensive player sends 58 % of
   matches there. The tiebreak then decides the match, and the rulebook spends one line on it.

Smaller notes for the same pass: the rulebook and player aid still teach Energy regeneration and the
Initiative buff, which no card places; Carapace is typed Passive and cast like any other spell; a player's own
second pick is made against a board that does not show the first (ADR 0089 says so); and `docs/domain/spells.md`
still carries the legacy table as history, which a new reader mistakes for the catalogue.

### F7. Smaller content notes

- **Initiative is a family trait more than a package one.** The Predator line reaches 16 (Deathstalker), the
  Brute line 8 (Colossus) to 12 (Ravager), the Warped line 10 to 13. A Colossus acts last in every Standard
  band for the whole match unless it goes Quick, which is the trade intended. Worth saying on the package
  card as a line identity ("the slow line") rather than leaving it to arithmetic.
- **Energy drains are tempo, not locks.** Soul Feast and Blood Hunt drain 3 for 3 against an income of 2, so
  neither can be sustained every round on income alone; Pursuit in between makes Blood Hunt every other round
  sustainable. The agents price a drain that empties a purse as a stun (ADR 0093). Fine as designed; the
  rulebook names this as the fourth cause of a fizzle and lists the three spells; the player aid could repeat it
  beside those three cards, because that is the moment a new player feels cheated.
- **Blood Price and Death Wail can kill their caster.** Intended, and the rulebook names the draw. At the
  table it is a story; on a bot it is a fizzle statistic. Keep.
- **Round 1 is nearly scripted.** Both players hold Strike and Focus and one just-bought opener; the first
  round's decisions are which opener (3 options) and whom to Strike. That is a fine first round for a
  teaching game and a dull one for the fiftieth. Setup asymmetry (section 4) is the lever.
- **Tier 3 is a small slice of play.** Under the lookahead 8 % of landed casts are tier-3 spells, under
  exploring Greedy 13 %. The deepest packages arrive at round 5 at the earliest, a match ends at round 10 or
  11, and the lookahead prefers breadth (a second family's opener, 74 to 80 % multiclassing under the strong
  weights) to depth. The top of each line is where the catalogue's identity is loudest and where it is least
  seen. That is a pacing choice ADR 0066 accepted knowingly; the table should say whether the finishers feel
  like finishers or like cards nobody reached.
- **Shock is the catalogue's centre of gravity.** 2514 of 8782 declarations by one Greedy in the mirror (29 %),
  2758 of 13,505 landed casts in the exploring run, and the opener that carries it is bought first 87 % of the time by Greedy.
  The lookahead buys differently (ADR 0094, ADR 0095), and the owner's forced experiments show every other
  opening beats "Warped twice", so this is Greedy's taste. It is still the spell a new player will cast most.

### F8. Late Evolution picks may be compulsory clutter

Raised by the owner on the pull request after reading this review, and measurable on the traces at hand.

**The hypothesis.** A side gets two picks at every opportunity. With three living creatures they can be
spread; with two, both survivors take a package every opportunity unless their player passes. By rounds 7,
9 and 11 a creature can have finished a coherent line, so another package has to open a sibling branch, open
another family, or add to an already large spellbook. Multiclassing would then be partly a rules artefact,
"I guess I have to take something", which the owner has felt at the table around rounds 7 and 9 with two
creatures left. It would connect three observations: the high multiclass rate under the strong weights, the
small tier-3 share although tier 3 is reached, and the size of the spellbook.

**What the rules already say.** Passing is legal: a player may pass their remaining picks (`game-rules.md`,
Evolution; rulebook 5.3, the Evolution pass). The bots never pass while anything can be bought, so every bot
multiclass rate in this review overstates wanted breadth by construction. The experiment the owner proposes,
allow passing and count how often people use it, needs no rule change: the table has to say the pass exists
and record it.

**What the traces say.** Picks that land on a creature already holding a tier-3 package, packages bought at
round 7 or later that never cast one of their spells before the match ended, and the cross-family share of
picks by how many creatures the buyer had alive:

| Batch | Picks on a creature already at tier 3 | Late packages never cast | Cross-family picks, 3 alive / 2 alive / 1 alive |
| --- | --- | --- | --- |
| exploring Greedy, 300 matches, 6004 picks | 20 % | 44 % of 2508 | 4 % / 11 % / 15 % |
| search-23 against Greedy, 200 matches, 4806 picks | 33 % | 33 % of 2406 | 17 % / too few / too few |
| defense-heavy against Greedy, 120 matches, 4582 picks | 62 % | 51 % of 3142 | 3 % / 19 % / 18 % |

A package bought from round 7 on has about an even chance of never being cast, partly because the match ends
(median round 10) and partly because the spellbook already holds better spells; both say the same thing to
the player who had to pick it. And a side down a creature crosses families three to five times as often as
a whole one, which is the signature the hypothesis predicts. Passes in the exploring run: 150 of 6004 picks,
the random fifth and the sides with nothing left to buy.

**What to record at the table**, the owner's list: at every opportunity, "would you have passed this pick?"
(it is legal; the table should say so and count the passes), and "did this pick make the build clearer,
stronger, or only broader?"; the round of the first tier 3; the round of the first cross-family pick;
packages per surviving creature by round; the spellbook size at which a player stops reading all of it; and
whether a late package was taken for a concrete counter or because a pick was there.

**Fixes to keep in reserve**, the owner's, none to build now: a cap on packages per creature, a different late
cadence, a declined pick converting into something small, a "build complete" state that takes a creature out
of future picks. Each moves the snowball or the climb; the cheapest experiment is counting the passes.

## 4. Should a last mechanic be added?

Not a system. The game has ten effect kinds in play, a timeline with two speeds, hidden intents, packages
with initiative, caster costs, stun immunity and a tiebreak. Each open question in `game-rules.md` is an
ADR-sized change that would reset every measurement:

| Candidate | What it buys | What it costs | Verdict |
| --- | --- | --- | --- |
| **Asymmetry at setup**: a second and third creature definition (different health, initiative, starting spell), or a draft of the opening package before round 1 | A team the player chose; round 1 stops being scripted; two players at the same table play different games from the first pick | The data model already allows it (`Creatures/*.json`, `startingSpellIds`, `talentTreeId`); one rule (how a roster is built) and a balance pass per definition; the benchmark stays one roster | **The one to consider.** It is the only candidate that adds a decision before the first round without adding a rule to the round |
| A comeback valve (F3) | Shorter execution phases, fewer concessions | One rule, measurable with what exists | Measure at the table first |
| A hit never deals less than 1 (F1) | Ends the frozen board | One sentence | A rule, not a mechanic; cheapest fix for the biggest risk |
| Positions, range, adjacency | A board in the board game | Every spell, every component, the agents, the learning features: a different game | Not now |
| Real passives | Carapace as authored | A new place for an effect to live, in the engine and on the card | Not worth a slot alone |
| Minions as a resource | The Necromancer's bank | A second currency on the table, tracked by hand | No: the blood price already says it |
| An objective other than elimination (sudden death, a zone) | Shorter or less snowbally endings | A new win condition (ADR 0011) | Only if F3 measures badly at the table |

**The owner's call (2026-10-07):** the core is frozen for the physical alpha, and a new mechanic is authorised
only when a playtest names the specific problem it solves. The candidate above stays a candidate.

The honest answer to "should I add one more thing" is that the content is not what is thin. What is thin is
the **shape of a match for a human**: the same two blank teams every time, a first round that plays itself, a
middle that is decided early, and an armour line that can freeze the board. Those are design problems, not
content problems, and each is small. They are also the creative work the author says they enjoy, and a
playtest is what produces them in a form worth working on.

## 5. Is the physical playtest the next step?

Yes. The simulator has said what it can say: length, economy, dominance and seat are inside their bands and
have been for a week of content passes. What it cannot say is whether the Speed tell is fun, whether people
concede at round 5, whether the arithmetic of a critical through defense is played right under time, and
whether the 168 tokens are the game or the obstacle. Only a table answers those, and the project is unusually
ready to record what a table says: the `table` host writes every decision into the same traces the bots write,
the viewer reads them, and the Automa plan gives a solo opponent.

Three things first, in this order:

1. Snap the seven critical chances (F6.1). Content pass, digest, journal. One hour.
2. Decide F1 or decide to measure it at the table on purpose: one session where one player is asked to play
   the armour line. If it is as dull as the bots say, the rule change is a sentence.
3. Print the lighter kit with the colour round track (`component-options.md`), which is decided, and settle
   the dial-or-deck fork by the prototype test that document already specifies.

The owner's priorities for the alpha, after this review: handling and the spellbook's decision load first,
then late-pick pressure (F8), then the armour stall against an opponent who deliberately pivots to punish
it, then whether progression feels like a payoff despite the 8 % tier-3 share, then whether the Speed signal
is a poker layer or a trivial one, then Oppressor and Tyrant as a legibility question, and comeback feel
last, pending the lookahead traces. Two prototype constraints come with them: hidden Evolution picks must not
leak through a public physical supply before the reveal, and Carapace's `Passive` type must be clarified
before a player's comprehension of it is judged.

Then play with hypotheses, not with a questionnaire. Each row names what the table's own trace answers and
what only the people at it can:

| Hypothesis | The trace says | The players say |
| --- | --- | --- |
| The armour line is found and is dull (F1): one player turtles on purpose, the other tries to punish it as early as possible | defense at the end of each round, hits for zero, matches past round 14; whether a human pivot collapses the stall back to 11 rounds or it still takes 18 to 22 | "nothing happened for five rounds" |
| Late picks are compulsory clutter (F8) | packages bought at round 7 or later that never cast; cross-family picks by living creatures; passes | "would you have passed?" at each opportunity |
| Players read the Speed tell (F2) | Standard chosen with a non-crit spell (a bluff); stuns, kills and wards aimed by creatures already Quick at the ones that went Standard | whether they noticed |
| Matches are over before they end (F3) | lock-in round, concession round, lead changes | the round they felt it was over |
| The dice decide (F5) | criticals that killed, matches lost within two rounds of one | "a 20 took my creature" |
| A round takes under two minutes (F6) | not the trace, which carries no clock, but `notes.jsonl`, which the table writes beside it: each decision's `at` and `elapsedMs`; a round's duration is the span from its first decision to its last | where the hands slowed down |
| The families feel different (F4) | packages bought, multiclass rate | one sentence per family, asked after the match |

Record every session (`--record`, `--who`), and read the first ten with the same scripts the bots are read
with before changing anything. One session is a sample.

## 6. What this review did not do

- No human played. Every number is bot play, and the bots have known tastes (Greedy opens Warped 87 % of the
  time; the strong weights stack armour). The variety run with the lookahead behind it is the fairest reading
  the project has and is in the appendix.
- No content number was moved and no candidate was measured against the objective. Each option above is a
  hypothesis with the measurement that would test it, not a result.
- The Automa, the hosted table and the learning loop were read, not reviewed. The app roadmap and the
  playtest-app specification were not re-read against the code.
- The legacy prototypes were not consulted.

## Appendix: the measurements

All on content `9419f935`, engine `6e3d1cb`, from a clean build.

```bash
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst
# the four readings the objective is built from, by hand
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 greedy --p2 greedy --seeds benchmarks/benchmark-seeds.json
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 greedy --p2 random --seeds benchmarks/benchmark-seeds.json
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 explore:0.2:lookahead --p2 explore:0.2:lookahead --seeds benchmarks/benchmark-seeds.json
# traced batches the per-round readings were taken from
dotnet run --project src/DownfallArena.Cli -- simulate --matches 300 --seed 500001 --p1 explore:0.2 --p2 explore:0.2 --record runs/review/rec/explore --traces 300
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 700001 --p1 heuristic:learning/weights/search-23.json --p2 greedy --record runs/review/rec/s23 --traces 200
dotnet run --project src/DownfallArena.Cli -- simulate --matches 120 --seed 900001 --p1 heuristic:runs/review/turtle.json --p2 greedy --record runs/review/rec/turtle --traces 120
uv run --project learning check-knobs
```

`runs/review/turtle.json` is not in the repository (`runs/` is ignored): write it first. It is Greedy's weights
with `defense` at 3.0 and `heal` at 1.6:

```json
{ "damage": 1.0, "kill": 5.0, "heal": 1.6, "stun": 3.0, "bleed": 0.8,
  "defense": 3.0, "energy": 0.3, "initiative": 2.1, "pressure": 0.0 }
```

The per-round readings (Speed
against the spell declared, energy at activation, lock-in, lead changes, defense held, hits for zero, the
result under a cap of 20) were computed from the traces by a scratch script that reads
`CombatActionResolved` frames and `SpeedChoiceSubmitted` and `IntentSubmitted` events; it is not committed.

### The Greedy mirror, 200 benchmark seeds

Rounds 11.4, draws 0.5 %, cap 0 %, fizzles 9.7 %, 24.5 % of actions critical. The most cast: Shock 2514, Strike
1434, Ice Grip 861, Deep Freeze 753, Incinerate 535, Vital Echo 464, Quill 338, Whisper 333. Defensive
spells read as cast by losing sides (Brace 3.7 % won-cast, Bonewall 0 %, Carapace 9 %, Body Slam 21 %),
which is correlation: a side casts a ward when it is losing.

### Skill, 200 benchmark seeds

Greedy 400 of 400 against Random, 9.3 rounds, Random's remaining health 0.

### Variety, 200 benchmark seeds, `explore:0.2:lookahead` both sides

Rounds 11.1, draws 0, cap 0 %, fizzles 7.1 %, 19.4 % of actions critical, entropy 4.51 bits against the Greedy
mirror's 3.57. 9188 landed casts, counted once. Every one of the 44 spells was declared by eight sides or more.

| Spells of | Landed casts | Share |
| --- | --- | --- |
| the starting kit (Strike, Focus) | 2082 | 23 % |
| tier-1 packages | 3681 | 40 % |
| tier-2 packages | 2649 | 29 % |
| tier-3 packages | 776 | 8 % |

Top-spell share per package, worst first: Blighted 0.86 (Toxic Mend 302, Overdrive 48), Ironhide 0.75 (Body
Slam 242, Carapace 82), Transcendent 0.75 (Void Pulse 61, Renewal Burst 20), Warped 0.72 (Shock 729,
Revitalize 281), Frenzied 0.71 (Fury 424, Wild Swing 170). The best splits are Parasite, Ethereal and Stormborn
at 0.51. Read as the objective reads it, the lower Wilson bound against 0.8, only Blighted is over, which is
the package the forced experiments flagged (journal, 2026-10-03).

Least cast: Deep Freeze 14, Renewal Burst 20, Wraithguard 22, Incinerate 28, Whirlwind 29, Blood Price 31,
Death Wail 31, Contagion 36, Soul Feast 38, Emberstorm 39. The lookahead's purchases are the reverse of
Greedy's: Brute spells 1548 casts, Predator 1123, Warped 1010, and the Wizard's top package, Cataclysm, is the
least reached of the 21 (42 casts in 200 matches) where Greedy casts Incinerate and Deep Freeze more than
anything but Shock and Strike.

Won-cast, the share of a spell's landed casts made by a side that won: Bonewall 26 %, Wraithguard 33 %,
Fortress 37 %, Infestation 39 % and Body Slam 39 % at the bottom; Renewal Burst 100 % of 16 sides, Deranged
Charge 87 %, Contagion 86 %, Blood Price 82 % and Incinerate 80 % at the top. Correlation, not cause: a side
casts wards when it is losing and finishers when it is winning.

### The strongest play, 40 benchmark seeds

The lookahead on `lookahead-34`, the strongest agent the project has, against `search-23`, the strongest
one-step weights, on the first 40 benchmark seeds, both seats: the lookahead wins 74 of 80 (92.5 %) in 14.8
rounds on average, no draw, nothing at the cap, with 62 health left against 4.5. search-23 plays its armour
line (Brace 826 declarations, Vital Surge 658, Carapace 409, Focus 290, Infestation 219) and the lookahead
answers it with bleeds, armour of its own and stuns (Brace 497, Venom Claw 469, Infestation 301, Focus 206,
Fury 173, Crash 162, Death Wail 158, Strike 155). Carapace and Vital Surge are cast by the losing side 76 %
and 84 % of the time here. It is the F1 matchup with a better player in the aggressor's seat: the stall is
broken, and it still takes 15 rounds.

The same lookahead against Greedy on the same 40 seeds: 73 of 80 (91.2 %) in 10.2 rounds, no draw, nothing
at the cap, 50 health left against 2.9. Here the lookahead plays a Brute and Predator game (Strike 326
declarations, Toxic Mend 256, Brace 227, Quill 193, Crash 117, Focus 112, Venom Claw 99, Fury 91) against
Greedy's Shock 549, Strike 374, Bonewall 293 and Death Wail 147. The strongest player beats the baseline in
the length the game is designed for, and beats the armour player in 15 rounds: the clock of F1 is what the
best play available pays, not only what Greedy pays.

Pending, replayed after a container restart interrupted the first batch: 40 traced matches of
`explore:0.2:lookahead`, read with the lock-in, comeback, depth and late-pick scripts above.

### `check-knobs`

Thirteen findings, none new: the spells it reads as unable to become a choice inside their bounds are
Overdrive, Ambush, Bonewall, Claim, Contagion, Pursuit, Blood Hunt, Pummel, Wild Swing, Renewal Burst, Whisper,
Soul Feast and Infestation. The reading prices a spell without a board, so it undervalues drains, debuffs,
energy and armour by construction (the knobs file says so spell by spell); the evaluations above are the
reading that counts.
