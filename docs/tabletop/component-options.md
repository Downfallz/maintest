# Component options: a lighter box

Status: **Draft** (2026-10-04). A design proposal, not a manifest. It records what the maintainer has
decided about the physical box, what he is still choosing between, and the evidence for each, so the next
pass on [components.md](components.md) and [rulebook.md](rulebook.md) can be made from it. Read at content
`e6f72578` and the table's rule set (`docs/tabletop/playtest.rules.json`). The counts of the catalogue and the
starting kit are re-read at `3c9eb083` (2026-10-06), where Basic Attack is gone and every Creature starts
with Strike and Focus; each line that moved says so. Packages and Spells carry the names of `9419f935`
throughout ([package-renaming-plan.md](../domain/package-renaming-plan.md)), also in readings at earlier
content: the ids did not move, and the lines a name's length moves are re-read there. The bot matches were not re-run: they are read at
`e6f72578`, with Basic Attack in every hand.

**Not re-read for the Capstones** ([ADR 0101](../adr/0101-a-capstone-package-buys-a-passive-not-a-spell.md),
`49c96577`, 2026-10-07). Every count of Tiers here is the 21 before them. What they move, without a re-run:
the package cards are 24 x 6 = **144** on 16 sheets (3.6, 3.11, 5); the families are eight, not seven, each
closed by a Capstone any of its level-3 Tiers opens (6, item 3: Titan for Brute, Archmage for Warped, Apex
for Predator); the named dial's outer ring gains three cells, 25, for packages that teach **no Spell**, so
`A` and `B` on a Capstone are never legal (4.1, and Part 8, question 1); and a Capstone card prints a Passive
read during play, so a cascade must leave it uncovered ([components.md](components.md) §3.7). The bot
matches of Part 5 predate the Capstones, so their copy counts say nothing about level 4.

## 1. Status and scope

The maintainer's goals: a fast setup, less upkeep a Round, fewer components, and nothing a player has to
remember or flip a card to read.

