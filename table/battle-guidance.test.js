import { test } from 'node:test';
import assert from 'node:assert/strict';
import { battleGuidance, helpPreferences, HELP_PREFERENCE_KEY, waitingOn } from './battle-guidance.js';
const catalogue = { rules: { roundCap: 17, combatDeclaration: 'AtActivation' } };
const view = (kind = 'Evolution', extras = {}) => ({ board: { roundNumber: 1, phase: 'Planning', subPhase: 'Evolution', slot: 'Player1', nextEvolutionRound: 1, allies: [{ id: 1, name: 'First' }], enemies: [], timeline: [] }, waitingFor: kind, waitingAsked: 12, waitingCreature: 1,
  options: { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['root'] }] } }, ...extras });
const text = model => JSON.stringify(model);
test('mutation guidance uses the team offer and explains the actual pass semantics', () => {
  const g = battleGuidance(view(), catalogue);
  assert.equal(g.topic, 'mutation'); assert.match(g.objective, /2 team picks left/);
  assert.equal(g.round, 'Round 1 of 17'); assert.equal(g.next, 'Mutation opportunity this round.');
  for (const word of ['spells or a passive', 'plus initiative', 'shared across your team', 'all your unused picks']) assert.ok(text(g).includes(word));
});
test('depleted picks and empty offers never tell the player to buy an unavailable mutation', () => {
  const depleted = battleGuidance(view('Evolution', { options: { evolution: { remainingPicks: 0 } } }), catalogue);
  assert.equal(depleted.topic, null); assert.match(depleted.objective, /No team picks remain/);
  assert.match(battleGuidance(view('Evolution', { options: { evolution: { remainingPicks: 1, creatures: [{ creature: 1, availableTiers: [] }] } } }), catalogue).objective, /No mutations offered\. Open Mutation tree to pass/);
});
test('the next mutation comes from the board on an even round, not a client schedule', () => {
  const input = view(null); input.board.roundNumber = 2; input.board.nextEvolutionRound = 6;
  assert.equal(battleGuidance(input, catalogue).next, 'Next mutation opportunity: round 6.');
  delete input.board.nextEvolutionRound; assert.equal(battleGuidance(input, catalogue).next, '');
});
test('waiting guards include an empty waiting.seats and suppress prepared first-round facts', () => {
  const g = battleGuidance(view('Evolution', { waiting: { seats: [] } }), catalogue);
  assert.equal(g.topic, null); assert.equal(g.round, 'Match not started'); assert.equal(g.phase, '');
  assert.match(g.objective, /Waiting for players/); assert.equal(g.next, ''); assert.equal(g.context, '');
});
test('tempos, ties and both declaration modes explain their actual current question', () => {
  for (const [kind, topic, word] of [['Speed', 'tempo', 'only Precision can crit'], ['TieOrder', 'ties', 'within your own tied groups'], ['Action', 'action', 'No hidden spell declaration'], ['Intent', 'intent', 'choose targets later'], ['Target', 'target', 'already declared']]) {
    const g = battleGuidance(view(kind), { rules: { combatDeclaration: kind === 'Intent' || kind === 'Target' ? 'HiddenIntent' : 'AtActivation' } });
    assert.equal(g.topic, topic); assert.ok(text(g).includes(word), kind);
  }
});
test('a catalogue whose rules name no declaration plays hidden intents, the way the stamp is written', () => {
  const served = { rules: { roundCap: 12 } };
  for (const kind of ['Evolution', 'Speed', 'TieOrder', null]) assert.match(text(battleGuidance(view(kind), served)), /declare hidden spells/, String(kind));
  assert.doesNotMatch(text(battleGuidance(view(), served)), /does not supply the declaration mode/);
  assert.match(text(battleGuidance(view(), { rules: { combatDeclaration: 'AtActivation' } })), /when your creature acts/);
});
test('without any rules the mode is explicitly unknown, while a spell question can explain itself', () => {
  assert.match(text(battleGuidance(view(), null)), /does not supply the declaration mode/);
  assert.match(text(battleGuidance(view('Intent'), null)), /declare hidden spells/);
  assert.doesNotMatch(text(battleGuidance(view('Action'), null)), /declare hidden spells/);
});
test('the objective names the creature the way the scene does', () => {
  assert.equal(battleGuidance(view('Speed'), catalogue).objective, 'Draft every eligible creature’s tempo, then confirm the team.');
  assert.equal(battleGuidance(view('Speed'), catalogue).context, 'Command · First');
  const twins = view('Action'); twins.board.allies = [{ id: 1, name: 'Twin' }, { id: 2, name: 'Twin' }];
  assert.match(battleGuidance(twins, catalogue).objective, /^Twin · 1: choose a spell/);
  assert.match(battleGuidance(view('Target', { waitingCreature: 9 }), catalogue, { targeting: true }).objective, /^Creature 9: select targets/);
});
test('opponent turns use public order without reading unrevealed plans', () => {
  const input = view(null); input.board.subPhase = 'Activation'; input.board.phase = 'Combat';
  input.board.timeline = [{ creature: 4, owner: 'Player2' }]; input.board.activationCursor = 0;
  input.board.intents = [{ spell: 'SECRET' }]; input.board.evolutionChoices = [{ tier: 'SECRET' }];
  const g = battleGuidance(input, catalogue);
  assert.equal(g.topic, null); assert.match(g.objective, /Waiting for your opponent: Creature 4 is choosing its action/); assert.doesNotMatch(text(g), /SECRET/);
});
test('historical playback never carries the next live purchase budget or round into its help', () => {
  const g = battleGuidance(view(), catalogue, { historical: true });
  assert.equal(g.topic, null); assert.equal(g.next, ''); assert.equal(g.round, 'Recorded results');
  assert.doesNotMatch(text(g), /2 team picks left|Round 1 of/);
});
test('accepted, stale, completed and bot-driven states do not invite another decision', () => {
  assert.match(battleGuidance(view(), catalogue, { accepted: true }).objective, /Choice sent/);
  assert.match(battleGuidance(view(), catalogue, { online: false }).objective, /Connection interrupted/);
  const over = battleGuidance(view('Evolution', { over: true }), catalogue); assert.equal(over.topic, null); assert.match(over.objective, /Match complete/);
  assert.equal(battleGuidance(view('Evolution', { playedByBot: true }), catalogue).topic, null);
});
test('a covered target draft keeps targeting guidance without creating a new declaration', () => {
  const g = battleGuidance(view('Action'), catalogue, { targeting: true });
  assert.match(g.objective, /select targets, then Confirm cast/); assert.equal(g.topic, 'action');
});
test('access loss clears every seat-bound guide field', () => {
  const g = battleGuidance(null, catalogue); assert.equal(g.available, false);
  for (const key of ['round', 'context', 'next', 'title', 'phase']) assert.equal(g[key], '');
  assert.deepEqual(g.lines, []); assert.deepEqual(g.flow, []);
});
function store(initial) {
  const writes = []; let data = initial;
  return { writes, getItem: () => data, setItem: (key, value) => { assert.equal(key, HELP_PREFERENCE_KEY); writes.push(value); data = value; } };
}
test('tips are once per subject in a game, across creatures, rounds and reloads', () => {
  const storage = store(null), p = helpPreferences(storage); p.setGame('table-a');
  assert.equal(p.shouldShow('mutation'), true); p.markSeen('mutation'); p.markSeen('mutation');
  assert.equal(p.shouldShow('mutation'), false); assert.equal(p.shouldShow('tempo'), true);
  const reloaded = helpPreferences(storage); reloaded.setGame('table-a');
  assert.equal(storage.writes.length, 1); assert.equal(reloaded.shouldShow('mutation'), false);
});
test('every game teaches its phases again', () => {
  const storage = store(null), p = helpPreferences(storage); p.setGame('table-a'); p.markSeen('mutation');

  p.setGame('table-b');

  assert.equal(p.shouldShow('mutation'), true);
  p.setGame('table-a'); assert.equal(p.shouldShow('mutation'), false);
});
test('a browser that read every tip once before still mutes them if it chose to, and is taught again if not', () => {
  const read = store(JSON.stringify({ version: 1, muted: false, seen: ['mutation', 'tempo'] }));
  const muted = store(JSON.stringify({ version: 1, muted: true, seen: [] }));

  const p = helpPreferences(read); p.setGame('table-a');

  assert.equal(p.shouldShow('mutation'), true);
  assert.equal(helpPreferences(muted).muted(), true);
});
test('only the latest games keep the tips they read', () => {
  const storage = store(null), p = helpPreferences(storage);
  for (let game = 0; game < 25; game++) { p.setGame(`table-${game}`); p.markSeen('tempo'); }

  const kept = JSON.parse(storage.writes.at(-1)).games;

  assert.equal(kept.length, 20); assert.equal(kept[0].game, 'table-24');
});
test('muting only instructional tips leaves reopening Help independent of the preference', () => {
  const storage = store(null), p = helpPreferences(storage); p.setMuted(true);
  assert.equal(p.shouldShow('tempo'), false); assert.equal(helpPreferences(storage).muted(), true);
  assert.equal(battleGuidance(view('Speed'), catalogue).topic, 'tempo');
  p.setMuted(false); assert.equal(p.shouldShow('tempo'), true);
});
test('stored values are bounded generic preferences, never arbitrary identities or payloads', () => {
  const storage = store(JSON.stringify({ version: 2, muted: 'true', token: 'SECRET',
    games: [{ game: 'table-a', seen: ['SECRET', 'mutation', 'mutation'], token: 'SECRET' }, { game: '../x', seen: ['tempo'] }] }));
  const p = helpPreferences(storage); p.setGame('table-a'); p.markSeen('tempo'); p.markSeen('SECRET');
  assert.deepEqual(JSON.parse(storage.writes.at(-1)), { version: 2, muted: false, games: [{ game: 'table-a', seen: ['mutation', 'tempo'] }] });
});
test('blocked, malformed and older preference versions keep Help usable in memory', () => {
  for (const storage of [null, store('broken'), store('{"version":0,"muted":true}'), { getItem() { throw Error(); }, setItem() { throw Error(); } }]) {
    const p = helpPreferences(storage); assert.equal(p.shouldShow('action'), true); p.markSeen('action'); assert.equal(p.shouldShow('action'), false); p.setMuted(true); assert.equal(p.muted(), true);
  }
});

