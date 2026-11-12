import { creatureForm } from './battle-art.js';
import { creatureLabel } from './battle-live.js';

// A finished match's battle report: what each creature did, read off the seat's whole public feed and its final
// board. Its damage counts the hits it landed and the bleeds it put on enemies, healing the heals it cast and the
// regenerations it left, both as the board took them (an overkill or an overheal counts what landed). What a cast
// did to its own caster is a cost, not something it dealt. Blocked is what this creature's defense took from the
// hits it met, read off what the rules computed: a hit its defense took whole dealt nothing and is not among what
// landed. A kill is credited to the creature whose cast left the target dead, or, for a creature that died at the
// upkeep, to whoever's bleed on it was the largest share of the last tick.

const STATS = ['dealt', 'bleed', 'healed', 'regen', 'taken', 'blocked', 'kills', 'casts', 'crits'];

// Named as every other surface of the battle names it ("Creature 4", as on the board), never by the host's raw name.
function blank(creature, side, packages, board) {
  const row = { id: creature.id, name: creatureLabel(board, creature.id), side, alive: creature.isAlive !== false,
    form: creatureForm(creature, packages), owner: creature.owner };
  for (const stat of STATS) row[stat] = 0;
  return row;
}

const add = (rows, id, stat, amount) => { const row = rows.get(id); if (row && amount > 0) row[stat] += amount; };

// Who the largest share of a tick came from, the first named on a tie.
function largest(shares) {
  let best = null;
  for (const share of shares ?? []) if (!best || share.amount > best.amount) best = share;
  return best?.source?.caster ?? null;
}

export function matchScoreboard(feed, board, packages = new Map()) {
  if (!board || !Array.isArray(feed)) return null;
  const rows = new Map();
  for (const creature of board.allies ?? []) rows.set(creature.id, blank(creature, 'ally', packages, board));
  for (const creature of board.enemies ?? []) rows.set(creature.id, blank(creature, 'enemy', packages, board));
  const alive = new Map([...rows.keys()].map(id => [id, true]));
  const lastBleed = new Map();
  let resolved = 0;
  const died = (id, killer) => {
    alive.set(id, false);
    const victim = rows.get(id), credited = rows.get(killer);
    if (victim && credited && credited.side !== victim.side) credited.kills++;
  };
  // A creature seen dead that was last seen alive died since: at an upkeep, where only a bleed takes health.
  const notice = (creature) => {
    if (creature?.isAlive === false && alive.get(creature.id)) died(creature.id, largest(lastBleed.get(creature.id)));
  };

  for (const entry of [...feed].sort((a, b) => (a.sequence ?? 0) - (b.sequence ?? 0))) {
    const event = entry?.event;
    if (event?.kind === 'CombatActionResolved') {
      const resolution = event.resolution ?? {}, actor = resolution.action?.actor;
      for (const creature of event.frame?.before ?? []) notice(creature);
      if (!resolution.fizzled) { resolved++; add(rows, actor, 'casts', 1); if (resolution.isCritical) add(rows, actor, 'crits', 1); }
      for (const outcome of resolution.outcomes ?? []) if (outcome.kind === 'DamageOutcome') add(rows, outcome.target, 'blocked', outcome.blocked ?? 0);
      for (const outcome of event.appliedOutcomes ?? []) {
        if (outcome.kind === 'DamageOutcome') {
          add(rows, outcome.target, 'taken', outcome.amount);
          if (!outcome.onCaster) add(rows, actor, 'dealt', outcome.amount);
        } else if (outcome.kind === 'HealOutcome' && !outcome.onCaster) add(rows, actor, 'healed', outcome.amount);
      }
      for (const creature of event.frame?.after ?? []) if (creature.isAlive === false && alive.get(creature.id)) died(creature.id, actor);
    } else if (event?.kind === 'OngoingEffectsApplied') {
      for (const tick of event.regenerationTicks ?? []) for (const share of tick.shares ?? []) {
        add(rows, share.source?.caster, 'healed', share.amount); add(rows, share.source?.caster, 'regen', share.amount);
      }
      for (const tick of event.bleedTicks ?? []) {
        add(rows, tick.creature, 'taken', tick.damage);
        lastBleed.set(tick.creature, tick.shares);
        // A bleed a spell puts on its own caster is a cost, as its direct recoil is: taken, never dealt.
        for (const share of (tick.shares ?? []).filter(one => one.source?.caster !== tick.creature)) {
          add(rows, share.source?.caster, 'dealt', share.amount); add(rows, share.source?.caster, 'bleed', share.amount);
        }
      }
    }
  }
  for (const creature of [...(board.allies ?? []), ...(board.enemies ?? [])]) notice(creature);

  const all = [...rows.values()];
  const best = (stat) => Math.max(0, ...all.map(row => row[stat]));
  // The creature of the match: the most damage dealt, then the most kills, then the most healing.
  const mvp = resolved ? [...all].sort((a, b) => b.dealt - a.dealt || b.kills - a.kills || b.healed - a.healed)[0] : null;
  const sum = (side, stat) => all.filter(row => row.side === side).reduce((total, row) => total + row[stat], 0);
  const sides = ['ally', 'enemy'].map(side => ({ side, creatures: all.filter(row => row.side === side),
    totals: Object.fromEntries(STATS.map(stat => [stat, sum(side, stat)])) }));
  return { sides, mvp: mvp && (mvp.dealt || mvp.kills || mvp.healed) ? mvp.id : null, top: Object.fromEntries(STATS.map(stat => [stat, best(stat)])), resolved };
}
