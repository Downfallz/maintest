// A creature board, as the components.md one is: numbers, not rails.
//
// The printed board has a health number and a dial, not a twenty-one-cell track, because a track is what a
// phone has no room for and a hand has no patience for (components.md §3). Everything here is arithmetic over
// the snapshot the host served; nothing names a creature, a spell or an effect, which is what keeps the page
// free of content (docs/tabletop/app-roadmap.md, stage 3).

export function healthText(creature) {
  return `${value(creature?.health)}/${value(creature?.maxHealth)}`;
}

// How full the bar is, 0 to 1. A creature at zero has an empty bar rather than no bar: dead is a state to see.
export function healthShare(creature) {
  const max = Number(creature?.maxHealth);
  const health = Number(creature?.health);
  if (!Number.isFinite(max) || max <= 0 || !Number.isFinite(health)) return 0;
  return Math.max(0, Math.min(1, health / max));
}

// The stats a board carries beside its health, in the order the printed board reads them.
export function statPairs(creature) {
  return [
    ['energy', value(creature?.energy)],
    ['defense', value(creature?.totalDefense)],
    ['initiative', value(creature?.currentInitiative)],
  ];
}

// The condition dock: chips grouped by what is left of them, soonest first, with the permanent ones in their
// own group at the end. Grouping by round is what makes a dock readable at a glance -- "these three go at the
// end of this round" is the question a player actually asks of it.
export function conditionDock(conditions) {
  const groups = new Map();
  for (const condition of conditions ?? []) {
    const rounds = Number.isInteger(condition?.remainingRounds) ? condition.remainingRounds : null;
    if (!groups.has(rounds)) groups.set(rounds, []);
    groups.get(rounds).push(condition);
  }

  const counted = [...groups.keys()].filter(rounds => rounds !== null).sort((a, b) => a - b);
  const ordered = groups.has(null) ? [...counted, null] : counted;
  return ordered.map(rounds => ({ rounds, conditions: groups.get(rounds) }));
}

// What a chip says: the kind the host named it, and nothing this page made up. The words are the engine's.
export function chipText(condition) {
  const kind = condition?.effect?.kind;
  return typeof kind === 'string' ? kind : '';
}

function value(stat) {
  return Number.isFinite(Number(stat)) ? Number(stat) : 0;
}
