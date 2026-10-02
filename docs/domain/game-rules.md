# Game rules

This document states what the engine does today and what is decided for next. Ideas belong under
"Open questions". Vocabulary is defined in [glossary.md](glossary.md). The spells themselves are
listed in [spells.md](spells.md).

## Implemented

- Shared kernel: identifiers and stats (phase 1).
- Game resources: spells with the closed effect taxonomy (ADR 0012), targeting specs, talent trees with
  prerequisites, creature definitions, and the data builder that validates and hashes the content (phase 2).
- Creatures and teams (phase 3): a creature spawns from its definition with the base stats and starting spells;
  damage floors health at zero and kills; healing is capped at the maximum; energy is spent only when affordable;
  a dead creature ignores damage, healing, energy, spells, and conditions. Conditions follow the stacking policy
  of their effect and count down when the rules tick them; stun, total defense, and current initiative are
  derived from the active conditions. A team is defeated when none of its creatures is alive.
- Round (phase 4): a forward-only walk through the sub-phases of ADR 0010, ten since `TieOrder` (ADR 0063) and
  `Activation` (ADR 0083). The round stores evolution choices per player, one speed choice per creature, at
  most one tie order per player, one intent per creature (kept per player until revealed), and the targeted
  actions bound in timeline order; one activation cursor tracks combat. Wrong
  sub-phase and duplicate submissions are rule failures; moving past finalization, installing the timeline
  outside turn-order resolution, or a timeline slot without an intent or action are invariant violations.
- Planning rules (phase 5): a package is available to a creature when it does not own it and owns every
  package it requires; an evolution choice must target an own, living creature that has not bought a package
  this round and an available package, within the picks the rule set's schedule gives that round, and the
  sub-phase completes when no player has an effective pick left (capped by how many of their living creatures
  have not bought yet and can buy something, ADR 0066) -- which is immediately, in a round the schedule offers
  no opportunity. A pick is recorded face down and bought only when the sub-phase completes, every pick of
  both players at once (ADR 0089). A speed choice must target an own, living, unstunned creature, and the
  sub-phase completes when every such creature has one. The timeline orders Quick before Standard, initiative
  descending, and rolls off a tie between the two sides on a d20 for the places each side holds; each Player
  then orders their own Creatures among their places in the tie (ADR 0063). A tie order must name every
  Creature of the Player's ties once, and only those. The `RuleSet` value object carries team size, energy per
  round, evolution picks per opportunity, the first evolution round and the interval between opportunities,
  the round cap, and the critical multiplier (ADR 0056).

- Combat rules (phase 6): an intent is valid for an own, living, unstunned creature that knows the spell and can
  afford it; the sub-phase completes when every creature on the timeline has one. Binding targets checks the
  spell's targeting spec fully (count, duplicates, origin, existence, death) against the board as it stands,
  any failure blocks the action, and the action resolves as soon as its targets are confirmed (ADR 0083). A
  creature that cannot act when its slot comes up -- dead, stunned, unable to pay or to cast, or with no legal
  target -- is revealed with no targets and fizzles at no cost, without its owner being asked. The critical roll adds the creature's and the spell's chances and multiplies what the cast
  puts on a target's health now -- damage and a direct heal (ADR 0033) -- floored; damage is then reduced by
  the target's total defense, floor zero. A lasting effect, an effect on the caster and energy are not
  multiplied. The energy cost is spent, instant
  effects apply, lasting effects attach as conditions. At the start of a round living creatures gain the rule
  set's energy and bleeds deal their summed damage, ignoring defense. At cleanup every condition counts one
  round down, except that the first countdown after an application does not count: a one-round stun applied
  in combat stuns the creature for the whole next round. A creature whose stun ends at cleanup is immune to
  stun through the next round, and a stun on a stunned or immune creature is ignored (ADR 0072).