// A live round in combat where the opponent's creature 4 holds the slot and this seat's creature 2 comes later.
const combat = (cursor = 1, extras = {}) => ({ board: { roundNumber: 2, phase: 'Combat', subPhase: 'Activation', slot: 'Player1', activationCursor: cursor,
  allies: [{ id: 1, name: 'Creature 1' }, { id: 2, name: 'Creature 2' }], enemies: [{ id: 4, name: 'Creature 4' }, { id: 5, name: 'Creature 5' }],
  timeline: [{ creature: 1, owner: 'Player1' }, { creature: 4, owner: 'Player2' }, { creature: 5, owner: 'Player2' }, { creature: 2, owner: 'Player1' }] },
waitingFor: null, waitingAsked: null, ...extras });

test('while the opponent\'s creature takes its slot, the seat is told whom it waits for, on which creature, and its own next', () => {
  assert.deepEqual(waitingOn(combat(), { opponent: 'bastien' }), { kind: 'action', eyebrow: "Opponent's turn", title: 'Waiting for bastien',
    detail: 'Creature 4 is choosing its action · your Creature 2 plays next', creature: 4 });
  assert.equal(waitingOn(combat(), { opponent: 'Lookahead', bot: true }).title, 'Lookahead is thinking');
  assert.equal(waitingOn(combat()).title, 'Waiting for your opponent', 'a seat whose opponent has no name yet');
  const last = combat(2, { board: { ...combat().board, activationCursor: 2, timeline: combat().board.timeline.slice(0, 3) } });
  assert.match(waitingOn(last).detail, /your team has acted this round$/);
});