| Part | Status |
| --- | --- |
| [2](#2-decided-the-colour-round-track-and-a-dock-that-never-slides). The colour Round track and the colour dock | **Decided** |
| [3](#3-the-hybrid-kit-piece-by-piece). The hybrid kit, piece by piece | Proposed |
| [4](#4-the-spellbook-and-the-intent). Dial or deck | The one fork left |
| [5](#5-package-copies). Package copies | Measured, open |
| [6](#6-proposals-from-emmerlaüs). Borrowed from *Emmerlaüs* | Questions for the director and the mathematician |
| [7](#7-downstream-edits). Edits to components.md, rulebook.md, player-aid.md | Listed, not made |
| [8](#8-open-questions-in-priority-order). Open questions | With what settles each |

**No rule is written here.** Where a component would need one, it is a question for the `boardgame-director`
(Part 8). Counts are tagged as in [components.md](components.md#how-a-count-is-marked): **RULE** moves only
with a rule, **VALUE** with a `RuleSet` number or content, and a move is a reprint.

**The measurements.** 200 bot matches a pairing, seeds 1 to 200, read from their traces. They use the
**engine's default rule set** (Round cap 30): `simulate` reads no `--rules` file, only `table` does
(`GameSession.cs`), so where a pairing runs past Round 20, the table's cap, the text says so. A **move** is
one marker, token or card moved by hand, counted as the current rulebook lays out the Round. The **realistic
profile** is `search-31` against `greedy`, the pairing whose matches look most like a person's:

```bash
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 \
  --p1 heuristic:learning/weights/search-31.json --p2 greedy --record <dir> --traces 200
```

A trace is about 16 MB. `--matches 10` from `--seed 1`, `11`, ..., `191` records the same 200 matches ten
at a time (match *i* plays seed `BaseSeed + i`, `SimulationScenario.SeedOf`). The analyzer that reads them is
a scratch script, not in the repository.

---

## 2. Decided: the colour round track and a dock that never slides

### 2.1 The rule, as a player reads it

The **Round track**'s 20 spaces are coloured in a cycle of four, each colour with a glyph: **1 amber ●,
2 teal ▲, 3 plum ■, 4 moss ◆**, 5 amber, and so on to 20 moss. The odd spaces keep their pick mark. Each
Creature's **Condition dock** has four lanes, one a colour, printed in the same order.

- **Apply.** A timed Condition of Duration N goes in the lane **N colours after the colour the Round marker
  stands on**: the colour of Round r+N. A Stun still puts its second token in the Speed slot.
- **Cleanup.** **Empty the lane of the colour the Round marker stands on.** A Stun token leaving it from a
  living Creature leaves an Immune token in the **next** colour, and the Speed slot's Stun token comes off.
- A **permanent** Condition takes no token, as now.

The rulebook's own examples, replayed. **Crushing Stomp** in Round 5 (amber), `Stun, 2 rounds`: plum
(Round 7). Stunned in Rounds 6 and 7; plum is emptied at Cleanup 7; the Immune token goes in moss (Round 8)
and leaves at Cleanup 8. **Infestation** in Round 5, `Bleed 2 a round, 3 rounds`: moss (Round 8); it ticks
at the start of Rounds 6, 7 and 8 and leaves at Cleanup 8. Both are what §5.9 and §6.5 say today.

### 2.2 Why it is exact

- A Condition applied in Round r with Duration N is active for the rest of r and N full Rounds, and expires
  at **Cleanup r+N**: its first tick only clears `Fresh` (`Condition.Tick`; rulebook §6.5). Its lane is
  emptied at Cleanup r+N and at no Cleanup before, because the colours of r, r+1, ..., r+N-1 all differ from
  the colour of r+N while **N < 4**.
- Nothing applies a Condition outside Combat (§6.5), and the Round marker stands on r all Round (it advances
  at Finalization, §5.10).
- **Stun immunity.** `Creature.TickConditions` counts the immunity down first, then ticks the Conditions,
  and sets it to `StunImmunityDuration` = 1 when a Stun expires on a living Creature. A Stun that leaves at
  Cleanup r makes the Creature immune through r+1, ending at Cleanup r+1: the next colour (ADR 0072). No Stun
  lands while the Creature is immune, so an Immune token never meets a Stun token in a lane.
- **Nothing moves once placed.** Every effect at `e6f72578` has `stacking: null`: every kind stacks and a
  Stun is ignored, so no Condition refreshes and no token changes lane.
- **The longest Duration is 3** (`summon_minions`' Bleed, `latch`'s Bleed and its caster Regeneration,
  `shield_bash`'s caster Defense buff):

  ```bash
  python3 -c "import json;G=json.load(open('data/dst/game.schema.json'));print(max(e.get('durationRounds') or 0 for s in G['spells'] for e in s['effects']+(s.get('casterEffects') or [])))"   # 3
  ```

**What breaks at 4.** A Duration of 4 applied in Round r lands in the colour of r, and Cleanup r removes it
four Rounds early. The rule beside the count is **colours = longest Duration + 1**; a Duration of 4 in
`data/` is a fifth colour and a reprint of the Round track and the Creature cards. **VALUE** (content), as
the slide dock's lane count was.

The dock prints the cycle too, so "N colours on" is counted on the dock and never runs off the end of the
track from Round 18, 19 or 20 (20 is a multiple of 4: moss, then amber again).

### 2.3 Counts and layout

| Piece | Count | The rule beside it | Tag |
| --- | --- | --- | --- |
| Dock lanes | 4 a Creature | Longest Duration + 1 | **VALUE** (content) |
| Round track | 20 spaces in 5 cycles, 10 pick marks | The table's cap; the schedule (components 3.6) | **VALUE** |
| Condition tokens | 168 in 6 kinds at `ad3e4d00` (150 at `e6f72578`), unchanged by the dock | One Round at the maximum rate (components 1.4) | **VALUE** |
| Immune tokens | 6, unchanged | One a Creature (ADR 0072) | **RULE** x **VALUE** |

**A glyph beside each colour**, on the track and on the lane: a home print is often greyscale, where teal and
moss are the same mid grey, and amber, moss and teal are what red-green colour blindness confuses.

**How big a lane is.** All the tokens in one lane leave together, so stacking loses no countdown; but the
Start reads every Bleed and Regeneration amount, so a lane must show its tokens side by side. Tokens of one
Creature in one non-empty lane, just before Cleanup:

| Lane holds | 1 | 2 | 3 | 4 | more |
| --- | --- | --- | --- | --- | --- |
| Realistic profile | 76.7% | 16.2% | 4.4% | 2.7% | never |

A lane of 46 x 16 mm, three 15 mm tokens in a row, shows 97.3% of lanes whole; a fourth overlaps the third.
Every pairing measured peaks at 4 in a lane. **A 15 mm square does not hold three tokens**: it holds one, and a
stack hides the amounts the Start reads. The brief's "at most 3 on a Creature in 92 to 97%, at most 7 to 9"
counted all of a Creature's lanes at `9659f610`; at `e6f72578` it is 85.8% and up to **13**.

### 2.4 What it saves

No slide, no `new` lane, no `new`-to-lane move. "The first countdown does not count" stops being geometry and
becomes the count itself: N colours on, from the next Round.

| Realistic profile | `9659f610` (brief) | `e6f72578` (re-run) |
| --- | --- | --- |
| Rounds a Match | 12.1 | 11.3 |
| Tokens on the table a Round: mean, p90, max | 8.7, 16, 21 | 9.9, 19, 27 |
| **Upkeep moves a Round** | **19.6** | **21.6** |
| Energy gain | 5.5 (28%) | 5.2 (24%) |
| Ticks | 1.2 (6%) | 2.0 (9%) |
| Dock slides | 6.1 (31%) | 6.3 (29%) |
| `new` to a lane | 2.65 (14%) | 3.6 (17%) |
| Expiries | 2.4 (12%) | 2.8 (13%) |
| Stun swaps; rails moved back at expiry | 0.1; 1.6 | 0.4; 1.25 |
| Dock share (slides, `new`, expiries) | 57% | 59% (67% with swaps and rails) |
| Combat moves a Round | 11.8 | 11.9 |
| Upkeep a Match | 237 | 245 |
| **With the colour dock**, a Round and a Match | 9.5 projected | **11.7 and 132** |
| Tokens placed | Defense buff 59%, Bleed 32%, Stun 8% | Bleed 41%, Defense buff 39%, Regeneration 13%, Stun 6% |

The colour dock removes the slides and the `new` moves, **46% of upkeep**. It adds no move: a token is placed
once either way. The brief's 9.5 does not reproduce: the same subtraction on the `9659f610` traces gives
**10.8**.

**A content observation, not acted on.** Some timed Conditions cost more to handle than they do. A Bleed for
1 Round is 3 token moves for one tick with the slide dock and 2 with the colour dock: `meteor`,
`poison_slash`, and the caster lines of `bone_ward`, `crazed_specter` and `revenant_guards`. A timed Defense
buff for 2 Rounds is 6 moves with the slide dock and 4 with the colour dock (place, rail, remove, rail back).
The brief's examples do not hold at `e6f72578`: `meteor`'s Bleed is 2 a round, `adrenaline_tonic` places no
Bleed, and `toxic_waves`' is 3 a round for 2 rounds; the only `Bleed 1 a round, 1 round` is `bone_ward`'s
caster line. A handling-cost term for the tuner is the `tabletop-mathematician`'s to measure (Part 8).

---

## 3. The hybrid kit, piece by piece

**Card-game style for what lives on a Creature, board-game style for what is shared, and a dedicated
component only for what card games lack**: the simultaneous hidden Intent.

### 3.1 The Creature card

**6** = 2 Players x team size 3, **VALUE**. Front: the number, a Health frame, the four colour lanes, the
spots its dice sit on. Back: `Defeated`, with no slots, so a dead Creature takes nothing (components 3.1).

**Health**: a frame of 30 cells around the card, 9 on each long side and 6 on each short one, numbered 1 to
30, **VALUE** (`baseHealth`, ADR 0068). Health 0 is the `Defeated` back, so there is no 0 cell, and a Heal
stops at the frame's end. **One marker a Creature, 6**, not 30 cubes. A clip on the edge survives a knocked
table where a cube does not, and the frame puts every cell on an edge.

**What fits, inside a 12 mm frame**, at 8 pt and 3.5 mm a line:

| Content | Size | Tarot 70 x 120 (interior 46 x 96) | 80 x 120 (interior 56 x 96) |
| --- | --- | --- | --- |
| Number and name | 10 mm high | fits | fits |
| Four lanes of three 15 mm tokens | 46 x 64 mm | fits, exactly | fits |
| Dice: Energy d20, Defense buffs d20, debuffs d10 (3.2, 3.3) | 56 x 22 mm | **no** | fits; the card is full |
| Two starting Spells, 2 lines each (three, 46 x 24 mm, until 2026-10-06) | 46 x 16 mm | fits beside the number and the lanes, 90 of 96 mm | no: the dice fill the card |
| Base initiative, tens and units rails | 70 mm long | no | no |

**A tarot card does not hold the maintainer's list.** The proposal is **80 x 120 mm**, a stocked card and
sleeve size, four to an A4 or Letter sheet with 5 mm margins: **2 sheets**. It carries the Creature's live
state; the Base initiative moves to the middle (3.4) and the starting Spells to 3.5, unless a larger,
non-standard card is preferred (Part 8, question 4).

### 3.2 Energy: a d20

**6 dice**, one a Creature, **VALUE**, in a colour the rolling d20s do not have. A d20 has no 0, so **a die
in the printed `0` box beside its spot reads 0**. Past 20, a **+20 chit** under the die: **6**, one a
Creature, so it reads to 40, the end components 1.7 derives (2 a Round for the 20-Round cap is the gain no
play refuses). Past 40, the blanks.

| Energy, living Creatures, before each Cleanup | Realistic `9659f610` (brief) | Realistic `e6f72578` | stun-first mirror `e6f72578`, matches of 20 Rounds or fewer |
| --- | --- | --- | --- |
| 0 | - | 10.3% | 0.4% |
| at most 12 | 94.6% | **85.4%** | 75.7% (brief: 66.5%) |
| at most 20 | - | 96.0% | 81.2% |
| highest | 26 | **43** | 48 (brief: 31); 65 past Round 20 |

The chit is in use in 4% of realistic readings and nearly 1 in 5 of the stun mirror's: a real piece.

### 3.3 Defense: two values, not one die

**A single d10 showing total Defense loses two facts**, which is why components 3.3 keeps two un-floored sums:

1. **Buffs held past 10.** `thundering_seal` (+3 permanent, +3 for 2 rounds) and `revenant_guards` (+3
   permanent, +4 for 2 rounds) give buffs of 13 and Defense 10 (ADR 0076). When the +4 expires, buffs are 9
   and Defense 9; a die at 10 reads 10 - 4 = 6. **Realistic profile: buffs past 10 in 6.7% of
   Creature-Rounds, in 193 of 200 matches; largest buff sum 17.**
2. **The floor.** `infectious_blast` (`Defense -3, permanent`) on a Creature with no buff: Defense 0. Then
   `guard` (+1 permanent, +1 for 2 rounds): buffs 2, debuffs 3, Defense still 0; a die at 0 reads 2.
   **Debuffs above what the buffs give in 0.31% of Creature-Rounds, in 15 of 200 matches (greedy mirror:
   2.3%, 39 matches); largest debuff sum 9.**

**Resolution: two dice**, with the reading printed between their spots as on today's board:
`Defense = buffs (at most 10) - debuffs, never below 0`.

| Value | Die | Range, and past it | Count |
| --- | --- | --- | --- |
| Defense buffs | d20, its own colour; off its spot is 0 | 1 to 20, then a +20 chit (largest seen 17; 25 in the stun mirror past Round 20) | 6 dice, 6 chits |
| Defense debuffs | d10, faces 0 to 9 | 0 to 9, then a +10 chit (largest seen 9) | 6 dice, 6 chits |

**RULE**: no bound exists (ADR 0076 bounds what buffs *add*, not what is held); the ends are **VALUE**s. Three
Spells lower Defense (`infectious_blast`, `noxious_cure`, `psycho_rush`'s caster line), and both values are
non-zero together in 1.4% of Creature-Rounds, so most cards carry one Defense die or none. Two mini rails do
the same job without dice, in room the card does not have.

### 3.4 Base initiative, on a shared ladder

**The "0 to 15" track was wrong, and 34 and 40 are the ceilings at `813bb91b`.** Re-derived with
components 1.1's command: one Creature makes at most 10 purchases in 20 Rounds (ADR 0066), the 10
prerequisite-closed packages that pay most pay 30, so **Base initiative tops out at 5 + 30 = 35**. **Current
initiative tops out at the same 35** (no Spell places an Initiative buff) and floors at 0. Highest seen:
29 (realistic), 29 (stun mirror within 20 Rounds; 40 past it).

The rails do not fit the card. The proposal is a **Base initiative ladder in the middle**, 5 to 39, **6
numbered discs**, **VALUE** (team size), moved only at a Purchase reveal, at most 10 times a Creature.

| Choice | The constraint it answers |
| --- | --- |
| In the middle | No room on the card; and a pick is chosen on where a purchase puts a Creature against the enemies (ADR 0088, 0089), which a ladder shows for all six at once |
| 5 to 39 | Base starts at the definition's 5 and only grows; 39 keeps the reach of today's rails, so Blighted's bonus at its knob `max` (ceiling 36, components 3.4) reprints nothing. **VALUE** |
| 35 cells of 22 x 33 mm, 5 rows of 7, 154 x 165 mm, its own sheet | A cell holds six 10 mm discs: all six Creatures start on 5 |

Current initiative is the ladder less the Initiative debuff tokens, read once at Turn order resolution, as
today. The ordering track keeps its own 6 discs (3.8).

### 3.5 The starting Spells

**2**, **VALUE** (`startingSpellIds`): Strike, Focus. It was 3, with Basic Attack, until 2026-10-06.
The maintainer wants them on the Creature card; next to its live state they do not fit (3.1). All six
Creatures play one Creature definition, so the two are the same everywhere. Two places that cost nothing: **the back of every dial** (identical on
all six, so it hides nothing, 4.1), or the player aid. With the deck fork an Intent is a card, so the
starting Spells need hand cards again whatever the Creature card prints.

### 3.6 The package card

**126** = 21 Tiers x 6 copies, poker size, 14 sheets: one copy a Creature that could own the Tier, **RULE**
(any Creature may buy any Tier, ADR 0056), times 21 and 6, **VALUE**s. With the dial it is the only card with
Spell text, so it prints **both Spells in full**.

**It fits.** A Spell is a head line (name, cost, critical), a targeting line and 1 to 3 effect lines: 14, 24
and 6 Spells at `3c9eb083` (15, 25 and 5 at `e6f72578`; the brief's 15, 26, 4 is `9659f610`). A card is a 2-line band and two Spells: **8 to 11 lines,
at most 38.5 mm of the 78.9 mm** inside 5 mm margins. The widest line is a head line of 36
characters, two under the 38 a line holds (`Paralyzing Barb (2)` with `No critical roll`, at `9419f935`). It
was exactly 38 before the Spells were renamed (`Restorative Burst (3)`, now Renewal Burst); moving the critical
to the targeting line or an icon (6.2) leaves no line tight either way.

```bash
python3 -c "
import json;G=json.load(open('data/dst/game.schema.json'));S={s['id']:s for s in G['spells']}
n=lambda s:2+len(s['effects'])+len(s.get('casterEffects') or [])
print(sorted({2+sum(n(S[x]) for x in t['spells']) for t in G['tiers']}))"   # [8, 9, 10, 11]
```

**What a cascade band can carry.** A whole Spell on one line, in the card's words (components 2.2), is 46 to
133 characters, median 74.5: **0 of 44 fit** a portrait line of 38, 9 of 44 a landscape line of 56, at
`9419f935` (46 to 137, median 77.5, and 7 of 44 at `3c9eb083`, before the Spells were renamed; median 76, and
0 and 7 of 45, at `e6f72578`). A 10 mm band
can name the Spells, not state them. To read every Spell without lifting a card, the cascade steps by
**half a card, about 45 mm**, the band and both Spells printed in the top half:

| Tiers one Creature owns at the end, realistic | Mean 3.5 | p90 5 | Max 10 (20 Rounds or fewer) |
| --- | --- | --- | --- |
| Column, 89 + 45 x (n - 1) mm | 201 mm | 269 mm | 494 mm |

**Compact-cascade prototype.** The half-card step is the fully-readable extreme, not the only layout worth
testing. A package can instead expose a **20 to 25 mm summary band** while most of its rules text remains
under the next card. The band should be enough to scan the build at a glance:

`RAVAGER · AoE / Burst / Sacrifice · +3 Init · A Whirlwind · B Deranged Charge`

The full text of both Spells remains on the same package card below the band and can be exposed when exact
wording is needed. This deliberately trades "every line of every Spell visible at all times" for a much
shorter physical spellbook while keeping every owned package face up and immediately identifiable. At a
25 mm step, five packages occupy about 189 mm instead of 269 mm; ten occupy about 314 mm instead of 494 mm.
The prototype should test whether players actually need full rules text continuously visible once they know
their own build, or whether the summary band is enough for normal planning.

### 3.7 The pick tokens go: 4 to 0

Since ADR 0089 a pick is a package card laid face down with the Creature (§5.3). That card, or the dial of
3.9, is already the token's mark: a Creature with something face down beside it has been picked this
opportunity (`Planning.CreatureAlreadyEvolved`). The token's other job, counting a Player's picks, is the
count of what they have laid. The pick mark on the track still says when. **No rule moves.**

### 3.8 The middle

As the maintainer liked it: the **colour Round track**; the **ordering track**, Quick | Standard, its
divider and **6 numbered discs** (components 3.5); the **Base initiative ladder**; the **2 rolling d20s**;
a **token tray sorted by face**. The **Round cap marker** goes only if the table always plays to 20: the
setup table makes the cap a value (§3.1), so it stays unless the maintainer fixes it (Part 8, question 7).

### 3.9 The supply leak

**Real, and the current design has it too.** ADR 0089 hid the picks because the Player who saw the other's
won: Player 1's share went from 0.422 to 0.512 once hidden. Taking a card from a public supply sorted by Tier
shows an opponent who is still choosing which Tier you took. A second, smaller leak is in the procedure: a
face-down card laid with Creature 2 shows that Creature 2 was picked, and stopping at one card shows a pass.
The engine hides both ("Neither the pick nor a pass is shown").

**Fix, with the dial:** pick with it. Set its Tier ring, lay it face down with the Creature, and take the
package card from the supply only at the Purchase reveal; nothing leaves the supply before. With `Start` read
as "no pick" and a dial laid with every living Creature, the picked Creatures and the pass are hidden too,
but that changes §5.3's procedure: a question for the `boardgame-director` (Part 8, question 2).

**Fix, with the deck:** two private libraries of 63, three copies a Tier a Player (the team size, so a Player
never runs short), browsed behind the player aid stood up as a screen. The second leak still needs the
director.

### 3.10 Optional

- **A variant on the Creature card's back** needs `Defeated` shown another way (the card turned sideways, or
  a token over it): the back is what stops a dead Creature taking anything.
- **The cascade as a tree**: a level-2 card on its level-1, a level-3 on its level-2. The 21 Tiers are three
  trees of seven (Part 6, 3), so a prerequisite reads as "it sits on its parent". Wider than a column
  when a Creature owns two branches of one family.
- **Keep Speed separate from the Intent dial.** Speed is committed and revealed **before** the Spell Intent
  is chosen, specifically so the players choose their Spells with turn order known. Combining Speed and
  Intent on one dial would collapse two sequential decisions into one and remove that information structure.
  The 12 Speed cards therefore remain in the hybrid proposal.

### 3.11 The box, counted

| | Today (components.md) | Hybrid, dial, 3v3 | Hybrid, dial, 2v2 |
| --- | --- | --- | --- |
| Creature cards (80 x 120) | 0: 6 boards, 2 mats | **6** | 4 |
| Spell cards | 264 (270 until 2026-10-06) | **0** | 0 |
| Package cards (poker) | 126 | 126 | 84 |
| Speed cards (poker) | 12 | 12 | 8 |
| **Cards** | **402** (408) | **144** | **96** |
| Dials, with split pins | 0 | 6 | 4 |
| Condition tokens | 168 | 168 | **88**: a Multi Spell reaches at most 2, plus Death Wail's caster |
| Immune tokens, tie order chits | 6, 6 | 6, 6 | 4, 4 |
| Ordering discs, ladder discs | 6, 0 | 6, 6 | 4, 4 |
| Stat markers | 36 | 0, and 6 Health clips | 4 clips |
| Pick tokens | 4 | **0** | 0 |
| Round marker, cap marker | 1, 1 | 1, 1 | 1, 1 |
| Overflow chits | 18 | 18: Energy, buffs, debuffs | 12 |
| Blanks | 20 | 20 | 20 (not derived, components question 5) |
| Dice | 2 d20 | 2 d20 to roll; 12 d20 and 6 d10 on cards | 2 d20; 8 d20 and 4 d10 |
| **Paper**, A4 or Letter | **55** | **26** | **18** |

The 26: 138 poker cards on 16 sheets (126 alone is exactly 14), 6 Creature cards on 2, dials on about 3 (two
90 mm discs and a 50 mm one each), the middle on 2, about 232 token pieces on 2 (one sheet holds about 185 at
15 mm), the player aid on 1. **The brief's "144 cards" holds, on 18 sheets of cards, not 21; the whole box is
about 26.** At 2v2 every per-Creature count falls by a third, and the Condition supply by more: 4 slots x 2
targets for the Multi faces, plus the caster for Death Wail's Bleed 4 (168 to 88: components 1.4 at
`ad3e4d00` counts its 24 Bleed-4 faces as 6 slots x 3 targets and the caster; at 2v2 that is 4 x 3 = 12). The middle and the dial's rings do not change.

---

## 4. The spellbook and the Intent

Rejected on the way, so not proposed again: **numbered pockets** 1 to 10 a Creature with two pointer chits
(13 chits a Creature, 78 to sort; a 10 x 3 grid of tarot pockets is about 70 cm a Creature); **a dial of 23
positional slots** (it points at a position, not a name); **a face-down stack with letter chits** (the
maintainer reads his Spells every Round and will not flip cards to do it).

### 4.1 Option B: a face-up cascade, and a dial that points by name

Package cards lie **face up in a cascade** under each Creature (3.6); nothing is held but the Speed cards. The
**Intent is a dial**: an outer ring of **22 cells**, the 21 Tiers grouped by family and level plus `Start`,
**VALUE** (enabled Tiers), and an inner ring **A / B**, because a Tier teaches 2 Spells and so does the
starting kit, **VALUE**. It was **A / B / C** until 2026-10-06, while the starting kit was 3. Set it, lay it face down, turn it at the slot; everyone reads the named Spell in that
Creature's cascade. **6 dials**, **VALUE** (team size): each a base with a window, the Tier ring, the letter
disc and a split pin, about 90 mm across, its back identical on all six.

It buys: no Spell card at all, nothing to count, no leak, and the pick without the supply leak (3.9). It
costs one lookup at each reveal. And **the component stops enforcing "the Creature knows this Spell"**: a
card could only come from a hand that held it, while a dial can name a Tier the Creature does not own. (It
could also name `C` on a Tier while the ring had a `C`.) The engine refuses such an Intent at submission (`IntentRules`, `SpellNotKnown`), so no rule
says what a table does with one turned at the reveal (Part 8, question 1).

#### 4.1.1 Relative spellbook dial: S / 1-10 + A / B

A second dial layout is now worth prototyping beside the 21-name catalogue dial.

Instead of printing every Tier name on the dial, **number packages in the order that this Creature acquires
them**. The cascade itself becomes the legend:

- `1 = Colossus`
- `2 = Frenzied`
- `3 = Ravager`
- `4 = Predator`
- and so on, up to the maximum 10 purchases a Creature can make in 20 Rounds.

The outer ring then needs only **`S / 1 / 2 / ... / 10`**. The inner ring is **A / B**.
`S` points to the common starting kit: `SA = Strike`, `SB = Focus`.
The starting kit and every package teach exactly two Spells, so both letters are legal on `S` and on every
package the Creature owns. Until 2026-10-06 the ring was **A / B / C**, with `SA = Basic Attack`, and `C` was legal on
`S` alone.

Example: if package 3 in this Creature's cascade is Ravager, **`3B` means Ravager's Spell B,
Deranged Charge**. Another Creature can have a different package in slot 3; the dial is an address into that
Creature's visible spellbook, not a global catalogue.

**Why prototype it:**

- one universal dial works for every Creature and does not contain content names;
- package renames or localization changes do not force a dial reprint;
- the dial can be physically smaller and less visually dense than 22 named cells;
- the cascade remains the visible record of what the Creature knows;
- it matches how players actually think about a growing personal spellbook rather than asking them to scan
  the full 21-Tier catalogue every Intent.

**Cost:** the component still does not enforce legality. A player can reveal `9B` on a Creature that owns
only four packages, just as the named dial can point to an unowned Tier. The rule therefore still needs an
invalid-Intent verdict (Part 8, question 1). The slot number is also a new physical convention, so the package
band should print that number clearly or provide a tiny write-on/clip area.

**Speed stays separate.** This relative dial is only the Spell Intent. Speed is committed and revealed first;
the turn order is then known, and only after that do players choose their Spells. Merging Speed onto the same
dial would change the decision sequence rather than merely save components.

### 4.2 Option A: the tree on the mat, the packages in hand

Each Player's mat prints the **21-Tier tree** (3, 9, 9, prerequisites drawn as branches). Ownership is a
**numbered marker** on a Tier's cell: 10 a Creature (10 opportunities, one purchase each), **60**, **RULE** x
**VALUE**; a cell holds 3. The package cards are **in hand**, numbered 1 to 6 by Creature, and the Intent is
one played face down. Which Spell needs a convention: Spell A upright, Spell B upside down, the end toward the
opponent cast, the card turned over sideways so its ends stay put. A hand of 12 to 18 cards, two mats, and
uniform backs (14 sheets, or sleeves, components question 8), because a hand is hidden.

### 4.3 The fork: dial or deck

Everything in Part 3 works with either.

| | Dial (B) | Deck (A) |
| --- | --- | --- |
| Cards | 144 | 156: 126 package, 12 starting (2 a Creature, 6 Creatures), 12 Speed, 6 Creature; and 60 markers. It was 162, with 18 starting, until 2026-10-06 |
| Where a Player reads their Spells | The face-up cascade, the opponent's view too | Their hand; the opponent reads names on the tree |
| Declaring an Intent | Two settings, one face-down dial | One card face down, the right way round |
| The reveal | A lookup, Tier and letter to the cascade | None: the card is the Spell |
| What the component enforces | Not "knows the Spell" (Part 8, question 1) | "Knows the Spell", by the hand and the number |
| The pick, without the supply leak | The same dial | Private libraries and a screen |
| Table space | A column of 200 to 270 mm a Creature | A hand, and a mat a Player |
| Print | About 3 sheets of dials, 6 split pins | 2 mats; card backs or sleeves |

**The recommendation is the dial**: the one design where a player never holds or flips a card to read a
Spell, which is the maintainer's own constraint, and the one that also closes the supply leak. What it gives
up, the hand enforcing what a Creature knows, is a question for the director, not a reason to keep 264
cards. Settled by a prototype (Part 8, question 3).

---

## 5. Package copies

Six copies of a **package card** are needed because a bought card lies face up with its Creature as the record
that it owns the Tier (3.6), and any of the six Creatures may own the same Tier (ADR 0056). That holds with the
dial as with the deck. Six copies of a **Spell card** are needed only because an Intent is played face down from
a hand, which is what the dial removes: it takes out the 264 Spell cards, not the package copies. The most Creatures sharing one
taught Spell, at the end of a match, dead or alive (each Spell is taught by exactly one Tier, so this is also
the most copies of one package card in use):

| `e6f72578`, 200 matches | Mean | Max | Level 1 | Level 2 | Level 3 |
| --- | --- | --- | --- | --- | --- |
| search-31 vs Greedy (realistic) | 3.25 | **5** | 5 (2.5% of matches) | 5 (0.5%) | 3 |
| Greedy mirror | 5.4 | **6** | 6 (46.5%) | 6 (1%) | 4 |
| stun-first mirror | 5.95 | **6** | 6 (95%) | 6 (51.5%) | 6 (8.5%) |
| search-31 mirror | 6 | **6** | 6 (100%) | 6 (97.5%) | 6 (59%) |

**Not as in the brief** (`9659f610`): the realistic profile reaches 5, not "4, never more", and 5 at levels
1 and 2, not 4 and 3; the greedy mirror reaches 6 at level 1 in 46.5% of matches, not 18%; the stun-first
mirror reaches 6 at every level, not 5, 4, 4 (115 of its 200 matches run past Round 20, but within 20 it still
reaches 6 at every level). The search-31 mirror reaches 6 at levels 1 and 2 in nearly every match, but at level 3 in 59%, not 100%; 190 of its 200 matches run past Round 20. A deterministic mirror buys the same sequence on both sides, which is not human play, but it shows a smaller copy count is not a ceiling.

| One Creature at the end, realistic | Mean | p90 | Max |
| --- | --- | --- | --- |
| Spells known (brief: 11, 15, 17) | 9.9 | 13 | 25; 23 within 20 Rounds |
| Tiers owned (brief: 4, 6, 7) | 3.5 | 5 | 11; 10 within 20 Rounds |

**The structure holds**: 21 Tiers, 3 at level 1, 9 at level 2, 9 at level 3, each teaching exactly 2 Spells;
44 Spells = 2 starting + 42 taught, none taught twice, at `3c9eb083` (45 = 3 + 42 until 2026-10-06).

| Option | Package cards | With today's Spell cards | What it costs |
| --- | --- | --- | --- |
| Today | 126 | 264 + 126 + 12 = 402 (408 with 270 Spell cards) | |
| **1. Copies by level**: 6 a level-1 Tier, 3 a level-2 or level-3; a blank card with the Tier written on it when a supply runs out (the escape components 1.4 uses for tokens) | 3 x 6 + 18 x 3 = **72** | 2 x 6 + 6 x 6 + 36 x 3 = 156 Spell cards; **240** in all (162 and 246 with three starting Spells) | 3 copies are exceeded at level 2 in 4% of realistic matches, 84% of greedy-mirror and all stun-mirror ones; at level 3 in 4.5% of greedy-mirror and 88.5% of stun-mirror ones. The blank keeps it legal, so no rule moves; a playtest box, not a general answer. It does not split into two private libraries (3.9) |
| **2. Separate the Intent from the card** | 126 | 0 with the dial | Part 4 |
| **3. A design limit**: a Tier owned by at most 2 Creatures of a Team | 21 x 2 x 2 = **84** | | A rule: the `boardgame-director`'s (Part 8) |
| **4. Mini cards**, 44 x 63 mm | 126 on 8 sheets | | **The text does not fit**: about 25 characters a line at 8 pt, against 33 for the widest Spell line and 36 for a head line (38 before the Spells were renamed). It fits only at 5 to 6 pt |

**At team size 2**: package cards 21 x 4 = **84**, and every Creature can still own every Tier (6
purchases for a Team of 2 to own a level-3 Tier on both, inside the 20 a Player makes); today's Spell cards
would be 44 x 4 = 176 (45 x 4 = 180 until 2026-10-06); copies by level, 3 x 4 + 18 x 2 = 48.

---

## 6. Proposals from Emmerlaüs

*Emmerlaüs: Duel of Mages*, a card game from Québec: one shared deck, one mage a player, a 5-card hand, one
card a turn, one scaling stat, equipment face up in **capped slots** (a new piece replaces one of its kind),
a defence band on every card, colour-coded categories.

1. **A capped spellbook.** A Creature holds at most K packages; buying another discards one. It answers
   "annoying to remember who has what" with a short cascade, a short dial and fewer copies. **A game-design
   change**, for the `boardgame-director` and the `tabletop-mathematician`. What the box can say: the share of
   realistic-profile Creatures ending with more than K Tiers is **49.5% at K = 3, 26.7% at 4, 9.8% at 5,
   6.7% at 6**. Open: K; whether a level-1 Tier may go while a level-2 Tier it gated stays (suggested yes,
   since prerequisites gate the purchase only, ADR 0056); and whether the bonus goes with it (Base initiative
   "only ever grows", glossary). Settled by an ADR and a measurement at K = 4, 5, 6 of match length, Player
   1's share and `tierUsageShare`, as ADR 0066 was.
2. **A fixed icon row on every Spell**: target, cost, critical threshold, and whether it ignores Defense (only
   Bleed does). A card-face change, not a rule, and what brings a Spell nearer one line (3.6). Icons replace
   words, which components 2.2 makes the maintainer's decision (the `Regen` precedent).
3. **Colour by Tier family.** Three families of seven: **Brute** (Frenzied-Ravager, Ironhide-Colossus,
   Oppressor-Tyrant), **Warped** (Stormborn-Cataclysm, Necrotic-Revenant, Ethereal-Transcendent),
   **Predator** (Deathmarked-Deathstalker, Parasite-Soulreaver, Blighted-Blightweaver). A hue a family makes
   a cascade readable across the table and groups the dial's ring. **Not** the dock's four colours, or "the
   plum card" and "the plum lane" collide; and a glyph each, for greyscale.

**Not borrowed**: damage dice, the 50% resistance roll, and multiplication past the critical's.

---

## 7. Downstream edits

Not made here. One line each.

**The colour dock (decided)**

- components.md 3.2: rewritten: place N colours on, empty the Round's colour, Immune in the next colour; the lane count's rule is "longest Duration + 1".
- components.md 3.1: the `new | 3 | 2 | 1` row becomes four colour lanes; 1.5, the Immune token: "in the next colour", not "in lane `1`".
- components.md 3.6: the coloured spaces and their glyphs; the strip's "conditions count down" becomes "empty this Round's colour".
- rulebook.md Part 2's table, §5.9 (both moves and the example), §6.4, §6.5 (the `new` lane as the first countdown), §7.1 (the Stun row and Stun immunity).
- player-aid.md: the Round, row 8; Condition timing, "Cleanup, in two moves" and the note on `new`.

**The hybrid kit (proposed)**

- components.md Part 1, 1.2, 1.3, 3.1: boards and mats become the Creature card; rails become a clip and dice; the ladder joins the middle.
- components.md 1.7 and 3.3: Energy on a d20 with a +20 chit, still ending at 40; Defense on two dice, same reading, which also answers Part 6, question 16.
- components.md 3.4: the Base initiative ladder.
- components.md 1.5, 3.7, 4.2; rulebook.md §5.3; player-aid.md "Buying a Tier": no pick tokens, and the director's procedure for the supply leak.
- components.md Part 6, question 8: **stale since ADR 0089**: a package card is laid face down now, so it needs a uniform back, unless the dial makes the pick.
- rulebook.md §3.2, steps 2 to 7: the setup.

**The dial (if chosen)**

- components.md 1.1, Part 2, 3.7, 3.8, Part 4: no Spell card; the package card prints both Spells; the dial.
- rulebook.md §5.6, §5.7, §6.1: an Intent is a dial, the reveal reads the cascade, and the director's answer to question 1.
- player-aid.md: the Round, row 6, and "One slot, in order".

---

## 8. Open questions, in priority order

1. **A dial set to a Spell its Creature does not know.** The engine refuses one at submission, so no rule
   says what a table does when one is turned at the reveal. **`boardgame-director`**, before the dial is
   chosen. Settled by a verdict.
2. **Does the table hide which Creature was picked, and a pass?** ADR 0089 hides both; the rulebook's
   procedure shows both. A dial with every living Creature hides them, and changes §5.3; it also needs a word
   on a Player who sets more Tiers than they have picks. **`boardgame-director`.** Settled by a verdict.
3. **Dial or deck, and which dial layout** (4.1, 4.3). **The maintainer.** Settled by a prototype of the
   21-name catalogue dial and the relative `S / 1-10 + A / B` dial beside the same cascade, plus ten
   reveals timed against ten card reveals. Test both the fully-readable half-card cascade and the compact
   20-25 mm summary-band cascade at real table distance.
4. **The Creature card's size** (3.1): 80 x 120 with the starting Spells elsewhere, or a larger card.
   **The maintainer.** Settled by a 1:1 print with real tokens and dice on it.
5. **Dice or clips** for Energy and Defense (3.2, 3.3): dice read fastest and get knocked over; clips survive
   and need an edge. Settled by a playtest on a table that gets bumped.
6. **Copies by level for a playtest box** (Part 5, option 1). **The maintainer**, once question 3 is
   answered: with the dial the saving is 54 cards and 6 sheets.
7. **The Round cap marker**: does a table ever play a cap other than 20? If not, it goes. **The maintainer.**
8. **A capped spellbook** (Part 6, 1). **`boardgame-director` and `tabletop-mathematician`**: an ADR and a
   measurement at K = 4, 5, 6.
9. **A Tier owned by at most 2 Creatures of a Team** (Part 5, option 3). **`boardgame-director`.**
10. **A handling-cost term for the tuner** (2.4). **`tabletop-mathematician`**: hand moves a match per
    Spell, read from the traces as Part 2's table is.
11. **Family colours and the icon row** (Part 6, 2 and 3). **The maintainer**, as a card's words are.
    Settled by a prototype image of one cascade.
