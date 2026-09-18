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

// The badges beside a row's stats: whether the creature is stunned, and the speed it was given once the
// timeline is built (playtest-app.md §3.1). A stun chip in the dock says a condition is running; it does not
// say the creature has lost its speed slot this round, which is the thing a player plans around -- and the
// speed lives in the timeline, which is a strip of six and not a thing read per creature.
//
// Both words are the payload's: `isStunned` is the snapshot's own field, and the speed is the band the engine
// put the slot in.
export function badges(creature, timeline) {
  const found = [];
  if (creature?.isStunned === true) found.push('stunned');
  const slot = (timeline ?? []).find(one => one?.creature === creature?.id);
  if (typeof slot?.speed === 'string' && slot.speed !== '') found.push(slot.speed);
  return found;
}

// A position, not an initiative score. Preserve the engine's tie-breaking and speed order exactly.
export function turnOrder(creature, board) {
  const index = (board?.timeline ?? []).findIndex(slot => slot.creature === creature?.id);
  return index < 0 ? null : index + 1;
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

// Which lane a condition sits in: null when it never counts down at all, `new` while its first countdown is
// still ahead of it, and its remaining rounds once that one has passed.
//
// Permanent is tested first and that order is the whole of it. A permanent condition is applied fresh like any
// other -- the domain sets the flag and leaves the countdown null -- so reading freshness first would put a new
// permanent buff in the `new` countdown lane, say it was counting down, and then move it to `permanent` at the
// next cleanup. The printed board is plainer than that: permanent conditions never enter the dock's lanes at
// all, because they never count down (components.md §3.2).
export function laneOf(condition) {
  if (!Number.isInteger(condition?.remainingRounds)) return null;
  return condition?.isFresh === true ? 'new' : condition.remainingRounds;
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

// Which casters point at a creature, from the casts whose markers are still on the table. The printed board
// has a `Targeted by` row of one box a caster (components.md §3.7), and it is there because reveal-and-target
// walks the whole timeline before anything resolves: six casts' markers are on the table at once, and "who is
// pointing at me" is what a player reads before choosing their own. A caster appearing twice is impossible --
// a cast cannot name one target twice -- so this is a list and not a count.
export function targetedBy(creature, board) {
  return standing(board)
    .filter(action => (action?.targets ?? []).includes(creature))
    .map(action => action?.actor);
}

// The casts whose markers are still on the table: revealed, and not yet resolved. At the table a marker is
// placed when a cast is revealed and taken off when it resolves, so the two are not the same set for the whole
// of the resolution sub-phase -- and the board's `revealedActions` only ever grows, because it is the reveal
// cursor's prefix. Taking the resolve cursor's prefix back out is what keeps a row from naming a caster whose
// cast is already spent. No sub-phase to test for: the resolve cursor is 0 while nothing has resolved and past
// the last slot once everything has, and both of those come out right.
export function standing(board) {
  const resolved = new Set(
    (board?.timeline ?? [])
      .slice(0, Number.isInteger(board?.resolveCursor) ? board.resolveCursor : 0)
      .map(slot => slot?.creature));

  return (board?.revealedActions ?? []).filter(action => !resolved.has(action?.actor));
}

// Keep only public declarations and actions: never consult private intents or infer a spellbook choice.
// Resolution events also recover the previous round after reload, without retaining another seat's state.
export function liveChoice(creature, board, entries) {
  const round = board?.roundNumber;
  const events = (entries ?? []).filter(entry => entry?.event?.kind === 'CombatActionResolved' &&
    entry.event.resolution?.action?.actor === creature?.id);
  const roundOf = entry => entry.event.roundId ?? entry.round;
  const resolved = events.filter(entry => roundOf(entry) === round).at(-1)?.event.resolution;
  const action = (board?.revealedActions ?? []).find(one => one.actor === creature?.id) ?? resolved?.action;
  const intent = (board?.revealedIntents ?? []).find(one => one.actor === creature?.id);
  const index = (board?.timeline ?? []).findIndex(slot => slot.creature === creature?.id);
  const status = !action ? (intent ? 'Choosing targets' : 'Hidden until reveal') : resolved?.fizzled ? 'Fizzled' : resolved?.isCritical ? 'Critical'
    : resolved || (index >= 0 && index < (board?.resolveCursor ?? 0)) ? 'Resolved' : 'Revealed';
  const previous = events.filter(entry => roundOf(entry) === round - 1).at(-1);
  return { round, action, intent, status, previous: previous ? { round: roundOf(previous), action: previous.event.resolution.action } : null };
}