test('nothing is waited on during this seat\'s own slot, its own question or after the end; before the start, the waiting room', () => {
  assert.equal(waitingOn(combat(0)), null);
  // Before the match the card is the waiting room's, whoever the opponent will be.
  const room = waitingOn({ ...combat(), waiting: { seats: [] } });
  assert.equal(room.kind, 'seats'); assert.equal(room.title, 'Waiting for your opponent');
  assert.equal(waitingOn({ ...combat(), waiting: { seats: [] } }, { opponent: 'bastien' }).title, 'Waiting for bastien');
  assert.equal(waitingOn(combat(1, { waitingFor: 'Action', waitingAsked: 9 })), null);
  assert.equal(waitingOn(combat(1, { waiting: true })).kind, 'seats');
  assert.equal(waitingOn(combat(1, { over: true })), null);
  assert.equal(waitingOn({ ...combat(), board: { ...combat().board, subPhase: 'Cleanup' } }), null, 'the host resolves cleanup by itself');
});

test('once this seat\'s command is sent, it is told what the other side is still choosing', () => {
  const planning = view('Evolution');

  assert.equal(waitingOn(planning), null, 'its own mutation question is still open');
  assert.deepEqual(waitingOn(planning, { accepted: true, opponent: 'bastien' }), { kind: 'choice', eyebrow: 'Your choice is in', title: 'Waiting for bastien', detail: 'Choosing their mutations', creature: null });
  assert.equal(waitingOn({ ...view('Speed'), waitingFor: null, waitingAsked: null, board: { ...planning.board, subPhase: 'Speed' } }).detail, 'Setting their tempos');
});

test('the dock says whom it waits for in plain words instead of sending the player to History', () => {
  assert.equal(battleGuidance(combat(), catalogue).objective, 'Waiting for your opponent: Creature 4 is choosing its action.');
});