- Match (phase 7): a match seats two players with a roster of creature definitions sized by the rule set and
  starts when the second one joins. Every player action is validated by the rules before anything changes; the
  driver then runs the automatic steps and the progression gates until the round waits on a player again. A
  player may pass their remaining evolution picks. An action or an upkeep that wipes a team ends the round and
  the match on the spot (ADR 0083). Activating the last slot of the timeline runs cleanup and finalization:
  the round cap of ADR 0011 either ends the match or starts the next round. Every step
  raises a domain event, and the match is stamped with the content hash of its game resources (ADR 0009).

- Application (phase 8): commands and queries over the match, projections of what a player sees and can
  decide, agents that decide from those options, and a driver that plays a match to its outcome. No rule lives
  there: the options come from the same gates the aggregate enforces.

- Hosts (phase 9): a console host plays bot versus bot, human versus bot, and batches of seeded matches
  summarized as win rates, rounds, and remaining health. A seed, the content hash, and the players' decisions
  replay a match identically.

## Decided

### Match lifecycle

- A Match waits for two Players and starts when both have joined.
- Each Player controls one Team of creatures built from Creature definitions in the Game resources. Team size
  comes from the Rule set (three in the prototypes).
- The Match ends per the Win condition: a Team defeated, the moment it is (ADR 0083) -- on the action or the
  upkeep that wipes it, with no further slot and no cleanup; both Teams at once is a draw -- or, at the end
  of the round cap, the Team with the highest total remaining Health wins; equality is a draw (ADR 0011).
- A Player may concede at any point of a Match in progress: it ends on the spot, the other Player wins, and
  the Round is left where it was, with no cleanup (ADR 0087).

### Round sequence (ADR 0010)

1. **Start of round**
   1. `EnergyGain`: every living Creature gains the Rule set's energy per round (two in the prototypes).
   2. `OngoingEffects`: energy regeneration Conditions give their Energy, regeneration Conditions heal, then
      bleed Conditions deal their damage, which ignores Defense. Healing goes before the Bleeds (ADR 0019), so
      a Regeneration can carry a Creature through a Bleed that would otherwise have killed it. Energy goes
      first, so a Creature its own Bleed kills that Round still gained it; nothing about Health depends on
      that position (ADR 0020).
2. **Planning**
   1. `Evolution`: each Player may buy **Tiers** -- named packages of Spells -- for living Creatures, up to
      the picks the Rule set's schedule gives that Round: two, at Round 1 and every second Round after it
      (ADR 0056). A Round the schedule skips gives nobody a pick, and the sub-phase completes as it opens
      rather than asking anyone to pass. A Creature may buy a package it does not own and whose prerequisite
      packages it does own, whatever family they belong to: **prerequisites are the only rule, so
      multiclassing is free**. One pick buys the whole package -- every Spell in it at once, a Spell it
      already knows granted without complaint -- and **a Creature buys at most one package an opportunity**
      (ADR 0066): the two picks go to two different Creatures, so no Creature climbs two levels in one Round,
      and a Player down to one living Creature has one pick. A Player may pass their remaining picks. **A purchase raises the Creature's Base initiative by the package's bonus,
      once, for the rest of the Match**: evolving is also how a Creature gets faster, and the bonus belongs to
      the package rather than to any Spell in it. The Current initiative the timeline orders on is that base
      plus the Creature's active initiative buffs and less its active debuffs, floored at zero (ADR 0036), so
      a Condition can still push a Creature forward or pull it back. A refused purchase changes nothing: no
      half-taught package, and no bonus without the Tier that paid for it. **A pick is face down until the
      sub-phase ends** (ADR 0089): it changes nothing on the board, and neither it nor a pass is shown to the
      other Player. When neither Player has a pick left, every package picked that Round is bought at once,
      Player 1's picks and then Player 2's, which is the same thing in any order, since each Creature takes at
      most one. From then on both Players see every Creature's Tiers, since the board shows both Teams whole.
      What a Creature knows is never hidden.
   2. `Speed`: each Player chooses `Quick` or `Standard` for every living, non-stunned Creature. A stunned
      Creature skips the Round entirely: no speed, no slot on the timeline, no intent. Completes when every
      such Creature has a choice. **The choice is a trade: a `Quick` Creature acts before every `Standard`
      one and cannot roll a critical that Round, whatever its own and its Spell's chances add up to.** Without
      that cost the choice decides nothing, since acting earlier is never worse.
   3. `TurnOrderResolution` (automatic): the Combat timeline is built: Quick slots by Initiative descending,
      then Standard slots by Initiative descending. A tie between the two sides is rolled off: every tied
      Creature rolls a d20 and the highest acts first, and when both sides rolled the same number, every
      Creature on that number rolls again, a side's own included; a number only one side rolled stays. That
      decides which places each side holds. A tie held by one side alone rolls nothing. The seat breaks no
      tie (ADR 0063).
   4. `TieOrder`: each Player who holds two places or more in one tie orders their own Creatures among those
      places; the other side's places do not move. Completes when every such Player has; a Round where no
      side holds two places in a tie passes through it without asking anyone.
