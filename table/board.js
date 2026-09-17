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

// The condition dock, as the printed board has it: four lanes, `new` then `3`, `2`, `1`, with the permanent
// ones in a group of their own (components.md §3.2, playtest-app.md §3.1).
//
// The `new` lane is not decoration and it is not "recently applied". It is the geometry that makes "the first
// countdown after an application does not count" a thing a player sees instead of a rule they have to recall
// on the round they apply something: a token is placed in `new`, and at Cleanup it moves into the lane its
// Duration names. Grouping by `remainingRounds` alone would put a fresh token beside a counted one of the same
// number and say they go at the same time, which is a round out; folding freshness into the number instead
// would be arithmetic the page did on the player's behalf, and it would dissolve the one lane the cardboard
// has. So freshness picks the lane, and the lane is the answer.
export function conditionDock(conditions) {
  const groups = new Map();
  for (const condition of conditions ?? []) {
    const lane = laneOf(condition);
    if (!groups.has(lane)) groups.set(lane, []);
    groups.get(lane).push(condition);
  }

  return [...lanes(groups)].map(lane => ({ lane, conditions: groups.get(lane) }));
}

// Which lane a condition sits in: `new` while its first countdown is still ahead of it, its remaining rounds
// once that one has passed, and null when it never counts down at all.
export function laneOf(condition) {
  if (condition?.isFresh === true) return 'new';
  return Number.isInteger(condition?.remainingRounds) ? condition.remainingRounds : null;
}

// The printed order, left to right: `new`, then the numbered lanes longest first, then permanent. Only the
// lanes that hold something are drawn -- a phone has no room for four empty boxes a creature.
function* lanes(groups) {
  if (groups.has('new')) yield 'new';
  const numbered = [...groups.keys()].filter(lane => typeof lane === 'number').sort((a, b) => b - a);
  for (const lane of numbered) yield lane;
  if (groups.has(null)) yield null;
}

// What a chip says: the kind the host named it and the number it carries, and nothing this page made up. The
// words are the engine's. Two conditions of one kind and different sizes are two different things to plan
// around, so a chip that printed only the kind would be a chip a player cannot use (playtest-app.md §3.1).
export function chipText(condition) {
  const effect = condition?.effect;
  const kind = typeof effect?.kind === 'string' ? effect.kind : '';
  if (kind === '') return '';
  const amount = amountOf(effect);
  return amount === null ? kind : `${kind} ${amount}`;
}

// Which cast put it there, so a player can tell two identical chips apart and knows whose upkeep it is
// (ADR 0027). The source is the pair -- the caster and the cast -- and one creature can put two of one kind on
// one target from two different spells, so the caster alone does not tell them apart. Empty when nothing named
// a source, which is every condition a cast did not apply.
export function chipSource(condition, cards) {
  const source = condition?.source;
  if (source === null || source === undefined) return '';

  const caster = source.caster === null || source.caster === undefined ? '' : `from ${source.caster}`;
  const spell = source.spell === null || source.spell === undefined
    ? ''
    : cards?.get?.(source.spell)?.name ?? source.spell;
  return [caster, spell].filter(part => part !== '').join(' · ');
}

// The one number a lasting effect carries, whatever it is called. Read off the payload rather than from a list
// of effects here: a per-round effect names it `amountPerRound` and a flat one `amount`, and an effect that
// carries no number at all has neither, which is a chip that is just its kind.
function amountOf(effect) {
  for (const field of ['amountPerRound', 'amount']) {
    if (Number.isInteger(effect?.[field])) return effect[field];
  }

  return null;
}

function value(stat) {
  return Number.isFinite(Number(stat)) ? Number(stat) : 0;
}

// What has already been revealed this round: who is acting, with what, and on whom. An action goes face up the
// moment the reveal cursor reaches its slot, and it stays face up -- which matters most in hotseat play, where
// the next player picks their targets on a screen that has to show them the action before theirs. Without this
// the reveal exists only in the payload: the board draws the creatures, and the feed prints the event's kind.
export function revealedText(action, cards) {
  const actor = action?.actor ?? '';
  const spell = cards?.get?.(action?.spell)?.name ?? action?.spell ?? '';
  const targets = (action?.targets ?? []).join(', ');
  return targets === '' ? `${actor}: ${spell}` : `${actor}: ${spell} → ${targets}`;
}