3. **Combat**
   1. `IntentSelection`: each Player submits, hidden, one Intent per living, non-stunned Creature. An Intent is
      valid if the Creature knows the Spell and can afford its energy cost. Completes when every such Creature
      has an Intent.
   2. `Activation` (ADR 0083): following the timeline, one slot at a time, the Creature's Intent is revealed,
      its owner chooses targets on the board as it stands, and **the action resolves as soon as they are
      confirmed**, before the next slot comes up. Its Spell and targets become public together, never before.
      The targets must satisfy the Spell's targeting spec (origin, scope, count). A Creature that cannot act
      when its slot comes up -- dead, stunned, no longer knowing or able to pay for the Spell, or left with no
      legal target -- is revealed all the same, with no targets, and fizzles at no cost: its owner is not
      asked. A Team wiped by an action ends the Match there. Otherwise the sub-phase completes when the cursor
      reaches the end of the timeline. An action resolves in this order:
      - the energy cost is spent;
      - a critical roll (creature chance plus Spell chance, and zero for a `Quick` Creature) multiplies a
        target's damage and direct heal by
        the Rule set's crit multiplier, floored, and nothing else (ADR 0033);
      - instant effects apply (damage reduced by the target's total Defense, floor zero; heal; energy given,
        or taken up to what the target has). A Creature's total Defense is its base plus its defense buffs,
        which count for at most 10 together however many are active (ADR 0076), less its defense debuffs,
        floored at zero (ADR 0035);
      - lasting effects attach as Conditions per their stacking policy: another one beside the ones already
        there, except a Stun, which is ignored on a Creature already stunned or immune to stun (ADR 0072; it
        restarted the running Stun under ADR 0041). The cast's other effects still land.
4. **End of round**
   1. `Cleanup`: every Condition counts one round down and expires at zero; the first countdown after an
      application does not count. A living Creature whose Stun expires here is immune to stun through the next
      Round, until the next Cleanup (ADR 0072).
   2. `Finalization`: the round cap is checked; either the Match ends or the next Round starts. A Team wiped
      at upkeep ends the Match before `Evolution`, like one wiped in combat.

### Determinism

Given the same Rule set, Game resources (same Content hash), Player decisions, and random seed, a Match
replays identically. Time comes from `TimeProvider`, randomness from `IRandomSource`.

## Open questions

- Board: slot-based teams only, or positions with range and adjacency.
- Effects on the caster (lifesteal, recoil, self-buffs), stat debuffs beyond initiative, and passive
  spells: three gaps the inherited spells fall into ([spells.md](spells.md)).
- Team composition between Matches: fixed roster, drafting, or swapping.
- Sudden death after the round cap instead of a health tiebreak.
- How much of the ML/simulation work (`legacy/DownfallArena/DA.Game.Tests/ml.md`) shapes the event model.
